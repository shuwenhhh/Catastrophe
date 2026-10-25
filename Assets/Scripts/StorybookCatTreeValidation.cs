#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public sealed class StorybookCatTreeValidation:MonoBehaviour
{
    const string Flag="SoftCatTreeQA",Folder="Validation/StorybookCatTree";
    static readonly string[] Keys={GameSaveStore.SaveKey,GameSaveStore.HistoryKey,"Catastrophe.HighScore"};
    int passed,failed,errors;bool finished;Key[] held=Array.Empty<Key>();
    CatController cat;Rigidbody body;CatSolidCollision solid;ThirdPersonCamera orbit;GameFlowUI flow;StorybookCatTree tree;Animator animator;
    Keyboard keyboard,previousKeyboard;InputSettings.EditorInputBehaviorInPlayMode previousInput;
    static StorybookCatTreeValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{
            if(!SessionState.GetBool(Flag,false))return;
            if(s==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>new GameObject("Disposable soft cat tree verification").AddComponent<StorybookCatTreeValidation>();
            if(s==PlayModeStateChange.EnteredEditMode)Restore();
        };
    }
    [MenuItem("Catastrophe/Storybook Cat Tree/2 Verify Hiding And Jumps")]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(var key in Keys){SessionState.SetBool(Flag+key+"Exists",PlayerPrefs.HasKey(key));SessionState.SetString(Flag+key,key=="Catastrophe.HighScore"?PlayerPrefs.GetInt(key,0).ToString():PlayerPrefs.GetString(key,""));}
        SessionState.SetBool(Flag+"Background",Application.runInBackground);SessionState.SetString(Flag+"Pipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/play-report.txt","Actual new cat tree, actual skinned cat/controller. Saves restored after Play.\n");
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;Application.logMessageReceived+=Log;StartCoroutine(Watchdog());
        yield return new WaitForSecondsRealtime(1.5f);
        cat=Object.FindAnyObjectByType<CatController>();tree=Object.FindAnyObjectByType<StorybookCatTree>();flow=Object.FindAnyObjectByType<GameFlowUI>();
        Check(cat&&tree&&flow,"Original player and new authored furniture load");if(!cat||!tree||!flow){Finish();yield break;}
        flow.BeginGame();typeof(GameFlowUI).GetField("runActive",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(flow,false);
        OwnerAlert.I.RestoreCycle(false,30);body=cat.GetComponent<Rigidbody>();solid=cat.GetComponent<CatSolidCollision>();animator=cat.GetComponentInChildren<Animator>();orbit=Camera.main.GetComponent<ThirdPersonCamera>();
        Check(cat.GetComponentInChildren<SkinnedMeshRenderer>()&&animator&&!animator.applyRootMotion,"Same skinned cat, bones and physics controller; no substitute model");
        Check(orbit&&orbit.FollowTarget==cat.transform&&Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1,"Only one gameplay camera follows the actual cat");
        Check(Object.FindObjectsByType<StorybookCatTree>(FindObjectsSortMode.None).Length==1,"Only one new cat tree is active");
        Check(tree.transform.Find("Playable hollow cubby")==null,"Legacy runtime stretching is not applied to the authored shell");
        var renderers=tree.GetComponentsInChildren<Renderer>();
        Check(renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader.isSupported&&!m.shader.name.Contains("Error"))),"All plush, rope and trim materials render correctly");
        Check(renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count()==5,"Pink, vanilla, oatmeal and rose material zones retained");
        var hide=tree.GetComponentInChildren<HideSpot>();
        Check(hide&&hide.GetComponent<Collider>().isTrigger,"Cubby hide interaction is present and non-blocking");
        if(!hide){Finish();yield break;}
        Check(solid.CanOccupy(hide.HideAnchor.position,hide.HideAnchor.rotation),"Whole cat fits in real hollow cavity, including tail");
        Check(solid.CanOccupy(hide.ExitAnchor.position,hide.ExitAnchor.rotation),"Doorway exit has body/head/tail clearance");
        Place(hide.ExitAnchor.position,270,20);
        yield return new WaitForSeconds(.35f);
        Check(cat.CanHideNearby,"Approaching the doorway activates hiding");
        yield return Beauty("01-soft-pink-tree");
        cat.EnterHide(hide.gameObject);yield return new WaitForSeconds(.35f);
        Check(cat.IsHidden&&Vector3.Distance(body.position,hide.HideAnchor.position)<.04f,"Cat enters and remains inside the actual cubby");
        Check(cat.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.enabled),"Hidden cat remains a visible 3D character");
        yield return Beauty("02-cat-in-cubby");
        cat.ExitHide();yield return new WaitForSeconds(.3f);
        Check(!cat.IsHidden&&solid.CanOccupy(body.position,body.rotation),"Cat exits without penetration or getting stuck");
        Check(!solid.CanOccupy(tree.transform.TransformPoint(new Vector3(3.3f,.60f,-2.83f)),Quaternion.Euler(0,90,0)),"Solid cubby side blocks the cat instead of allowing clipping");

        previousInput=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=Feed;InputSystem.onAfterUpdate+=SelectKeyboard;
        orbit.RestoreView(270,24);var before=body.position;held=new[]{Key.W};yield return new WaitForSeconds(.45f);
        Check(Vector3.Distance(body.position,before)>.18f,"Normal movement can retreat from doorway");
        Check(animator.GetFloat("Speed")>.1f,"Moving still plays the original walk animation");held=Array.Empty<Key>();yield return new WaitForSeconds(.3f);
        Check(animator.GetFloat("Speed")<.05f,"Releasing movement returns to idle");

        Place(new Vector3(1.65f,.225f,-2.43f),90,26);yield return new WaitForSeconds(.3f);
        yield return JumpTo(new Vector3(2.78f,1.105f,-2.43f),true,"03-cubby-roof");
        yield return JumpTo(new Vector3(2.30f,1.16f,-1.95f),false,"04-middle-bed");
        yield return JumpTo(new Vector3(2.84f,1.69f,-1.72f),true,"05-lookout-bed");
        yield return Beauty("06-cat-on-lookout");
        held=Array.Empty<Key>();
        Place(hide.ExitAnchor.position,270,24);yield return new WaitForSeconds(.3f);
        yield return Capture("07-normal-follow-camera");
        Check(orbit.enabled&&orbit.FramingDistance>1.0f,"Normal follow camera remains outside cat and furniture");
        QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        yield return new WaitForSecondsRealtime(.7f);yield return Beauty("08-mobile-tree");
        Check(GraphicsSettings.currentRenderPipeline.name=="Mobile_RPAsset","Pink/beige material separation also checked under actual Mobile pipeline");
        yield return CozyFeedbackPlayChecks.Verify(cat,Check);
        Check(errors==0,"No runtime exceptions or shader errors");Finish();
    }
    IEnumerator JumpTo(Vector3 targetFeet,bool high,string label)
    {
        held=Array.Empty<Key>();yield return new WaitForSeconds(.18f);
        var direction=targetFeet-(body.position-Vector3.up*.225f);direction.y=0;direction.Normalize();
        float yaw=Quaternion.LookRotation(direction).eulerAngles.y;orbit.RestoreView(yaw,26);
        Check(solid.CanOccupy(targetFeet+Vector3.up*.225f,Quaternion.Euler(0,yaw,0)),label+": intended standing pose fits the platform");
        // The normal motor turns before translating. Let that visible turn finish
        // before a short hop instead of spending the entire hop facing backwards.
        held=new[]{Key.W};yield return null;float turnDeadline=Time.time+.8f;
        while(Quaternion.Angle(body.rotation,Quaternion.Euler(0,yaw,0))>8&&Time.time<turnDeadline)yield return null;
        Check(Quaternion.Angle(body.rotation,Quaternion.Euler(0,yaw,0))<=8,label+": normal movement turns cat toward next perch");
        held=new[]{Key.W,Key.Space};float start=Time.time,apex=body.position.y-.225f;
        var trace=new System.Text.StringBuilder();bool releasedMovement=false;
        while(Time.time-start<2.1f)
        {
            float elapsed=Time.time-start;var delta=targetFeet-(body.position-Vector3.up*.225f);delta.y=0;
            // Releasing occurs on the next input/update tick, followed by physics.
            // Anticipate that one-frame travel rather than waiting until past the centre.
            float releaseDistance=Mathf.Max(.04f,Vector3.Dot(body.linearVelocity,direction)*(Time.deltaTime+Time.fixedDeltaTime));
            releasedMovement|=Vector3.Dot(delta,direction)<=releaseDistance;
            bool move=!releasedMovement;bool jump=elapsed<(high?.32f:.065f);
            held=move?(jump?new[]{Key.W,Key.Space}:new[]{Key.W}):(jump?new[]{Key.Space}:Array.Empty<Key>());
            apex=Mathf.Max(apex,body.position.y-.225f);
            trace.AppendLine($"{elapsed:F3} dt={Time.deltaTime:F3} p={body.position:F3} v={body.linearVelocity:F3} move={move} jump={jump}");
            // Never encode a 4K screenshot while a key is held: that stalls the
            // editor and can turn a tap into a hold or overrun the landing point.
            yield return null;
        }
        held=Array.Empty<Key>();yield return new WaitForSeconds(.2f);var feet=body.position-Vector3.up*.225f;float error=Vector3.Distance(feet,targetFeet);
        File.WriteAllText(Folder+"/"+label+"-input.txt",trace.ToString());
        Check(Mathf.Abs(feet.y-targetFeet.y)<.055f&&error<.25f,$"{label}: normal {(high?"hold":"tap")} input lands; feet={feet:F3}, target={targetFeet:F3}, apex={apex:F3}, error={error:F3}");
        Check(solid.CanOccupy(body.position,body.rotation),label+": no body penetration after landing");yield return Capture(label+"-landed");
    }
    void Place(Vector3 p,float yaw,float pitch){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.position=p;body.rotation=Quaternion.Euler(0,yaw,0);cat.transform.SetPositionAndRotation(p,body.rotation);Physics.SyncTransforms();orbit.Initialize(cat.transform);orbit.RestoreView(90,pitch);}
    IEnumerator Beauty(string label)
    {
        bool enabled=orbit.enabled;orbit.enabled=false;var cam=Camera.main;var pos=cam.transform.position;var rot=cam.transform.rotation;float fov=cam.fieldOfView;
        cam.transform.position=new Vector3(.65f,1.95f,-3.35f);cam.transform.LookAt(new Vector3(2.65f,.87f,-2.10f));cam.fieldOfView=67;
        yield return Capture(label);cam.transform.SetPositionAndRotation(pos,rot);cam.fieldOfView=fov;orbit.enabled=enabled;
    }
    static IEnumerator Capture(string label){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Folder+"/"+label+".png",image.EncodeToPNG());Object.Destroy(image);}
    void Feed(){if(keyboard!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keyboard.MakeCurrent();InputState.Change(keyboard,new KeyboardState(held));}}
    void SelectKeyboard(){keyboard?.MakeCurrent();}
    void RemoveInput(){if(keyboard==null)return;InputSystem.onBeforeUpdate-=Feed;InputSystem.onAfterUpdate-=SelectKeyboard;InputSystem.RemoveDevice(keyboard);keyboard=null;previousKeyboard?.MakeCurrent();InputSystem.settings.editorInputBehaviorInPlayMode=previousInput;}
    void Check(bool ok,string text){if(ok)passed++;else failed++;File.AppendAllText(Folder+"/play-report.txt",(ok?"PASS ":"FAIL ")+text+"\n");}
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(Folder+"/play-report.txt","ERROR "+message+"\n");}}
    IEnumerator Watchdog(){yield return new WaitForSecondsRealtime(55);if(!finished){Check(false,"Watchdog timeout");Finish();}}
    void Finish(){if(finished)return;finished=true;RemoveInput();Application.logMessageReceived-=Log;if(flow)typeof(GameFlowUI).GetField("runActive",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(flow,false);File.AppendAllText(Folder+"/play-report.txt",$"RESULT {passed} passed, {failed} failed. Visual scoring requires inspecting captured frames.\n");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;}
    static void Restore()
    {
        foreach(var key in Keys)if(SessionState.GetBool(Flag+key+"Exists",false)){var value=SessionState.GetString(Flag+key,"");if(key=="Catastrophe.HighScore")PlayerPrefs.SetInt(key,int.Parse(value));else PlayerPrefs.SetString(key,value);}else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();Application.runInBackground=SessionState.GetBool(Flag+"Background",false);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Flag+"Pipeline",""));SessionState.SetBool(Flag,false);
    }
}
#endif
