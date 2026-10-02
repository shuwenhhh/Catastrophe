using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>Owns the main menu, pause state and both end-of-run panels.</summary>
public sealed class GameFlowUI : MonoBehaviour
{
    public enum ScreenState { MainMenu, Playing, Paused, Victory, Failure }

    private const string HighScoreKey = "Catastrophe.HighScore";
    private static bool autoStartNextLoad;
    public ScreenState State { get; private set; } = ScreenState.MainMenu;
    public static bool GameplayInputEnabled => FindAnyObjectByType<GameFlowUI>() is { State: ScreenState.Playing };

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle receiptStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => autoStartNextLoad = false;

    private void Start()
    {
        if (GameManager.I != null)
        {
            GameManager.I.OnLevelCompleted += ShowVictory;
            GameManager.I.OnLevelFailedEvent += ShowFailure;
        }

        if (autoStartNextLoad)
        {
            autoStartNextLoad = false;
            BeginGame();
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;
        if (State == ScreenState.Playing)
            SetState(ScreenState.Paused);
        else if (State == ScreenState.Paused)
            SetState(ScreenState.Playing);
    }

    public void BeginGame()
    {
        GameManager.I?.BeginRun();
        SetState(ScreenState.Playing);
    }

    public void ResumeGame() => SetState(ScreenState.Playing);

    public void RestartGame()
    {
        autoStartNextLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        autoStartNextLoad = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ShowMainMenu() => SetState(ScreenState.MainMenu);
    private void ShowVictory() => SetState(ScreenState.Victory);
    private void ShowFailure() => SetState(ScreenState.Failure);

    private void SetState(ScreenState state)
    {
        State = state;
        Time.timeScale = state == ScreenState.Playing ? 1f : 0f;
        AudioListener.pause = state == ScreenState.Paused;
    }

    private void OnGUI()
    {
        if (State == ScreenState.Playing)
            return;

        SetupStyles();
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        GUI.color = new Color(0.10f, 0.07f, 0.06f, 0.92f);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        switch (State)
        {
            case ScreenState.MainMenu: DrawMainMenu(width, height); break;
            case ScreenState.Paused: DrawPause(width, height); break;
            case ScreenState.Victory: DrawVictory(width, height); break;
            case ScreenState.Failure: DrawFailure(width, height); break;
        }
        GUI.matrix = oldMatrix;
    }

    private void DrawMainMenu(float width, float height)
    {
        GUI.Label(new Rect(0f, height * 0.15f, width, 90f), "CATASTROPHE!", titleStyle);
        GUI.Label(new Rect(0f, height * 0.15f + 78f, width, 48f), "《猫咪大破坏》", subtitleStyle);
        DrawCatLogo(new Rect(width * 0.5f - 85f, height * 0.36f - 80f, 170f, 150f));
        if (WarmButton(new Rect(width * 0.5f - 150f, height * 0.58f, 300f, 58f), "开始游戏")) BeginGame();
        GUI.Label(new Rect(0f, height * 0.70f, width, 36f), "WASD 移动  ·  空格跳跃  ·  E 推 / 躲藏", bodyStyle);
        GUI.Label(new Rect(0f, height * 0.76f, width, 36f), $"历史最高  {PlayerPrefs.GetInt(HighScoreKey, 0)}", bodyStyle);
    }

    private void DrawPause(float width, float height)
    {
        DrawPanel(width, height, "暂停一下", out Rect panel);
        if (WarmButton(new Rect(panel.x + 80f, panel.y + 105f, panel.width - 160f, 48f), "继续游戏")) ResumeGame();
        if (WarmButton(new Rect(panel.x + 80f, panel.y + 165f, panel.width - 160f, 48f), "重新开始")) RestartGame();
        if (WarmButton(new Rect(panel.x + 80f, panel.y + 225f, panel.width - 160f, 48f), "返回主菜单")) ReturnToMainMenu();
    }

    private void DrawVictory(float width, float height)
    {
        Rect paper = new Rect(width * 0.5f - 265f, 75f, 530f, height - 150f);
        GUI.color = new Color(1f, 0.94f, 0.79f);
        GUI.Box(paper, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(paper.x + 20f, paper.y + 22f, paper.width - 40f, 54f), "破坏账单", titleStyle);
        float y = paper.y + 92f;
        if (GameManager.I != null)
        {
            foreach (GameManager.ChaosEntry entry in GameManager.I.Receipt)
            {
                GUI.Label(new Rect(paper.x + 46f, y, paper.width - 92f, 28f),
                    $"{entry.itemName}    ¥{entry.points * 100:N0}", receiptStyle);
                y += 29f;
                if (y > paper.yMax - 205f) break;
            }
            GUI.Label(new Rect(paper.x + 35f, paper.yMax - 185f, paper.width - 70f, 70f),
                $"TOTAL CHAOS  {GameManager.I.chaosScore}\nRANK  {GameManager.I.Grade}    历史最高  {GameManager.I.HighScore}", receiptStyle);
        }
        if (WarmButton(new Rect(paper.x + 38f, paper.yMax - 92f, 210f, 48f), "再来一局")) RestartGame();
        if (WarmButton(new Rect(paper.xMax - 248f, paper.yMax - 92f, 210f, 48f), "返回主菜单")) ReturnToMainMenu();
    }

    private void DrawFailure(float width, float height)
    {
        DrawPanel(width, height, "被发现了！", out Rect panel);
        int score = GameManager.I != null ? GameManager.I.chaosScore : 0;
        GUI.Label(new Rect(panel.x, panel.y + 95f, panel.width, 65f),
            $"本次 Chaos score  {score}\n历史最高  {PlayerPrefs.GetInt(HighScoreKey, 0)}", bodyStyle);
        if (WarmButton(new Rect(panel.x + 60f, panel.y + 190f, panel.width - 120f, 50f), "再试一次")) RestartGame();
        if (WarmButton(new Rect(panel.x + 60f, panel.y + 252f, panel.width - 120f, 50f), "返回主菜单")) ReturnToMainMenu();
    }

    private void DrawPanel(float width, float height, string title, out Rect panel)
    {
        panel = new Rect(width * 0.5f - 250f, height * 0.5f - 180f, 500f, 360f);
        GUI.color = new Color(0.38f, 0.19f, 0.10f, 0.98f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x, panel.y + 25f, panel.width, 58f), title, titleStyle);
    }

    private bool WarmButton(Rect rect, string text) => GUI.Button(rect, text, buttonStyle);

    private static void DrawCatLogo(Rect rect)
    {
        Color old = GUI.color;
        GUI.color = new Color(0.03f, 0.035f, 0.045f);
        GUI.DrawTexture(new Rect(rect.x + 25f, rect.y + 20f, 120f, 110f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(rect.x + 49f, rect.y + 65f, 72f, 48f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.45f, 0.56f);
        GUI.DrawTexture(new Rect(rect.x + 78f, rect.y + 91f, 14f, 9f), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void SetupStyles()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.78f, 0.30f) } };
        subtitleStyle ??= new GUIStyle(titleStyle) { fontSize = 26, normal = { textColor = Color.white } };
        bodyStyle ??= new GUIStyle(subtitleStyle) { fontSize = 20 };
        receiptStyle ??= new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.18f, 0.10f, 0.07f) } };
        buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = new Color(1f, 0.86f, 0.42f) } };
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (GameManager.I != null)
        {
            GameManager.I.OnLevelCompleted -= ShowVictory;
            GameManager.I.OnLevelFailedEvent -= ShowFailure;
        }
    }
}
