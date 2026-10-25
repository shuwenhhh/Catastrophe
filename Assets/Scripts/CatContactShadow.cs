using UnityEngine;

[DisallowMultipleComponent]
public sealed class CatContactShadow : MonoBehaviour
{
    [SerializeField] private Vector2 shadowSize = new Vector2(0.42f, 0.62f);
    [SerializeField, Range(0f, 1f)] private float groundedOpacity = 0.34f;
    [SerializeField] private float lightProjectionOffset = 0.035f;
    [SerializeField] private float maxGroundDistance = 1.4f;

    private readonly RaycastHit[] groundHits = new RaycastHit[12];
    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;
    private Sprite shadowSprite;
    private Texture2D shadowTexture;
    private Light sceneLight;
    private Renderer[] decorativeGround;

    // The thin rug is visual-only. Feet and shadow must agree with its visible
    // surface without turning it into a new physical obstacle for the capsule.
    public Vector3 VisibleGroundPoint(Vector3 physicalPoint)
    {
        if (decorativeGround == null)
        {
            GameObject rug = GameObject.Find("Large Cream Woven Rug");
            decorativeGround = rug != null ? rug.GetComponentsInChildren<Renderer>() : System.Array.Empty<Renderer>();
        }
        Vector3 point = physicalPoint;
        foreach (Renderer part in decorativeGround)
        {
            if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) continue;
            Bounds bounds = part.bounds;
            if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                point.z >= bounds.min.z && point.z <= bounds.max.z &&
                bounds.max.y >= point.y && bounds.max.y - physicalPoint.y <= .08f)
                point.y = bounds.max.y;
        }
        return point;
    }

    private void Start()
    {
        CreateShadow();
        sceneLight = FindDirectionalLight();
    }

    private void LateUpdate()
    {
        // Script hot-reload can destroy the generated renderer one frame before
        // its parent object. Treat either reference as unavailable so stopping
        // Play Mode never leaves a MissingReferenceException behind.
        if (shadowObject == null || shadowRenderer == null)
            return;

        Vector3 origin = transform.position + Vector3.up * 0.35f;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits,
            maxGroundDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        bool foundSurface = false;
        RaycastHit surface = default;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit candidate = groundHits[index];
            if (candidate.collider == null || candidate.transform.IsChildOf(transform))
                continue;
            if (!foundSurface || candidate.distance < surface.distance)
            {
                surface = candidate;
                foundSurface = true;
            }
        }

        shadowObject.SetActive(foundSurface);
        if (!foundSurface)
            return;

        if (sceneLight == null)
            sceneLight = FindDirectionalLight();
        Vector3 projectionDirection = sceneLight != null
            ? Vector3.ProjectOnPlane(sceneLight.transform.forward, Vector3.up).normalized
            : new Vector3(0.45f, 0f, -0.25f).normalized;
        if (projectionDirection.sqrMagnitude < 0.01f)
            projectionDirection = Vector3.forward;

        Vector3 visibleSurface = VisibleGroundPoint(surface.point);
        shadowObject.transform.position = visibleSurface + Vector3.up * 0.006f +
                                          projectionDirection * lightProjectionOffset;
        // The sprite plane's local Y is its long axis. Align it with the horizontal
        // projection of the directional light so the contact shadow agrees
        // with the room's illumination instead of rotating with the cat.
        shadowObject.transform.rotation = Quaternion.LookRotation(Vector3.down, projectionDirection);
        shadowObject.transform.localScale = new Vector3(shadowSize.x, shadowSize.y, 1f);

        float feetHeight = Mathf.Max(0f, transform.position.y - 0.225f - surface.point.y);
        float opacity = groundedOpacity * (1f - Mathf.Clamp01(feetHeight / 0.55f));
        shadowRenderer.color = new Color(0.075f, 0.055f, 0.045f, opacity);
    }

    private void CreateShadow()
    {
        shadowObject = new GameObject("Cat Contact Shadow");
        shadowObject.name = "Cat Contact Shadow";
        shadowObject.transform.SetParent(transform, false);

        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
        shadowTexture = CreateSoftEllipseTexture();
        shadowSprite = Sprite.Create(shadowTexture, new Rect(0f, 0f,
                shadowTexture.width, shadowTexture.height), new Vector2(0.5f, 0.5f), 64f);
        shadowSprite.name = "Cat Contact Shadow Sprite";
        shadowRenderer.sprite = shadowSprite;
        shadowRenderer.sortingOrder = 1;
    }

    private Texture2D CreateSoftEllipseTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Cat Contact Shadow Gradient",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Light FindDirectionalLight()
    {
        foreach (Light candidate in FindObjectsByType<Light>())
            if (candidate.type == LightType.Directional && candidate.enabled)
                return candidate;
        return null;
    }

    private void OnDestroy()
    {
        if (shadowSprite != null)
            Destroy(shadowSprite);
        if (shadowTexture != null)
            Destroy(shadowTexture);
    }
}
