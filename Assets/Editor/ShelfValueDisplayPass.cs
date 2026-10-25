using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ShelfValueDisplayPass
{
    [InitializeOnLoadMethod]
    static void DispatchRequestedSetup()
    {
        EditorApplication.delayCall+=()=> {
            const string repair="Validation/ValueShelf/align.request";
            if(File.Exists(repair)&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Move(repair,repair+".started");try{AlignDisplay();}catch(Exception e){File.WriteAllText("Validation/ValueShelf/align-error.txt",e.ToString());}return;}
            const string request="Validation/ValueShelf/arrange.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Move(request,request+".started");
            try { Inspect();Apply(); } catch(Exception e){File.WriteAllText("Validation/ValueShelf/error.txt",e.ToString());Debug.LogException(e);}
        };
    }
    static void AlignDisplay()
    {
        var shelf=GameObject.Find("Tripo Shelving Unit");Undo.RegisterFullObjectHierarchyUndo(shelf,"Align shelf display geometry");
        var original=shelf.GetComponentsInChildren<Renderer>(true).First(r=>r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh.subMeshCount==2);
        original.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(original);
        foreach(var r in shelf.GetComponentsInChildren<Renderer>(true))if(r!=original){r.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
        var bounds=original.bounds;
        var boards=shelf.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Climbable Shelf Platform")).OrderBy(c=>c.bounds.max.y).ToArray();
        foreach(var c in boards)
        {
            var y=c.bounds.center.y;
            c.transform.SetParent(shelf.transform,true);c.transform.rotation=Quaternion.identity;
            c.transform.localScale=Vector3.one;
            // Convert desired world footprint into local collider dimensions.
            var s=c.transform.lossyScale;
            c.center=Vector3.zero;c.size=new Vector3((bounds.size.x-.14f)/Mathf.Abs(s.x),.04f/Mathf.Abs(s.y),(bounds.size.z-.10f)/Mathf.Abs(s.z));
            c.transform.position=new Vector3(bounds.center.x,y,bounds.center.z);
        }
        Physics.SyncTransforms();
        var root=GameObject.Find("Shelf Value Display");Undo.RegisterFullObjectHierarchyUndo(root,"Align individual collectibles");
        var report=new List<string>();
        for(int tier=0;tier<3;tier++)
        {
            var parent=root.transform.GetChild(tier);var board=boards[tier+1].bounds;float slot=board.size.x/parent.childCount;
            for(int j=0;j<parent.childCount;j++)
            {
                var g=parent.GetChild(j).gameObject;var v=g.transform.GetChild(0);v.localRotation=Quaternion.Euler(-90,0,0);v.localScale=Vector3.one;
                var b=BoundsOf(g);string id=g.GetComponent<ScoreItem>().DisplayName;
                float h=id.StartsWith("Book")?.32f:id=="VintageCamera"?.23f:id=="SilverMusicBox"?.25f:id=="PiggyBank"?.30f:.38f;
                v.localScale*=h/b.size.y;b=BoundsOf(g);v.localScale*=Mathf.Min(1,(slot-.08f)/b.size.x,.34f/b.size.z);b=BoundsOf(g);
                g.transform.position+=new Vector3(board.min.x+slot*(j+.5f),board.max.y+.002f+b.extents.y,board.max.z-.06f-b.extents.z)-b.center;b=BoundsOf(g);
                var c=g.GetComponent<BoxCollider>();c.center=g.transform.InverseTransformPoint(b.center);c.size=b.size;
                report.Add($"Tier {tier+1} {id} value={g.GetComponent<ScoreItem>().Points} bottomGap={b.min.y-board.max.y:F4} bounds={b}");
            }
        }
        var names=new[]{"Desktop Globe","Lucky Cat","Shelf Books 02","Shelf Mug 01","Tripo Book Stack","Shelf Books 03","Shelf Plant 02","Shelf Cup 02"};
        var old=names.Select(n=>GameObject.Find(n)).Where(g=>g).ToArray();
        for(int i=0;i<old.Length;i++)
        {
            var g=old[i];Undo.RecordObject(g.transform,"Keep original shelf collection");var b=BoundsOf(g);var board=boards[0].bounds;
            float slot=board.size.x/old.Length;
            g.transform.position+=new Vector3(board.min.x+slot*(i+.5f),board.max.y+.002f+b.extents.y,board.max.z-.04f-b.extents.z)-b.center;PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
        }
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(shelf.scene);EditorSceneManager.SaveScene(shelf.scene);
        File.WriteAllLines("Validation/ValueShelf/placement.txt",report);Capture();
    }
    const string Folder="Assets/Art/LivingRoom/ShelfValueDisplay";
    [Serializable] public class Manifest { public Model[] items; }
    [Serializable] public class Model { public string id,path; public Mat[] materials; }
    [Serializable] public class Mat { public string name,texture; public float[] color; public float metallic,smoothness; public bool glass; }
    static Bounds BoundsOf(GameObject g)
    {
        var rs=g.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    [MenuItem("Catastrophe/Props/Inspect Value Shelf")]
    public static void Inspect()
    {
        Directory.CreateDirectory("Validation/ValueShelf");
        File.WriteAllLines("Validation/ValueShelf/inspect.txt",Object.FindObjectsByType<ScoreItem>(FindObjectsSortMode.None).Select(g=>$"{g.name}: {BoundsOf(g.gameObject)} value={g.Points}"));
        UniformShelfPass.Inspect();
    }
    [MenuItem("Catastrophe/Props/Arrange Three Value Tiers")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first");
        var shelf=GameObject.Find("Tripo Shelving Unit");
        if(!shelf)throw new Exception("Shelf missing");
        var boards=shelf.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Climbable Shelf Platform")).OrderBy(c=>c.bounds.max.y).ToArray();
        if(boards.Length!=4)throw new Exception("Expected four shelf boards");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/manifest.json"));
        foreach(var m in manifest.items)if(!AssetDatabase.LoadAssetAtPath<GameObject>(m.path))throw new Exception("Missing "+m.path);
        Directory.CreateDirectory("Validation/ValueShelf");
        var scene=shelf.scene;
        EditorSceneManager.SaveScene(scene,"Validation/ValueShelf/BeforeValueDisplay.unity",true);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Arrange shelf by value");
        var prior=GameObject.Find("Shelf Value Display");if(prior)throw new Exception("Display already exists; edit individual items instead of replacing them.");
        var root=new GameObject("Shelf Value Display");Undo.RegisterCreatedObjectUndo(root,"Create value display");
        var report=new List<string>{"Values are game points, not real prices. Bottom physical board remains access step."};
        string[][] ids={new[]{"Book_01","Book_02","Book_03","PiggyBank","HeadphonesStand"},new[]{"BronzeBird","BlueGoldVase","SilverMusicBox","GoldTrophy"},new[]{"SnowGlobe","VintageCamera","MantelClock","PocketClock"}};
        int[][] prices={new[]{100,100,100,150,300},new[]{400,450,600,700},new[]{900,1200,1600,2000}};
        // Reserve the back of each board for props, leaving the front strip clear.
        for(int tier=0;tier<3;tier++)
        {
            var parent=new GameObject($"Tier {tier+1} - {(tier==0?"Low":tier==1?"Medium":"High")} Value");parent.transform.SetParent(root.transform);
            var board=boards[tier+1].bounds;
            for(int j=0;j<ids[tier].Length;j++)
            {
                string id=ids[tier][j];var model=manifest.items.First(m=>m.id==id);
                var go=new GameObject($"{id} [{prices[tier][j]}]");go.transform.SetParent(parent.transform);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model.path));visual.transform.SetParent(go.transform,false);
                // Blender exports -Y as the front; FBX converts this to Unity -Z.
                visual.transform.localRotation=Quaternion.Euler(0,90,0);
                var mats=new Dictionary<string,Material>();
                foreach(var data in model.materials)
                {
                    string safe=string.Concat(data.name.Select(c=>char.IsLetterOrDigit(c)?c:'_'));
                    string path=$"{Folder}/{id}_{safe}.mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
                    var col=new Color(data.color[0],data.color[1],data.color[2],data.color[3]).gamma;
                    col.a=data.glass?.14f:data.color[3];mat.SetColor("_BaseColor",col);
                    mat.SetFloat("_Metallic",data.metallic);mat.SetFloat("_Smoothness",data.smoothness);
                    if(!string.IsNullOrEmpty(data.texture))mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(data.texture));
                    if(data.glass){mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;}
                    EditorUtility.SetDirty(mat);mats[data.name]=mat;
                }
                foreach(var r in visual.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m&&mats.TryGetValue(m.name,out var found)?found:m).ToArray();
                var b=BoundsOf(go);
                float targetHeight=id.StartsWith("Book")?.32f:id=="VintageCamera"?.23f:id=="SilverMusicBox"?.25f:id=="PiggyBank"?.30f:.38f;
                visual.transform.localScale*=targetHeight/b.size.y;b=BoundsOf(go);
                float slot=board.size.z/ids[tier].Length;
                float fit=Mathf.Min(1,(slot-.08f)/b.size.z,.40f/b.size.x);
                visual.transform.localScale*=fit;b=BoundsOf(go);
                var center=new Vector3(board.max.x-.08f-b.extents.x,board.max.y+.002f+b.extents.y,board.min.z+slot*(j+.5f));
                go.transform.position+=center-b.center;b=BoundsOf(go);
                var collider=go.AddComponent<BoxCollider>();collider.center=go.transform.InverseTransformPoint(b.center);collider.size=b.size;
                var body=go.AddComponent<Rigidbody>();body.mass=1;body.isKinematic=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                var score=go.AddComponent<ScoreItem>();score.DisplayName=id;score.Points=prices[tier][j];score.Weight=1;
                report.Add($"Tier {tier+1}: {id} value={score.Points} bounds={b} surface={board.max.y:F4} gap={b.min.y-board.max.y:F4}");
            }
        }
        // Preserve existing collectibles, placing them on the low physical board.
        foreach(var name in new[]{"Desktop Globe","Lucky Cat"})
        {
            var g=GameObject.Find(name);if(!g)continue;Undo.RecordObject(g.transform,"Retain existing shelf props");
            var b=BoundsOf(g);var board=boards[0].bounds;
            g.transform.position+=new Vector3(board.max.x-.08f-b.extents.x,board.max.y+.002f+b.extents.y,board.center.z+(name=="Desktop Globe"?-.65f:.65f))-b.center;
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
        }
        Physics.SyncTransforms();Undo.CollapseUndoOperations(group);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllLines("Validation/ValueShelf/placement.txt",report);Capture();Debug.Log("VALUE_SHELF_COMPLETE");
    }
    [MenuItem("Catastrophe/Props/Capture Value Shelf")]
    public static void Capture()
    {
        var shelf=GameObject.Find("Tripo Shelving Unit");var b=BoundsOf(shelf);
        var go=new GameObject("Temporary Value Shelf Preview");var cam=go.AddComponent<Camera>();cam.transform.position=b.center+new Vector3(-.5f,.65f,-5f);cam.transform.LookAt(b.center+Vector3.up*.15f);cam.fieldOfView=42;
        var rt=new RenderTexture(1600,1100,24);var old=RenderTexture.active;cam.targetTexture=rt;
        try{cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();Directory.CreateDirectory("Validation/ValueShelf");File.WriteAllBytes("Validation/ValueShelf/preview.png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
        finally{RenderTexture.active=old;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
}
