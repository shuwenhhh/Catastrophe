using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class WindowTableJumpValidation
{
    static CatController cat;static Rigidbody rb;static Camera cam;static Keyboard keys,priorKeyboard;
    static InputSettings priorSettings;static InputSettings.EditorInputBehaviorInPlayMode priorBehavior;static Key[] held=Array.Empty<Key>();
    static float apex;static int step,phase;static float ready,jumped;static Vector3 heading;static List<string> report;static bool sofaRoute;
    static Vector3[] feet={new Vector3(.35f,.7625f,2.58f)};
    static WindowTableJumpValidation(){EditorApplication.playModeStateChanged+=s=>{if(s!=PlayModeStateChange.EnteredPlayMode)return;bool sofa=SessionState.GetBool("SofaWindowJump",false);if(!sofa&&!SessionState.GetBool("WindowTableJump",false))return;SessionState.SetBool("SofaWindowJump",false);SessionState.SetBool("WindowTableJump",false);sofaRoute=sofa;feet=new[]{new Vector3(.35f,.7625f,2.58f)};cat=null;step=phase=0;report=new List<string>();EditorApplication.update+=Tick;};}
    [MenuItem("Catastrophe/Verify Live Window Table Jump")]
    static void Run(){if(Application.isPlaying)return;SessionState.SetBool("WindowTableJump",true);EditorApplication.isPlaying=true;}
    [MenuItem("Catastrophe/Verify Sofa To Window Table Jump")]
    static void RunSofa(){if(Application.isPlaying)return;SessionState.SetBool("SofaWindowJump",true);EditorApplication.isPlaying=true;}
    static void SelectKeyboard(){if(keys!=null)keys.MakeCurrent();}
    static void Feed(){if(keys!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keys.MakeCurrent();InputState.Change(keys,new KeyboardState(held));}}
    static void Tick(){try{
        if(!cat){cat=Object.FindFirstObjectByType<CatController>();if(!cat)return;Object.FindFirstObjectByType<GameFlowUI>().BeginGame();rb=cat.GetComponent<Rigidbody>();cam=Camera.main;cam.GetComponent<ThirdPersonCamera>().enabled=false;
            priorSettings=InputSystem.settings;priorBehavior=priorSettings.editorInputBehaviorInPlayMode;priorSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;priorKeyboard=Keyboard.current;keys=InputSystem.AddDevice<Keyboard>();keys.MakeCurrent();InputSystem.onBeforeUpdate+=Feed;InputSystem.onAfterUpdate+=SelectKeyboard;
            Vector3 start=new Vector3(.35f,.228f,1.45f);
            if(sofaRoute){var sofa=GameObject.Find("Sofa");if(!sofa)throw new Exception("Sofa missing");Collider arm=null;foreach(var c in sofa.GetComponentsInChildren<Collider>())if(c.name=="Soft arm"&&(arm==null||c.bounds.center.x>arm.bounds.center.x))arm=c;if(!arm)throw new Exception("Sofa arm collider missing");var b=arm.bounds;start=new Vector3(b.center.x,b.max.y+.225f,Mathf.Clamp(2.58f,b.min.z+.12f,b.max.z-.12f));report.Add("Sofa arm bounds="+b+" start="+start);}
            rb.position=start;rb.rotation=Quaternion.identity;cat.transform.SetPositionAndRotation(rb.position,rb.rotation);rb.linearVelocity=Vector3.zero;ready=Time.time+.3f;}
        if(Time.time<ready)return;
        if(step>=feet.Length){report.Add("PASS "+(sofaRoute?"sofa-to-window":"floor-to-window")+" table jump and stable landing driven by W/Space on actual CatController, no intermediate teleport.");Finish();return;}
        if(phase==0){heading=feet[step]-(rb.position-Vector3.up*.225f);heading.y=0;heading.Normalize();cam.transform.rotation=Quaternion.LookRotation(heading);held=new[]{Key.W};phase=1;ready=Time.time+.02f;return;}
        if(phase==1){if(Quaternion.Angle(rb.rotation,Quaternion.LookRotation(heading))>8f)return;held=new[]{Key.W,Key.Space};phase=2;apex=0;jumped=Time.time;ready=Time.time+.06f;return;}
        if(phase==2){apex=Mathf.Max(apex,rb.position.y-.225f);held=Time.time-jumped<.30f?new[]{Key.W,Key.Space}:new[]{Key.W};Vector3 delta=feet[step]-(rb.position-Vector3.up*.225f);delta.y=0;
            if(Vector3.Dot(delta,heading)<.035f||Time.time-jumped>3f){report.Add("At input release foot="+(rb.position-Vector3.up*.225f)+" time="+(Time.time-jumped));held=Array.Empty<Key>();phase=3;ready=Time.time+.7f;}return;}
        Vector3 foot=rb.position-Vector3.up*.225f;float error=Vector3.Distance(foot,feet[step]);report.Add("Jump "+(step+1)+" target="+feet[step]+" landed="+foot +" apex="+apex+" error="+error);
        Capture(step);
        if(Mathf.Abs(foot.y-feet[step].y)>.04f || error>.22f)throw new Exception("Live jump "+(step+1)+" failed");step++;phase=0;ready=Time.time+.1f;
    }catch(Exception e){report.Add("FAIL "+e.Message);Finish();}}
    static void Capture(int index){Directory.CreateDirectory("Validation/WindowTableJump");cam.transform.position=new Vector3(-.3f,1.9f,.5f);cam.transform.LookAt(new Vector3(1.25f,1f,2.65f));cam.orthographic=true;cam.orthographicSize=1.1f;var rt=new RenderTexture(1000,900,24);var prev=RenderTexture.active;cam.targetTexture=rt;try{cam.Render();RenderTexture.active=rt;var t=new Texture2D(1000,900,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1000,900),0,0);t.Apply();File.WriteAllBytes("Validation/WindowTableJump/"+(sofaRoute?"SofaJump":"Jump")+index+".png",t.EncodeToPNG());Object.DestroyImmediate(t);}finally{cam.targetTexture=null;RenderTexture.active=prev;Object.DestroyImmediate(rt);}}
    static void Finish(){EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=Feed;InputSystem.onAfterUpdate-=SelectKeyboard;held=Array.Empty<Key>();if(keys!=null){InputSystem.RemoveDevice(keys);keys=null;}priorKeyboard?.MakeCurrent();if(priorSettings)priorSettings.editorInputBehaviorInPlayMode=priorBehavior;Directory.CreateDirectory("Validation/WindowTableJump");File.WriteAllLines("Validation/WindowTableJump/"+(sofaRoute?"sofa-live.txt":"live.txt"),report);EditorApplication.isPlaying=false;}
}
