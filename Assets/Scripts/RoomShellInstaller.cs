using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class RoomShellInstaller
{
    public const float RoomWidth = 8.8f;
    public const float RoomDepth = 6.6f;
    public const float RoomHeight = 4.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeInstall() => EnsureInstalled();

    public static GameObject EnsureInstalled()
    {
        GameObject existing = GameObject.Find("Four Wall Room Shell (editable)");
        if (existing != null)
        {
            SanitizeMaterials(existing);
            return existing;
        }
        GameObject room = GameObject.Find("Living Room");
        if (room == null) return null;

        Material wall = Material(new Color(0.98f, 0.965f, 0.93f));
        Material trim = Material(new Color(0.98f, 0.96f, 0.91f));
        Material wood = Material(new Color(0.57f, 0.38f, 0.22f));
        Material glass = Material(new Color(0.52f, 0.76f, 0.86f));
        ConfigureBaseWall("Back Wall", new Vector3(0f, RoomHeight * .5f, RoomDepth * .5f),
            new Vector3(RoomWidth, RoomHeight, .18f), wall);
        ConfigureBaseWall("Left Wall", new Vector3(-RoomWidth * .5f, RoomHeight * .5f, 0f),
            new Vector3(.18f, RoomHeight, RoomDepth), wall);
        GameObject root = new("Four Wall Room Shell (editable)");
        root.transform.SetParent(room.transform, false);

        // Right wall with a 2.4 x 1.7 m window opening.
        Part(root.transform, "Right Wall - lower", new(RoomWidth * .5f, .60f, 0), new(.18f, 1.20f, RoomDepth), wall);
        Part(root.transform, "Right Wall - upper", new(RoomWidth * .5f, 3.45f, 0), new(.18f, 1.10f, RoomDepth), wall);
        Part(root.transform, "Right Wall - front pier", new(RoomWidth * .5f, 2.0f, -2.25f), new(.18f, 2.90f, 2.10f), wall);
        Part(root.transform, "Right Wall - rear pier", new(RoomWidth * .5f, 2.0f, 2.25f), new(.18f, 2.90f, 2.10f), wall);
        // Tripo_Curtain supplies the window dressing; keep the wall opening clear
        // instead of generating the old primitive placeholder.

        // Front wall with a framed door opening.
        Part(root.transform, "Front Wall - left", new(-3.15f, 2f, -RoomDepth * .5f), new(2.50f, 4f, .18f), wall);
        Part(root.transform, "Front Wall - right", new(1.25f, 2f, -RoomDepth * .5f), new(6.30f, 4f, .18f), wall);
        Part(root.transform, "Front Wall - door lintel", new(-1.05f, 3.55f, -RoomDepth * .5f), new(1.30f, .9f, .18f), wall);
        Part(root.transform, "Door", new(-1.05f, 1.5f, -RoomDepth * .5f + .09f), new(1.18f, 3f, .12f), wood);
        Part(root.transform, "Door frame left", new(-1.70f, 1.55f, -RoomDepth * .5f + .23f), new(.12f, 3.2f, .14f), trim);
        Part(root.transform, "Door frame right", new(-.40f, 1.55f, -RoomDepth * .5f + .23f), new(.12f, 3.2f, .14f), trim);
        Part(root.transform, "Door frame top", new(-1.05f, 3.12f, -RoomDepth * .5f + .23f), new(1.42f, .12f, .14f), trim);

        // Matching skirting, split around the door.
        Part(root.transform, "Right skirting", new(RoomWidth * .5f - .13f, .12f, 0), new(.12f, .24f, RoomDepth), trim);
        Part(root.transform, "Front skirting left", new(-3.15f, .12f, -RoomDepth * .5f + .15f), new(2.50f, .24f, .12f), trim);
        Part(root.transform, "Front skirting right", new(1.25f, .12f, -RoomDepth * .5f + .15f), new(6.30f, .24f, .12f), trim);

        // The sofa wall is reserved for the owner's cat portrait.
        SanitizeMaterials(root);
        return root;
    }

    private static void SanitizeMaterials(GameObject root)
    {
        Material wall = Material(new Color(0.98f, 0.965f, 0.93f));
        Material trim = Material(new Color(0.98f, 0.96f, 0.91f));
        Material wood = Material(new Color(0.57f, 0.38f, 0.22f));
        Material glass = Material(new Color(0.52f, 0.76f, 0.86f));
        Material curtain = Material(new Color(0.88f, 0.82f, 0.72f));
        Material paper = Material(new Color(.94f,.90f,.78f));
        Material leaf = Material(new Color(.30f,.56f,.30f));
        Material frame = Material(new Color(.62f,.46f,.29f));

        AssignSceneMaterial("Back Wall", wall);
        AssignSceneMaterial("Left Wall", wall);
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            // Explicitly authored room finishes must survive the legacy
            // placeholder cleanup on scene load. Keep its other safeguards.
            if (HasAuthoredFinish(renderer)) continue;
            string n = renderer.name.ToLowerInvariant();
            if (n.Contains("curtain")) renderer.sharedMaterial = curtain;
            else if (n.Contains("window glass")) renderer.sharedMaterial = glass;
            else if (n.Contains("wall")) renderer.sharedMaterial = wall;
            else if (n.Contains("skirting") || n.Contains("window frame") || n.Contains("door frame")) renderer.sharedMaterial = trim;
            else if (n == "door") renderer.sharedMaterial = wood;
            else if (n == "frame") renderer.sharedMaterial = frame;
            else if (n == "paper") renderer.sharedMaterial = paper;
            else if (n == "leaf") renderer.sharedMaterial = leaf;
        }
    }

    private static void AssignSceneMaterial(string objectName, Material material)
    {
        Renderer renderer = GameObject.Find(objectName)?.GetComponent<Renderer>();
        if (renderer != null && !HasAuthoredFinish(renderer)) renderer.sharedMaterial = material;
    }

    private static bool HasAuthoredFinish(Renderer renderer) => renderer.sharedMaterial != null &&
        renderer.sharedMaterial.name.StartsWith("Storybook_", System.StringComparison.Ordinal);

    private static void ConfigureBaseWall(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject wall = GameObject.Find(objectName);
        if (wall == null) return;
        wall.transform.position = position;
        wall.transform.rotation = Quaternion.identity;
        wall.transform.localScale = scale;
        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        wall.isStatic = true;
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
