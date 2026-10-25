using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WindowTableRearLandingValidation
{
    const string SessionKey = "ValidateWindowTableRearLanding";
    static CatController cat;
    static Rigidbody body;
    static float started;

    static WindowTableRearLandingValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, false);
            cat = null;
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Catastrophe/Window Nook/Verify Rear Landing In Play")]
    static void Run()
    {
        if (Application.isPlaying) throw new Exception("Stop Play first");
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        try
        {
            if (!cat)
            {
                cat = Object.FindAnyObjectByType<CatController>();
                if (!cat) return;
                Object.FindAnyObjectByType<GameFlowUI>().BeginGame();
                body = cat.GetComponent<Rigidbody>();
                var top = GameObject.Find("Console oak top")?.GetComponent<Collider>();
                var wall = GameObject.Find("V2 Back Wall lower")?.GetComponent<Collider>();
                if (!top || !wall) throw new Exception("Window table or wall collider missing");
                if (top.bounds.max.z < wall.bounds.min.z - .025f)
                    throw new Exception("Table still has a fall-through gap to the wall: top=" + top.bounds + " wall=" + wall.bounds);
                if (Mathf.Abs(top.bounds.min.z - 2.345f) > .025f)
                    throw new Exception("Front jump edge unexpectedly moved");
                var foot = new Vector3(1.65f, top.bounds.max.y, 3.05f);
                body.position = foot + Vector3.up * .225f;
                body.rotation = Quaternion.Euler(0f, 180f, 0f);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                cat.transform.SetPositionAndRotation(body.position, body.rotation);
                Physics.SyncTransforms();
                started = Time.time;
                return;
            }
            if (Time.time - started < 1.25f) return;
            var landed = body.position - Vector3.up * .225f;
            var surface = GameObject.Find("Console oak top").GetComponent<Collider>().bounds.max.y;
            if (Mathf.Abs(landed.y - surface) > .06f || Mathf.Abs(landed.z - 3.05f) > .12f)
                throw new Exception("Cat did not remain on the rear tabletop: foot=" + landed);
            Finish("PASS cat stayed on the rear tabletop by the window for 1.25 s; front jump edge and wall closure verified. foot=" + landed);
        }
        catch (Exception error) { Finish("FAIL " + error); }
    }

    static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        Directory.CreateDirectory("Validation/WindowTableRearLanding");
        File.WriteAllText("Validation/WindowTableRearLanding/result.txt", result + "\n");
        Debug.Log("WINDOW_TABLE_REAR_LANDING " + result);
        EditorApplication.isPlaying = false;
    }
}
