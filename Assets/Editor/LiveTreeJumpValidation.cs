using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class LiveTreeJumpValidation
{
    static CatController cat;static Rigidbody rb;static Camera cam;static Keyboard keys,priorKeyboard;
    static InputSettings priorSettings;static InputSettings.EditorInputBehaviorInPlayMode priorBehavior;static Key[] held=Array.Empty<Key>();
    static float apex;static int step,phase;static float ready,jumped;static Vector3 heading;static List<string> report;
    static readonly Vector3[] feet={new Vector3(2.85f,.67f,-2.43f),new Vector3(2.30f,1.16f,-1.95f),new Vector3(2.88f,1.69f,-1.75f),new Vector3(3.6f,2.09f,-.85f)};
    static LiveTreeJumpValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("LiveTree",false)){SessionState.SetBool("LiveTree",false);cat=null;step=phase=0;report=new List<string>();EditorApplication.update+=Tick;}};}
    [MenuItem("Catastrophe/Verify Live Keyboard Tree Jumps")]
    static void Run(){if(Application.isPlaying)return;SessionState.SetBool("LiveTree",true);EditorApplication.isPlaying=true;}
    static void SelectKeyboard(){if(keys!=null)keys.MakeCurrent();}
    static void Feed(){if(keys!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keys.MakeCurrent();InputState.Change(keys,new KeyboardState(held));}}
    static void Tick(){try{
        if(!cat){cat=Object.FindFirstObjectByType<CatController>();if(!cat)return;Object.FindFirstObjectByType<GameFlowUI>().BeginGame();rb=cat.GetComponent<Rigidbody>();cam=Camera.main;cam.GetComponent<ThirdPersonCamera>().enabled=false;
            priorSettings=InputSystem.settings;priorBehavior=priorSettings.editorInputBehaviorInPlayMode;priorSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;priorKeyboard=Keyboard.current;keys=InputSystem.AddDevice<Keyboard>();keys.MakeCurrent();InputSystem.onBeforeUpdate+=Feed;InputSystem.onAfterUpdate+=SelectKeyboard;
            rb.position=new Vector3(1.75f,.228f,-2.43f);rb.rotation=Quaternion.Euler(0,90,0);cat.transform.SetPositionAndRotation(rb.position,rb.rotation);rb.linearVelocity=Vector3.zero;ready=Time.time+.3f;}
        if(Time.time<ready)return;
        if(step>=feet.Length){report.Add("PASS 4 consecutive jumps driven by W/Space on actual CatController, no intermediate teleport.");Finish();return;}
        if(phase==0){heading=feet[step]-(rb.position-Vector3.up*.225f);heading.y=0;heading.Normalize();cam.transform.rotation=Quaternion.LookRotation(heading);held=new[]{Key.W};phase=1;ready=Time.time+.02f;return;}
        if(phase==1){if(Quaternion.Angle(rb.rotation,Quaternion.LookRotation(heading))>8f)return;held=new[]{Key.W,Key.Space};phase=2;apex=0;jumped=Time.time;ready=Time.time+.06f;return;}
        if(phase==2){apex=Mathf.Max(apex,rb.position.y-.225f);held=new[]{Key.W};Vector3 delta=feet[step]-(rb.position-Vector3.up*.225f);delta.y=0;
            if(Vector3.Dot(delta,heading)<.035f||Time.time-jumped>3f){held=Array.Empty<Key>();phase=3;ready=Time.time+.7f;}return;}
        Vector3 foot=rb.position-Vector3.up*.225f;float error=Vector3.Distance(foot,feet[step]);report.Add("Jump "+(step+1)+" target="+feet[step]+" landed="+foot +" apex="+apex+" error="+error);
        Capture(step);
        if(Mathf.Abs(foot.y-feet[step].y)>.04f || error>.22f)throw new Exception("Live jump "+(step+1)+" failed");step++;phase=0;ready=Time.time+.1f;
    }catch(Exception e){report.Add("FAIL "+e.Message);Finish();}}
    static void Capture(int index){Directory.CreateDirectory("Validation/ReferenceTree");cam.transform.position=new Vector3(.9f,2.3f,-1.4f);cam.transform.LookAt(new Vector3(3.1f,1.1f,-1.4f));cam.orthographic=true;cam.orthographicSize=1.5f;var rt=new RenderTexture(1000,900,24);var prev=RenderTexture.active;cam.targetTexture=rt;try{cam.Render();RenderTexture.active=rt;var t=new Texture2D(1000,900,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1000,900),0,0);t.Apply();File.WriteAllBytes("Validation/ReferenceTree/Jump"+index+".png",t.EncodeToPNG());Object.DestroyImmediate(t);}finally{cam.targetTexture=null;RenderTexture.active=prev;Object.DestroyImmediate(rt);}}
    static void Finish(){EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=Feed;InputSystem.onAfterUpdate-=SelectKeyboard;held=Array.Empty<Key>();if(keys!=null){InputSystem.RemoveDevice(keys);keys=null;}priorKeyboard?.MakeCurrent();if(priorSettings)priorSettings.editorInputBehaviorInPlayMode=priorBehavior;Directory.CreateDirectory("Validation/ReferenceTree");File.WriteAllLines("Validation/ReferenceTree/live.txt",report);EditorApplication.isPlaying=false;}
}
