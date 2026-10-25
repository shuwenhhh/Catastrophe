using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TelevisionCabinetSetup
{
    [MenuItem("Catastrophe/TV Cabinet/Build Walkable Top And Gate TV")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before editing the room.");
        GameObject cabinet = GameObject.Find("Tripo Tv Cabinet");
        GameObject tv = GameObject.Find("Tripo Television");
        if (!cabinet || !tv) throw new InvalidOperationException("TV cabinet or television is absent from the open scene.");
        BoxCollider original = cabinet.GetComponent<BoxCollider>();
        if (!original) throw new InvalidOperationException("Cabinet has no solid collider.");

        const string name = "TV cabinet - extended walkable top";
        GameObject top = GameObject.Find(name);
        if (top == null)
        {
            Directory.CreateDirectory("Validation/TVConsole");
            File.Copy("Assets/LivingRoom.unity", "Validation/TVConsole/Before.unity", true);
            top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(top, "Add TV cabinet standing surface");
            top.name = name;
            top.transform.SetParent(cabinet.transform.parent, true);
        }

        Bounds oldBounds = original.bounds;
        top.transform.position = new Vector3(oldBounds.center.x, oldBounds.max.y, oldBounds.center.z + .265f);
        top.transform.rotation = Quaternion.identity;
        top.transform.localScale = new Vector3(3.00f, .075f, 1.05f);
        Physics.SyncTransforms();
        BoxCollider surface = top.GetComponent<BoxCollider>();
        surface.enabled = true;
        MeshRenderer cabinetRenderer = cabinet.GetComponentInChildren<MeshRenderer>();
        if (cabinetRenderer) top.GetComponent<MeshRenderer>().sharedMaterial = cabinetRenderer.sharedMaterial;

        // The imported cabinet remains intact; the overhanging piece is visible and solid.
        // Lift the TV onto the new top without changing its dimensions or Rigidbody.
        BoxCollider tvCollider = tv.GetComponent<BoxCollider>();
        if (!tvCollider) throw new InvalidOperationException("Television has no solid collider.");
        float lift = surface.bounds.max.y + .012f - tvCollider.bounds.min.y;
        tv.transform.position += Vector3.up * lift;
        CabinetOnlyTelevision gate = tv.GetComponent<CabinetOnlyTelevision>();
        if (!gate) gate = Undo.AddComponent<CabinetOnlyTelevision>(tv);
        gate.SetStandingSurface(surface);
        EditorUtility.SetDirty(gate);
        PrefabUtility.RecordPrefabInstancePropertyModifications(tv.transform);

        EditorSceneManager.MarkSceneDirty(cabinet.scene);
        EditorSceneManager.SaveScene(cabinet.scene);
        Debug.Log($"TV_CONSOLE_BUILT top={surface.bounds} television={tvCollider.bounds}; stand on top to push TV.");
    }
}
