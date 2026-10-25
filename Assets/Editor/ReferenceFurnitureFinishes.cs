using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ReferenceFurnitureFinishes
{
    const string Folder = "Assets/Settings/LivingRoom/FurnitureFinishes";

    [MenuItem("Catastrophe/Furniture Finishes/01 Cream Sofa")]
    public static void Sofa() => Apply("01_Sofa", new[] { "Sofa" },
        new[] { Finish("CreamLinen", new Color(.94f,.895f,.79f), .13f), Oak() }, p => p.y < .145f ? 1 : 0);

    [MenuItem("Catastrophe/Furniture Finishes/02 Oak Table and Chairs")]
    public static void Wood() => Apply("02_TableChairs", new[] { "Table", "Chair 01", "Chair 02", "Chair 03" },
        new[] { Oak() }, p => 0);

    [MenuItem("Catastrophe/Furniture Finishes/03 Light Oak TV Cabinet and Navy Sideboard")]
    public static void Cabinets()
    {
        CabinetOak();
        Apply("03_Cabinets", new[] { "Tripo Navy Sideboard" },
            new[] { Finish("NavyPaint", new Color(.19f,.265f,.405f), .32f), Finish("DarkLegs", new Color(.075f,.085f,.10f), .22f) },
            p => p.y < .19f ? 1 : 0);
        Apply("03_Television", new[] { "Tripo Television" }, new[] { Finish("ScreenGraphite", new Color(.035f,.043f,.055f), .44f) }, p => 0);
    }

    [MenuItem("Catastrophe/Furniture Finishes/Light Oak TV Cabinet Only")]
    public static void CabinetOak()
    {
        var grain = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Settings/LivingRoom/ReferenceMaterials/FineOakGrain.png");
        if (!grain) throw new InvalidOperationException("FineOakGrain texture is missing.");
        var oak = Finish("TVConsoleLightOak", new Color(.73f,.56f,.39f), .22f);
        oak.SetTexture("_BaseMap", grain);
        var topOak = Finish("TVConsoleLightOakTop", new Color(.73f,.56f,.39f), .22f);
        topOak.SetTexture("_BaseMap", grain);
        topOak.SetTextureScale("_BaseMap", new Vector2(2.5f, 1.2f));
        var top = GameObject.Find("TV cabinet - extended walkable top");
        if (!top || !top.TryGetComponent<MeshRenderer>(out var topRenderer))
            throw new InvalidOperationException("Extended walkable TV top is missing.");
        Undo.RecordObject(topRenderer, "Light oak TV console top");
        topRenderer.sharedMaterial = topOak;
        EditorUtility.SetDirty(topRenderer);
        Apply("03_TVCabinetLightOak", new[] { "Tripo Tv Cabinet" }, new[] { oak }, p => 0, true);
    }

    [MenuItem("Catastrophe/Furniture Finishes/04 Cherry Red Lamp")]
    public static void Lamp() => Apply("04_Lamp", new[] { "Tripo Table Lamp" },
        new[] { Finish("CherryEnamel", new Color(.76f,.115f,.13f), .42f), Finish("WarmIvoryLamp", new Color(.97f,.88f,.67f), .24f) },
        p => p.y > .54f || p.y < .12f ? 0 : 1);

    [MenuItem("Catastrophe/Furniture Finishes/05 Sage Curtains")]
    public static void Curtains() => Apply("05_Curtains", new[] { "Tripo Curtain" },
        new[] { Finish("SageLinen", new Color(.65f,.715f,.56f), .10f), Oak() }, p => p.y > .965f ? 1 : 0);

    static Material Oak()
    {
        Material material = Finish("NaturalOak", new Color(.83f,.675f,.465f), .24f);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Settings/LivingRoom/ReferenceMaterials/FineOakGrain.png"));
        return material;
    }
    static Material Finish(string name, Color color, float smoothness)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play before editing finishes.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Settings/LivingRoom", "FurnitureFinishes");
        string path = Folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(m); return m;
    }
    static void Apply(string batch, string[] names, Material[] materials, Func<Vector3,int> region, bool directionalOakGrain = false)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/LivingRoom.unity") throw new InvalidOperationException("Open LivingRoom in Edit mode first.");
        List<GameObject> targets = names.Select(GameObject.Find).ToList();
        if (targets.Any(t => t == null)) throw new InvalidOperationException("Missing target in " + batch);
        foreach (GameObject go in targets)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Reference furniture finish " + batch);
            string id = go.name.Replace(" ", "_"); int part = 0;
            Bounds b = BoundsOf(go);
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                string sourcePath = Folder + $"/{id}_{part}_Source.asset";
                Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
                if (source == null) { source = Object.Instantiate(filter.sharedMesh); AssetDatabase.CreateAsset(source, sourcePath); }
                Mesh mesh = Object.Instantiate(source);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 w = filter.transform.TransformPoint(vertices[i]);
                    // Vertical front-door grain, lengthwise grain on the top.
                    // World-metre mapping keeps the pattern legible after scaling.
                    if (directionalOakGrain)
                    {
                        Vector3 n = normals.Length == vertices.Length
                            ? filter.transform.TransformDirection(normals[i]).normalized : Vector3.up;
                        uv[i] = Mathf.Abs(n.y) > .6f
                            ? new Vector2(w.x * .8f, w.z * 2.1f)
                            : Mathf.Abs(n.z) > .6f
                                ? new Vector2(w.y * .8f, w.x * 2.0f)
                                : new Vector2(w.y * .8f, w.z * 2.0f);
                    }
                    else uv[i] = new Vector2(w.x * .8f + w.z * .17f, w.z * 1.7f + w.y * 1.3f);
                }
                mesh.uv = uv;
                var buckets = Enumerable.Range(0, materials.Length).Select(_ => new List<int>()).ToArray();
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 p = filter.transform.TransformPoint((vertices[triangles[i]] + vertices[triangles[i+1]] + vertices[triangles[i+2]]) / 3f);
                    p = new Vector3((p.x-b.min.x)/b.size.x, (p.y-b.min.y)/b.size.y, (p.z-b.min.z)/b.size.z);
                    buckets[region(p)].AddRange(new[] { triangles[i], triangles[i+1], triangles[i+2] });
                }
                mesh.subMeshCount = materials.Length;
                for (int i = 0; i < materials.Length; i++) mesh.SetTriangles(buckets[i], i);
                string destination = Folder + $"/{id}_{part++}_Finished.asset";
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(destination);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, destination); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                filter.sharedMesh = saved; filter.GetComponent<Renderer>().sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter.GetComponent<Renderer>());
            }
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("FURNITURE_FINISH_SAVED " + batch + " targets=" + string.Join(",", names) + "; transforms and colliders unchanged");
        // Let the renderer update its instance data before taking comparison images.
        EditorApplication.delayCall += () => { foreach (GameObject go in targets) if (go != null) Capture(go, batch); };
    }
    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] r = go.GetComponentsInChildren<Renderer>(); Bounds b = r[0].bounds;
        foreach (Renderer next in r.Skip(1)) b.Encapsulate(next.bounds); return b;
    }
    static void Capture(GameObject target, string batch)
    {
        Directory.CreateDirectory("Validation/FurnitureFinishes");
        Bounds b = BoundsOf(target); float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        GameObject go = new("Temporary finish capture"); Camera c = go.AddComponent<Camera>();
        c.transform.position = b.center + new Vector3(1f,.6f,-1.25f).normalized * size * 2.0f;
        c.transform.LookAt(b.center); c.fieldOfView = 40; c.nearClipPlane = .03f;
        RenderTexture rt = new(1100, 900, 24); c.targetTexture = rt; RenderTexture previous = RenderTexture.active;
        try
        {
            c.Render(); RenderTexture.active = rt; Texture2D image = new(1100,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1100,900),0,0); image.Apply();
            File.WriteAllBytes("Validation/FurnitureFinishes/" + batch + "_" + target.name.Replace(" ","_") + ".png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = previous; c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
    }
}
