#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CuteCatCapture
{
    [MenuItem("Catastrophe/Capture Cute Cat Gameplay Screenshot")]
    public static void Capture()
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/CuteCat"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "CuteCat_After.png");
        ScreenCapture.CaptureScreenshot(path, 2);
        Debug.Log($"CUTE_CAT_SCREENSHOT_REQUESTED path={path}");
    }
}
#endif
