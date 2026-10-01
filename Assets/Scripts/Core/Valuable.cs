using System;
using UnityEngine;

public sealed class Valuable : MonoBehaviour
{
    public string itemName;
    public int chaosValue = 10;
    public float weight = 1f;
    public float noise = 30f;
    public bool fragile;

    [SerializeField] private float destroyedBelowY = 0.22f;

    private bool destroyed;
    private float startingY;

    public event Action<Valuable> OnDestroyed;

    private void Awake()
    {
        startingY = transform.position.y;
        if (string.IsNullOrWhiteSpace(itemName))
            itemName = gameObject.name;
    }

    private void Update()
    {
        // Only tabletop objects count as destroyed when they reach the floor.
        // This avoids awarding points for valuables intentionally placed at
        // floor height by a level designer.
        if (!destroyed && startingY > destroyedBelowY + 0.1f && transform.position.y <= destroyedBelowY)
            MarkDestroyed();
    }

    public void MarkDestroyed()
    {
        if (destroyed)
            return;

        destroyed = true;
        GameManager.I?.AddChaos(chaosValue);
        NoiseManager.I?.EmitNoise(noise);
        OnDestroyed?.Invoke(this);
    }
}
