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
using Object = UnityEngine.Object;

// Opt-in, disposable actual-scene validation. Never saves a test scene.
[InitializeOnLoad]
public sealed class StorybookRoomPlayValidation:MonoBehaviour
{
    const string Flag="StorybookRoomQA", Folder="Validation/StorybookRoom";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[] Keys={GameSaveStore.SaveKey,GameSaveStore.HistoryKey,"Catastrophe.HighScore"};
    int passed,failed,errors;bool finished,walking;
    CatController cat; Rigidbody body; ThirdPersonCamera orbit; GameFlowUI flow;
    Keyboard keyboard,previousKeyboard;
    InputSettings.EditorInputBehaviorInPlayMode oldInput;

    static StorybookRoomPlayValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(!SessionState.GetBool(Flag,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall+=()=>new GameObject("Disposable storybook room verification").AddComponent<StorybookRoomPlayValidation>();
            if(state==PlayModeStateChange.EnteredEditMode)Restore();
        };
    }
    [MenuItem("Catastrophe/Storybook Room/3 Verify Palette In Play")]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(var key in Keys){SessionState.SetBool(Flag+key+"Exists",PlayerPrefs.HasKey(key));SessionState.SetString(Flag+key,key=="Catastrophe.HighScore"?PlayerPrefs.GetInt(key,0).ToString():PlayerPrefs.GetString(key,""));}
        SessionState.SetBool(Flag+"Background",Application.runInBackground);
        SessionState.SetString(Flag+"Pipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/play-report.txt","Actual LivingRoom runtime, character-consistent room palette.\n");
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;Application.logMessageReceived+=Log;StartCoroutine(Watchdog());
        yield return new WaitForSecondsRealtime(1.5f);
        cat=Object.FindAnyObjectByType<CatController>();flow=Object.FindAnyObjectByType<GameFlowUI>();
        orbit=Camera.main?Camera.main.GetComponent<ThirdPersonCamera>():null;
        Check(cat&&flow&&orbit,"Actual cat, flow and orbit camera loaded");
        if(!cat||!flow||!orbit){Finish();yield break;}
        flow.BeginGame();typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);
        body=cat.GetComponent<Rigidbody>();
        var anim=cat.GetComponentInChildren<Animator>();var skins=cat.GetComponentsInChildren<SkinnedMeshRenderer>();
        Check(body&&anim&&skins.Length>0&&!anim.applyRootMotion&&anim.GetComponentInParent<CatController>()==cat,
            "Original skinned cat, Animator and physics hierarchy retained");
        Check(skins.SelectMany(s=>s.sharedMaterials).All(m=>m&&!m.name.StartsWith("Storybook_")),"Cat fur, eyes and face materials untouched");
        Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1&&orbit.FollowTarget==cat.transform,"One camera follows original cat");
        CheckColour("Table","Storybook_WarmHoneyTable","C99A58");
        CheckColour("Chair 01","Storybook_SoftNaturalChairs","DDB579");
        CheckColour("Uniform Light Oak Floor","Storybook_PaleOatFloor","E8CCAA");
        CheckColour("Warm window nook","Storybook_Terracotta woven cloth","D46C5D");
        if(GameObject.Find("Reference carpet cat tree").GetComponent<StorybookCatTree>())
            CheckColour("Reference carpet cat tree","Strawberry milk plush","F3BDCD");
        else CheckColour("Reference carpet cat tree","Storybook_Oatmeal carpet","F3BCD0");
        CheckColour("Four Wall Room Shell (editable)","Storybook_RoomShell_FAF6ED","FFF1DB");
        CheckColour("Large Cream Woven Rug","Storybook_RugBorder","D99785");
        foreach(string name in new[]{"Table","Chair 01","Uniform Light Oak Floor"})
        {
            var mat=GameObject.Find(name).GetComponentsInChildren<Renderer>().First(r=>r.enabled).sharedMaterial;
            Check(mat.GetTexture("_BaseMap")&&mat.GetFloat("_Surface")==0,name+": original grain retained, fully opaque");
        }
        var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled).ToArray();
        Check(renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).All(m=>m.shader.isSupported&&!m.shader.name.Contains("Error")),"No missing/pink room or character shaders");
        var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Check(lights.Count(l=>l.enabled&&l.type==LightType.Directional)==1,"Only the authored directional daylight survives bootstrap");
        var volume=Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v=>v.sharedProfile&&v.sharedProfile.name=="StorybookDaylight");
        Check(volume&&Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing,"Dedicated storybook grading active on gameplay camera");

        Place(new Vector3(-.35f,.99f,-.65f),155f,32f);
        yield return new WaitForSeconds(.6f);yield return Capture("01-tabletop-PC");
        var hud=Object.FindAnyObjectByType<ChaosJuiceUI>();OwnerAlert.I.RestoreCycle(true,3f);
        yield return null;yield return Capture("02-girl-and-cat-PC");
        Check(hud&&hud.GirlVisible&&hud.GirlArtworkLoaded,"Original supplied girl visible beside the recoloured room");
        var portrait=hud.GetComponentInChildren<UnityEngine.UI.RawImage>();
        Check(portrait&&portrait.color==Color.white&&!portrait.raycastTarget,"Girl artwork remains untinted and never blocks controls");
        OwnerAlert.I.RestoreCycle(false,30f);

        Place(new Vector3(1.7f,.225f,-1.7f),0,32);
        yield return new WaitForSeconds(.4f);yield return Capture("03-floor-PC");
        // Use an open aisle, rather than walking directly into the side chair.
        Place(new Vector3(-2.9f,.225f,-1.4f),0,32);
        yield return new WaitForSeconds(.3f);
        oldInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=Feed;InputSystem.onAfterUpdate+=SelectKeyboard;
        var position=body.position;float yaw=orbit.OrbitYaw;walking=true;
        yield return new WaitForSeconds(.32f);
        Check(Keyboard.current==keyboard&&keyboard.wKey.isPressed,"Validation keyboard is active and W is held");
        Check(Vector3.Distance(position,body.position)>.12f,$"Normal movement still advances the cat: {position} -> {body.position}");
        Check(anim.GetFloat("Speed")>.1f,"Movement still drives the walk animation; Speed="+anim.GetFloat("Speed"));
        yield return Capture("04-walk-PC");walking=false;
        yield return new WaitForSeconds(.3f);
        Check(anim.GetFloat("Speed")<.05f,"Releasing input returns animation to idle");
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,orbit.OrbitYaw))<.1f,"Movement does not recenter the player's camera angle");
        RemoveInput();
        Place(new Vector3(.55f,.225f,-.2f),0,5);
        yield return new WaitForSeconds(.4f);yield return Capture("05-under-table-PC");
        Check(GameObject.Find("Table").GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.sharedMaterial.GetFloat("_Surface")==0),"Table remains visible/opaque above the cat, no fading introduced");

        QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        yield return new WaitForSecondsRealtime(1.2f);
        Check(GraphicsSettings.currentRenderPipeline.name=="Mobile_RPAsset","Actual Mobile render pipeline active, not only quality index");
        Place(new Vector3(-.35f,.99f,-.65f),155,32);
        yield return new WaitForSeconds(.5f);yield return Capture("06-tabletop-Mobile");
        OwnerAlert.I.RestoreCycle(true,3);yield return null;yield return Capture("07-girl-and-cat-Mobile");
        var mobile=(UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        var sunlight=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.enabled&&l.type==LightType.Directional);
        Check(mobile.supportsMainLightShadows&&mobile.shadowDistance>0&&sunlight.shadows==LightShadows.Soft,
            "Mobile retains original lightweight shadow pipeline; experimental grainy AO not installed");
        OwnerAlert.I.RestoreCycle(false,30);flow.TogglePause();
        Check(Time.timeScale==0&&!GameFlowUI.GameplayInputEnabled,"Pause still freezes gameplay");
        flow.TogglePause();Check(Time.timeScale==1&&GameFlowUI.GameplayInputEnabled,"Resume still works");
        Check(errors==0,"No runtime exceptions or shader errors during the test");
        Finish();
    }
    void Place(Vector3 p,float facing,float pitch)
    {
        body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        body.position=p;body.rotation=Quaternion.Euler(0,facing,0);cat.transform.SetPositionAndRotation(p,body.rotation);
        Physics.SyncTransforms();orbit.Initialize(cat.transform);orbit.RestoreView(0,pitch);
    }
    void SelectKeyboard(){keyboard?.MakeCurrent();}
    void Feed(){if(keyboard!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keyboard.MakeCurrent();InputState.Change(keyboard,walking?new KeyboardState(Key.W):new KeyboardState());}}
    void RemoveInput(){if(keyboard==null)return;InputSystem.onBeforeUpdate-=Feed;InputSystem.onAfterUpdate-=SelectKeyboard;InputSystem.RemoveDevice(keyboard);keyboard=null;if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();InputSystem.settings.editorInputBehaviorInPlayMode=oldInput;}
    void CheckColour(string name,string material,string hex)
    {
        var root=GameObject.Find(name);var mat=root?root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).FirstOrDefault(m=>m&&m.name==material):null;
        Check(mat&&ColorUtility.ToHtmlStringRGB(mat.GetColor("_BaseColor"))==hex,name+": new finish survives runtime initialization");
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
    }
    void Check(bool ok,string message){if(ok)passed++;else failed++;File.AppendAllText(Folder+"/play-report.txt",(ok?"PASS ":"FAIL ")+message+"\n");}
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors++;}
    IEnumerator Watchdog(){yield return new WaitForSecondsRealtime(60);if(!finished){Check(false,"Watchdog timeout");Finish();}}
    void Finish()
    {
        if(finished)return;finished=true;RemoveInput();Application.logMessageReceived-=Log;
        if(flow)typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);
        File.AppendAllText(Folder+"/play-report.txt",$"RESULT {passed} passed, {failed} failed. Mobile pipeline in editor, not physical-phone performance.\n");
        // Keep the backup flag until EnteredEditMode, after all shutdown callbacks.
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    static void Restore()
    {
        foreach(string key in Keys)
            if(SessionState.GetBool(Flag+key+"Exists",false)){var value=SessionState.GetString(Flag+key,"");if(key=="Catastrophe.HighScore")PlayerPrefs.SetInt(key,int.Parse(value));else PlayerPrefs.SetString(key,value);}
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();Application.runInBackground=SessionState.GetBool(Flag+"Background",false);
        QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Flag+"Pipeline",""));
        SessionState.SetBool(Flag,false);
    }
}
#endif
