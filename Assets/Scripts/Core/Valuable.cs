using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Valuable : MonoBehaviour
{
    public static event Action<Valuable> AnyDestroyed;
    public string itemName;
    public int chaosValue = 10;
    public float weight = 1f;
    public float noise = 30f;
    public bool fragile;

    [SerializeField] private float minimumFallDistance = 0.20f;
    [SerializeField] private float initialSettleTime = 1.25f;
    [SerializeField] private float maximumSpeed = 2f;
    [SerializeField] private bool lockPhysicsUntilFirstPush = true;

    private bool destroyed;
    private float startingSupportY;
    private float armedAt;
    private bool destructionArmed;
    private Rigidbody body;
    private static PhysicsMaterial nudgeMaterial;
    private static PhysicsMaterial televisionSlideMaterial;
    private bool isCabinetTelevision;
    [SerializeField, Min(.03f)] private float nudgeDistance = .15f;
    private Vector3 nudgeDirection, nudgeStart;
    private float nudgeElapsed;
    private bool nudging;
    public bool IsDestroyed => destroyed;
    private Collider[] itemColliders;
    private readonly RaycastHit[] supportHits = new RaycastHit[16];

    public bool TryNudge(Vector3 direction)
    {
        if (destroyed || nudging) return false;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (direction.sqrMagnitude < .5f) return false;
        ActivatePhysics();
        if (body == null) return false;
        nudgeDirection = direction; nudgeStart = body.position; nudgeElapsed = 0f; nudging = true;
        return true;
    }

    public event Action<Valuable> OnDestroyed;

    public GameSaveStore.PropState CaptureSave()
    {
        return new GameSaveStore.PropState {
            key = GameSaveStore.ObjectKey(transform), position = transform.position, rotation = transform.rotation,
            destroyed = destroyed, support = startingSupportY, armed = destructionArmed,
            kinematic = body == null || body.isKinematic,
            velocity = body && !body.isKinematic ? body.linearVelocity : Vector3.zero,
            angularVelocity = body && !body.isKinematic ? body.angularVelocity : Vector3.zero
        };
    }

    public void RestoreSave(GameSaveStore.PropState saved)
    {
        destroyed = saved.destroyed; startingSupportY = saved.support;
        destructionArmed = saved.armed; nudging = false; armedAt = Time.time + .15f;
        transform.SetPositionAndRotation(saved.position, saved.rotation);
        if (!body) return;
        body.position = saved.position; body.rotation = saved.rotation; body.isKinematic = saved.kinematic;
        if (!body.isKinematic) { body.linearVelocity = saved.velocity; body.angularVelocity = saved.angularVelocity; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AnyDestroyed = null;
    }

    private void Awake()
    {
        isCabinetTelevision = GetComponent<CabinetOnlyTelevision>() != null;
        armedAt = Time.time + Mathf.Max(0f, initialSettleTime);
        if (string.IsNullOrWhiteSpace(itemName))
            itemName = gameObject.name;
        ApplyPhysicsTuning();
        CaptureFallBaseline();
        if (body != null && lockPhysicsUntilFirstPush)
        {
            // Imported props are authored in their final display positions. Keeping
            // them kinematic prevents Unity's first physics step from separating
            // tiny collider contacts and making untouched props creep or launch.
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
        // A thin television should slide across its stand before it tips. Once
        // its centre leaves the cabinet, FixedUpdate restores free rotation.
        if (isCabinetTelevision) body.constraints = RigidbodyConstraints.FreezeRotation;
        body.WakeUp();
        CaptureFallBaseline();
        armedAt = Time.time + 0.15f;
        // A deliberately pushed item must score even if it falls before ever settling.
        destructionArmed = true;
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

        if (isCabinetTelevision && televisionSlideMaterial == null)
        {
            televisionSlideMaterial = new PhysicsMaterial("Television Sliding Contact")
            {
                dynamicFriction = 0.05f,
                staticFriction = 0.05f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                hideFlags = HideFlags.DontSave
            };
        }

        itemColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider itemCollider in itemColliders)
            itemCollider.sharedMaterial = isCabinetTelevision ? televisionSlideMaterial : nudgeMaterial;
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (isCabinetTelevision && !body.isKinematic)
        {
            bool onCabinet = body.worldCenterOfMass.y > startingSupportY + .15f && HasSupport();
            body.constraints = onCabinet ? RigidbodyConstraints.FreezeRotation : RigidbodyConstraints.None;
        }

        if (nudging)
        {
            nudgeElapsed += Time.fixedDeltaTime;
            float moved = Vector3.Dot(body.position - nudgeStart, nudgeDirection);
            float strokeDistance = isCabinetTelevision ? .26f : nudgeDistance;
            bool supported = HasSupport();
            // A wide laptop can overhang the edge while its base still rests
            // on the table. Do not cancel the stroke just because the centre
            // support ray crossed the edge; release once it really starts falling.
            bool falling = !supported && body.linearVelocity.y < -.15f;
            if (falling || moved >= strokeDistance - .005f || nudgeElapsed >= .55f)
            {
                nudging = false;
                // Stop on the tabletop, but retain momentum/gravity after crossing its edge.
                if (supported) body.linearVelocity = Vector3.up * body.linearVelocity.y;
            }
            else
            {
                float speed = Mathf.Min(isCabinetTelevision ? 1.5f : .80f,
                    (strokeDistance - moved) / Time.fixedDeltaTime);
                body.linearVelocity = nudgeDirection * speed + Vector3.up * body.linearVelocity.y;
                body.WakeUp();
            }
        }

        if (body.linearVelocity.sqrMagnitude > maximumSpeed * maximumSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maximumSpeed;
    }

    private bool HasSupport()
    {
        if (itemColliders == null || itemColliders.Length == 0) return false;
        Bounds bounds = itemColliders[0].bounds;
        foreach (var c in itemColliders) if(c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
        Vector3 origin = new Vector3(body.worldCenterOfMass.x, bounds.min.y + .04f, body.worldCenterOfMass.z);
        // Query the owning physics scene so isolated verification uses the same code.
        var physicsScene = gameObject.scene.GetPhysicsScene();
        int count = physicsScene.Raycast(origin, Vector3.down, supportHits, .10f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i=0;i<count;i++) if(supportHits[i].rigidbody != body && supportHits[i].normal.y > .5f) return true;
        return false;
    }

    private void Update()
    {
        // Scene props can move a few centimetres while Unity resolves their
        // initial contact with a shelf or tabletop. Establish the destruction
        // baseline only after that short settling phase, otherwise several
        // decorative props can emit enough noise to trigger the owner at spawn.
        if (!destructionArmed)
        {
            CaptureFallBaseline();
            bool settled = body == null || body.IsSleeping() ||
                           (body.linearVelocity.sqrMagnitude < 0.0025f &&
                            body.angularVelocity.sqrMagnitude < 0.01f);
            if (Time.time >= armedAt && settled)
                destructionArmed = true;
            return;
        }

        // Tilting can lower a tall vase's pivot substantially without leaving
        // the table. Require its physical centre to fall BELOW the original
        // support plane, not merely below its original upright pivot.
        // Score on actual impact, not while still falling through the air.
        // Only the out-of-world safety fallback resolves without a collision.
        if (!destroyed && HasFallenOffSupport() && transform.position.y < -5f)
            MarkDestroyed();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!destructionArmed || Time.time < armedAt || !GameFlowUI.GameplayInputEnabled ||
            body == null || body.isKinematic) return;
        // Sliding or bumping an item on its current table/shelf stays quiet.
        // Noise is emitted once, by MarkDestroyed, only after the item has
        // genuinely left its support and impacts below the original surface.
        if (!destroyed && HasFallenOffSupport()) MarkDestroyed();
    }

    private void CaptureFallBaseline()
    {
        float bottom = float.PositiveInfinity;
        if (itemColliders != null)
            foreach (var c in itemColliders)
                if (c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy)
                    bottom = Mathf.Min(bottom, c.bounds.min.y);
        startingSupportY = float.IsPositiveInfinity(bottom) ? transform.position.y : bottom;
    }

    private bool HasFallenOffSupport()
    {
        return destructionArmed && body != null && !body.isKinematic &&
               body.worldCenterOfMass.y <= startingSupportY - minimumFallDistance;
    }

    public void MarkDestroyed()
    {
        if (destroyed || !HasFallenOffSupport())
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
