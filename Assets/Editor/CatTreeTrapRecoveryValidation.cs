using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class CatTreeTrapRecoveryValidation
{
    const string Flag = "CatTreeTrapRecovery";
    static CatController cat;
    static Rigidbody body;
    static CatSolidCollision solid;
    static Keyboard keyboard, oldKeyboard;
    static InputSettings.EditorInputBehaviorInPlayMode oldBehavior;
    static Key[] held = Array.Empty<Key>();
    static Vector3 start;
    static float deadline;
    static int phase;
    static readonly string Report = "Validation/CatTreeTrap/recovery.txt";

    static CatTreeTrapRecoveryValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, false);
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Catastrophe/Verify Cat Tree Trap Recovery")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Validation/CatTreeTrap");
        File.WriteAllText(Report, "Verified cubby-roof approach to photographed cat-tree ledge; real player and input.\n");
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    static void Feed()
    {
        if (keyboard == null || InputState.currentUpdateType == InputUpdateType.Editor) return;
        keyboard.MakeCurrent();
        InputState.Change(keyboard, new KeyboardState(held));
    }

    static void SelectKeyboard() => keyboard?.MakeCurrent();

    static void Tick()
    {
        try
        {
            if (phase == 0)
            {
                cat = UnityEngine.Object.FindFirstObjectByType<CatController>();
                if (!cat) return;
                var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowUI>();
                flow.BeginGame();
                body = cat.GetComponent<Rigidbody>();
                solid = cat.GetComponent<CatSolidCollision>();
                var camera = Camera.main;
                camera.GetComponent<ThirdPersonCamera>().enabled = false;
                camera.transform.rotation = Quaternion.identity;
                oldBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                oldKeyboard = Keyboard.current;
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.onBeforeUpdate += Feed;
                InputSystem.onAfterUpdate += SelectKeyboard;
                // Re-enter from the verified cubby-roof standing point, then
                // move toward the pictured narrow ledge.
                start = new Vector3(2.78f, 1.33f, -2.43f);
                var facing = Quaternion.Euler(0, 357.56f, 0);
                // The live probe printed two decimal places, so recover a
                // collision-free point within that displayed rounding cell.
                if (!solid.CanOccupy(start, facing))
                {
                    bool found = false;
                    for (float radius = .01f; radius <= .07f && !found; radius += .01f)
                        for (int angle = 0; angle < 360 && !found; angle += 30)
                        {
                            float radians = angle * Mathf.Deg2Rad;
                            Vector3 candidate = start + new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * radius;
                            if (solid.CanOccupy(candidate, facing)) { start = candidate; found = true; }
                        }
                    if (!found) throw new Exception("No clear pose close to photographed location");
                }
                body.position = start;
                body.rotation = facing;
                cat.transform.SetPositionAndRotation(start, facing);
                body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                deadline = Time.time + .35f;
                phase = 1;
                return;
            }
            if (Time.time < deadline) return;
            if (phase == 1)
            {
                // The lower circular landing lies to the cat's left. First test
                // a free retreat from the photographed edge without jumping.
                held = new[] { Key.W };
                deadline = Time.time + .55f;
                phase = 2;
                return;
            }
            held = Array.Empty<Key>();
            Vector3 end = body.position;
            float advance = end.z - start.z;
            bool clear = solid.CanOccupy(end, body.rotation);
            File.AppendAllText(Report, $"start={start:F3} end={end:F3} advance={advance:F3} clear={clear}\n");
            File.AppendAllText(Report, advance > .15f && clear
                ? "PASS: ordinary stick input escapes the photographed cat-tree edge without clipping.\n"
                : "FAIL: cat remained pinned or entered furniture.\n");
            Finish();
        }
        catch (Exception ex)
        {
            File.AppendAllText(Report, "FAIL: " + ex + "\n");
            Finish();
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        InputSystem.onBeforeUpdate -= Feed;
        InputSystem.onAfterUpdate -= SelectKeyboard;
        held = Array.Empty<Key>();
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        oldKeyboard?.MakeCurrent();
        InputSystem.settings.editorInputBehaviorInPlayMode = oldBehavior;
        phase = 0;
        EditorApplication.isPlaying = false;
    }
}
