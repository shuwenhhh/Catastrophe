using UnityEditor;
using UnityEngine;

public static class TableCollisionAuthoring
{
    [MenuItem("Catastrophe/Collision/Prepare Original Table Mesh")]
    public static void Run()
    {
        const string path="Assets/Models/Tripo/Prefabs/Tripo_Table.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.sharedMesh) continue;
                var collider=filter.GetComponent<MeshCollider>();
                if(!collider) collider=filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh=filter.sharedMesh;
                collider.convex=false;
                collider.isTrigger=false;
            }
            foreach(var box in root.GetComponentsInChildren<BoxCollider>())
                if(!box.isTrigger)box.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Debug.Log("Original table collision prepared before static batching.");
    }
}
