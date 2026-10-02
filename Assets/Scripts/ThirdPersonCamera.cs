using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ThirdPersonCamera : MonoBehaviour
{
    [Header("Fixed low-angle composition")]
    [SerializeField, Range(20f, 30f)] private float pitch = 25f;
    [SerializeField] private float yaw = 35f;
    [SerializeField] private float cameraHeight = 1.4f;
    [SerializeField] private float horizontalDistance = 3.6f;
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Limited look controls")]
    [SerializeField] private float dragSensitivity = 0.12f;
    [SerializeField] private float yawLimit = 25f;
    [SerializeField] private float pitchLimit = 10f;
    [SerializeField] private float returnSpeed = 5f;
    [SerializeField] private Vector2 distanceLimits = new Vector2(2.5f, 4.5f);
    [SerializeField] private float zoomSensitivity = 0.35f;

    [Header("Room follow limits")]
    [SerializeField] private Vector2 followBoundsX = new Vector2(-4.45f, 4.45f);
    [SerializeField] private Vector2 followBoundsZ = new Vector2(-3.05f, 3.05f);
    [SerializeField] private Vector2 cameraBoundsX = new Vector2(-4.72f, 4.72f);
    [SerializeField] private Vector2 cameraBoundsZ = new Vector2(-3.32f, 3.32f);

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.18f;
    [SerializeField] private float collisionPadding = 0.10f;
    [SerializeField] private float minimumDistance = 0.65f;
    [SerializeField] private float rayOriginHeight = 0.28f;
    [SerializeField] private float stablePivotHeight = 0.58f;
    [SerializeField] private float nearFurnitureIgnoreDistance = 0.85f;

    [SerializeField] private Transform target;

    private float yawOffset;
    private float pitchOffset;
    private float targetDistance;
    private Vector2 previousPointer;
    private float previousPinchDistance;
    private bool dragging;

    private float CurrentYaw => yaw + yawOffset;
    private float CurrentPitch => pitch + pitchOffset;
    private Quaternion FixedRotation => Quaternion.Euler(CurrentPitch, CurrentYaw, 0f);

    public void Initialize(Transform followTarget)
    {
        target = followTarget;
        targetDistance = Mathf.Clamp(horizontalDistance, distanceLimits.x, distanceLimits.y);
        if (target == null)
            return;

        transform.position = ResolveCollision(GetRayOrigin(), GetDesiredPosition());
        transform.rotation = FixedRotation;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        UpdateLookInput();

        Vector3 rayOrigin = GetRayOrigin();
        Vector3 desiredPosition = KeepInsideRoom(ResolveCollision(rayOrigin, GetDesiredPosition()));
        float lerpAmount = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, lerpAmount);

        // Resolve again after smoothing so a fast-moving target cannot pull the
        // camera through a shelf or table between the old and desired positions.
        transform.position = KeepInsideRoom(ResolveCollision(rayOrigin, smoothedPosition));
        transform.rotation = FixedRotation;
    }

    private void UpdateLookInput()
    {
        if (!GameFlowUI.GameplayInputEnabled)
            return;

        bool activelyDragging = false;
        Touchscreen touchscreen = Touchscreen.current;
        bool touch0 = touchscreen != null && touchscreen.touches.Count > 0 && touchscreen.touches[0].isInProgress;
        bool touch1 = touchscreen != null && touchscreen.touches.Count > 1 && touchscreen.touches[1].isInProgress;
        int activeTouches = touch0 ? (touch1 ? 2 : 1) : 0;
        if (activeTouches >= 2)
        {
            Vector2 a = touchscreen.touches[0].position.ReadValue();
            Vector2 b = touchscreen.touches[1].position.ReadValue();
            float pinch = Vector2.Distance(a, b);
            if (previousPinchDistance > 0f)
                targetDistance = Mathf.Clamp(targetDistance - (pinch - previousPinchDistance) * 0.006f,
                    distanceLimits.x, distanceLimits.y);
            previousPinchDistance = pinch;
            dragging = false;
        }
        else
        {
            previousPinchDistance = 0f;
            Mouse mouse = Mouse.current;
            bool pointerHeld = (mouse != null && mouse.leftButton.isPressed) || activeTouches == 1;
            Vector2 pointer = activeTouches == 1
                ? touchscreen.touches[0].position.ReadValue()
                : mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            if (pointerHeld)
            {
                if (dragging)
                {
                    Vector2 delta = pointer - previousPointer;
                    yawOffset = Mathf.Clamp(yawOffset + delta.x * dragSensitivity, -yawLimit, yawLimit);
                    pitchOffset = Mathf.Clamp(pitchOffset - delta.y * dragSensitivity, -pitchLimit, pitchLimit);
                }
                previousPointer = pointer;
                dragging = true;
                activelyDragging = true;
            }
            else dragging = false;
        }

        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
        if (Mathf.Abs(wheel) > 0.01f)
            targetDistance = Mathf.Clamp(targetDistance - wheel * zoomSensitivity, distanceLimits.x, distanceLimits.y);

        if (!activelyDragging && activeTouches < 2)
        {
            float returnT = 1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime);
            yawOffset = Mathf.Lerp(yawOffset, 0f, returnT);
            pitchOffset = Mathf.Lerp(pitchOffset, 0f, returnT);
        }
        horizontalDistance = Mathf.Lerp(horizontalDistance, targetDistance,
            1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
    }

    private Vector3 GetDesiredPosition()
    {
        Vector3 clampedFollowPoint = GetClampedFollowPoint();

        Vector3 horizontalOffset = Quaternion.Euler(0f, CurrentYaw, 0f) *
                                   new Vector3(0f, 0f, -horizontalDistance);
        return KeepInsideRoom(clampedFollowPoint + horizontalOffset + Vector3.up * cameraHeight);
    }

    private Vector3 GetRayOrigin()
    {
        // The camera specification follows the cat on XZ only. Using target.position.y
        // here made the collision pivot jump onto chairs/tables with the cat, which
        // pulled the camera to its minimum distance and produced extreme close-ups.
        return GetClampedFollowPoint() + Vector3.up * Mathf.Max(rayOriginHeight, stablePivotHeight);
    }

    private Vector3 GetClampedFollowPoint()
    {
        Vector3 catPosition = target.position;
        return new Vector3(
            Mathf.Clamp(catPosition.x, followBoundsX.x, followBoundsX.y),
            0f,
            Mathf.Clamp(catPosition.z, followBoundsZ.x, followBoundsZ.y));
    }

    private Vector3 KeepInsideRoom(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, cameraBoundsX.x, cameraBoundsX.y);
        position.z = Mathf.Clamp(position.z, cameraBoundsZ.x, cameraBoundsZ.y);
        return position;
    }

    private Vector3 ResolveCollision(Vector3 rayOrigin, Vector3 desiredPosition)
    {
        Vector3 offset = desiredPosition - rayOrigin;
        float desiredDistance = offset.magnitude;
        if (desiredDistance <= 0.001f)
            return desiredPosition;

        Vector3 direction = offset / desiredDistance;
        RaycastHit[] hits = Physics.SphereCastAll(
            rayOrigin, collisionRadius, direction, desiredDistance,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        float nearestDistance = desiredDistance;
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == null || hitTransform == target || hitTransform.IsChildOf(target))
                continue;

            // When the cat is standing on a chair/table, the collision pivot can
            // begin inside that furniture. Treating the zero-distance overlap as
            // an occluder caused the camera to snap to 65 cm and fill the screen
            // with the cat. Nearby furniture belongs to the subject cluster; room
            // boundaries still block the camera at every distance.
            if (hit.distance < nearFurnitureIgnoreDistance && !IsRoomBoundary(hitTransform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, hit.distance);
        }

        if (nearestDistance >= desiredDistance)
            return desiredPosition;

        float safeDistance = Mathf.Max(minimumDistance, nearestDistance - collisionPadding);
        return rayOrigin + direction * safeDistance;
    }

    private static bool IsRoomBoundary(Transform hitTransform)
    {
        for (Transform cursor = hitTransform; cursor != null; cursor = cursor.parent)
        {
            string name = cursor.name;
            if (name.Contains("Wall", System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Room Shell", System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Door", System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Window", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
