using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CatTreeGapDiagnostic
{
    [MenuItem("Catastrophe/Open Cat Tree Passage")]
    static void OpenPassage()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/LivingRoom.unity")
            throw new System.InvalidOperationException("Open LivingRoom in Edit Mode");
        var tree = GameObject.Find("Reference carpet cat tree");
        if (!tree) throw new System.InvalidOperationException("Reference cat tree is missing");
        Directory.CreateDirectory("Validation/CatTreeGap");
        const string backup = "Validation/CatTreeGap/Before.unity";
        if (!File.Exists(backup)) File.Copy("Assets/LivingRoom.unity", backup);
        Undo.RecordObject(tree.transform, "Open passage beside cat tree");
        tree.transform.localPosition = new Vector3(-.8f, 0, 0);
        PrefabUtility.SaveAsPrefabAsset(tree, "Assets/Art/LivingRoom/ReferenceCatTree/ReferenceCatTree.prefab");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Moved the complete cat tree 0.8 m left to clear the shelf entrance.");
    }
    [MenuItem("Catastrophe/Inspect Cat Tree Gap")]
    static void Inspect()
    {
        Directory.CreateDirectory("Validation/CatTreeGap");
        var lines = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(c => !c.isTrigger && c.bounds.Intersects(new Bounds(new Vector3(3.5f, .8f, -1.6f), new Vector3(3.5f, 2.5f, 3f))))
            .Select(c => $"{c.transform.GetHierarchyPath()} | {c.GetType().Name} | center={c.bounds.center:F3} size={c.bounds.size:F3}")
            .OrderBy(s => s).ToArray();
        File.WriteAllLines("Validation/CatTreeGap/colliders.txt", lines);
        Debug.Log($"Cat tree gap: {lines.Length} nearby solid colliders listed in Validation/CatTreeGap/colliders.txt");
    }
    static string GetHierarchyPath(this Transform t) => t.parent == null ? t.name : GetHierarchyPath(t.parent) + "/" + t.name;
}
