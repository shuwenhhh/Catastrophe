using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>Owns the main menu, pause state and both end-of-run panels.</summary>
public sealed partial class GameFlowUI : MonoBehaviour
{
    public enum ScreenState { MainMenu, Playing, Paused, Results, Failure }

    private const string HighScoreKey = "Catastrophe.HighScore";
    private static bool autoStartNextLoad;
    private bool runActive;
    private float nextAutosave;
    public ScreenState State { get; private set; } = ScreenState.MainMenu;
    public static bool GameplayInputEnabled => FindAnyObjectByType<GameFlowUI>() is { State: ScreenState.Playing };

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle resultTitleStyle;
    private GUIStyle resultCaptionStyle;
    private GUIStyle resultValueStyle;
    private GUIStyle resultRowStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => autoStartNextLoad = false;

    private void Start()
    {
        if (GameManager.I != null)
        {
            GameManager.I.OnLevelCompleted += ShowCompletionResults;
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
        if (State == ScreenState.Playing && runActive && Time.unscaledTime >= nextAutosave)
        {
            GameSaveStore.Capture(); nextAutosave = Time.unscaledTime + 15f;
        }
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
        OwnerAlert.I?.BeginRun();
        runActive = true;
        nextAutosave = Time.unscaledTime + 15f;
        SetState(ScreenState.Playing);
    }

    public void ResumeGame() => SetState(ScreenState.Playing);

    public void TogglePause()
    {
        if (State == ScreenState.Playing) SetState(ScreenState.Paused);
        else if (State == ScreenState.Paused) SetState(ScreenState.Playing);
    }

    public void RestartGame()
    {
        if (runActive) GameSaveStore.Archive("重新开始", GameManager.I.chaosScore, GameManager.I.ElapsedTime);
        GameSaveStore.ClearResume(); runActive = false;
        autoStartNextLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        if (runActive) GameSaveStore.Capture();
        runActive = false;
        autoStartNextLoad = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ShowMainMenu() => SetState(ScreenState.MainMenu);
    private void ShowCompletionResults() { FinishRun("完成挑战"); SetState(ScreenState.Results); }
    private void ShowFailure()
    {
        if (State == ScreenState.Failure) return;
        FinishRun("被主人发现");
        StartCaughtPresentation();
        SetState(ScreenState.Failure);
    }
    private void FinishRun(string result)
    {
        if (runActive) GameSaveStore.Archive(result, GameManager.I.chaosScore, GameManager.I.ElapsedTime);
        runActive = false; GameSaveStore.ClearResume();
    }
    private void OnApplicationPause(bool paused) { if (paused && runActive) GameSaveStore.Capture(); }
    private void OnApplicationQuit() { if (runActive) GameSaveStore.Capture(); }

    private void SetState(ScreenState state)
    {
        if (state == ScreenState.Paused && runActive) GameSaveStore.Capture();
        State = state;
        if (coverCanvas) coverCanvas.enabled = state == ScreenState.MainMenu;
        Time.timeScale = state == ScreenState.Playing ? 1f : 0f;
        AudioListener.pause = state == ScreenState.Paused;
    }

    private void OnGUI()
    {
        if (State == ScreenState.Playing)
            return;

        SetupStyles();
        if (State == ScreenState.MainMenu) { DrawCoverMenu(); return; }
        if (State == ScreenState.Failure && CaughtArtworkLoaded) { DrawCaughtScreen(); return; }
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
            case ScreenState.Paused: DrawPause(width, height); break;
            case ScreenState.Results: DrawCompletionResults(width, height); break;
            case ScreenState.Failure: DrawFailure(width, height); break;
        }
        GUI.matrix = oldMatrix;
    }

    private void DrawPause(float width, float height)
    {
        DrawPanel(width, height, "暂停一下", out Rect panel);
        if (WarmButton(new Rect(panel.x + 80f, panel.y + 105f, panel.width - 160f, 48f), "继续游戏")) ResumeGame();
        if (WarmButton(new Rect(panel.x + 80f, panel.y + 165f, panel.width - 160f, 48f), "重新开始")) RestartGame();
        if (WarmButton(new Rect(panel.x + 80f, panel.y + 225f, panel.width - 160f, 48f), "返回主菜单")) ReturnToMainMenu();
    }

    private void DrawCompletionResults(float width, float height)
    {
        // Match the caught screen and title screen: sage backdrop, cream paper,
        // cocoa outlines, rounded controls and warm yellow accents.
        Rect safe = new Rect(Screen.safeArea.x / GUI.matrix.m00,
            (Screen.height - Screen.safeArea.yMax) / GUI.matrix.m11,
            Screen.safeArea.width / GUI.matrix.m00, Screen.safeArea.height / GUI.matrix.m11);
        float panelHeight = Mathf.Min(790f, safe.height - 36f);
        float panelWidth = Mathf.Min(610f, safe.width - 36f);
        Rect panel = new Rect(safe.center.x - panelWidth * .5f, safe.center.y - panelHeight * .5f, panelWidth, panelHeight);
        GUI.color = Color.white;
        CaughtTile(new Rect(0, 0, width, height), new Color(.67f,.75f,.58f), 0);
        DrawResultPaws(width, height, panel);
        CaughtTile(panel, new Color(.30f,.16f,.08f), 42f);
        Rect paper = Inset(panel, 5f);
        CaughtTile(paper, new Color(1f,.98f,.93f), 37f);

        SetupResultStyles(panel.height / 790f);
        SetupCaughtStyles(panel.height / 790f);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 24f, panel.width - 60f, 55f), "DAMAGE RECEIPT", resultTitleStyle);
        GUI.Label(new Rect(panel.x + 44f, panel.y + 82f, panel.width - 88f, 30f), "TODAY'S LITTLE CATASTROPHE", resultCaptionStyle);

        Rect list = new Rect(panel.x + 38f, panel.y + 126f, panel.width - 76f, panel.height * .47f);
        CaughtTile(list, new Color(1f,.82f,.45f), 27f);
        CaughtTile(Inset(list, 4f), new Color(1f,.95f,.80f), 23f);
        float rowY = list.y + 15f;
        int shown = 0;
        if (GameManager.I != null)
        {
            foreach (GameManager.ChaosEntry entry in GameManager.I.Receipt)
            {
                if (rowY + 34f > list.yMax - 10f) break;
                string dots = new string('·', Mathf.Clamp(24 - entry.itemName.Length, 4, 24));
                GUI.Label(new Rect(list.x + 24f, rowY, list.width - 48f, 32f),
                    $"{entry.itemName}  {dots}  ¥{entry.points * 100:N0}", resultRowStyle);
                rowY += 36f; shown++;
            }
            if (shown < GameManager.I.Receipt.Count)
                GUI.Label(new Rect(list.x + 24f, list.yMax - 38f, list.width - 48f, 28f),
                    $"+ {GameManager.I.Receipt.Count - shown} more", resultCaptionStyle);
        }

        float statsY = list.yMax + 18f;
        float gap = 14f, statWidth = (list.width - gap) * .5f;
        Rect items = new Rect(list.x, statsY, statWidth, 104f);
        Rect damage = new Rect(items.xMax + gap, statsY, statWidth, 104f);
        DrawResultStat(items, "ITEMS BROKEN", GameManager.I != null ? GameManager.I.Receipt.Count.ToString() : "0");
        DrawResultStat(damage, "TOTAL DAMAGE", GameManager.I != null ? $"¥{GameManager.I.DamageAmount:N0}" : "¥0");

        Rect again = new Rect(panel.x + 38f, panel.yMax - 88f, (panel.width - 90f) * .61f, 58f);
        Rect home = new Rect(again.xMax + 14f, again.y, panel.xMax - 38f - again.xMax - 14f, 58f);
        if (DrawCaughtButton(again, "PLAY AGAIN", new Color(.52f,.65f,.43f), Color.white)) RestartGame();
        if (DrawCaughtButton(home, "HOME", new Color(1f,.97f,.88f), new Color(.30f,.16f,.08f))) ReturnToMainMenu();
    }

    private void SetupResultStyles(float factor)
    {
        Color cocoa = new Color(.30f,.16f,.08f);
        resultTitleStyle ??= new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold };
        resultCaptionStyle ??= new GUIStyle(resultTitleStyle);
        resultValueStyle ??= new GUIStyle(resultTitleStyle);
        resultRowStyle ??= new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleLeft, fontStyle=FontStyle.Bold };
        resultTitleStyle.fontSize=Mathf.RoundToInt(40f*factor);
        resultCaptionStyle.fontSize=Mathf.RoundToInt(16f*factor);
        resultValueStyle.fontSize=Mathf.RoundToInt(34f*factor);
        resultRowStyle.fontSize=Mathf.RoundToInt(18f*factor);
        SetCaughtInk(resultTitleStyle,cocoa);SetCaughtInk(resultCaptionStyle,new Color(.56f,.34f,.20f));
        SetCaughtInk(resultValueStyle,cocoa);SetCaughtInk(resultRowStyle,cocoa);
    }

    private void DrawResultStat(Rect rect, string caption, string value)
    {
        CaughtTile(rect,new Color(1f,.82f,.45f),24f);
        CaughtTile(Inset(rect,4f),new Color(1f,.95f,.80f),20f);
        GUI.Label(new Rect(rect.x+8f,rect.y+7f,rect.width-16f,30f),caption,resultCaptionStyle);
        int original=resultValueStyle.fontSize;
        while(resultValueStyle.fontSize>16&&resultValueStyle.CalcSize(new GUIContent(value)).x>rect.width-18f)resultValueStyle.fontSize--;
        GUI.Label(new Rect(rect.x+8f,rect.y+34f,rect.width-16f,62f),value,resultValueStyle);
        resultValueStyle.fontSize=original;
    }

    private static void DrawResultPaws(float width,float height,Rect panel)
    {
        for(int side=0;side<2;side++)for(int row=0;row<4;row++)
        {
            float x=side==0?panel.x*(.35f+(row%2)*.22f):panel.xMax+(width-panel.xMax)*(.42f+(row%2)*.20f);
            DrawCaughtPaw(new Vector2(x,height*(.16f+row*.22f)),38f+row%2*10f);
        }
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

    private void SetupStyles()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.78f, 0.30f) } };
        subtitleStyle ??= new GUIStyle(titleStyle) { fontSize = 26, normal = { textColor = Color.white } };
        bodyStyle ??= new GUIStyle(subtitleStyle) { fontSize = 20 };
        buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = new Color(1f, 0.86f, 0.42f) } };
    }

    private void OnDestroy()
    {
        ReleaseCaughtArtwork();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (GameManager.I != null)
        {
            GameManager.I.OnLevelCompleted -= ShowCompletionResults;
            GameManager.I.OnLevelFailedEvent -= ShowFailure;
        }
    }
}
