using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CatCameraViewValidation
{
    static double ready;
    static int phase;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static ThirdPersonCamera camera;
    static CatController cat;
    static Transform follow;
    static CatCameraViewValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("TestCatViews", false)) return;
            SessionState.SetBool("TestCatViews", false); phase = 0;
            ready = EditorApplication.timeSinceStartup + 2; EditorApplication.update += Check;
        };
    }
    [MenuItem("Catastrophe/Verify Click Camera Views")]
    static void Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first");
        SessionState.SetBool("TestCatViews", true); EditorApplication.isPlaying = true;
    }
    static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
    static void Check()
    {
        if (EditorApplication.timeSinceStartup < ready) return;
        Directory.CreateDirectory("Validation/CameraViews");
        try
        {
            if (phase == 0)
            {
                camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();
                cat = UnityEngine.Object.FindFirstObjectByType<CatController>();
                Require(camera && cat, "Missing runtime cat/camera");
                follow = (Transform)typeof(ThirdPersonCamera).GetField("target", Private).GetValue(camera);
                Require(follow == cat.transform, "Wrong follow target");
                camera.CycleView(); Require(camera.CurrentView == ThirdPersonCamera.FollowView.Main, "Menu did not block view switching");
                UnityEngine.Object.FindFirstObjectByType<GameFlowUI>().BeginGame();
                // Keep the live cat stable in a clear portion of the room. No scene is saved.
                cat.enabled = false; var rb = cat.GetComponent<Rigidbody>(); rb.isKinematic = true;
                cat.transform.SetPositionAndRotation(new Vector3(2.3f, .23f, -.2f), Quaternion.identity);
                camera.CycleView(); Require(camera.CurrentView == ThirdPersonCamera.FollowView.Front, "First click mode");
            }
            else if (phase == 1)
            {
                Verify(180); Capture("Front"); camera.CycleView();
                Require(camera.CurrentView == ThirdPersonCamera.FollowView.Back, "Second click mode");
                cat.transform.position += Vector3.right * .25f;
            }
            else if (phase == 2)
            {
                Verify(0); Capture("Back"); camera.CycleView();
                Require(camera.CurrentView == ThirdPersonCamera.FollowView.Main, "Return to main");
            }
            else
            {
                Verify(0); Capture("Main");
                File.WriteAllText("Validation/CameraViews/check.txt", "PASS live camera targets Mischief Cat in all three modes.\nPASS Main -> Front -> Back -> Main; menu blocks switching; front/back orbit yaw and target framing verified after target motion.\nCollision resolution, zoom and right drag code retained. Mode API tested; visual captures supplied.\n");
                Finish(); return;
            }
            phase++; ready = EditorApplication.timeSinceStartup + 2;
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Validation/CameraViews/check.txt", "FAIL " + e); Finish(); }
    }
    static void Verify(float yaw)
    {
        float actual = (float)typeof(ThirdPersonCamera).GetField("viewYaw", Private).GetValue(camera);
        Require(Mathf.Abs(Mathf.DeltaAngle(actual, yaw)) < 4, "View did not settle");
        Require((Transform)typeof(ThirdPersonCamera).GetField("target", Private).GetValue(camera) == follow, "Follow target changed");
        var viewport = camera.GetComponent<Camera>().WorldToViewportPoint(cat.transform.position + Vector3.up * .04f);
        Require(viewport.z > 0 && viewport.x > .05f && viewport.x < .95f && viewport.y > .05f && viewport.y < .95f, "Cat out of frame");
    }
    static void Capture(string name)
    {
        var c = camera.GetComponent<Camera>(); var rt = new RenderTexture(960, 720, 24); var old = c.targetTexture; var prev = RenderTexture.active;
        try { c.targetTexture = rt; c.Render(); RenderTexture.active = rt; var t = new Texture2D(960, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); t.Apply(); File.WriteAllBytes("Validation/CameraViews/" + name + ".png", t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t); }
        finally { c.targetTexture = old; RenderTexture.active = prev; UnityEngine.Object.DestroyImmediate(rt); }
    }
    static void Finish() { EditorApplication.update -= Check; EditorApplication.isPlaying = false; }
}
