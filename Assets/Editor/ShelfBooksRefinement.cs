using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ShelfBooksRefinement
{
    public const string Folder = "Assets/Art/LivingRoom/RefinedShelfBooks";
    public const string Evidence = "Validation/RefinedShelfBooks";
    public static readonly string[] Targets = { "Tripo Book Stack", "Shelf Books 02", "Shelf Books 03" };
    const string PrefabPath = "Assets/Models/Tripo/Prefabs/Tripo_BookStack.prefab";

    [MenuItem("Catastrophe/Books/2 Install Refined Hardbacks")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before changing the book prefab.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Evidence);
        AssetDatabase.Refresh();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/RefinedShelfBooks.fbx");
        if (!source) throw new InvalidOperationException("Refined FBX has not imported.");
        string backup = Evidence + "/OriginalBookStack.prefab.txt";
        if (!File.Exists(backup)) File.Copy(PrefabPath, backup);
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        GameObject preview = null;
        try
        {
            var collider = prefab.GetComponent<BoxCollider>();
            string oldCollider = EditorJsonUtility.ToJson(collider);
            string oldBody = EditorJsonUtility.ToJson(prefab.GetComponent<Rigidbody>());
            string oldValue = EditorJsonUtility.ToJson(prefab.GetComponent<Valuable>());
            string oldScore = EditorJsonUtility.ToJson(prefab.GetComponent<ScoreItem>());
            preview = Object.Instantiate(source);
            preview.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var filter = preview.GetComponentsInChildren<MeshFilter>().Single();
            var mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = "FittedHardbacks";
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var transform = filter.transform.localToWorldMatrix;
            var bounds = new Bounds(transform.MultiplyPoint3x4(vertices[0]),Vector3.zero);
            for (int i=0;i<vertices.Length;i++) { vertices[i]=transform.MultiplyPoint3x4(vertices[i]); bounds.Encapsulate(vertices[i]); }
            Vector3 fit = new Vector3(collider.size.x/bounds.size.x,collider.size.y/bounds.size.y,collider.size.z/bounds.size.z);
            for (int i=0;i<vertices.Length;i++)
            {
                vertices[i]=Vector3.Scale(vertices[i]-bounds.center,fit)+collider.center;
                Vector3 n=transform.MultiplyVector(normals[i]);
                normals[i]=new Vector3(n.x/fit.x,n.y/fit.y,n.z/fit.z).normalized;
            }
            mesh.vertices=vertices; mesh.normals=normals; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string meshPath=Folder+"/FittedHardbacks.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existing) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; }
            else AssetDatabase.CreateAsset(mesh,meshPath);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            var materials=filter.GetComponent<Renderer>().sharedMaterials.Select(m=>MakeMaterial(m.name)).ToArray();

            // Only replace the visual child. Root GUID, hierarchy slot, collider,
            // rigidbody, price, noise, save identity and push logic stay intact.
            foreach(Transform child in prefab.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var visual=new GameObject("Refined hardback books",typeof(MeshFilter),typeof(MeshRenderer));
            visual.transform.SetParent(prefab.transform,false);
            visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials=materials;
            if(oldCollider!=EditorJsonUtility.ToJson(collider)||oldBody!=EditorJsonUtility.ToJson(prefab.GetComponent<Rigidbody>())||
                oldValue!=EditorJsonUtility.ToJson(prefab.GetComponent<Valuable>())||oldScore!=EditorJsonUtility.ToJson(prefab.GetComponent<ScoreItem>()))
                throw new InvalidOperationException("Book gameplay components changed unexpectedly.");
            PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
            File.WriteAllText(Evidence+"/install.txt",$"Original source: 2,147,319 vertices per stack.\nNew fitted mesh: {mesh.vertexCount} vertices, {mesh.triangles.Length/3} triangles, {mesh.subMeshCount} material groups.\nCollider/body/value/score serialized contents unchanged.\nOriginal prefab backed up to {backup}. Source FBX retained. Authored scene not saved.\n");
        }
        finally
        {
            if(preview)Object.DestroyImmediate(preview);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        // Prefab scene instances refresh on the next editor update.
        EditorApplication.delayCall += () => CaptureBooks("after");
    }

    static Material MakeMaterial(string sourceName)
    {
        string name=sourceName.Replace(" (Instance)","");
        string hex; float metal=0f, smooth=.4f;
        switch(name)
        {
            case "Midnight cloth": hex="354F68"; break;
            case "Dusty rose cloth": hex="AA6558"; break;
            case "Sage cloth": hex="758976"; break;
            case "Oat linen cloth": hex="DCCBA9"; break;
            case "Warm ivory pages": hex="F4E8CC"; smooth=.16f; break;
            case "Fine page seams": hex="C5B798"; smooth=.12f; break;
            case "Antique gold foil": hex="C19A54"; metal=.55f; smooth=.66f; break;
            case "Ink embossing": hex="345C56"; smooth=.35f; break;
            case "Burgundy silk ribbon": hex="854F50"; smooth=.58f; break;
            default: throw new InvalidOperationException("Unexpected book material "+name);
        }
        string path=Folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
        ColorUtility.TryParseHtmlString("#"+hex,out Color color);
        mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",metal); mat.SetFloat("_Smoothness",smooth);
        // Sub-millimetre tooling should not cast noisy shadow-map dots on paper.
        bool tinyDetail = name == "Fine page seams" || name == "Ink embossing" || name == "Antique gold foil";
        mat.SetShaderPassEnabled("ShadowCaster", !tinyDetail);
        mat.SetFloat("_ReceiveShadows",tinyDetail?0f:1f);
        if(tinyDetail) mat.EnableKeyword("_RECEIVE_SHADOWS_OFF"); else mat.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        mat.enableInstancing=true; EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    [MenuItem("Catastrophe/Books/1 Inspect Existing Books")]
    public static void Inspect()
    {
        Directory.CreateDirectory(Evidence);
        var lines = new List<string> { "Playing=" + Application.isPlaying };
        foreach (string name in Targets)
        {
            var go = GameObject.Find(name);
            if (!go) { lines.Add("MISSING " + name); continue; }
            lines.Add($"{name}: prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)} parent={go.transform.parent?.name} world={go.transform.position} rotation={go.transform.eulerAngles} scale={go.transform.lossyScale}");
            foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
                lines.Add($"  {f.name}: mesh={AssetDatabase.GetAssetPath(f.sharedMesh)} vertices={f.sharedMesh?.vertexCount} local={f.transform.localPosition} rotation={f.transform.localEulerAngles} scale={f.transform.localScale} bounds={f.GetComponent<Renderer>()?.bounds} enabled={f.GetComponent<Renderer>()?.enabled}");
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                lines.Add($"  Collider {c.name}: {c.GetType().Name} worldBounds={c.bounds} enabled={c.enabled} trigger={c.isTrigger}");
            lines.Add($"  Value={go.GetComponent<ScoreItem>()?.Points} noise={go.GetComponent<ScoreItem>()?.Noise}");
        }
        File.WriteAllLines(Evidence + "/source-inspection.txt", lines);
        CaptureBooks("before");
    }

    public static void CaptureBooks(string suffix)
    {
        var books = Targets.Select(GameObject.Find).Where(g => g).ToArray();
        if (books.Length == 0) return;
        var all = books.SelectMany(g => g.GetComponentsInChildren<Renderer>()).Where(r => r.enabled).ToArray();
        var bounds = all[0].bounds; foreach (var r in all.Skip(1)) bounds.Encapsulate(r.bounds);
        Capture(bounds.center + new Vector3(-1.65f, 1.0f, -1.35f), bounds.center,
            Evidence + "/books-" + suffix + ".png", 40);
        var first = books[0].GetComponentsInChildren<Renderer>().First(r => r.enabled).bounds;
        Capture(first.center + new Vector3(-.55f,.42f,-.50f), first.center,
            Evidence + "/detail-" + suffix + ".png", 36);
    }

    [MenuItem("Catastrophe/Books/4 Inspect Fine Detail")]
    public static void DiagnoseDetail()
    {
        var g=GameObject.Find(Targets[0]);
        var r=g.GetComponentInChildren<MeshRenderer>();
        var mesh=g.GetComponentInChildren<MeshFilter>().sharedMesh;
        var lines=new List<string>();var verts=mesh.vertices;
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var indices=mesh.GetIndices(sub);var b=new Bounds(verts[indices[0]],Vector3.zero);
            foreach(int index in indices)b.Encapsulate(verts[index]);
            lines.Add(r.sharedMaterials[sub].name+" local bounds="+b);
        }
        File.WriteAllLines(Evidence+"/detail-diagnosis.txt",lines);
        var all=Targets.Select(GameObject.Find).SelectMany(o=>o.GetComponentsInChildren<MeshRenderer>()).ToArray();
        var shadows=all.Select(o=>o.shadowCastingMode).ToArray();var receives=all.Select(o=>o.receiveShadows).ToArray();
        try
        {
            foreach(var item in all){item.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;item.receiveShadows=false;}
            CaptureBooks("no-shadows-diagnostic");
        }
        finally {for(int i=0;i<all.Length;i++){all[i].shadowCastingMode=shadows[i];all[i].receiveShadows=receives[i];}}
    }

    public static void Capture(Vector3 position, Vector3 target, string path, float fov)
    {
        Directory.CreateDirectory(Evidence);
        var go = new GameObject("Disposable book preview camera");
        var camera = go.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target-position));
        camera.fieldOfView = fov; camera.nearClipPlane = .015f;
        var rt = RenderTexture.GetTemporary(1600,1100,24);
        var old = RenderTexture.active;
        var image = new Texture2D(1600,1100,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1600,1100),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=old; camera.targetTexture=null;
            RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(go);
        }
    }
}
