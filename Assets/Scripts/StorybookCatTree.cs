using UnityEngine;

// Authored, genuinely hollow furniture. It must not receive the legacy cubby's
// runtime 2x enlargement, which would separate the new rounded shell and trim.
public sealed class StorybookCatTree : MonoBehaviour
{
    public Transform insideAnchor;
    public Transform exitAnchor;

    public void ConfigureHiding()
    {
        if (transform.Find("Hide Spot - Cat Tree Cubby") || !insideAnchor || !exitAnchor) return;
        var spot = new GameObject("Hide Spot - Cat Tree Cubby");
        spot.transform.SetParent(transform, false);
        spot.transform.localPosition = new Vector3(3.08f, .50f, -2.43f);
        var trigger = spot.AddComponent<BoxCollider>();
        trigger.size = new Vector3(.75f, .75f, .78f);
        var hide = spot.AddComponent<HideSpot>();
        hide.Configure("猫爬架小窝");
        hide.SetAnchors(insideAnchor, exitAnchor);
    }
}
