using UnityEngine;
using UnityEditor;

public static class FitCollidersToVisual
{
    [MenuItem("CATASTROPHE/Fit All Tripo Colliders To Visual")]
    public static void FitAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Models/Tripo/Prefabs" });
        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            // Chair keeps manual seat-height collider (don't let cat stand on backrest)
            if (prefab.name.Contains("Chair")) continue;
            BoxCollider collider = prefab.GetComponent<BoxCollider>();
            if (collider == null) continue;

            // Calculate combined renderer bounds in prefab local space
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;
            temp.transform.localScale = Vector3.one;
            Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Object.DestroyImmediate(temp); continue; }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            Object.DestroyImmediate(temp);

            // Convert world bounds to prefab local (prefab is at origin, identity)
            Vector3 size = bounds.size;
            Vector3 center = bounds.center;

            // Only update if significantly different (avoid noise)
            if ((collider.size - size).magnitude > 0.01f || (collider.center - center).magnitude > 0.01f)
            {
                Debug.Log($"{prefab.name}: {collider.size} -> {size}, center {collider.center} -> {center}", prefab);
                collider.size = size;
                collider.center = center;
                EditorUtility.SetDirty(prefab);
                fixedCount++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Fitted {fixedCount} Tripo prefab colliders to visual bounds.");
    }
}
