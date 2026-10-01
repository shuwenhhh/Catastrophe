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
        GameBootstrap.Instance?.ShowChaosToast(valuable.chaosValue, valuable.itemName);
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
}
