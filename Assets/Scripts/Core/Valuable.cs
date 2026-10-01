using System;
using UnityEngine;

public sealed class Valuable : MonoBehaviour
{
    public string itemName;
    public int chaosValue = 10;
    public float weight = 1f;
    public float noise = 30f;
    public bool fragile;

    public event Action<Valuable> OnDestroyed;

    public void MarkDestroyed()
    {
        OnDestroyed?.Invoke(this);
    }
}
