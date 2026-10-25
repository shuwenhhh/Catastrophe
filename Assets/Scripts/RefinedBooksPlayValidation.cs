#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Disposable actual-scene check; no authored scene writes and no user-save loss.
[InitializeOnLoad]
public sealed class RefinedBooksPlayValidation : MonoBehaviour
{
    const string Active="RefinedBooksValidation", Folder="Validation/RefinedShelfBooks";
    static readonly string[] Keys={GameSaveStore.SaveKey,GameSaveStore.HistoryKey,"Catastrophe.HighScore"};
    static readonly string[] Names={"Tripo Book Stack","Shelf Books 02","Shelf Books 03"};
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    int passed,failed,falls;
    bool finished;
    Valuable tested;
    GameFlowUI flow;

    static RefinedBooksPlayValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{
            if(!SessionState.GetBool(Active,false))return;
            if(s==PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall+=()=>new GameObject("Disposable refined books test").AddComponent<RefinedBooksPlayValidation>();
            if(s==PlayModeStateChange.EnteredEditMode)Restore();
        };
    }
    [MenuItem("Catastrophe/Books/3 Verify Books In Play")]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(string k in Keys)
        {
            SessionState.SetBool(Active+k+"Exists",PlayerPrefs.HasKey(k));
            SessionState.SetString(Active+k,k=="Catastrophe.HighScore"?PlayerPrefs.GetInt(k,0).ToString():PlayerPrefs.GetString(k,""));
        }
        SessionState.SetBool(Active+"Background",Application.runInBackground);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder+"/play-report.txt","Refined shelf books: actual LivingRoom Play Mode.\n");
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;
        StartCoroutine(Watchdog());
        yield return new WaitForSecondsRealtime(1.5f);
        flow=Object.FindAnyObjectByType<GameFlowUI>();
        var cat=Object.FindAnyObjectByType<CatController>();
        Check(flow&&cat&&GameManager.I,"Actual scene and cat are loaded");
        if(!flow||!cat||!GameManager.I){Finish();yield break;}
        flow.BeginGame(); typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);
        var books=Names.Select(GameObject.Find).ToArray();
        Check(books.All(g=>g),"All three existing shelf stacks retain their original object names");
        if(books.Any(g=>!g)){Finish();yield break;}
        var board=GameObject.Find("Tripo Shelving Unit").GetComponentsInChildren<BoxCollider>()
            .Where(c=>c.name.StartsWith("Climbable Shelf Platform")).OrderBy(c=>c.bounds.max.y).First();
        foreach(var g in books)
        {
            var meshes=g.GetComponentsInChildren<MeshFilter>();
            var renders=g.GetComponentsInChildren<MeshRenderer>();
            var c=g.GetComponent<BoxCollider>(); var v=g.GetComponent<Valuable>();
            Check(meshes.Length==1&&AssetDatabase.GetAssetPath(meshes[0].sharedMesh)=="Assets/Art/LivingRoom/RefinedShelfBooks/FittedHardbacks.asset"&&meshes[0].sharedMesh.vertexCount<50000,
                g.name+": only the replacement mesh is present, below 50k vertices");
            Check(renders.Length==1&&renders[0].sharedMaterials.Length==9&&renders[0].sharedMaterials.All(m=>m&&m.shader.isSupported),
                g.name+": nine authored cover/paper/foil materials render correctly");
            Check(c&&g.GetComponentsInChildren<Collider>().Length==1&&!c.isTrigger&&g.GetComponent<Rigidbody>(),
                g.name+": original single solid collider and Rigidbody retained");
            var b=renders[0].bounds;
            Check(Vector3.Distance(b.center,c.bounds.center)<.001f&&Vector3.Distance(b.size,c.bounds.size)<.001f,
                g.name+": replacement fits original collision bounds");
            Check(Mathf.Abs(b.min.y-board.bounds.max.y)<.008f&&b.min.x>=board.bounds.min.x&&b.max.x<=board.bounds.max.x,
                g.name+": still supported by the shelf, no floating or wall intrusion");
            Check(v&&v.chaosValue==15&&Mathf.Abs(v.noise-12)<.01f&&Mathf.Abs(v.weight-1.2f)<.01f,
                g.name+": original price, noise and weight preserved");
        }
        var anim=cat.GetComponentInChildren<Animator>();
        Check(anim&&cat.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0&&!anim.applyRootMotion,
            "Original skinned cat and physics-driven Animator retained");
        tested=books[0].GetComponent<Valuable>();
        var collision=cat.GetComponent<CatSolidCollision>();
        var body=cat.GetComponent<Rigidbody>();
        Quaternion facing=Quaternion.LookRotation(Vector3.left);
        var bounds=tested.GetComponent<BoxCollider>().bounds;
        Vector3 pose=new Vector3(bounds.max.x+.30f,board.bounds.max.y+.225f,bounds.center.z);
        body.position=pose;body.rotation=facing;body.linearVelocity=Vector3.zero;
        cat.transform.SetPositionAndRotation(pose,facing);Physics.SyncTransforms();
        yield return new WaitForSeconds(.3f);
        Check(collision.CanOccupy(body.position,body.rotation),"Cat can stand in the wall-side aisle behind the new books");
        Check(!collision.CanOccupy(new Vector3(bounds.center.x,pose.y,bounds.center.z),facing),
            "Cat body cannot pass through the book stack");
        var orbit=Camera.main.GetComponent<ThirdPersonCamera>();
        orbit.Initialize(cat.transform); orbit.RestoreView(270f,32f);
        yield return new WaitForSeconds(.4f);
        yield return Screen("play-cat-and-books");
        CaptureDetail(books[0],"play-detail");
        Valuable.AnyDestroyed+=OnFall;
        int score=GameManager.I.chaosScore;
        float startX=tested.transform.position.x;
        for(int push=0;push<6&&!tested.CaptureSave().destroyed;push++)
        {
            bounds=tested.GetComponent<BoxCollider>().bounds;
            pose=new Vector3(bounds.max.x+.30f,board.bounds.max.y+.225f,bounds.center.z);
            body.position=pose;body.rotation=facing;body.linearVelocity=Vector3.zero;
            cat.transform.SetPositionAndRotation(pose,facing); Physics.SyncTransforms();
            Check(cat.TryPushNearby(),"Actual cat paw push accepted, press "+(push+1));
            yield return new WaitForSeconds(.85f);
            if(push==0) Check(tested.transform.position.x<startX-.05f&&GameManager.I.chaosScore==score,
                "First push slides books normally without awarding premature money");
        }
        Check(tested.CaptureSave().destroyed&&falls==1&&GameManager.I.chaosScore==score+15,
            "Books fall off the real shelf and award original value exactly once");
        int after=GameManager.I.chaosScore;
        tested.MarkDestroyed();tested.MarkDestroyed();
        Check(GameManager.I.chaosScore==after&&falls==1,"Repeat fall callbacks cannot award duplicate money");
        Check(GameSaveStore.Capture()&&GameSaveStore.Restore(),"New visual model retains working save/restore identities");
        flow.TogglePause(); yield return new WaitForSecondsRealtime(.2f);
        Check(Time.timeScale==0f,"Pause still works after book interaction");
        flow.TogglePause(); Check(GameFlowUI.GameplayInputEnabled&&Time.timeScale==1,"Resume still works");
        Finish();
    }
    void OnFall(Valuable value){if(value==tested)falls++;}
    IEnumerator Watchdog(){yield return new WaitForSecondsRealtime(75);if(!finished){Check(false,"Test timed out");Finish();}}
    static IEnumerator Screen(string name)
    {
        yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
    }
    static void CaptureDetail(GameObject g,string name)
    {
        var b=g.GetComponentInChildren<Renderer>().bounds;
        var go=new GameObject("Disposable book detail camera");var c=go.AddComponent<Camera>();
        c.transform.position=b.center+new Vector3(-.65f,.42f,-.48f);c.transform.LookAt(b.center);
        c.fieldOfView=36;c.nearClipPlane=.03f;
        var rt=RenderTexture.GetTemporary(1600,1100,24);var old=RenderTexture.active;
        var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=old;c.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);Object.Destroy(go);}
    }
    void Check(bool ok,string note){if(ok)passed++;else failed++;File.AppendAllText(Folder+"/play-report.txt",(ok?"PASS ":"FAIL ")+note+"\n");}
    void Finish()
    {
        if(finished)return;finished=true;Valuable.AnyDestroyed-=OnFall;
        if(flow)typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);
        File.AppendAllText(Folder+"/play-report.txt",$"Result: {passed} passed, {failed} failed.\n");
        Restore();EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    static void Restore()
    {
        if(!SessionState.GetBool(Active,false))return;
        foreach(string k in Keys)
        {
            if(SessionState.GetBool(Active+k+"Exists",false))
            {string s=SessionState.GetString(Active+k,"");if(k=="Catastrophe.HighScore")PlayerPrefs.SetInt(k,int.Parse(s));else PlayerPrefs.SetString(k,s);}
            else PlayerPrefs.DeleteKey(k);
        }
        PlayerPrefs.Save();Application.runInBackground=SessionState.GetBool(Active+"Background",false);SessionState.SetBool(Active,false);
    }
}
#endif
