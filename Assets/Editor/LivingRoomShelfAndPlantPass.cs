using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Bakes independent mesh/material overrides; never rebuilds the room or moves its furniture roots.
public static class LivingRoomShelfAndPlantPass
{
    const string Folder = "Assets/Settings/LivingRoom/ReferenceShelf";
    static readonly float[] SourceTops = { .222f, .499f, .755f, 1f };
    static readonly float[] Tops = { .38f, .95f, 1.52f, 2.09f };
    static readonly string[] Props = { "Tripo Book Stack", "Shelf Mug 01", "Shelf Books 02",
        "Shelf Cup 02", "Shelf Plant 02", "Shelf Books 03", "Desktop Globe", "Lucky Cat" };

    [MenuItem("Catastrophe/Reference Room/Apply Wide Shelf and Plant Finishes")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/LivingRoom.unity")
            throw new InvalidOperationException("Open LivingRoom and exit Play first.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        GameObject shelf = GameObject.Find("Tripo Shelving Unit");
        if (shelf == null) throw new InvalidOperationException("Missing Tripo Shelving Unit.");
        Undo.RegisterFullObjectHierarchyUndo(shelf, "Widen shelf and separate finishes");
        MeshFilter filter = shelf.GetComponentInChildren<MeshFilter>();
        Mesh source = SourceMesh(filter, "Shelf");
        Bounds before = WorldBounds(source, filter.transform);
        // Keep the scene anchor and TV clearance; grow into the freed cat-tree bay.
        float centerX = -.25f;
        // Keep the back against the same wall. Extra depth extends into the room.
        float back = before.max.z;
        const float width = 3.40f, depth = .82f;
        float front = back - depth;
        Vector3[] vertices = source.vertices;
        Vector2[] uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 w = filter.transform.TransformPoint(vertices[i]);
            float nx = Mathf.InverseLerp(before.min.x, before.max.x, w.x);
            float nz = Mathf.InverseLerp(before.min.z, before.max.z, w.z);
            float ny = Mathf.InverseLerp(before.min.y, before.max.y, w.y);
            w = new Vector3(centerX + (nx - .5f) * width, MapShelfHeight(ny), front + nz * depth);
            uv[i] = new Vector2(w.x * 1.2f, w.z * 3f);
            vertices[i] = filter.transform.InverseTransformPoint(w);
        }
        Mesh baked = Object.Instantiate(source);
        baked.name = "Wide shelf - oak boards and charcoal frame";
        baked.vertices = vertices;
        baked.uv = uv;
        Split(baked, tri =>
        {
            Vector3 p = filter.transform.TransformPoint((vertices[tri[0]] + vertices[tri[1]] + vertices[tri[2]]) / 3f);
            bool board = Tops.Any(top => p.y >= top - .065f && p.y <= top + .009f);
            bool upright = Mathf.Abs(p.x - centerX) > width * .43f;
            return board && !upright ? 0 : 1;
        }, 2);
        baked.RecalculateNormals(); baked.RecalculateBounds();
        filter.sharedMesh = SaveMesh(baked, "Shelf_Finished");
        Material wood = Material("HoneyOak", new Color(.80f, .62f, .39f), .23f);
        wood.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Settings/LivingRoom/ReferenceMaterials/FineOakGrain.png"));
        Material metal = Material("CharcoalMetal", new Color(.065f, .074f, .078f), .28f);
        metal.SetFloat("_Metallic", .25f);
        filter.GetComponent<Renderer>().sharedMaterials = new[] { wood, metal };
        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        PrefabUtility.RecordPrefabInstancePropertyModifications(filter.GetComponent<Renderer>());

        foreach (Collider c in shelf.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        for (int i = 0; i < Tops.Length; i++)
            Box(shelf, $"Climbable Shelf Platform {i + 1:00}",
                new Vector3(centerX, Tops[i] - .023f, (front + back) * .5f), new Vector3(width - .14f, .046f, depth - .035f));
        for (int side = -1; side <= 1; side += 2)
        for (int end = 0; end < 2; end++)
            Box(shelf, $"Shelf frame collision {side} {end}",
                new Vector3(centerX + side * (width * .5f - .04f), 1.045f, end == 0 ? front + .045f : back - .045f),
                new Vector3(.065f, 2.09f, .065f));

        MoveBottom("Tripo Cat Tree", new Vector3(-3.0f, 0f, 2.70f));
        MoveBottom("Tripo Pothos Plant", new Vector3(-3.65f, 0f, -1.0f));
        // Half-height front landings let the cat leave the low ceiling before
        // jumping to the next board. No increase to global jump power is needed.
        for (int i = 0; i < 7; i++)
        {
            float top = (i + 1) * .285f;
            Vector3 stepCenter = new(centerX - width * .5f + .24f + i * .42f, top - .023f, front - .35f);
            Box(shelf, $"Cat access step {i + 1}", stepCenter, new Vector3(.40f, .046f, .56f));
            Transform step = shelf.transform.Find($"Cat access step {i + 1}");
            AddCubeVisual(step, wood);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 a = new(centerX - width * .5f + .10f, .075f, front - .35f + side * .24f);
            Vector3 b = new(centerX - width * .5f + .24f + 6 * .42f, 1.89f, a.z);
            Box(shelf, $"Cat access frame {side}", (a + b) * .5f, new Vector3(.028f, Vector3.Distance(a, b), .028f));
            Transform rail = shelf.transform.Find($"Cat access frame {side}");
            rail.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            AddCubeVisual(rail, metal);
        }

        Material foliage = Material("LeafGreen", new Color(.23f, .43f, .115f), .24f);
        Material cream = Material("IvoryPlanter", new Color(.91f, .84f, .69f), .14f);
        Material soil = Material("PotSoil", new Color(.15f, .105f, .055f), .04f);
        foreach (string name in new[] { "Tripo Pothos Plant", "Shelf Plant 02" })
        {
            GameObject plant = GameObject.Find(name);
            if (plant == null) throw new InvalidOperationException("Missing plant: " + name);
            Undo.RegisterFullObjectHierarchyUndo(plant, "Separate planter and foliage");
            MeshFilter pf = plant.GetComponentInChildren<MeshFilter>();
            Mesh original = SourceMesh(pf, name.Replace(" ", "_"));
            Bounds pb = WorldBounds(original, pf.transform);
            Mesh pm = Object.Instantiate(original);
            Vector3[] pv = original.vertices;
            Split(pm, tri =>
            {
                Vector3 p = pf.transform.TransformPoint((pv[tri[0]] + pv[tri[1]] + pv[tri[2]]) / 3f);
                float height = (p.y - pb.min.y) / pb.size.y;
                float radius = new Vector2((p.x - pb.center.x) / pb.size.x, (p.z - pb.center.z) / pb.size.z).magnitude;
                if (height < .535f && radius < .30f)
                    return height > .508f && radius < .22f ? 2 : 1;
                return 0;
            }, 3);
            pf.sharedMesh = SaveMesh(pm, name.Replace(" ", "_") + "_Finished");
            pf.GetComponent<Renderer>().sharedMaterials = new[] { foliage, cream, soil };
            PrefabUtility.RecordPrefabInstancePropertyModifications(pf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pf.GetComponent<Renderer>());
        }

        // Rear display row, leaving a 0.45 m-wide front lane for the 0.40 m cat capsule.
        for (int i = 0; i < Props.Length; i++)
        {
            GameObject prop = GameObject.Find(Props[i]);
            if (prop == null) continue;
            Undo.RegisterFullObjectHierarchyUndo(prop, "Rest small prop on actual shelf");
            int level = i / 2;
            Bounds b = BoundsOf(prop);
            float maxDepth = .27f, maxHeight = level == 3 ? .38f : .32f;
            float scale = Mathf.Min(1f, maxDepth / b.size.z, maxHeight / b.size.y, .48f / b.size.x);
            prop.transform.localScale *= scale;
            b = BoundsOf(prop);
            Vector3 target = new(centerX + (i % 2 == 0 ? -.56f : .56f), Tops[level] + .008f, back - .16f);
            prop.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
        }
        Physics.SyncTransforms();
        EditorUtility.SetDirty(shelf);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Capture();
        Validate();
        Debug.Log("REFERENCE_SHELF_APPLIED: root unchanged; 3.40m width / .82m depth; four aligned walkable boards, half-height access steps; segmented plant finishes.");
    }

    static void MoveBottom(string name, Vector3 bottom)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) throw new InvalidOperationException("Missing " + name);
        Undo.RecordObject(go.transform, "Move aside for wide shelf");
        Bounds b = BoundsOf(go);
        go.transform.position += new Vector3(bottom.x - b.center.x, bottom.y - b.min.y, bottom.z - b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }
    static void AddCubeVisual(Transform parent, Material material)
    {
        Transform visual = parent.Find("Visible tread");
        if (visual == null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = "Visible tread"; cube.transform.SetParent(parent, false); visual = cube.transform;
        }
        visual.localScale = parent.GetComponent<BoxCollider>().size;
        visual.GetComponent<Renderer>().sharedMaterial = material;
    }

    static float MapShelfHeight(float y)
    {
        // Preserve thin boards instead of stretching them together with their gaps.
        float oldStart = 0, newStart = 0;
        for (int i = 0; i < Tops.Length; i++)
        {
            float bottom = SourceTops[i] - .039f;
            if (y < bottom) return Mathf.Lerp(newStart, Tops[i] - .046f, Mathf.InverseLerp(oldStart, bottom, y));
            if (y <= SourceTops[i]) return Mathf.Lerp(Tops[i] - .046f, Tops[i], Mathf.InverseLerp(bottom, SourceTops[i], y));
            oldStart = SourceTops[i]; newStart = Tops[i];
        }
        return Tops[3];
    }

    static Mesh SourceMesh(MeshFilter filter, string id)
    {
        string path = Folder + "/" + id + "_Source.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = Object.Instantiate(filter.sharedMesh); AssetDatabase.CreateAsset(mesh, path); }
        return mesh;
    }
    static Mesh SaveMesh(Mesh mesh, string id)
    {
        string path = Folder + "/" + id + ".asset";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); return saved;
    }
    static void Split(Mesh mesh, Func<int[], int> category, int count)
    {
        int[] triangles = mesh.triangles;
        var buckets = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int[] tri = { triangles[i], triangles[i + 1], triangles[i + 2] };
            buckets[category(tri)].AddRange(tri);
        }
        mesh.subMeshCount = count;
        for (int i = 0; i < count; i++) mesh.SetTriangles(buckets[i], i);
    }
    static Material Material(string name, Color color, float smoothness)
    {
        string path = Folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m); return m;
    }
    static void Box(GameObject root, string name, Vector3 center, Vector3 size)
    {
        Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        GameObject go = found == null ? new GameObject(name) : found.gameObject;
        go.transform.SetParent(root.transform, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.identity;
        Vector3 scale = root.transform.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.center = Vector3.zero; box.size = size; box.enabled = true; box.isTrigger = false;
        go.isStatic = true;
    }
    static Bounds WorldBounds(Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        Bounds b = new(transform.TransformPoint(vertices[0]), Vector3.zero);
        foreach (Vector3 v in vertices) b.Encapsulate(transform.TransformPoint(v));
        return b;
    }
    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    [MenuItem("Catastrophe/Reference Room/Validate Wide Shelf Walkways")]
    public static void Validate()
    {
        Physics.SyncTransforms();
        GameObject shelf = GameObject.Find("Tripo Shelving Unit");
        Bounds b = shelf.GetComponentInChildren<MeshFilter>().GetComponent<Renderer>().bounds;
        int support = 0, blocked = 0;
        foreach (float top in Tops)
        for (int i = 0; i <= 8; i++)
        {
            Vector3 foot = new(b.center.x - 1.35f + i * .3375f, top + .014f, b.min.z + .245f);
            if (Physics.Raycast(foot + Vector3.up * .05f, Vector3.down, out RaycastHit hit, .09f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.name.StartsWith("Climbable Shelf Platform")) support++;
            Collider[] overlaps = Physics.OverlapCapsule(foot + Vector3.up * .201f, foot + Vector3.up * .299f, .199f, ~0, QueryTriggerInteraction.Ignore);
            if (overlaps.Any(c => !c.GetComponentInParent<CatController>()))
            {
                blocked++;
                Debug.Log($"SHELF_BLOCKED foot={foot}: {string.Join(",", overlaps.Select(c => c.name))}");
            }
        }
        Directory.CreateDirectory("Validation/ReferenceShelf");
        string report = $"Shelf size={b.size}; support={support}/36; blocked={blocked}/36; 4 x 2.70m sampled walkways\n";
        File.WriteAllText("Validation/ReferenceShelf/checks.txt", report);
        if (support != 36 || blocked != 0) throw new InvalidOperationException(report);
        Debug.Log("REFERENCE_SHELF_PASS " + report);
    }

    [MenuItem("Catastrophe/Reference Room/Capture Shelf and Plant")]
    public static void Capture()
    {
        Directory.CreateDirectory("Validation/ReferenceShelf");
        Shot("Shelf", new Vector3(2f, 2.65f, -2.6f), new Vector3(-.25f, 1.15f, 2.8f));
        GameObject plant = GameObject.Find("Tripo Pothos Plant");
        Bounds b = BoundsOf(plant);
        Shot("Plant", b.center + new Vector3(1f, .45f, -1.5f), b.center);
    }
    static void Shot(string name, Vector3 position, Vector3 target)
    {
        GameObject go = new("Temporary reference capture");
        Camera c = go.AddComponent<Camera>(); c.transform.position = position; c.transform.LookAt(target);
        c.fieldOfView = 42; c.nearClipPlane = .03f;
        RenderTexture rt = new(1400, 1100, 24); c.targetTexture = rt;
        RenderTexture old = RenderTexture.active;
        try
        {
            c.Render(); RenderTexture.active = rt;
            Texture2D image = new(1400, 1100, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1400, 1100), 0, 0); image.Apply();
            File.WriteAllBytes($"Validation/ReferenceShelf/{name}.png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = old; c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
    }
}
