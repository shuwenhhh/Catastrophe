using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Self-contained presentation layer for destruction feedback. It owns no room
/// objects, so props remain independently editable in the scene hierarchy.
/// </summary>
public sealed class ChaosJuiceUI : MonoBehaviour
{
    private const float PopupLifetime = 1f;
    private const float ComboWindow = 3f;

    private sealed class Popup
    {
        public Vector3 worldPosition;
        public int yen;
        public float bornAt;
    }

    private readonly List<Popup> popups = new List<Popup>();
    private readonly Queue<float> recentDestructions = new Queue<float>();
    private NoiseManager noiseManager;
    private float noise;
    private string announcement;
    private float announcementUntil;
    private bool firstBloodShown;
    private bool tripleKillShownForRun;
    private GUIStyle popupStyle;
    private GUIStyle hudStyle;
    private GUIStyle zoneStyle;
    private GUIStyle announcementStyle;

    private void OnEnable()
    {
        Valuable.AnyDestroyed += HandleDestroyed;
        BindNoiseManager();
    }

    private void Start()
    {
        BindNoiseManager();
    }

    private void OnDisable()
    {
        Valuable.AnyDestroyed -= HandleDestroyed;
        if (noiseManager != null)
            noiseManager.OnNoiseChanged -= HandleNoiseChanged;
        noiseManager = null;
    }

    private void BindNoiseManager()
    {
        NoiseManager manager = NoiseManager.I != null ? NoiseManager.I : FindAnyObjectByType<NoiseManager>();
        if (manager == noiseManager)
            return;
        if (noiseManager != null)
            noiseManager.OnNoiseChanged -= HandleNoiseChanged;
        noiseManager = manager;
        if (noiseManager != null)
        {
            noise = noiseManager.noise;
            noiseManager.OnNoiseChanged += HandleNoiseChanged;
        }
    }

    private void HandleNoiseChanged(float value)
    {
        noise = value;
    }

    private void HandleDestroyed(Valuable valuable)
    {
        if (valuable == null)
            return;

        float now = Time.unscaledTime;
        popups.Add(new Popup
        {
            worldPosition = valuable.transform.position + Vector3.up * 0.35f,
            yen = Mathf.Max(0, valuable.chaosValue) * 100,
            bornAt = now
        });

        if (!firstBloodShown)
        {
            firstBloodShown = true;
            ShowAnnouncement("FIRST BLOOD");
        }

        recentDestructions.Enqueue(now);
        while (recentDestructions.Count > 0 && now - recentDestructions.Peek() > ComboWindow)
            recentDestructions.Dequeue();
        if (recentDestructions.Count >= 3 && !tripleKillShownForRun)
        {
            tripleKillShownForRun = true;
            ShowAnnouncement("TRIPLE KILL");
        }
    }

    private void ShowAnnouncement(string message)
    {
        announcement = message;
        announcementUntil = Time.unscaledTime + 1.8f;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        for (int i = popups.Count - 1; i >= 0; i--)
            if (now - popups[i].bornAt >= PopupLifetime)
                popups.RemoveAt(i);

        if (noiseManager == null)
            BindNoiseManager();
    }

    private void OnGUI()
    {
        if (!GameFlowUI.GameplayInputEnabled)
            return;

        float scale = Mathf.Clamp(Screen.height / 1080f, 1f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        SetupStyles();
        DrawNoiseBar(width);
        DrawPopups(scale);
        DrawAnnouncement(width);
        GUI.matrix = previousMatrix;
    }

    private void DrawNoiseBar(float width)
    {
        float threshold = noiseManager != null ? Mathf.Max(1f, noiseManager.threshold) : 70f;
        float ratio = Mathf.Clamp01(noise / threshold);
        bool risky = ratio >= 0.7f;
        float pulse = risky ? 0.65f + Mathf.PingPong(Time.unscaledTime * 3.5f, 0.35f) : 1f;
        Rect bar = new Rect(width - 326f, 70f, 290f, 24f);

        GUI.Label(new Rect(width - 326f, 28f, 290f, 36f),
            $"NOISE  {Mathf.RoundToInt(noise)} / {Mathf.RoundToInt(threshold)}", hudStyle);
        Color old = GUI.color;
        GUI.color = new Color(0.06f, 0.06f, 0.07f, 0.9f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = risky
            ? new Color(1f, 0.12f, 0.08f, pulse)
            : Color.Lerp(new Color(0.20f, 0.82f, 0.42f), new Color(1f, 0.72f, 0.16f), ratio / 0.7f);
        GUI.DrawTexture(new Rect(bar.x + 3f, bar.y + 3f, (bar.width - 6f) * ratio, bar.height - 6f),
            Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        float dividerX = bar.x + bar.width * 0.7f;
        GUI.DrawTexture(new Rect(dividerX, bar.y - 2f, 2f, bar.height + 4f), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(new Rect(bar.x, bar.y + 25f, bar.width * 0.7f, 24f), "CHILL", zoneStyle);
        GUI.Label(new Rect(dividerX, bar.y + 25f, bar.width * 0.3f, 24f), "RISKY", zoneStyle);
    }

    private void DrawPopups(float scale)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        float now = Time.unscaledTime;
        foreach (Popup popup in popups)
        {
            Vector3 screen = camera.WorldToScreenPoint(popup.worldPosition);
            if (screen.z <= 0f)
                continue;
            float age = Mathf.Clamp01((now - popup.bornAt) / PopupLifetime);
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.82f, 0.18f, 1f - age);
            float x = screen.x / scale - 90f;
            float y = (Screen.height - screen.y) / scale - age * 70f;
            GUI.Label(new Rect(x, y, 180f, 42f), $"-¥{popup.yen:N0}", popupStyle);
            GUI.color = old;
        }
    }

    private void DrawAnnouncement(float width)
    {
        if (Time.unscaledTime >= announcementUntil || string.IsNullOrEmpty(announcement))
            return;
        float remaining = announcementUntil - Time.unscaledTime;
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(remaining * 2f));
        GUI.Label(new Rect(0f, 135f, width, 70f), announcement, announcementStyle);
        GUI.color = old;
    }

    private void SetupStyles()
    {
        popupStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        hudStyle ??= new GUIStyle(popupStyle) { fontSize = 22, alignment = TextAnchor.MiddleRight };
        zoneStyle ??= new GUIStyle(popupStyle) { fontSize = 13, alignment = TextAnchor.UpperCenter };
        announcementStyle ??= new GUIStyle(popupStyle) { fontSize = 42, alignment = TextAnchor.MiddleCenter };
    }
}
