using System.Collections;
using UnityEngine;

public sealed class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    private string toast = "FIRST TARGET: push the iPad off the table  •  WASD move  •  SPACE jump  •  E right-paw push";
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
            DestroySceneObject(sceneCamera.gameObject);

        foreach (Light sceneLight in FindObjectsByType<Light>())
            DestroySceneObject(sceneLight.gameObject);
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
        GameObject catVisualPrefab = Resources.Load<GameObject>("Cat/CatSprite");
        if (catVisualPrefab != null)
        {
            GameObject visual = Instantiate(catVisualPrefab, cat.transform);
            visual.name = "Animated Cat Sprite";
            // The source sheets contain generous transparent padding. Keep the
            // visual near the collider's feet; CatController performs the exact
            // per-frame foot alignment after the Animator selects a sprite.
            visual.transform.localPosition = new Vector3(0f, 0.055f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.52f;
            foreach (Collider childCollider in visual.GetComponentsInChildren<Collider>())
                childCollider.enabled = false;
        }
        else
        {
            Debug.LogError("Animated cat sprite prefab was not found in Resources/Cat.");
        }

        // Add the controller after the visual child exists so its lifecycle can bind
        // the Animator and SpriteRenderer immediately.
        cat.AddComponent<CatController>();
        cat.AddComponent<CatContactShadow>();
    }

    private static void CreateHideSpots()
    {
        CreateHideSpot("Hide Spot - Cardboard Box", "纸箱", new Vector3(-5.1f, 0.35f, -2.4f),
            new Vector3(1.15f, 0.7f, 1.15f), new Color(0.55f, 0.32f, 0.13f, 0.72f));
        CreateHideSpot("Hide Spot - Under Sofa", "沙发底", new Vector3(3.9f, 0.22f, -2.25f),
            new Vector3(2.4f, 0.44f, 1.05f), new Color(0.34f, 0.22f, 0.16f, 0.32f));
        CreateHideSpot("Hide Spot - Behind Curtain", "窗帘后", new Vector3(-6.25f, 1.15f, 4.55f),
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
        gameCamera.tag = "MainCamera";
        gameCamera.fieldOfView = 55f;
        gameCamera.nearClipPlane = 0.08f;
        gameCamera.allowMSAA = true;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        // Match the room palette so no bright blue strip appears if the camera
        // briefly sees beyond the open dollhouse edges.
        gameCamera.backgroundColor = new Color(0.76f, 0.68f, 0.58f);

        GameObject cat = GameObject.Find("Mischief Cat");
        ThirdPersonCamera followCamera = gameCamera.gameObject.AddComponent<ThirdPersonCamera>();
        followCamera.Initialize(cat != null ? cat.transform : null);

        QualitySettings.antiAliasing = 4;

        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
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
        GUI.Label(new Rect(0f, logicalHeight - 76f, logicalWidth, 44f), toast, toastStyle);
    }
}
