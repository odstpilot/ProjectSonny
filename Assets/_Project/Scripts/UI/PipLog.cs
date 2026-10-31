using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// read back what pip said
public class PipLog : MonoBehaviour
{
    public const KeyCode LogKey = KeyCode.L;
    const int SortingOrder = 111;
    const float Margin = 40f;
    const float PipCorner = 150f + 16f;
    const float IconSize = 72f;
    const float PanelWidth = 1100f, PanelHeight = 780f;
    const float CardGap = 14f;
    const float TypeSpeed = 70f;
    const float FadeTime = 0.15f;
    static readonly Color PanelColor = new Color(0.03f, 0.08f, 0.075f, 0.97f);
    static readonly Color ScreenTop = new Color(0.06f, 0.24f, 0.2f);
    static readonly Color CardColor = new Color(0.05f, 0.13f, 0.115f, 0.95f);
    static readonly Color CardPicked = new Color(0.1f, 0.27f, 0.22f, 1f);
    static readonly string[] Filters = { "ALL", "PIP", "TIPS" };

    class Entry
    {
        public int number;
        public bool tip, fresh;
        public string text;
    }

    class Card
    {
        public Entry entry;
        public RectTransform rect;
        public Image back, accent;
        public TextMeshProUGUI head, body, badge;
        public float y, height;
    }

    static PipLog instance;

    public static bool MouseOverIcon { get; private set; }
    public static bool IsOpen => instance != null && instance.open;

    private bool built, open;
    private int openedFrame;
    private float savedTimeScale = 1f;
    private bool heldPlayer;
    private PlayerController player;
    private int seen;

    private CanvasGroup iconGroup;
    private RectTransform icon;
    private Image iconBack;
    private Image unread;
    private CanvasGroup panelGroup;
    private RectTransform panel;
    private RectTransform viewport;
    private RectTransform content;
    private TextMeshProUGUI empty, stats;
    private float scroll, scrollTarget;
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Card> cards = new List<Card>();
    private readonly HashSet<int> typed = new HashSet<int>();
    private readonly Image[] tabs = new Image[3];
    private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[3];
    private int filter, picked = -1, shownLetters;
    private float typeStart;

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
        bool pipOn = (SuitHelper.Exists || SuitHelper.History.Count > 0) && !TutorialHud.ScreenCovered;
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

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.mouseScrollDelta.y > 0f) Pick(picked - 1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.mouseScrollDelta.y < 0f) Pick(picked + 1);
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) SetFilter(filter + Filters.Length - 1);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) SetFilter(filter + 1);
        if (Input.GetMouseButtonDown(0))
        {
            for (int i = 0; i < tabs.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(tabs[i].rectTransform, Input.mousePosition, null)) SetFilter(i);
            for (int i = 0; i < cards.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, null)
                    && RectTransformUtility.RectangleContainsScreenPoint(cards[i].rect, Input.mousePosition, null)) Pick(i);
        }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)) FinishTyping();
        TypeOut();
        scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        content.anchoredPosition = new Vector2(0f, scroll);
    }

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
        Fill();
        seen = SuitHelper.History.Count;
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

    void Fill()
    {
        int before = seen;
        entries.Clear();
        for (int i = 0; i < SuitHelper.History.Count; i++)
        {
            string line = SuitHelper.History[i];
            bool tip = line.Length > 0 && line[0] == SuitHelper.TipMarker;
            entries.Add(new Entry { number = i + 1, tip = tip, text = tip ? line.Substring(1) : line, fresh = i >= before });
        }
        int tips = entries.FindAll(e => e.tip).Count, fresh = entries.FindAll(e => e.fresh).Count;
        tabLabels[0].text = $"ALL  {entries.Count}";
        tabLabels[1].text = $"PIP  {entries.Count - tips}";
        tabLabels[2].text = $"TIPS  {tips}";
        stats.text = fresh > 0 ? $"<color=#FFAE52>{fresh} NEW</color>" : "ALL CAUGHT UP";
        Lay();
        int first = cards.FindIndex(c => c.entry.fresh);
        Pick(first >= 0 ? first : cards.Count - 1, true);
    }

    void SetFilter(int to)
    {
        to %= Filters.Length;
        if (to == filter) return;
        filter = to;
        Sfx.Play("Dialogue Tick", 0.8f, 0.8f);
        Lay();
        Pick(cards.Count - 1, true);
    }

    void Lay()
    {
        foreach (Card card in cards) Destroy(card.rect.gameObject);
        cards.Clear();
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].color = i == filter ? (i == 2 ? GameUI.Amber : SuitHelper.PipColor) : new Color(0.12f, 0.2f, 0.18f);
            tabLabels[i].color = i == filter ? GameUI.Ink : GameUI.Dim;
        }
        Canvas.ForceUpdateCanvases();
        float width = viewport.rect.width > 1f ? viewport.rect.width : PanelWidth - 96f;
        float y = 0f;
        foreach (Entry entry in entries)
        {
            if ((filter == 1 && entry.tip) || (filter == 2 && !entry.tip)) continue;
            Card card = MakeCard(entry);
            float textHeight = card.body.GetPreferredValues(entry.text, width - 64f, 0f).y;
            card.height = 52f + textHeight + 20f;
            card.y = y;
            card.rect.anchoredPosition = new Vector2(0f, -y);
            card.rect.sizeDelta = new Vector2(0f, card.height);
            y += card.height + CardGap;
            cards.Add(card);
        }
        content.sizeDelta = new Vector2(0f, Mathf.Max(0f, y - CardGap));
        empty.enabled = cards.Count == 0;
        picked = -1;
    }

    Card MakeCard(Entry entry)
    {
        Color tone = entry.tip ? GameUI.Amber : SuitHelper.PipColor;
        var card = new Card { entry = entry };
        card.back = GameUI.Sliced("Card", content, GameUI.SoftPanelSprite, CardColor);
        card.back.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        card.rect = card.back.rectTransform;
        card.rect.anchorMin = new Vector2(0f, 1f);
        card.rect.anchorMax = new Vector2(1f, 1f);
        card.rect.pivot = new Vector2(0.5f, 1f);
        card.accent = GameUI.NewRect("Accent", card.rect).gameObject.AddComponent<Image>();
        card.accent.raycastTarget = false;
        card.accent.color = tone;
        GameUI.Anchor(card.accent.rectTransform, 0f, 0f, 0f, 1f, 10f, 10f, 0f, 10f);
        card.accent.rectTransform.sizeDelta = new Vector2(8f, card.accent.rectTransform.sizeDelta.y);
        card.head = GameUI.Label("Head", card.rect, $"#{entry.number:00}   {(entry.tip ? "TIP" : "PIP")}", 26f, tone, TextAlignmentOptions.TopLeft, GameUI.Readout);
        GameUI.Anchor(card.head.rectTransform, 0f, 1f, 1f, 1f, 40f, -46f, 24f, 12f);
        card.badge = GameUI.Label("New", card.rect, entry.fresh ? "NEW" : "", 26f, GameUI.Amber, TextAlignmentOptions.TopRight, GameUI.Readout);
        GameUI.Anchor(card.badge.rectTransform, 0f, 1f, 1f, 1f, 40f, -46f, 24f, 12f);
        card.body = GameUI.Label("Text", card.rect, entry.text, 28f, GameUI.Text, TextAlignmentOptions.TopLeft);
        card.body.textWrappingMode = TextWrappingModes.Normal;
        card.body.lineSpacing = 6f;
        GameUI.Anchor(card.body.rectTransform, 0f, 0f, 1f, 1f, 40f, 20f, 24f, 52f);
        Style(card, false);
        return card;
    }

    static void Style(Card card, bool on)
    {
        card.back.color = on ? CardPicked : CardColor;
        Color tone = card.entry.tip ? GameUI.Amber : SuitHelper.PipColor;
        card.accent.color = on ? tone : TitleUI.Fade(tone, 0.35f);
        card.body.color = on ? GameUI.Text : TitleUI.Fade(GameUI.Text, 0.55f);
        card.head.color = on ? tone : TitleUI.Fade(tone, 0.6f);
        card.rect.localScale = on ? new Vector3(1.01f, 1.01f, 1f) : Vector3.one;
    }

    void Pick(int index, bool snap = false)
    {
        if (cards.Count == 0) return;
        index = Mathf.Clamp(index, 0, cards.Count - 1);
        if (index == picked) return;
        FinishTyping();
        if (picked >= 0 && picked < cards.Count) Style(cards[picked], false);
        picked = index;
        Card card = cards[picked];
        Style(card, true);
        if (!snap) Sfx.Play("Dialogue Tick", 0.7f, 1.1f);
        if (card.entry.fresh && typed.Add(card.entry.number))
        {
            typeStart = Time.unscaledTime;
            shownLetters = 0;
            card.body.maxVisibleCharacters = 0;
        }
        card.entry.fresh = false;
        card.badge.text = "";

        float view = viewport.rect.height;
        float most = Mathf.Max(0f, content.sizeDelta.y - view);
        if (card.y < scrollTarget) scrollTarget = card.y;
        else if (card.y + card.height > scrollTarget + view) scrollTarget = card.y + card.height - view;
        scrollTarget = Mathf.Clamp(scrollTarget, 0f, most);
        if (snap) scroll = scrollTarget;
    }

    void TypeOut()
    {
        if (picked < 0 || picked >= cards.Count) return;
        TextMeshProUGUI body = cards[picked].body;
        if (body.maxVisibleCharacters >= body.text.Length) return;
        int letters = Mathf.FloorToInt((Time.unscaledTime - typeStart) * TypeSpeed);
        if (letters > shownLetters && letters % 3 == 0) Sfx.Play("Dialogue Blip", 0.5f, 1.25f);
        shownLetters = letters;
        body.maxVisibleCharacters = letters;
    }

    void FinishTyping()
    {
        if (picked >= 0 && picked < cards.Count) cards[picked].body.maxVisibleCharacters = 99999;
    }

    void Build()
    {
        if (built) return;
        built = true;
        PromptBadge.MakeCanvas(gameObject, SortingOrder);
        var rootRect = (RectTransform)transform;
        GameUI.Fill(rootRect);

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
        TextMeshProUGUI close = GameUI.Label("Close", panel, $"{LogKey} / ESC  CLOSE", 30f, GameUI.Text, TextAlignmentOptions.TopRight);
        GameUI.Anchor(close.rectTransform, 0f, 1f, 1f, 1f, 48f, -66f, 48f, 36f);
        TextMeshProUGUI hint = GameUI.Label("Hint", panel, "W S  PICK      A D  FILTER      SPACE  SKIP", 28f, GameUI.Dim, TextAlignmentOptions.BottomRight);
        GameUI.Anchor(hint.rectTransform, 0f, 0f, 1f, 0f, 48f, 24f, 48f, -56f);
        stats = GameUI.Label("Stats", panel, "", 30f, GameUI.Dim, TextAlignmentOptions.BottomLeft, GameUI.Readout);
        GameUI.Anchor(stats.rectTransform, 0f, 0f, 1f, 0f, 48f, 24f, 48f, -56f);
        for (int i = 0; i < Filters.Length; i++)
        {
            tabs[i] = GameUI.Sliced("Tab", panel, GameUI.SoftKeyCapSprite, Color.white);
            tabs[i].pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
            RectTransform tab = tabs[i].rectTransform;
            tab.anchorMin = tab.anchorMax = tab.pivot = new Vector2(0f, 1f);
            tab.anchoredPosition = new Vector2(48f + i * 190f, -100f);
            tab.sizeDelta = new Vector2(176f, 46f);
            tabLabels[i] = GameUI.Label("Label", tab, Filters[i], 26f, GameUI.Dim, TextAlignmentOptions.Center, GameUI.Heading);
            GameUI.Fill(tabLabels[i].rectTransform);
        }

        viewport = GameUI.Anchor(GameUI.NewRect("Viewport", panel), 0f, 0f, 1f, 1f, 48f, 76f, 48f, 166f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = GameUI.NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        empty = GameUI.Label("Empty", viewport, "Nothing yet.", 28f, GameUI.Dim, TextAlignmentOptions.Center);
        GameUI.Fill(empty.rectTransform);
    }

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
