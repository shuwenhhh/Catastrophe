using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The old imported mesh has an open gap between its two slabs. This is a
// single rigid prop: the display, hinge and keyboard are all children of the
// existing Rigidbody/Valuable root, so pushing and scoring are unchanged.
public static class DetailedLaptopBuilder
{
    const string PrefabPath = "Assets/Models/Tripo/Prefabs/Tripo_Laptop.prefab";
    const string MaterialFolder = "Assets/Art/LivingRoom/Materials";

    [MenuItem("Catastrophe/Refine Connected Laptop")]
    public static void RefineExistingPrefab()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before refining the laptop.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Apply(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("CONNECTED_LAPTOP_REFINED prefab=" + PrefabPath);
    }

    [MenuItem("Catastrophe/Preview Connected Laptop")]
    public static void CapturePreview()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!asset) throw new InvalidOperationException("Laptop prefab missing.");
        GameObject laptop = null, cameraObject = null, lightObject = null;
        RenderTexture target = null;
        Texture2D image = null;
        try
        {
            laptop = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            laptop.hideFlags = HideFlags.HideAndDontSave;
            laptop.transform.position = new Vector3(0f, .205f, 0f);
            SetLayer(laptop.transform, 31);
            var oldVisual = laptop.transform.Find("Mesh");
            var detailed = laptop.transform.Find("Detailed connected laptop");
            var lid = detailed ? detailed.Find("Screen lid - hinged to base") : null;
            var hinge = detailed ? detailed.Find("Hinge shaft") : null;
            var baseRail = detailed ? detailed.Find("Rear hinge rail") : null;
            if (!oldVisual || oldVisual.gameObject.activeSelf || !lid ||
                !hinge || !baseRail || !detailed.Find("Keyboard deck"))
                throw new InvalidOperationException("Connected laptop pieces or source-mesh state invalid.");
            if (Vector3.Distance(lid.position, hinge.position) > .005f ||
                Vector3.Distance(hinge.position, baseRail.position) > .015f)
                throw new InvalidOperationException("Screen/hinge/base connection is not contiguous.");
            if (!laptop.GetComponent<Rigidbody>() || !laptop.GetComponent<Valuable>() ||
                !laptop.GetComponent<BoxCollider>())
                throw new InvalidOperationException("Laptop push/scoring components missing.");
            if (detailed.GetComponentsInChildren<Collider>().Length != 0)
                throw new InvalidOperationException("Visual parts unexpectedly have separate colliders.");
            var inScene = GameObject.Find("MacBook");
            if (!inScene || !inScene.transform.Find("Detailed connected laptop") ||
                !inScene.GetComponent<Valuable>())
                throw new InvalidOperationException("LivingRoom MacBook is not using the refined pushable prefab.");

            cameraObject = new GameObject("Laptop preview camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(.77f, .72f, .90f);
            camera.transform.LookAt(new Vector3(0f, .22f, -.25f));
            camera.orthographic = true;
            camera.orthographicSize = .52f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.91f, .925f, .94f);
            lightObject = new GameObject("Laptop preview light");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -40f, 0f);

            target = new RenderTexture(1100, 900, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1100, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1100, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Validation/Laptop");
            File.WriteAllBytes("Validation/Laptop/connected-laptop.png", image.EncodeToPNG());
            File.WriteAllText("Validation/Laptop/check.txt",
                "PASS screen lid meets the hinge shaft and base.\n" +
                "PASS imported disconnected mesh hidden.\n" +
                "PASS one root Rigidbody, Valuable and BoxCollider retained for pushing/scoring.\n" +
                "PASS detail geometry has no independent colliders.\n" +
                "PASS LivingRoom MacBook instance uses the refined pushable prefab.\n");
            Debug.Log("CONNECTED_LAPTOP_PREVIEW_SAVED Validation/Laptop/connected-laptop.png");
        }
        finally
        {
            RenderTexture.active = null;
            if (image) Object.DestroyImmediate(image);
            if (target) Object.DestroyImmediate(target);
            if (laptop) Object.DestroyImmediate(laptop);
            if (cameraObject) Object.DestroyImmediate(cameraObject);
            if (lightObject) Object.DestroyImmediate(lightObject);
        }
    }

    static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayer(child, layer);
    }

    public static void Apply(GameObject root)
    {
        var oldVisual = root.transform.Find("Mesh");
        if (oldVisual) oldVisual.gameObject.SetActive(false);
        var previous = root.transform.Find("Detailed connected laptop");
        if (previous) Object.DestroyImmediate(previous.gameObject);

        var housing = Material("Laptop graphite anodized metal", new Color(.115f, .135f, .165f), .55f, .25f);
        var deck = Material("Laptop brushed deck", new Color(.22f, .245f, .275f), .48f, .30f);
        var edge = Material("Laptop dark edge", new Color(.055f, .065f, .077f), .30f, .05f);
        var keys = Material("Laptop charcoal keys", new Color(.065f, .074f, .086f), .22f, .05f);
        var glass = Material("Laptop blue display", new Color(.055f, .12f, .21f), .70f, .15f);
        var softBlue = Material("Laptop display accent", new Color(.18f, .37f, .52f), .42f, .20f);
        var pale = Material("Laptop pale detail", new Color(.61f, .68f, .72f), .35f, .13f);

        var visual = new GameObject("Detailed connected laptop").transform;
        visual.SetParent(root.transform, false);

        // Front edge tapers visually; the slim dark seam wraps a continuous base.
        Box(visual, "Aluminium base", new Vector3(0f, -.185f, -.23f), new Vector3(.58f, .039f, .46f), housing);
        Box(visual, "Keyboard deck", new Vector3(0f, -.163f, -.235f), new Vector3(.555f, .007f, .435f), deck);
        Box(visual, "Front edge seam", new Vector3(0f, -.184f, -.001f), new Vector3(.53f, .006f, .003f), edge);
        Box(visual, "Rear hinge rail", new Vector3(0f, -.143f, -.43f), new Vector3(.43f, .018f, .027f), edge);
        Cylinder(visual, "Hinge shaft", new Vector3(0f, -.139f, -.433f), .011f, .42f, housing);
        foreach (float x in new[] { -.222f, .222f })
            Cylinder(visual, "Hinge cap", new Vector3(x, -.139f, -.433f), .016f, .042f, edge);

        var lid = new GameObject("Screen lid - hinged to base").transform;
        lid.SetParent(visual, false);
        lid.localPosition = new Vector3(0f, -.139f, -.433f);
        lid.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Box(lid, "Thin screen shell", new Vector3(0f, .174f, -.002f), new Vector3(.58f, .352f, .018f), housing);
        Box(lid, "Inset black bezel", new Vector3(0f, .184f, .009f), new Vector3(.557f, .319f, .002f), edge);
        Box(lid, "Recessed display glass", new Vector3(0f, .193f, .011f), new Vector3(.531f, .286f, .0015f), glass);
        Box(lid, "Subtle desktop strip", new Vector3(0f, .082f, .0125f), new Vector3(.39f, .009f, .001f), softBlue);
        Box(lid, "Display lower chin", new Vector3(0f, .024f, .011f), new Vector3(.55f, .033f, .003f), deck);
        Sphere(lid, "Webcam", new Vector3(0f, .341f, .012f), .004f, pale);
        Box(lid, "Small rear lid emblem", new Vector3(0f, .19f, -.012f), new Vector3(.046f, .034f, .001f), pale);

        // The keyboard, palm rest and trackpad belong to the same moving base.
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 12; col++)
            {
                float x = -.226f + col * .041f;
                float z = -.371f + row * .040f;
                Box(visual, "Keyboard key", new Vector3(x, -.157f, z), new Vector3(.033f, .0025f, .029f), keys);
            }
        Box(visual, "Space bar", new Vector3(0f, -.156f, -.169f), new Vector3(.18f, .002f, .023f), keys);
        Box(visual, "Trackpad hairline", new Vector3(0f, -.158f, -.077f), new Vector3(.214f, .001f, .109f), edge);
        Box(visual, "Trackpad glass", new Vector3(0f, -.1565f, -.077f), new Vector3(.209f, .001f, .104f), deck);
        foreach (float x in new[] { -.271f, .271f })
            Box(visual, "Side USB port", new Vector3(x, -.182f, -.19f), new Vector3(.003f, .007f, .025f), edge);

        var collider = root.GetComponent<BoxCollider>();
        if (collider)
        {
            collider.center = new Vector3(0f, -.004f, -.267f);
            collider.size = new Vector3(.59f, .41f, .54f);
        }
    }

    static Material Material(string name, Color color, float smoothness, float metallic)
    {
        if (!Directory.Exists(MaterialFolder)) Directory.CreateDirectory(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void Cylinder(Transform parent, string name, Vector3 position, float radius, float length, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        part.transform.localScale = new Vector3(radius * 2f, length * .5f, radius * 2f);
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void Sphere(Transform parent, string name, Vector3 position, float radius, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = Vector3.one * radius * 2f;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
    }
}
