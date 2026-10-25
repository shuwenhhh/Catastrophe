using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ColoredShelfPropsPass
{
    const string Folder = "Assets/Art/LivingRoom/ColoredShelfProps";
    static readonly string[] Names = { "IvoryPorcelain", "VermilionEnamel", "BrushedGold", "InkBlack", "RosePink", "OceanBlue", "LandSage", "WalnutBase", "AntiqueBrass" };
    static readonly Color[] Colors = { new(.96f,.95f,.91f), new(.78f,.14f,.10f), new(.86f,.64f,.30f), new(.035f,.028f,.024f), new(.86f,.50f,.52f), new(.10f,.38f,.60f), new(.52f,.65f,.34f), new(.32f,.19f,.11f), new(.69f,.52f,.28f) };

    [MenuItem("Catastrophe/Props/Apply Realistic Shelf Colors")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i=0;i<Names.Length;i++)
        {
            string path=$"{Folder}/{Names[i]}.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",Colors[i]);mat.SetFloat("_Metallic",i==2||i==8?.78f:0);
            mat.SetFloat("_Smoothness",i==0?.78f:i==6?.35f:.58f);EditorUtility.SetDirty(mat);
        }
        foreach(string id in new[]{"DesktopGlobe","LuckyCat"})
        {
            string file=id=="DesktopGlobe"?"ColoredGlobe":"ColoredLuckyCat";
            var importer=(ModelImporter)AssetImporter.GetAtPath($"{Folder}/{file}.fbx");
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            string path=$"Assets/Models/Tripo/Prefabs/Tripo_{id}.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try { Install(prefab,file);PrefabUtility.SaveAsPrefabAsset(prefab,path); }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
            var sceneObject=GameObject.Find(id=="DesktopGlobe"?"Desktop Globe":"Lucky Cat");
            if(sceneObject) Install(sceneObject,file);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Preview();PreviewScene();Debug.Log("COLORED_SHELF_PROPS_APPLIED");
    }
    static Bounds LocalBounds(GameObject root,Renderer[] rs)
    {
        bool first=true;Bounds b=default;
        foreach(var r in rs)
        {
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;var mb=mesh.bounds;
            for(int i=0;i<8;i++)
            {
                var p=mb.center+Vector3.Scale(mb.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                p=root.transform.InverseTransformPoint(r.transform.TransformPoint(p));
                if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
            }
        }return b;
    }
    public static void Install(GameObject root,string file)
    {
        var existing=root.transform.Find("Painted Shelf Prop");
        var old=root.GetComponentsInChildren<Renderer>(true).Where(r=>!existing || !r.transform.IsChildOf(existing)).ToArray();
        if(old.Length==0)throw new Exception("Missing original geometry");
        var target=LocalBounds(root,old);
        var model=existing?existing.gameObject:UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{file}.fbx"),root.transform);
        model.name="Painted Shelf Prop";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(-90,0,0);model.transform.localScale=Vector3.one;
        var rs=model.GetComponentsInChildren<Renderer>();var actual=LocalBounds(root,rs);
        model.transform.localScale=new Vector3(target.size.x/actual.size.x,target.size.y/actual.size.y,target.size.z/actual.size.z);
        actual=LocalBounds(root,rs);model.transform.localPosition+=target.center-actual.center;
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        foreach(var r in rs)r.sharedMaterials=r.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{m.name}.mat")??m).ToArray();
        foreach(var r in old){r.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
    }
    [MenuItem("Catastrophe/Props/Preview Shelf Colors")]
    public static void Preview()
    {
        Directory.CreateDirectory("Validation/ColoredShelfProps");
        var camObj=new GameObject("Temporary prop camera");var cam=camObj.AddComponent<Camera>();
        cam.orthographic=true;cam.orthographicSize=.26f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.65f,.64f,.60f);cam.cullingMask=1<<31;
        var rt=new RenderTexture(720,720,24);cam.targetTexture=rt;var prior=RenderTexture.active;
        foreach(string id in new[]{"DesktopGlobe","LuckyCat"})
        {
            var clone=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Tripo/Prefabs/Tripo_{id}.prefab"));
            clone.transform.position=Vector3.one*100;
            foreach(var t in clone.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var rs=clone.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            cam.orthographicSize=b.size.y*.64f;cam.transform.position=b.center+new Vector3(-.15f,.07f,1);cam.transform.LookAt(b.center);
            cam.Render();RenderTexture.active=rt;var tex=new Texture2D(720,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,720,720),0,0);tex.Apply();File.WriteAllBytes($"Validation/ColoredShelfProps/{id}.png",tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(clone);
        }
        RenderTexture.active=prior;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camObj);
    }
    static void PreviewScene()
    {
        var globe=GameObject.Find("Desktop Globe");var cat=GameObject.Find("Lucky Cat");
        var rs=new[]{globe,cat}.SelectMany(g=>g.GetComponentsInChildren<Renderer>()).Where(r=>r.enabled).ToArray();
        var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
        var go=new GameObject("Temporary shelf check");var cam=go.AddComponent<Camera>();
        cam.transform.position=bounds.center+new Vector3(-.25f,.25f,-1.45f);cam.transform.LookAt(bounds.center);
        cam.fieldOfView=45;var rt=new RenderTexture(1200,800,24);cam.targetTexture=rt;var prior=RenderTexture.active;
        try
        {
            cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();
            File.WriteAllBytes("Validation/ColoredShelfProps/scene.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllText("Validation/ColoredShelfProps/check.txt",string.Join("\n",new[]{globe,cat}.Select(g=>$"{g.name}: rigidbody={g.GetComponent<Rigidbody>()!=null}, score={g.GetComponent<ScoreItem>()!=null}, colliders={g.GetComponentsInChildren<Collider>().Length}, visible renderers={g.GetComponentsInChildren<Renderer>().Count(r=>r.enabled)}, materials={string.Join(",",g.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).SelectMany(r=>r.sharedMaterials).Select(m=>m.name))}")));
        }
        finally{RenderTexture.active=prior;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
