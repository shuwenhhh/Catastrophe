using UnityEngine;
using UnityEngine.SceneManagement;

// Compound gameplay envelope, independent of bones: animation cannot move the
// collider through scenery. No authored transforms or scene files are changed.
[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(100)]
public sealed class CatSolidCollision : MonoBehaviour
{
    public const float SizeFactor = .90f;
    readonly System.Collections.Generic.List<CapsuleCollider> parts = new();
    public System.Collections.Generic.IReadOnlyList<CapsuleCollider> Parts => parts;
    Rigidbody body;
    PhysicsMaterial contactMaterial;
    PhysicsScene physicsScene;
    readonly RaycastHit[] sweepHits=new RaycastHit[128];
    readonly Collider[] overlapHits=new Collider[256];
    const float Skin = .012f;

    void Awake() => Initialize();

    public void Initialize()
    {
        if (parts.Count != 0) return;
        body = GetComponent<Rigidbody>();
        physicsScene=gameObject.scene.IsValid()?gameObject.scene.GetPhysicsScene():Physics.defaultPhysicsScene;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.solverIterations = 12;
        body.solverVelocityIterations = 6;
        contactMaterial=new PhysicsMaterial("Cat non-sticking contact"){dynamicFriction=0,staticFriction=0,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};
        // Feet/body support retains the original floor baseline without filling
        // the empty space between the raised tail and the head.
        var support=GetComponent<CapsuleCollider>();
        if(support){support.center=new Vector3(.04f,-.06f,0);support.radius=.105f;support.height=.33f;support.contactOffset=.003f;parts.Add(support);}
        AddPart("Body protection",new Vector3(.04f,-.035f,-.13f),new Vector3(.04f,-.035f,.12f),.105f);
        // Upper torso is wider than the belly/foot support. Protect the visible
        // shoulder and haunch without raising the feet or filling the tail gap.
        AddPart("Shoulder and back protection",new Vector3(.02f,.095f,-.13f),new Vector3(.02f,.095f,.10f),.18f);
        AddPart("Head protection",new Vector3(.04f,.115f,.225f),new Vector3(.04f,.135f,.225f),.135f);
        AddPart("Tail lower protection",new Vector3(.024f,-.015f,-.15f),new Vector3(.024f,.07f,-.25f),.043f);
        // A slightly fuller curl follows the visible tail beside cat-tree posts,
        // while still fitting under the 0.67 m clearance of the shelf bays.
        AddPart("Tail curl protection",new Vector3(.024f,.07f,-.25f),new Vector3(.024f,.19f,-.23f),.035f);
        AddPart("Tail tip protection",new Vector3(.024f,.19f,-.23f),new Vector3(.024f,.19f,-.18f),.025f);
        // Scale around the planted feet, keeping the controller's floor baseline.
        Vector3 feet = Vector3.down * .225f;
        foreach(var part in parts)
        {
            if(part.transform == transform)
            {
                part.center = feet + (part.center - feet) * SizeFactor;
                part.radius *= SizeFactor;
                part.height *= SizeFactor;
            }
            else
            {
                part.transform.localPosition = feet + (part.transform.localPosition - feet) * SizeFactor;
                part.transform.localScale = Vector3.one * SizeFactor;
            }
            part.sharedMaterial=contactMaterial;
        }
    }

    // Compatibility for old validation callers. Fixed curl never changes near furniture.
    public void UpdateTailClearance(float dt) { }

    void OnDestroy(){if(contactMaterial){if(Application.isPlaying)Destroy(contactMaterial);else DestroyImmediate(contactMaterial);}}

    void AddPart(string name,Vector3 a,Vector3 b,float radius)
    {
        var child=new GameObject(name);child.layer=gameObject.layer;child.transform.SetParent(transform,false);
        child.transform.localPosition=(a+b)*.5f;child.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        var capsule=child.AddComponent<CapsuleCollider>();capsule.radius=radius;capsule.height=Vector3.Distance(a,b)+2*radius;capsule.contactOffset=.003f;parts.Add(capsule);
    }

    void Shape(CapsuleCollider part,Vector3 position,Quaternion rotation,out Vector3 a,out Vector3 b,out float radius,out Vector3 objectPosition,out Quaternion objectRotation)
    {
        Vector3 local=transform.InverseTransformPoint(part.transform.position);
        Quaternion localRotation=Quaternion.Inverse(transform.rotation)*part.transform.rotation;
        objectPosition=position+rotation*Vector3.Scale(local,transform.lossyScale);objectRotation=rotation*localRotation;
        Vector3 scale=part.transform.lossyScale;radius=part.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
        Vector3 center=objectPosition+objectRotation*Vector3.Scale(part.center,scale);
        Vector3 extent=objectRotation*Vector3.up*Mathf.Max(0,part.height*Mathf.Abs(scale.y)*.5f-radius);
        a=center+extent;b=center-extent;
    }

    bool Blocks(Collider other) => other.enabled && !other.isTrigger &&
        other.attachedRigidbody != body && !other.transform.IsChildOf(transform) &&
        !Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer) &&
        other.gameObject.activeInHierarchy;

    public bool CanOccupy(Vector3 position, Quaternion rotation)
    {
        foreach(var part in parts)
        {
            Shape(part,position,rotation,out var a,out var b,out float radius,out var p,out var r);
            int count=physicsScene.OverlapCapsule(a,b,radius,overlapHits,Physics.AllLayers,QueryTriggerInteraction.Ignore);
            if(count==overlapHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                Collider other=overlapHits[i];
                if(Blocks(other)&&!Physics.GetIgnoreCollision(part,other)&&Physics.ComputePenetration(part,p,r,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.003f)return false;
            }
        }
        return true;
    }

    public Quaternion TurnTowards(Quaternion desired)
    {
        Quaternion start = body.rotation;
        // Test the swept turn, not just the final orientation, so the nose/tail
        // cannot swing through a narrow table leg between physics steps.
        int steps = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(start, desired) / 2f));
        Quaternion safe = start;
        for (int i = 1; i <= steps; i++)
        {
            Quaternion next = Quaternion.Slerp(start, desired, (float)i / steps);
            if (!CanOccupy(body.position, next)) break;
            safe = next;
        }
        body.MoveRotation(safe);
        return safe;
    }

    public Vector3 LimitHorizontalVelocity(Vector3 requested, float dt)
    {
        Vector3 direct = LimitStraightVelocity(requested, dt);
        // Retain a free tangent component when diagonal input meets furniture.
        // Each candidate uses the full sweep and destination overlap checks.
        if(Mathf.Abs(requested.x) < .001f || Mathf.Abs(requested.z) < .001f) return direct;
        Vector3 x = LimitStraightVelocity(new Vector3(requested.x, requested.y, 0), dt);
        Vector3 z = LimitStraightVelocity(new Vector3(0, requested.y, requested.z), dt);
        Vector3 best = direct;
        if(Vector3.Dot(x, requested) > Vector3.Dot(best, requested)) best = x;
        if(Vector3.Dot(z, requested) > Vector3.Dot(best, requested)) best = z;
        return best;
    }

    Vector3 LimitStraightVelocity(Vector3 requested, float dt)
    {
        Vector3 horizontal = new Vector3(requested.x, 0, requested.z);
        float distance = horizontal.magnitude * dt;
        if (distance < .00001f) return requested;
        Vector3 direction = horizontal.normalized;
        float available = distance;
        foreach(var part in parts)
        {
            Shape(part,body.position,body.rotation,out var a,out var b,out float radius,out var partPosition,out var partRotation);
            int count=physicsScene.CapsuleCast(a,b,radius-.003f,direction,sweepHits,distance+Skin,Physics.AllLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=sweepHits[i];
                if (!Blocks(hit.collider) || Physics.GetIgnoreCollision(part,hit.collider)) continue;
                // Flat floor contact is harmless, but a rounded capsule hitting
                // a shelf lip can also return an upward-facing normal. Do not
                // ignore that lip when motion points into it.
                if(hit.normal.y>.65f&&Vector3.Dot(direction,hit.normal)>=-.001f)continue;
                // A tail or head already brushing furniture can produce a
                // zero-distance cast in every direction. Let the cat back away
                // when the requested motion separates the overlapping shapes.
                if (hit.distance < .002f &&
                    Physics.ComputePenetration(part,partPosition,partRotation,hit.collider,
                        hit.collider.transform.position,hit.collider.transform.rotation,
                        out Vector3 separation,out _) &&
                    Vector3.Dot(direction,separation) > .05f) continue;
                available = Mathf.Min(available, Mathf.Max(0, hit.distance - Skin));
            }
        }
        // Triangle seams and rounded mesh corners can disagree with a cast.
        // Check the proposed pose as well; keep a clear start clear without
        // blocking separation when physics is resolving an existing contact.
        if(available>0 && !CanOccupy(body.position+direction*available,body.rotation)
            && CanOccupy(body.position,body.rotation))
        {
            float low=0,high=available;
            for(int i=0;i<10;i++)
            {
                float middle=(low+high)*.5f;
                if(CanOccupy(body.position+direction*middle,body.rotation))low=middle;
                else high=middle;
            }
            available=Mathf.Max(0,low-.001f);
        }
        horizontal *= available / distance;
        return new Vector3(horizontal.x, requested.y, horizontal.z);
    }

    public static int EnsureFurnitureCollisions(GameObject room, bool tablesAndChairsOnly = false)
    {
        if (room == null) return 0;
        int added = 0;
        foreach (MeshFilter filter in room.GetComponentsInChildren<MeshFilter>())
        {
            var visible = filter.GetComponent<Renderer>();
            if (visible != null && !visible.enabled) continue;
            if (filter.GetComponentInParent<SoftDecoration>() != null) continue;
            if (filter.sharedMesh == null || filter.GetComponentInParent<Rigidbody>() != null) continue;
            // Prepared chair collision pieces already cover the exact surface.
            Transform chair=filter.transform;
            while(chair!=null&&chair!=room.transform&&chair.Find("Chair surface collisions")==null)chair=chair.parent;
            if(chair!=null&&chair.Find("Chair surface collisions")!=null)continue;
            bool tableOrChair = false;
            for (Transform t = filter.transform; t != null && t != room.transform; t = t.parent)
            {
                string name = t.name.ToLowerInvariant();
                if (name.Contains("chair") || name == "table" || name.Contains("tripo_table")) tableOrChair = true;
            }
            bool hasSolid = false;
            if (tablesAndChairsOnly && !tableOrChair) continue;
            foreach (Collider c in filter.GetComponentsInParent<Collider>())
                if (c.enabled && !c.isTrigger) hasSolid = true;
            if (hasSolid && !tableOrChair) continue;
            if(tableOrChair)
                foreach(Collider c in filter.GetComponentsInParent<Collider>())
                    if(!c.isTrigger&&c is BoxCollider)c.enabled=false;
            var existingMesh = filter.GetComponent<MeshCollider>();
            if (existingMesh != null)
            {
                // Static batching replaces the renderer mesh with a combined,
                // world-space mesh. It is not valid in this collider's local
                // transform and can introduce invisible walls under furniture.
                // Preserve the authored collision mesh across runtime setup.
                if(existingMesh.sharedMesh == null && (visible == null || !visible.isPartOfStaticBatch))
                    existingMesh.sharedMesh = filter.sharedMesh;
                existingMesh.convex = false;
                existingMesh.enabled = true;
                existingMesh.isTrigger = false;
                continue;
            }
            // Static triangle collisions preserve the empty space under tables
            // while protecting the actual legs, seat and backrest.
            MeshCollider mesh = filter.gameObject.AddComponent<MeshCollider>();
            mesh.sharedMesh = filter.sharedMesh;
            mesh.convex = false;
            added++;
        }
        return added;
    }
}
