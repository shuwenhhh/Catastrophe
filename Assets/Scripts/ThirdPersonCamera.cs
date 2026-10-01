using UnityEngine;

public sealed class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 3.35f;
    [SerializeField] private float height = 2.25f;
    [SerializeField] private float lookHeight = 0.30f;
    [SerializeField] private float positionSmoothTime = 0.18f;
    [SerializeField] private float rotationSharpness = 12f;
    [SerializeField] private float collisionRadius = 0.18f;
    [SerializeField] private float collisionPadding = 0.10f;
    [SerializeField] private float minimumDistance = 0.75f;

    private Vector3 positionVelocity;
    // The camera keeps the direction it had when play starts. The cat is free to
    // turn inside the frame without dragging the whole camera around it.
    private Vector3 followForward = Vector3.forward;

    private Vector3 FramingOffset => Vector3.Cross(Vector3.up, followForward).normalized * 0.45f;

    public void Initialize(Transform followTarget)
    {
        target = followTarget;
        if (target == null)
            return;

        followForward = FlatForward(target.forward);
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 lookPoint = target.position + FramingOffset + Vector3.up * lookHeight + followForward * 0.25f;
        Vector3 desiredPosition = target.position + FramingOffset - followForward * distance + Vector3.up * height;
        desiredPosition = ResolveCollision(lookPoint, desiredPosition);

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);
        transform.position = ResolveCollision(lookPoint, smoothedPosition);

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
    }

    private void SnapToTarget()
    {
        Vector3 lookPoint = target.position + FramingOffset + Vector3.up * lookHeight + followForward * 0.25f;
        Vector3 desiredPosition = target.position + FramingOffset - followForward * distance + Vector3.up * height;
        transform.position = ResolveCollision(lookPoint, desiredPosition);
        transform.LookAt(lookPoint);
    }

    private Vector3 ResolveCollision(Vector3 lookPoint, Vector3 desiredPosition)
    {
        Vector3 offset = desiredPosition - lookPoint;
        float desiredDistance = offset.magnitude;
        if (desiredDistance <= 0.001f)
            return desiredPosition;

        Vector3 direction = offset / desiredDistance;
        RaycastHit[] hits = Physics.SphereCastAll(
            lookPoint, collisionRadius, direction, desiredDistance,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        float nearestDistance = desiredDistance;
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == null || hitTransform == target || hitTransform.IsChildOf(target))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, hit.distance);
        }

        if (nearestDistance >= desiredDistance)
            return desiredPosition;

        float safeDistance = Mathf.Max(minimumDistance, nearestDistance - collisionPadding);
        return lookPoint + direction * safeDistance;
    }

    private static Vector3 FlatForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }
}
