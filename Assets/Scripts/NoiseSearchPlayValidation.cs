#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Explicitly launched live-scene QA. Never saves a scene; user preferences restored on exit.
[InitializeOnLoad]
public sealed class NoiseSearchPlayValidation : MonoBehaviour
{
    const string Active = "NoiseSearchQA";
    static readonly string[] Keys = { GameSaveStore.SaveKey, GameSaveStore.HistoryKey, "Catastrophe.HighScore" };
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    string folder;
    int passed, failed, errors, visits;
    bool finished;
    GameFlowUI flow;
    CatController cat;
    OwnerAlert owner;
    ChaosJuiceUI hud;
    Rigidbody body;
    Keyboard keyboard, previousKeyboard;
    InputSettings.EditorInputBehaviorInPlayMode oldInput;
    Key[] held = Array.Empty<Key>();
    readonly List<GameObject> fixtures = new();
    static NoiseSearchPlayValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Active,false)) return;
            if (state==PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ()=>new GameObject("Disposable noise search QA").AddComponent<NoiseSearchPlayValidation>();
            if (state==PlayModeStateChange.EnteredEditMode) RestorePrefs();
        };
    }
    [MenuItem("Catastrophe/Verify Noise Search In Play")]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        string folder="Validation/NoiseSearch/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(folder);SessionState.SetString(Active+"Folder",folder);
        foreach(string key in Keys)
        {
            SessionState.SetBool(Active+key+"Exists",PlayerPrefs.HasKey(key));
            string value=key.EndsWith("HighScore")?PlayerPrefs.GetInt(key).ToString():PlayerPrefs.GetString(key);
            SessionState.SetString(Active+key,value);
        }
        SessionState.SetBool(Active+"Background",Application.runInBackground);
        File.WriteAllText(folder+"/report.txt","Actual LivingRoom Play Mode / noise 100 -> 5s warning -> 3s inspection.\n");
        SessionState.SetBool(Active,true);EditorApplication.isPlaying=true;
    }
    IEnumerator Start()
    {
        folder=SessionState.GetString(Active+"Folder","");
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;
        Application.logMessageReceived+=OnLog;StartCoroutine(Watchdog());
        yield return new WaitForSecondsRealtime(2);
        Bind();
        Check(flow&&cat&&owner&&hud&&NoiseManager.I,"One live set of gameplay services exists");
        if(!flow||!cat||!owner||!hud){Finish();yield break;}
        Check(flow.State==GameFlowUI.ScreenState.MainMenu&&!owner.IsRunning&&!hud.CountdownVisible&&!hud.GirlVisible,"No alert/HUD while on main menu");
        Check(Object.FindObjectsByType<OwnerAlert>(FindObjectsSortMode.None).Length==1&&
            Object.FindObjectsByType<ChaosJuiceUI>(FindObjectsSortMode.None).Length==1,"Exactly one alert controller and HUD");
        Check(hud.GirlArtworkLoaded&&hud.NoiseArtworkLoaded,"Approved paw and supplied girl frames load");
        Check(cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0&&cat.GetComponentInChildren<Animator>()&&!cat.GetComponentInChildren<Animator>().applyRootMotion,
            "Original 3D cat, rig and controller retained");
        Check(!hud.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(true),"HUD does not capture gameplay input");
        previousKeyboard=Keyboard.current;oldInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=Feed;InputSystem.onAfterUpdate+=Select;
        flow.BeginGame();NoAutosave();
        owner.OnInspectionChanged+=active=>{if(active)visits++;};
        Check(NoiseManager.I.threshold==100&&NoiseManager.I.noise==0&&GameManager.I.DamageAmount==0,"New run resets amount/noise, threshold is 100");
        yield return Capture("01-idle-hud");
        yield return new WaitForSeconds(31f);
        Check(owner.CurrentPhase==OwnerAlert.Phase.Idle&&!hud.CountdownVisible&&visits==0,"31 quiet seconds do not trigger a periodic patrol");
        foreach(var size in new[]{new Vector2(844,390),new Vector2(932,430),new Vector2(1280,720),new Vector2(2340,1080),new Vector2(640,360),new Vector2(390,844)})
        {
            Rect safe=new Rect(30,15,size.x-60,size.y-30);
            ChaosJuiceUI.HudLayout(size,safe,out float scale,out Rect a,out Rect n,out Rect t,out Rect p);
            Rect area=new Rect(safe.x/scale,(size.y-safe.yMax)/scale,safe.width/scale,safe.height/scale);
            Check(area.Contains(a.min)&&area.Contains(a.max)&&area.Contains(n.max)&&area.Contains(t.max)&&area.Contains(p.max)&&
                !a.Overlaps(n)&&!a.Overlaps(t)&&!n.Overlaps(t),"Safe area, no overlaps "+size);
        }
        // Real gravity and collision callbacks on temporary props, not direct score calls.
        var drop=GameObject.CreatePrimitive(PrimitiveType.Cube);drop.name="QA wooden book";
        drop.transform.localScale=Vector3.one*.12f;drop.transform.position=new Vector3(-2.9f,1.4f,-1.5f);
        drop.AddComponent<Rigidbody>();var value=drop.AddComponent<Valuable>();fixtures.Add(drop);
        value.itemName="QA wooden book";value.chaosValue=7;value.noise=18;value.ActivatePhysics();
        yield return new WaitForSeconds(.15f);
        Check(!value.IsDestroyed&&GameManager.I.Receipt.Count==0,"Falling in air does not award damage before impact");
        float deadline=Time.time+4;
        while(!value.IsDestroyed&&Time.time<deadline)yield return null;
        Check(value.IsDestroyed&&GameManager.I.DamageAmount==700&&GameManager.I.Receipt.Count==1,"Actual floor impact awards existing item price once");
        float noise=NoiseManager.I.noise;value.MarkDestroyed();value.MarkDestroyed();
        yield return new WaitForSeconds(.8f);
        Check(GameManager.I.DamageAmount==700&&GameManager.I.Receipt.Count==1&&NoiseManager.I.noise==noise,"Repeated destruction and resting contacts do not farm score/noise");
        var nudge=GameObject.CreatePrimitive(PrimitiveType.Cube);nudge.name="QA nudge";
        nudge.transform.position=new Vector3(-2.8f,.12f,-.8f);nudge.transform.localScale=Vector3.one*.15f;nudge.AddComponent<Rigidbody>();
        var nudgeValue=nudge.AddComponent<Valuable>();fixtures.Add(nudge);yield return new WaitForSeconds(.1f);
        float before=NoiseManager.I.noise;
        Check(nudgeValue.TryNudge(Vector3.right),"A normal nudge is accepted");
        float once=NoiseManager.I.noise;
        for(int i=0;i<20;i++)nudgeValue.TryNudge(Vector3.right);
        Check(Mathf.Approximately(once,before)&&Mathf.Approximately(NoiseManager.I.noise,before),"Pushing on support stays quiet; repeated pushes cannot add noise");
        yield return new WaitForSeconds(.8f);
        NoiseManager.I.RestoreNoise(0);owner.RestoreState(OwnerAlert.Phase.Idle,0);
        NoiseManager.I.EmitNoise(99);
        Check(!owner.IsWarning&&!owner.IsInspecting,"99 does not start warning");
        NoiseManager.I.EmitNoise(1);
        Check(owner.IsWarning&&Mathf.Abs(owner.SecondsRemaining-5)<.01f&&!hud.GirlVisible,"100 starts exactly 5 seconds, owner not visible");
        yield return Capture("02-warning-five-seconds");
        float started=Time.time;
        NoiseManager.I.EmitNoise(999);owner.TriggerAlert();
        Check(NoiseManager.I.noise==100&&owner.SecondsRemaining<=5,"Noise clamps to 100, repeated trigger cannot restart countdown");
        Warp(new Vector3(0,.225f,-1.5f),Quaternion.identity);
        Vector3 initial=body.position;float yaw=Camera.main.GetComponent<ThirdPersonCamera>().OrbitYaw;
        held=new[]{Key.W};yield return new WaitForSeconds(.45f);held=Array.Empty<Key>();
        Check(Vector3.Distance(initial,body.position)>.08f&&GameFlowUI.GameplayInputEnabled,"Normal movement remains active during warning");
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,Camera.main.GetComponent<ThirdPersonCamera>().OrbitYaw))<.1f,"Movement does not auto-rotate camera");
        float groundY=body.position.y;
        held=new[]{Key.Space};yield return new WaitForSeconds(.28f);held=Array.Empty<Key>();
        Check(body.position.y>groundY+.1f,"Normal jump remains active during warning");
        float remaining=owner.SecondsRemaining;
        flow.TogglePause();yield return new WaitForSecondsRealtime(.3f);
        Check(Mathf.Abs(owner.SecondsRemaining-remaining)<.001f,"Pause freezes warning");
        flow.TogglePause();NoAutosave();
        Check(GameSaveStore.Capture(),"Warning state can be saved");
        float saved=owner.SecondsRemaining;owner.RestoreState(OwnerAlert.Phase.Idle,0);
        Check(GameSaveStore.Restore()&&owner.IsWarning&&Mathf.Abs(saved-owner.SecondsRemaining)<.03f,"Resume preserves warning and remaining seconds");
        NoAutosave();
        var hide=GameObject.Find("Hide Spot - Cat Tree Cubby")?.GetComponent<HideSpot>();
        Check(hide&&hide.HideAnchor&&hide.ExitAnchor,"Real cubby has entry and exit anchors");
        if(hide)
        {
            Warp(hide.ExitAnchor.position,hide.ExitAnchor.rotation);yield return new WaitForSeconds(.2f);
            Check(cat.CanHideNearby&&!cat.IsHidden,"Near cubby alone is not explicit hiding");
            held=new[]{Key.E};yield return new WaitForSeconds(.08f);held=Array.Empty<Key>();yield return new WaitForSeconds(.1f);
            Check(cat.IsHidden&&RoomHiding.IsConcealed(cat),"Normal interaction enters real cubby");
        }
        deadline=Time.time+6;
        while(!owner.IsInspecting&&Time.time<deadline)yield return null;
        float inspectionStart=Time.time;
        Check(owner.IsInspecting&&hud.GirlVisible&&!hud.CountdownVisible&&visits==1,"Exactly one inspection starts after warning; warning HUD disappears");
        Check(inspectionStart-started>=4.9f,"Warning lasts at least five gameplay seconds (excluding pause)");
        yield return Capture("03-owner-searching");
        deadline=Time.time+4;
        while(owner.IsInspecting&&Time.time<deadline)yield return null;
        Check(Mathf.Abs(Time.time-inspectionStart-3f)<.15f,"Inspection lasts three seconds");
        Check(!GameManager.I.IsFailed&&NoiseManager.I.noise==0&&GameManager.I.DamageAmount==700&&owner.CurrentPhase==OwnerAlert.Phase.Idle,"Hidden success resets noise, keeps money and resumes idle");
        cat.ExitHide();
        // Find actual accessible cover positions, never change furniture or collision.
        var solid=cat.GetComponent<CatSolidCollision>();
        Vector3? tablePoint=null,sofaPoint=null;
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if(!c.isTrigger&&(c.name.ToLowerInvariant().Contains("table")||c.name.ToLowerInvariant().Contains("sofa")))
                File.AppendAllText(folder+"/geometry.txt",c.name+" "+c.bounds+"\n");
        for(float x=-3.5f;x<=3.6f;x+=.15f)
        for(float z=-2.8f;z<=3.5f;z+=.15f)
        {
            Vector3 p=new Vector3(x,.225f,z);
            if(solid&&!solid.CanOccupy(p,Quaternion.identity))continue;
            Warp(p,Quaternion.identity);
            if(!tablePoint.HasValue&&RoomHiding.IsUnderTable(cat))tablePoint=p;
            if(!sofaPoint.HasValue&&RoomHiding.IsBehindSofa(cat))sofaPoint=p;
            if(tablePoint.HasValue&&sofaPoint.HasValue)break;
        }
        Check(tablePoint.HasValue,"Accessible table-under cover exists in actual room");
        Check(sofaPoint.HasValue,"Accessible sofa-back cover exists in actual room");
        if(tablePoint.HasValue)
        {
            Warp(tablePoint.Value,Quaternion.identity);yield return new WaitForSeconds(.2f);
            Check(RoomHiding.IsUnderTable(cat)&&!body.isKinematic,"Walking under table grants cover without freezing cat");
            yield return Capture("04-table-cover");
        }
        if(sofaPoint.HasValue){Warp(sofaPoint.Value,Quaternion.identity);yield return Capture("05-sofa-cover");}
        Warp(new Vector3(0,.225f,-1.5f),Quaternion.identity);
        Check(!RoomHiding.IsConcealed(cat),"Open floor is not concealed");
        // Exercise optional music ducking with a disposable music source.
        var music=new GameObject("QA optional music").AddComponent<AudioSource>();fixtures.Add(music.gameObject);
        music.volume=.8f;AudioManager.I.SetBackgroundMusic(music);
        owner.RestoreState(OwnerAlert.Phase.Inspecting,3);
        yield return new WaitForSeconds(.5f);
        Check(AudioManager.I.InspectionMixActive&&music.volume<.3f&&AudioManager.I.HasCountdownClip,"Inspection ducks assigned music; gentle countdown clip available");
        Check(GameSaveStore.Capture(),"Inspection state can be saved");
        Check(GameSaveStore.Restore()&&owner.IsInspecting,"Resume preserves active inspection");
        NoAutosave();deadline=Time.realtimeSinceStartup+5;
        while(!GameManager.I.IsFailed&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(GameManager.I.IsFailed&&flow.CaughtVisible&&flow.State==GameFlowUI.ScreenState.Failure,"Exposed cat shows existing Caught you animation");
        Check(!owner.IsRunning&&!owner.IsInspecting&&!hud.GirlVisible&&Time.timeScale==0,"Failure stops owner, hides inspection portrait and freezes gameplay");
        yield return new WaitForSecondsRealtime(2.4f);
        Check(flow.CaughtActionsVisible&&flow.CaughtItems==1&&flow.CaughtDamage==700,"Caught result uses real item count and amount");
        Check(!AudioManager.I.InspectionMixActive&&Mathf.Abs(music.volume-.8f)<.01f,"Music volume restores after inspection");
        yield return Capture("06-caught-result");
        owner.TriggerAlert();NoiseManager.I.EmitNoise(100);
        Check(!owner.IsWarning,"Failure cannot retrigger warning");
        RemoveInput();
        flow.RestartGame();yield return new WaitForSecondsRealtime(2);Bind();NoAutosave();
        Check(flow.State==GameFlowUI.ScreenState.Playing&&NoiseManager.I.noise==0&&GameManager.I.DamageAmount==0&&
            owner.CurrentPhase==OwnerAlert.Phase.Idle&&!flow.CaughtVisible&&!hud.GirlVisible,"Restart creates clean playable state");
        Check(Object.FindObjectsByType<ChaosJuiceUI>(FindObjectsSortMode.None).Length==1,"No duplicated HUD after scene restart");
        yield return Capture("07-restarted");
        Check(errors==0,"No runtime errors/exceptions during test");
        Finish();
    }
    void Bind(){flow=Object.FindAnyObjectByType<GameFlowUI>();cat=Object.FindAnyObjectByType<CatController>();owner=OwnerAlert.I;hud=Object.FindAnyObjectByType<ChaosJuiceUI>();if(cat)body=cat.GetComponent<Rigidbody>();}
    void Warp(Vector3 p,Quaternion r){body.position=p;body.rotation=r;body.linearVelocity=Vector3.zero;cat.transform.SetPositionAndRotation(p,r);Physics.SyncTransforms();}
    void NoAutosave(){if(flow)typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);}
    void Feed(){if(keyboard!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keyboard.MakeCurrent();InputState.Change(keyboard,new KeyboardState(held));}}
    void Select(){keyboard?.MakeCurrent();}
    void RemoveInput(){if(keyboard==null)return;InputSystem.onBeforeUpdate-=Feed;InputSystem.onAfterUpdate-=Select;InputSystem.RemoveDevice(keyboard);keyboard=null;previousKeyboard?.MakeCurrent();InputSystem.settings.editorInputBehaviorInPlayMode=oldInput;}
    void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;File.AppendAllText(folder+"/report.txt","ERROR "+message+"\n");}}
    IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+name+".png",t.EncodeToPNG());Object.Destroy(t);}
    void Check(bool ok,string message){if(ok)passed++;else failed++;File.AppendAllText(folder+"/report.txt",(ok?"PASS ":"FAIL ")+message+"\n");}
    IEnumerator Watchdog(){yield return new WaitForSecondsRealtime(180);if(!finished){Check(false,"Watchdog timeout");Finish();}}
    void Finish(){if(finished)return;finished=true;NoAutosave();RemoveInput();Application.logMessageReceived-=OnLog;
        File.AppendAllText(folder+"/report.txt",$"RESULT {passed} passed / {failed} failed. Functional score: {Mathf.RoundToInt(100f*passed/Mathf.Max(1,passed+failed))}/100. Visual review separate.\n");
        RestorePrefs();EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;}
    static void RestorePrefs(){if(!SessionState.GetBool(Active,false))return;Application.runInBackground=SessionState.GetBool(Active+"Background",false);
        foreach(string key in Keys){if(SessionState.GetBool(Active+key+"Exists",false)){string v=SessionState.GetString(Active+key,"");if(key.EndsWith("HighScore"))PlayerPrefs.SetInt(key,int.Parse(v));else PlayerPrefs.SetString(key,v);}else PlayerPrefs.DeleteKey(key);}
        PlayerPrefs.Save();SessionState.SetBool(Active,false);}
    void OnDestroy(){RemoveInput();Application.logMessageReceived-=OnLog;}
}
#endif
