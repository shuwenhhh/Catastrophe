using UnityEngine;
using UnityEngine.UI;

public sealed partial class GameFlowUI
{
    Texture2D coverArt;
    Canvas coverCanvas;
    RawImage coverImage;
    GUIStyle coverTitle, coverCopy, coverSmall, coverButton, coverArrow, coverButtonSmall, coverPanelTitle;
    GameSaveStore.Snapshot menuSave;
    GameSaveStore.History menuHistory;
    bool coverLoaded, showingRecords, confirmingNew;
    int recordsPage;
    string menuNotice;

    void LoadCover()
    {
        if (coverLoaded) return;
        coverLoaded = true;
        coverArt = Resources.Load<Texture2D>("Menu/CatastropheMainMenu");
        if (coverArt)
        {
            var canvasObject = new GameObject("Main menu artwork", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            coverCanvas = canvasObject.GetComponent<Canvas>();
            coverCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            coverCanvas.sortingOrder = -100;
            var imageObject = new GameObject("Cover image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            coverImage = imageObject.GetComponent<RawImage>();
            coverImage.texture = coverArt;
            coverImage.raycastTarget = false;
            var imageRect = coverImage.rectTransform;
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0f, 1f);
            imageRect.pivot = new Vector2(0f, 1f);
        }
        RefreshMenuData();
        var ink = new Color(.28f, .20f, .17f).linear;
        coverTitle = new GUIStyle(GUI.skin.label) { fontSize = 58, fontStyle = FontStyle.Bold, normal = { textColor = ink } };
        coverCopy = new GUIStyle(coverTitle) { fontSize = 23, fontStyle = FontStyle.Normal, normal = { textColor = new Color(.43f,.34f,.29f).linear } };
        coverSmall = new GUIStyle(coverCopy) { fontSize = 16 };
        coverButton = new GUIStyle(coverTitle) { fontSize = 27, normal = { textColor = Color.white } };
        coverArrow = new GUIStyle(coverButton) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
        coverButtonSmall = new GUIStyle(GUI.skin.label) { fontSize=16, normal={textColor=new Color(1,1,1,.9f)} };
        coverPanelTitle = new GUIStyle(coverTitle) { fontSize=36 };
    }
    void RefreshMenuData() { menuSave = GameSaveStore.Read(); menuHistory = GameSaveStore.ReadHistory(); }
    void Tile(Rect rect, Color color)
    {
        GUI.color = Color.white;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color.linear, 0, 18);
        GUI.color = Color.white;
    }
    bool CoverButton(Rect rect, string title, string english, Color color, bool enabled = true)
    {
        bool hover = rect.Contains(Event.current.mousePosition) && enabled;
        Tile(new Rect(rect.x, rect.y + 5, rect.width, rect.height), new Color(.29f,.18f,.12f, enabled ? .16f : .06f));
        Tile(rect, enabled ? (hover ? Color.Lerp(color, Color.white, .10f) : color) : new Color(.72f,.68f,.61f,.66f));
        GUI.Label(new Rect(rect.x + 28, rect.y + 10, rect.width - 82, 38), title, coverButton);
        GUI.Label(new Rect(rect.x + 30, rect.y + 47, rect.width - 82, 24), english, coverButtonSmall);
        GUI.Label(new Rect(rect.xMax - 61, rect.y + 11, 45, 55), "›", coverArrow);
        bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && enabled;
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        GUI.enabled = wasEnabled;
        return clicked;
    }
    void DrawCoverMenu()
    {
        LoadCover();
        var previous = GUI.matrix;
        float scale = Screen.height / 900f;
        float width = Screen.width / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
        GUI.color = Color.white;
        Rect safe = new Rect(Screen.safeArea.x / scale, (Screen.height-Screen.safeArea.yMax)/scale,
            Screen.safeArea.width/scale, Screen.safeArea.height/scale);
        float artAspect = coverArt ? (float)coverArt.width/coverArt.height : 16f/9f;
        float artWidth = Mathf.Min(safe.width,safe.height*artAspect);
        Rect artRect = new Rect(safe.center.x-artWidth*.5f,safe.center.y-artWidth/artAspect*.5f,artWidth,artWidth/artAspect);
        bool useUiArtwork = coverImage;
        if (coverCanvas) coverCanvas.enabled = useUiArtwork;
        if (useUiArtwork)
        {
            coverImage.rectTransform.anchoredPosition = new Vector2(artRect.x, -artRect.y) * scale;
            coverImage.rectTransform.sizeDelta = artRect.size * scale;
        }
        GUI.color = new Color(.98f,.95f,.87f);
        if (useUiArtwork)
        {
            // IMGUI is rendered over the Canvas: fill only the letterbox area,
            // otherwise its opaque background would hide the colour-correct image.
            GUI.DrawTexture(new Rect(0, 0, width, artRect.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, artRect.yMax, width, 900 - artRect.yMax), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, artRect.y, artRect.x, artRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(artRect.xMax, artRect.y, width - artRect.xMax, artRect.height), Texture2D.whiteTexture);
        }
        else GUI.DrawTexture(new Rect(0,0,width,900),Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (coverArt && !useUiArtwork) GUI.DrawTexture(artRect, coverArt, ScaleMode.StretchToFill);
        else if (!coverArt) { GUI.color = new Color(.96f,.89f,.77f); GUI.DrawTexture(new Rect(0,0,width,900),Texture2D.whiteTexture); GUI.color = Color.white; }
        float left = Mathf.Max(Screen.safeArea.xMin / scale + 42, width * .065f);
        float menuWidth = Mathf.Min(460, width * .36f);
        bool menuEnabled = GUI.enabled;
        GUI.enabled = menuEnabled && !showingRecords && !confirmingNew;
        if (ArtworkButton(artRect,new Rect(.099f,.370f,.284f,.118f)))
        {
            if (menuSave != null) confirmingNew = true;
            else StartNewFromCover();
        }
        if (ArtworkButton(artRect,new Rect(.150f,.510f,.175f,.080f),menuSave != null))
        {
            if (GameSaveStore.Restore()) { runActive = true; nextAutosave = Time.unscaledTime + 15f; ResumeGame(); }
            else menuNotice = "这份进度暂时无法读取，请开始新游戏。";
        }
        if (ArtworkButton(artRect,new Rect(.120f,.610f,.225f,.080f)))
        { RefreshMenuData(); recordsPage=0; showingRecords = true; }
        GUI.enabled = menuEnabled;
        if(!string.IsNullOrEmpty(menuNotice)) GUI.Label(new Rect(left,740,menuWidth+100,60),menuNotice,coverSmall);
        if (showingRecords) DrawRecords(width);
        if (confirmingNew) DrawNewConfirmation(width);
        GUI.matrix = previous; GUI.color = Color.white;
    }
    bool ArtworkButton(Rect art, Rect normalized, bool enabled=true)
    {
        Rect rect = new Rect(art.x+normalized.x*art.width,art.y+normalized.y*art.height,
            normalized.width*art.width,normalized.height*art.height);
        bool previous=GUI.enabled;
        GUI.enabled=previous&&enabled;
        if(!enabled)
        {
            GUI.color=new Color(1f,.98f,.92f,.60f);
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=Color.white;
        }
        bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);
        GUI.enabled=previous;
        return clicked;
    }
    void StartNewFromCover()
    {
        if (menuSave != null) GameSaveStore.Archive("未完成",menuSave.score,menuSave.elapsed);
        GameSaveStore.ClearResume(); confirmingNew = false; menuSave = null;
        // The title screen owns a fresh scene; Resume is the only path that
        // applies a snapshot, so a new run starts with pristine props.
        BeginGame();
        GameSaveStore.Capture();
    }
    void DrawRecords(float width)
    {
        GUI.color = new Color(.18f,.13f,.1f,.42f); GUI.DrawTexture(new Rect(0,0,width,900),Texture2D.whiteTexture); GUI.color = Color.white;
        var panel = new Rect(width*.5f-350,85,700,755);
        Tile(panel,new Color(.99f,.96f,.89f));
        GUI.Label(new Rect(panel.x+44,130,550,55),"我的游戏记录",coverPanelTitle);
        GUI.Label(new Rect(panel.x+44,194,550,35),$"历史最高   {PlayerPrefs.GetInt(HighScoreKey,0)} 分",coverCopy);
        float y=260;
        if(menuSave!=null)
        {
            Tile(new Rect(panel.x+36,y,628,78),new Color(.88f,.91f,.83f));
            GUI.Label(new Rect(panel.x+56,y+9,580,30),$"等待继续   ·   {menuSave.score} 分",coverCopy);
            GUI.Label(new Rect(panel.x+56,y+44,580,26),menuSave.date,coverSmall); y+=94;
        }
        if(menuHistory.records.Count==0)
            GUI.Label(new Rect(panel.x+48,y+40,610,80),"还没有完成的记录\n每一次淘气，都会留下小小的回忆。",new GUIStyle(coverCopy){wordWrap=true});
        else foreach(var record in menuHistory.records.GetRange(recordsPage*4,Mathf.Min(4,menuHistory.records.Count-recordsPage*4)))
        {
            GUI.Label(new Rect(panel.x+50,y,570,32),$"{record.result}    ·    {record.score} 分",coverCopy);
            GUI.Label(new Rect(panel.x+50,y+33,570,28),$"{record.date}    ·    {(int)record.seconds/60:00}:{(int)record.seconds%60:00}",coverSmall); y+=81;
        }
        if(menuHistory.records.Count>4)
        {
            if(recordsPage>0&&GUI.Button(new Rect(panel.x+90,688,145,38),"上一页",coverSmall))recordsPage--;
            GUI.Label(new Rect(panel.x+310,688,90,38),$"{recordsPage+1} / {(menuHistory.records.Count+3)/4}",coverSmall);
            if((recordsPage+1)*4<menuHistory.records.Count&&GUI.Button(new Rect(panel.x+465,688,145,38),"下一页",coverSmall))recordsPage++;
        }
        if(CoverButton(new Rect(panel.x+180,737,340,76),"返回首页","BACK",new Color(.71f,.36f,.27f)))showingRecords=false;
    }
    void DrawNewConfirmation(float width)
    {
        GUI.color=new Color(.18f,.13f,.1f,.45f);GUI.DrawTexture(new Rect(0,0,width,900),Texture2D.whiteTexture);GUI.color=Color.white;
        var panel=new Rect(width*.5f-330,270,660,340);Tile(panel,new Color(.99f,.96f,.89f));
        GUI.Label(new Rect(panel.x+40,304,580,52),"开始新的冒险？",coverPanelTitle);
        GUI.Label(new Rect(panel.x+40,375,580,72),"新游戏会替换续玩进度。\n这次的分数会保留在游戏记录里。",coverCopy);
        if(CoverButton(new Rect(panel.x+38,482,280,84),"开始新游戏","NEW GAME",new Color(.71f,.36f,.27f)))StartNewFromCover();
        if(CoverButton(new Rect(panel.x+342,482,280,84),"再想一下","BACK",new Color(.48f,.52f,.43f)))confirmingNew=false;
    }
}
