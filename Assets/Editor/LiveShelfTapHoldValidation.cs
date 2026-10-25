using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LiveShelfTapHoldValidation
{
    static CatController cat;
    static Rigidbody body;
    static Keyboard keyboard, previousKeyboard;
    static InputSettings settings;
    static InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
    static Key[] held = Array.Empty<Key>();
    static Bounds[] boards;
    static readonly List<string> report = new();
    static int trial, phase;
    static float ready, launched, apex;
    static float rearX;
    static bool tickLogged;

    static LiveShelfTapHoldValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("LiveShelfTapHold", false)) return;
            SessionState.SetBool("LiveShelfTapHold", false);
            Begin();
        };
    }

    static void Begin()
    {
        EditorApplication.update -= Tick;
        cat = null; trial = phase = 0; tickLogged = false; report.Clear(); ready = Time.time + .5f;
        EditorApplication.update += Tick;
        Debug.Log("LIVE_SHELF: test initialized");
    }

    [MenuItem("Catastrophe/Verify Live Shelf Tap and Hold")]
    static void Run()
    {
        if (Application.isPlaying) { Begin(); return; }
        SessionState.SetBool("LiveShelfTapHold", true);
        EditorApplication.isPlaying = true;
    }

    static void Feed()
    {
        if (keyboard == null || InputState.currentUpdateType == InputUpdateType.Editor) return;
        keyboard.MakeCurrent();
        InputState.Change(keyboard, new KeyboardState(held));
    }

    static void KeepCurrent() { keyboard?.MakeCurrent(); }

    static void Tick()
    {
        try
        {
            if (!tickLogged) { Debug.Log($"LIVE_SHELF: tick time={Time.time} cat={Object.FindAnyObjectByType<CatController>()}"); tickLogged = true; }
            // The title screen pauses game time; start gameplay before waiting
            // on Time.time, otherwise this validation can never advance.
            if (cat != null && Time.time < ready) return;
            if (cat == null)
            {
                cat = Object.FindFirstObjectByType<CatController>();
                if (cat == null) return;
                Object.FindFirstObjectByType<GameFlowUI>().BeginGame();
                body = cat.GetComponent<Rigidbody>();
                var shelf = GameObject.Find("Tripo Shelving Unit");
                boards = shelf.GetComponentsInChildren<BoxCollider>(true)
                    .Where(c => c.enabled && c.name.StartsWith("Climbable Shelf Platform"))
                    .OrderBy(c => c.bounds.max.y).Select(c => c.bounds).ToArray();
                if (boards.Length < 2) throw new Exception("Two shelf colliders were not found");
                Camera.main.GetComponent<ThirdPersonCamera>().enabled = false;
                Camera.main.transform.rotation = Quaternion.LookRotation(Vector3.right);
                settings = InputSystem.settings;
                previousBehavior = settings.editorInputBehaviorInPlayMode;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                previousKeyboard = Keyboard.current;
                keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                InputSystem.onBeforeUpdate += Feed;
                InputSystem.onAfterUpdate += KeepCurrent;
                ready = Time.time + .2f;
                return;
            }
            if (phase == 0)
            {
                // Approach the room-facing edge, outside the solid shelf boards.
                float startY = trial == 2 ? boards[0].max.y : 0f;
                float startX = trial == 2 ? boards[0].min.x + .15f : boards[0].min.x - .40f;
                body.position = new Vector3(startX, startY + .225f, boards[0].center.z);
                body.rotation = Quaternion.LookRotation(Vector3.right);
                cat.transform.SetPositionAndRotation(body.position, body.rotation);
                body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                held = Array.Empty<Key>();
                phase = 1; ready = Time.time + .25f; return;
            }
            if (phase == 1)
            {
                held = new[] { Key.W, Key.Space };
                launched = Time.time; apex = 0f; phase = 2; return;
            }
            if(phase==4)
            {
                if(Time.time<ready)return;
                rearX=body.position.x;
                var b=boards[0];float footY=body.position.y-.225f;
                report.Add($"FIRST SHELF REAR LANE: x={rearX:F3} target>={b.max.x-.50f:F3} foot={footY:F3} result={(rearX>=b.max.x-.50f&&Mathf.Abs(footY-b.max.y)<.09f?"PASS":"FAIL")}");
                held=new[]{Key.S};ready=Time.time+1.2f;phase=5;return;
            }
            if(phase==5)
            {
                if(Time.time<ready)return;
                float footY=body.position.y-.225f;
                report.Add($"FIRST SHELF RETREAT: rear={rearX:F3} x={body.position.x:F3} foot={footY:F3} yaw={body.rotation.eulerAngles.y:F1} result={(body.position.x<rearX-.30f&&Mathf.Abs(footY-boards[0].max.y)<.09f?"PASS":"FAIL")}");
                held=Array.Empty<Key>();trial++;phase=0;ready=Time.time+.15f;return;
            }
            float foot = body.position.y - .225f;
            apex = Mathf.Max(apex, foot);
            // A tap is about three 60-fps frames. The held jump lasts through ascent.
            if (phase == 2 && Time.time - launched >= (trial == 0 ? .05f : .58f))
            {
                held = new[] { Key.W };
                phase = 3;
            }
            // Release forward once the support capsule reaches the landing
            // strip; holding W for three seconds simply walks off the back.
            if(body.position.x>=boards[trial==0?0:1].min.x+.14f)
                held=phase==2?new[]{Key.Space}:Array.Empty<Key>();
            if (Time.time - launched < 3.0f) return;
            held = Array.Empty<Key>();
            float expected = boards[trial == 0 ? 0 : 1].max.y;
            bool passed = Mathf.Abs(foot - expected) < .09f &&
                          body.position.x > boards[trial == 0 ? 0 : 1].min.x + .10f;
            string nearby = string.Join(",", Physics.OverlapSphere(body.position, .45f)
                .Where(c => !c.transform.IsChildOf(cat.transform)).Select(c => c.name).Distinct().Take(8));
            report.Add($"{(trial == 0 ? "GROUND TAP" : trial == 1 ? "GROUND HOLD" : "FIRST SHELF HOLD")}: target={expected:F3}m apex={apex:F3}m landed={foot:F3}m x={body.position.x:F3} shelfStart={boards[trial == 0 ? 0 : 1].min.x:F3} verticalSpeed={body.linearVelocity.y:F3} nearby={nearby} result={(passed ? "PASS" : "FAIL")}");
            if(trial==0&&passed){held=new[]{Key.W};phase=4;ready=Time.time+.8f;return;}
            trial++;
            if (trial == 3) Finish();
            else { phase = 0; ready = Time.time + .15f; }
        }
        catch (Exception error)
        {
            report.Add("ERROR: " + error);
            Finish();
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        InputSystem.onBeforeUpdate -= Feed;
        InputSystem.onAfterUpdate -= KeepCurrent;
        held = Array.Empty<Key>();
        if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        previousKeyboard?.MakeCurrent();
        if (settings != null) settings.editorInputBehaviorInPlayMode = previousBehavior;
        Directory.CreateDirectory("Validation/ReferenceShelf");
        File.WriteAllLines("Validation/ReferenceShelf/live-tap-hold.txt", report);
        foreach (string line in report) Debug.Log("LIVE_SHELF " + line);
        EditorApplication.isPlaying = false;
    }
}
