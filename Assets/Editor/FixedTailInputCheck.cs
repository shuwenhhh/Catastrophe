using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class FixedTailInputCheck
{
    static Keyboard keyboard,previous;static CatController cat;static Rigidbody body;
    static ThirdPersonCamera follow;static Vector3 start;static float began,apex;static int stage;static Key[] keys=new Key[0];
    static InputSettings.EditorInputBehaviorInPlayMode inputMode;static bool background;
    static readonly System.Collections.Generic.List<string> report=new();
    static FixedTailInputCheck(){EditorApplication.update+=Tick;}
    static void Feed(){if(keyboard!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keyboard.MakeCurrent();InputState.Change(keyboard,new KeyboardState(keys));}}
    static void Tick()
    {
        const string dir="Validation/FixedTail";
        if(File.Exists(dir+"/input.request")&&EditorApplication.isPlaying&&!EditorApplication.isCompiling&&stage==0)
        {
            File.Delete(dir+"/input.request");cat=Object.FindFirstObjectByType<CatController>();if(!cat)return;
            var flow=Object.FindFirstObjectByType<GameFlowUI>();
            // Do not begin a new run or capture/clear any save.
            if(flow.State!=GameFlowUI.ScreenState.MainMenu){File.WriteAllText(dir+"/input-checks.txt","NOT RUN: requires fresh main menu to preserve current run.");return;}
            flow.ResumeGame();body=cat.GetComponent<Rigidbody>();body.position=new Vector3(0,.225f,-2.3f);body.rotation=Quaternion.identity;
            cat.transform.SetPositionAndRotation(body.position,body.rotation);Physics.SyncTransforms();start=body.position;
            follow=Camera.main.GetComponent<ThirdPersonCamera>();if(follow)follow.enabled=false;
            Camera.main.transform.SetPositionAndRotation(start+new Vector3(1,1,-1.5f),Quaternion.Euler(25,0,0));
            previous=Keyboard.current;inputMode=InputSystem.settings.editorInputBehaviorInPlayMode;background=Application.runInBackground;
            Application.runInBackground=true;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=Feed;keys=new[]{Key.W};stage=1;began=Time.time;report.Clear();
        }
        if(stage==0||!cat)return;
        float elapsed=Time.time-began;
        if(stage==1&&elapsed>2.5f)
        {report.Add((body.position.z-start.z>.8f?"PASS":"FAIL")+" real W input through chair front/back lane: distance="+(body.position.z-start.z));keys=new Key[0];stage=2;began=Time.time;}
        else if(stage==2&&elapsed>.5f)
        {report.Add((Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude<.03f?"PASS":"FAIL")+" release stops movement");body.position=new Vector3(0,.225f,-2.3f);cat.transform.position=body.position;Physics.SyncTransforms();keys=new[]{Key.Space};stage=3;began=Time.time;apex=body.position.y;}
        else if(stage==3){apex=Mathf.Max(apex,body.position.y);if(elapsed>.25f)keys=new Key[0];if(elapsed>2){report.Add((apex>.4f&&body.position.y<.3f?"PASS":"FAIL")+" jump and landing apex="+apex+" finalY="+body.position.y);Finish(dir);}}
    }
    static void Finish(string dir)
    {
        keys=new Key[0];InputSystem.onBeforeUpdate-=Feed;InputSystem.RemoveDevice(keyboard);keyboard=null;previous?.MakeCurrent();InputSystem.settings.editorInputBehaviorInPlayMode=inputMode;Application.runInBackground=background;
        if(follow)follow.enabled=true;stage=0;File.WriteAllLines(dir+"/input-checks.txt",report);
    }
}
