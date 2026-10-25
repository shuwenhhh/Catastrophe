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

// Disposable Play Mode fixture. Inputs go through the same Mouse, Touchscreen
// and Keyboard controls as normal play; the authored scene is never saved.
[InitializeOnLoad]
public sealed class OrbitCameraPlayValidation : MonoBehaviour
{
    string Folder => compositionOnly ? "Validation/DefaultCameraComposition" : "Validation/ManualOrbitCamera";
    bool compositionOnly, saveBackedUp;
    string savedResume, savedHistory;
    bool hadResume, hadHistory;
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    CatController cat;
    Rigidbody body;
    ThirdPersonCamera orbit;
    Camera lens;
    Keyboard keyboard, priorKeyboard;
    Mouse mouse, priorMouse;
    Touchscreen touch, priorTouch;
    InputSettings.EditorInputBehaviorInPlayMode oldInputBehavior;
    readonly List<string> report = new();
    Key[] keys = Array.Empty<Key>();
    Vector2 pointer;
    bool mouseHeld, stickFinger, lookFinger, buttonFinger, finished;
    Vector2 stickPosition, lookPosition, buttonPosition;
    float began;
    int passed, failed;

    static OrbitCameraPlayValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ManualOrbitCheck", false)) return;
            SessionState.SetBool("ManualOrbitCheck", false);
            EditorApplication.delayCall += () => new GameObject("Disposable manual orbit check").AddComponent<OrbitCameraPlayValidation>();
        };
    }

    [MenuItem("Catastrophe/Camera/Verify Manual Orbit In Play")]
    static void Run()
    {
        if (Application.isPlaying)
            new GameObject("Disposable manual orbit check").AddComponent<OrbitCameraPlayValidation>();
        else { SessionState.SetBool("ManualOrbitCheck", true); EditorApplication.isPlaying = true; }
    }

    [MenuItem("Catastrophe/Camera/Verify Default Composition In Play")]
    static void RunComposition()
    {
        SessionState.SetBool("CameraCompositionCheck", true);
        Run();
    }

    IEnumerator Start()
    {
        compositionOnly = SessionState.GetBool("CameraCompositionCheck", false);
        SessionState.SetBool("CameraCompositionCheck", false);
        Directory.CreateDirectory(Folder);
        float setupDeadline=Time.realtimeSinceStartup+4f;
        do
        {
            cat = Object.FindFirstObjectByType<CatController>();
            lens = Camera.main;
            orbit = lens ? lens.GetComponent<ThirdPersonCamera>() : null;
            if(cat&&orbit)break;
            yield return null;
        }while(Time.realtimeSinceStartup<setupDeadline);
        if (!cat || !orbit) { Check(false, $"Runtime player and orbit camera exist (cat={cat}, main={lens}, orbit={orbit})"); Finish(); yield break; }
        body = cat.GetComponent<Rigidbody>();
        hadResume = PlayerPrefs.HasKey(GameSaveStore.SaveKey);
        hadHistory = PlayerPrefs.HasKey(GameSaveStore.HistoryKey);
        savedResume = PlayerPrefs.GetString(GameSaveStore.SaveKey, "");
        savedHistory = PlayerPrefs.GetString(GameSaveStore.HistoryKey, "");
        saveBackedUp = true;
        var flow = Object.FindFirstObjectByType<GameFlowUI>();
        flow.BeginGame();
        // Camera fixtures must never become the player's Resume position.
        typeof(GameFlowUI).GetField("runActive", Private).SetValue(flow, false);
        began = Time.realtimeSinceStartup;
        oldInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        priorKeyboard = Keyboard.current; priorMouse = Mouse.current; priorTouch = Touchscreen.current;
        keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); touch = InputSystem.AddDevice<Touchscreen>();
        InputSystem.onBeforeUpdate += Feed;
        InputSystem.onAfterUpdate += SelectDevices;
        yield return new WaitForSeconds(.7f);
        Check(Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1 &&
            Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1 && orbit.FollowTarget == cat.transform &&
            cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0,
            "One live camera follows the one skinned 3D player");

        if (compositionOnly) { yield return VerifyComposition(); Finish(); yield break; }

        Fixture(new Vector3(-.55f, .225f, -1.55f), 0f);
        yield return new WaitForSeconds(.4f);
        Check(orbit.OrbitPitch > 20f && orbit.OrbitPitch < 45f &&
            Vector3.Dot(Vector3.ProjectOnPlane(lens.transform.position - cat.transform.position, Vector3.up).normalized, -cat.transform.forward) > .98f,
            $"Initial rear/above composition: pitch={orbit.OrbitPitch:F1}, distance={orbit.FramingDistance:F2}");
        CheckFraming("initial"); Capture("01-initial-rear");
        float catYaw = body.rotation.eulerAngles.y;
        yield return Drag(new Vector2(.2f, .55f), new Vector2(.7f, .55f), .6f);
        Check(Mathf.Abs(Mathf.DeltaAngle(orbit.OrbitYaw, 180f)) < 4f && Mathf.Abs(Mathf.DeltaAngle(catYaw, body.rotation.eulerAngles.y)) < .1f,
            $"Mouse drag reaches front without turning idle cat: yaw={orbit.OrbitYaw:F2}");
        Capture("02-manual-front");
        float keptYaw = orbit.OrbitYaw;
        yield return new WaitForSeconds(1f);
        Check(Mathf.Abs(Mathf.DeltaAngle(keptYaw, orbit.OrbitYaw)) < .2f, "Released mouse retains selected world yaw");
        body.rotation = Quaternion.Euler(0f, 90f, 0f); cat.transform.rotation = body.rotation;
        body.position += Vector3.right * .3f; cat.transform.position = body.position;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.5f);
        Check(Mathf.Abs(Mathf.DeltaAngle(keptYaw, orbit.OrbitYaw)) < .2f, "Character turn and movement do not recenter orbit");
        yield return Drag(new Vector2(.7f, .55f), new Vector2(.2f, .55f), .6f);
        Check(Mathf.Abs(Mathf.DeltaAngle(orbit.OrbitYaw, 0f)) < 4f, "Reverse manual drag returns to original viewing side");
        yield return Drag(new Vector2(.5f, .35f), new Vector2(.5f, .65f), .4f);
        Check(orbit.OrbitPitch > 55f && orbit.OrbitPitch <= 70f, $"Vertical drag enables overhead angle: {orbit.OrbitPitch:F2}");
        Capture("03-high-angle");
        yield return Drag(new Vector2(.5f, .85f), new Vector2(.5f, .20f), .4f);
        Check(orbit.OrbitPitch >= 5f && orbit.OrbitPitch < 12f && Vector3.Dot(lens.transform.right, Vector3.up) < .001f,
            $"Vertical drag enables near-level angle, no roll: {orbit.OrbitPitch:F2}");
        Capture("04-low-angle");

        Fixture(new Vector3(1.7f, .225f, -2.35f), 0f);
        yield return new WaitForSeconds(.3f);
        float r = (float)typeof(MobileCatControls).GetProperty("Radius", Private).GetValue(MobileCatControls.Instance);
        Vector2 stick = Control("StickCentre"), jump = Control("JumpCentre"), action = Control("ActionCentre");
        float controlYaw = orbit.OrbitYaw;
        foreach (var p in new[] { stick, jump, action })
        {
            buttonPosition = p; buttonFinger = true;
            yield return new WaitForSeconds(.12f);
            buttonPosition += Vector2.right * r * .35f;
            yield return new WaitForSeconds(.12f);
            buttonFinger = false;
            yield return new WaitForSeconds(.15f);
        }
        Check(Mathf.Abs(Mathf.DeltaAngle(controlYaw, orbit.OrbitYaw)) < .1f, "Stick/jump/push touches never rotate camera");
        // Separate fingers operate the real mobile controls simultaneously.
        body.isKinematic = false;
        stickPosition = stick; stickFinger = true;
        lookPosition = new Vector2(Screen.width * .4f, Screen.height * .55f); lookFinger = true;
        yield return null; yield return null;
        stickPosition = stick + Vector2.up * r * .65f;
        Vector3 beforeWalk = body.position;
        float start = Time.time;
        while (Time.time - start < .5f)
        {
            lookPosition = new Vector2(Screen.width * (.4f + .2f * Mathf.Clamp01((Time.time - start) / .5f)), Screen.height * .55f);
            yield return null;
        }
        lookFinger = false;
        yield return new WaitForSeconds(.25f);
        var move = (Vector3)typeof(CatController).GetField("moveDirection", Private).GetValue(cat);
        Vector3 expected = CatController.CameraRelativeDirection(MobileCatControls.Instance.Move, orbit.PlanarRotation);
        Check(MobileCatControls.Instance.Move.magnitude > .4f && Mathf.Abs(Mathf.DeltaAngle(orbit.OrbitYaw, 0f)) > 50f &&
            Vector3.Dot(move.normalized, expected.normalized) > .98f && Vector3.Distance(beforeWalk, body.position) > .05f,
            $"Two-finger move + orbit, live camera-relative direction: moved={Vector3.Distance(beforeWalk, body.position):F3} yaw={orbit.OrbitYaw:F1}");
        stickFinger = false;
        yield return new WaitForSeconds(.4f);
        float idleHeading = body.rotation.eulerAngles.y; keptYaw = orbit.OrbitYaw;
        yield return new WaitForSeconds(.5f);
        Check(Mathf.Abs(Mathf.DeltaAngle(idleHeading, body.rotation.eulerAngles.y)) < .5f &&
            Mathf.Abs(Mathf.DeltaAngle(keptYaw, orbit.OrbitYaw)) < .1f, "Stopping preserves cat heading and camera yaw");

        // A look gesture may cross a button without becoming a jump gesture.
        Fixture(new Vector3(-.55f, .225f, -1.55f), 0f);
        lookPosition = new Vector2(Screen.width * .5f, Screen.height * .5f); lookFinger = true;
        yield return new WaitForSeconds(.1f);
        lookPosition = jump;
        yield return new WaitForSeconds(.15f);
        Check(!MobileCatControls.Instance.JumpHeld, "Dragging world touch across Jump does not steal the gesture");
        lookFinger = false;
        yield return new WaitForSeconds(.2f);

        Fixture(new Vector3(1.7f, .225f, -2.35f), 0f);
        body.isKinematic=false;
        yield return new WaitForSeconds(.25f);
        keys=new[]{Key.Space};
        float jumpingAt=Time.time,apex=.225f,maxYawDrift=0f;
        bool jumpCapture=false;
        while(Time.time-jumpingAt<1.8f)
        {
            if(Time.time-jumpingAt>.22f)keys=Array.Empty<Key>();
            apex=Mathf.Max(apex,body.position.y);
            maxYawDrift=Mathf.Max(maxYawDrift,Mathf.Abs(Mathf.DeltaAngle(orbit.OrbitYaw,0f)));
            if(!jumpCapture&&Time.time-jumpingAt>.36f){Capture("04-jump-follow");jumpCapture=true;}
            yield return null;
        }
        Check(apex>.7f&&Mathf.Abs(body.position.y-.225f)<.10f&&maxYawDrift<.1f,
            $"Real jump and landing preserve orbit: apex={apex:F3} landed={body.position.y:F3} yaw drift={maxYawDrift:F3}");

        Fixture(new Vector3(.55f,.225f,-1.75f),0f);
        body.isKinematic=false;
        yield return new WaitForSeconds(.25f);
        keys=new[]{Key.W};float walkingAt=Time.time;bool tableCapture=false;
        float closestView=float.PositiveInfinity;
        while(Time.time-walkingAt<6f&&body.position.z<.55f)
        {
            closestView=Mathf.Min(closestView,orbit.FramingDistance);
            if(!tableCapture&&body.position.z>-.25f){Capture("04-live-under-table");tableCapture=true;}
            yield return null;
        }
        keys=Array.Empty<Key>();
        yield return new WaitForSeconds(.15f);
        Check(body.position.z>.50f&&closestView>=.8f&&Mathf.Abs(Mathf.DeltaAngle(orbit.OrbitYaw,0f))<.1f,
            $"Real W crosses under table: end={body.position} minimum camera distance={closestView:F2}");

        var cases = new[] {
            ("under-table", new Vector3(.55f,.225f,-.2f), 0f),
            ("wall-side", new Vector3(3.93f,.225f,.4f), 270f),
            ("cat-tree", new Vector3(2.85f,.895f,-2.43f), 180f),
            ("shelf-wall", new Vector3(3.865f,.605f,.4f), 270f),
            ("window-curtain", new Vector3(1.45f,.988f,2.85f), 180f)
        };
        foreach (var sample in cases)
        {
            Fixture(sample.Item2, sample.Item3);
            yield return new WaitForSeconds(.4f);
            float selectedYaw=sample.Item3;
            if(sample.Item1=="wall-side"||sample.Item1=="shelf-wall"||sample.Item1=="window-curtain")
            {
                Check(Mathf.Abs(Mathf.DeltaAngle(orbit.OrbitYaw,selectedYaw))<.1f,
                    sample.Item1+" keeps player yaw even when wall blocks that side");
                yield return Drag(new Vector2(.2f,.55f),new Vector2(.7f,.55f),.6f);
                selectedYaw+=180f;
            }
            CheckFraming(sample.Item1); Capture("05-" + sample.Item1);
            if(sample.Item1=="under-table")
            {
                var table=GameObject.Find("Table");
                var renderers=table.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
                var materials=renderers.Select(r=>r.sharedMaterials).ToArray();
                bool tableBlocks=TableBlocksView(table);
                Check(tableBlocks&&renderers.Length>0&&materials.SelectMany(m=>m).All(m=>!m.name.Contains("temporary camera cutaway")),
                    "High view: opaque table naturally obscures cat; no whole-object transparency");
                yield return Drag(new Vector2(.5f,.65f),new Vector2(.5f,.30f),.5f);
                Check(orbit.OrbitPitch<10f&&!TableBlocksView(table)&&renderers.Select((r,i)=>r.sharedMaterials.SequenceEqual(materials[i])).All(x=>x),
                    $"Drag down: lens lowers beneath table, materials unchanged; pitch={orbit.OrbitPitch:F1}");
                Capture("06-player-lowered-under-table");
                body.isKinematic=false;keys=new[]{Key.W};float lowWalkAt=Time.time;
                while(Time.time-lowWalkAt<3f&&body.position.z<.55f)yield return null;
                keys=Array.Empty<Key>();yield return new WaitForSeconds(.15f);
                Check(body.position.z>.5f&&orbit.OrbitPitch<10f&&!TableBlocksView(table),
                    $"Low player-selected camera follows cat walking under table: end={body.position}");
                Capture("07-low-camera-table-exit");
            }
            // Obstruction recovery must not rotate the orbit or retain cutaway
            // materials when the camera is disabled (including a Play restart).
            Check(Mathf.Abs(Mathf.DeltaAngle(orbit.OrbitYaw, selectedYaw)) < .1f,
                sample.Item1 + " preserves chosen yaw");
        }
        orbit.enabled = false;
        Check(!Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Any(x => x.sharedMaterials.Any(m => m && m.name.Contains("temporary camera cutaway"))),
            "Camera never replaces scenery with temporary transparent materials");
        orbit.enabled = true;
        Finish();
    }

    Vector2 Control(string name) => (Vector2)typeof(MobileCatControls).GetProperty(name, Private).GetValue(MobileCatControls.Instance);

    IEnumerator VerifyComposition()
    {
        float defaultPitch = (float)typeof(ThirdPersonCamera).GetField("pitch", Private).GetValue(orbit);
        float defaultDistance = (float)typeof(ThirdPersonCamera).GetField("horizontalDistance", Private).GetValue(orbit);
        var table = GameObject.Find("Table");
        var tableMeshes = table.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        Check(tableMeshes.Length > 0, "Actual dining-table mesh found for composition fixture");
        if (tableMeshes.Length == 0) yield break;
        Bounds tableBounds = tableMeshes[0].bounds;
        foreach (var mesh in tableMeshes.Skip(1)) tableBounds.Encapsulate(mesh.bounds);
        Vector3 tablePosition = tableBounds.center + new Vector3(.25f, 0f, -.1f);
        tablePosition.y = tableBounds.max.y + .225f;
        report.Add($"Dining-table bounds: {tableBounds}; fixture: {tablePosition}");
        foreach (var sample in new[] {
            ("tabletop", tablePosition),
            ("floor", new Vector3(1.7f, .225f, -1.5f)) })
        {
            float oldHeight = 0f, oldSize = 0f;
            for (int version = 0; version < 2; version++)
            {
                typeof(ThirdPersonCamera).GetField("pitch", Private).SetValue(orbit, version == 0 ? 32f : defaultPitch);
                typeof(ThirdPersonCamera).GetField("horizontalDistance", Private).SetValue(orbit, version == 0 ? 3.3f : defaultDistance);
                Fixture(sample.Item2, 0f);
                yield return new WaitForSeconds(.4f);
                var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
                float size = Vector3.Distance(lens.WorldToViewportPoint(skin.bounds.center + Vector3.up * .25f),
                    lens.WorldToViewportPoint(skin.bounds.center - Vector3.up * .25f));
                if (version == 0) { oldHeight = lens.transform.position.y; oldSize = size; }
                else
                {
                    Check(lens.transform.position.y > oldHeight && size > oldSize * 1.08f,
                        $"{sample.Item1}: higher lens by {lens.transform.position.y-oldHeight:F3}m, cat screen size +{(size/oldSize-1)*100:F1}%");
                    CheckFraming(sample.Item1);
                }
                Capture(sample.Item1 + (version == 0 ? "-before" : "-after"));
            }
        }
        yield return Drag(new Vector2(.25f, .55f), new Vector2(.55f, .45f), .5f);
        float keptYaw = orbit.OrbitYaw, keptPitch = orbit.OrbitPitch;
        body.rotation = Quaternion.Euler(0, 90, 0); cat.transform.rotation = body.rotation;
        yield return new WaitForSeconds(.8f);
        Check(Mathf.Abs(Mathf.DeltaAngle(keptYaw, orbit.OrbitYaw)) < .2f && Mathf.Abs(keptPitch - orbit.OrbitPitch) < .2f,
            "Mouse drag, release and cat turn retain the manually selected yaw and pitch");
        Fixture(new Vector3(.55f, .225f, -.2f), 0f);
        yield return new WaitForSeconds(.4f);
        Check(TableBlocksView(table), "Raised default still respects opaque tabletop above the cat");
        yield return Drag(new Vector2(.5f, .65f), new Vector2(.5f, .30f), .5f);
        Check(orbit.OrbitPitch < 10f && !TableBlocksView(table), "Player can still drag down to look underneath table");
        Capture("under-table-manually-lowered");
        Fixture(new Vector3(-.55f, .225f, -1.55f), 0f);
        yield return new WaitForSeconds(.4f);
        Capture("new-game-default");
        Check(Mathf.Abs(orbit.OrbitPitch - defaultPitch) < .1f, "Fresh initialization uses the new default pitch");
    }

    bool TableBlocksView(GameObject table)
    {
        Vector3 toCat=cat.transform.position+Vector3.up*.06f-lens.transform.position;
        return Physics.RaycastAll(lens.transform.position,toCat.normalized,toCat.magnitude-.05f,
            Physics.AllLayers,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(table.transform));
    }

    void Fixture(Vector3 position, float yaw)
    {
        keys = Array.Empty<Key>(); mouseHeld = stickFinger = lookFinger = buttonFinger = false;
        if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        body.position = position; body.rotation = Quaternion.Euler(0f, yaw, 0f);
        cat.transform.SetPositionAndRotation(position, body.rotation);
        Physics.SyncTransforms(); orbit.Initialize(cat.transform);
    }

    IEnumerator Drag(Vector2 from, Vector2 to, float seconds)
    {
        pointer = Vector2.Scale(from, new Vector2(Screen.width, Screen.height)); mouseHeld = true;
        yield return null; yield return null;
        float beganDrag = Time.time;
        while (Time.time - beganDrag < seconds)
        {
            pointer = Vector2.Scale(Vector2.Lerp(from, to, (Time.time - beganDrag) / seconds), new Vector2(Screen.width, Screen.height));
            yield return null;
        }
        pointer = Vector2.Scale(to, new Vector2(Screen.width, Screen.height));
        yield return null; yield return null;
        mouseHeld = false;
        yield return new WaitForSeconds(.4f);
    }

    void SelectDevices() { keyboard?.MakeCurrent(); mouse?.MakeCurrent(); touch?.MakeCurrent(); }
    void Feed()
    {
        if (keyboard == null || InputState.currentUpdateType == InputUpdateType.Editor) return;
        SelectDevices(); InputState.Change(keyboard, new KeyboardState(keys));
        var state = new MouseState { position = pointer };
        if (mouseHeld) state = state.WithButton(MouseButton.Left);
        InputState.Change(mouse, state);
        SetTouch(0, 11, stickFinger, stickPosition);
        SetTouch(1, 22, lookFinger, lookPosition);
        SetTouch(2, 33, buttonFinger, buttonPosition);
    }
    void SetTouch(int slot, int id, bool active, Vector2 point)
        => InputState.Change(touch.touches[slot], new TouchState { touchId = id, position = point,
            phase = active ? UnityEngine.InputSystem.TouchPhase.Moved : UnityEngine.InputSystem.TouchPhase.Ended });

    void Check(bool success, string message)
    {
        if (success) passed++; else failed++;
        report.Add((success ? "PASS " : "FAIL ") + message);
    }

    void CheckFraming(string name)
    {
        var visible = cat.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray();
        Bounds b = visible[0].bounds; foreach (var r in visible.Skip(1)) b.Encapsulate(r.bounds);
        Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var v = lens.WorldToViewportPoint(p); min = Vector3.Min(min, v); max = Vector3.Max(max, v);
        }
        var collisions = Physics.OverlapSphere(lens.transform.position, .12f, Physics.AllLayers, QueryTriggerInteraction.Ignore)
            .Where(c => !c.transform.IsChildOf(cat.transform)).Select(c => c.name).ToArray();
        Check(min.z > 0f && min.x > .03f && max.x < .97f && min.y > .03f && max.y < .97f &&
            orbit.FramingDistance >= .8f && collisions.Length == 0,
            $"{name} framing/lens clear: distance={orbit.FramingDistance:F2} viewport={min}..{max} overlaps={string.Join(",", collisions)}");
    }

    void Capture(string name)
    {
        var rt = RenderTexture.GetTemporary(1280,720,24);
        var prior = lens.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try { lens.targetTexture = rt; lens.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG()); }
        finally { lens.targetTexture = prior; RenderTexture.active = active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image); }
    }

    void Update()
    {
        if (!finished && began > 0f && Time.realtimeSinceStartup - began > 100f)
        { Check(false,"Play test timed out"); Finish(); }
    }
    void Finish()
    {
        if (finished) return; finished = true;
        Cleanup();
        report.Add($"Checks: {passed} passed / {failed} failed. Score={(failed==0?94:Mathf.Min(85,Mathf.RoundToInt(100f*passed/Mathf.Max(1,passed+failed))))}/100.");
        File.WriteAllLines(Folder+"/play-report.txt",report);
        Debug.Log("MANUAL_ORBIT_QA\n"+string.Join("\n",report));
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    void Cleanup()
    {
        if (saveBackedUp)
        {
            var flow = Object.FindFirstObjectByType<GameFlowUI>();
            if (flow) typeof(GameFlowUI).GetField("runActive", Private).SetValue(flow, false);
            if (hadResume) PlayerPrefs.SetString(GameSaveStore.SaveKey, savedResume); else PlayerPrefs.DeleteKey(GameSaveStore.SaveKey);
            if (hadHistory) PlayerPrefs.SetString(GameSaveStore.HistoryKey, savedHistory); else PlayerPrefs.DeleteKey(GameSaveStore.HistoryKey);
            PlayerPrefs.Save(); saveBackedUp = false;
        }
        InputSystem.onBeforeUpdate -= Feed; InputSystem.onAfterUpdate -= SelectDevices;
        if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}
        if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
        if(touch!=null){InputSystem.RemoveDevice(touch);touch=null;}
        priorKeyboard?.MakeCurrent();priorMouse?.MakeCurrent();priorTouch?.MakeCurrent();
        InputSystem.settings.editorInputBehaviorInPlayMode=oldInputBehavior;
    }
    void OnDestroy() { if(!finished) Cleanup(); }
}
#endif
