using UnityEngine;

/// <summary>
/// Adds the v3.4 lived-in art pass to the editable LivingRoom scene.
/// Everything is made from simple, dependable geometry so the dressing remains
/// visible even when optional art packages are unavailable.
/// </summary>
public sealed class LivingRoomArtInstaller : MonoBehaviour
{
    private static readonly Color WarmWood = new(0.58f, 0.33f, 0.16f);
    private static readonly Color DarkMetal = new(0.055f, 0.065f, 0.075f);
    private static readonly Color Cream = new(0.93f, 0.86f, 0.72f);
    private static readonly Color Leaf = new(0.18f, 0.48f, 0.25f);
    private static readonly Color AccentBlue = new(0.12f, 0.36f, 0.62f);
    private static readonly Color AccentRed = new(0.78f, 0.12f, 0.09f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("V3.4 Room Dressing") != null || GameObject.Find("Tripo Art Pass (editable)") != null)
            return;

        GameObject room = GameObject.Find("Living Room");
        if (room == null)
            return;

        BuildInRoom(room.transform);
    }

    /// <summary>Shared by the editor scene builder so the same independent objects are serialized.</summary>
    public static GameObject BuildInRoom(Transform room)
    {
        GameObject root = new("V3.4 Room Dressing");
        root.transform.SetParent(room, false);
        BuildShelf(root.transform);
        BuildColourFurniture(root.transform);
        BuildDiningTableDecor(root.transform);
        BuildSoftFurnishings(root.transform);
        return root;
    }

    private static void BuildShelf(Transform root)
    {
        Transform shelf = Group(root, "Display Shelf - black metal and warm oak", new Vector3(2.55f, 0f, 3.48f));
        foreach (float x in new[] { -2.05f, 2.05f })
        {
            Part(shelf, "Black metal upright", PrimitiveType.Cube, new Vector3(x, 1.55f, 0f),
                new Vector3(0.10f, 3.10f, 0.12f), DarkMetal);
            Part(shelf, "Foot", PrimitiveType.Cube, new Vector3(x, 0.05f, -0.20f),
                new Vector3(0.34f, 0.10f, 0.52f), DarkMetal);
        }
        foreach (float y in new[] { 0.38f, 1.18f, 1.98f, 2.78f })
            Part(shelf, "Warm wood shelf", PrimitiveType.Cube, new Vector3(0f, y, 0f),
                new Vector3(4.25f, 0.13f, 0.62f), WarmWood);

        // Bottom: books, camera, mug, photo and a small lucky cat.
        Books(shelf, new Vector3(-1.62f, 0.72f, -0.01f), 4);
        CameraProp(shelf, new Vector3(-0.72f, 0.61f, -0.02f));
        Mug(shelf, "Shelf Mug", new Vector3(0.05f, 0.65f, -0.02f), new Color(0.95f, 0.55f, 0.20f), 20);
        PictureFrame(shelf, new Vector3(0.75f, 0.68f, 0f), 35);
        LuckyCat(shelf, new Vector3(1.55f, 0.77f, 0f));

        // Middle shelves intentionally alternate silhouettes and heights.
        Plant(shelf, new Vector3(-1.62f, 1.48f, 0f), 45);
        Books(shelf, new Vector3(-0.75f, 1.52f, 0f), 5);
        Globe(shelf, new Vector3(0.28f, 1.53f, 0f), 70);
        Controller(shelf, new Vector3(1.18f, 1.39f, -0.05f), 40);
        Mug(shelf, "Blue Shelf Mug", new Vector3(1.72f, 1.45f, 0f), AccentBlue, 20);

        PictureFrame(shelf, new Vector3(-1.66f, 2.29f, 0f), 35);
        CameraProp(shelf, new Vector3(-0.92f, 2.21f, 0f));
        Plant(shelf, new Vector3(0f, 2.30f, 0f), 50);
        Books(shelf, new Vector3(0.82f, 2.32f, 0f), 4);
        LuckyCat(shelf, new Vector3(1.68f, 2.37f, 0f));

        Globe(shelf, new Vector3(-1.63f, 3.10f, 0f), 80);
        Controller(shelf, new Vector3(-0.75f, 2.99f, 0f), 45);
        Books(shelf, new Vector3(0.12f, 3.10f, 0f), 5);
        PictureFrame(shelf, new Vector3(1.08f, 3.08f, 0f), 40);
        Mug(shelf, "Red Shelf Mug", new Vector3(1.78f, 3.05f, 0f), AccentRed, 25);
    }

    private static void BuildColourFurniture(Transform root)
    {
        Transform chair = Group(root, "Green Armchair", new Vector3(-4.15f, 0f, 0.55f));
        Part(chair, "Seat", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(1.15f, 0.28f, 1.05f), Leaf);
        Part(chair, "Back", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0.42f), new Vector3(1.15f, 0.90f, 0.22f), Leaf);
        Part(chair, "Left rounded arm", PrimitiveType.Capsule, new Vector3(-0.56f, 0.68f, 0f), new Vector3(0.24f, 0.36f, 0.24f), Leaf);
        Part(chair, "Right rounded arm", PrimitiveType.Capsule, new Vector3(0.56f, 0.68f, 0f), new Vector3(0.24f, 0.36f, 0.24f), Leaf);

        Transform cabinet = Group(root, "Blue Sideboard", new Vector3(4.35f, 0f, 1.80f));
        Part(cabinet, "Cabinet", PrimitiveType.Cube, new Vector3(0f, 0.58f, 0f), new Vector3(1.55f, 0.95f, 0.55f), AccentBlue);
        Part(cabinet, "Warm wood top", PrimitiveType.Cube, new Vector3(0f, 1.08f, 0f), new Vector3(1.68f, 0.12f, 0.64f), WarmWood);
        foreach (float x in new[] { -0.38f, 0.38f })
            Part(cabinet, "Brass knob", PrimitiveType.Sphere, new Vector3(x, 0.65f, -0.30f), Vector3.one * 0.08f, new Color(0.92f, 0.68f, 0.18f));

        Transform lamp = Group(cabinet, "Red Table Lamp", new Vector3(0.35f, 1.14f, 0f));
        Part(lamp, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.30f, 0.06f, 0.30f), AccentRed);
        Part(lamp, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.08f, 0.38f, 0.08f), AccentRed);
        GameObject shade = Part(lamp, "Red shade", PrimitiveType.Cylinder, new Vector3(0f, 0.80f, 0f), new Vector3(0.48f, 0.34f, 0.48f), AccentRed);
        shade.transform.localScale = new Vector3(0.48f, 0.34f, 0.48f);

        // A deliberately heavy, stable test prop for the 3–4 nudge interaction.
        // Its collider bottom sits 1 cm above the sideboard top, avoiding the
        // spawn penetration that used to launch the runtime-generated vase.
        ValuableGroup vase = Valuable(cabinet, "Pink Vase", new Vector3(-0.38f, 1.53f, 0f), 50);
        Part(vase, "Vase Body", PrimitiveType.Cylinder, Vector3.zero,
            new Vector3(0.28f, 0.38f, 0.28f), new Color(0.96f, 0.38f, 0.55f));
        Part(vase, "Vase Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f),
            new Vector3(0.15f, 0.12f, 0.15f), new Color(0.98f, 0.48f, 0.63f));
    }

    private static void BuildDiningTableDecor(Transform root)
    {
        // Authored table top is at roughly 0.76 m. These clusters read clearly from the low camera.
        Transform table = Group(root, "Dining Table Styling", Vector3.zero);
        Part(table, "Tea tray", PrimitiveType.Cylinder, new Vector3(1.35f, 0.80f, -0.18f), new Vector3(0.82f, 0.035f, 0.58f), Cream);
        ValuableGroup teapot = Valuable(table, "Tea Pot", new Vector3(1.20f, 0.98f, -0.18f), 45);
        Part(teapot, "Pot", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.34f, 0.27f, 0.34f), Cream);
        Part(teapot, "Lid", PrimitiveType.Sphere, new Vector3(0f, 0.20f, 0f), Vector3.one * 0.10f, AccentRed);
        Mug(table, "Tea Cup", new Vector3(1.62f, 1.04f, -0.18f), Cream, 20);

        Part(table, "Fruit Bowl", PrimitiveType.Sphere, new Vector3(0.78f, 0.86f, 0.20f), new Vector3(0.54f, 0.16f, 0.42f), new Color(0.35f, 0.62f, 0.70f));
        foreach (Vector3 p in new[] { new Vector3(0.65f, 0.98f, 0.20f), new Vector3(0.86f, 0.99f, 0.15f), new Vector3(0.78f, 1.02f, 0.32f) })
            Part(table, "Orange", PrimitiveType.Sphere, p, Vector3.one * 0.17f, new Color(1f, 0.40f, 0.04f));
        Part(table, "Banana", PrimitiveType.Capsule, new Vector3(0.94f, 1.03f, 0.25f), new Vector3(0.10f, 0.28f, 0.10f), new Color(1f, 0.82f, 0.10f), new Vector3(0f, 0f, 66f));

        Color[] lego = { AccentRed, new Color(1f, 0.76f, 0.05f), AccentBlue, Leaf };
        for (int i = 0; i < lego.Length; i++)
            Part(table, "Toy brick", PrimitiveType.Cube, new Vector3(1.75f + i * 0.15f, 0.84f + (i % 2) * 0.08f, 0.28f),
                new Vector3(0.22f, 0.10f, 0.13f), lego[i], new Vector3(0f, i * 18f, 0f));
    }

    private static void BuildSoftFurnishings(Transform root)
    {
        Part(root, "Large woven rug", PrimitiveType.Cube, new Vector3(-0.50f, 0.025f, 0.35f),
            new Vector3(4.70f, 0.05f, 3.10f), new Color(0.73f, 0.30f, 0.22f));
        Part(root, "Rug centre", PrimitiveType.Cube, new Vector3(-0.50f, 0.055f, 0.35f),
            new Vector3(4.20f, 0.025f, 2.60f), new Color(0.88f, 0.68f, 0.43f));

        Transform window = Group(root, "Window and Curtains", new Vector3(-1.15f, 0f, 3.66f));
        Part(window, "Window", PrimitiveType.Cube, new Vector3(0f, 2.15f, 0f), new Vector3(2.40f, 1.55f, 0.08f), new Color(0.55f, 0.78f, 0.88f));
        Part(window, "Cross bar H", PrimitiveType.Cube, new Vector3(0f, 2.15f, -0.06f), new Vector3(2.46f, 0.07f, 0.09f), Cream);
        Part(window, "Cross bar V", PrimitiveType.Cube, new Vector3(0f, 2.15f, -0.06f), new Vector3(0.07f, 1.60f, 0.09f), Cream);
        Part(window, "Curtain rod", PrimitiveType.Cylinder, new Vector3(0f, 3.05f, -0.12f), new Vector3(0.06f, 1.55f, 0.06f), DarkMetal, new Vector3(0f, 0f, 90f));
        Part(window, "Left curtain", PrimitiveType.Cube, new Vector3(-1.45f, 1.70f, -0.16f), new Vector3(0.52f, 2.65f, 0.13f), new Color(0.76f, 0.36f, 0.30f));
        Part(window, "Right curtain", PrimitiveType.Cube, new Vector3(1.45f, 1.70f, -0.16f), new Vector3(0.52f, 2.65f, 0.13f), new Color(0.76f, 0.36f, 0.30f));
    }

    private static void Books(Transform parent, Vector3 centre, int count)
    {
        Color[] colours = { AccentRed, AccentBlue, Leaf, new Color(0.95f, 0.66f, 0.15f), Cream };
        for (int i = 0; i < count; i++)
        {
            float height = 0.30f + 0.05f * (i % 3);
            Part(parent, "Book", PrimitiveType.Cube, centre + new Vector3(i * 0.13f, height * 0.5f, 0f),
                new Vector3(0.10f, height, 0.28f), colours[i % colours.Length], new Vector3(0f, 0f, (i - 2) * 2f));
        }
    }

    private static void CameraProp(Transform parent, Vector3 position)
    {
        ValuableGroup prop = Valuable(parent, "Retro Camera", position, 55);
        Part(prop, "Body", PrimitiveType.Cube, Vector3.zero, new Vector3(0.40f, 0.25f, 0.24f), DarkMetal);
        Part(prop, "Lens", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f), new Vector3(0.18f, 0.09f, 0.18f), AccentBlue, new Vector3(90f, 0f, 0f));
    }

    private static void Mug(Transform parent, string name, Vector3 position, Color colour, int points)
    {
        ValuableGroup prop = Valuable(parent, name, position, points);
        Part(prop, $"{name} Mesh", PrimitiveType.Cylinder, Vector3.zero,
            new Vector3(0.20f, 0.20f, 0.20f), colour);
        Part(prop, "Handle", PrimitiveType.Sphere, new Vector3(0.13f, 0.02f, 0f), new Vector3(0.12f, 0.13f, 0.08f), colour);
    }

    private static void PictureFrame(Transform parent, Vector3 position, int points)
    {
        ValuableGroup prop = Valuable(parent, "Family Photo Frame", position, points);
        Part(prop, "Frame", PrimitiveType.Cube, Vector3.zero, new Vector3(0.38f, 0.48f, 0.08f), new Color(0.88f, 0.63f, 0.18f));
        Part(prop, "Photo", PrimitiveType.Cube, new Vector3(0f, 0f, -0.05f), new Vector3(0.28f, 0.37f, 0.02f), new Color(0.72f, 0.83f, 0.78f));
    }

    private static void LuckyCat(Transform parent, Vector3 position)
    {
        ValuableGroup prop = Valuable(parent, "Lucky Cat", position, 80);
        Part(prop, "Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.24f, 0.30f, 0.24f), Cream);
        Part(prop, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.31f, 0f), Vector3.one * 0.27f, Cream);
        Part(prop, "Red collar", PrimitiveType.Cube, new Vector3(0f, 0.17f, -0.12f), new Vector3(0.24f, 0.05f, 0.04f), AccentRed);
    }

    private static void Plant(Transform parent, Vector3 position, int points)
    {
        ValuableGroup prop = Valuable(parent, "Trailing Plant", position, points);
        Part(prop, "Pot", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.28f, 0.23f, 0.28f), new Color(0.78f, 0.36f, 0.18f));
        for (int i = 0; i < 5; i++)
            Part(prop, "Leaf", PrimitiveType.Sphere, new Vector3((i - 2) * 0.08f, 0.22f + 0.08f * (i % 2), 0f), new Vector3(0.12f, 0.25f, 0.08f), Leaf, new Vector3(0f, 0f, (i - 2) * 17f));
    }

    private static void Globe(Transform parent, Vector3 position, int points)
    {
        ValuableGroup prop = Valuable(parent, "Desk Globe", position, points);
        Part(prop, "Base", PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f), new Vector3(0.28f, 0.07f, 0.28f), DarkMetal);
        Part(prop, "Globe", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0f), Vector3.one * 0.35f, new Color(0.17f, 0.58f, 0.72f));
    }

    private static void Controller(Transform parent, Vector3 position, int points)
    {
        ValuableGroup prop = Valuable(parent, "Switch Controller", position, points);
        Part(prop, "Console", PrimitiveType.Cube, Vector3.zero, new Vector3(0.38f, 0.10f, 0.20f), DarkMetal);
        Part(prop, "Blue JoyCon", PrimitiveType.Capsule, new Vector3(-0.23f, 0f, 0f), new Vector3(0.14f, 0.12f, 0.15f), new Color(0.04f, 0.72f, 0.88f));
        Part(prop, "Red JoyCon", PrimitiveType.Capsule, new Vector3(0.23f, 0f, 0f), new Vector3(0.14f, 0.12f, 0.15f), new Color(0.95f, 0.18f, 0.28f));
    }

    private static Transform Group(Transform parent, string name, Vector3 position)
    {
        GameObject group = new(name);
        group.transform.SetParent(parent, false);
        group.transform.localPosition = position;
        return group.transform;
    }

    private static ValuableGroup Valuable(Transform parent, string name, Vector3 position, int points)
    {
        GameObject root = new(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        return new ValuableGroup(root.transform, points);
    }

    private readonly struct ValuableGroup
    {
        public readonly Transform transform;
        public ValuableGroup(Transform value, int points)
        {
            transform = value;
            Rigidbody body = value.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.45f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            Valuable valuable = value.gameObject.AddComponent<Valuable>();
            valuable.itemName = value.name;
            valuable.chaosValue = points;
            valuable.noise = 24f;
            ScoreItem score = value.gameObject.AddComponent<ScoreItem>();
            score.Points = points;
            score.DisplayName = value.name;
        }
        public static implicit operator Transform(ValuableGroup value) => value.transform;
    }

    private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position,
        Vector3 scale, Color colour, Vector3? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        if (rotation.HasValue)
            part.transform.localEulerAngles = rotation.Value;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.material.color = colour;
        return part;
    }
}
