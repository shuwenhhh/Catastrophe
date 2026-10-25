using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Updates finishes in place: furniture transforms and gameplay colliders stay authored.
public static class LivingRoomBrightPalette
{
    private const string Folder = "Assets/Settings/LivingRoom/ReferenceMaterials";

    [MenuItem("Catastrophe/Reference Room/Apply Bright Cream and Oak")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before saving room finishes.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/LivingRoom.unity")
            throw new InvalidOperationException("Open LivingRoom first.");

        SetMaterial("Assets/Models/Tripo/Materials/Tripo_CatTree_0_0.mat",
            new Color(.94f, .865f, .74f), .12f);
        Material floor = SetMaterial(Folder + "/LightOakFloor.mat", new Color(.88f, .735f, .54f), .24f);
        SetMaterial(Folder + "/LightOakSeam.mat", new Color(.70f, .55f, .38f), .12f);
        floor.SetTexture("_BaseMap", CreateOakGrain());
        floor.SetTextureScale("_BaseMap", new Vector2(3f, 6f));

        GameObject floorRoot = GameObject.Find("Uniform Light Oak Floor");
        if (floorRoot == null) throw new InvalidOperationException("Continuous floor is missing.");
        foreach (Transform part in floorRoot.GetComponentsInChildren<Transform>())
        {
            if (!part.name.StartsWith("Oak seam")) continue;
            Vector3 scale = part.localScale;
            if (part.name.Contains("horizontal")) scale.z = .004f;
            else scale.x = .004f;
            scale.y = .002f;
            part.localScale = scale;
            Vector3 position = part.localPosition;
            position.y = .001f;
            part.localPosition = position;
        }
        RoomShellInstaller.EnsureInstalled();
        ApplyLightInPlace();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BRIGHT_CREAM_OAK_APPLIED: cream cat tree, warm-white walls, fine oak grain, neutral daylight. Layout and colliders preserved.");
    }

    private static Material SetMaterial(string path, Color color, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) throw new InvalidOperationException("Missing material: " + path);
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CreateOakGrain()
    {
        // Low-contrast periodic grain; it adds detail without drawing black stripes across the room.
        const int size = 512;
        Texture2D texture = new(size, size, TextureFormat.RGB24, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x * Mathf.PI * 2f / size;
            float v = y * Mathf.PI * 2f / size;
            float bend = .65f * Mathf.Sin(u) + .22f * Mathf.Sin(3f * u);
            float grain = .025f * Mathf.Sin(27f * v + bend) +
                          .012f * Mathf.Sin(63f * v + 2f * bend) + .01f * Mathf.Sin(5f * v);
            float value = .955f + grain;
            pixels[y * size + x] = new Color(value, value, value);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        string path = Folder + "/FineOakGrain.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.sRGBTexture = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void ApplyLightInPlace()
    {
        GameObject rig = GameObject.Find(LivingRoomLightingSetup.RigName);
        if (rig == null) throw new InvalidOperationException("Authored lighting rig is missing.");
        foreach (Light light in rig.GetComponentsInChildren<Light>(true))
        {
            light.color = light.type == LightType.Directional ? new Color(1f, .97f, .91f) : new Color(.94f, .97f, 1f);
            light.intensity = light.type == LightType.Directional ? 1.25f : 1.35f;
            light.shadowStrength = .55f;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.84f, .86f, .87f);
        RenderSettings.ambientEquatorColor = new Color(.76f, .75f, .71f);
        RenderSettings.ambientGroundColor = new Color(.55f, .51f, .45f);
        RenderSettings.ambientIntensity = 1f;
        Volume volume = rig.GetComponentInChildren<Volume>(true);
        if (volume == null || volume.sharedProfile == null)
            throw new InvalidOperationException("Room volume profile is missing.");
        VolumeProfile profile = volume.sharedProfile;
        profile.components.RemoveAll(c => c == null);
        ColorAdjustments color = Component<ColorAdjustments>(profile);
        color.postExposure.Override(.25f);
        color.contrast.Override(3f);
        color.colorFilter.Override(Color.white);
        color.saturation.Override(0f);
        WhiteBalance balance = Component<WhiteBalance>(profile);
        balance.temperature.Override(0f);
        balance.tint.Override(0f);
        Vignette vignette = Component<Vignette>(profile);
        vignette.intensity.Override(.07f);
        vignette.smoothness.Override(.34f);
        EditorUtility.SetDirty(color);
        EditorUtility.SetDirty(balance);
        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(profile);
    }

    private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        component.active = true;
        return component;
    }
}
