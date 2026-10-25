using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PreviewCameraSetup
{
    [MenuItem("Catastrophe/Camera/Show Room In Edit Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/LivingRoom.unity") return;
        var camera = Camera.main;
        if (!camera) return;
        Undo.RecordObject(camera.transform, "Show room in Game preview");
        camera.transform.position = new Vector3(-.55f, 2.8f, -2.8f);
        camera.transform.LookAt(new Vector3(0f, .45f, -.25f));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
