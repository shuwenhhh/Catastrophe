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
    private const string MaterialMigrationKey = "Catastrophe.TripoMaterialMigration.v3";
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
        public Color fallback;
        public Spec(string id, bool valuable, int chaos, float weight, Color fallback)
        { this.id = id; this.valuable = valuable; this.chaos = chaos; this.weight = weight; this.fallback = fallback; }
    }

    private static readonly Spec[] Specs =
    {
        new("BookStack", true, 15, 1.2f, new Color(0.64f, 0.25f, 0.17f)),
        new("CatTree", false, 0, 0, new Color(0.66f, 0.48f, 0.30f)),
        new("Chair", false, 0, 0, new Color(0.66f, 0.43f, 0.25f)),
        new("Cup", true, 20, 1.0f, new Color(0.93f, 0.86f, 0.72f)),
        new("Floor", false, 0, 0, new Color(0.46f, 0.27f, 0.16f)),
        new("FlowerVase", true, 40, 2.4f, new Color(0.78f, 0.24f, 0.34f)),
        new("Laptop", true, 50, 2.2f, new Color(0.16f, 0.18f, 0.22f)),
        new("Mug", true, 20, 1.0f, new Color(0.20f, 0.42f, 0.66f)),
        new("PothosPlant", true, 35, 2.0f, new Color(0.20f, 0.48f, 0.24f)),
        new("ShelvingUnit", false, 0, 0, new Color(0.39f, 0.23f, 0.13f)),
        new("Sofa", false, 0, 0, new Color(0.30f, 0.40f, 0.50f)),
        new("Table", false, 0, 0, new Color(0.79f, 0.56f, 0.31f)),
        new("TableLamp", true, 25, 1.6f, new Color(0.78f, 0.12f, 0.09f)),
        new("Tablet", true, 35, 1.2f, new Color(0.12f, 0.16f, 0.22f)),
        new("Television", true, 80, 3.0f, new Color(0.06f, 0.07f, 0.09f)),
        new("TvCabinet", false, 0, 0, new Color(0.12f, 0.36f, 0.62f)),
    };

    [InitializeOnLoadMethod]
    private static void RunMaterialMigrationOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(MaterialMigrationKey, false)) return;
            SessionState.SetBool(MaterialMigrationKey, true);
            FixAllMaterials();
        };
    }

    [MenuItem("Catastrophe/Import All Tripo Models and Rebuild Room")]
    public static void ImportAndBuild()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (Spec spec in Specs) ConfigureAndBuildPrefab(spec);
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("TRIPO_BATCH_IMPORT_COMPLETE models=16 prefabs=16 scene=LivingRoom");
    }

    [MenuItem("Catastrophe/Fix All Tripo Materials (Textures + URP)")]
    public static void FixAllMaterials()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (Spec spec in Specs) ConfigureAndBuildPrefab(spec);
        BuildScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TRIPO_MATERIAL_FIX_COMPLETE models=16 shader=URP_Lit");
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
            valuable.noise = Mathf.Clamp(spec.chaos * 0.65f, 12f, 45f);
            valuable.fragile = spec.id is "Cup" or "Mug" or "FlowerVase";
            ScoreItem score = root.AddComponent<ScoreItem>();
            score.Points = spec.chaos;
            score.DisplayName = valuable.itemName;
        }
        else SetStaticRecursively(root);

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
            Shader shader = usesVertexColors && vertexShader != null ? vertexShader : urpShader;
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
                Texture texture = original != null ? original.mainTexture : null;
                if (texture == null)
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
                material.SetColor("_BaseColor", usesVertexColors && texture == null ? Color.white : spec.fallback);
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
        // This Tripo floor includes a 21.4 cm slab; sink it so the walkable top remains exactly Y=0.
        GameObject visualFloor = Place(root.transform, "Floor", new Vector3(0f, -0.214f, 0f), 0f, "Floor");
        AlignTop(visualFloor, 0f);
        // The visual floor mesh is decorative and may have cut-outs or imperfect
        // topology. A separate full-room collider guarantees that the sofa zone
        // and every wall edge remain walkable without changing the visible asset.
        GameObject gameplayFloor = new("Gameplay Floor Collider - full room");
        gameplayFloor.transform.SetParent(root.transform, false);
        gameplayFloor.transform.localPosition = new Vector3(0f, -0.10f, 0f);
        BoxCollider gameplayFloorCollider = gameplayFloor.AddComponent<BoxCollider>();
        gameplayFloorCollider.size = new Vector3(10.5f, 0.20f, 7.7f);
        gameplayFloor.isStatic = true;
        GameObject sofa = Place(root.transform, "Sofa", new Vector3(-3.7f, 0f, 1.55f), 180f, "Sofa");
        sofa.transform.localScale = Vector3.one * 1.05f;
        AlignBottom(sofa, 0f);
        GameObject table = Place(root.transform, "Table", new Vector3(1.05f, 0f, -0.15f), 0f, "Table");
        // Tripo's authored table is a compact side table. Widen the scene instance
        // into a four-seat dining table while keeping the imported prefab metric.
        table.transform.localScale = new Vector3(2f, 1.19f, 2f);
        AlignBottom(table, 0f);
        GameObject chair01 = Place(root.transform, "Chair", new Vector3(1.05f, 0f, -1.90f), 180f, "Chair 01");
        GameObject chair02 = Place(root.transform, "Chair", new Vector3(2.25f, 0f, -0.25f), -90f, "Chair 02");
        GameObject chair03 = Place(root.transform, "Chair", new Vector3(-0.15f, 0f, -0.25f), 90f, "Chair 03");
        Vector3 chairScale = new(0.60f, 0.54f, 0.60f);
        chair01.transform.localScale = chairScale;
        chair02.transform.localScale = chairScale;
        chair03.transform.localScale = chairScale;
        AlignBottom(chair01, 0f);
        AlignBottom(chair02, 0f);
        AlignBottom(chair03, 0f);
        Place(root.transform, "ShelvingUnit", new Vector3(2.60f, 0f, 3.42f), 180f);
        Place(root.transform, "TvCabinet", new Vector3(-4.55f, 0f, 3.30f), 180f);
        Place(root.transform, "CatTree", new Vector3(-5.20f, 0f, -1.65f), 25f);
        Place(root.transform, "Television", new Vector3(-4.55f, 0.93f, 3.25f), 180f);
        Place(root.transform, "TableLamp", new Vector3(-4.05f, 0.93f, 3.12f), 180f);

        // Table props: bottom pivots sit just above the 76 cm tabletop.
        // Leave generous collider clearance between every prop. Tripo meshes often
        // have wider bounds than their visible silhouette (handles/leaves/screens).
        Place(root.transform, "FlowerVase", new Vector3(0.08f, 0.78f, -0.35f), 0f, "Pink Vase");
        Place(root.transform, "Laptop", new Vector3(0.78f, 0.78f, -0.36f), 200f, "MacBook");
        Place(root.transform, "Cup", new Vector3(1.24f, 0.78f, -0.08f), 0f, "Cup");
        Place(root.transform, "Mug", new Vector3(1.66f, 0.78f, 0.10f), 15f, "Cup 2");
        GameObject tablet = Place(root.transform, "Tablet", new Vector3(1.88f, 0.78f, -1.19f), 12f, "iPad");
        tablet.transform.localScale = Vector3.one * 1.15f;
        AlignBottom(tablet, 0.78f);

        // Shelf styling: independent roots, staggered silhouette and depth.
        Place(root.transform, "BookStack", new Vector3(1.65f, 0.43f, 3.13f), 180f);
        Place(root.transform, "PothosPlant", new Vector3(3.45f, 0.43f, 3.13f), 180f);
        Place(root.transform, "Mug", new Vector3(2.20f, 1.23f, 3.13f), 180f, "Shelf Mug 01");
        Place(root.transform, "BookStack", new Vector3(2.95f, 1.23f, 3.13f), 170f, "Shelf Books 02");
        Place(root.transform, "Cup", new Vector3(1.72f, 2.03f, 3.13f), 180f, "Shelf Cup 02");
        Place(root.transform, "PothosPlant", new Vector3(3.38f, 2.03f, 3.13f), 190f, "Shelf Plant 02");
        Place(root.transform, "BookStack", new Vector3(2.35f, 2.83f, 3.13f), 185f, "Shelf Books 03");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
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

    private static void AlignBottom(GameObject instance, float desiredBottomY)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        instance.transform.position += Vector3.up * (desiredBottomY - bounds.min.y);
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
