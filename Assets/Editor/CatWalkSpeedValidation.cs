using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CatWalkSpeedValidation
{
    static double ready;
    static CatWalkSpeedValidation()
    {
        EditorApplication.playModeStateChanged += s => {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CheckCatWalkSpeed", false)) return;
            SessionState.SetBool("CheckCatWalkSpeed", false);
            ready = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += Check;
        };
    }
    [MenuItem("Catastrophe/Verify Cat Walk Speed")]
    static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before validation.");
        SessionState.SetBool("CheckCatWalkSpeed", true);
        EditorApplication.isPlaying = true;
    }
    static void Check()
    {
        if (EditorApplication.timeSinceStartup < ready) return;
        EditorApplication.update -= Check;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(CatController);
        try
        {
            var cat = UnityEngine.Object.FindFirstObjectByType<CatController>();
            if (!cat) throw new Exception("No runtime cat");
            float speed = (float)type.GetField("moveSpeed", flags).GetValue(cat);
            float jump = (float)type.GetField("jumpMoveSpeed", flags).GetValue(cat);
            const float expectedWalk = .20f / .64f * .65f * 3.1f;
            if (Mathf.Abs(speed - expectedWalk) > .002f || Mathf.Abs(jump - 1.8f) > .0001f) throw new Exception($"Unexpected stride-derived runtime speed: walk={speed:F3}, jump={jump:F3}");
            // Disposable Play state only: exercise the real movement method in open space.
            var rb = cat.GetComponent<Rigidbody>(); rb.position = new Vector3(0, 5, 0);
            rb.rotation = Quaternion.LookRotation(Vector3.right);
            cat.transform.SetPositionAndRotation(rb.position, rb.rotation);
            Physics.SyncTransforms();
            var direction = type.GetField("moveDirection", flags);
            var tick = type.GetMethod("FixedUpdate", flags);
            direction.SetValue(cat, Vector3.right); tick.Invoke(cat, null);
            float moving = rb.linearVelocity.x;
            direction.SetValue(cat, Vector3.zero); tick.Invoke(cat, null);
            float stopped = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude;
            if (Mathf.Abs(moving - expectedWalk) > .002f || stopped > .001f) throw new Exception("Move/stop mismatch");
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/CatWalkSpeed.txt", $"PASS runtime {cat.name}: planted paw travel=.20 model metres over 64% stance, scale=.65, cadence=3.1 cycles/s; derived moveSpeed={speed:F3}, actual horizontal velocity={moving:F3}, stopped={stopped:F3}, jumpMoveSpeed={jump:F2} unchanged.\nTargeted movement smoke test; no scene saved.\n");
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Validation/CatWalkSpeed.txt", "FAIL " + e); }
        finally { EditorApplication.isPlaying = false; }
    }
}
