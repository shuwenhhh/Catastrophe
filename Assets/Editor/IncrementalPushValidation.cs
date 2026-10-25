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
public static class IncrementalPushValidation
{
    static double ready;
    static IncrementalPushValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TestIncrementalPush",false)){SessionState.SetBool("TestIncrementalPush",false);ready=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Check;}};}
    [MenuItem("Catastrophe/Verify Incremental Paw Push")]
    static void Run(){if(EditorApplication.isPlaying)throw new Exception("Stop Play first");SessionState.SetBool("TestIncrementalPush",true);EditorApplication.isPlaying=true;}
    static Bounds BoundsOf(GameObject g){var cs=g.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();var b=cs[0].bounds;foreach(var c in cs.Skip(1))b.Encapsulate(c.bounds);return b;}
    static void Check()
    {
        if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=Check;
        var report=new List<string>();Scene fixture=default;
        try{
            var sources=Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None);
            var mug=sources.First(v=>v.name=="Cup");
            var laptop=sources.First(v=>v.name=="MacBook");
            report.Add("Runtime sources: "+mug.name+", "+laptop.name);
            // Real player target-selection path; no authored transforms are saved.
            var cat=Object.FindFirstObjectByType<CatController>();var cb=cat.GetComponent<Rigidbody>();cat.enabled=false;cb.isKinematic=true;
            var mb=BoundsOf(mug.gameObject);cat.transform.SetPositionAndRotation(new Vector3(mb.center.x,mb.min.y+.228f,mb.min.z-.30f),Quaternion.identity);Physics.SyncTransforms();
            if(!cat.TryPushNearby())throw new Exception("Actual cat could not select nearby cup");
            report.Add("PASS actual CatController.TryPushNearby selected a reachable cup");
            fixture=SceneManager.CreateScene("Disposable nudge fixture",new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=fixture.GetPhysicsScene();
            var floor=new GameObject("Test tabletop");SceneManager.MoveGameObjectToScene(floor,fixture);var table=floor.AddComponent<BoxCollider>();table.size=new Vector3(4,.10f,2);floor.transform.position=new Vector3(0,.95f,0);
            var tick=typeof(Valuable).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
            foreach(var source in new[]{mug,laptop})foreach(float margin in new[]{.22f,.62f}){
                var obj=Object.Instantiate(source.gameObject);SceneManager.MoveGameObjectToScene(obj,fixture);obj.transform.localScale=source.transform.lossyScale;obj.transform.rotation=Quaternion.identity;
                var item=obj.GetComponent<Valuable>();var rb=obj.GetComponent<Rigidbody>();rb.isKinematic=true;
                Physics.SyncTransforms();var bounds=BoundsOf(obj);obj.transform.position+=new Vector3(-bounds.center.x,1.005f-bounds.min.y,1-margin-rb.worldCenterOfMass.z);Physics.SyncTransforms();
                float initialY=rb.position.y;int fell=0;
                for(int press=1;press<=8;press++){
                    float before=rb.position.z;
                    if(!item.TryNudge(Vector3.forward))throw new Exception("Nudge rejected "+source.name);
                    for(int i=0;i<70;i++){tick.Invoke(item,null);physics.Simulate(.02f);}
                    report.Add($"{source.name} margin={margin:F2} press={press} delta={rb.position.z-before:F3} drop={initialY-rb.position.y:F3}");
                    if(initialY-rb.position.y>.20f){fell=press;break;}
                    if(press==1 && (rb.position.z-before<.09f||rb.position.z-before>.22f))throw new Exception("First step not controlled");
                }
                if(fell==0)throw new Exception("Never fell off tabletop: "+source.name);
                if(margin<.3f && (fell<2||fell>3))throw new Exception("Near edge should take about two pushes");
                if(margin>.3f && (fell<4||fell>6))throw new Exception("Far edge should take four to six pushes");
                report.Add($"PASS {source.name} margin {margin:F2} falls after {fell} presses");Object.DestroyImmediate(obj);
            }
            var clip=(AudioClip)typeof(AudioManager).GetField("pawPush",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(AudioManager.I);
            if(!clip.name.Contains("single")||clip.length>.2f)throw new Exception("Not single du audio");
            report.Add("PASS single du clip duration="+clip.length);report.Add("ALL PASS");
        }catch(Exception e){report.Add("FAIL "+e);Debug.LogException(e);}
        finally{Directory.CreateDirectory("Validation/Push");File.WriteAllLines("Validation/Push/check.txt",report);if(fixture.IsValid())SceneManager.UnloadSceneAsync(fixture);EditorApplication.isPlaying=false;}
    }
}
