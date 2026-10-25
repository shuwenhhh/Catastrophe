using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Disposable play session; never saves the scene or changes the player's save.
[InitializeOnLoad]
public static class SmoothKitten3DPlayValidation
{
    const string KeyName = "SmoothKitten3D.PlayCheck";
    static double started;
    static int step;
    static float gameStarted;
    static Vector3 origin;
    static CatController cat;
    static Animator animator;
    static Keyboard keyboard;
    static SmoothKitten3DPlayValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(KeyName, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { started = EditorApplication.timeSinceStartup; step = 0; EditorApplication.update += Tick; }
        };
    }
    public static void Run()
    {
        SessionState.SetBool(KeyName, true);
        EditorSceneManager.OpenScene("Assets/LivingRoom.unity");
        EditorApplication.isPlaying = true;
    }
    static void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); Debug.Log("3D_PLAY_PASS " + message); }
    static void Tick()
    {
        try
        {
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed > 45) throw new TimeoutException("3D play check timed out");
            if (step == 0 && elapsed > 2)
            {
                UnityEngine.Object.FindAnyObjectByType<GameFlowUI>().BeginGame();
                cat = UnityEngine.Object.FindAnyObjectByType<CatController>();
                animator = cat.GetComponentInChildren<Animator>();
                Check(cat.name == "Mischief Cat" && animator != null && animator.transform.IsChildOf(cat.transform), "moving root owns skeleton Animator");
                Check(animator.GetComponentInChildren<SpriteRenderer>() == null && animator.GetComponentInChildren<SkinnedMeshRenderer>() != null, "actual 3D visual, no sprite cat (contact shadow excluded)");
                // Test in the open floor lane, away from spawn-adjacent furniture.
                cat.GetComponent<Rigidbody>().position = new Vector3(1.7f, .225f, -2.35f);
                Physics.SyncTransforms();
                origin = new Vector3(1.7f, .225f, -2.35f);
                keyboard = InputSystem.AddDevice<Keyboard>();
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.onBeforeUpdate += FeedInput;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                step = 1;
                gameStarted = Time.time;
                started = EditorApplication.timeSinceStartup;
            }
            else if (step == 1 && Time.time - gameStarted > 1.2f)
            {
                Check(cat.transform.position.x - origin.x > .15f, $"D moves cat right: {origin} -> {cat.transform.position}");
                Check(Vector3.Dot(cat.transform.forward, Vector3.right) > .8f, "root faces movement direction");
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "movement selects Walk");
                Transform head = Find(animator.transform,"Head"), rear = Find(animator.transform,"Root");
                Check(Vector3.Dot(head.position-rear.position,cat.transform.forward)>0, "model head points forward, not backward");
                Capture("Gameplay_Walk.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                step = 2; started = EditorApplication.timeSinceStartup;
                gameStarted = Time.time;
            }
            else if (step == 2 && Time.time - gameStarted > .7f)
            {
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "release returns to Idle");
                Capture("Gameplay_Idle.png");
                InputSystem.RemoveDevice(keyboard);
                SessionState.SetBool(KeyName,false);
                EditorApplication.update -= Tick;
                Debug.Log("SMOOTH_KITTEN_3D_PLAY_VALIDATION_PASSED");
                EditorApplication.Exit(0);
            }
        }
        catch(Exception e)
        { SessionState.SetBool(KeyName,false); EditorApplication.update -= Tick; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Transform Find(Transform t,string name)
    { if(t.name==name)return t; foreach(Transform c in t){var r=Find(c,name);if(r!=null)return r;}return null; }
    static void FeedInput()
    {
        if (keyboard != null && keyboard.added)
            InputSystem.QueueStateEvent(keyboard, step == 1 ? new KeyboardState(Key.D) : new KeyboardState());
    }
    static void Capture(string name)
    {
        var camera=Camera.main;var rt=new RenderTexture(960,720,24);
        var old=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(960,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();
        Directory.CreateDirectory("Validation/SmoothKitten3D");
        File.WriteAllBytes("Validation/SmoothKitten3D/"+name,image.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=active;
        UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);
    }
}
