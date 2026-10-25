using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CatCornerMovementFix
{
    const string Output = "Validation/CatCornerMovement.txt";
    [MenuItem("Catastrophe/Reference Room/Fix and Test Cat Corner Movement")]
    public static void Apply()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/LivingRoom.unity")
            throw new InvalidOperationException("Exit Play and open LivingRoom.");
        var report = new List<string>();
        GameObject tree = GameObject.Find("Tripo Cat Tree");
        GameObject shelf = GameObject.Find("Tripo Shelving Unit");
        if (!tree || !shelf) throw new InvalidOperationException("Missing furniture.");
        Undo.RegisterFullObjectHierarchyUndo(tree, "Widen cat corner passage");
        Physics.SyncTransforms();
        Bounds sb = VisualBounds(shelf);
        Bounds tb = VisualBounds(tree);
        report.Add($"Before: visual gap={sb.min.z-tb.max.z:F3}m; tree visual={tb}; colliders=" +
            string.Join("; ", tree.GetComponentsInChildren<Collider>().Select(c => c.name + " " + c.bounds)));
        // Preserve shelf width and location. Open a 65cm passage at its end.
        float targetMaxZ = sb.min.z - .65f;
        tree.transform.position += Vector3.forward * (targetMaxZ - tb.max.z);
        tb = VisualBounds(tree);
        if (tb.min.z < -3.20f) throw new InvalidOperationException("Cat tree would enter front wall.");
        // Existing tree is static: retain its solid hull, but remove oversized
        // imported-pivot offsets rather than shrinking the player's capsule.
        BoxCollider box = tree.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.center = tree.transform.InverseTransformPoint(tb.center);
            Vector3 size = tree.transform.InverseTransformVector(tb.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(tree.transform);
        Physics.SyncTransforms();
        report.Add($"After: gap={sb.min.z-tb.max.z:F3}m; cat diameter=.400m; tree={tb}");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Directory.CreateDirectory("Validation"); File.WriteAllLines(Output,report);
        Debug.Log("CAT_CORNER_SAVED: enter Play and run Test Cat Corner Movement (Play).");
    }
    [MenuItem("Catastrophe/Reference Room/Test Cat Corner Movement (Play)")]
    public static void Validate()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
        var report = File.Exists(Output) ? File.ReadAllLines(Output).Take(2).ToList() : new List<string>();
        Bounds sb=VisualBounds(GameObject.Find("Tripo Shelving Unit"));
        Bounds tb=VisualBounds(GameObject.Find("Tripo Cat Tree"));
        try
        {
            foreach (Vector2 input in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, Vector2.one })
            {
                Vector3 actual = CatController.RoomAlignedDirection(input);
                Vector3 expected = Vector3.ClampMagnitude(new Vector3(input.x,0,input.y),1);
                if ((actual-expected).sqrMagnitude > .000001f) throw new Exception("Movement axis failure");
                report.Add($"PASS input {input} -> {actual}");
            }
            TestPassage((sb.min.z+tb.max.z)*.5f, report);
            report.Add("PASS: saved room alignment and corner walk/jump physics tests.");
            Debug.Log("CAT_CORNER_PASS");
        }
        catch (Exception e) { report.Add("FAIL: " + e); Debug.LogException(e); }
        finally { Directory.CreateDirectory("Validation"); File.WriteAllLines(Output,report); }
    }
    static Bounds VisualBounds(GameObject go)
    {
        Renderer[] rr = go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        Bounds b = rr[0].bounds; foreach (Renderer r in rr.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    static void TestPassage(float z, List<string> report)
    {
        Scene source = SceneManager.GetActiveScene();
        Scene test = SceneManager.CreateScene("Corner collision test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try
        {
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.gameObject.scene != source || !c.enabled || c.isTrigger || c.GetComponentInParent<CatController>()) continue;
                GameObject clone = new("Test " + c.name); SceneManager.MoveGameObjectToScene(clone,test);
                clone.transform.SetPositionAndRotation(c.transform.position,c.transform.rotation);
                clone.transform.localScale = c.transform.lossyScale;
                UnityEditorInternal.ComponentUtility.CopyComponent(c);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(clone);
            }
            GameObject cat = new("Test cat"); SceneManager.MoveGameObjectToScene(cat,test);
            CapsuleCollider capsule = cat.AddComponent<CapsuleCollider>(); capsule.radius=.20f; capsule.height=.50f;
            capsule.center=new Vector3(0,.025f,0);
            Rigidbody rb = cat.AddComponent<Rigidbody>(); rb.mass=1.5f; rb.constraints=RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            PhysicsScene physics = test.GetPhysicsScene();
            foreach (bool jumping in new[] {false,true})
            foreach (bool entering in new[] {false,true})
            {
                float start = entering ? 2.55f : 3.75f, target = entering ? 3.75f : 2.55f;
                rb.position=new Vector3(start,.24f,z); rb.linearVelocity=Vector3.zero;
                Physics.SyncTransforms();
                for(int i=0;i<20;i++) physics.Simulate(.02f);
                if(jumping) rb.linearVelocity=Vector3.up*Mathf.Sqrt(2*9.81f*.58f);
                float highest=rb.position.y;
                for(int i=0;i<85;i++)
                {
                    Vector3 v=rb.linearVelocity; v.x=Mathf.Clamp((target-rb.position.x)/.02f,-1.15f,1.15f); v.z=0; rb.linearVelocity=v;
                    if(v.y<0) rb.AddForce(Physics.gravity*1.1f,ForceMode.Acceleration);
                    physics.Simulate(.02f); highest=Mathf.Max(highest,rb.position.y);
                }
                string label=$"{(jumping ? "jump" : "walk")} {(entering ? "into" : "out of")} corner";
                float error=Mathf.Abs(rb.position.x-target);
                report.Add($"{label}: end={rb.position}, error={error:F3}, peak={highest:F3}");
                if(error>.06f || Mathf.Abs(rb.position.z-z)>.08f || rb.position.y<.15f || (jumping && highest<.65f))
                    throw new Exception(label+" blocked or unsupported");
                report.Add("PASS " + label);
            }
        }
        finally { SceneManager.UnloadSceneAsync(test); }
    }
}
