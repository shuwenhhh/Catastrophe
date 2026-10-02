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
    private const string CatPrefabPath = "Assets/Resources/Cat/CatSprite.prefab";
    private const string CatControllerPath = "Assets/Art/Cat/CatSprite.controller";
    private const string SmoothSpriteFolder = "Assets/Art/TuxedoKitten/GeneratedSmoothSprites";
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
    public static void Batch11() => Execute(11);
    public static void Batch12() => Execute(12);
    public static void Batch13() => Execute(13);
    public static void Batch14() => Execute(14);
    public static void Batch15() => Execute(15);
    public static void Batch16() => Execute(16);
    public static void Batch17() => Execute(17);
    public static void Batch18() => Execute(18);
    public static void Batch19() => Execute(19);
    public static void Batch20() => Execute(20);

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

    [MenuItem("Catastrophe/Run Final Delivery Batch 20")]
    public static void RunFinalDeliveryInEditor()
    {
        try
        {
            TripoBatchImporter.FixAllMaterials();
            for (int i = 1; i <= 19; i++) Run(i);
            Debug.Log("GAME_VALIDATION_BATCH_20_PASSED");
        }
        catch (Exception exception)
        {
            Debug.LogError($"GAME_VALIDATION_BATCH_20_FAILED: {exception.Message}");
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
            else if (number == 20)
            {
                TripoBatchImporter.FixAllMaterials();
                for (int i = 1; i <= 19; i++) Run(i);
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
            case 10: for (int i = 1; i <= 9; i++) Run(i); break;
            case 11: ValidateCoreLoop(); break;
            case 12: ValidateTargetSequence(); break;
            case 13: ValidateAlertAndHide(); break;
            case 14: ValidateResultsFlow(); break;
            case 15: ValidateStablePhysics(); break;
            case 16: ValidateAudioFeedback(); break;
            case 17: ValidateJuiceAndCutePass(); break;
            case 18: ValidateIndependentSceneArt(); break;
            case 19: ValidatePresentationAndCamera(); break;
            default: throw new ArgumentOutOfRangeException(nameof(number));
        }
    }

    private static void ValidateCoreLoop()
    {
        Require(typeof(GameManager).GetMethod("AddChaos", new[] { typeof(int), typeof(string) }) != null,
            "Scored destruction entry point is missing.");
        Require(typeof(Valuable).GetEvent("AnyDestroyed") != null, "Global Valuable destruction event is missing.");
        Require(typeof(NoiseManager).GetMethod("EmitNoise") != null, "Noise emission is missing.");
        OpenRoom();
        foreach (Valuable valuable in UnityEngine.Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None))
            Require(valuable.GetComponent<Rigidbody>() != null, $"{valuable.name} has Valuable but no Rigidbody.");
    }

    private static void ValidateTargetSequence()
    {
        Require(typeof(TargetManager).GetMethod("CompleteTarget") != null, "Target completion API is missing.");
        Require(typeof(TargetManager).GetEvent("OnTargetChanged") != null, "Target UI change event is missing.");
        string source = File.ReadAllText("Assets/Scripts/Core/TargetManager.cs");
        foreach (string target in new[] { "ipad", "cups", "fragile", "chaos100" })
            Require(source.Contains(target, StringComparison.Ordinal), $"Target queue is missing {target}.");
        Require(source.Contains("IsTablet(itemName)", StringComparison.Ordinal) &&
                source.Contains("\"tablet\"", StringComparison.Ordinal),
            "The first objective must accept the imported Tablet item name, not only iPad.");
    }

    private static void ValidateAlertAndHide()
    {
        Require(typeof(OwnerAlert).GetMethod("TriggerAlert") != null, "Owner alert trigger is missing.");
        Require(typeof(CatController).GetProperty("IsHidden") != null, "Cat hidden state is missing.");
        Require(typeof(CatController).GetMethod("EnterHide") != null && typeof(CatController).GetMethod("ExitHide") != null,
            "Hide enter/exit flow is incomplete.");
        OpenRoom();
        int authoredHideSpots = UnityEngine.Object.FindObjectsByType<HideSpot>(FindObjectsSortMode.None).Length;
        bool runtimeHideSpots = typeof(GameBootstrap).GetMethod("CreateHideSpots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) != null;
        Require(authoredHideSpots >= 3 || runtimeHideSpots,
            "LivingRoom needs all three authored or bootstrap-generated hide spots.");
    }

    private static void ValidateResultsFlow()
    {
        Require(typeof(GameManager).GetMethod("OnLevelComplete") != null, "Level completion API is missing.");
        Require(typeof(GameManager).GetMethod("OnLevelFailed") != null, "Failure result API is missing.");
        Require(typeof(GameManager).GetProperty("Receipt") != null, "Receipt data is missing.");
        Require(typeof(GameManager).GetProperty("HighScore") != null && typeof(GameManager).GetProperty("Grade") != null,
            "Persistent high score or S/A/B rating is missing.");
        foreach (string method in new[] { "RestartGame", "ReturnToMainMenu", "ResumeGame" })
            Require(typeof(GameFlowUI).GetMethod(method) != null, $"Result/menu action is missing: {method}.");
    }

    private static void ValidateStablePhysics()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Require(typeof(Valuable).GetField("maximumSpeed", flags) != null, "Valuable speed clamp is missing.");
        Require(typeof(Valuable).GetField("destructionArmed", flags) != null,
            "Spawn-settling guard is missing; startup impacts may score or play audio.");
        OpenRoom();
        Valuable[] valuables = UnityEngine.Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None);
        foreach (Valuable valuable in valuables)
        {
            Rigidbody body = valuable.GetComponent<Rigidbody>();
            Require(body != null && body.mass >= 1f && body.mass <= 3f,
                $"{valuable.name} nudge physics is not delivery-safe.");
        }
        for (int left = 0; left < valuables.Length; left++)
        for (int right = left + 1; right < valuables.Length; right++)
        {
            float separation = Vector3.Distance(valuables[left].transform.position,
                valuables[right].transform.position);
            Require(separation >= 0.02f,
                $"Duplicate overlapping valuables at spawn: {valuables[left].name} and {valuables[right].name}.");
        }
    }

    private static void ValidateAudioFeedback()
    {
        foreach (string method in new[] { "PlayItemThud", "PlayFragileBreak", "PlayPawPush", "PlayOwnerAlert", "PlayHideSuccess", "PlayFootstep", "PlayLanding" })
            Require(typeof(AudioManager).GetMethod(method) != null, $"Audio event is missing: {method}.");
        Require(typeof(AudioManager).GetProperty("I") != null, "AudioManager singleton is missing.");
    }

    private static void ValidateJuiceAndCutePass()
    {
        Require(typeof(ChaosJuiceUI).GetMethod("OnGUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null,
            "Destruction/noise juice UI is missing.");
        Require(File.Exists($"{SmoothSpriteFolder}/Smooth_Walk_Side_6Frames.png") &&
                File.Exists($"{SmoothSpriteFolder}/Smooth_Jump_Side_6Frames.png") &&
                File.Exists($"{SmoothSpriteFolder}/Smooth_GentleLeftPawPush_4Frames.png"),
            "Smooth tuxedo-kitten movement sheets are missing.");
    }

    private static void ValidateIndependentSceneArt()
    {
        OpenRoom();
        GameObject dressing = GameObject.Find("Tripo Art Pass (editable)") ?? RequireObject("V3.4 Room Dressing");
        Require(dressing.transform.childCount >= 4, "Room dressing groups are incomplete.");
        Valuable[] valuables = dressing.GetComponentsInChildren<Valuable>(true);
        Require(valuables.Length >= 10, "The lived-in room needs at least ten independent valuables.");
        foreach (Valuable valuable in valuables)
            Require(valuable.transform.parent != null && valuable.GetComponent<Rigidbody>() != null,
                $"{valuable.name} must remain an independent editable Rigidbody root.");

        string prefabFolder = "Assets/Models/Tripo/Prefabs";
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        Require(prefabGuids.Length == 16, $"Expected 16 Tripo prefabs, found {prefabGuids.Length}.");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, $"Could not load Tripo prefab: {path}");
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                Require(material != null, $"Missing material in {path}.");
                Require(material.shader != null &&
                        (material.shader.name == "Universal Render Pipeline/Lit" ||
                         material.shader.name == "Catastrophe/Tripo Vertex Color Lit"),
                    $"Non-URP Tripo material in {path}: {material.shader?.name ?? "missing shader"}.");
            }
        }
    }

    private static void ValidatePresentationAndCamera()
    {
        OpenRoom();
        bool authoredCamera = Camera.main != null && Camera.main.GetComponent<ThirdPersonCamera>() != null;
        bool runtimeCamera = typeof(GameBootstrap).GetMethod("CreateCameraAndLight",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) != null;
        Require(authoredCamera || runtimeCamera, "Low-angle follow camera is missing.");
        const System.Reflection.BindingFlags cameraFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Require(typeof(ThirdPersonCamera).GetField("yawLimit", cameraFlags) != null &&
                typeof(ThirdPersonCamera).GetField("distanceLimits", cameraFlags) != null,
            "Limited drag/zoom camera controls are missing.");
        Require(typeof(ThirdPersonCamera).GetField("cameraBoundsX", cameraFlags) != null &&
                typeof(ThirdPersonCamera).GetField("cameraBoundsZ", cameraFlags) != null &&
                typeof(ThirdPersonCamera).GetField("stablePivotHeight", cameraFlags) != null,
            "Camera has no room-position clamp or stable XZ-only collision pivot.");

        GameObject cameraProbe = new GameObject("Camera Bounds Probe");
        ThirdPersonCamera cameraController = cameraProbe.AddComponent<ThirdPersonCamera>();
        GameObject targetProbe = new GameObject("Camera Target Probe");
        targetProbe.transform.position = new Vector3(20f, 3f, 20f);
        cameraController.Initialize(targetProbe.transform);
        var desiredMethod = typeof(ThirdPersonCamera).GetMethod("GetDesiredPosition", cameraFlags);
        var originMethod = typeof(ThirdPersonCamera).GetMethod("GetRayOrigin", cameraFlags);
        Require(desiredMethod != null && originMethod != null, "Camera composition helpers are missing.");
        Vector3 desired = (Vector3)desiredMethod.Invoke(cameraController, null);
        Vector3 origin = (Vector3)originMethod.Invoke(cameraController, null);
        Require(desired.x >= -4.72f && desired.x <= 4.72f && desired.z >= -3.32f && desired.z <= 3.32f,
            $"Camera can leave the four-wall room: desired={desired}.");
        Require(origin.y < 1f,
            $"Camera collision pivot incorrectly follows jump height: origin.y={origin.y:F2}.");
        UnityEngine.Object.DestroyImmediate(cameraProbe);
        UnityEngine.Object.DestroyImmediate(targetProbe);
        Require(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l => l.type == LightType.Directional && l.shadows != LightShadows.None),
            "Warm directional light with shadows is missing.");
        Require(GameObject.Find("V3.4 Lighting Rig (editable)") != null, "LivingRoom lighting rig is missing.");
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
        Require(prefab.GetComponentInChildren<SpriteRenderer>() != null, "Cat prefab has no SpriteRenderer.");
        Require(prefab.GetComponentInChildren<BillboardSprite>() != null,
            "Cat sprite must stay camera-facing in the 3D room.");
    }

    private static void ValidateAnimations()
    {
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CatControllerPath);
        Require(controller != null, "Cat sprite Animator Controller is missing.");
        AnimationClip[] clips = controller.animationClips;
        string[] required = {
            "Cat_Idle", "Cat_Idle_Front", "Cat_Idle_Back",
            "Cat_Walk", "Cat_Walk_Front", "Cat_Walk_Back",
            "Cat_Jump_Side", "Cat_Jump_Front", "Cat_Jump_Back", "Cat_Push_Side"
        };
        foreach (string name in required)
            Require(clips.Any(c => c.name == name), $"Animation {name} is missing.");
        Require(clips.All(c => c.length > 0.05f), "One or more animation clips are empty.");

        string[] sheets = {
            "Smooth_Walk_Side_6Frames.png", "Smooth_Walk_TowardCamera_6Frames.png",
            "Smooth_Walk_Away_Back_6Frames.png", "Smooth_Jump_Side_6Frames.png",
            "Smooth_Jump_TowardCamera_6Frames.png", "Smooth_Jump_Away_Back_6Frames.png",
            "Smooth_GentleLeftPawPush_4Frames.png"
        };
        foreach (string sheet in sheets)
        {
            int expected = sheet.Contains("4Frames", StringComparison.Ordinal) ? 4 : 6;
            int actual = AssetDatabase.LoadAllAssetsAtPath($"{SmoothSpriteFolder}/{sheet}")
                .OfType<Sprite>().Count();
            Require(actual == expected, $"{sheet} has {actual} frames; expected {expected}.");
        }
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
        GameObject gameplayFloor = RequireObject("Gameplay Floor Collider - full room");
        BoxCollider safety = gameplayFloor.GetComponent<BoxCollider>();
        Require(safety != null && safety.bounds.size.x >= 10.4f && safety.bounds.size.z >= 7.6f,
            $"Continuous gameplay floor is missing under part of the room: {safety?.bounds.size.ToString() ?? "no collider"}.");
        Require(Mathf.Abs(safety.bounds.max.y) <= 0.015f,
            $"Gameplay floor top must align to Y=0, actual={safety.bounds.max.y:F3}.");
        RequireObject("Back Wall");
        RequireObject("Left Wall");

        GameObject shell = RequireObject("Four Wall Room Shell (editable)");
        foreach (Renderer renderer in shell.GetComponentsInChildren<Renderer>(true))
        foreach (Material material in renderer.sharedMaterials)
            Require(material != null && material.shader != null &&
                    !material.shader.name.Contains("InternalErrorShader", StringComparison.OrdinalIgnoreCase),
                $"Room shell has a missing/pink material on {renderer.name}.");
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
        foreach (string groundedName in new[] { "Sofa", "Table", "Chair 01", "Chair 02", "Chair 03" })
        {
            Bounds grounded = BoundsOf(RequireObject(groundedName));
            Require(grounded.min.y >= -0.025f && grounded.min.y <= 0.035f,
                $"{groundedName} is sunk below or floating above the floor: min.y={grounded.min.y:F3}.");
        }
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
        Require(cup.size.y >= 0.08f && cup.size.y <= 0.18f,
            $"Cup must stay near the 10 cm import target without becoming toy-sized or oversized: {cup.size}.");

        Vector3 laptopToCup = RequireObject("Cup").transform.position -
                              RequireObject("MacBook").transform.position;
        laptopToCup.y = 0f;
        Require(laptopToCup.magnitude <= 0.55f,
            "Cup must stay close to the MacBook to communicate the spill opportunity.");
        Require(tablet.min.z - table.min.z <= 0.22f,
            $"iPad must sit close to the front edge for the first push tutorial: table.min.z={table.min.z:F2}, tablet.min.z={tablet.min.z:F2}.");
        Require(RequireObject("Chair 01").transform.position.z <= -1.82f,
            "Chair 01 must be pulled out as a readable jump step.");
        foreach (string gameplayItem in new[] { "MacBook", "iPad", "Cup" })
        {
            GameObject item = RequireObject(gameplayItem);
            Require(item.GetComponent<Rigidbody>() != null && item.GetComponent<ScoreItem>() != null,
                $"{gameplayItem} must remain pushable and scoreable.");
        }
        Transform seatCollider = RequireObject("Chair 01").transform.Find("Seat Collider");
        if (seatCollider != null)
        {
            float seatTop = seatCollider.GetComponent<BoxCollider>().bounds.max.y;
            Require(table.max.y - seatTop >= 0.20f && table.max.y - seatTop <= 0.38f,
                $"Chair-to-table jump step is unclear: seat={seatTop:F2}, table={table.max.y:F2}.");
        }
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

        bool tripoRoom = GameObject.Find("Tripo Art Pass (editable)") != null;
        if (!tripoRoom)
        {
            Require(RequireObject("Chair 01").GetComponent<BoxCollider>() == null,
                "Chair must not use a single full-bounds box that lets the cat stand in empty space near its back.");
            Collider[] chairColliders = RequireObject("Chair 01").GetComponentsInChildren<Collider>(true);
            Require(chairColliders.All(c => c.bounds.max.y < 0.55f),
                "Chair backrest must not be a landable surface.");
        }
    }

    private static void ValidateBootstrapAndCamera()
    {
        Require(typeof(GameBootstrap).GetMethod("StartGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) != null,
            "Runtime bootstrap entry point is missing.");
        Require(typeof(ThirdPersonCamera).GetMethod("Initialize") != null, "Third-person camera initialization is missing.");
        RuntimeAnimatorController catController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            CatControllerPath);
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
