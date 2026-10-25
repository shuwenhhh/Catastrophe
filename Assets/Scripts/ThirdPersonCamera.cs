using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Position follows the player. World-space orbit belongs only to the player:
// character turns, idle, jumping and collision never add a yaw command.
[DefaultExecutionOrder(200)]
public sealed class ThirdPersonCamera : MonoBehaviour
{
    public enum FollowView { Main, Front, Back }
    public FollowView CurrentView { get; private set; } = FollowView.Main;

    [Header("Orbit composition")]
    [SerializeField, Range(5f, 70f)] private float pitch = 40f;
    [SerializeField] private float yaw;
    [SerializeField] private float horizontalDistance = 2.5f;
    [SerializeField] private float pivotHeight = .06f;
    [SerializeField, Range(35f, 70f)] private float fieldOfView = 55f;
    [SerializeField] private float smoothSpeed = 12f;
    [SerializeField] private float lookSensitivity = 1f;
    [SerializeField] private Vector2 distanceLimits = new(2.5f, 4.5f);

    [Header("Safe framing")]
    [SerializeField] private float collisionRadius = .14f;
    [SerializeField] private float collisionPadding = .08f;
    [SerializeField] private Transform target;

    private Camera lens;
    private float viewYaw, yawOffset, targetYawOffset;
    private float targetPitch, smoothedPitch, targetDistance, resolvedDistance;
    private Vector3 focus;
    private Vector2 previousPointer;
    private bool dragging, gameplayInputLastFrame;
    private int orbitTouch = -1;
    private readonly RaycastHit[] collisionHits = new RaycastHit[128];
    private readonly Collider[] overlaps = new Collider[128];
    private readonly List<RaycastResult> uiHits = new();

    public float OrbitYaw => viewYaw + yawOffset;
    public float OrbitPitch => smoothedPitch;
    public Quaternion PlanarRotation => Quaternion.Euler(0f, OrbitYaw, 0f);
    public Transform FollowTarget => target;
    public float FramingDistance => Vector3.Distance(transform.position, focus);
    public void RestoreView(float savedYaw, float savedPitch)
    {
        viewYaw = savedYaw; yawOffset = targetYawOffset = 0f;
        targetPitch = smoothedPitch = Mathf.Clamp(savedPitch, 5f, 70f);
        focus = target.position + Vector3.up * pivotHeight;
        PositionCamera(0f, true);
    }

    public void Initialize(Transform followTarget)
    {
        target = followTarget;
        lens = GetComponent<Camera>();
        CurrentView = FollowView.Main;
        viewYaw = target ? target.eulerAngles.y : yaw;
        yawOffset = targetYawOffset = 0f;
        targetPitch = smoothedPitch = Mathf.Clamp(pitch, 5f, 70f);
        horizontalDistance = targetDistance = Mathf.Clamp(horizontalDistance, distanceLimits.x, distanceLimits.y);
        resolvedDistance = horizontalDistance;
        dragging = gameplayInputLastFrame = false;
        orbitTouch = -1;
        if (lens)
        {
            lens.fieldOfView = fieldOfView;
            lens.nearClipPlane = .035f;
        }
        if (!target) return;
        focus = target.position + Vector3.up * pivotHeight;
        PositionCamera(0f, true);
    }

    private void LateUpdate()
    {
        if (!target) return;
        UpdateLookInput();
        float dt = Time.deltaTime;
        float lookT = 1f - Mathf.Exp(-18f * dt);
        yawOffset = Mathf.LerpAngle(yawOffset, targetYawOffset, lookT);
        smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, lookT);
        horizontalDistance = Mathf.Lerp(horizontalDistance, targetDistance, 1f - Mathf.Exp(-8f * dt));
        focus = Vector3.Lerp(focus, target.position + Vector3.up * pivotHeight,
            1f - Mathf.Exp(-smoothSpeed * dt));
        PositionCamera(dt, false);
    }

    private void UpdateLookInput()
    {
        if (!GameFlowUI.GameplayInputEnabled)
        {
            dragging = gameplayInputLastFrame = false;
            orbitTouch = -1;
            return;
        }
        if (!gameplayInputLastFrame)
        {
            gameplayInputLastFrame = true;
            dragging = false;
            return;
        }

        bool touchPresent = false, pointerHeld = false;
        Vector2 pointer = Vector2.zero;
        var screen = Touchscreen.current;
        if (screen != null)
        {
            foreach (var touch in screen.touches)
            {
                if (!touch.isInProgress) continue;
                touchPresent = true;
                int id = touch.touchId.ReadValue();
                if (orbitTouch != id) continue;
                pointerHeld = true;
                pointer = touch.position.ReadValue();
                break;
            }
            if (!pointerHeld)
            {
                orbitTouch = -1;
                foreach (var touch in screen.touches)
                {
                    if (!touch.isInProgress || MobileCatControls.IsControlTouch(touch.touchId.ReadValue())) continue;
                    Vector2 point = touch.position.ReadValue();
                    if (PointBlocked(point)) continue;
                    orbitTouch = touch.touchId.ReadValue();
                    pointer = point;
                    pointerHeld = true;
                    dragging = false;
                    break;
                }
            }
        }
        // A real touch owns its gesture until release, even when it crosses a
        // button. Synthetic mouse events cannot replace the second finger.
        if (!touchPresent)
        {
            orbitTouch = -1;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointer = mouse.position.ReadValue();
                bool held = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
                pointerHeld = held && !MobileCatControls.IsControlTouch(-2) &&
                    (dragging || !PointBlocked(pointer));
                if (lens && !lens.pixelRect.Contains(pointer) && !dragging) pointerHeld = false;
                float wheel = mouse.scroll.ReadValue().y / 120f;
                if (Mathf.Abs(wheel) > .01f && !PointBlocked(pointer))
                    targetDistance = Mathf.Clamp(targetDistance - wheel * .25f, distanceLimits.x, distanceLimits.y);
            }
        }
        if (pointerHeld)
        {
            if (dragging)
            {
                Vector2 delta = pointer - previousPointer;
                targetYawOffset = Mathf.Repeat(targetYawOffset + delta.x * 360f /
                    Mathf.Max(1, Screen.width) * lookSensitivity + 180f, 360f) - 180f;
                // Drag down to lower the lens, so the player can deliberately
                // look beneath a tabletop instead of hiding that tabletop.
                targetPitch = Mathf.Clamp(targetPitch + delta.y * 100f /
                    Mathf.Max(1, Screen.height) * lookSensitivity, 5f, 70f);
            }
            previousPointer = pointer;
        }
        dragging = pointerHeld;
    }

    private bool PointBlocked(Vector2 point)
    {
        if (MobileCatControls.IsScreenPointInControls(point)) return true;
        if (!EventSystem.current) return false;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, uiHits);
        return uiHits.Count != 0;
    }

    // Retain compatibility for external callers; the touch HUD has no reset
    // control. Ordinary play never invokes this explicit command.
    public void CycleView()
    {
        if (!target || !GameFlowUI.GameplayInputEnabled) return;
        targetYawOffset = Mathf.DeltaAngle(viewYaw, target.eulerAngles.y);
        targetPitch = pitch;
    }

    private bool IsPlayer(Collider other) => !other || other.transform == target || other.transform.IsChildOf(target);

    private bool PositionClear(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, collisionRadius, overlaps,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length)
        {
            foreach (var other in Physics.OverlapSphere(position, collisionRadius,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (!IsPlayer(other)) return false;
            return true;
        }
        for (int i = 0; i < count; i++) if (!IsPlayer(overlaps[i])) return false;
        return true;
    }

    private float ClearEndpointDistance(Vector3 direction, float wanted, float maximum)
    {
        if (PositionClear(focus + direction * wanted)) return wanted;
        // Stay on the player's selected ray and keep the lens in free space.
        // Objects along the sightline remain opaque; the player controls when
        // to lower the camera and look underneath them.
        for (float step = .10f; step <= 3.5f; step += .10f)
        {
            float shorter = wanted - step;
            if (shorter >= Mathf.Min(.25f,maximum) && PositionClear(focus + direction * shorter)) return shorter;
            float longer = wanted + step;
            if (longer<=maximum&&PositionClear(focus + direction * longer)) return longer;
        }
        return wanted;
    }

    private void PositionCamera(float dt, bool snap)
    {
        Quaternion rotation = Quaternion.Euler(smoothedPitch, OrbitYaw, 0f);
        Vector3 direction = -(rotation * Vector3.forward);
        int count = Physics.SphereCastNonAlloc(focus, collisionRadius, direction, collisionHits,
            horizontalDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float nearestBoundary = horizontalDistance;
        for (int i = 0; i < count; i++)
        {
            var hit = collisionHits[i];
            if (IsPlayer(hit.collider) || hit.distance <= .001f || !IsRoomBoundary(hit.transform)) continue;
            nearestBoundary = Mathf.Min(nearestBoundary, Mathf.Max(.20f,hit.distance - collisionPadding));
        }
        // A nearby obstacle must not collapse the view onto the cat. Preserve
        // the player's viewing angle; furniture may naturally occlude the cat.
        // Tables may occlude the cat at high pitch. Room boundaries, however,
        // must keep the lens inside the room rather than behind an opaque wall.
        float wanted = ClearEndpointDistance(direction, nearestBoundary, nearestBoundary);
        resolvedDistance = snap ? wanted : Mathf.Lerp(resolvedDistance, wanted, 1f - Mathf.Exp(-8f * dt));
        resolvedDistance = ClearEndpointDistance(direction, Mathf.Min(nearestBoundary,resolvedDistance),nearestBoundary);
        transform.SetPositionAndRotation(focus + direction * resolvedDistance, rotation);
        if(lens)
        {
            float adjustedFov=Mathf.Clamp(2f*Mathf.Atan(Mathf.Tan(fieldOfView*.5f*Mathf.Deg2Rad)*
                horizontalDistance/Mathf.Max(.3f,resolvedDistance))*Mathf.Rad2Deg,fieldOfView,100f);
            lens.fieldOfView=snap?adjustedFov:Mathf.Lerp(lens.fieldOfView,adjustedFov,1f-Mathf.Exp(-10f*dt));
        }
    }

    private static bool IsRoomBoundary(Transform part)
    {
        for(var t=part;t;t=t.parent)
            if(t.name.Contains("Wall",System.StringComparison.OrdinalIgnoreCase)||t.name=="Door"||t.name.StartsWith("V2 Window"))return true;
        return false;
    }

}
