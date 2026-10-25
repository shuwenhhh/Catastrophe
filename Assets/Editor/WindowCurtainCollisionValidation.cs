using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class WindowCurtainCollisionValidation
{
    const string Report="Validation/WindowCurtainCollision/play-check.txt";
    static double ready;
    static WindowCurtainCollisionValidation()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("TestCurtainCollision",false))return;
            SessionState.SetBool("TestCurtainCollision",false);
            ready=EditorApplication.timeSinceStartup+2;
            EditorApplication.update+=Check;
        };
    }

    [MenuItem("Catastrophe/Verify Cat Cannot Cross Curtain In Play")]
    static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        SessionState.SetBool("TestCurtainCollision",true);
        EditorApplication.isPlaying=true;
    }

    static void Check()
    {
        if(EditorApplication.timeSinceStartup<ready)return;
        EditorApplication.update-=Check;
        try
        {
            var cat=Object.FindFirstObjectByType<CatController>();
            var solid=cat?.GetComponent<CatSolidCollision>();
            var nook=GameObject.Find("Warm window nook");
            var hide=GameObject.Find("Hide Spot - Behind Curtain")?.GetComponent<HideSpot>();
            if(!cat||!solid||!nook||!hide)throw new Exception("Live cat, curtain or hide spot missing");
            var bands=nook.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Curtain collision band")).ToArray();
            if(bands.Length!=18||bands.Any(c=>c.isTrigger))throw new Exception("Expected 18 solid shallow curtain bands; found "+bands.Length);
            // Isolate the drape: the cup/books are separate valid blockers on this tabletop.
            foreach(var prop in Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None).Where(v=>v.name.StartsWith("Window ")))
                foreach(var collider in prop.GetComponentsInChildren<Collider>())collider.enabled=false;
            Physics.SyncTransforms();
            var rotation=Quaternion.Euler(0,90,0);
            // Begin away from the coffee cup and the stacked books; approach the right drape from the table.
            var start=new Vector3(1.45f,.988f,2.85f);
            if(!solid.CanOccupy(start,rotation))throw new Exception("Table-side starting position is blocked");
            if(solid.CanOccupy(new Vector3(2.40f,.988f,2.85f),rotation))throw new Exception("Cat can still occupy visible right curtain from the table");
            var rb=cat.GetComponent<Rigidbody>();cat.enabled=false;rb.isKinematic=true;
            rb.position=start;rb.rotation=rotation;cat.transform.SetPositionAndRotation(start,rotation);Physics.SyncTransforms();
            for(int i=0;i<45;i++)
            {
                var permitted=solid.LimitHorizontalVelocity(Vector3.right*1.8f,.02f);
                var next=rb.position+permitted*.02f;
                rb.position=next;cat.transform.position=next;Physics.SyncTransforms();
            }
            if(rb.position.x>2.21f)throw new Exception("Horizontal sweep entered curtain: x="+rb.position.x);
            float blockedSpeed=solid.LimitHorizontalVelocity(Vector3.right*1.8f,.02f).x;
            foreach(var band in bands)band.enabled=false;
            Physics.SyncTransforms();
            float freeSpeed=solid.LimitHorizontalVelocity(Vector3.right*1.8f,.02f).x;
            if(blockedSpeed>.15f||freeSpeed<1.0f)throw new Exception($"Sweep blocker was not the curtain (blocked={blockedSpeed:F3}, curtain off={freeSpeed:F3})");
            foreach(var band in bands)band.enabled=true;
            Physics.SyncTransforms();
            // The opposite drape must not seal the short jump from the sofa arm
            // onto the front half of the window console. Keep the cat's full
            // compound collision envelope, not just its central capsule, clear.
            var left=nook.transform.Find("Left pleated curtain");
            if(!left||Mathf.Abs(left.position.z-.22f)>.01f)
                throw new Exception("Sofa-side curtain was not tucked toward the window");
            var across=Quaternion.Euler(0f,90f,0f);
            for(int i=0;i<=16;i++)
            {
                var p=new Vector3(Mathf.Lerp(-.18f,.46f,i/16f),1.02f,2.42f);
                if(!solid.CanOccupy(p,across))
                    throw new Exception("Sofa-to-console jump lane blocked at "+p);
            }
            for(int i=0;i<=20;i++)
            {
                var p=Vector3.Lerp(hide.ExitAnchor.position,hide.HideAnchor.position,i/20f);
                if(!solid.CanOccupy(p,hide.HideAnchor.rotation))throw new Exception("Lower hide passage blocked at "+p);
            }
            Directory.CreateDirectory("Validation/WindowCurtainCollision");
            File.WriteAllText(Report,$"PASS 18 shallow solid curtain bands present.\nPASS table-height cat overlap rejected.\nPASS cat horizontal sweep stopped at x={rb.position.x:F3}; speed rose from {blockedSpeed:F3} to {freeSpeed:F3} when only curtain collisions were temporarily disabled.\nPASS 17 full-cat poses clear across the sofa-to-console jump lane.\nPASS 21 low hide-passage poses remain unobstructed.\n");
        }
        catch(Exception e)
        {
            Directory.CreateDirectory("Validation/WindowCurtainCollision");
            File.WriteAllText(Report,"FAIL "+e);
            Debug.LogException(e);
        }
        finally{EditorApplication.isPlaying=false;}
    }
}
