using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WindowBookPushValidation
{
    const string Report = "Validation/WindowBooks/play-check.txt";
    static double ready;
    static CatController cat;
    static Valuable books;
    static Rigidbody body;
    static float initialY;
    static int presses;
    static int falls;
    static readonly List<string> lines = new List<string>();

    static WindowBookPushValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("WindowBookPushTest", false)) return;
            SessionState.SetBool("WindowBookPushTest", false);
            ready = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Catastrophe/Verify Window Books Push In Play")]
    static void Run()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play first");
        lines.Clear();presses=0;falls=0;cat=null;books=null;body=null;
        SessionState.SetBool("WindowBookPushTest", true);
        EditorApplication.isPlaying=true;
    }

    static void Tick()
    {
        if(EditorApplication.timeSinceStartup < ready) return;
        try
        {
            if(cat==null)
            {
                cat=Object.FindFirstObjectByType<CatController>();
                books=Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None).FirstOrDefault(v=>v.name=="Window book stack - top");
                var lower=Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None).FirstOrDefault(v=>v.name=="Window book stack - lower");
                var cup=Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None).FirstOrDefault(v=>v.name=="Window coffee cup");
                if(!cat||!books||!lower||!cup)throw new Exception("Missing live cat, top books, lower books or coffee cup");
                foreach(var item in new[]{books,lower,cup})
                {
                    if(!item.GetComponent<Rigidbody>()||!item.GetComponent<BoxCollider>())throw new Exception(item.name+" lacks push components");
                    if(item.GetComponentsInChildren<Collider>().Count(c=>c.enabled&&!c.isTrigger)!=1)throw new Exception(item.name+" has extra part colliders");
                }
                lines.Add("PASS all three visible table props have one Rigidbody, one BoxCollider and Valuable");
                Object.FindFirstObjectByType<GameFlowUI>()?.BeginGame();
                cat.enabled=false;
                cat.GetComponent<Rigidbody>().isKinematic=true;
                body=books.GetComponent<Rigidbody>();initialY=body.position.y;
                Valuable.AnyDestroyed += OnFall;
            }

            if(presses>0)
            {
                if(presses==1)
                {
                    var delta=body.position.z - 2.59f;
                    if(delta<.08f||delta>.24f)throw new Exception("First book push distance incorrect: "+delta);
                    if(falls!=0)throw new Exception("Books scored before falling");
                    lines.Add("PASS first push moved books "+delta.ToString("F3")+" m without scoring");
                }
                if(falls>0)
                {
                    if(initialY-body.position.y<.20f)throw new Exception("Books scored before dropping 0.20m");
                    lines.Add("PASS books fell after "+presses+" separate pushes; score fired only after the fall");
                    Finish(true);return;
                }
                if(presses>=8)throw new Exception("Books did not fall after eight pushes");
            }

            var bounds=books.GetComponent<BoxCollider>().bounds;
            var pose=new Vector3(bounds.center.x,bounds.min.y+.228f,bounds.min.z-.30f);
            cat.transform.SetPositionAndRotation(pose,Quaternion.identity);
            Physics.SyncTransforms();
            if(!cat.TryPushNearby())throw new Exception("Cat could not select books on push "+(presses+1));
            presses++;
            lines.Add("Push "+presses+" accepted at z="+body.position.z.ToString("F3"));
            ready=EditorApplication.timeSinceStartup+1.1;
        }
        catch(Exception exception)
        {
            lines.Add("FAIL "+exception);
            Debug.LogException(exception);
            Finish(false);
        }
    }

    static void OnFall(Valuable item){if(item==books)falls++;}

    static void Finish(bool pass)
    {
        EditorApplication.update-=Tick;
        Valuable.AnyDestroyed-=OnFall;
        lines.Add(pass?"ALL PASS":"VALIDATION FAILED");
        Directory.CreateDirectory("Validation/WindowBooks");
        File.WriteAllLines(Report,lines);
        EditorApplication.isPlaying=false;
    }
}
