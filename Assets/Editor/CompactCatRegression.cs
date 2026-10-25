using System.IO;
using UnityEditor;
using UnityEngine;

// Runs existing live-input regressions serially; never saves the scene.
[InitializeOnLoad]
public static class CompactCatRegression
{
    static readonly string[] Menus = {
        "Catastrophe/Collision/Verify Table and Follow Camera",
        "Catastrophe/Collision/Verify Table and Follow Camera",
        "Catastrophe/Collision/Verify Table and Follow Camera",
        "Catastrophe/Verify Cat Tree Escape",
        "Catastrophe/Jump/Test Short And Long Jump In Play"
    };
    static readonly string[] Reports = {
        "Validation/TableCamera/live.txt", "Validation/TableCamera/live.txt",
        "Validation/TableCamera/live.txt", "Validation/CatTreeGap/escape.txt",
        "Validation/RefinedJump/play-check.txt"
    };
    static CompactCatRegression()
    {
        EditorApplication.playModeStateChanged += state => {
            if(state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("CompactCatSuite",false)) return;
            int i = SessionState.GetInt("CompactCatIndex",0);
            Directory.CreateDirectory("Validation/CompactCat");
            if(File.Exists(Reports[i])) File.Copy(Reports[i],$"Validation/CompactCat/run-{i+1}.txt",true);
            SessionState.SetInt("CompactCatIndex",i+1);
            EditorApplication.delayCall += Next;
        };
    }
    [MenuItem("Catastrophe/Collision/Repeat Compact Cat Checks")]
    public static void Run()
    {
        if(EditorApplication.isPlaying) return;
        SessionState.SetBool("CompactCatSuite",true);
        SessionState.SetInt("CompactCatIndex",0);
        Next();
    }
    static void Next()
    {
        int i=SessionState.GetInt("CompactCatIndex",0);
        if(i >= Menus.Length){SessionState.SetBool("CompactCatSuite",false);Debug.Log("COMPACT_CAT_SUITE_FINISHED");return;}
        EditorApplication.ExecuteMenuItem(Menus[i]);
    }
}
