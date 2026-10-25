using System.IO;
using UnityEditor;

// Explicit file opt-in for running the same menu test in an already-open editor.
[InitializeOnLoad]
public static class NoiseSearchValidationRequest
{
    const string Request = "Validation/NoiseSearch/run.request";
    static NoiseSearchValidationRequest() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            File.Delete(Request); SessionState.SetBool("NoiseSearchQA.Pending", true);
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
        }
        if (!SessionState.GetBool("NoiseSearchQA.Pending", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("NoiseSearchQA.Pending", false);
        EditorApplication.ExecuteMenuItem("Catastrophe/Verify Noise Search In Play");
    }
}
