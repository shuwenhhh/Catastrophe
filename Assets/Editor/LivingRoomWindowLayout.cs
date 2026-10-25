using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LivingRoomWindowLayout
{
    [MenuItem("Catastrophe/Reference Room/Inspect Window Corner")]
    public static void Inspect()
    {
        foreach (string name in new[] { "Tripo Curtain", "Tripo Navy Sideboard", "Door", "Tripo Hide Cardboard" })
        {
            GameObject item = GameObject.Find(name);
            if (item == null) continue;
            Bounds b = BoundsOf(item);
            Debug.Log($"WINDOW_CORNER {name} pos={item.transform.position} rot={item.transform.eulerAngles} scale={item.transform.localScale} bounds={b}");
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
                Debug.Log($"WINDOW_RENDER {name}/{renderer.name} bounds={renderer.bounds} mats={string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "null" : m.name))}");
        }
    }

    [MenuItem("Catastrophe/Reference Room/Validate Invisible Hide Volumes (Play)")]
    public static void ValidateHideVolumes()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play mode before validating runtime hide volumes.");

        foreach (string name in new[] { "Hide Spot - Cardboard Box", "Hide Spot - Behind Curtain" })
        {
            GameObject[] matches = UnityEngine.Object.FindObjectsByType<HideSpot>(FindObjectsSortMode.None)
                .Select(spot => spot.gameObject).Where(spot => spot.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected exactly one {name}, found {matches.Length}.");
            GameObject item = matches[0];
            BoxCollider trigger = item.GetComponent<BoxCollider>();
            if (trigger == null || !trigger.enabled || !trigger.isTrigger)
                throw new InvalidOperationException("Missing active trigger: " + name);
            if (item.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled))
                throw new InvalidOperationException("Visible gameplay volume obscures furniture: " + name);
            Debug.Log($"HIDE_VOLUME PASS {name}: invisible, active trigger, bounds={trigger.bounds}");
        }
    }

    private static Bounds BoundsOf(GameObject item)
    {
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Missing renderer: " + item.name);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}
