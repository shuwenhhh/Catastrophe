using System;
using UnityEngine;

// One state machine owns the noise-triggered warning and the owner's visit.
public sealed class OwnerAlert : MonoBehaviour
{
    public enum Phase { Idle, HidingCountdown, Inspecting }
    public static OwnerAlert I { get; private set; }
    public const float WarningDuration = 5f;
    public const float InspectionDuration = 3f;
    public const float PatrolInterval = WarningDuration; // Compatibility, no periodic patrol.
    public Phase CurrentPhase { get; private set; }
    public bool IsInspecting => CurrentPhase == Phase.Inspecting;
    public bool IsWarning => CurrentPhase == Phase.HidingCountdown;
    public bool IsRunning { get; private set; }
    public float SecondsRemaining { get; private set; }
    public float CountdownRemaining => IsWarning ? SecondsRemaining : 0f;
    public int InspectionFrame => IsInspecting
        ? Mathf.Clamp(Mathf.FloorToInt(InspectionDuration - SecondsRemaining), 0, 2) : -1;
    public event Action<bool> OnInspectionChanged;
    NoiseManager observedNoise;
    int lastTick;

    void Awake()
    {
        if (I && I != this) { Destroy(this); return; }
        I = this;
    }
    void OnEnable() => BindNoise();
    void BindNoise()
    {
        if (observedNoise == NoiseManager.I) return;
        if (observedNoise) observedNoise.OnThresholdExceeded -= TriggerAlert;
        observedNoise = NoiseManager.I;
        if (observedNoise) observedNoise.OnThresholdExceeded += TriggerAlert;
    }
    void OnDisable()
    {
        if (observedNoise) observedNoise.OnThresholdExceeded -= TriggerAlert;
        observedNoise = null;
        AudioManager.I?.SetInspectionMix(false);
    }
    void OnDestroy() { if (I == this) I = null; }
    public void BeginRun()
    {
        IsRunning = true;
        SetPhase(Phase.Idle, 0f);
        NoiseManager.I?.ResetNoise();
    }
    public void RestoreState(Phase phase, float remaining)
    {
        IsRunning = true;
        if (!Enum.IsDefined(typeof(Phase), phase)) phase = Phase.Idle;
        if (phase == Phase.Idle && NoiseManager.I && NoiseManager.I.noise >= 100f)
        { phase = Phase.HidingCountdown; remaining = WarningDuration; }
        float duration = phase == Phase.Inspecting ? InspectionDuration : phase == Phase.HidingCountdown ? WarningDuration : 0f;
        if (float.IsNaN(remaining) || float.IsInfinity(remaining)) remaining = duration;
        SetPhase(phase, Mathf.Clamp(remaining, 0f, duration));
    }
    public void RestoreCycle(bool inspecting, float remaining) =>
        RestoreState(inspecting ? Phase.Inspecting : Phase.HidingCountdown, remaining);
    public static float DestructionPenalty(float noise) => 0f;

    public void TriggerAlert()
    {
        if (!IsRunning || CurrentPhase != Phase.Idle || !GameFlowUI.GameplayInputEnabled) return;
        SetPhase(Phase.HidingCountdown, WarningDuration);
        Tick();
    }
    void SetPhase(Phase phase, float seconds)
    {
        bool wasInspecting = IsInspecting;
        CurrentPhase = phase;
        SecondsRemaining = seconds;
        lastTick = -1;
        AudioManager.I?.SetInspectionMix(IsInspecting);
        if (wasInspecting != IsInspecting) OnInspectionChanged?.Invoke(IsInspecting);
    }
    void Tick()
    {
        int second = Mathf.CeilToInt(SecondsRemaining);
        if (second > 0 && second != lastTick)
        { lastTick = second; AudioManager.I?.PlayCountdownTick(); }
    }
    void Update()
    {
        BindNoise();
        if (!IsRunning || !GameFlowUI.GameplayInputEnabled || CurrentPhase == Phase.Idle) return;
        SecondsRemaining = Mathf.Max(0f, SecondsRemaining - Time.deltaTime);
        if (IsWarning)
        {
            if (SecondsRemaining <= 0f) SetPhase(Phase.Inspecting, InspectionDuration);
            else Tick();
        }
    }
    void LateUpdate()
    {
        if (!IsRunning || !IsInspecting || SecondsRemaining > 0f || !GameFlowUI.GameplayInputEnabled) return;
        var cat = FindAnyObjectByType<CatController>();
        bool caught = cat && !RoomHiding.IsConcealed(cat);
        SetPhase(Phase.Idle, 0f);
        IsRunning = !caught;
        if (caught) GameManager.I?.OnCaught();
        else NoiseManager.I?.ResetNoise();
    }
}
