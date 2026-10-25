using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CatDirectionValidation
{
    const string Dir="Validation/CatDirection";
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static CatController cat;static Rigidbody rb;static ThirdPersonCamera view;static Camera camera;
    static Keyboard keyboard,previousKeyboard;static int test,phase;static double ready;
    static InputSettings previousSettings;static InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
    static Vector3 start,desired;static Quaternion stopped;static List<string> report;
    static CatDirectionValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CatDirectionTest",false)){SessionState.SetBool("CatDirectionTest",false);test=phase=0;report=new List<string>();ready=Time.timeAsDouble+2;EditorApplication.update+=Tick;}};}
    [MenuItem("Catastrophe/Verify Screen Relative Movement")]
    static void Run(){if(Application.isPlaying)throw new Exception("Stop Play first");SessionState.SetBool("CatDirectionTest",true);EditorApplication.isPlaying=true;}
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static Key[] heldKeys=Array.Empty<Key>();
    static void Keys(params Key[] keys){heldKeys=keys;InputSystem.onBeforeUpdate-=FeedInput;InputSystem.onBeforeUpdate+=FeedInput;}
    static void FeedInput(){if(keyboard!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(heldKeys));}}
    static void Tick(){if(cat && Time.timeAsDouble<ready)return;try{
        if(!cat){cat=Object.FindFirstObjectByType<CatController>();rb=cat.GetComponent<Rigidbody>();view=Object.FindFirstObjectByType<ThirdPersonCamera>();camera=view.GetComponent<Camera>();Object.FindFirstObjectByType<GameFlowUI>().BeginGame();previousSettings=InputSystem.settings;previousBehavior=previousSettings.editorInputBehaviorInPlayMode;previousSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();view.enabled=false;Directory.CreateDirectory(Dir);report.Add("Live player="+cat.name+" skin="+cat.GetComponentInChildren<SkinnedMeshRenderer>().name+" animator="+cat.GetComponentInChildren<Animator>().name);}
        if(test<16){
            if(phase==0){Keys();rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.position=new Vector3(-2f,.228f,-1.8f);rb.rotation=Quaternion.identity;cat.transform.SetPositionAndRotation(rb.position,rb.rotation);float yaw=(test/4)*90f;camera.transform.rotation=Quaternion.Euler(20,yaw,0);camera.transform.position=rb.position-camera.transform.forward*2.8f;phase=1;ready=Time.timeAsDouble+.15;return;}
            if(phase==1){Vector2[] inputs={Vector2.up,Vector2.left,Vector2.right,Vector2.down};Key[] keys={Key.W,Key.A,Key.D,Key.S};desired=CatController.CameraRelativeDirection(inputs[test%4],camera.transform.rotation);start=rb.position;Keys(keys[test%4]);phase=2;ready=Time.timeAsDouble+1.1;return;}
            if(phase==2){var delta=Vector3.ProjectOnPlane(rb.position-start,Vector3.up);Require(delta.magnitude>.10f,"No movement case "+test+" delta="+delta+" key="+Keyboard.current?.wKey.isPressed+" input="+typeof(CatController).GetField("moveDirection",Flags).GetValue(cat)+" velocity="+rb.linearVelocity+" yaw="+rb.rotation.eulerAngles+" gameplay="+GameFlowUI.GameplayInputEnabled+" timeScale="+Time.timeScale);Require(Vector3.Dot(delta.normalized,desired)>.97f,"Incorrect screen direction "+test);Require(Vector3.Dot(rb.rotation*Vector3.forward,desired)>.98f,"Facing mismatch "+test);Require(cat.GetComponentInChildren<Animator>().GetFloat("Speed")>.1f,"Walk not active "+test);Keys();phase=3;ready=Time.timeAsDouble+.12;return;}
            if(phase==3){stopped=rb.rotation;rb.AddTorque(Vector3.up*3,ForceMode.Impulse);phase=4;ready=Time.timeAsDouble+.35;return;}
            Require(Quaternion.Angle(stopped,rb.rotation)<.25f,"Idle rotation drift case "+test);Require(Vector3.ProjectOnPlane(rb.linearVelocity,Vector3.up).magnitude<.015f,"Idle sliding case "+test);Require(cat.GetComponentInChildren<Animator>().GetFloat("Speed")<.01f,"Idle Speed not zero");report.Add("PASS camera yaw "+(test/4)*90+" key "+new[]{"W","A","D","S"}[test%4]+": screen direction, facing, Walk, Idle and torque-resistant stop");test++;phase=0;ready=Time.timeAsDouble+.05;return;
        }
        if(test<20){if(phase==0){Keys();var positions=new[]{new Vector3(3.1f,.228f,1.6f),new Vector3(.25f,.228f,2.10f),new Vector3(2.3f,.228f,-2.35f),new Vector3(3.15f,.228f,-.2f)};rb.position=positions[test-16];rb.rotation=Quaternion.identity;cat.transform.SetPositionAndRotation(rb.position,rb.rotation);view.enabled=true;view.Initialize(cat.transform);if(test%2==0)view.CycleView();phase=1;ready=Time.timeAsDouble+1.2;return;}
            Vector3 d=camera.transform.position-cat.transform.position;float horizontal=new Vector2(d.x,d.z).magnitude;Require(horizontal>=1.30f,"Camera collapsed near furniture "+test+": "+horizontal);Require(Vector3.Angle(camera.transform.forward,Vector3.down)>25f,"Camera became top-down");report.Add("PASS obstacle camera case "+(test-16)+" horizontal distance="+horizontal.ToString("F2"));Capture("Camera"+(test-16));test++;phase=0;ready=Time.timeAsDouble+.1;return;}
        report.Add("PASS 16 live keyboard movement/stop cases and 4 furniture camera cases; scene not saved during testing.");Finish();
    }catch(Exception e){report.Add("FAIL "+e.Message);Debug.LogException(e);Finish();}}
    static void Capture(string name){var rt=new RenderTexture(960,720,24);var prev=RenderTexture.active;var old=camera.targetTexture;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(960,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,720),0,0);tex.Apply();File.WriteAllBytes(Dir+"/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}finally{camera.targetTexture=old;RenderTexture.active=prev;Object.DestroyImmediate(rt);}}
    static void Finish(){EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=FeedInput;Directory.CreateDirectory(Dir);File.WriteAllLines(Dir+"/check.txt",report);if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}previousKeyboard?.MakeCurrent();if(previousSettings)previousSettings.editorInputBehaviorInPlayMode=previousBehavior;cat=null;EditorApplication.isPlaying=false;}
}
