using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit room art pass. No automatic execution or gameplay geometry edits.
public static class StorybookRoomFinish
{
    public const string Folder = "Assets/Settings/LivingRoom/StorybookPalette";
    public const string Evidence = "Validation/StorybookRoom";
    public const string Prefix = "Storybook_";

    // The girl's butter-yellow shirt, peach cheeks and mint clips inform the
    // accent palette. The cat and girl textures themselves are never recoloured.
    static readonly Dictionary<string,(string hex,float smooth)> Colours = new()
    {
        ["WarmHoneyTable"]=("C99A58",.20f), ["SoftNaturalChairs"]=("DDB579",.18f),
        ["PaleOatFloor"]=("E8CCAA",.16f), ["LightOakSeam"]=("C3A17B",.12f),
        ["RugCream"]=("F3D9C3",.08f), ["RugBorder"]=("D99785",.08f),
        ["Ivory upholstery"]=("FFF0D2",.12f), ["Warm seam"]=("D6B994",.10f),
        ["Honey oak"]=("926236",.20f), ["Kraft cardboard"]=("B98853",.06f), ["Paper tape"]=("DFB97F",.08f),
        ["Terracotta woven cloth"]=("D46C5D",.08f), ["Curtain hem and ties"]=("B65149",.08f),
        ["Walnut rod"]=("795138",.18f),
        ["Oatmeal carpet"]=("F3BCD0",.08f), ["Natural sisal"]=("DFA2B7",.08f),
        ["HoneyOak"]=("D8AB6C",.18f), ["CharcoalMetal"]=("414949",.20f),
        ["TVConsoleLightOak"]=("CFAB76",.22f), ["TVConsoleLightOakTop"]=("CFAB76",.22f),
        ["Furniture_LightOak"]=("FFF2DD",.18f),
        ["LeafGreen"]=("629553",.22f), ["Tripo_PothosPlant_0_0"]=("57985A",.24f),
        ["IvoryPlanter"]=("FFF3DF",.38f), ["WindowWhitePorcelain"]=("FFFAEF",.42f),
        ["Cream glazed ceramic"]=("F9DD9C",.40f), ["Tripo_Cup_0_0"]=("F9E4B2",.35f),
        ["Tripo_Mug_0_0"]=("5299BC",.38f),
        ["Tripo_TableLamp_0_0"]=("F6C35B",.24f), ["Tripo_TableLamp_0_1"]=("F6C35B",.24f),
        ["Book cover 0"]=("8CBAA0",.18f), ["Book cover 1"]=("D98779",.18f),
        ["Book cover 2"]=("739DB4",.18f), ["Book cover 3"]=("ECC86E",.18f),
        ["Ivory book pages"]=("F6E9CC",.08f),
        ["RoomShell_FAF6ED"]=("FFF1DB",.12f), ["RoomShell_FAF5E8"]=("FFFAED",.16f),
        ["RoomShell_916138"]=("AD7C54",.18f)
    };

    [MenuItem("Catastrophe/Storybook Room/2 Apply Character Palette")]
    public static void Apply()
    {
        var scene=RequireScene();
        Directory.CreateDirectory(Evidence); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        // Preserve both the saved scene and the current unsaved user state.
        string backup=Evidence+"/BeforeCharacterPalette";Directory.CreateDirectory(backup);
        if(!File.Exists(backup+"/SavedLivingRoom.unity")) File.Copy(scene.path,backup+"/SavedLivingRoom.unity");
        if(!File.Exists(backup+"/WorkingLivingRoom.unity"))
            if(!EditorSceneManager.SaveScene(scene,backup+"/WorkingLivingRoom.unity",true))throw new Exception("Could not back up the current scene.");
        var colliders=Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.gameObject.scene==scene).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        var transforms=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(t=>t.gameObject.scene==scene).ToDictionary(t=>t,t=>t.localToWorldMatrix.ToString("F6"));
        int replaced=0;var changedMaterials=new HashSet<Material>();
        foreach(var r in Renderers(scene))
        {
            Material[] materials=r.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];if(!source)continue;
                string key=source.name.StartsWith(Prefix)?source.name.Substring(Prefix.Length):source.name;
                var palette=Colours.TryGetValue(key,out var entry)?entry:
                    key.StartsWith("Tripo_FlowerVase_0_")?("D87995",.24f):(null,0f);
                if(palette.Item1==null)continue;
                string path=Folder+"/"+Prefix+key+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!mat){mat=new Material(source){name=Prefix+key};AssetDatabase.CreateAsset(mat,path);}
                Undo.RecordObject(mat,"Character-consistent room colours");
                ColorUtility.TryParseHtmlString("#"+palette.Item1,out Color colour);
                mat.SetColor("_BaseColor",colour);if(mat.HasProperty("_Color"))mat.SetColor("_Color",colour);
                mat.SetFloat("_Smoothness",palette.Item2);
                EditorUtility.SetDirty(mat);changedMaterials.Add(mat);materials[i]=mat;changed=true;replaced++;
            }
            if(!changed)continue;
            Undo.RecordObject(r,"Character-consistent room colours");r.sharedMaterials=materials;
            EditorUtility.SetDirty(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        if(replaced<40)throw new Exception("Unexpectedly few room surfaces were matched; inspect the active room.");
        ApplyLighting();
        if(colliders.Any(p=>!p.Key||p.Value!=EditorJsonUtility.ToJson(p.Key)+p.Key.transform.localToWorldMatrix.ToString("F6")) ||
            transforms.Any(p=>!p.Key||p.Value!=p.Key.localToWorldMatrix.ToString("F6")))
            throw new Exception("An existing object or collider moved unexpectedly. Do not save.");
        foreach(var mat in changedMaterials)AssetDatabase.SaveAssetIfDirty(mat);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Evidence+"/authoring.txt",$"PASS {replaced} material slots, {changedMaterials.Count} dedicated palette materials.\nPASS {colliders.Count} original colliders and {transforms.Count} transforms unchanged.\nOriginal materials retained. Disk and unsaved working scene backed up.\nNo new geometry, runtime camera, controls or character texture changes.\n");
        EditorApplication.delayCall+=()=>CaptureOverview("after");
        Debug.Log("STORYBOOK_PALETTE_APPLIED — warm cream, coral, mint and butter yellow; gameplay geometry preserved.");
    }

    static void ApplyLighting()
    {
        var rig=GameObject.Find(LivingRoomLightingSetup.RigName);
        if(!rig)throw new Exception("Authored room lighting rig missing.");
        foreach(var light in rig.GetComponentsInChildren<Light>(true))
        {
            Undo.RecordObject(light,"Soft storybook daylight");
            light.color=light.type==LightType.Directional?new Color(1f,.975f,.93f):new Color(.94f,.98f,1f);
            light.intensity=light.type==LightType.Directional?1.35f:1.20f;
            if(light.type==LightType.Directional){light.shadowStrength=.58f;light.shadows=LightShadows.Soft;}
            EditorUtility.SetDirty(light);
        }
        // GameBootstrap already removes this template light in Play. Disable it
        // in authoring too, so previews do not double-light the whole room.
        var legacy=GameObject.Find("Directional Light")?.GetComponent<Light>();
        if(legacy){Undo.RecordObject(legacy,"Match edit lighting to gameplay");legacy.enabled=false;EditorUtility.SetDirty(legacy);}
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.84f,.87f,.89f);
        RenderSettings.ambientEquatorColor=new Color(.78f,.76f,.71f);
        RenderSettings.ambientGroundColor=new Color(.58f,.50f,.43f);
        RenderSettings.ambientIntensity=1f;
        var volume=rig.GetComponentInChildren<Volume>(true);
        if(!volume||!volume.sharedProfile)throw new Exception("Room colour profile missing.");
        const string path=Folder+"/StorybookDaylight.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile)
        {
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile),path))throw new Exception("Could not clone room volume.");
            profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }
        var colour=Component<ColorAdjustments>(profile);colour.postExposure.Override(.15f);colour.contrast.Override(5f);
        colour.saturation.Override(7f);colour.colorFilter.Override(Color.white);
        var white=Component<WhiteBalance>(profile);white.temperature.Override(0f);white.tint.Override(0f);
        var vignette=Component<Vignette>(profile);vignette.intensity.Override(.035f);
        foreach(var component in profile.components){EditorUtility.SetDirty(component);AssetDatabase.SaveAssetIfDirty(component);}
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        Undo.RecordObject(volume,"Storybook daylight profile");volume.sharedProfile=profile;EditorUtility.SetDirty(volume);
    }
    static T Component<T>(VolumeProfile profile) where T:VolumeComponent
    {
        if(!profile.TryGet(out T component)){component=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(component,profile);}
        component.active=true;return component;
    }

    [MenuItem("Catastrophe/Storybook Room/1 Inspect Current Finishes")]
    public static void Inspect()
    {
        var scene = RequireScene();
        Directory.CreateDirectory(Evidence);
        var lines = new List<string> { "Scene="+scene.path+" dirty="+scene.isDirty };
        foreach (var group in Renderers(scene).SelectMany(r => r.sharedMaterials.Where(m=>m).Select(m=>(r,m)))
            .GroupBy(pair => pair.m).OrderBy(g=>AssetDatabase.GetAssetPath(g.Key)))
        {
            var mat=group.Key;
            lines.Add(AssetDatabase.GetAssetPath(mat)+" | "+mat.name+" | "+mat.shader.name+
                " | "+(mat.HasProperty("_BaseColor")?ColorUtility.ToHtmlStringRGB(mat.GetColor("_BaseColor")):"no base color")+
                " | "+string.Join(", ",group.Select(p=>PathOf(p.r.transform)).Distinct().Take(8)));
        }
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            lines.Add($"LIGHT {PathOf(light.transform)} enabled={light.enabled} type={light.type} intensity={light.intensity} colour={light.color} shadows={light.shadows} strength={light.shadowStrength}");
        foreach(var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            lines.Add($"VOLUME {v.name} priority={v.priority} profile={AssetDatabase.GetAssetPath(v.sharedProfile)}");
        File.WriteAllLines(Evidence+"/inspection.txt",lines);
        CaptureOverview("before");
        Debug.Log("STORYBOOK_INSPECTED: "+lines.Count+" finish/light entries.");
    }

    static Scene RequireScene()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/LivingRoom.unity")
            throw new InvalidOperationException("Open LivingRoom in Edit mode first.");
        return scene;
    }
    static Renderer[] Renderers(Scene scene) => Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
        .Where(r=>r.gameObject.scene==scene&&r.enabled&&r.gameObject.activeInHierarchy).ToArray();
    static string PathOf(Transform t) => t.parent?PathOf(t.parent)+"/"+t.name:t.name;

    public static void CaptureOverview(string suffix)
    {
        Directory.CreateDirectory(Evidence);
        var go=new GameObject("Disposable room palette preview");
        var c=go.AddComponent<Camera>();
        c.transform.SetPositionAndRotation(new Vector3(-.55f,3.25f,-2.85f),Quaternion.LookRotation(new Vector3(.25f,.60f,.70f)-new Vector3(-.55f,3.25f,-2.85f)));
        c.fieldOfView=69;c.nearClipPlane=.04f;c.farClipPlane=25;
        c.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var rt=RenderTexture.GetTemporary(1600,1000,24);var old=RenderTexture.active;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try {c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(Evidence+"/overview-"+suffix+".png",image.EncodeToPNG());}
        finally {RenderTexture.active=old;c.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);}
    }
}
