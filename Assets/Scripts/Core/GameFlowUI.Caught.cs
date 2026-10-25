using UnityEngine;
using UnityEngine.UI;

// Uses the supplied strip directly: UV animation, not a replacement drawing.
public sealed partial class GameFlowUI
{
    public const float CaughtAnimationDuration = 2.2f;
    Texture2D caughtFrames;
    Texture2D caughtRoundTexture;
    Sprite caughtRoundSprite;
    RawImage caughtPortrait;
    Image caughtBackground, caughtBorder, caughtPaper;
    float caughtElapsed;
    GUIStyle caughtCaption, caughtNumber, caughtButton;
    public bool CaughtArtworkLoaded => caughtFrames;
    public bool CaughtVisible => State == ScreenState.Failure && caughtPortrait && caughtPortrait.enabled;
    public bool CaughtActionsVisible => State == ScreenState.Failure && caughtElapsed >= CaughtAnimationDuration;
    public int CaughtFrame => FrameAt(caughtElapsed);
    public int CaughtItems => GameManager.I ? GameManager.I.Receipt.Count : 0;
    public long CaughtDamage => GameManager.I ? GameManager.I.DamageAmount : 0L;
    public static int FrameAt(float elapsed) => elapsed < .55f ? 0 : elapsed < 1.2f ? 1 : 2;

    void StartCaughtPresentation()
    {
        caughtElapsed = 0f;
        if (caughtPortrait) return;
        caughtFrames = Resources.Load<Texture2D>("Owner/GirlCaught");
        if (!caughtFrames) return; // The original failure menu remains a safe fallback.
        var root = new GameObject("Caught artwork", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CreateCaughtRoundSprite();
        caughtBackground = CaughtImage(root.transform, "Sage background", new Color(.67f,.75f,.58f), false);
        caughtBorder = CaughtImage(root.transform, "Cocoa card outline", new Color(.30f,.16f,.08f), true);
        caughtPaper = CaughtImage(root.transform, "Cream paper", new Color(1f,.98f,.93f), true);
        var art = new GameObject("Supplied caught frames", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        art.transform.SetParent(root.transform, false);
        caughtPortrait = art.GetComponent<RawImage>();
        caughtPortrait.texture = caughtFrames;
        caughtPortrait.raycastTarget = false;
        caughtPortrait.rectTransform.anchorMin = caughtPortrait.rectTransform.anchorMax = new Vector2(0f, 1f);
        caughtPortrait.rectTransform.pivot = new Vector2(.5f, .5f);
        caughtPortrait.enabled = false;
    }

    void LateUpdate()
    {
        if (!caughtPortrait) return;
        caughtPortrait.enabled = State == ScreenState.Failure;
        caughtBackground.enabled = caughtBorder.enabled = caughtPaper.enabled = caughtPortrait.enabled;
        if (!caughtPortrait.enabled) return;
        // The world is frozen on failure; the illustration still plays normally.
        caughtElapsed += Time.unscaledDeltaTime;
        CaughtLayout(new Vector2(Screen.width, Screen.height), Screen.safeArea,
            out float scale, out Rect panel, out Rect artwork, out _, out _, out _, out _);
        PlaceCaughtImage(caughtBackground, new Rect(0,0,Screen.width,Screen.height), 1);
        PlaceCaughtImage(caughtBorder, panel, scale);
        PlaceCaughtImage(caughtPaper, Inset(panel,4), scale);
        caughtBorder.pixelsPerUnitMultiplier = 32f / (52f * scale);
        caughtPaper.pixelsPerUnitMultiplier = 32f / (48f * scale);
        float enter = Mathf.SmoothStep(.96f, 1f, Mathf.Clamp01(caughtElapsed / .18f));
        caughtPortrait.rectTransform.anchoredPosition = new Vector2(artwork.center.x, -artwork.center.y) * scale;
        caughtPortrait.rectTransform.sizeDelta = artwork.size * scale * enter;
        // Half-texel inset prevents neighbouring frames bleeding at small sizes.
        float inset = .5f / caughtFrames.width;
        caughtPortrait.uvRect = new Rect(CaughtFrame / 3f + inset, .5f / caughtFrames.height,
            1f / 3f - 2f * inset, 1f - 1f / caughtFrames.height);
    }

    void OnDisable()
    {
        if (caughtPortrait) caughtPortrait.enabled = false;
        if (caughtBackground) caughtBackground.enabled = caughtBorder.enabled = caughtPaper.enabled = false;
    }

    Image CaughtImage(Transform parent, string name, Color color, bool rounded)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent,false);
        var image = go.GetComponent<Image>();
        image.color = color; image.raycastTarget = false; image.enabled = false;
        if(rounded) { image.sprite=caughtRoundSprite; image.type=Image.Type.Sliced; }
        return image;
    }

    static void PlaceCaughtImage(Image image, Rect r, float scale)
    {
        var t=image.rectTransform;
        t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1);
        t.anchoredPosition=new Vector2(r.x,-r.y)*scale;t.sizeDelta=r.size*scale;
    }

    void CreateCaughtRoundSprite()
    {
        const int size=128; const float radius=32f;
        caughtRoundTexture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        caughtRoundTexture.wrapMode=TextureWrapMode.Clamp;
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            Vector2 q=new Vector2(Mathf.Abs(x+.5f-size*.5f),Mathf.Abs(y+.5f-size*.5f))-Vector2.one*(size*.5f-radius);
            float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
            pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(.5f-d));
        }
        caughtRoundTexture.SetPixels(pixels);caughtRoundTexture.Apply();
        caughtRoundSprite=Sprite.Create(caughtRoundTexture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(40,40,40,40));
    }

    void ReleaseCaughtArtwork()
    {
        if(caughtRoundSprite)Destroy(caughtRoundSprite);
        if(caughtRoundTexture)Destroy(caughtRoundTexture);
    }

    public static void CaughtLayout(Vector2 screen, Rect safe, out float scale, out Rect panel,
        out Rect artwork, out Rect items, out Rect damage, out Rect again, out Rect home)
    {
        scale = Mathf.Max(.01f, Mathf.Min(screen.x / 1280f, screen.y / 720f));
        var area = new Rect(safe.x / scale, (screen.y - safe.yMax) / scale, safe.width / scale, safe.height / scale);
        float height = Mathf.Min(684f, area.height - 24f);
        float width = Mathf.Min(height * 1.02f, area.width - 24f);
        // Portrait devices retain a compact card rather than stretch the artwork.
        height = Mathf.Min(height, width / .88f);
        panel = new Rect(area.center.x - width / 2f, area.center.y - height / 2f, width, height);
        float artSize = height * .595f;
        artwork = new Rect(panel.center.x - artSize / 2f, panel.y + height * .018f, artSize, artSize);
        float inset = width * .045f, gap = width * .025f;
        float statWidth = (width - inset * 2f - gap) / 2f;
        items = new Rect(panel.x + inset, panel.y + height * .627f, statWidth, height * .19f);
        damage = new Rect(items.xMax + gap, items.y, statWidth, items.height);
        again = new Rect(panel.x + inset, panel.y + height * .848f, (width - inset * 2f - gap) * .61f, height * .105f);
        home = new Rect(again.xMax + gap, again.y, width - inset * 2f - gap - again.width, again.height);
    }

    void DrawCaughtScreen()
    {
        CaughtLayout(new Vector2(Screen.width, Screen.height), Screen.safeArea,
            out float scale, out Rect panel, out _, out Rect items, out Rect damage, out Rect again, out Rect home);
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.color = Color.white;
        float width = Screen.width / scale, height = Screen.height / scale;
        for (int side = 0; side < 2; side++)
        for (int row = 0; row < 4; row++)
        {
            float x = side == 0 ? panel.x * (.35f + (row % 2) * .22f)
                : panel.xMax + (width - panel.xMax) * (.42f + (row % 2) * .2f);
            DrawCaughtPaw(new Vector2(x, height * (.16f + row * .22f)), 38f + row % 2 * 10f);
        }
        // Background, paper and artwork share one Canvas in back-to-front order.
        // IMGUI only draws stats/buttons below the artwork, never covers it.
        float factor = panel.height / 684f;
        SetupCaughtStyles(factor);
        DrawCaughtStat(items, "破坏物品", CaughtItems.ToString(), factor);
        DrawCaughtStat(damage, "破坏金额", $"¥{CaughtDamage:N0}", factor);
        if (CaughtActionsVisible)
        {
            if (DrawCaughtButton(again, "再玩一次", new Color(.52f, .65f, .43f), Color.white)) RestartGame();
            if (DrawCaughtButton(home, "返回首页", new Color(1f, .97f, .88f), new Color(.30f, .16f, .08f))) ReturnToMainMenu();
        }
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    static Rect Inset(Rect r, float inset) => new Rect(r.x + inset, r.y + inset, r.width - inset * 2, r.height - inset * 2);
    static void CaughtTile(Rect r, Color color, float radius)
        => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color.linear, 0, radius);

    static void DrawCaughtPaw(Vector2 centre, float size)
    {
        var color = new Color(.84f, .89f, .71f, .42f);
        CaughtTile(new Rect(centre.x - size * .34f, centre.y, size * .68f, size * .52f), color, size * .3f);
        for (int i = 0; i < 4; i++)
            CaughtTile(new Rect(centre.x + (i - 2f) * size * .25f, centre.y - size * (.30f + (i == 1 || i == 2 ? .12f : 0)),
                size * .20f, size * .26f), color, size * .12f);
    }

    void SetupCaughtStyles(float factor)
    {
        caughtCaption ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        caughtNumber ??= new GUIStyle(caughtCaption);
        caughtButton ??= new GUIStyle(caughtCaption);
        caughtCaption.fontSize = Mathf.RoundToInt(24f * factor);
        caughtNumber.fontSize = Mathf.RoundToInt(48f * factor);
        caughtButton.fontSize = Mathf.RoundToInt(26f * factor);
        SetCaughtInk(caughtCaption, new Color(.30f, .16f, .08f));
        SetCaughtInk(caughtNumber, new Color(.30f, .16f, .08f));
    }

    static void SetCaughtInk(GUIStyle style, Color ink)
    {
        // Editor skins can supply white hover text; keep cream buttons legible
        // in every pointer/focus state, including immediately after a click.
        Color color = ink.linear;
        style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = color;
        style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = color;
    }

    void DrawCaughtStat(Rect r, string label, string value, float factor)
    {
        CaughtTile(r, new Color(1f, .82f, .45f), 28f);
        CaughtTile(Inset(r, 4f), new Color(1f, .95f, .80f), 24f);
        GUI.Label(new Rect(r.x, r.y + 7f, r.width, r.height * .36f), label, caughtCaption);
        // Keep real, potentially large totals inside the card.
        caughtNumber.fontSize = Mathf.RoundToInt(48f * factor);
        while (caughtNumber.fontSize > 18 && caughtNumber.CalcSize(new GUIContent(value)).x > r.width - 20f)
            caughtNumber.fontSize--;
        GUI.Label(new Rect(r.x + 8f, r.y + r.height * .36f, r.width - 16f, r.height * .60f), value, caughtNumber);
    }

    bool DrawCaughtButton(Rect r, string label, Color fill, Color ink)
    {
        CaughtTile(r, new Color(.30f, .16f, .08f), r.height / 2f);
        bool hover = r.Contains(Event.current.mousePosition);
        CaughtTile(Inset(r, 4f), hover ? Color.Lerp(fill, Color.white, .08f) : fill, r.height / 2f - 4f);
        SetCaughtInk(caughtButton, ink);
        GUI.Label(r, label, caughtButton);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }
}
