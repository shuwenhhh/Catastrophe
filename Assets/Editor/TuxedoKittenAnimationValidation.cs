#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TuxedoKittenAnimationValidation
{
    private const string FbxPath = "Assets/Art/TuxedoKitten/TuxedoKitten_Rigged.fbx";
    private const string PrefabPath = "Assets/Resources/TuxedoKitten/TuxedoKitten.prefab";

    public static void Run()
    {
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Tuxedo kitten prefab is missing.");

            Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToDictionary(clip => ShortName(clip.name), clip => clip);

            string[] required = { "Cat_Idle", "Cat_Walk", "Cat_Push_Right", "Cat_Jump" };
            foreach (string name in required)
            {
                if (!clips.ContainsKey(name))
                    throw new InvalidOperationException($"Missing animation clip: {name}");
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cat = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            cat.name = "Tuxedo Kitten Animation Validation";
            Animator animator = cat.GetComponentInChildren<Animator>();
            if (animator == null)
                throw new InvalidOperationException("The cat prefab has no Animator.");
            GameObject animationRoot = animator.gameObject;

            Transform rightPaw = FindDeep(cat.transform, "Front_R_Lower");
            Transform leftPaw = FindDeep(cat.transform, "Front_L_Lower");
            Transform root = FindDeep(cat.transform, "Root");
            if (rightPaw == null || leftPaw == null || root == null)
                throw new InvalidOperationException("Required rig bones were not found.");

            clips["Cat_Idle"].SampleAnimation(animationRoot, 0f);
            Vector3 bindRight = rightPaw.position;
            Vector3 bindLeft = leftPaw.position;
            Vector3 bindRoot = root.position;

            clips["Cat_Push_Right"].SampleAnimation(animationRoot, clips["Cat_Push_Right"].length * 0.48f);
            float rightPushTravel = Vector3.Distance(bindRight, rightPaw.position);
            float leftPushTravel = Vector3.Distance(bindLeft, leftPaw.position);

            clips["Cat_Idle"].SampleAnimation(animationRoot, 0f);
            Vector3 walkStart = rightPaw.position;
            clips["Cat_Walk"].SampleAnimation(animationRoot, clips["Cat_Walk"].length * 0.25f);
            float walkPawTravel = Vector3.Distance(walkStart, rightPaw.position);

            clips["Cat_Idle"].SampleAnimation(animationRoot, 0f);
            Vector3 jumpStart = root.position;
            clips["Cat_Jump"].SampleAnimation(animationRoot, clips["Cat_Jump"].length * 0.5f);
            float jumpTravel = Vector3.Distance(jumpStart, root.position);

            if (rightPushTravel < 0.015f)
                throw new InvalidOperationException($"Right-paw push travel is too small: {rightPushTravel:F4}");
            if (walkPawTravel < 0.015f)
                throw new InvalidOperationException($"Walk paw travel is too small: {walkPawTravel:F4}");
            if (jumpTravel < 0.015f)
                throw new InvalidOperationException($"Jump root travel is too small: {jumpTravel:F4}");
            if (rightPushTravel <= leftPushTravel)
                throw new InvalidOperationException("Push animation does not favor the right front paw.");

            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/UnityPreviews"));
            Directory.CreateDirectory(outputDirectory);

            RenderPreview(cat, animationRoot, clips["Cat_Idle"], 0f, Path.Combine(outputDirectory, "01_Idle.png"));
            RenderPreview(cat, animationRoot, clips["Cat_Walk"], clips["Cat_Walk"].length * 0.25f, Path.Combine(outputDirectory, "02_Walk.png"));
            RenderPreview(cat, animationRoot, clips["Cat_Push_Right"], clips["Cat_Push_Right"].length * 0.48f, Path.Combine(outputDirectory, "03_Push_Right.png"));
            RenderPreview(cat, animationRoot, clips["Cat_Jump"], clips["Cat_Jump"].length * 0.5f, Path.Combine(outputDirectory, "04_Jump.png"));

            Debug.Log(
                $"TUXEDO_KITTEN_ANIMATION_VALIDATION_PASSED " +
                $"rightPush={rightPushTravel:F4} leftPush={leftPushTravel:F4} " +
                $"walk={walkPawTravel:F4} jump={jumpTravel:F4} previews={outputDirectory}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void RenderPreview(GameObject cat, GameObject animationRoot, AnimationClip clip, float time, string outputPath)
    {
        clip.SampleAnimation(animationRoot, time);

        foreach (GameObject temporary in GameObject.FindGameObjectsWithTag("EditorOnly"))
            UnityEngine.Object.DestroyImmediate(temporary);

        Bounds bounds = CalculateBounds(cat);
        float groundY = bounds.min.y - 0.01f;

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.tag = "EditorOnly";
        ground.name = "Preview Ground";
        ground.transform.position = new Vector3(bounds.center.x, groundY, bounds.center.z);
        ground.transform.localScale = Vector3.one * Mathf.Max(0.5f, bounds.size.magnitude * 0.22f);
        UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
        Material groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMaterial.color = new Color(0.82f, 0.86f, 0.91f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        GameObject key = CreateLight("Key", new Vector3(45f, -30f, 0f), 0.75f);
        GameObject fill = CreateLight("Fill", new Vector3(25f, 145f, 0f), 0.35f);
        GameObject rim = CreateLight("Rim", new Vector3(55f, 45f, 180f), 0.45f);
        key.tag = fill.tag = rim.tag = "EditorOnly";

        GameObject cameraObject = new GameObject("Preview Camera") { tag = "EditorOnly" };
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.92f, 0.95f, 0.98f);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.y * 1.35f, bounds.extents.magnitude * 0.75f);
        camera.transform.position = bounds.center + new Vector3(-bounds.size.x * 1.35f, bounds.size.y * 0.42f, bounds.size.z * 1.8f);
        camera.transform.LookAt(bounds.center + Vector3.up * bounds.size.y * 0.03f);

        const int size = 768;
        RenderTexture target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(size, size, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        image.Apply();
        File.WriteAllBytes(outputPath, image.EncodeToPNG());

        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
    }

    private static GameObject CreateLight(string name, Vector3 eulerAngles, float intensity)
    {
        GameObject lightObject = new GameObject(name);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = name == "Fill" ? new Color(0.72f, 0.82f, 1f) : new Color(1f, 0.92f, 0.82f);
        lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
        return lightObject;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            throw new InvalidOperationException("The cat prefab has no renderer.");

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform result = FindDeep(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    private static string ShortName(string name)
    {
        int separator = name.LastIndexOf('|');
        return separator >= 0 ? name.Substring(separator + 1) : name;
    }
}
#endif
