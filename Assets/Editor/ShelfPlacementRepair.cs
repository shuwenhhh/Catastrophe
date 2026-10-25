using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ShelfPlacementRepair
{
    [MenuItem("Catastrophe/Props/Place Sorted Collectibles On Tree Side Shelf")]
    public static void Apply()
    {
        if(Application.isPlaying)throw new Exception("Exit Play before arranging the shelf");
        var shelf=GameObject.Find("Tripo Shelving Unit");var display=GameObject.Find("Shelf Value Display");
        if(!shelf||!display)throw new Exception("Shelf or collection missing");
        var frame=shelf.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Uniform Shelf Frame");
        var wall=GameObject.Find("V2 Right Wall")?.GetComponent<Collider>();
        if(!wall)throw new Exception("Right wall collision is missing");
        // The rear edge nearly meets the solid wall; the front edge remains
        // reachable from the room. Keep one identical footprint on every tier.
        float front=3.04f, back=wall.bounds.min.x-.045f;
        if(back-front<1.05f)throw new Exception("Shelf does not have enough depth for cat and props");
        var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/LivingRoom/ReferenceShelf/UniformShelfFrame.asset");
        if(!source)throw new Exception("Original shelf frame mesh is missing");
        var boards=shelf.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name.StartsWith("Climbable Shelf Platform")).OrderBy(c=>c.bounds.max.y).ToArray();
        if(boards.Length!=4)throw new Exception("Expected four shelves");
        Undo.RegisterFullObjectHierarchyUndo(shelf,"Restore shelf beside cat tree");Undo.RegisterFullObjectHierarchyUndo(display,"Arrange collectibles by value");
        FitFrameToWall(frame,source,front,back);
        var footprint=ActualBounds(frame);
        foreach(var r in shelf.GetComponentsInChildren<Renderer>(true))r.enabled=r.gameObject==frame.gameObject;
        var report=new List<string>{"Game values, descending from top to bottom, left to right when facing shelf."};
        int boardIndex=0;
        foreach(var board in boards)
        {
            float y=.36f+.67f*boardIndex++;board.transform.position=new Vector3((front+back)*.5f,y,footprint.center.z);
            var s=board.transform.lossyScale;board.center=Vector3.zero;
            board.size=new Vector3((back-front)/Mathf.Abs(s.x),.04f/Mathf.Abs(s.y),(footprint.size.z-.14f)/Mathf.Abs(s.z));
            var visual=board.transform.Find("Matching Oak Board");visual.localPosition=Vector3.zero;visual.localRotation=Quaternion.identity;visual.localScale=board.size;visual.GetComponent<Renderer>().enabled=true;
        }
        var posts=shelf.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name.StartsWith("Shelf frame collision")).OrderBy(c=>c.name).ToArray();
        for(int i=0;i<posts.Length;i++)
        {
            var c=posts[i];var s=c.transform.lossyScale;
            c.transform.position=new Vector3(i%2==0?front+.04f:back-.08f,footprint.center.y,footprint.center.z+(i<2?-1.66f:1.66f));
            c.center=Vector3.zero;c.size=new Vector3(.08f/Mathf.Abs(s.x),footprint.size.y/Mathf.Abs(s.y),.035f/Mathf.Abs(s.z));
        }
        Physics.SyncTransforms();
        for(int tier=0;tier<3;tier++)
        {
            var items=display.transform.GetChild(tier).GetComponentsInChildren<ScoreItem>().OrderByDescending(c=>c.Points).ThenBy(c=>c.DisplayName).ToArray();
            Place(items,boards[tier+1].bounds,report);
        }
        var names=new[]{"Desktop Globe","Lucky Cat","Shelf Books 02","Shelf Mug 01","Tripo Book Stack","Shelf Books 03","Shelf Plant 02","Shelf Cup 02"};
        var old=names.Select(n=>GameObject.Find(n)).Where(g=>g).Select(g=>g.GetComponent<ScoreItem>()).Where(c=>c).OrderByDescending(c=>c.Points).ToArray();
        Place(old,boards[0].bounds,report);
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(shelf.scene);EditorSceneManager.SaveScene(shelf.scene);
        Directory.CreateDirectory("Validation/ValueShelf");File.WriteAllLines("Validation/ValueShelf/sorted-placement.txt",report);Capture();
    }
    static void Place(ScoreItem[] items,Bounds board,List<string> report)
    {
        int upper=(items.Length+1)/2;
        float upperStart=board.max.z-.24f,upperEnd=board.center.z+.60f;
        float lowerStart=board.center.z-.60f,lowerEnd=board.min.z+.24f;
        float spacing=items.Length>6?.26f:.44f;
        for(int i=0;i<items.Length;i++)
        {
            var g=items[i].gameObject;Undo.RegisterFullObjectHierarchyUndo(g,"Place shelf collectible");
            g.transform.rotation=Quaternion.Euler(0,270,0);
            var b=VisibleBounds(g);float fit=Mathf.Min(1,(spacing-.04f)/b.size.z,.28f/b.size.x);
            g.transform.localScale*=fit;b=VisibleBounds(g);
            // Props occupy the room-facing row. The cat can land and stand
            // behind them, against the wall, before nudging them outward.
            // An empty central aisle also lets the cat turn and leave the bay.
            float z=i<upper?Mathf.Lerp(upperStart,upperEnd,upper==1?0f:(float)i/(upper-1)):
                Mathf.Lerp(lowerStart,lowerEnd,items.Length-upper==1?0f:(float)(i-upper)/(items.Length-upper-1));
            g.transform.position+=new Vector3(board.min.x+.10f+b.extents.x,board.max.y+.002f+b.extents.y,z)-b.center;
            b=VisibleBounds(g);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            report.Add($"{items[i].DisplayName} value={items[i].Points} bottom={b.min.y:F3} shelf={board.max.y:F3} bounds={b} walkway={b.min.x-board.min.x:F3}");
        }
    }
    static Bounds VisibleBounds(GameObject g){var meshes=g.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>()?.enabled==true).ToArray();var b=ActualBounds(meshes[0]);foreach(var f in meshes.Skip(1))b.Encapsulate(ActualBounds(f));return b;}
    static void FitFrameToWall(MeshFilter frame,Mesh source,float front,float back)
    {
        // Reuse the original imported frame mesh. Scaling its depth avoids
        // duplicating a very large mesh asset merely to move the rear posts.
        frame.sharedMesh=source;
        var tr=frame.transform;
        var bounds=ActualBounds(frame);
        float ratio=(back-front-.07f)/bounds.size.x;
        var axes=new[]{tr.TransformVector(Vector3.right),tr.TransformVector(Vector3.up),tr.TransformVector(Vector3.forward)};
        int axis=0;
        for(int i=1;i<3;i++)if(Mathf.Abs(axes[i].x)>Mathf.Abs(axes[axis].x))axis=i;
        Vector3 scale=tr.localScale;scale[axis]*=ratio;tr.localScale=scale;
        bounds=ActualBounds(frame);
        tr.position+=Vector3.right*((front+back-.03f)*.5f-bounds.center.x);
        PrefabUtility.RecordPrefabInstancePropertyModifications(frame);
        PrefabUtility.RecordPrefabInstancePropertyModifications(tr);
    }
    [MenuItem("Catastrophe/Props/Capture Tree Side Shelf")]
    public static void Capture()
    {
        var shelf=GameObject.Find("Tripo Shelving Unit");var b=VisibleBounds(shelf);
        var g=new GameObject("Temporary shelf preview");var cam=g.AddComponent<Camera>();cam.transform.position=b.center+new Vector3(-4.8f,.8f,-1.3f);cam.transform.LookAt(b.center);cam.fieldOfView=47;
        var rt=RenderTexture.GetTemporary(1400,1000,24);var old=RenderTexture.active;var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();File.WriteAllBytes("Validation/ValueShelf/tree-side.png",tex.EncodeToPNG());}
        finally{RenderTexture.active=old;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(g);}
    }
    [InitializeOnLoadMethod] static void OnLoad(){EditorApplication.delayCall+=Inspect;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("ShelfPlacementCheck",false)){SessionState.SetBool("ShelfPlacementCheck",false);EditorApplication.delayCall+=ValidateRuntime;}};}
    [MenuItem("Catastrophe/Props/Test Sorted Shelf In Play")]
    public static void Test(){if(!Application.isPlaying){SessionState.SetBool("ShelfPlacementCheck",true);EditorApplication.isPlaying=true;}else ValidateRuntime();}
    static void ValidateRuntime()
    {
        var shelf=GameObject.Find("Tripo Shelving Unit");var display=GameObject.Find("Shelf Value Display");
        var boards=shelf.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Climbable Shelf Platform")).OrderBy(c=>c.bounds.max.y).ToArray();
        var report=new List<string>();int failures=0;
        for(int tier=0;tier<3;tier++)
        {
            var items=display.transform.GetChild(tier).GetComponentsInChildren<ScoreItem>().OrderByDescending(c=>VisibleBounds(c.gameObject).center.z).ToArray();int prior=int.MaxValue;
            foreach(var item in items){var b=VisibleBounds(item.gameObject);var board=boards[tier+1].bounds;bool pass=Mathf.Abs(b.min.y-board.max.y)<.01f&&b.min.x>=board.min.x&&b.max.x<=board.max.x&&b.min.z>=board.min.z&&b.max.z<=board.max.z&&item.Points<=prior;prior=item.Points;if(!pass)failures++;report.Add($"{item.DisplayName} value={item.Points} supported/sorted={(pass?"PASS":"FAIL")}");}
        }
        var cat=Object.FindAnyObjectByType<CatController>();var guard=cat.GetComponent<CatSolidCollision>();Physics.SyncTransforms();
        var wall=GameObject.Find("V2 Right Wall").GetComponent<Collider>().bounds;
        foreach(var board in boards)
        {
            var b=board.bounds;
            bool wallGap=wall.min.x-b.max.x>=0f&&wall.min.x-b.max.x<.08f;
            int blocked=0;
            for(int i=0;i<8;i++)
            {
                var p=new Vector3(b.max.x-.40f,b.max.y+.225f,Mathf.Lerp(b.min.z+.46f,b.max.z-.46f,i/7f));
                if(!guard.CanOccupy(p,Quaternion.LookRotation(Vector3.left)))blocked++;
            }
            report.Add($"{board.name}: wall gap={wall.min.x-b.max.x:F3}m {(wallGap?"PASS":"FAIL")}; behind-props cat poses={8-blocked}/8");
            failures+=blocked+(wallGap?0:1);
        }
        report.Add("Failures="+failures);File.WriteAllLines("Validation/ValueShelf/runtime.txt",report);Capture();EditorApplication.isPlaying=false;
    }
    [MenuItem("Catastrophe/Props/Inspect Actual Shelf Geometry")]
    public static void Inspect()
    {
        var shelf=GameObject.Find("Tripo Shelving Unit");if(!shelf)return;
        var lines=new List<string>();
        foreach(var f in shelf.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!f.sharedMesh)continue;
            var b=ActualBounds(f);
            lines.Add($"{f.name} active={f.gameObject.activeInHierarchy} enabled={f.GetComponent<Renderer>()?.enabled} actual={b} local={f.transform.localPosition} world={f.transform.position} rotation={f.transform.eulerAngles} scale={f.transform.lossyScale}");
        }
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.ToLower().Contains("tree")))lines.Add($"TREE {t.name} at {t.position}");
        Directory.CreateDirectory("Validation/ValueShelf");File.WriteAllLines("Validation/ValueShelf/actual.txt",lines);
    }
    static Bounds ActualBounds(MeshFilter f)
        =>ActualBounds(f.sharedMesh,f.transform);
    static Bounds ActualBounds(Mesh mesh,Transform tr)
    {
        using var data=MeshUtility.AcquireReadOnlyMeshData(mesh);
        using var verts=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);
        data[0].GetVertices(verts);var b=new Bounds(tr.TransformPoint(verts[0]),Vector3.zero);
        foreach(var v in verts)b.Encapsulate(tr.TransformPoint(v));return b;
    }
}
