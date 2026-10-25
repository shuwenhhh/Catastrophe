using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WindowCurtainCollisionRepair
{
    const string Folder="Validation/WindowCurtainCollision";

    [MenuItem("Catastrophe/Prevent Cat Crossing Window Curtain")]
    public static void Apply()
    {
        if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")
            throw new Exception("Open LivingRoom in Edit Mode first");
        var nook=GameObject.Find("Warm window nook");
        if(!nook)throw new Exception("Warm window nook is missing");
        var left=nook.transform.Find("Left pleated curtain");
        var right=nook.transform.Find("Right pleated curtain");
        if(!left||!right)throw new Exception("Expected both pleated curtains");
        if(left.Find("Curtain solid bands")||right.Find("Curtain solid bands"))
            throw new Exception("Curtain collision bands already exist; inspect rather than duplicating");
        Directory.CreateDirectory(Folder);
        File.Copy("Assets/LivingRoom.unity",Folder+"/Before.unity",true);
        int leftCount=WindowNookSetup.AddCurtainCollisionBands(left);
        int rightCount=WindowNookSetup.AddCurtainCollisionBands(right);
        PrefabUtility.SaveAsPrefabAsset(nook,"Assets/Art/LivingRoom/WindowNook/WarmWindowNook.prefab");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        File.WriteAllText(Folder+"/setup.txt",$"Left curtain: {leftCount} shallow upper collision bands. Right curtain: {rightCount}. Lower passage starts below 0.65m and stays open for E-hide.\n");
    }
}
