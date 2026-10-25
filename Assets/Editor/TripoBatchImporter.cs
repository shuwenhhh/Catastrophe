#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TripoBatchImporter
{
    private const string FbxFolder = "Assets/Models/Tripo/FBX";
    private const string PrefabFolder = "Assets/Models/Tripo/Prefabs";
    private const string MaterialFolder = "Assets/Models/Tripo/Materials";
    private const string ScenePath = "Assets/LivingRoom.unity";

    private sealed class Spec
    {
        public string id;
        public bool valuable;
        public int chaos;
        public float weight;
        public float noise;
        public Color fallback;
        public Spec(string id, bool valuable, int chaos, float weight, Color fallback, float noise = -1f)
        { this.id = id; this.valuable = valuable; this.chaos = chaos; this.weight = weight; this.fallback = fallback; this.noise = noise; }
    }

    private static readonly Spec[] Specs =
    {
        new("BookStack", true, 15, 1.2f, new Color(0.34f, 0.48f, 0.43f)),
        new("CatTree", false, 0, 0, new Color(0.94f, 0.865f, 0.74f)),
        new("Chair", false, 0, 0, new Color(0.76f, 0.57f, 0.36f)),
        new("Cup", true, 20, 1.0f, new Color(0.93f, 0.86f, 0.72f)),
        new("Curtain", false, 0, 0, new Color(0.88f, 0.82f, 0.72f)),
        new("DesktopGlobe", true, 70, 0.45f, new Color(0.26f, 0.55f, 0.78f), 24f),
        new("Floor", false, 0, 0, new Color(0.78f, 0.61f, 0.42f)),
        new("FlowerVase", true, 40, 2.4f, new Color(0.78f, 0.24f, 0.34f)),
        new("HideCardboard", false, 0, 0, new Color(0.54f, 0.35f, 0.19f)),
        new("Laptop", true, 50, 2.2f, new Color(0.16f, 0.18f, 0.22f)),
        new("LuckyCat", true, 80, 0.45f, new Color(0.95f, 0.88f, 0.70f), 24f),
        new("Mug", true, 20, 1.0f, new Color(0.20f, 0.42f, 0.66f)),
        new("NavySideboard", false, 0, 0, new Color(0.08f, 0.20f, 0.38f)),
        new("PothosPlant", true, 35, 2.0f, new Color(0.20f, 0.48f, 0.24f)),
        new("ShelvingUnit", false, 0, 0, new Color(0.39f, 0.23f, 0.13f)),
        new("Sofa", false, 0, 0, new Color(0.91f, 0.86f, 0.76f)),
        new("Table", false, 0, 0, new Color(0.77f, 0.58f, 0.36f)),
        new("TableLamp", true, 25, 1.6f, new Color(0.94f, 0.63f, 0.24f)),
        new("Tablet", true, 35, 1.2f, new Color(0.12f, 0.16f, 0.22f)),
        new("Television", true, 80, 3.0f, new Color(0.06f, 0.07f, 0.09f)),
        new("TvCabinet", false, 0, 0, new Color(0.73f, 0.56f, 0.39f)),
    };

    // Import/rebuild is deliberately explicit. Running FixAllMaterials on editor
    // startup also rebuilt LivingRoom and erased hand-authored layout/finishes.

    [MenuItem("Catastrophe/Import All Tripo Models and Rebuild Room")]
    public static void ImportAndBuild()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (Spec spec in Specs) ConfigureAndBuildPrefab(spec);
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("TRIPO_BATCH_IMPORT_COMPLETE models=21 prefabs=21 scene=LivingRoom");
    }

    [MenuItem("Catastrophe/Fix All Tripo Materials (Textures + URP)")]
    public static void FixAllMaterials()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (Spec spec in Specs) ConfigureAndBuildPrefab(spec);
        BuildScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TRIPO_MATERIAL_FIX_COMPLETE models=21 shader=URP_Lit");
    }

    [MenuItem("Catastrophe/Reference Room/Rebuild Layout Only")]
    public static void RebuildReferenceLayout()
    {
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("REFERENCE_ROOM_LAYOUT_REBUILT");
    }

    private static void ConfigureAndBuildPrefab(Spec spec)
    {
        string fbxPath = $"{FbxFolder}/Tripo_{spec.id}.fbx";
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException($"Missing Tripo FBX: {fbxPath}");
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        GameObject root = new($"Tripo {Pretty(spec.id)}");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        model.name = "Mesh";
        model.transform.SetParent(root.transform, false);
        ApplyUrpMaterials(model, spec);
        AddBoundsCollider(root, model, spec.valuable);

        if (spec.valuable)
        {
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = spec.weight;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            Valuable valuable = root.AddComponent<Valuable>();
            valuable.itemName = Pretty(spec.id);
            valuable.chaosValue = spec.chaos;
            valuable.weight = spec.weight;
            valuable.noise = spec.noise >= 0f ? spec.noise : Mathf.Clamp(spec.chaos * 0.65f, 12f, 45f);
            valuable.fragile = spec.id is "Cup" or "Mug" or "FlowerVase" or "LuckyCat";
            ScoreItem score = root.AddComponent<ScoreItem>();
            score.Points = spec.chaos;
            score.DisplayName = valuable.itemName;
            score.Noise = valuable.noise;
            score.Weight = spec.weight;
        }
        else SetStaticRecursively(root);

        if (spec.id == "Laptop") DetailedLaptopBuilder.Apply(root);
        if (spec.id is "DesktopGlobe" or "LuckyCat")
        {
            string painted = spec.id == "DesktopGlobe" ? "ColoredGlobe" : "ColoredLuckyCat";
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/LivingRoom/ColoredShelfProps/{painted}.fbx") != null &&
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LivingRoom/ColoredShelfProps/IvoryPorcelain.mat") != null)
                ColoredShelfPropsPass.Install(root, painted);
        }

        string prefabPath = $"{PrefabFolder}/Tripo_{spec.id}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void ApplyUrpMaterials(GameObject model, Spec spec)
    {
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        Shader vertexShader = Shader.Find("Catastrophe/Tripo Vertex Color Lit");
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            bool usesVertexColors = RendererHasVertexColors(renderer);
            bool forceReferencePalette = spec.id is "Floor" or "Sofa" or "CatTree" or "Chair" or
                "Table" or "Curtain" or "TableLamp" or "BookStack" or "TvCabinet";
            Shader shader = usesVertexColors && !forceReferencePalette && vertexShader != null ? vertexShader : urpShader;
            Material[] originals = renderer.sharedMaterials;
            Material[] replacements = new Material[Mathf.Max(1, originals.Length)];
            for (int materialIndex = 0; materialIndex < replacements.Length; materialIndex++)
            {
                Material original = materialIndex < originals.Length ? originals[materialIndex] : null;
                string path = $"{MaterialFolder}/Tripo_{spec.id}_{rendererIndex}_{materialIndex}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = $"Tripo_{spec.id}_{rendererIndex}_{materialIndex}" };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                Texture texture = spec.id == "TvCabinet"
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Settings/LivingRoom/ReferenceMaterials/FineOakGrain.png")
                    : forceReferencePalette ? null : original != null ? original.mainTexture : null;
                if (texture == null && !forceReferencePalette)
                {
                    string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { $"{FbxFolder}/Textures/Tripo_{spec.id}" });
                    if (textureGuids.Length > 0)
                        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(textureGuids[0]));
                }
                if (texture != null) material.SetTexture("_BaseMap", texture);
                else material.SetTexture("_BaseMap", null);
                material.SetTextureScale("_BaseMap", spec.id == "Floor"
                    ? new Vector2(0.65f, 0.65f)
                    : Vector2.one);
                // Vertex colour assets must stay neutral so their authored colour
                // is not multiplied darker. Textured/fallback assets use the room
                // palette as a deliberate tint, keeping floor and furniture values
                // distinct instead of accepting Tripo's inconsistent white defaults.
                material.SetColor("_BaseColor", usesVertexColors && texture == null && !forceReferencePalette
                    ? Color.white
                    : spec.fallback);
                material.SetFloat("_Smoothness", 0.28f);
                EditorUtility.SetDirty(material);
                replacements[materialIndex] = material;
            }
            renderer.sharedMaterials = replacements;
        }
    }

    private static bool RendererHasVertexColors(Renderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null && renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        return mesh != null && mesh.colors != null && mesh.colors.Length == mesh.vertexCount;
    }

    private static void AddBoundsCollider(GameObject root, GameObject model, bool valuable)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        Bounds world = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) world.Encapsulate(renderer.bounds);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = root.transform.InverseTransformPoint(world.center);
        Vector3 size = root.transform.InverseTransformVector(world.size);
        collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }

    private static void BuildScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (Transform existingRoot in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(t => t.name == "V3.4 Room Dressing" || t.name == "Tripo Art Pass (editable)").ToArray())
            UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);
        foreach (string old in new[] { "Floor", "Sofa", "Table", "Chair 01", "Chair 02", "Chair 03", "MacBook", "iPad", "Cup" })
        {
            GameObject found = GameObject.Find(old);
            if (found != null) UnityEngine.Object.DestroyImmediate(found);
        }

        GameObject oldShell = GameObject.Find("Four Wall Room Shell (editable)");
        if (oldShell != null) UnityEngine.Object.DestroyImmediate(oldShell);
        RoomShellInstaller.EnsureInstalled();

        GameObject root = new("Tripo Art Pass (editable)");
        // Keep the imported floor source in the hierarchy, but hide its incomplete,
        // dark-red mesh.  The source has large cut-outs, which exposed a white
        // safety plane around the sofa.  A full-room light-oak surface below gives
        // one continuous colour and one continuous walkable level.
        GameObject visualFloor = Place(root.transform, "Floor", new Vector3(0f, -0.214f, 0f), 0f, "Floor");
        AlignTop(visualFloor, 0f);
        foreach (Renderer renderer in visualFloor.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        foreach (Collider collider in visualFloor.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        CreateUniformFloor(root.transform);
        GameObject gameplayFloor = new("Gameplay Floor Collider - full room");
        gameplayFloor.transform.SetParent(root.transform, false);
        gameplayFloor.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        BoxCollider gameplayFloorCollider = gameplayFloor.AddComponent<BoxCollider>();
        gameplayFloorCollider.size = new Vector3(RoomShellInstaller.RoomWidth, 0.12f, RoomShellInstaller.RoomDepth);
        gameplayFloor.isStatic = true;

        // Reference layout: cream sofa on the left wall, central dining/rug
        // cluster, tall display shelf and TV zone across the back wall.
        GameObject sofa = PlaceCenter(root.transform, "Sofa", new Vector3(-3.72f, 0.45f, 0.95f), 90f, "Sofa");
        ScaleBoundsToSize(sofa, new Vector3(0.96f, 0.90f, 2.42f));
        AlignBoundsCenter(sofa, new Vector3(-3.72f, 0.45f, 0.95f));
        AlignBottom(sofa, 0f);
        GameObject table = PlaceCenter(root.transform, "Table", new Vector3(0f, 0.38f, -0.55f), 0f, "Table");
        ScaleBoundsToSize(table, new Vector3(2.35f, 0.76f, 1.35f));
        AlignBoundsCenter(table, new Vector3(0f, 0.38f, -0.55f));
        GameObject chair01 = Place(root.transform, "Chair", new Vector3(0f, 0f, -1.65f), 180f, "Chair 01");
        GameObject chair02 = Place(root.transform, "Chair", new Vector3(1.48f, 0f, -0.55f), -90f, "Chair 02");
        GameObject chair03 = Place(root.transform, "Chair", new Vector3(-1.48f, 0f, -0.55f), 90f, "Chair 03");
        ScaleBoundsToSize(chair01, new Vector3(0.58f, 0.88f, 0.58f));
        ScaleBoundsToSize(chair02, new Vector3(0.58f, 0.88f, 0.58f));
        ScaleBoundsToSize(chair03, new Vector3(0.58f, 0.88f, 0.58f));
        AlignBoundsXZ(chair01, 0f, -1.65f);
        AlignBoundsXZ(chair02, 1.48f, -0.55f);
        AlignBoundsXZ(chair03, -1.48f, -0.55f);
        AlignBottom(chair01, 0f);
        AlignBottom(chair02, 0f);
        AlignBottom(chair03, 0f);

        CreateReferenceRug(root.transform);

        GameObject shelving = PlaceCenter(root.transform, "ShelvingUnit", new Vector3(0.35f, 1.05f, 3.02f), 180f);
        ScaleBoundsToSize(shelving, new Vector3(1.72f, 2.10f, 0.48f));
        AlignBoundsCenter(shelving, new Vector3(0.35f, 1.05f, 3.02f));
        ConfigureClimbableShelf(shelving);
        GameObject tvCabinet = PlaceCenter(root.transform, "TvCabinet", new Vector3(2.65f, 0.32f, 2.93f), 180f);
        ScaleBoundsToSize(tvCabinet, new Vector3(2.15f, 0.64f, 0.52f));
        AlignBoundsCenter(tvCabinet, new Vector3(2.65f, 0.32f, 2.93f));
        GameObject navySideboard = PlaceCenter(root.transform, "NavySideboard", new Vector3(3.98f, 0.32f, 0.50f), -90f);
        ScaleToHeight(navySideboard, 0.64f);
        AlignBoundsCenter(navySideboard, new Vector3(3.98f, 0.32f, 0.50f));
        GameObject catTree = PlaceCenter(root.transform, "CatTree", new Vector3(-1.45f, 0.78f, 2.70f), 180f);
        ScaleBoundsToSize(catTree, new Vector3(1.15f, 1.56f, 0.92f));
        AlignBoundsCenter(catTree, new Vector3(-1.45f, 0.78f, 2.70f));
        GameObject television = PlaceCenter(root.transform, "Television", new Vector3(2.65f, 1.24f, 3.08f), 180f);
        ScaleBoundsToSize(television, new Vector3(1.45f, 0.82f, 0.12f));
        AlignBoundsCenter(television, new Vector3(2.65f, 1.24f, 3.08f));
        GameObject tableLamp = PlaceCenter(root.transform, "TableLamp", new Vector3(3.35f, 0.87f, 2.68f), 180f);
        ScaleToHeight(tableLamp, 0.46f);
        AlignBoundsCenter(tableLamp, new Vector3(3.35f, 0.87f, 2.68f));
        GameObject curtain = PlaceCenter(root.transform, "Curtain", new Vector3(4.31f, 1.95f, 0f), 0f);
        ScaleBoundsToSize(curtain, new Vector3(0.16f, 2.55f, 2.40f));
        AlignBoundsCenter(curtain, new Vector3(4.31f, 1.95f, 0f));
        GameObject hideCardboard = Place(root.transform, "HideCardboard", new Vector3(3.55f, 0f, -2.42f), 15f);
        ScaleToHeight(hideCardboard, 0.50f);
        AlignBoundsXZ(hideCardboard, 3.55f, -2.42f);
        AlignBottom(hideCardboard, 0f);

        // Table props: bottom pivots sit just above the 76 cm tabletop.
        // Leave generous collider clearance between every prop. Tripo meshes often
        // have wider bounds than their visible silhouette (handles/leaves/screens).
        GameObject pinkVase = PlaceCenter(root.transform, "FlowerVase", new Vector3(0.25f, 0.97f, -0.48f), 0f, "Pink Vase");
        pinkVase.transform.localScale = Vector3.one * 0.42f;
        AlignBottom(pinkVase, 0.76f);
        Place(root.transform, "Laptop", new Vector3(-0.65f, 0.76f, -0.52f), 195f, "MacBook");
        Place(root.transform, "Cup", new Vector3(0.72f, 0.76f, -0.30f), 0f, "Cup");
        Place(root.transform, "Mug", new Vector3(0.98f, 0.76f, -0.65f), 15f, "Cup 2");
        GameObject tablet = PlaceCenter(root.transform, "Tablet", new Vector3(-0.10f, 0.79f, -0.83f), 12f, "iPad");
        tablet.transform.localScale = Vector3.one * 1.15f;
        AlignCenterY(tablet, 0.79f);

        // Larger shelf styling: every prop remains an independent scene object.
        // The vertical spacing matches the invisible climb platforms so the cat
        // can work upward and reach the globe/lucky cat instead of facing a solid box.
        PlaceCenter(root.transform, "BookStack", new Vector3(-0.05f, 0.40f, 2.82f), 180f);
        PlaceCenter(root.transform, "PothosPlant", new Vector3(-3.10f, 0.39f, 2.76f), 180f);
        PlaceCenter(root.transform, "Mug", new Vector3(0.75f, 0.52f, 2.82f), 180f, "Shelf Mug 01");
        PlaceCenter(root.transform, "BookStack", new Vector3(0.02f, 0.93f, 2.82f), 170f, "Shelf Books 02");
        PlaceCenter(root.transform, "Cup", new Vector3(0.76f, 1.00f, 2.82f), 180f, "Shelf Cup 02");
        PlaceCenter(root.transform, "PothosPlant", new Vector3(0.05f, 1.42f, 2.78f), 190f, "Shelf Plant 02");
        PlaceCenter(root.transform, "BookStack", new Vector3(0.70f, 1.48f, 2.82f), 185f, "Shelf Books 03");
        GameObject desktopGlobe = PlaceCenter(root.transform, "DesktopGlobe", new Vector3(0.02f, 1.84f, 2.83f), 185f, "Desktop Globe");
        ScaleToHeight(desktopGlobe, 0.32f);
        AlignBottom(desktopGlobe, 1.68f);
        GameObject luckyCat = PlaceCenter(root.transform, "LuckyCat", new Vector3(0.67f, 1.84f, 2.83f), 175f, "Lucky Cat");
        ScaleToHeight(luckyCat, 0.32f);
        AlignBottom(luckyCat, 1.68f);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    private static void CreateUniformFloor(Transform parent)
    {
        GameObject floorRoot = new("Uniform Light Oak Floor");
        floorRoot.transform.SetParent(parent, false);
        Material oak = ReferenceMaterial("LightOakFloor", new Color(0.88f, 0.735f, 0.54f), 0.24f);
        Material seam = ReferenceMaterial("LightOakSeam", new Color(0.70f, 0.55f, 0.38f), 0.12f);
        DecorativeCube(floorRoot.transform, "Continuous oak surface", new Vector3(0f, -0.04f, 0f),
            new Vector3(RoomShellInstaller.RoomWidth, 0.08f, RoomShellInstaller.RoomDepth), oak);

        const float plankDepth = 0.55f;
        int rows = Mathf.CeilToInt(RoomShellInstaller.RoomDepth / plankDepth);
        float firstZ = -RoomShellInstaller.RoomDepth * .5f;
        for (int row = 0; row <= rows; row++)
        {
            float z = Mathf.Min(RoomShellInstaller.RoomDepth * .5f, firstZ + row * plankDepth);
            DecorativeCube(floorRoot.transform, $"Oak seam horizontal {row:00}", new Vector3(0f, .001f, z),
                new Vector3(RoomShellInstaller.RoomWidth, .002f, .004f), seam);
        }

        for (int row = 0; row < rows; row++)
        {
            float z = firstZ + row * plankDepth + plankDepth * .5f;
            float offset = row % 2 == 0 ? -2.20f : -1.10f;
            for (float x = offset; x < RoomShellInstaller.RoomWidth * .5f; x += 2.20f)
            {
                if (x <= -RoomShellInstaller.RoomWidth * .5f + .05f) continue;
                DecorativeCube(floorRoot.transform, $"Oak seam stagger {row:00}", new Vector3(x, .001f, z),
                    new Vector3(.004f, .002f, plankDepth), seam);
            }
        }
    }

    private static void CreateReferenceRug(Transform parent)
    {
        GameObject rug = new("Large Cream Woven Rug");
        rug.transform.SetParent(parent, false);
        Material border = ReferenceMaterial("RugBorder", new Color(0.77f, 0.69f, 0.57f), 0.05f);
        Material centre = ReferenceMaterial("RugCream", new Color(0.88f, 0.82f, 0.70f), 0.05f);
        DecorativeCube(rug.transform, "Woven border", new Vector3(0f, .012f, -.52f),
            new Vector3(4.25f, .024f, 2.95f), border);
        DecorativeCube(rug.transform, "Woven centre", new Vector3(0f, .027f, -.52f),
            new Vector3(3.90f, .012f, 2.60f), centre);
    }

    private static void ConfigureClimbableShelf(GameObject shelf)
    {
        foreach (Collider collider in shelf.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        Bounds bounds = BoundsOfRenderers(shelf);
        float[] platformTops = { 0.38f, 0.82f, 1.25f, 1.68f };
        for (int index = 0; index < platformTops.Length; index++)
        {
            float top = platformTops[index];
            GameObject step = new($"Climbable Shelf Platform {index + 1:00}");
            step.transform.SetParent(shelf.transform, false);
            Vector3 worldCenter = new(bounds.center.x, top - .04f, bounds.center.z - .03f);
            step.transform.position = worldCenter;
            step.transform.rotation = shelf.transform.rotation;
            Vector3 scale = shelf.transform.lossyScale;
            BoxCollider collider = step.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                1.56f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                .08f / Mathf.Max(.001f, Mathf.Abs(scale.y)),
                .52f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            step.isStatic = true;
        }
    }

    private static GameObject DecorativeCube(Transform parent, string name, Vector3 position, Vector3 scale,
        Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.isStatic = true;
        return cube;
    }

    private static Material ReferenceMaterial(string name, Color color, float smoothness)
    {
        const string folder = "Assets/Settings/LivingRoom/ReferenceMaterials";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings/LivingRoom"))
                throw new InvalidOperationException("LivingRoom settings folder is missing.");
            AssetDatabase.CreateFolder("Assets/Settings/LivingRoom", "ReferenceMaterials");
        }
        string path = $"{folder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", null);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Bounds BoundsOfRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException($"{root.name} has no renderer.");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static GameObject Place(Transform parent, string id, Vector3 position, float yaw, string name = null)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Tripo_{id}.prefab");
        if (prefab == null) throw new InvalidOperationException($"Missing Tripo prefab: {id}");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name ?? $"Tripo {Pretty(id)}";
        instance.transform.SetParent(parent, true);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        AlignBottom(instance, position.y);
        return instance;
    }

    private static GameObject PlaceCenter(Transform parent, string id, Vector3 centerPosition, float yaw, string name = null)
    {
        GameObject instance = Place(parent, id, new Vector3(centerPosition.x, 0f, centerPosition.z), yaw, name);
        AlignCenterY(instance, centerPosition.y);
        return instance;
    }

    private static void ScaleToHeight(GameObject instance, float targetHeight)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        float factor = targetHeight / Mathf.Max(0.001f, bounds.size.y);
        instance.transform.localScale *= factor;
    }

    private static void AlignBottom(GameObject instance, float desiredBottomY)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        instance.transform.position += Vector3.up * (desiredBottomY - bounds.min.y);
    }

    private static void AlignCenterY(GameObject instance, float desiredCenterY)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        instance.transform.position += Vector3.up * (desiredCenterY - bounds.center.y);
    }

    private static void AlignBoundsCenter(GameObject instance, Vector3 desiredCenter)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        instance.transform.position += desiredCenter - bounds.center;
    }

    private static void AlignBoundsXZ(GameObject instance, float desiredX, float desiredZ)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        instance.transform.position += new Vector3(desiredX - bounds.center.x, 0f, desiredZ - bounds.center.z);
    }

    private static void ScaleBoundsToSize(GameObject instance, Vector3 targetSize)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        Vector3 scale = instance.transform.localScale;
        if (bounds.size.x > 0.001f) scale.x *= targetSize.x / bounds.size.x;
        if (bounds.size.y > 0.001f) scale.y *= targetSize.y / bounds.size.y;
        if (bounds.size.z > 0.001f) scale.z *= targetSize.z / bounds.size.z;
        instance.transform.localScale = scale;
    }

    private static void AlignTop(GameObject instance, float desiredTopY)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        instance.transform.position += Vector3.up * (desiredTopY - bounds.max.y);
    }

    private static string Pretty(string id) => System.Text.RegularExpressions.Regex.Replace(id, "([a-z])([A-Z])", "$1 $2");

    private static void SetStaticRecursively(GameObject root)
    {
        root.isStatic = true;
        foreach (Transform child in root.transform) SetStaticRecursively(child.gameObject);
    }
}
#endif
