using System;
using UnityEngine;

public sealed class NoiseManager : MonoBehaviour
{
    public static NoiseManager I;

    public float noise = 0f;
    public float threshold = 70f;

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
    }

    public void EmitNoise(float amount)
    {
        if (amount <= 0f || Time.timeScale <= 0f)
            return;

        float previousNoise = noise;
        noise = Mathf.Clamp(noise + amount, 0f, 100f);
        OnNoiseChanged?.Invoke(noise);
        if (previousNoise < threshold && noise >= threshold)
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
}
