using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    // Legacy scoring callers remain compatible; the clean HUD owns all feedback.
    private string toast = string.Empty;

    // These points are deliberately in the open half of the room, not near the
    // sofa, hide-box, chair legs, or dining table.  Imported furniture sometimes
    // changes its pivot/collider slightly when its prefab is rebuilt, so a single
    // hard-coded spawn point is not sufficiently safe.
    private static readonly Vector3[] CatSpawnCandidates =
    {
        new(-0.55f, 0.225f, -1.55f),
        new(1.70f, 0.225f, -2.35f),
        new(3.25f, 0.225f, -1.65f),
        new(0.10f, 0.225f, -2.70f)
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (FindAnyObjectByType<GameBootstrap>() == null)
            new GameObject("Catastrophe Game").AddComponent<GameBootstrap>();
        if (FindAnyObjectByType<TargetManager>() == null)
            new GameObject("Target Manager").AddComponent<TargetManager>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneRestart()
    {
        SceneManager.sceneLoaded -= OnGameplaySceneLoaded;
        SceneManager.sceneLoaded += OnGameplaySceneLoaded;
    }

    private static void OnGameplaySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "LivingRoom") StartGame();
    }

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        CreateCoreServices();
        ClearTemplateScene();
        CreateRoom();
        CatSolidCollision.EnsureFurnitureCollisions(GameObject.Find("Living Room"));
        CatSolidCollision.EnsureFurnitureCollisions(GameObject.Find("Tripo Art Pass (editable)"), true);
        EnsureObjectiveProps();
        CreateCat();
        CreateHideSpots();
        CreateCameraAndLight();
    }

    private void CreateCoreServices()
    {
        if (FindAnyObjectByType<GameManager>() == null)
            gameObject.AddComponent<GameManager>();

        NoiseManager noiseManager = FindAnyObjectByType<NoiseManager>();
        if (noiseManager == null)
            noiseManager = gameObject.AddComponent<NoiseManager>();
        noiseManager.threshold = 100f;

        if (FindAnyObjectByType<OwnerAlert>() == null)
            gameObject.AddComponent<OwnerAlert>();

        if (FindAnyObjectByType<AudioManager>() == null)
            gameObject.AddComponent<AudioManager>();

        if (FindAnyObjectByType<ChaosJuiceUI>() == null)
            gameObject.AddComponent<ChaosJuiceUI>();

        if (FindAnyObjectByType<GameFlowUI>() == null)
            gameObject.AddComponent<GameFlowUI>();

        if (FindAnyObjectByType<MobileCatControls>() == null)
            gameObject.AddComponent<MobileCatControls>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddScore(int points, string itemName)
    {
        GameManager.I?.AddChaos(points);
        ShowChaosToast(points, itemName);
    }

    public void ShowChaosToast(int points, string itemName)
    {
        // Only the amount and timer change. No floating money or narration.
        toast = string.Empty;
    }

    private static void ClearTemplateScene()
    {
        foreach (Camera sceneCamera in FindObjectsByType<Camera>())
        {
            AudioListener listener = sceneCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
            DestroySceneObject(sceneCamera.gameObject);
        }

        // Authored lighting is part of the editable scene. Only remove legacy
        // template lights; preserve the v3.4 rig and its independent children.
        foreach (Light sceneLight in FindObjectsByType<Light>())
        {
            Transform cursor = sceneLight.transform;
            bool authoredLighting = false;
            while (cursor != null)
            {
                if (cursor.name == "V3.4 Lighting Rig (editable)")
                {
                    authoredLighting = true;
                    break;
                }
                cursor = cursor.parent;
            }
            if (!authoredLighting)
                DestroySceneObject(sceneLight.gameObject);
        }
    }

    private static void DestroySceneObject(GameObject target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void CreateRoom()
    {
        // The editable living-room layout is authored into the scene by
        // LivingRoomSceneSetup. Keep it intact in Play Mode so every imported
        // piece remains an independently movable GameObject in the Hierarchy.
        if (GameObject.Find("Living Room") != null)
            return;

        CreateStatic("Floor", PrimitiveType.Cube, new Vector3(0f, -0.3f, 0f),
            new Vector3(14f, 0.6f, 11f), new Color(0.87f, 0.73f, 0.53f));
        CreateStatic("Back Wall", PrimitiveType.Cube, new Vector3(0f, 3.5f, 5.5f),
            new Vector3(14f, 7f, 0.3f), new Color(0.79f, 0.88f, 0.86f));
        CreateStatic("Left Wall", PrimitiveType.Cube, new Vector3(-7f, 3.5f, 0f),
            new Vector3(0.3f, 7f, 11f), new Color(0.86f, 0.91f, 0.89f));

        CreateTable(new Vector3(-3.2f, 1.2f, 1.8f), new Vector3(4.3f, 0.3f, 2.2f));
        CreateTable(new Vector3(3.5f, 0.9f, 2.2f), new Vector3(3.2f, 0.28f, 1.7f));
        CreateItems();
    }

    private void CreateTable(Vector3 position, Vector3 size)
    {
        Color wood = new Color(0.39f, 0.20f, 0.09f);
        CreateStatic("Table Top", PrimitiveType.Cube, position, size, wood);

        float legHeight = position.y;
        float x = size.x * 0.42f;
        float z = size.z * 0.38f;
        foreach (Vector3 offset in new[]
                 {
                     new Vector3(-x, -legHeight * 0.5f, -z), new Vector3(x, -legHeight * 0.5f, -z),
                     new Vector3(-x, -legHeight * 0.5f, z), new Vector3(x, -legHeight * 0.5f, z)
                 })
        {
            CreateStatic("Table Leg", PrimitiveType.Cube, position + offset,
                new Vector3(0.22f, legHeight, 0.22f), wood);
        }
    }

    private void CreateItems()
    {
        CreateItem("Coffee Mug", PrimitiveType.Cylinder, new Vector3(-4.3f, 1.7f, 1.7f),
            new Vector3(0.36f, 0.48f, 0.36f), Color.white, 20);
        CreateItem("Pink Vase", PrimitiveType.Cylinder, new Vector3(-3.0f, 1.82f, 1.7f),
            new Vector3(0.42f, 0.7f, 0.42f), new Color(0.96f, 0.38f, 0.55f), 50);
        CreateItem("Lamp", PrimitiveType.Cylinder, new Vector3(-1.7f, 1.82f, 1.8f),
            new Vector3(0.34f, 0.7f, 0.34f), new Color(1f, 0.78f, 0.20f), 40);
        CreateItem("Game Controller", PrimitiveType.Cube, new Vector3(-3.6f, 1.5f, 2.2f),
            new Vector3(0.72f, 0.24f, 0.38f), new Color(0.16f, 0.20f, 0.30f), 35);
        CreateItem("Speaker", PrimitiveType.Cube, new Vector3(3.5f, 1.45f, 2.1f),
            new Vector3(0.65f, 0.82f, 0.58f), new Color(0.12f, 0.12f, 0.16f), 120);
        CreateItem("Picture Frame", PrimitiveType.Cube, new Vector3(4.35f, 1.4f, 2.25f),
            new Vector3(0.56f, 0.72f, 0.13f), new Color(0.90f, 0.65f, 0.13f), 70);
        CreateItem("Plant Pot", PrimitiveType.Cylinder, new Vector3(2.65f, 1.42f, 2.1f),
            new Vector3(0.46f, 0.62f, 0.46f), new Color(0.30f, 0.60f, 0.25f), 60);
    }

    private static void EnsureObjectiveProps()
    {
        ScoreItem[] scoreItems = FindObjectsByType<ScoreItem>(FindObjectsInactive.Include);
        ScoreItem firstCup = null;
        int cupCount = 0;
        bool hasFragileProp = false;
        foreach (ScoreItem item in scoreItems)
        {
            string itemName = item.gameObject.name;
            if (itemName.Contains("Cup", System.StringComparison.OrdinalIgnoreCase) ||
                itemName.Contains("Mug", System.StringComparison.OrdinalIgnoreCase))
            {
                cupCount++;
                firstCup ??= item;
            }

            // Cups and mugs are already configured as fragile by ScoreItem. Treat
            // them as satisfying the fragile-prop requirement so the bootstrap
            // does not spawn a Pink Vase beside an arbitrary shelf mug. That old
            // fallback could overlap another shelf prop and physics-launch both
            // objects on the first frame.
            if (itemName.Contains("Vase", System.StringComparison.OrdinalIgnoreCase) ||
                itemName.Contains("Cup", System.StringComparison.OrdinalIgnoreCase) ||
                itemName.Contains("Mug", System.StringComparison.OrdinalIgnoreCase))
                hasFragileProp = true;
        }

        if (cupCount < 2 && firstCup != null)
        {
            GameObject secondCup = Instantiate(firstCup.gameObject, firstCup.transform.parent);
            secondCup.name = "Cup 2";
            secondCup.transform.localPosition = firstCup.transform.localPosition + new Vector3(-0.48f, 0f, 0.12f);
        }

        if (!hasFragileProp && firstCup != null)
        {
            Vector3 vasePosition = firstCup.transform.position + new Vector3(-0.9f, 0.12f, 0.1f);
            CreateItem("Pink Vase", PrimitiveType.Cylinder, vasePosition,
                new Vector3(0.32f, 0.55f, 0.32f), new Color(0.96f, 0.38f, 0.55f), 35);
        }
    }

    private void CreateCat()
    {
        // A cat authored into a test scene used to survive alongside the runtime
        // cat.  That made it look as though the player had spawned inside the
        // sofa whenever the old test cat happened to be there.  The bootstrap
        // owns the one gameplay cat, so remove any stale instance first.
        foreach (CatController existingCat in FindObjectsByType<CatController>(FindObjectsSortMode.None))
            DestroySceneObject(existingCat.gameObject);

        GameObject cat = new GameObject("Mischief Cat");
        cat.name = "Mischief Cat";
        // Keep the cat at an animal scale relative to the 76 cm dining table.
        // Spawn clear of the chairs so it begins on the floor, not intersecting
        // a chair collider.
        // The capsule bottom is 0.225 m below the root. Spawn tangent to the
        // floor so the cat does not visibly settle downward on the first frame.
        cat.transform.position = CatSpawnCandidates[0];

        Rigidbody body = cat.AddComponent<Rigidbody>();
        body.mass = 1.5f;
        body.angularDamping = 8f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        // Only the controller may author facing; contacts must not spin the cat.
        body.constraints = RigidbodyConstraints.FreezeRotation;
        CapsuleCollider collider = cat.AddComponent<CapsuleCollider>();
        collider.radius = 0.20f;
        collider.height = 0.50f;
        collider.center = new Vector3(0f, 0.025f, 0f);
        // Actual skinned 3D model imported from hi3d-smooth-kitten_1.glb.
        // Keep the old sprite prefab intact for rollback, but never instantiate it here.
        GameObject catVisualPrefab = Resources.Load<GameObject>("SmoothKitten3D/SmoothKitten3D");
        if (catVisualPrefab != null)
        {
            GameObject visual = Instantiate(catVisualPrefab, cat.transform);
            visual.name = "Smooth Kitten 3D Visual";
            // Source feet are at -0.444115 m. Align them to capsule bottom -0.225.
            visual.transform.localPosition = Vector3.down * .225f +
                (new Vector3(0f, 0.063675f, 0f) + Vector3.up * .225f) * CatSolidCollision.SizeFactor;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * (0.65f * CatSolidCollision.SizeFactor);
            foreach (Collider childCollider in visual.GetComponentsInChildren<Collider>())
                childCollider.enabled = false;
        }
        else
        {
            Debug.LogError("3D kitten prefab was not found in Resources/SmoothKitten3D/SmoothKitten3D.");
        }

        // Add the controller after the visual child exists so its lifecycle can bind
        // the skeleton Animator immediately.
        cat.AddComponent<CatSolidCollision>();
        cat.AddComponent<CatController>();
        cat.AddComponent<CatContactShadow>();
        PlaceCatAtSafeSpawn(cat);
        StartCoroutine(RecheckCatSpawnAfterPhysics(cat));
    }

    private void PlaceCatAtSafeSpawn(GameObject cat)
    {
        if (cat == null)
            return;

        Physics.SyncTransforms();
        foreach (Vector3 candidate in CatSpawnCandidates)
        {
            cat.transform.position = candidate;
            Physics.SyncTransforms();
            if (!CatOverlapsSolidFurniture(cat))
                return;
        }

        // This should never be reached, but leaving a clear diagnostic is much
        // safer than silently creating the cat inside a sofa or cabinet.
        Debug.LogError("No clear cat spawn point was found; check LivingRoom furniture colliders.");
        cat.transform.position = CatSpawnCandidates[0];
    }

    private IEnumerator RecheckCatSpawnAfterPhysics(GameObject cat)
    {
        // Prefab-instantiated colliders can register one physics step after
        // Awake. Re-checking here catches that timing edge without ever moving a
        // player who has begun walking around the room.
        yield return new WaitForFixedUpdate();
        if (cat != null && CatOverlapsSolidFurniture(cat))
            PlaceCatAtSafeSpawn(cat);
    }

    private static bool CatOverlapsSolidFurniture(GameObject cat)
    {
        CatSolidCollision solid = cat.GetComponent<CatSolidCollision>();
        if (solid != null) return !solid.CanOccupy(cat.transform.position, cat.transform.rotation);
        CapsuleCollider capsule = cat.GetComponent<CapsuleCollider>();
        if (capsule == null)
            return false;

        Vector3 center = cat.transform.TransformPoint(capsule.center);
        float radius = capsule.radius * Mathf.Max(cat.transform.lossyScale.x, cat.transform.lossyScale.z);
        float halfSegment = Mathf.Max(0f, capsule.height * cat.transform.lossyScale.y * 0.5f - radius);
        Vector3 top = center + Vector3.up * halfSegment;
        Vector3 bottom = center - Vector3.up * halfSegment;

        foreach (Collider hit in Physics.OverlapCapsule(top, bottom, radius * 0.98f,
                     Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit == capsule || hit.transform.IsChildOf(cat.transform) || hit.isTrigger)
                continue;

            // The floor is expected contact.  Furniture and walls have volume
            // above it and must not intersect the initial cat capsule.
            Bounds bounds = hit.bounds;
            if (bounds.max.y <= 0.04f && bounds.size.y <= 0.30f)
                continue;
            return true;
        }

        return false;
    }

    private static void BuildSmoothCartoonShell(Transform parent)
    {
        // Keep the rig/Animator for gameplay clips, but replace its visibly furry
        // mesh with an intentionally simple, glossy toy-like silhouette.
        foreach (Renderer renderer in parent.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        Transform shell = new GameObject("CartoonShell").transform;
        shell.SetParent(parent, false);
        Color black = new(0.045f, 0.055f, 0.07f);
        Color white = new(0.96f, 0.94f, 0.90f);
        Color pink = new(1f, 0.48f, 0.58f);
        Color blush = new(1f, 0.42f, 0.52f);

        CreateCatPart(shell, "CuteBody", PrimitiveType.Capsule, new Vector3(0f, 0.62f, -0.10f), new Vector3(0.78f, 0.72f, 0.88f), black);
        CreateCatPart(shell, "CuteChest", PrimitiveType.Sphere, new Vector3(0f, 0.73f, 0.40f), new Vector3(0.60f, 0.72f, 0.42f), white);
        CreateCatPart(shell, "CuteHead", PrimitiveType.Sphere, new Vector3(0f, 1.42f, 0.18f), new Vector3(1.18f, 1.05f, 1.06f), black);
        CreateCatPart(shell, "CuteMuzzle", PrimitiveType.Sphere, new Vector3(0f, 1.30f, 0.70f), new Vector3(0.66f, 0.42f, 0.30f), white);
        CreateCatPart(shell, "CuteEarL", PrimitiveType.Sphere, new Vector3(-0.42f, 1.91f, 0.12f), new Vector3(0.38f, 0.46f, 0.25f), black);
        CreateCatPart(shell, "CuteEarR", PrimitiveType.Sphere, new Vector3(0.42f, 1.91f, 0.12f), new Vector3(0.38f, 0.46f, 0.25f), black);
        CreateCatPart(shell, "CuteEyeL", PrimitiveType.Sphere, new Vector3(-0.25f, 1.52f, 0.67f), new Vector3(0.27f, 0.33f, 0.14f), Color.black);
        CreateCatPart(shell, "CuteEyeR", PrimitiveType.Sphere, new Vector3(0.25f, 1.52f, 0.67f), new Vector3(0.27f, 0.33f, 0.14f), Color.black);
        CreateCatPart(shell, "EyeHighlightL", PrimitiveType.Sphere, new Vector3(-0.20f, 1.62f, 0.77f), Vector3.one * 0.075f, Color.white);
        CreateCatPart(shell, "EyeHighlightR", PrimitiveType.Sphere, new Vector3(0.30f, 1.62f, 0.77f), Vector3.one * 0.075f, Color.white);
        CreateCatPart(shell, "PinkTriangleNose", PrimitiveType.Sphere, new Vector3(0f, 1.31f, 0.89f), new Vector3(0.16f, 0.10f, 0.08f), pink);
        CreateCatPart(shell, "BlushL", PrimitiveType.Sphere, new Vector3(-0.36f, 1.27f, 0.79f), new Vector3(0.18f, 0.08f, 0.04f), blush);
        CreateCatPart(shell, "BlushR", PrimitiveType.Sphere, new Vector3(0.36f, 1.27f, 0.79f), new Vector3(0.18f, 0.08f, 0.04f), blush);
        CreateCatPart(shell, "Front_L_Lower", PrimitiveType.Capsule, new Vector3(-0.25f, 0.22f, 0.34f), new Vector3(0.24f, 0.25f, 0.24f), white);
        CreateCatPart(shell, "Front_R_Lower", PrimitiveType.Capsule, new Vector3(0.25f, 0.22f, 0.34f), new Vector3(0.24f, 0.25f, 0.24f), white);
        CreateCatPart(shell, "Back_L_Lower", PrimitiveType.Capsule, new Vector3(-0.28f, 0.22f, -0.36f), new Vector3(0.28f, 0.27f, 0.30f), black);
        CreateCatPart(shell, "Back_R_Lower", PrimitiveType.Capsule, new Vector3(0.28f, 0.22f, -0.36f), new Vector3(0.28f, 0.27f, 0.30f), black);
        CreateCatPart(shell, "Tail_01", PrimitiveType.Capsule, new Vector3(0.42f, 0.78f, -0.55f), new Vector3(0.18f, 0.65f, 0.18f), black, new Vector3(38f, 0f, -25f));
    }

    private static void NormalizeCatVisual(GameObject visual, Transform cat, float targetHeight)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        float scale = targetHeight / Mathf.Max(0.01f, bounds.size.y);
        visual.transform.localScale = Vector3.one * scale;
        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        float desiredBottom = cat.position.y - 0.225f;
        visual.transform.position += Vector3.up * (desiredBottom - bounds.min.y);
    }

    private static void CreateHideSpots()
    {
        CreateHideSpot("Hide Spot - Cardboard Box", "纸箱", new Vector3(3.55f, 0.30f, -2.42f),
            new Vector3(0.95f, 0.60f, 0.95f));
        // The current Tripo sofa has a solid plinth, not an open cavity.  Its old
        // "under sofa" trigger let the cat hide inside opaque geometry, exactly
        // the visual bug seen in Play mode. Keep the valid cardboard/curtain
        // hiding points and remove this legacy trigger until the sofa asset has
        // a real playable gap below it.
        GameObject underSofa = GameObject.Find("Hide Spot - Under Sofa");
        if (underSofa != null)
            DestroySceneObject(underSofa);
        CreateHideSpot("Hide Spot - Behind Curtain", "窗帘后", new Vector3(4.05f, 1.15f, 0f),
            new Vector3(0.50f, 2.30f, 1.45f));
        CreateCatTreeHideSpot();
    }

    private static void CreateCatTreeHideSpot()
    {
        var tree = GameObject.Find("Reference carpet cat tree");
        if (tree && tree.TryGetComponent<StorybookCatTree>(out var storybookTree))
        {
            storybookTree.ConfigureHiding();
            return;
        }
        if (!tree || tree.transform.Find("Playable hollow cubby")) return;
        // The original decorative opening is lower than the actual cat's
        // shoulders/tail. Enlarge the real hollow shell, never disable walls.
        var shell = new GameObject("Playable hollow cubby").transform;
        shell.SetParent(tree.transform, false);
        shell.localPosition = new Vector3(3.65f, .235f, -2.43f);
        foreach (var part in tree.GetComponentsInChildren<MeshRenderer>())
            if (part.name.StartsWith("Cubby ") || part.name.StartsWith("Door ") || part.name == "Arched cubby entrance")
                part.transform.SetParent(shell, true);
        shell.localScale = new Vector3(1.12f, 2f, 1.15f);

        var spot = new GameObject("Hide Spot - Cat Tree Cubby");
        spot.transform.SetParent(tree.transform, false);
        spot.transform.localPosition = new Vector3(3.08f, .50f, -2.43f);
        var trigger = spot.AddComponent<BoxCollider>();
        trigger.size = new Vector3(.75f, .75f, .78f);
        var hide = spot.AddComponent<HideSpot>();
        hide.Configure("猫爬架小窝");
        var inside = new GameObject("Inside cat tree cubby").transform;
        inside.SetParent(tree.transform, false);
        inside.localPosition = new Vector3(3.55f, .466f, -2.43f);
        inside.localRotation = Quaternion.Euler(0f, 270f, 0f);
        var outside = new GameObject("Outside cat tree cubby").transform;
        outside.SetParent(tree.transform, false);
        outside.localPosition = new Vector3(2.91f, .371f, -2.43f);
        outside.localRotation = inside.localRotation;
        hide.SetAnchors(inside, outside);
    }

    private static void CreateHideSpot(string objectName, string displayName, Vector3 position,
        Vector3 scale)
    {
        GameObject spot = GameObject.Find(objectName);
        if (spot == null)
        {
            // Gameplay volumes have no visible geometry. The independent Tripo
            // models supply the appearance; a tinted primitive hides furniture.
            spot = new GameObject(objectName);
            spot.transform.position = position;
            spot.transform.localScale = scale;
        }

        // Also repair legacy volumes if a scene already contains one. Changing
        // only URP's _Surface does not configure transparent blending/render state.
        Renderer renderer = spot.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;

        BoxCollider trigger = spot.GetComponent<BoxCollider>();
        if (trigger == null)
            trigger = spot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        HideSpot hideSpot = spot.GetComponent<HideSpot>();
        if (hideSpot == null)
            hideSpot = spot.AddComponent<HideSpot>();
        hideSpot.Configure(displayName);
    }

    private static void CreateCatPart(Transform parent, string name, PrimitiveType type, Vector3 localPosition,
        Vector3 localScale, Color color, Vector3? localEulerAngles = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        if (localEulerAngles.HasValue)
            part.transform.localEulerAngles = localEulerAngles.Value;
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
            partCollider.enabled = false;
        SetColor(part, color);
    }

    private static void CreateCameraAndLight()
    {
        Camera gameCamera = new GameObject("Main Camera").AddComponent<Camera>();
        gameCamera.gameObject.AddComponent<AudioListener>();
        gameCamera.tag = "MainCamera";
        gameCamera.fieldOfView = 55f;
        gameCamera.nearClipPlane = 0.08f;
        gameCamera.allowMSAA = true;
        gameCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        // Match the room palette so no bright blue strip appears if the camera
        // briefly sees beyond the open dollhouse edges.
        gameCamera.backgroundColor = new Color(0.76f, 0.68f, 0.58f);

        GameObject cat = GameObject.Find("Mischief Cat");
        ThirdPersonCamera followCamera = gameCamera.gameObject.AddComponent<ThirdPersonCamera>();
        followCamera.Initialize(cat != null ? cat.transform : null);

        QualitySettings.antiAliasing = 4;

        // Scene-authored lights remain fully editable. This neutral fallback is
        // used only by scenes that do not contain the LivingRoom lighting rig.
        if (FindAnyObjectByType<Light>() == null)
        {
            Light sun = new GameObject("Fallback Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.76f, 0.55f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
        }
    }

    private static void CreateStatic(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = name;
        item.transform.position = position;
        item.transform.localScale = scale;
        SetColor(item, color);
    }

    private static void CreateItem(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, int points)
    {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = name;
        item.transform.position = position;
        item.transform.localScale = scale;
        SetColor(item, color);

        Rigidbody body = item.AddComponent<Rigidbody>();
        body.mass = 0.75f;
        ScoreItem scoreItem = item.AddComponent<ScoreItem>();
        scoreItem.Points = points;
        scoreItem.DisplayName = name;
    }

    private static void SetColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

}
