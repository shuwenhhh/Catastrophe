using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class HideSpot : MonoBehaviour
{
    [SerializeField] private string displayName = "Hide spot";

    public string DisplayName => displayName;

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
        other.GetComponentInParent<CatController>()?.SetNearbyHideSpot(this);
    }

    private void OnTriggerExit(Collider other)
    {
        other.GetComponentInParent<CatController>()?.ClearNearbyHideSpot(this);
    }
}
