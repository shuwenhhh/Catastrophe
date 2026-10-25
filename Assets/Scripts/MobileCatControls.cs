using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// Runtime controls live with the game rather than in the editable furniture scene.
// Touch IDs are reserved per control so moving the stick cannot orbit the camera.
public sealed class MobileCatControls : MonoBehaviour
{
    public static MobileCatControls Instance { get; private set; }
    public Vector2 Move { get; private set; }
    public bool JumpHeld => jumpTouch != NoTouch;

    private const int NoTouch = -1;
    private const int MouseTouch = -2;
    private int stickTouch = NoTouch;
    private int jumpTouch = NoTouch;
    private int actionTouch = NoTouch;
    private int pauseTouch = NoTouch;
    private readonly HashSet<int> worldPointers = new();
    private readonly HashSet<int> activePointers = new();
    private Vector2 stickPosition;
    private bool jumpPressed;
    private bool jumpReleased;
    private bool actionPressed;
    private Texture2D disc;
    private Texture2D pushIcon;
    private Texture2D jumpIcon;
    private Texture2D ringTrack;
    private Texture2D ringFill;
    private Color32[] ringPixels;
    private byte[] ringCoverage;
    private float[] ringAngles;
    private int lastRingStep = -1;
    private const int RingSize = 256;
    private const int RingSteps = 60;
    private const float JumpHitRadius = 1.14f;
    private const float RingRadius = 1.10f;
    private GUIStyle label;

    // Also fit compact/notched screens instead of letting the minimum radius
    // push hit targets outside the safe area or into the movement stick.
    public static float ControlRadius(Vector2 screenSize, Rect safeArea) =>
        Mathf.Min(Mathf.Clamp(Mathf.Min(screenSize.y * .085f, screenSize.x * .10f), 55f, 140f),
            Mathf.Min(safeArea.width / 9f, safeArea.height / 4.5f));
    public static Vector2 JumpButtonCentre(Rect safeArea, float radius) =>
        new(safeArea.xMax - radius * 2.15f, safeArea.yMin + radius * 1.85f);
    public static Vector2 ActionButtonCentre(Rect safeArea, float radius) =>
        JumpButtonCentre(safeArea, radius) - Vector2.right * radius * 2.5f;

    private float Radius => ControlRadius(new Vector2(Screen.width, Screen.height), Safe);
    private Rect Safe => Screen.safeArea;
    private Vector2 StickCentre => new(Safe.xMin + Radius * 1.4f, Safe.yMin + Radius * 1.45f);
    private Vector2 JumpCentre => JumpButtonCentre(Safe, Radius);
    private Vector2 ActionCentre => ActionButtonCentre(Safe, Radius);
    private Vector2 PauseCentre => new(Safe.xMax - Radius * .85f, Safe.yMax - Radius * .85f);

    public bool ConsumeJumpPressed() { bool value = jumpPressed; jumpPressed = false; return value; }
    public bool ConsumeJumpReleased() { bool value = jumpReleased; jumpReleased = false; return value; }
    public bool ConsumeActionPressed() { bool value = actionPressed; actionPressed = false; return value; }

    public static bool IsControlTouch(int id) => Instance != null &&
        (Instance.stickTouch == id || Instance.jumpTouch == id || Instance.actionTouch == id ||
         Instance.pauseTouch == id);

    public static bool IsScreenPointInControls(Vector2 point)
    {
        if (Instance == null || !GameFlowUI.GameplayInputEnabled) return false;
        MobileCatControls c = Instance;
        float r = c.Radius;
        return Vector2.Distance(point, c.StickCentre) < r * 1.3f ||
               Vector2.Distance(point, c.JumpCentre) < r * JumpHitRadius ||
               Vector2.Distance(point, c.ActionCentre) < r ||
               Vector2.Distance(point, c.PauseCentre) < r * .65f;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        disc = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                pixels[y * 64 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((31f - d) * 1.5f));
            }
        disc.SetPixels(pixels);
        disc.Apply();
        pushIcon=CatActionIcons.Create(false);
        jumpIcon=CatActionIcons.Create(true);
        CreateJumpRing();
    }

    private void Update()
    {
        if (!GameFlowUI.GameplayInputEnabled)
        {
            ResetTouches();
            return;
        }

        bool stickSeen = false, jumpSeen = false, actionSeen = false, pauseSeen = false;
        activePointers.Clear();
        bool hasTouch = false;
        Touchscreen screen = Touchscreen.current;
        if (screen != null)
            foreach (var touch in screen.touches)
            {
                if (!touch.isInProgress) continue;
                hasTouch = true;
                ProcessPointer(touch.touchId.ReadValue(), touch.position.ReadValue(),
                    ref stickSeen, ref jumpSeen, ref actionSeen, ref pauseSeen);
            }

        // In the editor one mouse can try each on-screen control. Real touches
        // take priority, avoiding a synthetic mouse event stealing a finger.
        Mouse mouse = Mouse.current;
        if (!hasTouch && mouse != null && mouse.leftButton.isPressed)
            ProcessPointer(MouseTouch, mouse.position.ReadValue(),
                ref stickSeen, ref jumpSeen, ref actionSeen, ref pauseSeen);

        if (!stickSeen) { stickTouch = NoTouch; Move = Vector2.zero; stickPosition = StickCentre; }
        if (!jumpSeen && jumpTouch != NoTouch) { jumpTouch = NoTouch; jumpReleased = true; }
        if (!actionSeen) actionTouch = NoTouch;
        if (!pauseSeen) pauseTouch = NoTouch;
        worldPointers.RemoveWhere(id => !activePointers.Contains(id));
    }

    private void ProcessPointer(int id, Vector2 position, ref bool stickSeen, ref bool jumpSeen,
        ref bool actionSeen, ref bool pauseSeen)
    {
        activePointers.Add(id);
        // A gesture that began in the world remains a camera gesture until
        // release; crossing a button cannot steal it or trigger a jump.
        if (worldPointers.Contains(id)) return;
        if (stickTouch == id) { stickSeen = true; SetStick(position); return; }
        if (jumpTouch == id) { jumpSeen = true; return; }
        if (actionTouch == id) { actionSeen = true; return; }
        if (pauseTouch == id) { pauseSeen = true; return; }

        float r = Radius;
        if (stickTouch == NoTouch && Vector2.Distance(position, StickCentre) <= r * 1.3f)
        { stickTouch = id; stickSeen = true; SetStick(position); }
        else if (jumpTouch == NoTouch && Vector2.Distance(position, JumpCentre) <= r * JumpHitRadius)
        { jumpTouch = id; jumpSeen = true; jumpPressed = true; }
        else if (actionTouch == NoTouch && Vector2.Distance(position, ActionCentre) <= r)
        { actionTouch = id; actionSeen = true; actionPressed = true; }
        else if (pauseTouch == NoTouch && Vector2.Distance(position, PauseCentre) <= r * .65f)
        { pauseTouch = id; pauseSeen = true; FindAnyObjectByType<GameFlowUI>()?.TogglePause(); }
        else worldPointers.Add(id);
    }

    private void SetStick(Vector2 position)
    {
        Vector2 delta = Vector2.ClampMagnitude((position - StickCentre) / Radius, 1f);
        Move = delta.magnitude < .13f ? Vector2.zero : delta;
        stickPosition = StickCentre + delta * Radius * .64f;
    }

    private void ResetTouches()
    {
        if (jumpTouch != NoTouch) jumpReleased = true;
        jumpPressed = actionPressed = false;
        stickTouch = jumpTouch = actionTouch = pauseTouch = NoTouch;
        worldPointers.Clear();activePointers.Clear();
        Move = Vector2.zero;
    }

    private void OnApplicationFocus(bool focused) { if (!focused) ResetTouches(); }
    private void OnApplicationPause(bool paused) { if (paused) ResetTouches(); }
    private void OnDisable() => ResetTouches();

    private void OnGUI()
    {
        if (!GameFlowUI.GameplayInputEnabled || disc == null) return;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
        }
        float r = Radius;
        Draw(StickCentre, r, new Color(.20f, .18f, .15f, .42f), null);
        DrawPawKnob(stickTouch == NoTouch ? StickCentre : stickPosition, r * .49f);
        var cat = Object.FindFirstObjectByType<CatController>();
        float actionRadius=r*.82f*(actionTouch==NoTouch?1f:.96f);
        float jumpRadius=r*.82f*(jumpTouch==NoTouch?1f:.96f);
        string hideLabel=cat&&cat.IsHidden?"出":cat&&cat.CanHideNearby?"藏":null;
        Draw(ActionCentre, actionRadius, new Color(.98f, .52f, .26f, .97f), hideLabel);
        if(hideLabel==null)DrawIcon(ActionCentre,actionRadius,pushIcon);
        Draw(JumpCentre, jumpRadius, new Color(.46f, .63f, .37f, .97f), null);
        DrawIcon(JumpCentre,jumpRadius,jumpIcon);
        if (JumpHeld && cat && cat.JumpHoldFeedbackActive)
            DrawJumpRing(JumpCentre, r * RingRadius, cat.JumpHoldProgress);
        Draw(PauseCentre, r * .48f, new Color(.26f, .25f, .23f, .68f), "Ⅱ");
    }

    private void DrawPawKnob(Vector2 centre, float radius)
    {
        // Decoration only: the original stick radius, dead zone and pointer
        // ownership stay unchanged. Reuse the disc instead of font/emoji assets.
        Draw(centre, radius * 1.06f, new Color(.80f, .66f, .61f, .90f), null);
        Draw(centre, radius, new Color(.99f, .97f, .92f, .98f), null);
        Color paw = stickTouch == NoTouch
            ? new Color(.73f, .40f, .46f) : new Color(.68f, .31f, .39f);
        DrawPawPad(centre, radius, new Vector2(-.46f, .15f), new Vector2(.25f, .33f), paw);
        DrawPawPad(centre, radius, new Vector2(-.17f, .44f), new Vector2(.27f, .35f), paw);
        DrawPawPad(centre, radius, new Vector2( .17f, .44f), new Vector2(.27f, .35f), paw);
        DrawPawPad(centre, radius, new Vector2( .46f, .15f), new Vector2(.25f, .33f), paw);
        DrawPawPad(centre, radius, new Vector2(0f, -.10f), new Vector2(.40f, .46f), paw);
        DrawPawPad(centre, radius, new Vector2(-.14f, -.26f), new Vector2(.44f, .36f), paw);
        DrawPawPad(centre, radius, new Vector2( .14f, -.26f), new Vector2(.44f, .36f), paw);
    }

    private void DrawIcon(Vector2 centre,float radius,Texture2D icon)
    {
        var previous=GUI.color;GUI.color=Color.white;
        GUI.DrawTexture(new Rect(centre.x-radius,Screen.height-centre.y-radius,radius*2,radius*2),icon);
        GUI.color=previous;
    }

    private void DrawPawPad(Vector2 centre, float radius, Vector2 offset, Vector2 size, Color tint)
    {
        Vector2 position = centre + offset * radius;
        Vector2 dimensions = size * radius;
        Rect rect = new(position.x - dimensions.x * .5f,
            Screen.height - position.y - dimensions.y * .5f, dimensions.x, dimensions.y);
        Color old = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(rect, disc);
        GUI.color = old;
    }

    private void CreateJumpRing()
    {
        ringTrack = new Texture2D(RingSize, RingSize, TextureFormat.RGBA32, false)
            { name = "Jump hold ring track", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        ringFill = new Texture2D(RingSize, RingSize, TextureFormat.RGBA32, false)
            { name = "Jump hold ring fill", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        ringPixels = new Color32[RingSize * RingSize];
        ringCoverage = new byte[ringPixels.Length];
        ringAngles = new float[ringPixels.Length];
        float outer = RingSize * .48f, inner = RingSize * .435f;
        for (int y = 0; y < RingSize; y++)
            for (int x = 0; x < RingSize; x++)
            {
                int i = y * RingSize + x;
                float dx = x + .5f - RingSize * .5f, dy = y + .5f - RingSize * .5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner));
                ringCoverage[i] = alpha;
                // Texture coordinates are bottom-up: begin at twelve o'clock,
                // filling clockwise without rotating the button label.
                ringAngles[i] = Mathf.Repeat(Mathf.Atan2(dx, dy) / (2f * Mathf.PI), 1f);
                ringPixels[i] = new Color32(255, 255, 255, alpha);
            }
        ringTrack.SetPixels32(ringPixels);
        ringTrack.Apply(false, true);
        UpdateJumpRing(0);
    }

    private void UpdateJumpRing(int step)
    {
        if (step == lastRingStep) return;
        lastRingStep = step;
        float progress = step / (float)RingSteps;
        for (int i = 0; i < ringPixels.Length; i++)
            ringPixels[i].a = ringAngles[i] < progress ? ringCoverage[i] : (byte)0;
        ringFill.SetPixels32(ringPixels);
        ringFill.Apply(false, false);
    }

    private void DrawJumpRing(Vector2 centre, float radius, float progress)
    {
        if (ringTrack == null || ringFill == null) return;
        UpdateJumpRing(Mathf.FloorToInt(Mathf.Clamp01(progress) * RingSteps));
        Rect rect = new(centre.x - radius, Screen.height - centre.y - radius, radius * 2f, radius * 2f);
        Color old = GUI.color;
        GUI.color = new Color(.12f, .23f, .12f, .38f);
        GUI.DrawTexture(rect, ringTrack);
        GUI.color = progress >= 1f ? new Color(1f, .97f, .72f) : new Color(1f, .83f, .40f);
        GUI.DrawTexture(rect, ringFill);
        GUI.color = old;
    }

    private void Draw(Vector2 centre, float radius, Color tint, string text)
    {
        Rect rect = new(centre.x - radius, Screen.height - centre.y - radius, radius * 2f, radius * 2f);
        Color old = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(rect, disc);
        GUI.color = old;
        if (string.IsNullOrEmpty(text)) return;
        label.fontSize = Mathf.RoundToInt(radius * (text.Length > 1 ? .38f : .7f));
        GUI.Label(rect, text, label);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (disc != null) Destroy(disc);
        if (pushIcon != null) Destroy(pushIcon);
        if (jumpIcon != null) Destroy(jumpIcon);
        if (ringTrack != null) Destroy(ringTrack);
        if (ringFill != null) Destroy(ringFill);
    }
}
