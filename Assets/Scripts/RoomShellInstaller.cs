using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class RoomShellInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeInstall() => EnsureInstalled();

    public static GameObject EnsureInstalled()
    {
        GameObject existing = GameObject.Find("Four Wall Room Shell (editable)");
        if (existing != null) return existing;
        GameObject room = GameObject.Find("Living Room");
        if (room == null) return null;

        Renderer source = GameObject.Find("Back Wall")?.GetComponent<Renderer>();
        Material wall = source != null ? source.sharedMaterial : Material(new Color(0.88f, 0.82f, 0.70f));
        Material trim = Material(new Color(0.94f, 0.90f, 0.82f));
        Material wood = Material(new Color(0.46f, 0.25f, 0.12f));
        Material glass = Material(new Color(0.52f, 0.76f, 0.86f));
        Material curtain = Material(new Color(0.72f, 0.27f, 0.20f));
        GameObject root = new("Four Wall Room Shell (editable)");
        root.transform.SetParent(room.transform, false);

        // Right wall with a 2.4 x 1.7 m window opening.
        Part(root.transform, "Right Wall - lower", new(5.25f, .65f, 0), new(.18f, 1.3f, 7.7f), wall);
        Part(root.transform, "Right Wall - upper", new(5.25f, 3.45f, 0), new(.18f, 1.1f, 7.7f), wall);
        Part(root.transform, "Right Wall - front pier", new(5.25f, 2f, -2.55f), new(.18f, 2.8f, 2.6f), wall);
        Part(root.transform, "Right Wall - rear pier", new(5.25f, 2f, 2.55f), new(.18f, 2.8f, 2.6f), wall);
        Transform window = new GameObject("Window and Curtains - replaceable Tripo 16").transform;
        window.SetParent(root.transform, false); window.localPosition = new(5.12f, 2.05f, 0);
        Part(window, "Window glass", Vector3.zero, new(.05f, 1.62f, 2.35f), glass);
        Part(window, "Window frame top", new(-.05f, .86f, 0), new(.14f, .10f, 2.55f), trim);
        Part(window, "Window frame bottom", new(-.05f, -.86f, 0), new(.14f, .10f, 2.55f), trim);
        Part(window, "Window frame centre", new(-.06f, 0, 0), new(.14f, 1.72f, .09f), trim);
        Part(window, "Curtain front", new(-.22f, 0, -1.47f), new(.16f, 2.65f, .48f), curtain);
        Part(window, "Curtain rear", new(-.22f, 0, 1.47f), new(.16f, 2.65f, .48f), curtain);

        // Front wall with a framed door opening.
        Part(root.transform, "Front Wall - left", new(-3.55f, 2f, -3.85f), new(3.4f, 4f, .18f), wall);
        Part(root.transform, "Front Wall - right", new(1.45f, 2f, -3.85f), new(7.6f, 4f, .18f), wall);
        Part(root.transform, "Front Wall - door lintel", new(-1.05f, 3.55f, -3.85f), new(1.6f, .9f, .18f), wall);
        Part(root.transform, "Door", new(-1.05f, 1.5f, -3.76f), new(1.38f, 3f, .12f), wood);
        Part(root.transform, "Door frame left", new(-1.80f, 1.55f, -3.62f), new(.12f, 3.2f, .14f), trim);
        Part(root.transform, "Door frame right", new(-.30f, 1.55f, -3.62f), new(.12f, 3.2f, .14f), trim);
        Part(root.transform, "Door frame top", new(-1.05f, 3.12f, -3.62f), new(1.62f, .12f, .14f), trim);

        // Matching skirting, split around the door.
        Part(root.transform, "Right skirting", new(5.12f, .12f, 0), new(.12f, .24f, 7.7f), trim);
        Part(root.transform, "Front skirting left", new(-3.55f, .12f, -3.70f), new(3.4f, .24f, .12f), trim);
        Part(root.transform, "Front skirting right", new(1.45f, .12f, -3.70f), new(7.6f, .24f, .12f), trim);

        // Two independent plant prints (replaceable with Tripo prompt 8 output).
        Painting(root.transform, new(-3.55f, 2.15f, 3.72f), "Plant Print 01", new(.22f,.52f,.28f));
        Painting(root.transform, new(-2.35f, 2.35f, 3.72f), "Plant Print 02", new(.38f,.62f,.32f));
        return root;
    }

    private static void Painting(Transform p, Vector3 pos, string name, Color leaf)
    {
        Transform g = new GameObject(name + " - replaceable Tripo 08").transform; g.SetParent(p, false); g.localPosition = pos;
        Part(g, "Frame", Vector3.zero, new(.78f, 1.0f, .08f), Material(new Color(.50f,.29f,.14f)));
        Part(g, "Paper", new(0,0,-.05f), new(.64f,.84f,.025f), Material(new Color(.94f,.90f,.78f)));
        Part(g, "Leaf", new(0,.05f,-.075f), new(.30f,.48f,.02f), Material(leaf));
    }

    private static GameObject Part(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name; o.transform.SetParent(parent, false);
        o.transform.localPosition = pos; o.transform.localScale = scale; o.GetComponent<Renderer>().sharedMaterial = mat; o.isStatic = true; return o;
    }

    private static Material Material(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
#if UNITY_EDITOR
        const string folder = "Assets/Settings/LivingRoom/RoomShellMaterials";
        EnsureEditorFolder(folder);
        string path = $"{folder}/RoomShell_{ColorUtility.ToHtmlStringRGB(c)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = c;
        EditorUtility.SetDirty(material);
        return material;
#else
        return new Material(shader) { color = c };
#endif
    }

#if UNITY_EDITOR
    private static void EnsureEditorFolder(string path)
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
#endif
}
