using UnityEngine;

public sealed class ScoreItem : MonoBehaviour
{
    [SerializeField] private int points;
    [SerializeField] private string displayName;
    [SerializeField] private float noise = 30f;

    public int Points
    {
        get => points;
        set
        {
            points = value;
            Valuable valuable = GetComponent<Valuable>();
            if (valuable != null)
                valuable.chaosValue = value;
        }
    }

    public string DisplayName
    {
        get => displayName;
        set
        {
            displayName = value;
            Valuable valuable = GetComponent<Valuable>();
            if (valuable != null)
                valuable.itemName = value;
        }
    }

    private void Awake()
    {
        ApplyDefaultsForExistingScene();

        Valuable valuable = GetComponent<Valuable>();
        if (valuable == null)
            valuable = gameObject.AddComponent<Valuable>();

        valuable.itemName = displayName;
        valuable.chaosValue = points;
        valuable.noise = noise;
        valuable.fragile = IsFragile(displayName);
        valuable.weight = DefaultWeight(displayName);
        valuable.ApplyPhysicsTuning();
        valuable.OnDestroyed += HandleDestroyed;
    }

    private void OnDestroy()
    {
        Valuable valuable = GetComponent<Valuable>();
        if (valuable != null)
            valuable.OnDestroyed -= HandleDestroyed;
    }

    private void HandleDestroyed(Valuable valuable)
    {
        if (GameBootstrap.Instance != null)
            GameBootstrap.Instance.ShowChaosToast(valuable.chaosValue, valuable.itemName);
    }

    private void ApplyDefaultsForExistingScene()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = gameObject.name;

        if (points > 0)
            return;

        points = IsFragile(displayName) ? 35 : 10;
    }

    private static bool IsFragile(string itemName)
    {
        return itemName.Contains("Cup", System.StringComparison.OrdinalIgnoreCase) ||
               itemName.Contains("Mug", System.StringComparison.OrdinalIgnoreCase) ||
               itemName.Contains("Vase", System.StringComparison.OrdinalIgnoreCase);
    }

    private static float DefaultWeight(string itemName)
    {
        if (itemName.Contains("Vase", System.StringComparison.OrdinalIgnoreCase)) return 3f;
        if (itemName.Contains("MacBook", System.StringComparison.OrdinalIgnoreCase)) return 2.6f;
        if (itemName.Contains("Globe", System.StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains("Plant", System.StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains("Tea Pot", System.StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains("Lucky Cat", System.StringComparison.OrdinalIgnoreCase)) return 2.2f;
        if (itemName.Contains("iPad", System.StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains("Camera", System.StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains("Frame", System.StringComparison.OrdinalIgnoreCase)) return 1.6f;
        return 1f;
    }
}
