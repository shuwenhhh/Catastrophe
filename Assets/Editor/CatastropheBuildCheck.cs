#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatastropheBuildCheck
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/LivingRoom.unity");
            GameBootstrap bootstrap = new GameObject("Catastrophe Validation").AddComponent<GameBootstrap>();
            MethodInfo awake = typeof(GameBootstrap).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(bootstrap, null);

            if (GameBootstrap.Instance == null)
                throw new InvalidOperationException("Game bootstrap was not created.");
            if (GameObject.Find("Mischief Cat") == null)
                throw new InvalidOperationException("The cat was not created.");
            if (Camera.main == null)
                throw new InvalidOperationException("The main camera was not created.");
            if (GameObject.Find("Coffee Mug") == null || GameObject.Find("Pink Vase") == null)
                throw new InvalidOperationException("The breakable room items were not created.");

            Debug.Log("CATASTROPHE VALIDATION PASSED: room, cat, camera, and score items generated successfully.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
#endif
