using System;
using UnityEngine;

public sealed class TargetManager : MonoBehaviour
{
    public static TargetManager I;

    public event Action<string> OnTargetChanged;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
    }

    public void CompleteTarget(string targetId)
    {
        // Session B owns target progression. This method is the shared entry point.
    }

    protected void RaiseTargetChanged(string text)
    {
        OnTargetChanged?.Invoke(text);
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;
    }
}
