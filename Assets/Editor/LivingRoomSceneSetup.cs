using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LivingRoomSceneSetup
{
    // Rebuilds the scene from source assets; decorative brand marks are intentionally omitted.
    private const string ModelFolder = "Assets/Art/LivingRoom/Models";
    private const string ScenePath = "Assets/LivingRoom.unity";
    private const string MaterialFolder = "Assets/Art/LivingRoom/Materials";
    private const string FloorTexturePath = ModelFolder + "/Textures/Floor/Oak_grain_texture.png";
    private const string TableTexturePath = ModelFolder + "/Textures/Table/Oak_grain_texture.png";
    private const string ChairTexturePath = ModelFolder + "/Textures/Chair/Oak_grain_texture.png";

    [MenuItem("Catastrophe/Build Editable Living Room")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject old = GameObject.Find("Living Room");
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);

        GameObject room = new GameObject("Living Room");
        GameObject architecture = Child(room, "Architecture (editable)");
        GameObject floor = PlaceSized("Floor", architecture.transform, Vector3.zero, 0f,
            new Vector3(10.5f, 0.18f, 7.7f), false);
        // Imported floor meshes have thickness. Keep the walkable top exactly at Y=0.
        floor.transform.position += Vector3.down * GetBounds(floor).max.y;
        CreateBox(architecture.transform, "Back Wall", new Vector3(0f, 2f, 3.85f),
            new Vector3(10.5f, 4f, 0.18f), new Color(0.91f, 0.84f, 0.74f));
        CreateBox(architecture.transform, "Left Wall", new Vector3(-5.25f, 2f, 0f),
            new Vector3(0.18f, 4f, 7.7f), new Color(0.91f, 0.84f, 0.74f));

        GameObject furniture = Child(room, "Furniture (move each item freely)");
        PlaceSized("Sofa", furniture.transform, new Vector3(-3.45f, 0f, 2.85f), 180f,
            new Vector3(2.45f, 0.90f, 1.02f), false);

        GameObject table = PlaceSized("Table", furniture.transform, new Vector3(1.55f, 0f, -0.35f), 0f,
            new Vector3(2.70f, 0.76f, 1.40f), false);
        // Pull the left-front chair out far enough to read as an intentional
        // floor -> seat -> tabletop jump route in the gameplay camera.
        GameObject chair01 = PlaceSized("Chair 01", furniture.transform, new Vector3(0.62f, 0f, -1.90f), 180f,
            new Vector3(0.58f, 0.90f, 0.62f), false, "Chair");
        GameObject chair02 = PlaceSized("Chair 02", furniture.transform, new Vector3(2.28f, 0f, -1.62f), 180f,
            new Vector3(0.58f, 0.90f, 0.62f), false, "Chair");
        GameObject chair03 = PlaceSized("Chair 03", furniture.transform, new Vector3(1.55f, 0f, 0.92f), 0f,
            new Vector3(0.58f, 0.90f, 0.62f), false, "Chair");
        ConfigureChairColliders(chair01);
        ConfigureChairColliders(chair02);
        ConfigureChairColliders(chair03);

        float tableTop = GetBounds(table).max.y + 0.008f;
        PlaceSized("MacBook", furniture.transform, new Vector3(1.00f, tableTop, -0.25f),
            -10f, new Vector3(0.54f, 0.31f, 0.38f), true, "Laptop", false);
        // Keep the cup close enough to the laptop to communicate the spill
        // opportunity while leaving a clear approach lane for the cat.
        PlaceSized("Cup", furniture.transform, new Vector3(1.47f, tableTop, -0.20f), 0f,
            new Vector3(0.15f, 0.19f, 0.15f), true, null, false);
        // The first tutorial target sits close to the front edge so one paw
        // push gives immediate, legible feedback.
        PlaceSized("iPad", furniture.transform, new Vector3(2.30f, tableTop, -0.88f),
            12f, new Vector3(0.28f, 0.025f, 0.40f), true, "Tablet", false);

        ConfigureWoodMaterials(floor, table, chair01, chair02, chair03);

        // v3.4 art direction: serialize every shelf piece, prop and soft furnishing
        // as an independent hierarchy object so artists can freely move or restyle it.
        LivingRoomArtInstaller.BuildInRoom(room.transform);

        // Lighting is installed after this scene save by its independent menu command.
        // Keeping the rig outside "Living Room" means rebuilding art never deletes it.

        Selection.activeGameObject = room;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Living Room built: all furniture is separate and editable in the Hierarchy.");
    }

    private static void ConfigureWoodMaterials(GameObject floor, GameObject table,
        params GameObject[] chairs)
    {
        EnsureAssetFolder(MaterialFolder);
        string softenedFloorTexture = CreateTonedTexture(FloorTexturePath,
            MaterialFolder + "/Floor_LowContrast.png", new Color(0.43f, 0.27f, 0.18f), 0.22f);
        string lightTableTexture = CreateTonedTexture(TableTexturePath,
            MaterialFolder + "/Furniture_LightOak.png", new Color(0.73f, 0.50f, 0.29f), 0.26f);
        string lightChairTexture = CreateTonedTexture(ChairTexturePath,
            MaterialFolder + "/Chair_LightOak.png", new Color(0.69f, 0.46f, 0.27f), 0.26f);
        Material floorMaterial = CreateOrUpdateWoodMaterial(
            MaterialFolder + "/Floor_LowContrast.mat", softenedFloorTexture,
            new Color(0.78f, 0.68f, 0.61f), new Vector2(0.72f, 0.72f), 0.16f);
        Material lightOak = CreateOrUpdateWoodMaterial(
            MaterialFolder + "/Furniture_LightOak.mat", lightTableTexture,
            new Color(1.00f, 0.96f, 0.90f), new Vector2(0.62f, 0.62f), 0.23f);
        Material chairOak = CreateOrUpdateWoodMaterial(
            MaterialFolder + "/Chair_LightOak.mat", lightChairTexture,
            new Color(0.98f, 0.94f, 0.88f), new Vector2(0.66f, 0.66f), 0.21f);

        AssignMaterial(floor, floorMaterial);
        AssignMaterial(table, lightOak);
        foreach (GameObject chair in chairs)
            AssignMaterial(chair, chairOak);
    }

    private static string CreateTonedTexture(string sourcePath, string outputPath,
        Color baseTone, float retainedContrast)
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        if (source == null)
            throw new InvalidOperationException($"Missing wood texture: {sourcePath}");

        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;
        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        Color[] pixels = readable.GetPixels();
        float average = 0f;
        for (int index = 0; index < pixels.Length; index++)
            average += pixels[index].grayscale;
        average /= Mathf.Max(1, pixels.Length);
        for (int index = 0; index < pixels.Length; index++)
        {
            float grain = (pixels[index].grayscale - average) * retainedContrast;
            Color toned = new Color(baseTone.r + grain, baseTone.g + grain,
                baseTone.b + grain, pixels[index].a);
            pixels[index] = new Color(Mathf.Clamp01(toned.r), Mathf.Clamp01(toned.g),
                Mathf.Clamp01(toned.b), toned.a);
        }
        readable.SetPixels(pixels);
        readable.Apply();
        File.WriteAllBytes(outputPath, readable.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(readable);

        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(outputPath) is TextureImporter importer)
        {
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return outputPath;
    }

    private static Material CreateOrUpdateWoodMaterial(string assetPath, string texturePath,
        Color tint, Vector2 textureScale, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader is unavailable.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
            throw new InvalidOperationException($"Missing wood texture: {texturePath}");
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", tint);
        material.SetTextureScale("_BaseMap", textureScale);
        material.SetFloat("_Smoothness", smoothness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AssignMaterial(GameObject target, Material material)
    {
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
                materials[index] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[index]);
            current = next;
        }
    }

    private static GameObject Place(string name, Transform parent, Vector3 floorPosition,
        float yaw, float targetLargestSize, bool scoreItem, string assetName = null)
    {
        assetName ??= name;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{assetName}.fbx");
        if (asset == null)
            throw new InvalidOperationException($"Missing living-room model: {assetName}.fbx");

        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = floorPosition;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
        visual.name = "Model";

        Bounds bounds = GetBounds(visual);
        float largest = Mathf.Max(bounds.size.x, bounds.size.z);
        visual.transform.localScale *= targetLargestSize / Mathf.Max(0.001f, largest);
        bounds = GetBounds(visual);
        visual.transform.position += new Vector3(-bounds.center.x + floorPosition.x,
            floorPosition.y - bounds.min.y, -bounds.center.z + floorPosition.z);

        BoxCollider collider = root.AddComponent<BoxCollider>();
        bounds = GetBounds(visual);
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
        collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        if (scoreItem)
        {
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = name == "Cup" ? 0.25f : 0.8f;
            ScoreItem score = root.AddComponent<ScoreItem>();
            score.Points = name == "Cup" ? 20 : 80;
            score.DisplayName = name;
        }
        return root;
    }

    private static GameObject PlaceSized(string name, Transform parent, Vector3 floorPosition,
        float yaw, Vector3 targetSize, bool scoreItem, string assetName = null, bool preciseCollider = true)
    {
        assetName ??= name;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{assetName}.fbx");
        if (asset == null)
            throw new InvalidOperationException($"Missing living-room model: {assetName}.fbx");

        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = floorPosition;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
        visual.name = "Model";

        Bounds bounds = GetBounds(visual);
        Vector3 worldSizeScale = new Vector3(
            targetSize.x / Mathf.Max(0.001f, bounds.size.x),
            targetSize.y / Mathf.Max(0.001f, bounds.size.y),
            targetSize.z / Mathf.Max(0.001f, bounds.size.z));
        // FBX roots are imported with a -90 degree X rotation, so their local
        // Y/Z scale axes map to world Z/Y. Convert the requested world-size
        // ratios back onto the model's local axes instead of stretching the
        // wrong dimension.
        Vector3 localSizeScale = new Vector3(
            RatioForAxis(visual.transform.right, worldSizeScale),
            RatioForAxis(visual.transform.up, worldSizeScale),
            RatioForAxis(visual.transform.forward, worldSizeScale));
        visual.transform.localScale = Vector3.Scale(visual.transform.localScale, localSizeScale);
        bounds = GetBounds(visual);
        visual.transform.position += new Vector3(-bounds.center.x + floorPosition.x,
            floorPosition.y - bounds.min.y, -bounds.center.z + floorPosition.z);

        if (preciseCollider)
        {
            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
            }
        }
        else
        {
            BoxCollider collider = root.AddComponent<BoxCollider>();
            bounds = GetBounds(visual);
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        if (scoreItem)
        {
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = name == "Cup" ? 0.25f : 0.8f;
            ScoreItem score = root.AddComponent<ScoreItem>();
            score.Points = name == "Cup" ? 20 : 80;
            score.DisplayName = name;
        }
        return root;
    }

    private static float RatioForAxis(Vector3 localAxisInWorld, Vector3 worldRatios)
    {
        Vector3 axis = new Vector3(Mathf.Abs(localAxisInWorld.x),
            Mathf.Abs(localAxisInWorld.y), Mathf.Abs(localAxisInWorld.z));
        if (axis.x >= axis.y && axis.x >= axis.z) return worldRatios.x;
        if (axis.y >= axis.z) return worldRatios.y;
        return worldRatios.z;
    }

    private static void ConfigureChairColliders(GameObject chair)
    {
        // The imported chair mesh includes a thin horizontal strip along the
        // top of the backrest. A MeshCollider makes that strip a valid landing
        // platform, which looks as if the cat is balancing on empty space.
        // Keep collision on the seat and legs only; the backrest is visual.
        foreach (Collider collider in chair.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);

        CreateChairBox(chair.transform, "Seat Collider", new Vector3(0f, 0.43f, 0f),
            new Vector3(0.52f, 0.10f, 0.52f));
        foreach (float x in new[] { -0.22f, 0.22f })
        foreach (float z in new[] { -0.22f, 0.22f })
            CreateChairBox(chair.transform, "Leg Collider", new Vector3(x, 0.20f, z),
                new Vector3(0.07f, 0.40f, 0.07f));
    }

    private static void CreateChairBox(Transform chair, string name, Vector3 localPosition, Vector3 size)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(chair, false);
        part.transform.localPosition = localPosition;
        BoxCollider collider = part.AddComponent<BoxCollider>();
        collider.size = size;
    }

    private static Bounds GetBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static GameObject Child(GameObject parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        SetColor(box, color);
    }

    private static void SetColor(GameObject target, Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        target.GetComponent<Renderer>().sharedMaterial = material;
    }
}
