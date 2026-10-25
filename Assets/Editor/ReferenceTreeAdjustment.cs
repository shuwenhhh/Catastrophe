using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public static class ReferenceTreeAdjustment
{
    [MenuItem("Catastrophe/Align Reference Cat Tree")]
    public static void Build()
    {
        if(Application.isPlaying)throw new System.Exception("Stop Play first");
        Directory.CreateDirectory("Validation/ReferenceTree");
        if(!File.Exists("Validation/ReferenceTree/Before.unity"))
            File.Copy("Assets/LivingRoom.unity","Validation/ReferenceTree/Before.unity");
        var tree=GameObject.Find("Tripo Cat Tree");
        Undo.RegisterFullObjectHierarchyUndo(tree,"Reference cat tree route");
        var bounds=tree.GetComponentInChildren<Renderer>().bounds;
        tree.transform.position+=new Vector3(3.58f,0,-1.78f)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(tree.transform);
        Support(tree,"Cubby roof landing",new Vector3(3.58f,.80f,-1.78f),new Vector3(.65f,.06f,.76f));
        Support(tree,"Upper bed landing",new Vector3(3.39f,1.47f,-1.79f),new Vector3(.48f,.06f,.46f));
        // Preserve the original carpeted cubby, rope posts and rimmed beds.
        // The artificial staircase is retained disabled for easy recovery.
        var steps=GameObject.Find("Cat tree access perches");
        if(steps){Undo.RecordObject(steps,"Hide non-reference stepping posts");steps.SetActive(false);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        CatTreeRouteSetup.Inspect();
    }
    static void Support(GameObject tree,string name,Vector3 p,Vector3 size)
    {
        var old=tree.transform.Find(name);if(old)Undo.DestroyObjectImmediate(old.gameObject);
        var g=new GameObject(name);g.transform.position=p;g.AddComponent<BoxCollider>().size=size;
        g.transform.SetParent(tree.transform,true);
    }
}
