#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Opt-in runtime evidence. Preserves player saves, never saves authored scenes.
[InitializeOnLoad]
public sealed class PatrolLoopPlayValidation : MonoBehaviour
{
    const string Active = "PatrolLoopValidation";
    const string Folder = "Validation/PatrolLoop";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly string[] SaveKeys = { GameSaveStore.SaveKey, GameSaveStore.HistoryKey, "Catastrophe.HighScore" };
    readonly List<GameObject> fixtures = new();
    int passed, failed, visits;
    bool finished;
    float visitStart, visitEnd;
    OwnerAlert clock;
    GameFlowUI flow;

    static PatrolLoopPlayValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += () => new GameObject("Disposable patrol verification").AddComponent<PatrolLoopPlayValidation>();
            if (state == PlayModeStateChange.EnteredEditMode) RestoreSaves();
        };
    }

    [MenuItem("Catastrophe/Verify Patrol Countdown In Play")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Active + "RunInBackground", Application.runInBackground);
        foreach (string key in SaveKeys)
        {
            SessionState.SetBool(Active + key + "Exists", PlayerPrefs.HasKey(key));
            SessionState.SetString(Active + key, key == "Catastrophe.HighScore"
                ? PlayerPrefs.GetInt(key, 0).ToString() : PlayerPrefs.GetString(key, ""));
        }
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/report.txt", "Actual LivingRoom Play Mode: 30s patrol / 3s supplied girl animation.\n");
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        StartCoroutine(Watchdog());
        yield return new WaitForSecondsRealtime(1.5f);
        clock = OwnerAlert.I;
        flow = Object.FindAnyObjectByType<GameFlowUI>();
        var cat = Object.FindAnyObjectByType<CatController>();
        var hud = Object.FindAnyObjectByType<ChaosJuiceUI>();
        Check(clock && flow && cat && hud && GameManager.I, "Actual runtime services exist");
        if (!clock || !flow || !cat || !hud || !GameManager.I) { Finish(); yield break; }
        Check(!clock.IsRunning && clock.SecondsRemaining == 30f && !hud.GirlVisible,
            "Main menu does not advance the patrol or show girl");
        Check(Object.FindObjectsByType<OwnerAlert>(FindObjectsSortMode.None).Length == 1 &&
            Object.FindObjectsByType<ChaosJuiceUI>(FindObjectsSortMode.None).Length == 1,
            "Exactly one timer and one status HUD");
        Check(typeof(OwnerAlert).GetMethod("OnGUI", Private) == null &&
            typeof(GameBootstrap).GetMethod("OnGUI", Private) == null,
            "Legacy alert box, title and duplicate score renderers removed");
        Check(hud.GirlArtworkLoaded, "User-supplied three-frame girl texture loads from runtime Resources");
        var artwork = hud.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
        Check(artwork && !artwork.raycastTarget &&
            !hud.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(true),
            "Colour-correct girl artwork does not capture gameplay touches");
        var anim = cat.GetComponentInChildren<Animator>();
        Check(cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0 && anim &&
            anim.GetComponentInParent<CatController>() == cat && !anim.applyRootMotion,
            "Original 3D cat/Animator/physics identity is preserved");

        var body = cat.GetComponent<Rigidbody>();
        body.position = new Vector3(-.35f, .99f, -.65f);
        body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero;
        cat.transform.SetPositionAndRotation(body.position, body.rotation);
        Physics.SyncTransforms();
        Camera.main.GetComponent<ThirdPersonCamera>().Initialize(cat.transform);
        clock.OnInspectionChanged += TrackVisit;
        flow.BeginGame();
        typeof(GameFlowUI).GetField("runActive", Private).SetValue(flow, false);
        Check(clock.IsRunning && !clock.IsInspecting && clock.SecondsRemaining == 30f,
            "New game begins with exactly 30 seconds");
        Check(GameManager.I.DamageAmount == 0L, "New game damage amount starts at zero");
        yield return Capture("01-clean-30-seconds");
        float before = clock.SecondsRemaining, stamp = Time.time;
        yield return new WaitForSeconds(31f);
        Check(clock.SecondsRemaining == before && !clock.IsInspecting && visits == 0,
            "No destruction for more than 30 seconds: budget stays full, no visit");
        before = clock.SecondsRemaining;
        flow.TogglePause();
        yield return new WaitForSecondsRealtime(.4f);
        Check(clock.SecondsRemaining == before && !hud.GirlVisible, "Pause freezes countdown and gameplay HUD");
        flow.TogglePause();

        NoiseManager.I.EmitNoise(100f);
        Check(!clock.IsInspecting, "Old noise threshold no longer opens warning or triggers a visit");
        foreach (var size in new[] { new Vector2(844,390), new Vector2(932,430), new Vector2(1280,720),
            new Vector2(2340,1080), new Vector2(640,360), new Vector2(390,844) })
        {
            Rect safe = new Rect(44,21,size.x-88,size.y-21);
            ChaosJuiceUI.Layout(size,safe,out float scale,out Rect amount,out Rect timer,out Rect portrait);
            Rect logicalSafe = new Rect(safe.x/scale,(size.y-safe.yMax)/scale,safe.width/scale,safe.height/scale);
            Check(logicalSafe.Contains(amount.min) && logicalSafe.Contains(amount.max) &&
                logicalSafe.Contains(timer.min) && logicalSafe.Contains(timer.max) &&
                logicalSafe.Contains(portrait.min) && logicalSafe.Contains(portrait.max) &&
                !amount.Overlaps(timer), $"HUD safe-area layout {size.x}x{size.y}");
        }

        yield return DropAndCheck(8f, 7, 2f);
        yield return DropAndCheck(30f, 11, 4f);
        yield return DropAndCheck(65f, 13, 6f);
        Check(GameManager.I.DamageAmount == 3100L, "Damage amount sums existing prices: ¥3,100");
        Check(string.IsNullOrEmpty((string)typeof(GameBootstrap).GetField("toast", Private).GetValue(GameBootstrap.Instance)),
            "Destruction adds no narration, warning or money popup");
        yield return Capture("02-damage-shortens-clock");

        // A successful patrol now requires hiding; caught paths have their own test.
        var safeHide = GameObject.Find("Hide Spot - Cat Tree Cubby");
        cat.EnterHide(safeHide);
        Check(cat.IsHidden, "Cat is genuinely hidden for the successful patrol-cycle regression");
        before = clock.SecondsRemaining;
        yield return new WaitForSeconds(1f);
        Check(clock.SecondsRemaining == before && !clock.IsInspecting,
            "After damage, quiet time still does not reduce the remaining budget");
        // Actual falls, not elapsed time or a direct forced visit, exhaust the budget.
        foreach(var item in new[]{Drop(65f,1),Drop(65f,1),Drop(65f,1)}) item.ActivatePhysics();
        float deadline = Time.time + 3f;
        while (!clock.IsInspecting && Time.time < deadline) yield return null;
        Check(clock.IsInspecting && visits == 1 && clock.CountdownRemaining == 0f && hud.GirlVisible,
            "Destruction exhausts budget and starts exactly one visible girl visit");
        Check(clock.InspectionFrame == 0, "Girl begins looking left");
        yield return Capture("03-girl-left");
        before = clock.SecondsRemaining;
        flow.TogglePause();
        yield return new WaitForSecondsRealtime(.4f);
        Check(clock.SecondsRemaining == before && !hud.GirlVisible, "Pause also freezes a visit; girl is not over pause menu");
        flow.TogglePause();
        Check(hud.GirlVisible && GameFlowUI.GameplayInputEnabled, "Resume restores same girl phase without blocking play");
        long money = GameManager.I.DamageAmount;
        var duringVisit = Drop(25f, 5);
        duringVisit.ActivatePhysics();
        float visitDeadline = Time.time + 4f;
        while (clock.IsInspecting && clock.InspectionFrame < 1 && Time.time < visitDeadline) yield return null;
        Check(clock.IsInspecting && clock.InspectionFrame == 1, "Second second shows supplied right-looking frame");
        yield return Capture("04-girl-right");
        while (clock.IsInspecting && clock.InspectionFrame < 2 && Time.time < visitDeadline) yield return null;
        Check(clock.IsInspecting && clock.InspectionFrame == 2, "Third second shows supplied front/smile frame");
        yield return Capture("05-girl-front");
        while (clock.IsInspecting && Time.time < visitDeadline) yield return null;
        Check(!clock.IsInspecting && !hud.GirlVisible && clock.SecondsRemaining > 29.8f,
            "Girl disappears and next cycle resets to 30 seconds");
        Check(visitEnd - visitStart >= 2.98f && visitEnd - visitStart < 3.15f,
            $"Visit lasts 3 game seconds, measured {visitEnd-visitStart:F3}s");
        Check(duringVisit.CaptureSave().destroyed && GameManager.I.DamageAmount == money + 500 && visits == 1,
            "Destruction during visit still earns money but cannot shorten/restart visit");
        Check(!GameManager.I.IsFailed && flow.State == GameFlowUI.ScreenState.Playing,
            "Hidden cat has no caught/failure screen; play continues after visit");
        yield return Capture("06-next-cycle");

        // Many objects falling together at the deadline must not stack visits.
        clock.RestoreCycle(false, 1f);
        int priorVisits = visits;
        var burst = new[] { Drop(65f,1), Drop(65f,1), Drop(65f,1) };
        foreach (var item in burst) item.ActivatePhysics();
        yield return new WaitForSeconds(.7f);
        Check(clock.IsInspecting && visits == priorVisits + 1 && clock.SecondsRemaining > 2f &&
            Array.TrueForAll(burst, v => v.CaptureSave().destroyed),
            "Simultaneous loud falls clamp at zero and trigger exactly one 3s visit");

        clock.RestoreCycle(false, 14.25f);
        Check(GameSaveStore.Capture(), "Waiting state can save");
        clock.BeginRun();
        Check(GameSaveStore.Restore() && !clock.IsInspecting && Mathf.Abs(clock.SecondsRemaining-14.25f)<.02f,
            "Resume preserves remaining waiting time and money");
        clock.RestoreCycle(true, 1.5f);
        Check(GameSaveStore.Capture(), "In-progress visit can save");
        clock.BeginRun();
        Check(GameSaveStore.Restore() && clock.IsInspecting && clock.InspectionFrame == 1 &&
            Mathf.Abs(clock.SecondsRemaining-1.5f)<.02f, "Resume restores visit phase and correct girl frame");
        var legacy = GameSaveStore.Read();
        legacy.hasPatrolState = false; legacy.alertRemaining = .1f;
        PlayerPrefs.SetString(GameSaveStore.SaveKey,JsonUtility.ToJson(legacy));
        Check(GameSaveStore.Restore() && !clock.IsInspecting && clock.SecondsRemaining == 30f,
            "Old saves migrate to fresh 30s clock instead of obsolete caught timer");
        clock.RestoreCycle(true, 1f);
        flow.BeginGame();
        typeof(GameFlowUI).GetField("runActive", Private).SetValue(flow, false);
        Check(!clock.IsInspecting && clock.SecondsRemaining == 30f && !hud.GirlVisible,
            "Starting a new run clears any previous visit");
        Finish();
    }

    Valuable Drop(float noise, int points)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Disposable patrol fall " + fixtures.Count;
        go.transform.position = new Vector3(25f+fixtures.Count*2f,2f,25f);
        go.transform.localScale = Vector3.one*.2f;
        var rb = go.AddComponent<Rigidbody>();
        var value = go.AddComponent<Valuable>();
        value.itemName = go.name; value.noise = noise; value.chaosValue = points;
        fixtures.Add(go);
        return value;
    }

    IEnumerator DropAndCheck(float noise, int points, float expectedPenalty)
    {
        Valuable value = Drop(noise,points);
        long money = GameManager.I.DamageAmount;
        float before = clock.SecondsRemaining;
        value.MarkDestroyed();
        Check(GameManager.I.DamageAmount == money && clock.SecondsRemaining == before,
            "Unfallen prop cannot award money or deduct time");
        value.ActivatePhysics();
        float stamp = Time.time, deadline = Time.time + 2f;
        while (!value.CaptureSave().destroyed && Time.time < deadline) yield return null;
        float deduction = before - clock.SecondsRemaining;
        Check(value.CaptureSave().destroyed && Mathf.Abs(deduction-expectedPenalty)<.1f &&
            GameManager.I.DamageAmount == money + points*100L,
            $"Actual gravity fall noise={noise}: penalty={deduction:F2}s, expected={expectedPenalty}s; money once");
        before = clock.SecondsRemaining; money = GameManager.I.DamageAmount;
        value.MarkDestroyed(); value.MarkDestroyed();
        Check(clock.SecondsRemaining == before && GameManager.I.DamageAmount == money,
            "Repeated destruction callbacks do not duplicate money/time loss");
    }

    void TrackVisit(bool inspecting)
    {
        if (inspecting) { visits++; visitStart=Time.time; }
        else visitEnd=Time.time;
    }
    IEnumerator Watchdog()
    {
        yield return new WaitForSecondsRealtime(90f);
        if (finished) yield break;
        Check(false, $"Timed out: flow={flow?.State}, timeScale={Time.timeScale}, remaining={clock?.SecondsRemaining}, inspecting={clock?.IsInspecting}");
        Finish();
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());
        Object.Destroy(texture);
    }
    void Check(bool ok,string message)
    {
        if(ok)passed++;else failed++;
        File.AppendAllText(Folder+"/report.txt",(ok?"PASS ":"FAIL ")+message+"\n");
    }
    void Finish()
    {
        if(finished)return; finished=true;
        File.AppendAllText(Folder+"/report.txt",$"Result: {passed} passed, {failed} failed. Visual review required.\n");
        if(clock)clock.OnInspectionChanged-=TrackVisit;
        if(flow)typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);
        foreach(var go in fixtures)if(go)Object.Destroy(go);
        RestoreSaves();
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    static void RestoreSaves()
    {
        if(!SessionState.GetBool(Active,false))return;
        Application.runInBackground = SessionState.GetBool(Active + "RunInBackground", false);
        foreach(string key in SaveKeys)
            if(SessionState.GetBool(Active+key+"Exists",false))
            {
                string value=SessionState.GetString(Active+key,"");
                if(key=="Catastrophe.HighScore")PlayerPrefs.SetInt(key,int.Parse(value));
                else PlayerPrefs.SetString(key,value);
            }
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save(); SessionState.SetBool(Active,false);
    }
}
#endif
