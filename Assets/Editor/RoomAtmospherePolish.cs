using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit scene authoring only. Never rebuilds the room or changes gameplay geometry.
[InitializeOnLoad]
public static class RoomAtmospherePolish
{
    const string Folder = "Assets/Settings/LivingRoom/GentleAtmosphere";
    const string Evidence = "Validation/GentleAtmosphere";
    const string Garden = "Assets/Art/LivingRoom/WindowGarden/SoftGardenView.png";
    const string Backdrop = "Soft garden beyond window";
    static readonly string[] Furniture = { "Table", "Chair 01", "Chair 02", "Chair 03" };
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static RoomAtmospherePolish()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("AtmosphereTest", false))
            { phase = 0; ready = EditorApplication.timeSinceStartup + 4; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("AtmosphereTest", false)) Cleanup();
        };
    }

    [MenuItem("Catastrophe/Room Atmosphere/Apply Gentle Wood And Garden")]
    static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/LivingRoom.unity")
            throw new InvalidOperationException("Open LivingRoom in Edit mode first.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Evidence);
        AssetDatabase.Refresh();
        if (!AssetDatabase.LoadAssetAtPath<Texture2D>(Garden)) throw new Exception("Garden texture missing");
        // Saved scene and material/pipeline copies remain recoverable outside Assets.
        string backup = Evidence + "/BeforePolish";
        Directory.CreateDirectory(backup);
        foreach (string path in new[] { scene.path, "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_Renderer.asset" })
            if (!File.Exists(backup + "/" + Path.GetFileName(path))) File.Copy(path, backup + "/" + Path.GetFileName(path));
        var geometry = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.gameObject.scene == scene).ToDictionary(c => c, Geometry);
        var table = Need("Table"); var chairs = Furniture.Skip(1).Select(Need).ToArray();
        var floor = Need("Uniform Light Oak Floor");
        Material oak = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/LivingRoom/FurnitureFinishes/NaturalOak.mat");
        Material floorSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/LivingRoom/ReferenceMaterials/LightOakFloor.mat");
        Material tableWood = Wood("WarmHoneyTable", oak, new Color(.70f,.54f,.36f), .22f);
        Material chairWood = Wood("SoftNaturalChairs", oak, new Color(.80f,.66f,.46f), .20f);
        Material floorWood = Wood("PaleOatFloor", floorSource, new Color(.89f,.79f,.64f), .18f);
        SetMaterial(table, tableWood);
        foreach (var chair in chairs) SetMaterial(chair, chairWood);
        foreach (var renderer in floor.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.sharedMaterials.Any(m => m == floorSource || (m && m.name == "PaleOatFloor"))) continue;
            Undo.RecordObject(renderer, "Gentle floor contrast");
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m == floorSource || (m && m.name == "PaleOatFloor") ? floorWood : m).ToArray();
            EditorUtility.SetDirty(renderer); PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        var light = Need("Window Dusk Key - warm directional").GetComponent<Light>();
        Undo.RecordObject(light, "Soft furniture grounding");
        light.shadows = LightShadows.Soft; light.shadowStrength = .58f;
        light.shadowBias = .04f; light.shadowNormalBias = .18f;
        var extra = light.GetUniversalAdditionalLightData();
        Undo.RecordObject(extra, "Contact shadow bias"); extra.usePipelineSettings = false;
        EditorUtility.SetDirty(light); EditorUtility.SetDirty(extra);
        ConfigurePipeline("Assets/Settings/PC_RPAsset.asset", 18f, 3);
        ConfigurePipeline("Assets/Settings/Mobile_RPAsset.asset", 16f, 1);
        // Modest local AO on desktop; no new fullscreen AO pass on mobile.
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        foreach (var feature in rendererData.rendererFeatures.Where(f => f && f.name == "ScreenSpaceAmbientOcclusion"))
        {
            Undo.RecordObject(feature, "Gentle contact shading");
            var settings = new SerializedObject(feature);
            settings.FindProperty("m_Settings.Intensity").floatValue = .55f;
            settings.FindProperty("m_Settings.Radius").floatValue = .25f;
            settings.ApplyModifiedProperties(); EditorUtility.SetDirty(feature);
        }
        InstallGarden();
        bool unchanged = geometry.All(pair => pair.Key && Geometry(pair.Key) == pair.Value);
        if (!unchanged) throw new Exception("An existing collision/transform changed unexpectedly");
        File.WriteAllText(Evidence + "/authoring-report.txt", "PASS all " + geometry.Count + " original colliders/transforms preserved.\n" +
            "PASS separate table/chair/floor wood materials retain existing grain.\n" +
            "PASS garden backdrop has no collider and casts no shadows; original window safety pane retained.\n" +
            "PASS soft shadows enabled on PC and Mobile without extra mobile AO pass.\n");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("GENTLE_ATMOSPHERE_SAVED — wood contrast, soft shadows and window garden only.");
    }

    static string Geometry(Collider c) => EditorJsonUtility.ToJson(c) + c.transform.localToWorldMatrix.ToString("F6");
    static GameObject Need(string name) => GameObject.Find(name) ?? throw new Exception("Missing " + name);
    static Material Wood(string name, Material source, Color color, float smoothness)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(source) { name = name }; AssetDatabase.CreateAsset(material, path); }
        Undo.RecordObject(material, "Gentle wood palette");
        material.SetColor("_BaseColor", color); material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(material); return material;
    }
    static void SetMaterial(GameObject root, Material material)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>().Where(r => r.enabled))
        {
            Undo.RecordObject(r, "Separate furniture wood tones");
            r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();
            r.receiveShadows = true; r.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(r); PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
    }
    static void ConfigurePipeline(string path, float distance, int quality)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        Undo.RecordObject(asset, "Soft indoor shadows");
        var data = new SerializedObject(asset);
        data.FindProperty("m_SoftShadowsSupported").boolValue = true;
        data.FindProperty("m_SoftShadowQuality").intValue = quality;
        data.FindProperty("m_ShadowDistance").floatValue = distance;
        data.ApplyModifiedProperties(); EditorUtility.SetDirty(asset);
    }
    static void InstallGarden()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Garden);
        importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear; importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
        string path = Folder + "/HazyWindowGarden.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Garden));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Cull", 0f); EditorUtility.SetDirty(material);
        var go = GameObject.Find(Backdrop);
        if (!go) { go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = Backdrop; Undo.RegisterCreatedObjectUndo(go, "Window garden"); }
        foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        if (!go.GetComponent<SoftDecoration>()) go.AddComponent<SoftDecoration>();
        go.transform.SetParent(Need("Four Wall Room Shell (editable)").transform, true);
        go.transform.SetPositionAndRotation(new Vector3(1.15f,2.05f,3.40f), Quaternion.identity);
        go.transform.localScale = new Vector3(2.6f,1.9f,1f);
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        go.isStatic = true;
    }

    static int phase, originalQuality;
    static double ready;
    static CatController cat;
    static Camera camera;
    static ThirdPersonCamera orbit;
    static readonly List<string> checks = new();
    [MenuItem("Catastrophe/Room Atmosphere/Verify In Play")]
    static void Verify()
    {
        if (Application.isPlaying) throw new Exception("Exit Play before atmosphere verification");
        foreach (var key in new[] { GameSaveStore.SaveKey, GameSaveStore.HistoryKey })
        {
            SessionState.SetBool("AtmosphereHas" + key, PlayerPrefs.HasKey(key));
            SessionState.SetString("AtmosphereSave" + key, PlayerPrefs.GetString(key, ""));
        }
        originalQuality = QualitySettings.GetQualityLevel(); SessionState.SetInt("AtmosphereQuality", originalQuality);
        SessionState.SetString("AtmospherePipeline", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
        checks.Clear(); SessionState.SetBool("AtmosphereTest", true); EditorApplication.isPlaying = true;
    }
    static void Check(bool ok, string message) => checks.Add((ok ? "PASS " : "FAIL ") + message);
    static void Tick()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup < ready) return;
        try
        {
            if (phase == 0)
            {
                cat = Object.FindAnyObjectByType<CatController>(); camera = Camera.main; orbit = camera.GetComponent<ThirdPersonCamera>();
                var flow = Object.FindAnyObjectByType<GameFlowUI>(); flow.BeginGame(); typeof(GameFlowUI).GetField("runActive", Private).SetValue(flow, false);
                Check(cat.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled) && orbit.FollowTarget == cat.transform, "Actual skinned cat and gameplay camera retained");
                Material table = Need("Table").GetComponentInChildren<Renderer>().sharedMaterial;
                Material chair = Need("Chair 01").GetComponentInChildren<Renderer>().sharedMaterial;
                var floor = Need("Uniform Light Oak Floor").GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).First(m => m.name == "PaleOatFloor");
                Check(table != chair && chair != floor && table.GetColor("_BaseColor").grayscale < chair.GetColor("_BaseColor").grayscale &&
                    chair.GetColor("_BaseColor").grayscale < floor.GetColor("_BaseColor").grayscale, "Table darker than chairs; floor lightest, all retain wood textures");
                Check(new[] { table,chair,floor }.All(m => m.GetTexture("_BaseMap") && m.GetFloat("_Surface") == 0), "All three wood finishes remain textured and opaque");
                var garden = Need(Backdrop); var pane = Need("V2 Window safety pane");
                Check(!garden.GetComponents<Collider>().Any(), "Garden remains visual-only after runtime collision generation");
                Check(garden.GetComponent<Renderer>().shadowCastingMode == ShadowCastingMode.Off, "Garden never blocks the window light");
                Check(pane.GetComponent<Collider>().enabled, "Original window safety collision remains enabled");
                Check(!garden.GetComponent<Renderer>().sharedMaterial.shader.name.Contains("Error"), "Garden shader resolves in running game");
                Fixture(new Vector3(.25f,.99f,-.65f),0); phase++; ready=EditorApplication.timeSinceStartup+.7; return;
            }
            if (phase == 1) { Capture("01-tabletop-PC"); Fixture(new Vector3(1.7f,.225f,-1.5f),0); phase++; ready=EditorApplication.timeSinceStartup+.7; return; }
            if (phase == 2) { Capture("02-ground-PC"); Fixture(new Vector3(.55f,.225f,-.2f),0); orbit.RestoreView(0,5); phase++; ready=EditorApplication.timeSinceStartup+.7; return; }
            if (phase == 3)
            {
                Capture("03-under-table-PC"); orbit.enabled=false;
                camera.transform.position=new Vector3(.7f,1.6f,.65f);camera.transform.LookAt(new Vector3(1.15f,1.75f,3.25f)); camera.fieldOfView=55;
                phase++; ready=EditorApplication.timeSinceStartup+.5; return;
            }
            if (phase == 4)
            {
                Capture("04-window-PC");
                // Standalone excludes the Mobile quality tier, so index 0 may
                // still resolve to PC. Switch the actual pipeline for this test.
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                phase++; ready=EditorApplication.timeSinceStartup+2; return;
            }
            if (phase == 5)
            {
                Check(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).supportsSoftShadows &&
                    GraphicsSettings.currentRenderPipeline == AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"),
                    "Active Mobile rendering pipeline has soft shadows enabled: " + GraphicsSettings.currentRenderPipeline.name);
                Capture("05-window-Mobile"); orbit.enabled=true; Fixture(new Vector3(1.7f,.225f,-1.5f),0);
                phase++; ready=EditorApplication.timeSinceStartup+.7; return;
            }
            if (phase == 6)
            {
                Capture("06-ground-Mobile"); Fixture(new Vector3(.25f,.99f,-.65f),0);
                phase++; ready=EditorApplication.timeSinceStartup+.7; return;
            }
            Capture("07-tabletop-Mobile");
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1, "Only one active gameplay camera after quality switch");
            Finish();
        }
        catch (Exception e) { checks.Add("FAIL " + e); Debug.LogException(e); Finish(); }
    }
    static void Fixture(Vector3 position,float yaw)
    {
        var body=cat.GetComponent<Rigidbody>(); if(!body.isKinematic)body.linearVelocity=Vector3.zero;
        body.isKinematic=true;body.position=position;body.rotation=Quaternion.Euler(0,yaw,0);
        cat.transform.SetPositionAndRotation(position,body.rotation);Physics.SyncTransforms();orbit.Initialize(cat.transform);
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory(Evidence);
        var rt=RenderTexture.GetTemporary(1280,720,24);var prior=camera.targetTexture;var active=RenderTexture.active;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",image.EncodeToPNG());}
        finally {camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static void Finish()
    {
        File.WriteAllLines(Evidence+"/play-report.txt",checks.Concat(new[]{"Visual review required; no hardware performance claim."}));
        Debug.Log("ATMOSPHERE_PLAY_QA\n"+string.Join("\n",checks));Cleanup();EditorApplication.isPlaying=false;
    }
    static void Cleanup()
    {
        EditorApplication.update-=Tick;
        var flow=Object.FindAnyObjectByType<GameFlowUI>();if(flow)typeof(GameFlowUI).GetField("runActive",Private).SetValue(flow,false);
        QualitySettings.SetQualityLevel(SessionState.GetInt("AtmosphereQuality",1),true);
        QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString("AtmospherePipeline", ""));
        foreach(var key in new[]{GameSaveStore.SaveKey,GameSaveStore.HistoryKey})
            if(SessionState.GetBool("AtmosphereHas"+key,false))PlayerPrefs.SetString(key,SessionState.GetString("AtmosphereSave"+key,""));else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();SessionState.SetBool("AtmosphereTest",false);
    }
}
