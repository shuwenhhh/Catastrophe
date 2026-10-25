using UnityEngine;

// Spatial cover does not freeze, teleport, or change any collider.
// Existing explicit cubbies retain their normal enter/exit interaction.
public static class RoomHiding
{
    static readonly Vector3[] Samples = {
        Vector3.zero, Vector3.forward * .18f, Vector3.back * .18f,
        Vector3.left * .11f, Vector3.right * .11f
    };
    public static bool IsConcealed(CatController cat)
    {
        if (!cat) return false;
        if (cat.IsHidden) return true;
        return IsUnderTable(cat) || IsBehindSofa(cat);
    }
    public static bool IsUnderTable(CatController cat)
    {
        if (!cat) return false;
        Transform table = null;
        foreach (var offset in Samples)
        {
            Vector3 origin = cat.transform.position + Vector3.up * .15f + cat.transform.rotation * offset;
            Transform found = null;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.up, 1.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<CatController>() || hit.collider.GetComponentInParent<Valuable>()) continue;
                var furniture = FindFurniture(hit.collider.transform, "table");
                if (furniture && (table == null || table == furniture)) { found = furniture; break; }
            }
            if (!found) return false;
            table = found;
        }
        return true;
    }
    public static bool IsBehindSofa(CatController cat)
    {
        if (!cat) return false;
        // Owner looks into this room from its open/front side, not from the camera.
        Vector3 eye = new Vector3(0f, 1.25f, -2.6f);
        foreach (var offset in new[] { Vector3.zero, Vector3.forward * .18f, Vector3.back * .18f })
        {
            Vector3 target = cat.transform.position + Vector3.up * .17f + cat.transform.rotation * offset;
            Vector3 delta = target - eye;
            bool covered = false;
            foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (FindFurniture(hit.collider.transform, "sofa") &&
                    Vector3.Distance(hit.point, target) < 1.6f) { covered = true; break; }
            if (!covered) return false;
        }
        return true;
    }
    static Transform FindFurniture(Transform part, string word)
    {
        for (var t = part; t; t = t.parent)
            if (t.name.ToLowerInvariant().Contains(word)) return t;
        return null;
    }
}
