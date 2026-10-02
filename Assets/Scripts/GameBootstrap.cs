using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    private string toast = "WASD move  •  SPACE jump  •  E push / hide";
    private GUIStyle titleStyle;
    private GUIStyle scoreStyle;
    private GUIStyle toastStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (FindAnyObjectByType<GameBootstrap>() == null)
            new GameObject("Catastrophe Game").AddComponent<GameBootstrap>();
    }

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        CreateCoreServices();
        ClearTemplateScene();
        CreateRoom();
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
        // The design contract fixes the alert threshold at 70.
        noiseManager.threshold = 70f;

        if (FindAnyObjectByType<OwnerAlert>() == null)
            gameObject.AddComponent<OwnerAlert>();

        if (FindAnyObjectByType<AudioManager>() == null)
            gameObject.AddComponent<AudioManager>();

        if (FindAnyObjectByType<ChaosJuiceUI>() == null)
            gameObject.AddComponent<ChaosJuiceUI>();

        if (FindAnyObjectByType<GameFlowUI>() == null)
            gameObject.AddComponent<GameFlowUI>();
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
        toast = $"{itemName} crashed!  +{points}";
        StopAllCoroutines();
        StartCoroutine(ClearToast());
    }

    private IEnumerator ClearToast()
    {
        yield return new WaitForSeconds(2.5f);
        toast = "Keep causing chaos!";
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
        GameObject cat = new GameObject("Mischief Cat");
        cat.name = "Mischief Cat";
        // Keep the cat at an animal scale relative to the 76 cm dining table.
        // Spawn clear of the chairs so it begins on the floor, not intersecting
        // a chair collider.
        // The capsule bottom is 0.225 m below the root. Spawn tangent to the
        // floor so the cat does not visibly settle downward on the first frame.
        cat.transform.position = new Vector3(-0.55f, 0.225f, -1.55f);

        Rigidbody body = cat.AddComponent<Rigidbody>();
        body.mass = 1.5f;
        body.angularDamping = 8f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        CapsuleCollider collider = cat.AddComponent<CapsuleCollider>();
        collider.radius = 0.20f;
        collider.height = 0.50f;
        collider.center = new Vector3(0f, 0.025f, 0f);
        // The gameplay cat is authored from the same smooth tuxedo-kitten
        // sprite set used by the art-direction sheets.  Do not silently fall
        // back to the old furry FBX: its proportions and rigged poses are a
        // different character and make walking/pushing look unrelated.
        GameObject catVisualPrefab = Resources.Load<GameObject>("Cat/CatSprite");
        if (catVisualPrefab != null)
        {
            GameObject visual = Instantiate(catVisualPrefab, cat.transform);
            visual.name = "Smooth Tuxedo Kitten Visual";
            // The source frames are 724 px tall with generous transparent
            // padding.  This calibrated scale keeps the kitten at roughly
            // 50–60 cm and lets CatController's per-frame paw baseline land on
            // the capsule bottom without a first-frame pop.
            visual.transform.localPosition = new Vector3(0f, 0.055f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.52f;
            foreach (Collider childCollider in visual.GetComponentsInChildren<Collider>())
                childCollider.enabled = false;
        }
        else
        {
            Debug.LogError("Smooth kitten prefab was not found in Resources/Cat/CatSprite.");
        }

        // Add the controller after the visual child exists so its lifecycle can bind
        // the Animator and SpriteRenderer immediately.
        cat.AddComponent<CatController>();
        cat.AddComponent<CatContactShadow>();
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
        // Cardboard box kept clear of the left wall (inner face x=-5.16).
        CreateHideSpot("Hide Spot - Cardboard Box", "纸箱", new Vector3(-4.5f, 0.35f, -2.4f),
            new Vector3(1.15f, 0.7f, 1.15f), new Color(0.55f, 0.32f, 0.13f, 0.72f));
        CreateHideSpot("Hide Spot - Under Sofa", "沙发底", new Vector3(3.9f, 0.22f, -2.25f),
            new Vector3(2.4f, 0.44f, 1.05f), new Color(0.34f, 0.22f, 0.16f, 0.32f));
        // Behind the right-wall curtains (Tripo window at x=5.12). The old
        // (-6.25, 1.15, 4.55) was outside the four-wall room.
        CreateHideSpot("Hide Spot - Behind Curtain", "窗帘后", new Vector3(4.7f, 1.0f, 0f),
            new Vector3(0.65f, 2.3f, 1.45f), new Color(0.72f, 0.32f, 0.38f, 0.32f));
    }

    private static void CreateHideSpot(string objectName, string displayName, Vector3 position,
        Vector3 scale, Color color)
    {
        if (GameObject.Find(objectName) != null)
            return;

        GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spot.name = objectName;
        spot.transform.position = position;
        spot.transform.localScale = scale;
        SetColor(spot, color);
        Renderer renderer = spot.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.SetFloat("_Surface", 1f);
            renderer.material.color = color;
        }

        spot.GetComponent<BoxCollider>().isTrigger = true;
        HideSpot hideSpot = spot.AddComponent<HideSpot>();
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

    private void OnGUI()
    {
        if (!GameFlowUI.GameplayInputEnabled)
            return;

        Matrix4x4 previousMatrix = GUI.matrix;
        float uiScale = Mathf.Clamp(Screen.height / 1080f, 1f, 2f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float logicalWidth = Screen.width / uiScale;
        float logicalHeight = Screen.height / uiScale;
        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };
        scoreStyle ??= new GUIStyle(titleStyle) { fontSize = 23 };
        toastStyle ??= new GUIStyle(titleStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

        GUI.Label(new Rect(36f, 30f, 500f, 48f), "CATASTROPHE!", titleStyle);
        int score = GameManager.I != null ? GameManager.I.chaosScore : 0;
        GUI.Label(new Rect(36f, 76f, 420f, 38f), $"CHAOS SCORE  {score}", scoreStyle);
        GUI.Label(new Rect(0f, logicalHeight - 132f, logicalWidth, 38f), toast, toastStyle);
        GUI.matrix = previousMatrix;
    }
}
