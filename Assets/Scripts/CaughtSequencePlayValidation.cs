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
using Object = UnityEngine.Object;

// Disposable live-scene checks: saves restored, no authored scene is written.
[InitializeOnLoad]
public sealed class CaughtSequencePlayValidation : MonoBehaviour
{
    const string Active = "CaughtSequenceQA", Folder = "Validation/CaughtSequence";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Keys = { GameSaveStore.SaveKey, GameSaveStore.HistoryKey, "Catastrophe.HighScore" };
    GameFlowUI flow;
    CatController cat;
    OwnerAlert clock;
    Keyboard keyboard, previousKeyboard;
    InputSettings.EditorInputBehaviorInPlayMode previousInput;
    Key[] held = Array.Empty<Key>();
    bool finished;
    int passed, failed, exceptions;

    static CaughtSequencePlayValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += () => new GameObject("Disposable caught verification").AddComponent<CaughtSequencePlayValidation>();
            if (state == PlayModeStateChange.EnteredEditMode) Restore();
        };
    }

    [MenuItem("Catastrophe/Verify Caught Animation In Play")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        AssetDatabase.ImportAsset("Assets/Resources/Owner/GirlCaught.png", ImportAssetOptions.ForceUpdate);
        SessionState.SetBool(Active + "Background", Application.runInBackground);
        foreach (string key in Keys)
        {
            SessionState.SetBool(Active + key + "Exists", PlayerPrefs.HasKey(key));
            SessionState.SetString(Active + key, key.EndsWith("HighScore") ? PlayerPrefs.GetInt(key).ToString() : PlayerPrefs.GetString(key));
        }
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/report.txt", "Actual LivingRoom: supplied caught artwork, real hide input, patrol outcome, frozen-world animation.\n");
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        Application.logMessageReceived += Log;
        StartCoroutine(Watchdog());
        yield return new WaitForSecondsRealtime(1.5f);
        Bind();
        var hide = GameObject.Find("Hide Spot - Cat Tree Cubby")?.GetComponent<HideSpot>();
        Check(flow && cat && clock && hide, "Actual flow, patrol, original cat and cubby are present");
        if (!flow || !cat || !clock || !hide) { Finish(); yield break; }
        Check(flow.State == GameFlowUI.ScreenState.MainMenu && !flow.CaughtVisible && !clock.IsRunning,
            "Title screen does not show caught art or run detection");
        Check(cat.GetComponent<Rigidbody>() && cat.GetComponentInChildren<Animator>() &&
            cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0,
            "Original 3D character, rig and physics are retained");
        var texture = Resources.Load<Texture2D>("Owner/GirlCaught");
        Check(texture && texture.width == 2172 && texture.height == 724,
            "Supplied three-frame strip imports unscaled at 2172x724");
        foreach (Vector2 size in new[] { new Vector2(844,390),new Vector2(932,430),new Vector2(1280,720),
            new Vector2(2340,1080),new Vector2(640,360),new Vector2(390,844) })
        {
            Rect safe = new Rect(30,16,size.x-60,size.y-32);
            GameFlowUI.CaughtLayout(size,safe,out float scale,out Rect panel,out Rect art,out Rect items,out Rect damage,out Rect again,out Rect home);
            Rect area = new Rect(safe.x/scale,(size.y-safe.yMax)/scale,safe.width/scale,safe.height/scale);
            Check(area.Contains(panel.min)&&area.Contains(panel.max)&&panel.Contains(art.min)&&panel.Contains(art.max)&&
                panel.Contains(home.max)&&!items.Overlaps(damage)&&!again.Overlaps(home)&&!art.Overlaps(items)&&!items.Overlaps(again),
                $"Artwork, stats and buttons fit safe area without overlap: {size.x}x{size.y}");
        }

        previousKeyboard = Keyboard.current;
        previousInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.onBeforeUpdate += Feed;
        InputSystem.onAfterUpdate += Select;
        flow.BeginGame(); NoAutosave();
        Check(clock.SecondsRemaining == 30f && !clock.IsInspecting, "New run preserves the 30-second wait");
        var body = cat.GetComponent<Rigidbody>();
        body.position=hide.ExitAnchor.position;body.rotation=hide.ExitAnchor.rotation;body.linearVelocity=Vector3.zero;
        cat.transform.SetPositionAndRotation(body.position,body.rotation);Physics.SyncTransforms();
        yield return new WaitForSeconds(.3f);
        Check(cat.CanHideNearby && !cat.IsHidden, "Standing at the hide entrance alone is not hidden");
        yield return PressE();
        Check(cat.IsHidden && Vector3.Distance(body.position,hide.HideAnchor.position)<.04f,
            "Normal interaction input enters the actual cubby");
        Check(GameSaveStore.Capture(), "Hidden progress can be saved");
        yield return PressE();
        Check(!cat.IsHidden, "Same interaction input safely exits the cubby");
        Check(GameSaveStore.Restore()&&cat.IsHidden, "Resume restores actual hiding state, not just cat position");

        clock.RestoreCycle(false,.12f);
        yield return new WaitForSeconds(.25f);
        Check(clock.IsInspecting && !flow.CaughtVisible, "Countdown expiry begins normal girl inspection, not caught art");
        float remaining=clock.SecondsRemaining;
        flow.TogglePause();
        yield return new WaitForSecondsRealtime(.35f);
        Check(clock.SecondsRemaining==remaining && !flow.CaughtVisible, "Pause freezes inspection without a false catch");
        flow.TogglePause();
        float deadline=Time.time+4f;
        while(clock.IsInspecting && Time.time<deadline)yield return null;
        Check(!GameManager.I.IsFailed&&flow.State==GameFlowUI.ScreenState.Playing&&clock.SecondsRemaining>29.7f,
            "Hidden cat survives the full 3-second inspection and restarts 30 seconds");
        yield return Capture("01-hidden-safe");
        yield return PressE();
        Check(!cat.IsHidden,"Cat exits after a successful inspection");

        // Leave the entrance (nearby is deliberately still not hidden).
        clock.RestoreCycle(false,.12f);
        yield return new WaitForSeconds(.25f);
        Check(clock.IsInspecting&&!GameManager.I.IsFailed,"Exposed cat still has the full inspection window to hide");
        yield return PressE();
        Check(cat.IsHidden,"Can still hide after the girl's inspection has begun");
        deadline=Time.time+4;
        while(clock.IsInspecting&&Time.time<deadline)yield return null;
        Check(!GameManager.I.IsFailed&&!flow.CaughtVisible,"Accepted last-window hide avoids caught animation");
        yield return PressE();
        GameManager.I.AddChaos(7,"Validation book");
        GameManager.I.AddChaos(11,"Validation cup");
        int expectedItems=GameManager.I.Receipt.Count;
        long expectedAmount=GameManager.I.DamageAmount;
        // Let the real failure path archive exactly once, but all original saves are restored on exit.
        PlayerPrefs.SetString(GameSaveStore.HistoryKey,"{\"records\":[]}");
        typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,true);
        GameSaveStore.Capture();
        clock.RestoreCycle(false,.12f);
        deadline=Time.realtimeSinceStartup+6f;
        while(!GameManager.I.IsFailed&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(GameManager.I.IsFailed&&flow.State==GameFlowUI.ScreenState.Failure&&!clock.IsRunning&&!clock.IsInspecting,
            "Unhidden cat is caught once at inspection end and patrol stops");
        Check(Time.timeScale==0f&&!GameFlowUI.GameplayInputEnabled,"Caught screen freezes world physics and gameplay input");
        Check(flow.CaughtVisible&&flow.CaughtFrame==0&&!flow.CaughtActionsVisible,"Caught animation starts at first frame; buttons wait");
        Check(!Object.FindAnyObjectByType<ChaosJuiceUI>().GirlVisible,"Inspection portrait is not stacked over caught illustration");
        Check(flow.CaughtItems==expectedItems&&flow.CaughtDamage==expectedAmount&&expectedAmount==1800L,
            "Result uses actual 2 items / ¥1,800, not the reference's baked 12 / $280");
        yield return Capture("02-caught-grab");
        deadline=Time.realtimeSinceStartup+4f;
        while(flow.CaughtFrame<1&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(flow.CaughtFrame==1,"Second supplied frame plays while world time remains zero");
        yield return Capture("03-caught-lift");
        while(flow.CaughtFrame<2&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(flow.CaughtFrame==2,"Third supplied cuddle frame plays in order");
        yield return Capture("04-caught-cuddle");
        while(!flow.CaughtActionsVisible&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return Capture("05-result-buttons");
        Check(flow.CaughtActionsVisible&&flow.CaughtFrame==2,"Animation holds the final illustration and reveals result buttons");
        Check(GameSaveStore.ReadHistory().records.Count==1&&!PlayerPrefs.HasKey(GameSaveStore.SaveKey),
            "Failure archives once and clears failed Resume slot");
        GameManager.I.OnCaught();clock.TriggerAlert();
        yield return new WaitForSecondsRealtime(.2f);
        Check(GameSaveStore.ReadHistory().records.Count==1&&flow.CaughtFrame==2,"Repeated callbacks cannot restart animation or duplicate history");
        held=new[]{Key.W,Key.Space,Key.E};
        Vector3 frozen=body.position;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Vector3.Distance(frozen,body.position)<.001f&&!cat.IsHidden,"Movement, jump and hide are blocked behind result screen");
        held=Array.Empty<Key>();
        yield return null;
        RemoveInput();
        File.AppendAllText(Folder+"/report.txt","READY: awaiting actual Play Again button click (120 seconds).\n");
        // The user-facing buttons are exercised with the editor's actual pointer.
        var priorFlow=flow;
        deadline=Time.realtimeSinceStartup+120f;
        while(priorFlow&&Time.realtimeSinceStartup<deadline)yield return null;
        if(priorFlow){Check(false,"Play Again was not exercised before timeout");Finish();yield break;}
        yield return new WaitForSecondsRealtime(1.5f);Bind();NoAutosave();
        Check(flow&&flow.State==GameFlowUI.ScreenState.Playing&&!GameManager.I.IsFailed&&!flow.CaughtVisible&&
            clock.IsRunning&&!clock.IsInspecting&&GameManager.I.chaosScore==0,
            "Actual Play Again click reloads a fresh playable run with no stale caught overlay");
        Check(Object.FindObjectsByType<GameFlowUI>(FindObjectsSortMode.None).Length==1&&
            Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1,
            "Restart has one flow and one enabled gameplay camera");
        clock.RestoreCycle(true,.12f);
        yield return new WaitForSecondsRealtime(3f);
        Check(flow.CaughtVisible&&flow.CaughtActionsVisible,"Next exposed run can play caught animation again");
        File.AppendAllText(Folder+"/report.txt","READY: awaiting actual Home button click (120 seconds).\n");
        priorFlow=flow;deadline=Time.realtimeSinceStartup+120f;
        while(priorFlow&&Time.realtimeSinceStartup<deadline)yield return null;
        if(priorFlow){Check(false,"Home was not exercised before timeout");Finish();yield break;}
        yield return new WaitForSecondsRealtime(1.5f);Bind();
        Check(flow&&flow.State==GameFlowUI.ScreenState.MainMenu&&!flow.CaughtVisible&&!clock.IsRunning,
            "Actual Home click returns to cover without caught artwork or active patrol");
        Check(exceptions==0,"No runtime exceptions during inspection, animation or scene reload");
        Finish();
    }

    void Bind(){flow=Object.FindAnyObjectByType<GameFlowUI>();cat=Object.FindAnyObjectByType<CatController>();clock=OwnerAlert.I;}
    void NoAutosave(){if(flow)typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);}
    IEnumerator PressE(){held=new[]{Key.E};yield return new WaitForSeconds(.08f);held=Array.Empty<Key>();yield return new WaitForSeconds(.18f);}
    void Feed(){if(keyboard!=null&&InputState.currentUpdateType!=InputUpdateType.Editor){keyboard.MakeCurrent();InputState.Change(keyboard,new KeyboardState(held));}}
    void Select(){keyboard?.MakeCurrent();}
    void RemoveInput(){if(keyboard==null)return;InputSystem.onBeforeUpdate-=Feed;InputSystem.onAfterUpdate-=Select;InputSystem.RemoveDevice(keyboard);keyboard=null;previousKeyboard?.MakeCurrent();InputSystem.settings.editorInputBehaviorInPlayMode=previousInput;}
    void Log(string message,string stack,LogType type){if(type==LogType.Exception){exceptions++;File.AppendAllText(Folder+"/report.txt","EXCEPTION "+message+"\n");}}
    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        if(name.StartsWith("02-")||name.StartsWith("03-")||name.StartsWith("04-"))
        {
            GameFlowUI.CaughtLayout(new Vector2(Screen.width,Screen.height),Screen.safeArea,out float scale,out _,out Rect art,out _,out _,out _,out _);
            int x=Mathf.RoundToInt((art.x+art.width*.1f)*scale), y=Mathf.RoundToInt(Screen.height-(art.y+art.height*.9f)*scale);
            int size=Mathf.RoundToInt(art.width*.8f*scale);
            var pixels=image.GetPixels(x,y,size,size);
            int dark=pixels.Count(p=>p.r<.50f&&p.g<.45f&&p.b<.40f);
            Check(dark>pixels.Length*.08f,$"Rendered {name} contains visible illustration pixels, not a covering blank card");
        }
        File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
    }
    void Check(bool ok,string message){if(ok)passed++;else failed++;File.AppendAllText(Folder+"/report.txt",(ok?"PASS ":"FAIL ")+message+"\n");}
    IEnumerator Watchdog(){yield return new WaitForSecondsRealtime(300f);if(!finished){Check(false,"Timed out");Finish();}}
    void Finish(){if(finished)return;finished=true;NoAutosave();RemoveInput();Application.logMessageReceived-=Log;File.AppendAllText(Folder+"/report.txt",$"RESULT {passed} passed, {failed} failed. Visual review required.\n");Restore();EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;}
    static void Restore(){if(!SessionState.GetBool(Active,false))return;Application.runInBackground=SessionState.GetBool(Active+"Background",false);foreach(string key in Keys){if(SessionState.GetBool(Active+key+"Exists",false)){string value=SessionState.GetString(Active+key,"");if(key.EndsWith("HighScore"))PlayerPrefs.SetInt(key,int.Parse(value));else PlayerPrefs.SetString(key,value);}else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();SessionState.SetBool(Active,false);}
    void OnDestroy(){RemoveInput();Application.logMessageReceived-=Log;}
}
#endif
