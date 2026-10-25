using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Progress is tracked silently; GameFlowUI owns the end-of-run presentation.
public sealed class TargetManager : MonoBehaviour
{
    public static TargetManager I;

    public event Action<string> OnTargetChanged;

    private const string IpadTarget = "ipad";
    private const string CupsTarget = "cups";
    private const string FragileTarget = "fragile";
    private const string ChaosTarget = "chaos100";

    private static readonly string[] TargetIds =
    {
        IpadTarget, CupsTarget, FragileTarget, ChaosTarget
    };

    private static readonly string[] TargetPrompts =
    {
        "目标 1/4：把平板推下桌",
        "目标 2/4：推倒两个杯子",
        "目标 3/4：打碎一个易碎品",
        "目标 4/4：达到 100 CHAOS"
    };

    private readonly List<Valuable> observedValuables = new List<Valuable>();
    private readonly HashSet<Valuable> fallenCups = new HashSet<Valuable>();
    private int targetIndex;
    private string currentPrompt;
    private bool levelComplete;
    private GameManager observedGameManager;

    public string CurrentTargetId => levelComplete ? string.Empty : TargetIds[targetIndex];
    public string CurrentPrompt => currentPrompt;
    public bool IsLevelComplete => levelComplete;
    public int SavedTargetIndex => targetIndex;
    public List<string> SavedCups => new List<Valuable>(fallenCups).ConvertAll(v => GameSaveStore.ObjectKey(v.transform));

    public void RestoreProgress(int index, List<string> cups)
    {
        targetIndex = Mathf.Clamp(index, 0, TargetIds.Length - 1);
        levelComplete = false;
        fallenCups.Clear();
        DiscoverValuables();
        foreach (var value in observedValuables)
            if (value && cups.Contains(GameSaveStore.ObjectKey(value.transform))) fallenCups.Add(value);
        currentPrompt = TargetPrompts[targetIndex];
        RaiseTargetChanged(currentPrompt);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindAnyObjectByType<TargetManager>() == null)
            new GameObject("Target Manager").AddComponent<TargetManager>();
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        currentPrompt = TargetPrompts[0];
    }

    private void Start()
    {
        BindGameManager();
        DiscoverValuables();
        RaiseTargetChanged(currentPrompt);
        StartCoroutine(RefreshBindings());
    }

    private IEnumerator RefreshBindings()
    {
        WaitForSeconds delay = new WaitForSeconds(0.25f);
        while (!levelComplete)
        {
            BindGameManager();
            DiscoverValuables();
            CheckChaosTarget();
            yield return delay;
        }
    }

    private void BindGameManager()
    {
        if (observedGameManager == GameManager.I)
            return;

        if (observedGameManager != null)
            observedGameManager.OnChaosChanged -= HandleChaosChanged;

        observedGameManager = GameManager.I;
        if (observedGameManager != null)
            observedGameManager.OnChaosChanged += HandleChaosChanged;
    }

    private void DiscoverValuables()
    {
        Valuable[] valuables = FindObjectsByType<Valuable>(FindObjectsInactive.Include);
        foreach (Valuable valuable in valuables)
        {
            if (valuable == null || observedValuables.Contains(valuable))
                continue;

            observedValuables.Add(valuable);
            valuable.OnDestroyed += HandleValuableDestroyed;
        }

        observedValuables.RemoveAll(valuable => valuable == null);
    }

    private void HandleValuableDestroyed(Valuable valuable)
    {
        if (levelComplete || valuable == null)
            return;

        string itemName = string.IsNullOrWhiteSpace(valuable.itemName)
            ? valuable.gameObject.name
            : valuable.itemName;

        if (CurrentTargetId == IpadTarget && IsTablet(itemName))
        {
            CompleteTarget(IpadTarget);
            return;
        }

        if (CurrentTargetId == CupsTarget && IsCup(itemName))
        {
            fallenCups.Add(valuable);
            if (fallenCups.Count >= 2)
                CompleteTarget(CupsTarget);
            return;
        }

        if (CurrentTargetId == FragileTarget && valuable.fragile)
            CompleteTarget(FragileTarget);
    }

    private void HandleChaosChanged(int chaosScore)
    {
        if (CurrentTargetId == ChaosTarget && chaosScore >= 100)
            CompleteTarget(ChaosTarget);
    }

    private void CheckChaosTarget()
    {
        if (CurrentTargetId == ChaosTarget && GameManager.I != null && GameManager.I.chaosScore >= 100)
            CompleteTarget(ChaosTarget);
    }

    public void CompleteTarget(string targetId)
    {
        if (levelComplete || !string.Equals(targetId, CurrentTargetId, StringComparison.OrdinalIgnoreCase))
            return;

        targetIndex++;
        if (targetIndex >= TargetIds.Length)
        {
            levelComplete = true;
            currentPrompt = "目标全部完成！";
            RaiseTargetChanged(currentPrompt);
            GameManager.I?.OnLevelComplete();
            return;
        }

        currentPrompt = TargetPrompts[targetIndex];
        RaiseTargetChanged(currentPrompt);
        CheckChaosTarget();
    }

    private void RaiseTargetChanged(string text)
    {
        OnTargetChanged?.Invoke(text);
    }

    private static bool IsCup(string itemName)
    {
        return ContainsIgnoreCase(itemName, "cup") || ContainsIgnoreCase(itemName, "mug");
    }

    private static bool IsTablet(string itemName)
    {
        return ContainsIgnoreCase(itemName, "ipad") || ContainsIgnoreCase(itemName, "tablet");
    }

    private static bool ContainsIgnoreCase(string value, string expected)
    {
        return value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }


    private void OnDestroy()
    {
        if (observedGameManager != null)
            observedGameManager.OnChaosChanged -= HandleChaosChanged;

        foreach (Valuable valuable in observedValuables)
        {
            if (valuable != null)
                valuable.OnDestroyed -= HandleValuableDestroyed;
        }

        if (I == this)
            I = null;
    }
}
