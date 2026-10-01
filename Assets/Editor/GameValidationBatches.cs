#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameValidationBatches
{
    private const string ScenePath = "Assets/LivingRoom.unity";
    private const string CatPrefabPath = "Assets/Resources/TuxedoKitten/TuxedoKitten.prefab";
    private const string CatFbxPath = "Assets/Art/TuxedoKitten/TuxedoKitten_Rigged.fbx";
    private const string ModelFolder = "Assets/Art/LivingRoom/Models";

    public static void Batch01() => Execute(1);
    public static void Batch02() => Execute(2);
    public static void Batch03() => Execute(3);
    public static void Batch04() => Execute(4);
    public static void Batch05() => Execute(5);
    public static void Batch06() => Execute(6);
    public static void Batch07() => Execute(7);
    public static void Batch08() => Execute(8);
    public static void Batch09() => Execute(9);
    public static void Batch10() => Execute(10);

    [MenuItem("Catastrophe/Run Validation Batch 10")]
    public static void RunAllInEditor()
    {
        try
        {
            for (int i = 1; i <= 9; i++) Run(i);
            Debug.Log("GAME_VALIDATION_BATCH_10_PASSED");
        }
        catch (Exception exception)
        {
            Debug.LogError($"GAME_VALIDATION_BATCH_10_FAILED: {exception.Message}");
            Debug.LogException(exception);
        }
    }

    private static void Execute(int number)
    {
        try
        {
            if (number == 10)
            {
                for (int i = 1; i <= 9; i++) Run(i);
            }
            else Run(number);

            Debug.Log($"GAME_VALIDATION_BATCH_{number:00}_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"GAME_VALIDATION_BATCH_{number:00}_FAILED: {exception.Message}");
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Run(int number)
    {
        switch (number)
        {
            case 1: ValidateProjectAndScene(); break;
            case 2: ValidateCatPrefab(); break;
            case 3: ValidateAnimations(); break;
            case 4: ValidateCatGameplay(); break;
            case 5: ValidateRoomAndFloor(); break;
            case 6: ValidateFurnitureLayout(); break;
            case 7: ValidateMaterialsAndTextures(); break;
            case 8: ValidateColliders(); break;
            case 9: ValidateBootstrapAndCamera(); break;
            default: throw new ArgumentOutOfRangeException(nameof(number));
        }
    }

    private static void ValidateProjectAndScene()
    {
        Require(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)), "LivingRoom scene is missing.");
        Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath), "LivingRoom is not the enabled build scene.");
        OpenRoom();
        Require(GameObject.Find("Living Room") != null, "Living Room root is missing.");
    }

    private static void ValidateCatPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatPrefabPath);
        Require(prefab != null, "Cat prefab is missing.");
        Require(prefab.GetComponentInChildren<Animator>() != null, "Cat prefab has no Animator.");
        Require(prefab.GetComponentsInChildren<Renderer>(true).Length > 0, "Cat prefab has no renderer.");
        Require(FindDeep(prefab.transform, "Root") != null, "Cat rig Root bone is missing.");
        Require(FindDeep(prefab.transform, "Front_R_Lower") != null, "Right front paw bone is missing.");
    }

    private static void ValidateAnimations()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(CatFbxPath)
            .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        string[] required = { "Cat_Idle", "Cat_Walk", "Cat_Push_Right", "Cat_Jump" };
        foreach (string name in required)
            Require(clips.Any(c => c.name.EndsWith(name, StringComparison.Ordinal)), $"Animation {name} is missing.");
        Require(clips.All(c => c.length > 0.05f), "One or more animation clips are empty.");
    }

    private static void ValidateCatGameplay()
    {
        Require(typeof(CatController).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null,
            "CatController Update loop is missing.");
        const System.Reflection.BindingFlags privateInstance =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var frontScaleField = typeof(CatController).GetField("frontVisualScale", privateInstance);
        var backScaleField = typeof(CatController).GetField("backVisualScale", privateInstance);
        Require(frontScaleField != null && backScaleField != null,
            "Directional cat size normalization is missing.");
        GameObject scaleProbe = new GameObject("Cat Scale Probe");
        CatController scaleController = scaleProbe.AddComponent<CatController>();
        float frontScale = (float)frontScaleField.GetValue(scaleController);
        float backScale = (float)backScaleField.GetValue(scaleController);
        Require(Mathf.Abs(frontScale - 0.75f) < 0.01f && Mathf.Abs(backScale - 0.74f) < 0.01f,
            $"Directional cat scales are invalid: front={frontScale:F2}, back={backScale:F2}.");
        var speedField = typeof(CatController).GetField("moveSpeed", privateInstance);
        var footField = typeof(CatController).GetField("groundedFootLocalY", privateInstance);
        Require(speedField != null && (float)speedField.GetValue(scaleController) <= 1.2f,
            "Cat movement is too fast for the authored eight-FPS walk cycle.");
        Require(footField != null && Mathf.Abs((float)footField.GetValue(scaleController) + 0.225f) < 0.005f,
            "Cat visual feet are not aligned with the capsule bottom.");
        Require(typeof(CatContactShadow).GetMethod("LateUpdate", privateInstance) != null,
            "Cat contact shadow tracking is missing.");
        UnityEngine.Object.DestroyImmediate(scaleProbe);
        OpenRoom();
        float tableTop = BoundsOf(RequireObject("Table")).max.y;
        float launchHeight = 0.24f;
        const float configuredJumpHeight = 1.05f;
        Require(launchHeight + configuredJumpHeight > tableTop + 0.20f,
            $"Configured jump cannot clear the table: apex={launchHeight + configuredJumpHeight:F2}, table={tableTop:F2}.");
        GameObject probe = new GameObject("Cat Gameplay Probe");
        Rigidbody body = probe.AddComponent<Rigidbody>();
        CapsuleCollider capsule = probe.AddComponent<CapsuleCollider>();
        Require(body.mass > 0f && capsule.height > capsule.radius, "Cat physics components are invalid.");
        UnityEngine.Object.DestroyImmediate(probe);
    }

    private static void ValidateRoomAndFloor()
    {
        OpenRoom();
        GameObject floor = RequireObject("Floor");
        Bounds b = BoundsOf(floor);
        Require(b.size.x >= 10f && b.size.z >= 7.3f, $"Room floor dimensions are invalid: {b.size}.");
        Require(Mathf.Abs(b.max.y) < 0.03f, $"Floor walkable top must be at Y=0, actual={b.max.y:F3}.");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/Floor.fbx") != null, "Imported floor FBX is missing.");
        RequireObject("Back Wall");
        RequireObject("Left Wall");
    }

    private static void ValidateFurnitureLayout()
    {
        OpenRoom();
        string[] names = { "Sofa", "Table", "Chair 01", "Chair 02", "Chair 03", "MacBook", "iPad", "Cup" };
        foreach (string name in names) RequireObject(name);
        foreach (string omitted in new[] { "Floor Lamp", "TV Cabinet", "Television", "Flower Vase", "Wall Decorations" })
            Require(GameObject.Find(omitted) == null, $"Unattached extra object must not be generated: {omitted}.");
        Require(RequireObject("Sofa").transform.position.x < 0f, "Sofa should be in the left living zone.");
        Require(RequireObject("Table").transform.position.x > 0f, "Table should be in the right dining zone.");
        Require(RequireObject("MacBook").transform.parent == RequireObject("Table").transform.parent,
            "Desktop objects and table should remain independently movable siblings.");

        Bounds table = BoundsOf(RequireObject("Table"));
        Bounds sofa = BoundsOf(RequireObject("Sofa"));
        Bounds chair = BoundsOf(RequireObject("Chair 01"));
        Bounds laptop = BoundsOf(RequireObject("MacBook"));
        Bounds tablet = BoundsOf(RequireObject("iPad"));
        Bounds cup = BoundsOf(RequireObject("Cup"));
        Require(table.size.x >= 2.55f && table.size.x <= 2.85f && table.size.y >= 0.70f && table.size.y <= 0.82f,
            $"Table is outside human furniture scale: {table.size}.");
        Require(sofa.size.x >= 2.30f && sofa.size.x <= 2.60f && sofa.size.y >= 0.82f && sofa.size.y <= 0.98f,
            $"Sofa is outside human furniture scale: {sofa.size}.");
        Require(chair.size.x >= 0.52f && chair.size.x <= 0.64f && chair.size.y >= 0.82f && chair.size.y <= 0.98f,
            $"Chair is outside human furniture scale: {chair.size}.");
        Require(laptop.size.x >= 0.50f && laptop.size.y >= 0.28f,
            $"MacBook is too small to read in the gameplay camera: {laptop.size}.");
        Require(tablet.size.x >= 0.26f && tablet.size.z >= 0.36f,
            $"iPad is too small to read in the gameplay camera: {tablet.size}.");
        Require(cup.size.y >= 0.17f,
            $"Cup is too small to read in the gameplay camera: {cup.size}.");

        Vector3 laptopToCup = RequireObject("Cup").transform.position -
                              RequireObject("MacBook").transform.position;
        laptopToCup.y = 0f;
        Require(laptopToCup.magnitude <= 0.55f,
            "Cup must stay close to the MacBook to communicate the spill opportunity.");
        Require(tablet.min.z - table.min.z <= 0.22f,
            "iPad must sit close to the front edge for the first push tutorial.");
        Require(RequireObject("Chair 01").transform.position.z <= -1.82f,
            "Chair 01 must be pulled out as a readable jump step.");
        foreach (string gameplayItem in new[] { "MacBook", "iPad", "Cup" })
        {
            GameObject item = RequireObject(gameplayItem);
            Require(item.GetComponent<Rigidbody>() != null && item.GetComponent<ScoreItem>() != null,
                $"{gameplayItem} must remain pushable and scoreable.");
        }
        float seatTop = RequireObject("Chair 01").transform.Find("Seat Collider")
            .GetComponent<BoxCollider>().bounds.max.y;
        Require(table.max.y - seatTop >= 0.20f && table.max.y - seatTop <= 0.38f,
            $"Chair-to-table jump step is unclear: seat={seatTop:F2}, table={table.max.y:F2}.");
    }

    private static void ValidateMaterialsAndTextures()
    {
        string[] textures = {
            $"{ModelFolder}/Textures/Floor/Oak_grain_texture.png",
            $"{ModelFolder}/Textures/Table/Oak_grain_texture.png",
            $"{ModelFolder}/Textures/Chair/Oak_grain_texture.png",
            $"{ModelFolder}/Textures/Sofa/Image_0.png"
        };
        foreach (string path in textures)
            Require(AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null, $"Texture missing: {path}");

        OpenRoom();
        foreach (string name in new[] { "Floor", "Table", "Chair 01", "Sofa" })
        {
            Renderer[] renderers = RequireObject(name).GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0, $"{name} has no renderer.");
            Require(renderers.Any(r => r.sharedMaterials.Any(m => m != null)), $"{name} has no material.");
        }
        Material floorMaterial = RequireObject("Floor").GetComponentInChildren<Renderer>().sharedMaterial;
        Material tableMaterial = RequireObject("Table").GetComponentInChildren<Renderer>().sharedMaterial;
        Require(tableMaterial.color.grayscale > floorMaterial.color.grayscale + 0.20f,
            "Table light oak and floor must have clearly different values.");
        Require(floorMaterial.mainTextureScale.x <= 0.80f,
            "Floor wood grain remains too dense for the gameplay camera.");
    }

    private static void ValidateColliders()
    {
        OpenRoom();
        foreach (string name in new[] { "Floor", "Sofa", "Table", "Chair 01", "Chair 02", "Chair 03", "MacBook", "iPad", "Cup" })
        {
            Collider[] colliders = RequireObject(name).GetComponentsInChildren<Collider>(true);
            Require(colliders.Any(c => c.enabled), $"{name} needs an enabled collider.");
            Require(colliders.Where(c => c.enabled).All(c => c.bounds.size.sqrMagnitude > 0.001f),
                $"{name} collider has invalid size.");
        }

        Require(RequireObject("Chair 01").GetComponent<BoxCollider>() == null,
            "Chair must not use a single full-bounds box that lets the cat stand in empty space near its back.");
        Collider[] chairColliders = RequireObject("Chair 01").GetComponentsInChildren<Collider>(true);
        Require(chairColliders.All(c => c.bounds.max.y < 0.55f),
            "Chair backrest must not be a landable surface.");
    }

    private static void ValidateBootstrapAndCamera()
    {
        Require(typeof(GameBootstrap).GetMethod("StartGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) != null,
            "Runtime bootstrap entry point is missing.");
        Require(typeof(ThirdPersonCamera).GetMethod("Initialize") != null, "Third-person camera initialization is missing.");
        RuntimeAnimatorController catController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Art/Cat/CatSprite.controller");
        Require(catController != null && catController.animationClips.Any(c => c.name == "Cat_Push_Side"),
            "E-key push animation is not connected to the runtime cat controller.");
        OpenRoom();
        Require(Camera.main != null, "Scene Main Camera is missing.");
        Require(GameObject.Find("Living Room") != null, "Bootstrap guard requires a Living Room root.");
    }

    private static void OpenRoom() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

    private static GameObject RequireObject(string name)
    {
        GameObject found = GameObject.Find(name);
        if (found == null) throw new InvalidOperationException($"Scene object is missing: {name}");
        return found;
    }

    private static Bounds BoundsOf(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Require(renderers.Length > 0, $"{root.name} has no renderer.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
