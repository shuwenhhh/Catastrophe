using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ReferenceRoomLayoutV2
{
    static readonly string[] ShelfProps = { "Tripo Book Stack", "Shelf Mug 01", "Shelf Books 02", "Shelf Cup 02", "Shelf Plant 02", "Shelf Books 03", "Desktop Globe", "Lucky Cat" };
    public static void ApplyFromBatch()
    {
        EditorSceneManager.OpenScene("Assets/LivingRoom.unity"); Apply(); Inspect();
    }
    [MenuItem("Catastrophe/Reference Room/Apply Reference Layout V2")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/LivingRoom.unity") throw new InvalidOperationException("Exit Play and open LivingRoom first.");
        foreach (GameObject root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "Reference layout V2");
        GameObject shelf = Need("Tripo Shelving Unit");
        // Keep every independent prop in shelf-local coordinates while rotating the entire display.
        Matrix4x4 old = shelf.transform.worldToLocalMatrix;
        var positions = ShelfProps.ToDictionary(n => n, n => old.MultiplyPoint3x4(Need(n).transform.position));
        Quaternion oldRotation = shelf.transform.rotation;
        Move("Tripo Shelving Unit", new Vector3(3.75f,0,.40f), 90, true);
        foreach (Collider oldSolid in shelf.GetComponents<Collider>()) oldSolid.enabled = false;
        Bounds sb = shelf.GetComponentInChildren<MeshFilter>().GetComponent<Renderer>().bounds;
        float[] tops = { .38f,.95f,1.52f,2.09f };
        for (int i = 0; i < 4; i++)
            ResetBox(shelf, $"Climbable Shelf Platform {i+1:00}",new(sb.center.x,tops[i]-.023f,sb.center.z),new(sb.size.x-.035f,.046f,sb.size.z-.14f));
        foreach (Transform frame in shelf.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Shelf frame collision")))
        {
            string[] bits = frame.name.Split(' '); int side = int.Parse(bits[3]), end = int.Parse(bits[4]);
            ResetBox(shelf,frame.name,new(end == 0 ? sb.min.x+.045f : sb.max.x-.045f,1.045f,sb.center.z+side*(sb.size.z*.5f-.04f)),new(.065f,2.09f,.065f));
        }
        Quaternion rotationDelta = shelf.transform.rotation * Quaternion.Inverse(oldRotation);
        foreach (string name in ShelfProps)
        {
            Transform t = Need(name).transform;
            t.position = shelf.transform.TransformPoint(positions[name]); t.rotation = rotationDelta * t.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        // The screenshot already contains manually moved furniture and orphaned
        // props; settle them onto the actual boards rather than preserving stale offsets.
        Physics.SyncTransforms();
        for (int i = 0; i < ShelfProps.Length; i++)
        {
            Transform board = shelf.transform.Find($"Climbable Shelf Platform {i / 2 + 1:00}");
            if (board == null) throw new InvalidOperationException("Missing shelf platform.");
            Bounds support = board.GetComponent<Collider>().bounds;
            GameObject prop = Need(ShelfProps[i]); Bounds p = BoundsOf(prop);
            Vector3 target = new(support.max.x - .16f, support.max.y + .008f, support.center.z + (i % 2 == 0 ? -.6f : .6f));
            prop.transform.position += target - new Vector3(p.center.x,p.min.y,p.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
        }
        Move("Sofa", new Vector3(-1.70f,0,2.65f), 180);
        Move("Tripo Hide Cardboard", new Vector3(-3.50f,0,2.58f), 180);
        Move("Tripo Curtain", new Vector3(1.15f,.675f,3.15f), 90);
        Move("Tripo Tv Cabinet", new Vector3(-1.70f,0,-2.82f), 0);
        GameObject cabinet = Need("Tripo Tv Cabinet");
        float cabinetTop = BoundsOf(cabinet).max.y;
        // Leave a standing lane between the back wall and television so the
        // cat can push the screen out toward the room instead of into the wall.
        Move("Tripo Television", new Vector3(-1.70f,cabinetTop + .008f,-2.42f), 0);
        Move("Tripo Table Lamp", new Vector3(-.50f,cabinetTop + .008f,-2.79f), 0);
        // Keep a >= .65m passage beside the .40m-diameter cat capsule.
        Move("Tripo Cat Tree", new Vector3(3.68f,0,-2.53f), 90);
        Move("Tripo Pothos Plant", new Vector3(-3.70f,0,1.55f), 180);
        Move("Tripo Navy Sideboard", new Vector3(1.12f,0,2.91f), 180);
        // Preserve the user's central table, chairs, rug and table-top props.
        BuildWalls();
        HideVolume("Hide Spot - Cardboard Box", "纸箱", BoundsOf(Need("Tripo Hide Cardboard")).center, new Vector3(.85f,.60f,.85f));
        HideVolume("Hide Spot - Behind Curtain", "窗帘后", new Vector3(1.15f,1.15f,2.98f), new Vector3(1.45f,2.30f,.50f));
        GameObject key = GameObject.Find("Window Dusk Key - warm directional");
        if (key != null) key.transform.rotation = Quaternion.Euler(42f, -155f, 0);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("REFERENCE_LAYOUT_V2_SAVED: shelf right, sofa back, window back-right, TV front, door left, box beside sofa.");
    }
    static GameObject Need(string name) => GameObject.Find(name) ?? throw new InvalidOperationException("Missing " + name);
    static void ResetBox(GameObject parent,string name,Vector3 center,Vector3 worldSize)
    {
        Transform t = parent.transform.Find(name); if (t == null) throw new InvalidOperationException("Missing " + name);
        t.localRotation = Quaternion.identity; t.localScale = Vector3.one; t.position = center;
        Vector3 local = t.InverseTransformVector(worldSize);
        BoxCollider c = t.GetComponent<BoxCollider>(); c.center = Vector3.zero;
        c.size = new Vector3(Mathf.Abs(local.x),Mathf.Abs(local.y),Mathf.Abs(local.z)); c.enabled = true;
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(); Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    static void Move(string name, Vector3 bottomCenter, float yaw, bool shelf = false)
    {
        GameObject go = Need(name); go.transform.rotation = Quaternion.Euler(0,yaw,0);
        Bounds b = shelf ? go.GetComponentInChildren<MeshFilter>().GetComponent<Renderer>().bounds : BoundsOf(go);
        go.transform.position += bottomCenter - new Vector3(b.center.x,b.min.y,b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }
    static void HideVolume(string name, string label, Vector3 position, Vector3 size)
    {
        GameObject go = GameObject.Find(name) ?? new GameObject(name);
        go.transform.SetPositionAndRotation(position, Quaternion.identity); go.transform.localScale = Vector3.one;
        BoxCollider c = go.GetComponent<BoxCollider>(); if (c == null) c = go.AddComponent<BoxCollider>(); c.size = size; c.isTrigger = true;
        HideSpot spot = go.GetComponent<HideSpot>(); if (spot == null) spot = go.AddComponent<HideSpot>(); spot.Configure(label);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }
    static void BuildWalls()
    {
        Transform root = Need("Four Wall Room Shell (editable)").transform;
        Material wall = Need("Back Wall").GetComponent<Renderer>().sharedMaterial;
        Material trim = Need("Right skirting").GetComponent<Renderer>().sharedMaterial;
        foreach (string name in new[] { "Back Wall", "Left Wall", "Right Wall - lower", "Right Wall - upper", "Right Wall - front pier", "Right Wall - rear pier", "Front Wall - left", "Front Wall - right", "Front Wall - door lintel", "Front skirting left", "Front skirting right" })
        {
            // Retain old pieces for recovery, but remove both their drawing and physics.
            GameObject go = GameObject.Find(name); if (go == null) continue;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        }
        Part(root,"V2 Right Wall",new(4.4f,2,0),new(.18f,4,6.6f),wall);
        Part(root,"V2 Front Wall",new(0,2,-3.3f),new(8.8f,4,.18f),wall);
        // Back opening x [-.05,2.35], y [1.20,2.90].
        Part(root,"V2 Back Wall lower",new(0,.6f,3.3f),new(8.8f,1.2f,.18f),wall);
        Part(root,"V2 Back Wall upper",new(0,3.45f,3.3f),new(8.8f,1.1f,.18f),wall);
        Part(root,"V2 Back Wall left",new(-2.225f,2.05f,3.3f),new(4.35f,1.7f,.18f),wall);
        Part(root,"V2 Back Wall right",new(3.375f,2.05f,3.3f),new(2.05f,1.7f,.18f),wall);
        // Left door opening z [-.15,1.15].
        Part(root,"V2 Left Wall front",new(-4.4f,2,-1.725f),new(.18f,4,3.15f),wall);
        Part(root,"V2 Left Wall rear",new(-4.4f,2,2.225f),new(.18f,4,2.15f),wall);
        Part(root,"V2 Left Wall lintel",new(-4.4f,3.55f,.5f),new(.18f,.9f,1.30f),wall);
        PositionPart("Door",new(-4.30f,1.5f,.5f),90);
        PositionPart("Door frame left",new(-4.17f,1.55f,1.15f),90);
        PositionPart("Door frame right",new(-4.17f,1.55f,-.15f),90);
        PositionPart("Door frame top",new(-4.17f,3.12f,.5f),90);
        Part(root,"V2 Front skirting",new(0,.12f,-3.15f),new(8.8f,.24f,.12f),trim);
        Part(root,"V2 Back skirting",new(0,.12f,3.15f),new(8.8f,.24f,.12f),trim);
        Part(root,"V2 Left skirting front",new(-4.25f,.12f,-1.725f),new(.12f,.24f,3.15f),trim);
        Part(root,"V2 Left skirting rear",new(-4.25f,.12f,2.225f),new(.12f,.24f,2.15f),trim);
        Part(root,"V2 Window frame left",new(-.05f,2.05f,3.17f),new(.09f,1.78f,.12f),trim);
        Part(root,"V2 Window frame right",new(2.35f,2.05f,3.17f),new(.09f,1.78f,.12f),trim);
        Part(root,"V2 Window frame top",new(1.15f,2.90f,3.17f),new(2.49f,.09f,.12f),trim);
        Part(root,"V2 Window frame bottom",new(1.15f,1.20f,3.17f),new(2.49f,.09f,.12f),trim);
        Part(root,"V2 Window frame middle",new(1.15f,2.05f,3.20f),new(.06f,1.7f,.08f),trim);
        // An invisible pane keeps the playable room sealed without filling the view with a wall.
        GameObject pane = Part(root,"V2 Window safety pane",new(1.15f,2.05f,3.34f),new(2.4f,1.7f,.06f),trim);
        pane.GetComponent<Renderer>().enabled = false;
    }
    static void PositionPart(string name, Vector3 position, float yaw)
    {
        Transform t = Need(name).transform; t.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
    }
    static GameObject Part(Transform root, string name, Vector3 position, Vector3 size, Material material)
    {
        Transform found = root.Find(name); GameObject go = found == null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : found.gameObject;
        go.name = name; go.transform.SetParent(root,false); go.transform.SetPositionAndRotation(position,Quaternion.identity); go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material; go.isStatic = true; return go;
    }

    [MenuItem("Catastrophe/Reference Room/Inspect and Capture Layout V2")]
    public static void Inspect()
    {
        Directory.CreateDirectory("Validation/ReferenceLayoutV2");
        string[] names = { "Sofa", "Tripo Shelving Unit", "Tripo Television", "Tripo Tv Cabinet", "Tripo Curtain", "Tripo Hide Cardboard", "Tripo Cat Tree", "Door" };
        string report = string.Join("\n", names.Select(n => n + ": " + BoundsOf(Need(n))));
        File.WriteAllText("Validation/ReferenceLayoutV2/bounds.txt", report);
        GameObject go = new("Temporary layout capture"); Camera c = go.AddComponent<Camera>();
        c.orthographic = true; c.orthographicSize = 4.8f; c.transform.position = new Vector3(0,12,0);
        c.transform.rotation = Quaternion.Euler(90,0,0); c.nearClipPlane = .03f;
        RenderTexture rt = new(1400,1100,24); RenderTexture old = RenderTexture.active; c.targetTexture = rt;
        try
        {
            c.Render(); RenderTexture.active = rt; Texture2D image = new(1400,1100,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1400,1100),0,0); image.Apply();
            File.WriteAllBytes("Validation/ReferenceLayoutV2/Top.png",image.EncodeToPNG()); Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = old; c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        Debug.Log("REFERENCE_LAYOUT_V2_INSPECT\n" + report);
    }
}
