using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ShelfJumpAudit
{
    [MenuItem("Catastrophe/Verify Shelf Geometry")]
    public static void Run()
    {
        var shelf = GameObject.Find("Tripo Shelving Unit");
        if (shelf == null) { Debug.LogError("SHELF_AUDIT: shelf not found"); return; }
        var boards = shelf.GetComponentsInChildren<BoxCollider>(true)
            .Where(c => c.enabled && c.name.StartsWith("Climbable Shelf Platform"))
            .OrderBy(c => c.bounds.max.y).ToArray();
        if (boards.Length != 4) throw new InvalidOperationException("Expected four solid shelf levels.");
        var first = boards[0].bounds;
        var report = new System.Collections.Generic.List<string>();
        foreach (var board in boards)
        {
            Bounds b = board.bounds;
            bool same = Mathf.Abs(b.min.x - first.min.x) < .015f &&
                        Mathf.Abs(b.max.x - first.max.x) < .015f &&
                        Mathf.Abs(b.min.z - first.min.z) < .015f &&
                        Mathf.Abs(b.max.z - first.max.z) < .015f;
            report.Add($"{board.name}: top={b.max.y:F3} x={b.min.x:F3}..{b.max.x:F3} z={b.min.z:F3}..{b.max.z:F3} footprint={(same ? "MATCH" : "DIFFERENT")}");
            if (!same) throw new InvalidOperationException("Shelf collider footprints still differ.");
        }
        Directory.CreateDirectory("Validation/ReferenceShelf");
        File.WriteAllLines("Validation/ReferenceShelf/uniform-levels.txt", report);
        Debug.Log("SHELF_AUDIT: four solid platforms have the same footprint.\n" + string.Join("\n", report));
        // A shelf must not merely look uniform: a cat on its first tier must
        // have a non-intersecting stance and a path back toward the room.
        var fixture = new GameObject("Disposable shelf-cat clearance check") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var rigidbody = fixture.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            fixture.AddComponent<CapsuleCollider>();
            var solid = fixture.AddComponent<CatSolidCollision>();
            bool found = false;
            for (float z = first.min.z + .35f; z < first.max.z - .35f; z += .25f)
            {
                Vector3 pose = new(first.center.x, first.max.y + .228f, z);
                fixture.transform.SetPositionAndRotation(pose, Quaternion.LookRotation(Vector3.right));
                Physics.SyncTransforms();
                if (!solid.CanOccupy(pose, fixture.transform.rotation)) continue;
                if (solid.LimitHorizontalVelocity(Vector3.left * .5f, .02f).x > -.1f) continue;
                report.Add($"PASS first shelf cat stance and room-side retreat at z={z:F2}");
                found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("Cat cannot stand on first shelf and retreat toward room.");
            File.WriteAllLines("Validation/ReferenceShelf/uniform-levels.txt", report);
        }
        finally { Object.DestroyImmediate(fixture); }
        var cat = Object.FindFirstObjectByType<CatController>();
        if (cat != null)
        {
            var body = cat.GetComponent<Rigidbody>();
            Debug.Log($"SHELF_AUDIT: cat={body.position} capsule={cat.GetComponent<CapsuleCollider>().bounds}");
        }
    }
}
