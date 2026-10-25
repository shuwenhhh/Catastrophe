#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Opt-in Play Mode fixture only. Never saves or modifies the authored scene.
[InitializeOnLoad]
public sealed class CleanHudPlayValidation : MonoBehaviour
{
    static string Folder => SessionState.GetBool("PawJoystickCheck", false) ? "Validation/PawJoystick"
        : SessionState.GetBool("MobileJumpFeedbackCheck", false)
        ? "Validation/MobileJumpFeedback" : "Validation/CleanHud";
    const string Active = "CleanHudValidation";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly string[] SaveKeys = { GameSaveStore.SaveKey, GameSaveStore.HistoryKey, "Catastrophe.HighScore" };
    Keyboard keyboard, priorKeyboard;
    Mouse mouse, priorMouse;
    Touchscreen touch, priorTouch;
    InputSettings.EditorInputBehaviorInPlayMode oldInputBehavior;
    Vector2 touchPoint;
    bool touching, finished;
    int passed, failed;
    bool movingFinger;
    bool cancelTouch;
    Vector2 movingPoint;

    static CleanHudPlayValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += () => new GameObject("Disposable clean HUD validation").AddComponent<CleanHudPlayValidation>();
            if (state == PlayModeStateChange.EnteredEditMode) RestoreSaves();
        };
    }

    [MenuItem("Catastrophe/Verify Clean HUD In Play")]
    static void Run() => RunValidation(false);

    [MenuItem("Catastrophe/Verify Mobile Button Margins And Jump Ring")]
    static void RunMobile() => RunValidation(true);

    [MenuItem("Catastrophe/Verify Paw Joystick In Play")]
    static void RunPaw() => RunValidation(true, true);

    static void RunValidation(bool mobile, bool paw = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Active + "RunInBackground", Application.runInBackground);
        SessionState.SetBool("MobileJumpFeedbackCheck", mobile);
        SessionState.SetBool("PawJoystickCheck", paw);
        foreach (var key in SaveKeys)
        {
            SessionState.SetBool(Active + key + "Exists", PlayerPrefs.HasKey(key));
            SessionState.SetString(Active + key, key == "Catastrophe.HighScore"
                ? PlayerPrefs.GetInt(key, 0).ToString() : PlayerPrefs.GetString(key, ""));
        }
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/report.txt", (paw ? "Paw joystick" : mobile ? "Mobile buttons and jump ring" : "Clean HUD") +
            ": actual LivingRoom Play Mode, editor touch-input simulation.\n");
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return new WaitForSecondsRealtime(1.5f);
        var flow = Object.FindAnyObjectByType<GameFlowUI>();
        var cat = Object.FindAnyObjectByType<CatController>();
        Check(flow && cat && TargetManager.I && MobileCatControls.Instance, "Actual runtime services are present");
        if (!flow || !cat || !TargetManager.I || !MobileCatControls.Instance) { Finish(); yield break; }
        flow.BeginGame();
        typeof(GameFlowUI).GetField("runActive", Private).SetValue(flow, false);
        Check(typeof(TargetManager).GetMethod("OnGUI", Private) == null &&
              typeof(CatController).GetMethod("OnGUI", Private) == null,
              "No objective marker/bar or contextual tutorial renderer remains");
        Check(string.IsNullOrEmpty((string)typeof(GameBootstrap).GetField("toast", Private).GetValue(GameBootstrap.Instance)),
              "First entry has no operation instructions");

        var body = cat.GetComponent<Rigidbody>();
        // Stay clear of the newly authored cat-tree base when testing UI travel.
        // Furniture collision is verified separately; it must not mask stick input.
        body.position = new Vector3(.1f, .225f, -2.7f);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        cat.transform.SetPositionAndRotation(body.position, body.rotation);
        Physics.SyncTransforms();
        Camera.main.GetComponent<ThirdPersonCamera>().Initialize(cat.transform);
        yield return new WaitForSeconds(.5f);
        yield return Capture("01-clean-gameplay");

        oldInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        priorKeyboard = Keyboard.current; priorMouse = Mouse.current; priorTouch = Touchscreen.current;
        keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); touch = InputSystem.AddDevice<Touchscreen>();
        InputSystem.onBeforeUpdate += Feed;
        InputSystem.onAfterUpdate += SelectDevices;
        var controls = MobileCatControls.Instance;
        Vector2 stick = Control("StickCentre");
        float radius = (float)typeof(MobileCatControls).GetProperty("Radius", Private).GetValue(controls);
        Vector3 start = body.position;
        touchPoint = stick; touching = true;
        yield return new WaitForSeconds(.06f);
        touchPoint = stick + Vector2.right * radius * .65f;
        // Include the motor's normal turn-before-translate phase. A quarter
        // second can sample only the turn at variable editor frame rates.
        float movementDeadline=Time.time+.8f;
        while(Vector3.Distance(start,body.position)<.18f&&Time.time<movementDeadline)yield return null;
        Check(controls.Move.x > .5f && Vector3.Distance(start, body.position) > .05f,
            $"On-screen joystick still moves cat: input={controls.Move}, distance={Vector3.Distance(start, body.position):F3}");
        if (SessionState.GetBool("PawJoystickCheck", false))
        {
            Vector2 knob = (Vector2)typeof(MobileCatControls).GetField("stickPosition", Private).GetValue(controls);
            Check(Vector2.Distance(knob, stick + Vector2.right * radius * .65f * .64f) < .1f &&
                MobileCatControls.IsControlTouch(71), "Paw knob follows original stick travel without stealing camera input");
            yield return Capture("05-paw-dragging");
        }
        touching = false;
        yield return new WaitForSeconds(.25f);
        Check(controls.Move == Vector2.zero, "Joystick release stops input");
        if (SessionState.GetBool("PawJoystickCheck", false))
        {
            Vector2 knob = (Vector2)typeof(MobileCatControls).GetField("stickPosition", Private).GetValue(controls);
            Check(Vector2.Distance(knob, stick) < .1f && !MobileCatControls.IsControlTouch(71),
                "Paw knob recentres and releases touch ownership after lifting finger");
            yield return Capture("06-paw-released");
        }
        float beforeJump = body.position.y;
        touchPoint = Control("JumpCentre"); touching = true;
        yield return new WaitForSeconds(.14f);
        touching = false;
        yield return new WaitForSeconds(.1f);
        Check(body.position.y > beforeJump + .08f, "On-screen jump still launches the cat");
        yield return new WaitForSeconds(1.2f);
        // Test Push separately from the tree's context-sensitive Hide action.
        body.position = new Vector3(.1f, .225f, -2.7f);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        cat.transform.SetPositionAndRotation(body.position, body.rotation);
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.3f);
        Check(!cat.CanHideNearby, "Push fixture is outside hide triggers");
        touchPoint = Control("ActionCentre"); touching = true;
        yield return new WaitForSeconds(.1f);
        Check((bool)typeof(CatController).GetField("isPushing", Private).GetValue(cat), "Push button still starts paw interaction");
        touching = false;
        yield return new WaitForSeconds(.6f);

        // Regression of the objective tracker, independent of its removed view.
        TargetManager.I.CompleteTarget("ipad");
        Check(TargetManager.I.CurrentTargetId == "cups" && TargetManager.I.SavedTargetIndex == 1,
              "Objective progression remains active without objective text");
        GameBootstrap.Instance.ShowChaosToast(10, "Test item");
        yield return new WaitForSeconds(2.7f);
        Check(string.IsNullOrEmpty((string)typeof(GameBootstrap).GetField("toast", Private).GetValue(GameBootstrap.Instance)),
              "Temporary score feedback clears without restoring any tutorial slogan");
        flow.TogglePause();
        Check(flow.State == GameFlowUI.ScreenState.Paused && Time.timeScale == 0f, "Pause still works");
        flow.TogglePause();
        Check(GameFlowUI.GameplayInputEnabled && Time.timeScale == 1f, "Resume still works");
        // Show the uncluttered HUD with the original objective prop in frame.
        body.position = new Vector3(-.35f, .99f, -.65f);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        cat.transform.SetPositionAndRotation(body.position, body.rotation);
        Physics.SyncTransforms();
        Camera.main.GetComponent<ThirdPersonCamera>().Initialize(cat.transform);
        yield return new WaitForSeconds(.2f);
        yield return Capture("02-after-progress-and-resume");
        if (SessionState.GetBool("MobileJumpFeedbackCheck", false))
            yield return VerifyMobileFeedback(cat, body, flow);
        Finish();
    }

    IEnumerator VerifyMobileFeedback(CatController cat, Rigidbody body, GameFlowUI flow)
    {
        // Insets are relative to each phone's safe area (including a notch and
        // home indicator). These use the same layout functions as live input/UI.
        var sizes = new[] { new Vector2(844,390), new Vector2(932,430), new Vector2(1280,720),
            new Vector2(2340,1080), new Vector2(640,360), new Vector2(390,844) };
        foreach (var size in sizes)
        {
            Rect safe = new Rect(44, 21, size.x - 88, size.y - 21);
            float r = MobileCatControls.ControlRadius(size, safe);
            Vector2 j = MobileCatControls.JumpButtonCentre(safe, r);
            Vector2 a = MobileCatControls.ActionButtonCentre(safe, r);
            Vector2 stick = new Vector2(safe.xMin + r * 1.4f, safe.yMin + r * 1.45f);
            Check(j.x + r * 1.14f < safe.xMax && j.y - r * 1.14f > safe.yMin &&
                a.x - r > safe.xMin && a.y - r > safe.yMin &&
                Vector2.Distance(a,j) > r * 2.14f && Vector2.Distance(a,stick) > r * 2.3f,
                $"Safe layout {size.x}x{size.y}: right hit margin={safe.xMax-j.x-r*1.14f:F1}px, bottom={j.y-r*1.14f-safe.yMin:F1}px; no overlap");
        }
        var animator = cat.GetComponentInChildren<Animator>();
        Check(cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0 && animator &&
            animator.GetComponentInParent<CatController>() == cat && !animator.applyRootMotion,
            "Real skinned cat and Animator share the physics player; animation has no root motion");

        // The earlier composition shot intentionally stands on the cluttered
        // tabletop. Start ring checks on settled, unobstructed floor instead of
        // issuing the press while the body is separating from the tablet edge.
        touching=false;body.linearVelocity=Vector3.zero;
        body.position=new Vector3(.1f,.225f,-2.7f);body.rotation=Quaternion.identity;
        cat.transform.SetPositionAndRotation(body.position,body.rotation);
        Physics.SyncTransforms();Camera.main.GetComponent<ThirdPersonCamera>().Initialize(cat.transform);
        yield return new WaitForSeconds(.5f);

        // Get representative in-room screenshots at actual intermediate/full
        // progress; slow game time only to make the short feedback window readable.
        touching = true; touchPoint = Control("JumpCentre");
        Time.timeScale = .25f;
        yield return new WaitForSeconds(.11f);
        Check(cat.JumpHoldFeedbackActive && cat.JumpHoldProgress > 0f && cat.JumpHoldProgress < 1f,
            $"Accepted hold shows partial ring: {cat.JumpHoldProgress:F2}");
        yield return Capture("03-partial-ring");
        float chargeDeadline = Time.unscaledTime + 2f;
        while (cat.JumpHoldProgress < 1f && Time.unscaledTime < chargeDeadline) yield return null;
        Check(cat.JumpHoldFeedbackActive && cat.JumpHoldProgress >= .99f,
            $"Hold past actual release window fills ring: progress={cat.JumpHoldProgress:F2}");
        yield return Capture("04-full-ring");
        touching = false;
        yield return new WaitForSeconds(.04f);
        Check(!cat.JumpHoldFeedbackActive && cat.JumpHoldProgress == 0f, "Release clears ring immediately");
        Time.timeScale = 1f;
        yield return new WaitForSeconds(1.3f);

        // A temporary unobstructed platform isolates jump height from furniture.
        // Actual touch input, physics and the real Animator stay enabled.
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Disposable jump feedback platform";
        platform.transform.position = new Vector3(30,-.1f,30);
        platform.transform.localScale = new Vector3(6,.2f,6);
        body.position = new Vector3(30,.225f,30); body.linearVelocity = Vector3.zero;
        body.rotation = Quaternion.identity;
        cat.transform.SetPositionAndRotation(body.position, body.rotation);
        Physics.SyncTransforms();
        Camera.main.GetComponent<ThirdPersonCamera>().Initialize(cat.transform);
        yield return new WaitForSeconds(.5f);
        float shortApex = 0f, longApex = 0f;
        for (int trial = 0; trial < 2; trial++)
        {
            float baseY = body.position.y, apex = 0f, began = Time.time;
            bool sawJump = false, sawTuck = false, full = false;
            touching = true; touchPoint = Control("JumpCentre");
            while (Time.time - began < 1.6f)
            {
                if (Time.time - began > (trial == 0 ? .05f : .42f)) touching = false;
                apex = Mathf.Max(apex, body.position.y - baseY);
                sawJump |= animator.GetCurrentAnimatorStateInfo(0).IsName("Jump");
                sawTuck |= animator.GetFloat("JumpPhase") > .45f && animator.GetFloat("JumpPhase") < .8f;
                full |= cat.JumpHoldProgress >= .99f;
                yield return null;
            }
            if (trial == 0) shortApex = apex; else longApex = apex;
            Check(sawJump && sawTuck && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") &&
                Mathf.Abs(body.position.y-baseY) < .04f && !cat.JumpHoldFeedbackActive,
                $"{(trial==0?"Tap":"Hold")} jump/pose/landing returns to Idle: rise={apex:F3}m");
            Check(full == (trial == 1), trial == 0 ? "Tap never falsely reaches full ring" : "Full ring corresponds to held high jump");
        }
        Check(shortApex > .35f && shortApex < .65f && longApex > 1.1f && longApex < 1.5f && longApex > shortApex + .5f,
            $"Unchanged short/high jump ranges: {shortApex:F3}m / {longApex:F3}m");

        // Two-finger regression: stick + new enlarged jump hit target, no orbit.
        float yaw = Camera.main.GetComponent<ThirdPersonCamera>().OrbitYaw;
        movingPoint = Control("StickCentre"); movingFinger = true;
        touching = true; touchPoint = Control("JumpCentre");
        yield return new WaitForSeconds(.06f);
        movingPoint += Vector2.up * (float)typeof(MobileCatControls).GetProperty("Radius",Private).GetValue(MobileCatControls.Instance) * .6f;
        yield return new WaitForSeconds(.14f);
        Check(MobileCatControls.Instance.Move.y > .4f && cat.JumpHoldFeedbackActive &&
            Mathf.Abs(Mathf.DeltaAngle(yaw,Camera.main.GetComponent<ThirdPersonCamera>().OrbitYaw)) < .1f,
            "Simultaneous stick + jump retains independent touch ownership and camera angle");
        flow.TogglePause();
        yield return new WaitForSecondsRealtime(.12f);
        Check(!cat.JumpHoldFeedbackActive && !MobileCatControls.Instance.JumpHeld, "Pause clears held input and ring");
        touching = movingFinger = false;
        flow.TogglePause();
        yield return new WaitForSeconds(1.2f);
        Check(!cat.JumpHoldFeedbackActive, "Resume does not leave stale ring");

        // Hit the visible outer ring, not just the green disc. Ownership must
        // stay with Jump even if the finger drifts off it while held.
        float liveRadius = (float)typeof(MobileCatControls).GetProperty("Radius",Private).GetValue(MobileCatControls.Instance);
        touchPoint = Control("JumpCentre") + Vector2.right * liveRadius * 1.06f;
        touching = true;
        yield return new WaitForSeconds(.1f);
        Check(MobileCatControls.Instance.JumpHeld && MobileCatControls.IsScreenPointInControls(touchPoint) &&
            cat.JumpHoldFeedbackActive, "Visible ring belongs to jump hit target, not camera drag");
        touchPoint += Vector2.left * liveRadius * 3f;
        yield return new WaitForSeconds(.1f);
        Check(MobileCatControls.Instance.JumpHeld && MobileCatControls.IsControlTouch(71),
            "Finger drifting outside retains original jump ownership");
        cancelTouch = true; touching = false;
        yield return new WaitForSeconds(.1f);
        Check(!MobileCatControls.Instance.JumpHeld && !cat.JumpHoldFeedbackActive,
            "Canceled touch releases jump and clears ring");
        cancelTouch = false;
        yield return new WaitForSeconds(1.3f);
        touchPoint = Control("JumpCentre"); touching = true;
        yield return new WaitForSeconds(1.6f);
        Check(!cat.JumpHoldFeedbackActive && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),
            "Holding through landing clears ring without auto-repeat jump");
        touching = false;
        yield return new WaitForSeconds(.1f);
        Object.Destroy(platform);
    }

    Vector2 Control(string name) => (Vector2)typeof(MobileCatControls).GetProperty(name, Private).GetValue(MobileCatControls.Instance);
    void SelectDevices() { keyboard?.MakeCurrent(); mouse?.MakeCurrent(); touch?.MakeCurrent(); }
    void Feed()
    {
        if (keyboard == null || InputState.currentUpdateType == InputUpdateType.Editor) return;
        SelectDevices();
        InputState.Change(keyboard, new KeyboardState());
        InputState.Change(mouse, new MouseState());
        InputState.Change(touch.touches[0], new TouchState { touchId = 71, position = touchPoint,
            phase = cancelTouch ? UnityEngine.InputSystem.TouchPhase.Canceled :
                touching ? UnityEngine.InputSystem.TouchPhase.Moved : UnityEngine.InputSystem.TouchPhase.Ended });
        InputState.Change(touch.touches[1], new TouchState { touchId = 72, position = movingPoint,
            phase = movingFinger ? UnityEngine.InputSystem.TouchPhase.Moved : UnityEngine.InputSystem.TouchPhase.Ended });
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder + "/" + name + ".png", image.EncodeToPNG());
        Object.Destroy(image);
    }
    void Check(bool ok, string message)
    {
        if (ok) passed++; else failed++;
        File.AppendAllText(Folder + "/report.txt", (ok ? "PASS " : "FAIL ") + message + "\n");
    }
    void Finish()
    {
        if (finished) return;
        finished = true;
        File.AppendAllText(Folder + "/report.txt", $"Result: {passed} passed, {failed} failed. Screenshot visual review required.\n");
        CleanupInput();
        var flow = Object.FindAnyObjectByType<GameFlowUI>();
        if (flow) typeof(GameFlowUI).GetField("runActive", Private).SetValue(flow, false);
        RestoreSaves();
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    void CleanupInput()
    {
        InputSystem.onBeforeUpdate -= Feed;
        InputSystem.onAfterUpdate -= SelectDevices;
        if (keyboard == null) return;
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(touch);
        keyboard = null; mouse = null; touch = null;
        priorKeyboard?.MakeCurrent(); priorMouse?.MakeCurrent(); priorTouch?.MakeCurrent();
        InputSystem.settings.editorInputBehaviorInPlayMode = oldInputBehavior;
    }
    void OnDestroy() => CleanupInput();
    static void RestoreSaves()
    {
        if (!SessionState.GetBool(Active, false)) return;
        Application.runInBackground = SessionState.GetBool(Active + "RunInBackground", false);
        foreach (var key in SaveKeys)
        {
            if (SessionState.GetBool(Active + key + "Exists", false))
            {
                string value = SessionState.GetString(Active + key, "");
                if (key == "Catastrophe.HighScore") PlayerPrefs.SetInt(key, int.Parse(value));
                else PlayerPrefs.SetString(key, value);
            }
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        SessionState.SetBool(Active, false);
    }
}
#endif
