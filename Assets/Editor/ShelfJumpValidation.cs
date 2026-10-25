using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ShelfJumpValidation
{
    [MenuItem("Catastrophe/Reference Room/Test Shelf Jump Route")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode to run isolated physics simulation.");
        Scene source = SceneManager.GetActiveScene();
        Scene test = SceneManager.CreateScene("Shelf jump validation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var report = new List<string>();
        try
        {
            foreach (BoxCollider original in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                if (original.gameObject.scene != source || !original.enabled || original.isTrigger || original.GetComponentInParent<CatController>()) continue;
                GameObject clone = new("Test " + original.name);
                SceneManager.MoveGameObjectToScene(clone, test);
                clone.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                clone.transform.localScale = original.transform.lossyScale;
                BoxCollider box = clone.AddComponent<BoxCollider>(); box.size = original.size; box.center = original.center;
            }
            GameObject cat = new("Test cat capsule"); SceneManager.MoveGameObjectToScene(cat, test);
            CapsuleCollider capsule = cat.AddComponent<CapsuleCollider>(); capsule.radius = .20f; capsule.height = .50f;
            Rigidbody rb = cat.AddComponent<Rigidbody>(); rb.mass = 1.5f; rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var physics = test.GetPhysicsScene();
            var shelf = GameObject.Find("Tripo Shelving Unit");
            var steps = Enumerable.Range(1, 7).Select(i => shelf.transform.Find($"Cat access step {i}").GetComponent<BoxCollider>().bounds).ToArray();
            var boards = Enumerable.Range(1, 4).Select(i => shelf.transform.Find($"Climbable Shelf Platform {i:00}").GetComponent<BoxCollider>().bounds).ToArray();
            Vector3 foot = steps[0].center + new Vector3(-.42f, 0f, 0f); foot.y = .015f;
            for (int i = 0; i < steps.Length; i++)
            {
                Vector3 target = steps[i].center; target.y = steps[i].max.y + .01f;
                foot = Jump(physics, rb, foot, target, "ground/stair -> step " + (i + 1), report);
            }
            Vector3 top = boards[3].center; top.x = foot.x; top.z = boards[3].min.z + .25f; top.y = boards[3].max.y + .01f;
            Jump(physics, rb, foot, top, "step 7 -> top shelf", report);
            // Access to each lower board from the closest stair, not a teleport-to-shelf assertion.
            for (int level = 0; level < 3; level++)
            {
                Bounds step = steps[level * 2];
                Vector3 start = step.center; start.y = step.max.y + .01f;
                Vector3 target = boards[level].center; target.x = start.x; target.z = boards[level].min.z + .25f; target.y = boards[level].max.y + .01f;
                Jump(physics, rb, start, target, "step " + (level * 2 + 1) + " -> shelf " + (level + 1), report);
            }
            report.Add("PASS: 11 physics trajectories; capsule diameter .40m, height .50m, speed 1.15m/s, jump height .58m, descent gravity x2.1.");
            Debug.Log("SHELF_JUMPS_PASS " + report.Last());
        }
        catch (Exception e) { report.Add("FAIL: " + e.Message); Debug.LogException(e); }
        finally
        {
            Directory.CreateDirectory("Validation/ReferenceShelf");
            File.WriteAllLines("Validation/ReferenceShelf/jumps.txt", report);
            SceneManager.UnloadSceneAsync(test);
        }
    }

    static Vector3 Jump(PhysicsScene scene, Rigidbody rb, Vector3 start, Vector3 target, string name, List<string> report)
    {
        rb.position = start + Vector3.up * .25f;
        rb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        for (int i = 0; i < 10; i++) scene.Simulate(.02f);
        rb.linearVelocity = Vector3.up * Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * .58f);
        for (int i = 0; i < 90; i++)
        {
            Vector3 delta = target - (rb.position - Vector3.up * .25f); delta.y = 0;
            Vector3 horizontal = Vector3.ClampMagnitude(delta / .02f, 1.15f);
            Vector3 velocity = rb.linearVelocity; velocity.x = horizontal.x; velocity.z = horizontal.z; rb.linearVelocity = velocity;
            if (velocity.y < 0) rb.AddForce(Physics.gravity * 1.1f, ForceMode.Acceleration);
            scene.Simulate(.02f);
        }
        Vector3 landed = rb.position - Vector3.up * .25f;
        float error = Vector3.Distance(landed, target);
        report.Add($"{name}: landed={landed}, target={target}, error={error:F3}m");
        if (error > .07f) throw new InvalidOperationException(name + " did not land on target.");
        return landed;
    }
}
