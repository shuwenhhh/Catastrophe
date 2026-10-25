using UnityEngine;
using UnityEngine.UI;

// A single non-interactive, colour-correct overlay. Controls retain input ownership.
public sealed class ChaosJuiceUI : MonoBehaviour
{
    static readonly Color Cocoa = new(.30f,.15f,.08f);
    static readonly Color Cream = new(1f,.97f,.89f,.98f);
    static readonly Color Orange = new(1f,.65f,.20f);
    Canvas canvas;
    RectTransform root, amountPanel, noisePanel, timerPanel;
    Texture2D girlFrames, noiseTexture;
    RawImage girlPortrait;
    Text amountText, noiseText, timerText;
    Image noiseFill, countdownRing;
    Sprite round, circle;
    Font font;
    public bool GirlArtworkLoaded => girlFrames != null;
    public bool NoiseArtworkLoaded => noiseTexture != null;
    public bool GirlVisible => GameFlowUI.GameplayInputEnabled && OwnerAlert.I &&
        OwnerAlert.I.IsInspecting && girlFrames;
    public bool CountdownVisible => GameFlowUI.GameplayInputEnabled && OwnerAlert.I && OwnerAlert.I.IsWarning;

    void Awake()
    {
        girlFrames = Resources.Load<Texture2D>("Owner/GirlInspection");
        noiseTexture = Resources.Load<Texture2D>("Hud/NoisePaw");
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        round = Shape(false); circle = Shape(true);
        var go = new GameObject("Noise search HUD", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        root = Rect("HUD layout", go.transform);
        amountPanel = Panel("Total damage", root, new Rect(0,0,285,78));
        var coin = Graphic("Coin rim", amountPanel, new Rect(14,14,48,48), circle, Cocoa);
        Graphic("Coin", coin.transform, new Rect(3,3,42,42), circle, new Color(1f,.75f,.28f));
        Label("Coin mark", coin.transform, new Rect(0,0,48,48), "$", 29, Cocoa, TextAnchor.MiddleCenter);
        Label("Amount caption", amountPanel, new Rect(76,9,200,23), "TOTAL DAMAGE", 18, Cocoa);
        amountText = Label("Amount", amountPanel, new Rect(76,31,198,39), "¥0", 32, Cocoa);
        noisePanel = Panel("Noise", root, new Rect(0,0,356,78));
        var icon = new GameObject("Approved noise paw",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RawImage>();
        icon.transform.SetParent(noisePanel,false); icon.texture = noiseTexture; icon.raycastTarget = false;
        Place(icon.rectTransform,new Rect(8,7,64,64));
        Label("Noise caption",noisePanel,new Rect(81,8,150,25),"NOISE",18,Cocoa);
        noiseText = Label("Noise amount",noisePanel,new Rect(251,34,95,30),"0 / 100",18,Cocoa,TextAnchor.MiddleCenter);
        var track = Graphic("Noise track",noisePanel,new Rect(81,40,165,20),round,Cocoa);
        Graphic("Noise empty",track.transform,new Rect(2,2,161,16),round,new Color(.90f,.83f,.69f));
        noiseFill = Graphic("Noise fill",track.transform,new Rect(2,2,161,16),round,Orange);
        noiseFill.type = Image.Type.Filled; noiseFill.fillMethod=Image.FillMethod.Horizontal;
        timerPanel = Rect("Hiding countdown",root);
        Graphic("Timer rim",timerPanel,new Rect(0,0,104,104),circle,Cocoa);
        Graphic("Timer cream",timerPanel,new Rect(2,2,100,100),circle,Cream);
        Graphic("Timer track",timerPanel,new Rect(7,7,90,90),circle,new Color(.97f,.86f,.76f));
        countdownRing=Graphic("Remaining ring",timerPanel,new Rect(7,7,90,90),circle,new Color(.98f,.49f,.32f));
        countdownRing.type=Image.Type.Filled;countdownRing.fillMethod=Image.FillMethod.Radial360;
        countdownRing.fillOrigin=2;countdownRing.fillClockwise=true;
        Graphic("Timer centre",timerPanel,new Rect(15,15,74,74),circle,Cream);
        timerText=Label("Seconds",timerPanel,new Rect(15,18,74,65),"5s",39,Cocoa,TextAnchor.MiddleCenter);
        girlPortrait=new GameObject("Girl frames",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RawImage>();
        girlPortrait.transform.SetParent(root,false);girlPortrait.texture=girlFrames;girlPortrait.raycastTarget=false;
        canvas.enabled=false;
    }
    void LateUpdate()
    {
        if (!canvas) return; // Live script reload: rebuild on the next normal scene load.
        canvas.enabled=GameFlowUI.GameplayInputEnabled;
        if(!canvas.enabled)return;
        HudLayout(new Vector2(Screen.width,Screen.height),Screen.safeArea,out float scale,out Rect amount,out Rect noise,out Rect timer,out Rect portrait);
        root.localScale=new Vector3(scale,scale,1f);
        Place(amountPanel,amount);Place(noisePanel,noise);Place(timerPanel,timer);
        amountText.text = "¥" + (GameManager.I ? GameManager.I.DamageAmount : 0L).ToString("N0");
        float value=NoiseManager.I?NoiseManager.I.noise:0f;
        noiseText.text=Mathf.CeilToInt(value)+" / 100";
        noiseFill.fillAmount=value/100f;
        timerPanel.gameObject.SetActive(CountdownVisible);
        if(CountdownVisible)
        {
            timerText.text=Mathf.CeilToInt(OwnerAlert.I.SecondsRemaining)+"s";
            countdownRing.fillAmount=OwnerAlert.I.SecondsRemaining/OwnerAlert.WarningDuration;
        }
        girlPortrait.enabled=GirlVisible;
        if(GirlVisible)
        {
            Place(girlPortrait.rectTransform,portrait);
            girlPortrait.uvRect=new Rect(OwnerAlert.I.InspectionFrame/3f,0,1f/3f,1);
        }
    }
    void OnDisable(){if(canvas)canvas.enabled=false;}
    public static void HudLayout(Vector2 screen, Rect safe, out float scale, out Rect amount,
        out Rect noise, out Rect timer, out Rect portrait)
    {
        scale=Mathf.Max(.05f,Mathf.Min(screen.x/1280f,screen.y/720f));
        float left=safe.xMin/scale+18, top=(screen.y-safe.yMax)/scale+16;
        float right=safe.xMax/scale-MobileCatControls.ControlRadius(screen,safe)*1.6f/scale-12;
        amount=new Rect(left,top,285,78);
        noise=new Rect(right-356,top,356,78);
        timer=new Rect((amount.xMax+noise.xMin)*.5f-52,top,104,104);
        portrait=new Rect(timer.center.x-85,timer.yMax+10,170,170);
    }
    public static void Layout(Vector2 screen,Rect safe,out float scale,out Rect amount,out Rect timer,out Rect portrait)
    { HudLayout(screen,safe,out scale,out amount,out _,out timer,out portrait); }
    RectTransform Panel(string name, Transform parent,Rect bounds)
    {
        var border=Graphic(name,parent,bounds,round,Cocoa);
        Graphic("Cream",border.transform,new Rect(2,2,bounds.width-4,bounds.height-4),round,Cream);
        return border.rectTransform;
    }
    static RectTransform Rect(string name,Transform parent)
    {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=Vector2.zero;
        return rt;
    }
    static void Place(RectTransform rt,Rect r)
    {
        rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);
        rt.anchoredPosition=new Vector2(r.x,-r.y);rt.sizeDelta=r.size;
    }
    Image Graphic(string name,Transform parent,Rect bounds,Sprite sprite,Color color)
    {
        var rt=Rect(name,parent);Place(rt,bounds);
        var image=rt.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;
        image.type=sprite==round?Image.Type.Sliced:Image.Type.Simple;image.raycastTarget=false;return image;
    }
    Text Label(string name,Transform parent,Rect bounds,string text,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var rt=Rect(name,parent);Place(rt,bounds);var label=rt.gameObject.AddComponent<Text>();
        label.font=font;label.fontSize=size;label.fontStyle=FontStyle.Bold;label.text=text;label.color=color;
        label.alignment=align;label.raycastTarget=false;return label;
    }
    static Sprite Shape(bool disc)
    {
        const int n=128;var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            var p=new Vector2(Mathf.Abs(x-63.5f),Mathf.Abs(y-63.5f));
            float distance=disc?p.magnitude-62f:new Vector2(Mathf.Max(0,p.x-39f),Mathf.Max(0,p.y-39f)).magnitude-23f;
            pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(.5f-distance));
        }
        var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){name="HUD geometry",filterMode=FilterMode.Bilinear};
        tex.SetPixels(pixels);tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,
            disc?Vector4.zero:new Vector4(26,26,26,26));
    }
    void OnDestroy()
    {
        if(round){Destroy(round.texture);Destroy(round);}
        if(circle){Destroy(circle.texture);Destroy(circle);}
    }
}
