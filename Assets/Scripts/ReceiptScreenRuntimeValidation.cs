#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ReceiptScreenRuntimeValidation : MonoBehaviour
{
    const string Folder="Validation/ReceiptScreen";
    static readonly string[] SaveKeys={GameSaveStore.SaveKey,GameSaveStore.HistoryKey,"Catastrophe.HighScore"};
    readonly bool[] existed=new bool[3];
    readonly string[] values=new string[3];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        string root=Path.GetDirectoryName(Application.dataPath)??".";
        string request=Path.Combine(root,Folder,"run.flag");
        if(!File.Exists(request))return;
        File.Delete(request);
        DontDestroyOnLoad(new GameObject("Disposable receipt screen runtime verification")
            .AddComponent<ReceiptScreenRuntimeValidation>().gameObject);
    }

    IEnumerator Start()
    {
        for(int i=0;i<SaveKeys.Length;i++)
        {
            string key=SaveKeys[i];
            existed[i]=PlayerPrefs.HasKey(key);
            values[i]=key.EndsWith("HighScore")?PlayerPrefs.GetInt(key,0).ToString():PlayerPrefs.GetString(key,"");
        }

        yield return new WaitForSecondsRealtime(1.2f);
        var flow=FindAnyObjectByType<GameFlowUI>();
        flow.BeginGame();
        GameManager.I.AddChaos(35,"Tablet");
        GameManager.I.AddChaos(50,"Laptop");
        GameManager.I.AddChaos(20,"Blue Mug");
        GameManager.I.AddChaos(35,"Pothos Plant");
        GameManager.I.AddChaos(15,"Book Stack");
        GameManager.I.AddChaos(20,"Porcelain Cup");
        GameManager.I.OnLevelComplete();
        yield return new WaitForEndOfFrame();

        string root=Path.GetDirectoryName(Application.dataPath)??".";
        string folder=Path.Combine(root,Folder);
        Directory.CreateDirectory(folder);
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(folder,"receipt.png"),image.EncodeToPNG());
        Destroy(image);
        File.WriteAllText(Path.Combine(folder,"report.txt"),
            flow.State==GameFlowUI.ScreenState.Results&&GameManager.I.Receipt.Count==6&&GameManager.I.DamageAmount==17500
                ? "PASS real completion-results state, six receipt rows, ¥17,500 total, world frozen. Visual review required.\n"
                : $"FAIL state={flow.State} rows={GameManager.I.Receipt.Count} amount={GameManager.I.DamageAmount}.\n");

        for(int i=0;i<SaveKeys.Length;i++)
        {
            string key=SaveKeys[i];
            if(!existed[i])PlayerPrefs.DeleteKey(key);
            else if(key.EndsWith("HighScore"))PlayerPrefs.SetInt(key,int.Parse(values[i]));
            else PlayerPrefs.SetString(key,values[i]);
        }
        PlayerPrefs.Save();
        yield return new WaitForSecondsRealtime(.3f);
        EditorApplication.isPlaying=false;
    }
}
#endif
