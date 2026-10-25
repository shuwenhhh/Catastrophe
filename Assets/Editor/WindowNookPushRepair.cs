using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class WindowNookPushRepair
{
    const string Report="Validation/WindowBooks";
    static readonly string[] BookNames={"Book pages","Book cover","Bound spine"};
    static readonly string[] CupNames={"Open cream coffee cup","Rounded cup handle","Dark coffee"};

    [MenuItem("Catastrophe/Make Window Table Props Pushable")]
    public static void Apply()
    {
        if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")
            throw new Exception("Open LivingRoom in Edit Mode first");
        var nook=GameObject.Find("Warm window nook");
        if(!nook)throw new Exception("Warm window nook missing from the scene");
        var root=nook.transform;
        if(root.Find("Window book stack - top")||root.Find("Window book stack - lower")||root.Find("Window coffee cup"))
            throw new Exception("Window table props are already grouped; inspect before applying again");
        var children=root.Cast<Transform>().ToArray();
        var books=children.Where(t=>BookNames.Contains(t.name)).ToArray();
        var top=books.Where(t=>t.GetComponent<Renderer>().bounds.center.y>.7f).ToArray();
        var lower=books.Where(t=>t.GetComponent<Renderer>().bounds.center.y<.5f).ToArray();
        var cup=children.Where(t=>CupNames.Contains(t.name)).ToArray();
        if(top.Length!=16||lower.Length!=12||cup.Length!=3)
            throw new Exception($"Unexpected window prop parts: top={top.Length}, lower={lower.Length}, cup={cup.Length}");

        Directory.CreateDirectory(Report);
        if(!File.Exists(Report+"/Before.unity"))File.Copy("Assets/LivingRoom.unity",Report+"/Before.unity");
        Group(root,"Window book stack - top",new Vector3(1.08f,.7625f,2.59f),top,35,false);
        Group(root,"Window book stack - lower",new Vector3(.90f,.214f,2.63f),lower,25,false);
        Group(root,"Window coffee cup",new Vector3(1.67f,.7625f,2.59f),cup,30,true);
        PrefabUtility.SaveAsPrefabAsset(nook,"Assets/Art/LivingRoom/WindowNook/WarmWindowNook.prefab");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        File.WriteAllText(Report+"/setup.txt",$"Top books: {top.Length} original visible parts; lower books: {lower.Length}; coffee cup: {cup.Length}. Each is one Rigidbody, BoxCollider and Valuable; original part colliders removed.\n");
    }

    static void Group(Transform parent,string name,Vector3 position,Transform[] parts,int score,bool fragile)
    {
        var group=new GameObject(name).transform;Undo.RegisterCreatedObjectUndo(group.gameObject,"Make window prop pushable");
        group.SetParent(parent,false);group.position=position;
        foreach(var part in parts)
        {
            foreach(var collider in part.GetComponents<Collider>())Object.DestroyImmediate(collider);
            if(!part.GetComponent<SoftDecoration>())part.gameObject.AddComponent<SoftDecoration>();
            Undo.SetTransformParent(part,group,"Group pushable window prop");
        }
        WindowNookSetup.FinalizePushable(group,score,fragile);
    }
}
