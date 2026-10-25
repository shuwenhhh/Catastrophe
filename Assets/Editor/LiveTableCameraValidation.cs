using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class LiveTableCameraValidation
{
    static CatController cat;
    static Rigidbody body;
    static ThirdPersonCamera camera;
    static Keyboard keyboard,previousKeyboard;
    static Mouse mouse,previousMouse;
    static float releasedOrbit;
    static InputSettings.EditorInputBehaviorInPlayMode oldBehavior;
    static Key[] held=Array.Empty<Key>();
    static float started,ready,furthest,maxYawStep,lastYaw,stoppedYaw,stopDrift;
    static int phase;
    static bool captured;
    static readonly List<string> lines=new();
    static LiveTableCameraValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TableCameraTest",false)){SessionState.SetBool("TableCameraTest",false);EditorApplication.delayCall+=Run;}};}
    [MenuItem("Catastrophe/Collision/Verify Table and Follow Camera")]
    static void Run()
    {
        if(!Application.isPlaying){SessionState.SetBool("TableCameraTest",true);EditorApplication.isPlaying=true;return;}
        cat=Object.FindAnyObjectByType<CatController>();
        if(!cat)throw new Exception("Runtime cat missing");
        Object.FindAnyObjectByType<GameFlowUI>().BeginGame();
        body=cat.GetComponent<Rigidbody>();camera=Camera.main.GetComponent<ThirdPersonCamera>();
        body.position=new Vector3(.55f,.228f,-1.75f);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;
        cat.transform.SetPositionAndRotation(body.position,body.rotation);Physics.SyncTransforms();camera.Initialize(cat.transform);
        previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        previousMouse=Mouse.current;mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
        oldBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        captured=false;phase=0;ready=Time.time+.3f;started=Time.time;furthest=body.position.z;maxYawStep=stopDrift=0;lastYaw=camera.transform.eulerAngles.y;lines.Clear();
        InputSystem.onBeforeUpdate+=Feed;EditorApplication.update+=Tick;
    }
    static void Feed()
    {
        if(keyboard==null||InputState.currentUpdateType==InputUpdateType.Editor)return;
        keyboard.MakeCurrent();InputState.Change(keyboard,new KeyboardState(held));
        if(mouse!=null)
        {
            mouse.MakeCurrent();
            float progress=phase==6?Mathf.Clamp01((Time.time-started)/2f):1f;
            var state=new MouseState{position=new Vector2(Screen.width*.35f+progress*Screen.width*.25f,Screen.height*.55f)};
            if(phase==6)state=state.WithButton(MouseButton.Left);
            InputState.Change(mouse,state);
        }
    }
    static void Tick()
    {
        try
        {
            if(!Application.isPlaying){Finish();return;}
            if(Time.time<ready)return;
            float yaw=camera.transform.eulerAngles.y;maxYawStep=Mathf.Max(maxYawStep,Mathf.Abs(Mathf.DeltaAngle(lastYaw,yaw)));lastYaw=yaw;
            if(phase==0){held=new[]{Key.W};phase=1;started=Time.time;return;}
            if(phase==1)
            {
                furthest=Mathf.Max(furthest,body.position.z);
                if(!captured&&Time.time-started>2f){captured=true;Capture("under-table");}
                if(Time.time-started<6f)return;
                lines.Add($"Real keyboard W under table: start z=-1.750 end={body.position} {(furthest>.35f?"PASS":"FAIL")}");
                var guard=cat.GetComponent<CatSolidCollision>();
                foreach(var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                    if(mc.name=="table_mesh")
                    {
                        lines.Add($"TABLE convex={mc.convex} bounds={mc.bounds} mesh={mc.sharedMesh.name} triangles={mc.sharedMesh.triangles.Length/3}");
                        foreach(float y in new[]{.10f,.30f,.50f,.65f,.74f})
                            if(mc.Raycast(new Ray(new Vector3(.55f,y,-2f),Vector3.forward),out var h,4f))lines.Add($"TABLE ray y={y} point={h.point} normal={h.normal}");
                    }
                lines.Add("tailLowering="+typeof(CatSolidCollision).GetField("tailLowering",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(guard));
                foreach(var part in guard.Parts)
                {
                    var center=part.transform.TransformPoint(part.center);var ext=part.transform.up*Mathf.Max(0,part.height*.5f-part.radius);
                    foreach(var hit in Physics.CapsuleCastAll(center+ext,center-ext,part.radius-.003f,Vector3.forward,.1f,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                        if(!hit.collider.transform.IsChildOf(cat.transform))lines.Add($"CAST {part.name} -> {hit.collider.name} d={hit.distance:F4} n={hit.normal}");
                }
                foreach(var part in guard.Parts)
                foreach(var other in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if(other.enabled&&!other.isTrigger&&!other.transform.IsChildOf(cat.transform)&&Physics.ComputePenetration(part,part.transform.position+Vector3.forward*.03f,part.transform.rotation,other,other.transform.position,other.transform.rotation,out var normal,out var depth)&&depth>.002f)
                        lines.Add($"BLOCK {part.name} {other.name} depth={depth} normal={normal}");
                held=Array.Empty<Key>();phase=2;ready=Time.time+.4f;return;
            }
            if(phase==2){held=new[]{Key.A};phase=3;started=Time.time;return;}
            if(phase==3){if(Time.time-started<1.5f)return;held=Array.Empty<Key>();phase=4;ready=Time.time+2f;return;}
            if(phase==4){stoppedYaw=cat.transform.eulerAngles.y;phase=5;ready=Time.time+1f;return;}
            if(phase==6){if(Time.time-started<2f)return;phase=7;ready=Time.time+2f;return;}
            if(phase==7){releasedOrbit=Orbit();phase=8;ready=Time.time+2f;return;}
            if(phase==8){lines.Add($"Mouse drag release: orbit={Orbit():F2}, drift={Mathf.Abs(Orbit()-releasedOrbit):F3} {(Mathf.Abs(Orbit()-releasedOrbit)<.1f&&Mathf.Abs(Orbit())>20f?"PASS":"FAIL")}");Capture("manual-orbit");Finish();return;}
            stopDrift=Mathf.Abs(Mathf.DeltaAngle(stoppedYaw,cat.transform.eulerAngles.y));
            lines.Add($"Stopped cat heading drift={stopDrift:F3} degrees {(stopDrift<1?"PASS":"FAIL")}");
            Capture("after-turn");
            lines.Add($"Largest camera frame yaw change={maxYawStep:F2} degrees (includes deliberate turn); final follow error={Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y,cat.transform.eulerAngles.y)):F2}");
            phase=6;started=Time.time;
        }
        catch(Exception ex){lines.Add("ERROR "+ex);Finish();}
    }
    static float Orbit()=>(float)typeof(ThirdPersonCamera).GetField("targetYawOffset",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(camera);
    static void Capture(string name)
    {
        Directory.CreateDirectory("Validation/TableCamera");
        var cam=camera.GetComponent<Camera>();var old=cam.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Validation/TableCamera/"+name+".png",image.EncodeToPNG());}
        finally{cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=Feed;held=Array.Empty<Key>();
        if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;previousKeyboard?.MakeCurrent();InputSystem.settings.editorInputBehaviorInPlayMode=oldBehavior;}
        if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;previousMouse?.MakeCurrent();}
        Directory.CreateDirectory("Validation/TableCamera");File.WriteAllLines("Validation/TableCamera/live.txt",lines);
        Debug.Log("TABLE_CAMERA_TEST "+string.Join("\n",lines));
        EditorApplication.isPlaying=false;
    }
}
