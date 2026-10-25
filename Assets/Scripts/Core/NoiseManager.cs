using System;
using UnityEngine;

public sealed class NoiseManager : MonoBehaviour
{
    public static NoiseManager I;

    public float noise = 0f;
    public float threshold = 100f;

    public event Action OnThresholdExceeded;
    public event Action<float> OnNoiseChanged;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        threshold = 100f;
    }

    public void EmitNoise(float amount)
    {
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) ||
            Time.timeScale <= 0f || !GameFlowUI.GameplayInputEnabled || GameManager.I?.IsFailed == true)
            return;

        float previousNoise = noise;
        noise = Mathf.Clamp(noise + amount, 0f, 100f);
        OnNoiseChanged?.Invoke(noise);
        if (previousNoise < 100f && noise >= 100f)
            OnThresholdExceeded?.Invoke();
    }

    public void ResetNoise()
    {
        noise = 0f;
        OnNoiseChanged?.Invoke(noise);
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;
    }

    public void RestoreNoise(float value)
    {
        noise = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, 100f);
        OnNoiseChanged?.Invoke(noise);
    }
}
