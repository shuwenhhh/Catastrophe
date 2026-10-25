using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class CatTreeGapValidation
{
    static CatController cat;
    static Rigidbody rb;
    static Camera camera;
    static Keyboard keys, originalKeyboard;
    static InputSettings settings;
    static InputSettings.EditorInputBehaviorInPlayMode originalBehavior;
    static Key[] held=Array.Empty<Key>();
    static int phase;
    static float until;
    static Vector3 origin;
    static readonly List<string> report=new();

    static CatTreeGapValidation()
    {
        EditorApplication.playModeStateChanged+=s=>
        {
            if(s!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("CatTreeEscape",false))return;
            SessionState.SetBool("CatTreeEscape",false);
            cat=null;phase=0;report.Clear();EditorApplication.update+=Tick;
        };
    }

    [MenuItem("Catastrophe/Verify Cat Tree Escape")]
    static void Start()
    {
        if(Application.isPlaying)return;
        SessionState.SetBool("CatTreeEscape",true);
        EditorApplication.isPlaying=true;
    }

    static void Feed()
    {
        if(keys!=null&&InputState.currentUpdateType!=InputUpdateType.Editor)
        {
            keys.MakeCurrent();InputState.Change(keys,new KeyboardState(held));
        }
    }
    static void SelectKeyboard(){keys?.MakeCurrent();}

    static void Tick()
    {
        try
        {
            if(!cat)
            {
                cat=UnityEngine.Object.FindFirstObjectByType<CatController>();
                if(!cat)return;
                UnityEngine.Object.FindFirstObjectByType<GameFlowUI>().BeginGame();
                rb=cat.GetComponent<Rigidbody>();camera=Camera.main;
                camera.GetComponent<ThirdPersonCamera>().enabled=false;
                settings=InputSystem.settings;originalBehavior=settings.editorInputBehaviorInPlayMode;
                settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                originalKeyboard=Keyboard.current;keys=InputSystem.AddDevice<Keyboard>();keys.MakeCurrent();
                InputSystem.onBeforeUpdate+=Feed;InputSystem.onAfterUpdate+=SelectKeyboard;
                var solid=cat.GetComponent<CatSolidCollision>();
                for(float z=-2.0f;z<=-1.0f;z+=.10f)
                {
                    var row="z="+z.ToString("F2")+" ";
                    for(float x=2.4f;x<=3.7f;x+=.10f)
                        row+=solid.CanOccupy(new Vector3(x,.228f,z),Quaternion.Euler(0,90,0))?'.':'#';
                    report.Add(row);
                }
                origin=new Vector3(3.60f,.228f,-1.72f);
                if(!solid.CanOccupy(origin,Quaternion.identity))
                    throw new Exception("Escape sample occupied; choose a free position from the survey grid");
                rb.position=origin;rb.rotation=Quaternion.identity;
                cat.transform.SetPositionAndRotation(origin,rb.rotation);rb.linearVelocity=Vector3.zero;
                camera.transform.rotation=Quaternion.identity;
                until=Time.time+.35f;phase=1;return;
            }
            if(Time.time<until)return;
            if(phase==1){held=new[]{Key.W};until=Time.time+.7f;phase=2;return;}
            if(phase==2)
            {
                var stopped=rb.position;
                report.Add("Advance toward shelf from="+origin+" to="+stopped);
                // Test a diagonal retreat into the surveyed clear +X side;
                // a perfectly opposite S input had hidden the corner trap.
                held=new[]{Key.S,Key.D};until=Time.time+1.4f;phase=3;return;
            }
            held=Array.Empty<Key>();
            var escaped=rb.position;
            float retreat=Vector3.Distance(escaped,origin);
            report.Add("Diagonal retreat final="+escaped+" distance from trap="+retreat.ToString("F3"));
            report.Add(retreat>.25f?"PASS diagonal retreat moves the runtime cat out of the narrow furniture gap":"FAIL cat did not leave the gap");
            Finish();
        }
        catch(Exception ex){report.Add("FAIL "+ex);Finish();}
    }

    static void Finish()
    {
        EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=Feed;InputSystem.onAfterUpdate-=SelectKeyboard;
        held=Array.Empty<Key>();if(keys!=null){InputSystem.RemoveDevice(keys);keys=null;}
        originalKeyboard?.MakeCurrent();if(settings)settings.editorInputBehaviorInPlayMode=originalBehavior;
        Directory.CreateDirectory("Validation/CatTreeGap");
        File.WriteAllLines("Validation/CatTreeGap/escape.txt",report);
        EditorApplication.isPlaying=false;
    }
}
