using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs the v3.4 dusk lighting as independent, editable scene objects.
/// Re-running this command only replaces the lighting rig, never the room dressing.
/// </summary>
public static class LivingRoomLightingSetup
{
    public const string RigName = "V3.4 Lighting Rig (editable)";
    private const string ScenePath = "Assets/LivingRoom.unity";
    private const string ProfileFolder = "Assets/Settings/LivingRoom";
    private const string ProfilePath = ProfileFolder + "/LivingRoom_Dusk_Volume.asset";

    [MenuItem("Catastrophe/Install Living Room Dusk Lighting")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject oldRig = GameObject.Find(RigName);
        if (oldRig != null)
            Object.DestroyImmediate(oldRig);

        GameObject rig = new(RigName);
        CreateDirectional(rig.transform);
        CreateCoolFill(rig.transform);
        CreateVolume(rig.transform);
        ConfigureEnvironment();
        EnableCameraPostProcessing();

        Selection.activeGameObject = rig;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("LivingRoom dusk lighting installed: warm key, cool fill and global volume are independently editable.");
    }

    private static void CreateDirectional(Transform parent)
    {
        GameObject go = new("Window Dusk Key - warm directional");
        go.transform.SetParent(parent, false);
        // The window is on the back wall; this angle reads as low, warm light entering it.
        go.transform.rotation = Quaternion.Euler(38f, 154f, 0f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1.00f, 0.72f, 0.48f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.72f;
        light.shadowBias = 0.045f;
        light.shadowNormalBias = 0.35f;
        light.renderMode = LightRenderMode.ForcePixel;
        RenderSettings.sun = light;
    }

    private static void CreateCoolFill(Transform parent)
    {
        GameObject go = new("Room Fill - cool soft point");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(-2.6f, 2.35f, -1.35f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.53f, 0.68f, 0.90f);
        light.intensity = 1.05f;
        light.range = 7.5f;
        // The key owns the readable shadow; shadowless fill prevents doubled shadows and dead black areas.
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    private static void CreateVolume(Transform parent)
    {
        EnsureFolder(ProfileFolder);
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.active = true;
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.34f);
        vignette.rounded.Override(true);

        WhiteBalance balance = GetOrAdd<WhiteBalance>(profile);
        balance.active = true;
        balance.temperature.Override(11f);
        balance.tint.Override(2f);

        ColorAdjustments colour = GetOrAdd<ColorAdjustments>(profile);
        colour.active = true;
        colour.postExposure.Override(-0.08f);
        colour.contrast.Override(7f);
        colour.saturation.Override(3f);
        colour.colorFilter.Override(new Color(1.0f, 0.97f, 0.92f));
        EditorUtility.SetDirty(profile);

        GameObject go = new("Global Volume - subtle warm dusk");
        go.transform.SetParent(parent, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
            component = profile.Add<T>(true);
        return component;
    }

    private static void ConfigureEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.31f, 0.38f, 0.51f);
        RenderSettings.ambientEquatorColor = new Color(0.20f, 0.25f, 0.34f);
        RenderSettings.ambientGroundColor = new Color(0.09f, 0.10f, 0.14f);
        RenderSettings.ambientIntensity = 0.82f;
        RenderSettings.subtractiveShadowColor = new Color(0.22f, 0.28f, 0.40f);
        RenderSettings.reflectionIntensity = 0.82f;
    }

    private static void EnableCameraPostProcessing()
    {
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
