using System.Collections;
using UnityEngine;

public sealed class OwnerAlert : MonoBehaviour
{
    public float countdown = 5f;

    private NoiseManager noiseManager;
    private Coroutine alertRoutine;
    private float secondsRemaining;
    private GUIStyle warningStyle;
    private GUIStyle countdownStyle;
    private GUIStyle noiseStyle;

    private void Start()
    {
        BindNoiseManager();
    }

    private void OnEnable()
    {
        BindNoiseManager();
    }

    private void OnDisable()
    {
        UnbindNoiseManager();
        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
            alertRoutine = null;
        }
    }

    private void BindNoiseManager()
    {
        NoiseManager manager = NoiseManager.I != null
            ? NoiseManager.I
            : FindAnyObjectByType<NoiseManager>();
        if (manager == noiseManager)
            return;

        UnbindNoiseManager();
        noiseManager = manager;
        if (noiseManager != null)
            noiseManager.OnThresholdExceeded += TriggerAlert;
    }

    private void UnbindNoiseManager()
    {
        if (noiseManager != null)
            noiseManager.OnThresholdExceeded -= TriggerAlert;
        noiseManager = null;
    }

    public void TriggerAlert()
    {
        if (alertRoutine == null)
            alertRoutine = StartCoroutine(AlertCountdown());
    }

    private IEnumerator AlertCountdown()
    {
        secondsRemaining = Mathf.Max(0f, countdown);
        while (secondsRemaining > 0f)
        {
            secondsRemaining = Mathf.Max(0f, secondsRemaining - Time.deltaTime);
            yield return null;
        }

        CatController cat = FindAnyObjectByType<CatController>();
        if (cat != null && cat.IsHidden)
        {
            // Resetting to zero crosses below the threshold, so the very next
            // accumulation can trigger another alert. There is no cooldown.
            noiseManager?.ResetNoise();
            alertRoutine = null;
            yield break;
        }

        alertRoutine = null;
        GameManager.I?.OnCaught();
    }

    private void OnGUI()
    {
        float uiScale = Mathf.Clamp(Screen.height / 1080f, 1f, 2f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float logicalWidth = Screen.width / uiScale;
        float logicalHeight = Screen.height / uiScale;

        noiseStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = Color.white }
        };
        warningStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 38,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.28f, 0.20f) }
        };
        countdownStyle ??= new GUIStyle(warningStyle)
        {
            fontSize = 30,
            normal = { textColor = Color.white }
        };

        float currentNoise = noiseManager != null ? noiseManager.noise : 0f;
        GUI.Label(new Rect(logicalWidth - 300f, 30f, 264f, 42f),
            $"噪音  {Mathf.RoundToInt(currentNoise)} / 100", noiseStyle);

        if (alertRoutine == null)
            return;

        GUI.Box(new Rect(logicalWidth * 0.5f - 260f, logicalHeight * 0.5f - 80f, 520f, 160f), GUIContent.none);
        GUI.Label(new Rect(0f, logicalHeight * 0.5f - 62f, logicalWidth, 58f),
            "主人来了！快躲起来！", warningStyle);
        GUI.Label(new Rect(0f, logicalHeight * 0.5f + 4f, logicalWidth, 48f),
            $"{Mathf.CeilToInt(secondsRemaining)}", countdownStyle);
    }
}
