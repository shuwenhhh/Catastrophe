using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DiningFurnitureClearance
{
    const string ScenePath = "Assets/LivingRoom.unity";
    static readonly string[] ChairNames = { "Chair 01", "Chair 02", "Chair 03" };
    static readonly string[] TableProps = { "MacBook", "Pink Vase", "Cup", "Cup 2", "iPad" };

    [MenuItem("Catastrophe/Collision/Increase Dining Size October 24")]
    public static void IncreaseDiningSize()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open LivingRoom in Edit Mode first.");
        const string backup="../ArtSource/DiningSizeBackup";
        Directory.CreateDirectory(backup);
        if(File.Exists(backup+"/applied.txt")) { Debug.Log("Dining size adjustment already applied."); return; }
        File.Copy(ScenePath,backup+"/LivingRoom-before.unity",false);
        var table=FindDiningObject("Table");var before=VisualBounds(table);
        ResizeFromBottom(table,new Vector3(1.10f,1.08f,1.10f));
        var after=VisualBounds(table);
        foreach(string name in TableProps)
        {
            var prop=FindDiningObject(name);Undo.RecordObject(prop,"Raise tabletop items");
            prop.position+=Vector3.up*(after.max.y-before.max.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
        }
        foreach(string name in ChairNames)
        {
            var chair=FindDiningObject(name);
            var offset=VisualBounds(chair).center-before.center;offset.y=0;
            ResizeFromBottom(chair,new Vector3(1.10f,1.15f,1.10f));
            chair.position+=offset*.10f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(chair);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(backup+"/applied.txt",$"Table {before.size} -> {after.size}\n"+string.Join("\n",ChairNames.Select(n=>n+" "+VisualBounds(FindDiningObject(n)).size)));
        Debug.Log("DINING_SIZE_INCREASED "+File.ReadAllText(backup+"/applied.txt"));
    }

    [MenuItem("Catastrophe/Collision/Enlarge Dining Table And Chairs")]
    public static void Enlarge()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open LivingRoom in Edit Mode before resizing furniture.");

        Transform table = FindDiningObject("Table");
        Bounds oldTable = VisualBounds(table);
        if (oldTable.size.y < .83f)
            ResizeFromBottom(table, new Vector3(1.05f, 1.14f, 1.05f));
        Bounds newTable = VisualBounds(table);
        float tabletopRise = newTable.max.y - oldTable.max.y;

        if (tabletopRise > .001f)
            foreach (string name in TableProps)
            {
                Transform prop = FindDiningObject(name);
                Undo.RecordObject(prop, "Keep prop on raised dining table");
                prop.position += Vector3.up * tabletopRise;
                PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
            }

        foreach (string name in ChairNames)
        {
            Transform chair = FindDiningObject(name);
            if (VisualBounds(chair).size.y < 1.02f)
                ResizeFromBottom(chair, new Vector3(1.10f, 1.27f, 1.10f));
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log($"Dining clearance updated: table {oldTable.size} -> {newTable.size}; " +
                  string.Join(", ", ChairNames.Select(n => n + " " + VisualBounds(FindDiningObject(n)).size)));
    }

    static Transform FindDiningObject(string name)
    {
        Transform art = GameObject.Find("Tripo Art Pass (editable)")?.transform;
        if (!art) throw new InvalidOperationException("Dining art root was not found.");
        Transform match = art.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (!match) throw new InvalidOperationException(name + " was not found in dining art.");
        return match;
    }

    static Bounds VisualBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no renderers.");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void ResizeFromBottom(Transform root, Vector3 factor)
    {
        float originalBottom = VisualBounds(root).min.y;
        Undo.RecordObject(root, "Enlarge dining furniture clearance");
        root.localScale = Vector3.Scale(root.localScale, factor);
        root.position += Vector3.up * (originalBottom - VisualBounds(root).min.y);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
    }

    [MenuItem("Catastrophe/Collision/Test Dining Furniture Passage In Play")]
    public static void TestInPlay()
    {
        if (!EditorApplication.isPlaying)
        {
            SessionState.SetBool("DiningPassageTest", true);
            EditorApplication.isPlaying = true;
            return;
        }
        RunPassageTest();
    }

    [InitializeOnLoadMethod]
    static void RegisterPlayTest()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("DiningPassageTest", false)) return;
            SessionState.SetBool("DiningPassageTest", false);
            EditorApplication.delayCall += RunPassageTest;
        };
    }

    static void RunPassageTest()
    {
        var cat = UnityEngine.Object.FindFirstObjectByType<CatController>();
        if (!cat) throw new InvalidOperationException("Runtime cat was not found.");
        var guard = cat.GetComponent<CatSolidCollision>();
        Transform table = FindDiningObject("Table");
        Bounds tableBounds = VisualBounds(table);
        var lines = new System.Collections.Generic.List<string>
        {
            "Runtime player=" + cat.name + " colliderParts=" + guard.Parts.Count,
            "Table visual=" + tableBounds + " top=" + tableBounds.max.y.ToString("F3")
        };
        foreach (string name in ChairNames)
        {
            Transform chair = FindDiningObject(name);
            var seat = chair.GetComponentsInChildren<MeshCollider>().First(c => c.name == "Seat");
            Bounds seatBounds = seat.bounds;
            lines.Add(name + " bounds=" + VisualBounds(chair) + " seat underside=" + seatBounds.min.y.ToString("F3"));
            if (name == "Chair 01")
            {
                int clear = 0;
                for (int i = -12; i <= 12; i++)
                {
                    Vector3 pose = new Vector3(seatBounds.center.x + i * .05f, .225f, seatBounds.center.z);
                    bool canPass = guard.CanOccupy(pose, Quaternion.LookRotation(Vector3.right));
                    if (canPass) clear++;
                    lines.Add($"Chair side passage x={pose.x:F2} z={pose.z:F2} clear={canPass}");
                }
                lines.Add("Chair side passage clear samples=" + clear + "/25");
                for (int i = -6; i <= 6; i++)
                {
                    Vector3 lane = new Vector3(seatBounds.center.x, .225f, seatBounds.center.z + i * .05f);
                    lines.Add($"Chair center lane z={lane.z:F2} clear={guard.CanOccupy(lane, Quaternion.LookRotation(Vector3.right))}");
                }
                lines.AddRange(ContactsAt(cat, guard,
                    new Vector3(seatBounds.center.x, .225f, seatBounds.center.z),
                    Quaternion.LookRotation(Vector3.right)));
                var body = cat.GetComponent<Rigidbody>();
                Vector3 originalPosition = body.position, originalVelocity = body.linearVelocity;
                Quaternion originalRotation = body.rotation;
                Vector3 centerPose = new Vector3(seatBounds.center.x, .225f, seatBounds.center.z);
                Quaternion sideFacing = Quaternion.LookRotation(Vector3.right);
                try
                {
                    body.position = centerPose;
                    body.rotation = sideFacing;
                    body.linearVelocity = Vector3.right * 1.5f;
                    Physics.SyncTransforms();
                    for (int step = 0; step < 10; step++) guard.UpdateTailClearance(.02f);
                    lines.Add("Chair center after tail duck clear=" + guard.CanOccupy(centerPose, sideFacing));
                    lines.AddRange(ContactsAt(cat, guard, centerPose, sideFacing));
                body.position = new Vector3(seatBounds.center.x - .75f, .225f, seatBounds.center.z);
                body.rotation = sideFacing;
                body.linearVelocity = Vector3.right * 1.5f;
                Physics.SyncTransforms();
                for (int step = 0; step < 90; step++)
                {
                    Vector3 allowed = guard.LimitHorizontalVelocity(Vector3.right * 1.5f, .02f);
                    body.position += allowed * .02f;
                    body.linearVelocity = allowed;
                    Physics.SyncTransforms();
                    guard.UpdateTailClearance(.02f);
                    if (step % 10 == 0)
                        lines.Add($"Chair walking step={step} x={body.position.x:F3} speed={allowed.x:F3} clear={guard.CanOccupy(body.position, sideFacing)}");
                }
                lines.Add("Chair walking exit=" + (body.position.x > seatBounds.center.x + .55f));
                lines.AddRange(ContactsAt(cat, guard, body.position + Vector3.right * .08f, body.rotation));
                foreach (float direction in new[] { 1f, -1f })
                {
                    Vector3 travel = Vector3.forward * direction;
                    body.position = new Vector3(seatBounds.center.x, .225f,
                        seatBounds.center.z - direction * .70f);
                    body.rotation = Quaternion.LookRotation(travel);
                    body.linearVelocity = travel * 1.5f;
                    Physics.SyncTransforms();
                    for (int step = 0; step < 10; step++) guard.UpdateTailClearance(.02f);
                    for (int step = 0; step < 90; step++)
                    {
                        Vector3 allowed = guard.LimitHorizontalVelocity(travel * 1.5f, .02f);
                        body.position += allowed * .02f;
                        body.linearVelocity = allowed;
                        Physics.SyncTransforms();
                        guard.UpdateTailClearance(.02f);
                    }
                    bool exited = direction * (body.position.z - seatBounds.center.z) > .55f;
                    lines.Add($"Chair lengthwise direction={direction} finalZ={body.position.z:F3} exit={exited}");
                    lines.AddRange(ContactsAt(cat, guard, body.position + travel * .08f, body.rotation));
                }
                }
                finally
                {
                    body.position = originalPosition;
                    body.rotation = originalRotation;
                    body.linearVelocity = originalVelocity;
                    Physics.SyncTransforms();
                    for (int step = 0; step < 10; step++) guard.UpdateTailClearance(.02f);
                }
            }
        }
        for (int i = -6; i <= 6; i++)
        {
            Vector3 pose = new Vector3(.55f, .225f, tableBounds.center.z + i * .08f);
            lines.Add($"Table underpass z={pose.z:F2} clear={guard.CanOccupy(pose, Quaternion.LookRotation(Vector3.forward))}");
        }
        Directory.CreateDirectory("Validation/DiningPassage");
        File.WriteAllLines("Validation/DiningPassage/live.txt", lines);
        Debug.Log(string.Join("\n", lines));
    }

    static System.Collections.Generic.IEnumerable<string> ContactsAt(CatController cat,
        CatSolidCollision guard, Vector3 position, Quaternion rotation)
    {
        var found = new System.Collections.Generic.List<string>();
        var shape = typeof(CatSolidCollision).GetMethod("Shape",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (CapsuleCollider part in guard.Parts)
        {
            object[] args = { part, position, rotation, Vector3.zero, Vector3.zero, 0f, Vector3.zero, Quaternion.identity };
            shape.Invoke(guard, args);
            Vector3 a = (Vector3)args[3], b = (Vector3)args[4];
            float radius = (float)args[5];
            Vector3 partPosition = (Vector3)args[6];
            Quaternion partRotation = (Quaternion)args[7];
            foreach (Collider other in Physics.OverlapCapsule(a, b, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (other.enabled && !other.isTrigger && !other.transform.IsChildOf(cat.transform) &&
                    Physics.ComputePenetration(part, partPosition, partRotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .003f)
                    found.Add($"CENTER BLOCK {part.name} vs {other.name} parent={other.transform.parent?.name} depth={depth:F3}");
        }
        return found;
    }
}
