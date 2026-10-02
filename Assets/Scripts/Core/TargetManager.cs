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
    private GUIStyle promptStyle;
    private GUIStyle completionTitleStyle;
    private GUIStyle completionBodyStyle;
    private GUIStyle objectiveMarkerStyle;
    private float levelStartedAt;

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
        levelStartedAt = Time.time;
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

    private void OnGUI()
    {
        if (!GameFlowUI.GameplayInputEnabled)
            return;

        float uiScale = Mathf.Clamp(Screen.height / 1080f, 1f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float logicalWidth = Screen.width / uiScale;
        float logicalHeight = Screen.height / uiScale;

        promptStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUI.Box(new Rect(0f, logicalHeight - 86f, logicalWidth, 58f), GUIContent.none);
        GUI.Label(new Rect(20f, logicalHeight - 82f, logicalWidth - 40f, 48f), currentPrompt, promptStyle);
        DrawObjectiveMarker(uiScale);

        GUI.matrix = previousMatrix;
    }

    private void DrawObjectiveMarker(float uiScale)
    {
        Valuable objective = FindObjectiveValuable();
        Camera camera = Camera.main;
        if (objective == null || camera == null)
            return;

        Vector3 world = objective.transform.position + Vector3.up * 0.42f;
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (screen.z <= 0f)
            return;

        objectiveMarkerStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.78f, 0.18f) }
        };
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f;
        float x = screen.x / uiScale;
        float y = (Screen.height - screen.y) / uiScale;
        Matrix4x4 old = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(pulse, pulse), new Vector2(x, y));
        GUI.Label(new Rect(x - 76f, y - 52f, 152f, 44f), "▼  目标", objectiveMarkerStyle);
        GUI.matrix = old;
    }

    private Valuable FindObjectiveValuable()
    {
        foreach (Valuable valuable in observedValuables)
        {
            if (valuable == null)
                continue;
            string itemName = string.IsNullOrWhiteSpace(valuable.itemName) ? valuable.name : valuable.itemName;
            if (CurrentTargetId == IpadTarget && IsTablet(itemName)) return valuable;
            if (CurrentTargetId == CupsTarget && IsCup(itemName) && !fallenCups.Contains(valuable)) return valuable;
            if (CurrentTargetId == FragileTarget && valuable.fragile) return valuable;
        }
        return null;
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
        int score = GameManager.I != null ? GameManager.I.chaosScore : 0;
        float elapsed = Mathf.Max(0f, Time.time - levelStartedAt);
        string grade = elapsed <= 90f && score >= 150 ? "S" : elapsed <= 150f && score >= 100 ? "A" : "B";
        string receipt = BuildReceipt();
        GUI.Label(new Rect(panel.x + 20f, panel.y + 88f, panel.width - 40f, 122f),
            $"{receipt}\nTOTAL  ¥{score * 100:N0}    CHAOS {score}    RANK {grade}", completionBodyStyle);
    }

    private static string BuildReceipt()
    {
        if (GameManager.I == null || GameManager.I.Receipt.Count == 0)
            return "No damaged items recorded";

        System.Text.StringBuilder text = new System.Text.StringBuilder();
        int first = Mathf.Max(0, GameManager.I.Receipt.Count - 3);
        for (int index = first; index < GameManager.I.Receipt.Count; index++)
        {
            GameManager.ChaosEntry entry = GameManager.I.Receipt[index];
            if (text.Length > 0)
                text.Append('\n');
            text.Append(entry.itemName).Append("   ¥").Append((entry.points * 100).ToString("N0"));
        }
        return text.ToString();
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
