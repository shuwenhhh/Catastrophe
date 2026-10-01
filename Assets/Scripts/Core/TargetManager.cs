using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        "FIRST TARGET: push the iPad off the table",
        "TARGET: knock over both cups",
        "TARGET: smash something fragile!",
        "FINAL TARGET: reach 100 chaos!"
    };

    private readonly List<Valuable> observedValuables = new List<Valuable>();
    private readonly HashSet<Valuable> fallenCups = new HashSet<Valuable>();
    private int targetIndex;
    private string currentPrompt;
    private bool levelComplete;
    private GameManager observedGameManager;
    private GUIStyle promptStyle;
    private GUIStyle completionTitleStyle;
    private GUIStyle completionBodyStyle;

    public string CurrentTargetId => levelComplete ? string.Empty : TargetIds[targetIndex];
    public string CurrentPrompt => currentPrompt;
    public bool IsLevelComplete => levelComplete;

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

        if (CurrentTargetId == IpadTarget && ContainsIgnoreCase(itemName, "ipad"))
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
            currentPrompt = "ALL TARGETS COMPLETE!";
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

    private static bool ContainsIgnoreCase(string value, string expected)
    {
        return value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnGUI()
    {
        float uiScale = Mathf.Clamp(Screen.height / 1080f, 1f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float logicalWidth = Screen.width / uiScale;
        float logicalHeight = Screen.height / uiScale;

        promptStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUI.Box(new Rect(0f, logicalHeight - 80f, logicalWidth, 52f), GUIContent.none);
        GUI.Label(new Rect(20f, logicalHeight - 76f, logicalWidth - 40f, 44f), currentPrompt, promptStyle);

        if (levelComplete)
            DrawCompletionPanel(logicalWidth, logicalHeight);

        GUI.matrix = previousMatrix;
    }

    private void DrawCompletionPanel(float width, float height)
    {
        completionTitleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        completionBodyStyle ??= new GUIStyle(completionTitleStyle) { fontSize = 20 };

        Rect panel = new Rect(width * 0.5f - 230f, height * 0.5f - 115f, 460f, 230f);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 20f, panel.y + 30f, panel.width - 40f, 56f),
            "ROOM WRECKED!", completionTitleStyle);
        GUI.Label(new Rect(panel.x + 20f, panel.y + 92f, panel.width - 40f, 72f),
            "All targets complete\nReceipt coming in a future update", completionBodyStyle);
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
