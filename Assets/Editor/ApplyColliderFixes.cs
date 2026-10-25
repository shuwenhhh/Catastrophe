using UnityEngine;
using UnityEditor;

public static class ApplyColliderFixes
{
    [MenuItem("CATASTROPHE/Apply All Collider Fixes")] 
    public static void ApplyAll()
    {
        // Chair: keep width/depth, fix height 1.676 -> 0.94 (no standing on invisible box)
        FixPrefab("Tripo_Chair", new Vector3(0.8672461f, 0.94f, 1.1376578f), null);
        // Table: only cover tabletop, leave space underneath for cat to walk through
        FixPrefab("Tripo_Table", new Vector3(1.3340423f, 0.093f, 0.75999993f), new Vector3(0f, 0.276f, -0.38f));
        // Sofa: restore original (was incorrectly shrunk)
        FixPrefab("Tripo_Sofa", new Vector3(2.213708f, 0.81280786f, 0.92f), null);
        // TV Cabinet: restore original (was incorrectly shrunk)
        FixPrefab("Tripo_TvCabinet", new Vector3(3.5564294f, 0.968223f, 0.92000014f), null);
        
        AssetDatabase.SaveAssets();
        Debug.Log("Collider fixes applied. Press Ctrl+S to save.");
    }

    private static void FixPrefab(string prefabName, Vector3 size, Vector3? center)
    {
        string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab", new[] { "Assets/Models/Tripo/Prefabs" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !prefab.name.Contains(prefabName)) continue;
            BoxCollider c = prefab.GetComponent<BoxCollider>();
            if (c == null) continue;
            c.size = size;
            if (center.HasValue) c.center = center.Value;
            EditorUtility.SetDirty(prefab);
            Debug.Log($"Fixed {prefab.name}: size={size}" + (center.HasValue ? $", center={center.Value}" : ""));
        }
    }
}
