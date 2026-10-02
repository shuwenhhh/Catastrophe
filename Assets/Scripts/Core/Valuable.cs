using System;
using UnityEngine;

public sealed class Valuable : MonoBehaviour
{
    public static event Action<Valuable> AnyDestroyed;
    public string itemName;
    public int chaosValue = 10;
    public float weight = 1f;
    public float noise = 30f;
    public bool fragile;

    [SerializeField] private float minimumFallDistance = 0.20f;
    [SerializeField, Range(15f, 90f)] private float toppledAngle = 65f;
    [SerializeField] private float initialSettleTime = 1.25f;
    [SerializeField] private float maximumSpeed = 2f;
    [SerializeField] private bool lockPhysicsUntilFirstPush = true;

    private bool destroyed;
    private float startingY;
    private Vector3 startingUp;
    private float armedAt;
    private bool destructionArmed;
    private Rigidbody body;
    private static PhysicsMaterial nudgeMaterial;

    public event Action<Valuable> OnDestroyed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AnyDestroyed = null;
    }

    private void Awake()
    {
        startingY = transform.position.y;
        startingUp = transform.up;
        armedAt = Time.time + Mathf.Max(0f, initialSettleTime);
        if (string.IsNullOrWhiteSpace(itemName))
            itemName = gameObject.name;
        ApplyPhysicsTuning();
        if (body != null && lockPhysicsUntilFirstPush)
        {
            // Imported props are authored in their final display positions. Keeping
            // them kinematic prevents Unity's first physics step from separating
            // tiny collider contacts and making untouched props creep or launch.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    public void ActivatePhysics()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (body == null || !body.isKinematic)
            return;

        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        startingY = transform.position.y;
        startingUp = transform.up;
        armedAt = Time.time + 0.15f;
    }

    public void ApplyPhysicsTuning()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
            return;

        weight = Mathf.Clamp(weight, 1f, 3f);
        body.mass = weight;
        body.linearDamping = Mathf.Lerp(1.5f, 2.5f, (weight - 1f) / 2f);
        body.angularDamping = 2f;
        body.maxAngularVelocity = 3f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (nudgeMaterial == null)
        {
            nudgeMaterial = new PhysicsMaterial("Valuable Nudge Material")
            {
                dynamicFriction = 0.5f,
                staticFriction = 0.5f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                hideFlags = HideFlags.DontSave
            };
        }

        foreach (Collider itemCollider in GetComponentsInChildren<Collider>(true))
            itemCollider.sharedMaterial = nudgeMaterial;
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (body.linearVelocity.sqrMagnitude > maximumSpeed * maximumSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maximumSpeed;
    }

    private void Update()
    {
        // Scene props can move a few centimetres while Unity resolves their
        // initial contact with a shelf or tabletop. Establish the destruction
        // baseline only after that short settling phase, otherwise several
        // decorative props can emit enough noise to trigger the owner at spawn.
        if (!destructionArmed)
        {
            startingY = transform.position.y;
            startingUp = transform.up;
            bool settled = body == null || body.IsSleeping() ||
                           (body.linearVelocity.sqrMagnitude < 0.0025f &&
                            body.angularVelocity.sqrMagnitude < 0.01f);
            if (Time.time >= armedAt && settled)
                destructionArmed = true;
            return;
        }

        // Use a displacement from the authored position rather than a hard-coded
        // floor height. This works on every table and completes the iPad target as
        // soon as it leaves its support, even if it lands on a chair on the way down.
        bool fellOffSupport = startingY - transform.position.y >= minimumFallDistance;
        bool toppled = Vector3.Angle(startingUp, transform.up) >= toppledAngle;
        if (!destroyed && (fellOffSupport || toppled))
            MarkDestroyed();
    }

    public void MarkDestroyed()
    {
        if (destroyed)
            return;

        destroyed = true;
        if (fragile)
            AudioManager.I?.PlayFragileBreak();
        else
            AudioManager.I?.PlayItemThud();
        GameManager.I?.AddChaos(chaosValue, itemName);
        NoiseManager.I?.EmitNoise(noise);
        OnDestroyed?.Invoke(this);
        AnyDestroyed?.Invoke(this);
    }
}
