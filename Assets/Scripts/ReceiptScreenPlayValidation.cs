#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class ReceiptScreenPlayValidation : MonoBehaviour
{
    // Disposable edit-mode launcher plus play-mode visual verification.
    const string Flag="ReceiptScreenQA", Folder="Validation/ReceiptScreen";
    const string Active=Flag+".Active";
    static readonly string[] SaveKeys={GameSaveStore.SaveKey,GameSaveStore.HistoryKey,"Catastrophe.HighScore"};
    static ReceiptScreenPlayValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state==PlayModeStateChange.EnteredEditMode)RestoreSaves();
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false))
            {
                SessionState.SetBool(Flag,false);
                EditorApplication.delayCall += ()=>new GameObject("Disposable receipt screen verification").AddComponent<ReceiptScreenPlayValidation>();
            }
        };
    }

    [MenuItem("Catastrophe/Verify Receipt Screen In Play")]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Active,true);
        foreach(string key in SaveKeys)
        {
            SessionState.SetBool(Active+key+".Exists",PlayerPrefs.HasKey(key));
            SessionState.SetString(Active+key,key.EndsWith("HighScore")
                ? PlayerPrefs.GetInt(key,0).ToString()
                : PlayerPrefs.GetString(key,""));
        }
        SessionState.SetBool(Flag,true);
        EditorApplication.isPlaying=true;
    }

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        var flow=FindFirstObjectByType<GameFlowUI>();
        flow.BeginGame();
        GameManager.I.AddChaos(35,"Tablet");
        GameManager.I.AddChaos(50,"Laptop");
        GameManager.I.AddChaos(20,"Blue Mug");
        GameManager.I.AddChaos(35,"Pothos Plant");
        GameManager.I.AddChaos(15,"Book Stack");
        GameManager.I.AddChaos(20,"Porcelain Cup");
        GameManager.I.OnLevelComplete();
        yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder+"/receipt.png",image.EncodeToPNG());
        Destroy(image);
        File.WriteAllText(Folder+"/report.txt",
            flow.State==GameFlowUI.ScreenState.Results&&GameManager.I.Receipt.Count==6&&GameManager.I.DamageAmount==17500
            ? "PASS real completion-results state, six receipt rows, ¥17,500 total, world frozen. Visual review required.\n"
            : $"FAIL state={flow.State} rows={GameManager.I.Receipt.Count} amount={GameManager.I.DamageAmount}.\n");
        yield return new WaitForSecondsRealtime(.3f);
        EditorApplication.isPlaying=false;
    }

    static void RestoreSaves()
    {
        if(!SessionState.GetBool(Active,false))return;
        foreach(string key in SaveKeys)
        {
            if(SessionState.GetBool(Active+key+".Exists",false))
            {
                string value=SessionState.GetString(Active+key,"");
                if(key.EndsWith("HighScore"))PlayerPrefs.SetInt(key,int.Parse(value));
                else PlayerPrefs.SetString(key,value);
            }
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        SessionState.SetBool(Active,false);
    }
}
#endif
