using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WindowPlantFinish
{
    private const string Folder = "Assets/Art/LivingRoom/WindowNook";
    private const string PrefabPath = Folder + "/WarmWindowNook.prefab";
    private const string VaseName = "Window white porcelain vase";

    [MenuItem("Catastrophe/Window Nook/Green Leaves and White Porcelain Vase")]
    public static void Apply()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/LivingRoom.unity")
            throw new InvalidOperationException("Open LivingRoom in Edit mode first.");

        GameObject sceneNook = GameObject.Find("Warm window nook");
        if (sceneNook == null) throw new InvalidOperationException("Window nook not found in the scene.");
        Material porcelain = Porcelain();
        Mesh vaseMesh = VaseMesh();

        // The authored scene and the reusable nook prefab are both maintained;
        // no other plant or furniture material is changed.
        AddVase(sceneNook.transform, porcelain, vaseMesh, true);
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            AddVase(prefab.transform, porcelain, vaseMesh, false);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("WINDOW_PLANT_FINISH_SAVED: green leaves retained; white ceramic cover pot fitted over the original green pot in scene and prefab.");
    }

    public static void AddVase(Transform nook, Material porcelain = null, Mesh vaseMesh = null, bool undo = false)
    {
        Transform plant = nook.Find("Window green plant");
        if (plant == null) throw new InvalidOperationException("Window green plant not found.");
        Transform existing = plant.Find(VaseName);
        Renderer[] renderers = plant.GetComponentsInChildren<Renderer>()
            .Where(r => existing == null || !r.transform.IsChildOf(existing)).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("Window plant has no mesh.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        GameObject pot = existing == null ? new GameObject(VaseName) : existing.gameObject;
        if (undo && existing == null) Undo.RegisterCreatedObjectUndo(pot, "Add white porcelain vase under window");
        // The plant is pushable, so its ceramic cover must travel with it.
        pot.transform.SetParent(plant, false);
        pot.transform.position = new Vector3(bounds.center.x, bounds.min.y + .004f, bounds.center.z);
        pot.transform.rotation = Quaternion.identity;
        // Pothos has a single green mesh/material. This ceramic shell masks only
        // the green source pot while its existing leaves remain fully visible.
        float height = Mathf.Clamp(bounds.size.y * .38f, .19f, .23f);
        float radius = Mathf.Clamp(bounds.size.y * .35f, .19f, .22f);
        Vector3 parentScale = plant.lossyScale;
        pot.transform.localScale = new Vector3(radius / parentScale.x,
            height / parentScale.y, radius / parentScale.z);
        MeshFilter filter = pot.GetComponent<MeshFilter>();
        if (filter == null) filter = pot.AddComponent<MeshFilter>();
        filter.sharedMesh = vaseMesh != null ? vaseMesh : VaseMesh();
        MeshRenderer renderer = pot.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = pot.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = porcelain != null ? porcelain : Porcelain();
        if (pot.GetComponent<SoftDecoration>() == null) pot.AddComponent<SoftDecoration>();
    }

    private static Material Porcelain()
    {
        const string path = Folder + "/WindowWhitePorcelain.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", new Color(.98f, .975f, .95f));
        material.SetFloat("_Smoothness", .72f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh VaseMesh()
    {
        const string path = Folder + "/WindowPorcelainVase.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        const int sides = 40;
        // Normalized pottery profile: tapered foot, rounded shoulder, rolled rim,
        // and a short inset inner wall, rather than a solid cylinder.
        Vector2[] profile = {
            new(.76f, 0f), new(.87f, .07f), new(.97f, .70f),
            new(1f, .88f), new(1.02f, .95f), new(.97f, 1f),
            new(.88f, .97f), new(.84f, .76f), new(.80f, .14f), new(0f, .14f)
        };
        Vector3[] vertices = new Vector3[profile.Length * sides];
        int[] triangles = new int[(profile.Length - 1) * sides * 6];
        for (int j = 0; j < profile.Length; j++)
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[j * sides + i] = new Vector3(Mathf.Cos(angle) * profile[j].x,
                    profile[j].y, Mathf.Sin(angle) * profile[j].x);
            }
        int t = 0;
        for (int j = 0; j < profile.Length - 1; j++)
            for (int i = 0; i < sides; i++)
            {
                int a = j * sides + i, b = j * sides + (i + 1) % sides;
                int c = (j + 1) * sides + i, d = (j + 1) * sides + (i + 1) % sides;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        Mesh mesh = new Mesh { name = "Window Porcelain Vase" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
