using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ShelfClearancePass
{
    static readonly float[] Old = { .38f, .95f, 1.52f, 2.09f };
    static readonly float[] New = { .38f, 1.05f, 1.72f, 2.39f };
    const string MeshPath = "Assets/Settings/LivingRoom/ReferenceShelf/Shelf_Clearance.asset";

    [MenuItem("Catastrophe/Reference Room/Give Shelf Real Cat Clearance")]
    public static void Apply()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/LivingRoom.unity")
            throw new InvalidOperationException("Open LivingRoom and exit Play first.");
        GameObject shelf = GameObject.Find("Tripo Shelving Unit");
        if (shelf == null) throw new InvalidOperationException("Shelf is missing.");
        MeshFilter filter = shelf.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Shelf mesh is missing.");
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(
            "Assets/Settings/LivingRoom/ReferenceShelf/Shelf_Finished.asset");
        if (source == null) throw new InvalidOperationException("Expected finished shelf mesh was not found.");
        Undo.RegisterFullObjectHierarchyUndo(shelf, "Give shelf cat clearance");
        Mesh clone = Object.Instantiate(source);
        clone.name = "Shelf with four matching oak boards";
        Vector3[] vertices = clone.vertices;
        // Preserve the source footprint at every level. The older clearance
        // pass extended level one and recessed the upper levels, leaving a
        // visual overhang and a pocket that trapped the cat beneath a board.
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = filter.transform.TransformPoint(vertices[i]);
            world.y = NewHeight(world.y);
            vertices[i] = filter.transform.InverseTransformPoint(world);
        }
        clone.vertices = vertices;
        clone.RecalculateBounds();
        clone.RecalculateNormals();
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (saved == null) { Directory.CreateDirectory(Path.GetDirectoryName(MeshPath)); AssetDatabase.CreateAsset(clone, MeshPath); saved = clone; }
        else { EditorUtility.CopySerialized(clone, saved); Object.DestroyImmediate(clone); EditorUtility.SetDirty(saved); }
        filter.sharedMesh = saved;
        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        for (int i = 0; i < Old.Length; i++)
        {
            Transform board = shelf.transform.Find($"Climbable Shelf Platform {i + 1:00}");
            if (board == null) throw new InvalidOperationException("Missing platform " + (i + 1));
            BoxCollider solid = board.GetComponent<BoxCollider>();
            Bounds before = solid.bounds;
            board.position += Vector3.up * (New[i] - before.max.y);
            const float targetWidth = .78f, targetCenter = 3.75f;
            board.position += Vector3.right * (targetCenter - before.center.x);
            Vector3 axis = board.TransformVector(Vector3.right);
            Vector3 size = solid.size;
            if (Mathf.Abs(axis.x) > Mathf.Abs(board.TransformVector(Vector3.forward).x)) size.x *= targetWidth / before.size.x;
            else size.z *= targetWidth / before.size.x;
            solid.size = size;
        }
        foreach (BoxCollider frame in shelf.GetComponentsInChildren<BoxCollider>(true)
                     .Where(c => c.name.StartsWith("Shelf frame collision")))
        {
            Vector3 p = frame.transform.position; p.y = New[3] * .5f; frame.transform.position = p;
            Vector3 size = frame.size; size.y *= New[3] / frame.bounds.size.y; frame.size = size;
        }
        string[] props = { "Tripo Book Stack", "Shelf Mug 01", "Shelf Books 02", "Shelf Cup 02",
            "Shelf Plant 02", "Shelf Books 03", "Desktop Globe", "Lucky Cat" };
        for (int i = 0; i < props.Length; i++)
        {
            GameObject prop = GameObject.Find(props[i]);
            if (prop == null) continue;
            Undo.RecordObject(prop.transform, "Raise shelf prop with board");
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            Bounds propBounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) propBounds.Encapsulate(renderer.bounds);
            prop.transform.position += Vector3.up * (New[i / 2] + .008f - propBounds.min.y);
            if (i / 2 >= 2) prop.transform.position += Vector3.right * (4.12f - propBounds.center.x);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
        }
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("SHELF_CLEARANCE: four identical shelf footprints, board tops 0.38/1.05/1.72/2.39m; mesh, colliders and props aligned.");
    }

    static float NewHeight(float y)
    {
        if (y <= Old[0]) return y;
        for (int i = 1; i < Old.Length; i++)
        {
            if (y < Old[i] - .046f)
                return Mathf.Lerp(New[i - 1], New[i] - .046f,
                    Mathf.InverseLerp(Old[i - 1], Old[i] - .046f, y));
            if (y <= Old[i]) return y + New[i] - Old[i];
        }
        return y + New[3] - Old[3];
    }
}
