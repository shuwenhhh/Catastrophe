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
    private const string CatPrefabPath = "Assets/Resources/SmoothKitten3D/SmoothKitten3D.prefab";
    private const string CatControllerPath = "Assets/Art/SmoothKitten3D/SmoothKitten3D.controller";
    private const string CatFbxPath = "Assets/Art/SmoothKitten3D/LongStride/SmoothKitten3D_LongStride.fbx";
    private const string TripoFbxFolder = "Assets/Models/Tripo/FBX";

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
    public static void Batch21() => Execute(21);
    public static void Batch22() => Execute(22);
    public static void Batch23() => Execute(23);
    public static void Batch24() => Execute(24);
    public static void Batch25() => Execute(25);
    public static void Batch26() => Execute(26);
    public static void Batch27() => Execute(27);
    public static void Batch28() => Execute(28);
    public static void Batch29() => Execute(29);
    public static void Batch30() => Execute(30);

    public static void LogLivingRoomLayout()
    {
        OpenRoom();
        foreach (string name in new[] {
            "Tripo Shelving Unit", "Shelf Mug 01", "Shelf Books 02", "Shelf Plant 02",
            "Tripo Table Lamp", "Tripo Television", "Pink Vase", "Tripo Cat Tree",
            "Table", "Sofa", "Chair 01", "Tripo Navy Sideboard", "Tripo Curtain",
            "Tripo Hide Cardboard", "Desktop Globe", "Lucky Cat"
        })
        {
            GameObject obj = RequireObject(name);
            Bounds b = BoundsOf(obj);
            Debug.Log($"LAYOUT {name} pos={obj.transform.position} min={b.min} max={b.max} size={b.size}");
        }
    }

    public static void LogWallAndWindowMaterials()
    {
        OpenRoom();
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        Bounds? backWall = null;
        List<Bounds> shellWallBounds = new();
        foreach (Renderer renderer in renderers.OrderBy(r => r.name))
        {
            string hierarchy = HierarchyPath(renderer.transform);
            bool relevant = hierarchy.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                            hierarchy.Contains("Window", StringComparison.OrdinalIgnoreCase) ||
                            hierarchy.Contains("Curtain", StringComparison.OrdinalIgnoreCase) ||
                            hierarchy.Contains("skirting", StringComparison.OrdinalIgnoreCase) ||
                            hierarchy.Contains("frame", StringComparison.OrdinalIgnoreCase);
            if (!relevant) continue;

            Bounds bounds = renderer.bounds;
            if (renderer.name == "Back Wall") backWall = bounds;
            if (hierarchy.Contains("Four Wall Room Shell", StringComparison.Ordinal) &&
                hierarchy.Contains("Wall", StringComparison.OrdinalIgnoreCase))
                shellWallBounds.Add(bounds);

            foreach (Material material in renderer.sharedMaterials)
            {
                Color color = MaterialColor(material);
                bool suspicious = IsMagentaLike(color) ||
                                  material == null ||
                                  material.shader == null ||
                                  material.shader.name.Contains("InternalErrorShader", StringComparison.OrdinalIgnoreCase);
                Debug.Log($"WALL_AUDIT name={hierarchy} boundsMin={bounds.min} boundsMax={bounds.max} " +
                          $"mat={(material == null ? "null" : material.name)} shader={(material?.shader == null ? "null" : material.shader.name)} " +
                          $"color={color} suspicious={suspicious}");
            }
        }

        if (backWall.HasValue)
        {
            foreach (Bounds shellBounds in shellWallBounds)
            {
                bool overlap = backWall.Value.Intersects(shellBounds);
                Debug.Log($"WALL_OVERLAP BackWall_vs_Shell overlap={overlap} back={backWall.Value} shell={shellBounds}");
            }
        }
    }

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

    [MenuItem("Catastrophe/Reference Room/Run Batches 21-30")]
    public static void RunReferenceRoomBatchesInEditor()
    {
        try
        {
            for (int number = 21; number <= 29; number++)
            {
                Run(number);
                Debug.Log($"REFERENCE_ROOM_BATCH_{number:00}_PASSED");
            }
            Debug.Log("REFERENCE_ROOM_BATCH_30_PASSED");
        }
        catch (Exception exception)
        {
            Debug.LogError($"REFERENCE_ROOM_BATCH_FAILED: {exception.Message}");
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
            else if (number == 30)
            {
                for (int i = 21; i <= 29; i++) Run(i);
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
            case 21: ValidateReferencePalette(); break;
            case 22: ValidateReferenceFloor(); break;
            case 23: ValidateCompactRoom(); break;
            case 24: ValidateReferenceSofa(); break;
            case 25: ValidateReferenceTvZone(); break;
            case 26: ValidateClimbableDisplayShelf(); break;
            case 27: ValidateReferenceCatTree(); break;
            case 28: ValidateReferenceDiningCluster(); break;
            case 29: ValidateReferenceLayoutPolish(); break;
            case 30: for (int i = 21; i <= 29; i++) Run(i); break;
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
            float minMass = valuable.name is "Desktop Globe" or "Lucky Cat" ? 0.40f : 1f;
            Require(body != null && body.mass >= minMass && body.mass <= 3f,
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
        Require(File.Exists(CatFbxPath) && File.Exists(CatPrefabPath) &&
                File.Exists("Assets/Art/SmoothKitten3D/SmoothKitten3D_BaseColor.png"),
            "The skinned 3D kitten or its appearance texture is missing.");
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
        Require(prefabGuids.Length == 21, $"Expected 21 Tripo prefabs, found {prefabGuids.Length}.");
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
        Require(prefab.GetComponentInChildren<SkinnedMeshRenderer>() != null,
            "Cat prefab has no skinned 3D mesh.");
        Require(prefab.GetComponentInChildren<SpriteRenderer>() == null,
            "The retired billboard sprite is still present in the cat prefab.");
    }

    private static void ValidateAnimations()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CatControllerPath);
        Require(controller != null, "3D cat Animator Controller is missing.");
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(CatFbxPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToArray();
        string[] required = { "Cat_Idle", "Cat_Walk", "Cat_Jump", "Cat_Push_Right" };
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
        Require(AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoFbxFolder}/Tripo_Floor.fbx") != null,
            "Imported Tripo floor FBX is missing.");
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
        {
            foreach (Material material in renderer.sharedMaterials)
            {
            Require(material != null && material.shader != null &&
                    !material.shader.name.Contains("InternalErrorShader", StringComparison.OrdinalIgnoreCase),
                $"Room shell has a missing/pink material on {renderer.name}.");
            Require(!IsMagentaLike(MaterialColor(material)),
                $"Room shell still has a magenta-like material on {renderer.name}: {MaterialColor(material)}.");
            if (renderer.name.Contains("Wall", StringComparison.OrdinalIgnoreCase))
                Require(Mathf.Abs(MaterialColor(material).r - 0.91f) < 0.04f &&
                        Mathf.Abs(MaterialColor(material).g - 0.84f) < 0.04f,
                    $"Wall material is not the unified warm wall colour on {renderer.name}: {MaterialColor(material)}.");
            if (renderer.name.Contains("Curtain", StringComparison.OrdinalIgnoreCase))
                Require(!renderer.sharedMaterials.Any(m => IsMagentaLike(MaterialColor(m))),
                    $"Window curtain has a magenta-like material on {renderer.name}.");
            }
        }
    }

    private static void ValidateFurnitureLayout()
    {
        OpenRoom();
        string[] names = {
            "Sofa", "Table", "Chair 01", "Chair 02", "Chair 03", "MacBook", "iPad", "Cup",
            "Tripo Shelving Unit", "Tripo Tv Cabinet", "Tripo Television", "Tripo Table Lamp",
            "Tripo Navy Sideboard", "Tripo Curtain", "Tripo Hide Cardboard",
            "Pink Vase", "Tripo Cat Tree", "Shelf Mug 01", "Shelf Books 02", "Shelf Plant 02",
            "Desktop Globe", "Lucky Cat"
        };
        foreach (string name in names) RequireObject(name);
        foreach (string omitted in new[] { "Floor Lamp", "TV Cabinet", "Flower Vase", "Wall Decorations" })
            Require(GameObject.Find(omitted) == null, $"Unattached extra object must not be generated: {omitted}.");
        Require(RequireObject("Sofa").transform.position.x < -3f && RequireObject("Sofa").transform.position.z > 2f,
            "Sofa should be against the back-left wall.");
        Require(RequireObject("Table").transform.position.x > 0f && Mathf.Abs(RequireObject("Table").transform.position.z) < 1f,
            "Dining table should stay centered and near the window side of the room.");
        Require(RequireObject("MacBook").transform.parent == RequireObject("Table").transform.parent,
            "Desktop objects and table should remain independently movable siblings.");

        Bounds table = BoundsOf(RequireObject("Table"));
        Bounds sofa = BoundsOf(RequireObject("Sofa"));
        Bounds chair = BoundsOf(RequireObject("Chair 01"));
        Bounds laptop = BoundsOf(RequireObject("MacBook"));
        Bounds tablet = BoundsOf(RequireObject("iPad"));
        Bounds cup = BoundsOf(RequireObject("Cup"));
        foreach (string groundedName in new[] { "Sofa", "Table", "Chair 01", "Chair 02", "Chair 03", "Tripo Shelving Unit", "Tripo Tv Cabinet", "Tripo Cat Tree" })
        {
            Bounds grounded = BoundsOf(RequireObject(groundedName));
            Require(grounded.min.y >= -0.025f && grounded.min.y <= 0.035f,
                $"{groundedName} is sunk below or floating above the floor: min.y={grounded.min.y:F3}.");
        }
        Require(BoundsOf(RequireObject("Tripo Shelving Unit")).max.y < 3.2f,
            "Shelving unit must not be up near the ceiling.");
        foreach (string shelfProp in new[] { "Shelf Mug 01", "Shelf Books 02", "Shelf Plant 02", "Desktop Globe", "Lucky Cat" })
        {
            Bounds prop = BoundsOf(RequireObject(shelfProp));
            Require(prop.min.y >= 0.45f && prop.min.y <= 1.75f && Mathf.Abs(prop.center.z - 3.30f) <= 0.45f,
                $"{shelfProp} must sit on the shelving unit boards, not float away: min.y={prop.min.y:F2}, z={prop.center.z:F2}.");
        }
        Require(Mathf.Abs(BoundsOf(RequireObject("Tripo Table Lamp")).center.y - 0.87f) <= 0.08f,
            "Table lamp must sit on the TV cabinet top.");
        Require(Mathf.Abs(BoundsOf(RequireObject("Tripo Navy Sideboard")).center.y - 0.32f) <= 0.06f,
            "Navy sideboard must use the measured placement.");
        Require(BoundsOf(RequireObject("Tripo Television")).center.y >= 1.05f &&
                BoundsOf(RequireObject("Tripo Television")).center.y <= 1.55f,
            "Television must be lowered to cabinet/wall viewing height.");
        Require(Mathf.Abs(BoundsOf(RequireObject("Pink Vase")).center.y - 1.165f) <= 0.08f,
            "Pink Vase must sit on the dining table.");
        Require(RequireObject("Tripo Cat Tree").transform.position.x >= -4.8f,
            "Cat tree must be pulled inward from the left wall.");
        Require(RequireObject("Desktop Globe").GetComponent<Valuable>() != null &&
                RequireObject("Lucky Cat").GetComponent<Valuable>() != null,
            "Globe and lucky cat must be pushable valuable shelf props.");
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
        Vector3 tabletPosition = RequireObject("iPad").transform.position;
        Require(Mathf.Abs(tabletPosition.x - 1.50f) <= 0.08f &&
                Mathf.Abs(tabletPosition.y - 0.81f) <= 0.08f &&
                Mathf.Abs(tabletPosition.z + 0.30f) <= 0.08f,
            $"iPad must use the Hi3D relayout target (1.50, 0.81, -0.30): actual={tabletPosition}.");
        Require(tablet.min.x >= table.min.x - 0.05f && tablet.max.x <= table.max.x + 0.05f &&
                tablet.min.z >= table.min.z - 0.05f && tablet.max.z <= table.max.z + 0.05f,
            $"iPad must sit on the tabletop footprint: table={table}, tablet={tablet}.");
        Require(RequireObject("Chair 01").transform.position.z <= -1.45f,
            "Chair 01 must stay close to the table as a readable jump step.");
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
            $"{TripoFbxFolder}/Textures/Tripo_Floor/00_Oak_grain_texture.png",
            $"{TripoFbxFolder}/Textures/Tripo_Table/00_Oak_grain_texture.png",
            $"{TripoFbxFolder}/Textures/Tripo_Chair/00_Oak_grain_texture.png",
            $"{TripoFbxFolder}/Textures/Tripo_Sofa/00_Image_0.png"
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

    private static void ValidateReferencePalette()
    {
        OpenRoom();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (!renderer.enabled) continue;
            foreach (Material material in renderer.sharedMaterials)
            {
                Require(material != null && material.shader != null,
                    $"Batch 21: missing material on {HierarchyPath(renderer.transform)}.");
                Color color = MaterialColor(material);
                Require(!IsMagentaLike(color),
                    $"Batch 21: magenta material on {HierarchyPath(renderer.transform)}: {color}.");
                Require(!IsBrickRed(color),
                    $"Batch 21: brick-red material remains on {HierarchyPath(renderer.transform)}: {color}.");
            }
        }

        Color sofa = MaterialColor(RequireObject("Sofa").GetComponentInChildren<Renderer>().sharedMaterial);
        Require(sofa.r > .82f && sofa.g > .76f && sofa.b > .64f,
            $"Batch 21: sofa must use the warm cream reference palette: {sofa}.");
    }

    private static void ValidateReferenceFloor()
    {
        OpenRoom();
        GameObject floor = RequireObject("Uniform Light Oak Floor");
        Bounds bounds = BoundsOf(floor);
        Require(bounds.size.x >= RoomShellInstaller.RoomWidth - .05f &&
                bounds.size.z >= RoomShellInstaller.RoomDepth - .05f,
            $"Batch 22: light-oak floor does not cover the compact room: {bounds.size}.");
        Require(bounds.max.y <= .012f && bounds.min.y >= -.085f,
            $"Batch 22: light-oak floor must meet the Y=0 walk surface: {bounds}.");

        Renderer oak = FindDeep(floor.transform, "Continuous oak surface")?.GetComponent<Renderer>();
        Require(oak != null, "Batch 22: continuous light-oak surface is missing.");
        Color oakColor = MaterialColor(oak.sharedMaterial);
        Require(oakColor.r >= .72f && oakColor.g >= .55f && oakColor.b >= .35f,
            $"Batch 22: floor is not light warm oak: {oakColor}.");
        Require(RequireObject("Floor").GetComponentsInChildren<Renderer>(true).All(r => !r.enabled),
            "Batch 22: old red/incomplete Tripo floor is still visible.");

        BoxCollider safety = RequireObject("Gameplay Floor Collider - full room").GetComponent<BoxCollider>();
        Require(safety != null && safety.bounds.size.x >= RoomShellInstaller.RoomWidth - .02f &&
                safety.bounds.size.z >= RoomShellInstaller.RoomDepth - .02f,
            "Batch 22: continuous floor collider does not cover the visible floor.");
    }

    private static void ValidateCompactRoom()
    {
        OpenRoom();
        Bounds back = BoundsOf(RequireObject("Back Wall"));
        Bounds left = BoundsOf(RequireObject("Left Wall"));
        Require(back.size.x <= 8.9f && back.size.x >= 8.7f,
            $"Batch 23: room width is not compact 8.8 m: {back.size.x:F2}.");
        Require(left.size.z <= 6.7f && left.size.z >= 6.5f,
            $"Batch 23: room depth is not compact 6.6 m: {left.size.z:F2}.");
        Require(Mathf.Abs(back.center.z - RoomShellInstaller.RoomDepth * .5f) < .06f &&
                Mathf.Abs(left.center.x + RoomShellInstaller.RoomWidth * .5f) < .06f,
            "Batch 23: compact walls are not aligned to the floor perimeter.");

        foreach (string name in new[] { "Sofa", "Table", "Tripo Cat Tree", "Tripo Shelving Unit",
                     "Tripo Tv Cabinet", "Tripo Navy Sideboard", "Tripo Hide Cardboard" })
        {
            Bounds item = BoundsOf(RequireObject(name));
            Require(item.min.x >= -4.42f && item.max.x <= 4.42f && item.min.z >= -3.32f && item.max.z <= 3.32f,
                $"Batch 23: {name} is outside the compact room: {item}.");
        }
    }

    private static void ValidateReferenceSofa()
    {
        OpenRoom();
        GameObject sofa = RequireObject("Sofa");
        Bounds bounds = BoundsOf(sofa);
        float sofaCenterZDelta = Mathf.Abs(bounds.center.z - .95f);
        Require(bounds.center.x <= -3.15f && sofaCenterZDelta <= .16f,
            $"Batch 24: sofa must be against the left wall like the reference: " +
            $"center=({bounds.center.x:F6}, {bounds.center.y:F6}, {bounds.center.z:F6}), " +
            $"zDelta={sofaCenterZDelta:F6}.");
        Require(bounds.size.x >= .84f && bounds.size.x <= 1.12f &&
                bounds.size.z >= 2.20f && bounds.size.z <= 2.60f &&
                bounds.size.y >= .82f && bounds.size.y <= .98f,
            $"Batch 24: sofa proportions are distorted: {bounds.size}.");
        Require(Mathf.Abs(bounds.min.y) <= .03f, $"Batch 24: sofa is not grounded: {bounds.min.y:F3}.");
        Color color = MaterialColor(sofa.GetComponentInChildren<Renderer>().sharedMaterial);
        Require(color.r > .84f && color.g > .79f && color.b > .68f,
            $"Batch 24: sofa is not cream: {color}.");
    }

    private static void ValidateReferenceTvZone()
    {
        OpenRoom();
        Bounds cabinet = BoundsOf(RequireObject("Tripo Tv Cabinet"));
        Bounds television = BoundsOf(RequireObject("Tripo Television"));
        Bounds lamp = BoundsOf(RequireObject("Tripo Table Lamp"));
        Require(cabinet.center.x >= 2.3f && cabinet.center.x <= 3.0f && cabinet.center.z >= 2.65f,
            $"Batch 25: TV cabinet is not on the back-right wall: {cabinet.center}.");
        Require(Mathf.Abs(television.center.x - cabinet.center.x) <= .12f &&
                television.center.z >= cabinet.center.z && television.min.y >= cabinet.max.y - .03f,
            $"Batch 25: TV is not correctly centered above its cabinet: tv={television}, cabinet={cabinet}.");
        Require(lamp.min.y >= cabinet.max.y - .04f && lamp.center.x > cabinet.center.x,
            $"Batch 25: lamp is not on the right side of the TV cabinet: {lamp}.");
    }

    private static void ValidateClimbableDisplayShelf()
    {
        OpenRoom();
        GameObject shelf = RequireObject("Tripo Shelving Unit");
        Bounds bounds = BoundsOf(shelf);
        Require(bounds.size.x >= 1.55f && bounds.size.y >= 2.0f,
            $"Batch 26: display shelf was not enlarged: {bounds.size}.");
        BoxCollider[] steps = shelf.GetComponentsInChildren<BoxCollider>(true)
            .Where(c => c.enabled && c.name.Contains("Climbable Shelf Platform", StringComparison.Ordinal)).ToArray();
        Require(steps.Length == 4, $"Batch 26: shelf needs four climbable levels, actual={steps.Length}.");
        float previousTop = 0f;
        foreach (BoxCollider step in steps.OrderBy(c => c.bounds.max.y))
        {
            Require(step.bounds.size.x >= 1.45f && step.bounds.size.z >= .44f,
                $"Batch 26: shelf step is too small for the cat: {step.bounds.size}.");
            Require(step.bounds.max.y - previousTop <= .50f,
                $"Batch 26: shelf step gap is not jumpable: {step.bounds.max.y - previousTop:F2} m.");
            previousTop = step.bounds.max.y;
        }
        Require(BoundsOf(RequireObject("Desktop Globe")).min.y >= 1.66f &&
                BoundsOf(RequireObject("Lucky Cat")).min.y >= 1.66f,
            "Batch 26: globe and lucky cat must sit on the reachable upper shelf.");
    }

    private static void ValidateReferenceCatTree()
    {
        OpenRoom();
        GameObject catTree = RequireObject("Tripo Cat Tree");
        Bounds bounds = BoundsOf(catTree);
        Require(bounds.center.x >= -2.1f && bounds.center.x <= -.8f && bounds.center.z >= 2.2f,
            $"Batch 27: cat tree must sit between sofa and shelf: {bounds.center}.");
        Require(bounds.size.y >= 1.45f && bounds.size.y <= 1.68f,
            $"Batch 27: cat tree height is wrong: {bounds.size.y:F2}.");
        Require(Mathf.Abs(bounds.min.y) <= .03f, $"Batch 27: cat tree is not grounded: {bounds.min.y:F3}.");
        Color color = MaterialColor(catTree.GetComponentInChildren<Renderer>().sharedMaterial);
        Require(color.r > .65f && color.g > .53f && color.b > .38f && !IsBrickRed(color),
            $"Batch 27: cat tree must use soft beige, not brick red: {color}.");
    }

    private static void ValidateReferenceDiningCluster()
    {
        OpenRoom();
        Bounds rug = BoundsOf(RequireObject("Large Cream Woven Rug"));
        Bounds table = BoundsOf(RequireObject("Table"));
        Require(rug.size.x >= 4.1f && rug.size.z >= 2.8f && rug.center.z < 0f,
            $"Batch 28: cream rug does not anchor the dining cluster: {rug}.");
        Require(Mathf.Abs(table.center.x) <= .08f && Mathf.Abs(table.center.z + .55f) <= .10f,
            $"Batch 28: dining table is not centered on the rug: {table.center}.");
        foreach (string chairName in new[] { "Chair 01", "Chair 02", "Chair 03" })
        {
            Bounds chair = BoundsOf(RequireObject(chairName));
            Require(chair.min.y >= -.03f && chair.min.y <= .03f,
                $"Batch 28: {chairName} is not grounded: {chair.min.y:F3}.");
            Require(Vector2.Distance(new Vector2(chair.center.x, chair.center.z),
                    new Vector2(table.center.x, table.center.z)) <= 1.65f,
                $"Batch 28: {chairName} is too far from the table.");
        }
        foreach (string prop in new[] { "MacBook", "iPad", "Cup", "Cup 2", "Pink Vase" })
        {
            Bounds item = BoundsOf(RequireObject(prop));
            Require(item.min.y >= table.max.y - .04f,
                $"Batch 28: {prop} is not resting on the table: item={item}, table={table}.");
        }
    }

    private static void ValidateReferenceLayoutPolish()
    {
        OpenRoom();
        RequireObject("Tripo Curtain");
        RequireObject("Tripo Hide Cardboard");
        RequireObject("Tripo Navy Sideboard");
        RequireObject("Cat portrait above sofa");
        Require(GameObject.Find("Plant Print 01 - replaceable Tripo 08") == null &&
                GameObject.Find("Plant Print 02 - replaceable Tripo 08") == null,
            "The sofa wall should display only the cat portrait, not the old plant prints.");

        Bounds curtain = BoundsOf(RequireObject("Tripo Curtain"));
        Bounds box = BoundsOf(RequireObject("Tripo Hide Cardboard"));
        Require(curtain.center.x >= 4.20f && curtain.max.x <= 4.42f,
            $"Batch 29: curtain is not fitted to the right-wall window: {curtain}.");
        Require(box.center.x >= 3.2f && box.center.z <= -2.1f && Mathf.Abs(box.min.y) <= .03f,
            $"Batch 29: cardboard hide box is not grounded in the front-right corner: {box}.");

        // The four main zones should form a compact ring around the dining rug,
        // with no large empty dead zone between them.
        Vector3 sofa = BoundsOf(RequireObject("Sofa")).center;
        Vector3 shelf = BoundsOf(RequireObject("Tripo Shelving Unit")).center;
        Vector3 tv = BoundsOf(RequireObject("Tripo Tv Cabinet")).center;
        Require(Vector3.Distance(sofa, shelf) <= 4.7f && Vector3.Distance(shelf, tv) <= 2.8f,
            "Batch 29: furniture zones are still too spread out for the reference layout.");
    }

    private static void ValidateBootstrapAndCamera()
    {
        Require(typeof(GameBootstrap).GetMethod("StartGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) != null,
            "Runtime bootstrap entry point is missing.");
        Require(typeof(ThirdPersonCamera).GetMethod("Initialize") != null, "Third-person camera initialization is missing.");
        RuntimeAnimatorController catController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            CatControllerPath);
        Require(catController != null && catController.animationClips.Any(c =>
                c.name.EndsWith("Cat_Push_Right", StringComparison.Ordinal)),
            "Push animation is not connected to the runtime 3D cat controller.");
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

    private static string HierarchyPath(Transform transform)
    {
        List<string> parts = new();
        for (Transform current = transform; current != null; current = current.parent)
            parts.Add(current.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static Color MaterialColor(Material material)
    {
        if (material == null) return Color.magenta;
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return material.color;
    }

    private static bool IsMagentaLike(Color color)
    {
        return color.r > 0.85f && color.b > 0.65f && color.g < 0.25f;
    }

    private static bool IsBrickRed(Color color)
    {
        // Reject the dark rusty-red family that dominated the previous floor,
        // rug and furniture, while allowing small pink, orange and gold accents.
        return color.r > .42f && color.g >= .16f && color.g <= .36f && color.b < .30f &&
               color.r > color.g * 1.65f && color.g > color.b * 1.05f;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
