#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public sealed class CoverMenuPlayValidation : MonoBehaviour
{
    const string Folder="Validation/CoverMenu";
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static CoverMenuPlayValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{
            if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CoverMenuTest",false))
                EditorApplication.delayCall+=()=>new GameObject("Disposable cover verification").AddComponent<CoverMenuPlayValidation>();
        };
        SceneManager.sceneLoaded+=(s,m)=>{
            if(Application.isPlaying&&SessionState.GetInt("CoverMenuPhase",0)==2&&SessionState.GetBool("CoverMenuTest",false))
                new GameObject("Disposable resume verification").AddComponent<CoverMenuPlayValidation>();
        };
    }
    [MenuItem("Catastrophe/Verify Cover Menu In Play")]
    static void Run()
    {
        if(Application.isPlaying) { Debug.LogWarning("Stop Play before cover test"); return; }
        foreach(var key in new[]{GameSaveStore.SaveKey,GameSaveStore.HistoryKey}) {
            SessionState.SetBool("CoverBackupExists"+key,PlayerPrefs.HasKey(key));
            SessionState.SetString("CoverBackup"+key,PlayerPrefs.GetString(key,""));
        }
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder+"/report.txt","");
        SessionState.SetInt("CoverMenuPhase",1); SessionState.SetBool("CoverMenuTest",true);
        EditorApplication.isPlaying=true;
    }
    void Check(bool ok,string message){File.AppendAllText(Folder+"/report.txt",(ok?"PASS ":"FAIL ")+message+"\n");}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(4);
        var flow=Object.FindAnyObjectByType<GameFlowUI>();
        if(!flow){Check(false,"GameFlowUI created");Cleanup();yield break;}
        if(SessionState.GetInt("CoverMenuPhase",0)==1)
        {
            GameSaveStore.ClearResume();PlayerPrefs.DeleteKey(GameSaveStore.HistoryKey);
            typeof(GameFlowUI).GetMethod("RefreshMenuData",Private).Invoke(flow,null);
            Check(flow.State==GameFlowUI.ScreenState.MainMenu&&Time.timeScale==0,"Title screen pauses gameplay");
            Check(Resources.Load<Texture2D>("Menu/CatastropheMainMenu")!=null,"Selected cover loads from project resource");
            Check(GameSaveStore.Read()==null,"No save: Resume disabled");
            yield return Capture("01-title-new-player");
            typeof(GameFlowUI).GetMethod("StartNewFromCover",Private).Invoke(flow,null);
            yield return new WaitForSecondsRealtime(.4f);
            Check(GameFlowUI.GameplayInputEnabled,"New Game starts actual gameplay");
            GameManager.I.AddChaos(17,"Test history entry");
            TargetManager.I.CompleteTarget("ipad");
            NoiseManager.I.noise=23;
            GameSaveStore.Capture();
            var save=GameSaveStore.Read();
            SessionState.SetString("CoverExpectedPosition",JsonUtility.ToJson(save));
            Check(save!=null&&save.score==17&&save.target==1&&save.props.Count>0,"Resume records score, objective and prop state");
            SessionState.SetInt("CoverMenuPhase",2);
            flow.ReturnToMainMenu();
            yield break;
        }
        Check(flow.State==GameFlowUI.ScreenState.MainMenu,"Scene reload returns to title");
        yield return Capture("02-title-with-resume");
        Check(GameSaveStore.Restore(),"Saved gameplay survives a full scene reload");
        Check(GameManager.I.chaosScore==17&&TargetManager.I.SavedTargetIndex==1&&Mathf.Abs(NoiseManager.I.noise-23)<.01f,"Resume restores score, objective and noise");
        var cat=Object.FindAnyObjectByType<CatController>();
        var saved=GameSaveStore.Read();
        Check(Vector3.Distance(cat.transform.position,saved.position)<.01f,"Resume restores cat position");
        foreach(var prop in Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None))
        {
            var state=saved.props.Find(p=>p.key==GameSaveStore.ObjectKey(prop.transform));
            if(state!=null)Check(Vector3.Distance(prop.transform.position,state.position)<.001f&&prop.CaptureSave().destroyed==state.destroyed,"Restored prop state: "+prop.itemName);
        }
        flow.ResumeGame();
        yield return new WaitForSecondsRealtime(.5f);
        Check(GameManager.I.chaosScore==17,"Resuming simulation does not duplicate points");
        typeof(GameFlowUI).GetMethod("ShowMainMenu",Private).Invoke(flow,null);
        GameSaveStore.Archive("测试记录",17,42);
        typeof(GameFlowUI).GetMethod("RefreshMenuData",Private).Invoke(flow,null);
        typeof(GameFlowUI).GetField("showingRecords",Private).SetValue(flow,true);
        yield return Capture("03-records");
        Check(GameSaveStore.ReadHistory().records.Count==1,"History persists actual run entries");
        typeof(GameFlowUI).GetField("showingRecords",Private).SetValue(flow,false);
        // Malformed data must disable resume rather than break the menu.
        PlayerPrefs.SetString(GameSaveStore.SaveKey,"invalid JSON");
        Check(GameSaveStore.Read()==null,"Malformed save is handled safely");
        File.AppendAllText(Folder+"/report.txt","Functional checks complete; visual review required.\n");
        Cleanup();
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
    }
    static void Cleanup()
    {
        var flow=Object.FindAnyObjectByType<GameFlowUI>();
        if(flow)typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);
        foreach(var key in new[]{GameSaveStore.SaveKey,GameSaveStore.HistoryKey}) {
            if(SessionState.GetBool("CoverBackupExists"+key,false))PlayerPrefs.SetString(key,SessionState.GetString("CoverBackup"+key,""));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save(); SessionState.SetBool("CoverMenuTest",false);SessionState.SetInt("CoverMenuPhase",0);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
}
#endif
