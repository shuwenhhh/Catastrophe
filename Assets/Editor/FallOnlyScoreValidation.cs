using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FallOnlyScoreValidation
{
    static double ready;
    static FallOnlyScoreValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TestFallOnly",false)){SessionState.SetBool("TestFallOnly",false);ready=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Check;}};}
    [MenuItem("Catastrophe/Verify Fall Only Scoring")]
    static void Run(){if(EditorApplication.isPlaying)throw new Exception("Stop Play first");SessionState.SetBool("TestFallOnly",true);EditorApplication.isPlaying=true;}
    static Bounds BoundsOf(GameObject g){var cs=g.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();var b=cs[0].bounds;foreach(var c in cs.Skip(1))b.Encapsulate(c.bounds);return b;}
    static void Check()
    {
        if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=Check;
        Scene fixture=default;var report=new List<string>();
        try{
            Object.FindFirstObjectByType<GameFlowUI>().BeginGame();
            var sources=Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None);
            var source=sources.First(v=>v.name.Contains("Vase",StringComparison.OrdinalIgnoreCase));
            report.Add("Runtime vase source: "+source.name);
            fixture=SceneManager.CreateScene("Disposable fall scoring",new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=fixture.GetPhysicsScene();
            var table=new GameObject("Test table");SceneManager.MoveGameObjectToScene(table,fixture);table.transform.position=new Vector3(0,.95f,0);table.AddComponent<BoxCollider>().size=new Vector3(3,.1f,3);
            var obj=Object.Instantiate(source.gameObject);SceneManager.MoveGameObjectToScene(obj,fixture);obj.transform.localScale=source.transform.lossyScale;
            var item=obj.GetComponent<Valuable>();var rb=obj.GetComponent<Rigidbody>();Physics.SyncTransforms();var b=BoundsOf(obj);obj.transform.position+=new Vector3(-b.center.x,1.005f-b.min.y,-b.center.z);Physics.SyncTransforms();item.ActivatePhysics();
            var update=typeof(Valuable).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance);
            var tick=typeof(Valuable).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
            int events=0;item.OnDestroyed+=_=>events++;int score=GameManager.I.chaosScore;
            obj.transform.rotation=Quaternion.Euler(0,0,90);Physics.SyncTransforms();b=BoundsOf(obj);obj.transform.position+=Vector3.up*(1.005f-b.min.y);Physics.SyncTransforms();
            for(int i=0;i<100;i++){tick.Invoke(item,null);physics.Simulate(.02f);update.Invoke(item,null);}
            item.MarkDestroyed();
            if(events!=0||GameManager.I.chaosScore!=score)throw new Exception("Toppled vase incorrectly scored");
            if(!item.TryNudge(Vector3.forward))throw new Exception("Toppled vase cannot be pushed again");
            report.Add("PASS vase lying sideways on tabletop: zero score/events; can still be nudged; direct premature score request rejected.");
            for(int i=0;i<50;i++){tick.Invoke(item,null);physics.Simulate(.02f);update.Invoke(item,null);}
            // Move beyond the fixture edge, then let actual gravity produce the fall.
            rb.position+=Vector3.right*3;Physics.SyncTransforms();
            for(int i=0;i<150;i++){tick.Invoke(item,null);physics.Simulate(.02f);update.Invoke(item,null);}
            if(events!=1||GameManager.I.chaosScore<=score)throw new Exception("True fall did not score exactly once");
            int after=GameManager.I.chaosScore;item.MarkDestroyed();update.Invoke(item,null);
            if(events!=1||GameManager.I.chaosScore!=after)throw new Exception("Duplicate score");
            report.Add($"PASS actual gravity fall scores once: {score} -> {after}; repeat updates do not score again.");report.Add("ALL PASS");
        }catch(Exception e){report.Add("FAIL "+e);Debug.LogException(e);}
        finally{Directory.CreateDirectory("Validation/Push");File.WriteAllLines("Validation/Push/FallOnlyScore.txt",report);if(fixture.IsValid())SceneManager.UnloadSceneAsync(fixture);EditorApplication.isPlaying=false;}
    }
}
