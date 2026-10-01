using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Everything Pip's said (SuitHelper.History), to read back. Once Pip's on, a little icon sits in the top right corner,
// under where Pip pops up: a speech bubble on Pip's green screen, with an amber dot when there's something new since it
// was last opened. Click it, or press L, and the log opens over the game, paused: Pip's lines oldest first, scrolled to
// the newest, with the mouse wheel, W/S, or the arrow keys to go back through them. L, Tab, Esc, or a click outside it
// closes it.
// Built from code the first time Pip asks for it (SuitHelper.Get). Runs on unscaled time, since it pauses the game.
public class PipLog : MonoBehaviour
{
    public const KeyCode LogKey = KeyCode.L;
    const int SortingOrder = 111;               // over Pip (109) and the map (108)
    const float Margin = 40f;
    const float PipCorner = 150f + 16f;         // Pip's portrait, and a gap, above the icon
    const float IconSize = 72f;
    const float PanelWidth = 1100f, PanelHeight = 780f;
    const float ScrollSpeed = 900f;
    const float FadeTime = 0.15f;
    static readonly Color PanelColor = new Color(0.03f, 0.08f, 0.075f, 0.97f);
    static readonly Color ScreenTop = new Color(0.06f, 0.24f, 0.2f);
    const string PipTag = "<color=#73FFC7><b>PIP</b></color>   ";

    static PipLog instance;

    // The pointer's over the icon, so a click there is for it, not for swinging or firing whatever's in hand.
    public static bool MouseOverIcon { get; private set; }
    public static bool IsOpen => instance != null && instance.open;

    private bool built, open;
    private int openedFrame;
    private float savedTimeScale = 1f;
    private bool heldPlayer;
    private PlayerController player;
    private int seen;                           // how many lines there were when it was last opened

    private CanvasGroup iconGroup;
    private RectTransform icon;
    private Image iconBack;
    private Image unread;
    private CanvasGroup panelGroup;
    private RectTransform panel;
    private RectTransform viewport;
    private RectTransform content;
    private TextMeshProUGUI lines;
    private TextMeshProUGUI empty;
    private float scroll;

    public static void Ensure()
    {
        if (instance != null) return;
        instance = new GameObject("PipLog", typeof(RectTransform)).AddComponent<PipLog>();
    }

    void Awake()
    {
        if (instance == null) instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (open) Release();
        MouseOverIcon = false;
    }

    void Update()
    {
        bool pipOn = SuitHelper.Exists && !TutorialHud.ScreenCovered;
        iconGroup.alpha = Mathf.MoveTowards(iconGroup.alpha, pipOn && !open ? 1f : 0f, Time.unscaledDeltaTime / FadeTime);
        unread.enabled = SuitHelper.History.Count > seen;
        MouseOverIcon = pipOn && !open && RectTransformUtility.RectangleContainsScreenPoint(icon, Input.mousePosition, null);
        iconBack.color = MouseOverIcon ? Color.Lerp(ScreenTop, SuitHelper.PipColor, 0.35f) : ScreenTop;

        if (!open)
        {
            if (pipOn && (Input.GetKeyDown(LogKey) || (MouseOverIcon && Input.GetMouseButtonDown(0)))) Open();
            return;
        }

        bool clickedOutside = Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null);
        if (Time.frameCount != openedFrame && (Input.GetKeyDown(LogKey) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape) || clickedOutside))
        {
            Close();
            return;
        }

        float move = -Input.mouseScrollDelta.y * 90f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move -= ScrollSpeed * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move += ScrollSpeed * Time.unscaledDeltaTime;
        if (move != 0f) ScrollTo(scroll - move);
    }

    // --- Opening and closing ---

    bool CanOpen()
    {
        if (Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || MapScreen.IsOpen || CraftingScreen.IsOpen) return false;
        GameObject found = GameObject.FindWithTag("Player");
        if (found == null) return false;
        player = found.GetComponent<PlayerController>();
        Health health = found.GetComponent<Health>();
        return health == null || !health.IsDead;
    }

    void Open()
    {
        if (open || !CanOpen()) return;
        open = true;
        openedFrame = Time.frameCount;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        heldPlayer = player != null && !player.IsScripted;
        if (heldPlayer) player.SetScriptedInput(Vector2.zero, false);
        seen = SuitHelper.History.Count;
        Fill();
        StopAllCoroutines();
        StartCoroutine(FadePanel(1f));
    }

    void Close()
    {
        if (!open) return;
        open = false;
        Release();
        StopAllCoroutines();
        StartCoroutine(FadePanel(0f));
    }

    void Release()
    {
        if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
        if (heldPlayer && player != null) player.ClearScriptedInput();
        heldPlayer = false;
    }

    System.Collections.IEnumerator FadePanel(float to)
    {
        float from = panelGroup.alpha;
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / FadeTime);
            panelGroup.alpha = Mathf.Lerp(from, to, p);
            panel.localScale = new Vector3(1f, Mathf.Lerp(to > 0f ? 0.04f : 1f, to > 0f ? 1f : 0.04f, p), 1f);
            yield return null;
        }
        panelGroup.alpha = to;
        panel.localScale = Vector3.one;
    }

    // The lines, oldest first, scrolled to the newest.
    void Fill()
    {
        var text = new StringBuilder();
        foreach (string line in SuitHelper.History)
        {
            if (text.Length > 0) text.Append("\n\n");
            text.Append(PipTag).Append(line);
        }
        lines.text = text.ToString();
        empty.enabled = SuitHelper.History.Count == 0;
        lines.ForceMeshUpdate();
        content.sizeDelta = new Vector2(0f, lines.preferredHeight + 20f);
        ScrollTo(float.MaxValue);
    }

    void ScrollTo(float y)
    {
        float most = Mathf.Max(0f, content.sizeDelta.y - viewport.rect.height);
        scroll = Mathf.Clamp(y, 0f, most);
        content.anchoredPosition = new Vector2(0f, scroll);
    }

    // --- Building ---

    void Build()
    {
        if (built) return;
        built = true;
        PromptBadge.MakeCanvas(gameObject, SortingOrder);
        var rootRect = (RectTransform)transform;
        GameUI.Fill(rootRect);

        // The icon: a speech bubble on Pip's screen, its key under it.
        icon = GameUI.NewRect("Icon", transform);
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(1f, 1f);
        icon.anchoredPosition = new Vector2(-Margin, -(Margin + PipCorner));
        icon.sizeDelta = new Vector2(IconSize, IconSize);
        iconGroup = icon.gameObject.AddComponent<CanvasGroup>();
        iconGroup.alpha = 0f;
        iconGroup.interactable = iconGroup.blocksRaycasts = false;
        iconBack = GameUI.Sliced("Screen", icon, GameUI.SoftPanelSprite, ScreenTop);
        iconBack.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        GameUI.Fill(iconBack.rectTransform);
        Image glyph = GameUI.NewRect("Bubble", icon).gameObject.AddComponent<Image>();
        glyph.sprite = BubbleGlyph;
        glyph.color = SuitHelper.PipColor;
        glyph.raycastTarget = false;
        GameUI.Anchor(glyph.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f);
        glyph.rectTransform.sizeDelta = new Vector2(44f, 38f);
        glyph.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        Image cap = GameUI.KeyCap(icon, LogKey.ToString(), 20f, out _);
        cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = cap.rectTransform.pivot = new Vector2(0.5f, 0f);
        cap.rectTransform.sizeDelta = new Vector2(30f, 30f);
        cap.rectTransform.anchoredPosition = new Vector2(0f, -22f);
        unread = GameUI.NewRect("New", icon).gameObject.AddComponent<Image>();
        unread.sprite = PixelArt.ToSprite(CombatSprites.SoftCircleTexture, CombatSprites.SoftCircleTexture.width, new Vector2(0.5f, 0.5f));
        unread.color = GameUI.Amber;
        unread.raycastTarget = false;
        unread.rectTransform.anchorMin = unread.rectTransform.anchorMax = new Vector2(1f, 1f);
        unread.rectTransform.sizeDelta = new Vector2(22f, 22f);
        unread.rectTransform.anchoredPosition = new Vector2(-4f, -4f);

        // The log itself.
        RectTransform screen = GameUI.Fill(GameUI.NewRect("Log", transform));
        panelGroup = screen.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.interactable = panelGroup.blocksRaycasts = false;
        Image backdrop = GameUI.NewRect("Backdrop", screen).gameObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.6f);
        backdrop.raycastTarget = false;
        GameUI.Fill(backdrop.rectTransform);

        Image frame = GameUI.Sliced("Panel", screen, GameUI.PanelSprite, PanelColor);
        panel = frame.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        var edge = frame.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.12f, 0.4f, 0.32f);
        edge.effectDistance = new Vector2(4f, -4f);

        TextMeshProUGUI title = GameUI.Label("Title", panel, "PIP  //  LOG", 34f, SuitHelper.PipColor, TextAlignmentOptions.TopLeft, GameUI.Heading);
        GameUI.Anchor(title.rectTransform, 0f, 1f, 1f, 1f, 48f, -70f, 48f, 30f);
        title.characterSpacing = 6f;
        TextMeshProUGUI close = GameUI.Label("Close", panel, $"{LogKey} / ESC  CLOSE", 24f, GameUI.Dim, TextAlignmentOptions.TopRight);
        GameUI.Anchor(close.rectTransform, 0f, 1f, 1f, 1f, 48f, -66f, 48f, 36f);
        TextMeshProUGUI hint = GameUI.Label("Hint", panel, "SCROLL / W S  TO READ BACK", 22f, GameUI.Dim, TextAlignmentOptions.BottomRight);
        GameUI.Anchor(hint.rectTransform, 0f, 0f, 1f, 0f, 48f, 24f, 48f, -56f);

        viewport = GameUI.Anchor(GameUI.NewRect("Viewport", panel), 0f, 0f, 1f, 1f, 48f, 76f, 48f, 100f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = GameUI.NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        lines = GameUI.Label("Lines", content, "", 28f, GameUI.Text, TextAlignmentOptions.TopLeft);
        lines.textWrappingMode = TextWrappingModes.Normal;
        lines.richText = true;
        lines.lineSpacing = 6f;
        GameUI.Fill(lines.rectTransform);
        empty = GameUI.Label("Empty", viewport, "Nothing yet.", 28f, GameUI.Dim, TextAlignmentOptions.Center);
        GameUI.Fill(empty.rectTransform);
    }

    // A pixel speech bubble with three lines of text in it.
    static Sprite bubbleGlyph;
    static Sprite BubbleGlyph => bubbleGlyph != null ? bubbleGlyph : (bubbleGlyph = PixelArt.FromText(new[]
    {
        ".############.",
        "#............#",
        "#.#########..#",
        "#............#",
        "#.#######....#",
        "#............#",
        "#.#####......#",
        "#............#",
        ".####.#######.",
        "....#.#.......",
        "....##........",
    }, new System.Collections.Generic.Dictionary<char, Color32> { { '#', new Color32(255, 255, 255, 255) } }, 16f, new Vector2(0.5f, 0.5f)));
}
