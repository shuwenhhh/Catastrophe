using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public sealed class HideSpot : MonoBehaviour
{
    [SerializeField] private string displayName = "Hide spot";
    private readonly HashSet<Collider> catContacts = new HashSet<Collider>();

    public string DisplayName => displayName;
    [SerializeField] private Transform hideAnchor;
    [SerializeField] private Transform exitAnchor;
    public Transform HideAnchor => hideAnchor;
    public Transform ExitAnchor => exitAnchor;
    public void SetAnchors(Transform inside, Transform outside) { hideAnchor = inside; exitAnchor = outside; }

    public void Configure(string spotName)
    {
        displayName = spotName;
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var cat = other.GetComponentInParent<CatController>();
        if (cat != null) { catContacts.Add(other); cat.SetNearbyHideSpot(this); }
    }

    private void OnTriggerExit(Collider other)
    {
        var cat = other.GetComponentInParent<CatController>();
        catContacts.Remove(other);
        if (catContacts.Count == 0) cat?.ClearNearbyHideSpot(this);
    }
    private void OnDisable() { catContacts.Clear(); }
}
