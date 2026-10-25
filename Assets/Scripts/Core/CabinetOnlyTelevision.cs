using UnityEngine;

// The television is a tabletop objective: a cat jumping beside it cannot push it mid-air.
public sealed class CabinetOnlyTelevision : MonoBehaviour
{
    [SerializeField] private BoxCollider standingSurface;

    public void SetStandingSurface(BoxCollider surface) => standingSurface = surface;

    public bool CanPush(CatController cat)
    {
        if (cat == null || standingSurface == null || !standingSurface.enabled)
            return false;

        float feetY = cat.transform.position.y - 0.225f;
        Bounds top = standingSurface.bounds;
        if (Mathf.Abs(feetY - top.max.y) > 0.12f)
            return false;

        Vector3 feet = cat.transform.position;
        if (feet.x < top.min.x + 0.07f || feet.x > top.max.x - 0.07f ||
            feet.z < top.min.z + 0.07f || feet.z > top.max.z - 0.07f)
            return false;

        Ray ray = new Ray(new Vector3(feet.x, top.max.y + 0.10f, feet.z), Vector3.down);
        return standingSurface.Raycast(ray, out RaycastHit hit, 0.22f) &&
               Mathf.Abs(hit.point.y - top.max.y) < 0.025f;
    }
}
