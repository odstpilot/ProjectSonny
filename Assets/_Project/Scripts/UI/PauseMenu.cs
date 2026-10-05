using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The pause screen, in the painted PAUSED panel (Art/UI/Resources/Pause Screen 1) with the painted button frames (Pause
// Screen Buttons). Escape (or P, or a controller's Start) stops time and the game's sound, dims the level, and opens the
// panel out of a line: RESUME, SETTINGS, CONTROLS, EXIT TO MENU and QUIT down the left, and down the right where the
// technician is (chapter, room, objective, vitals) over a heartbeat, with what the station AI is up to. Up and down (or
// the mouse) pick, Enter or a click chooses; EXIT TO MENU and QUIT ask for a second press. SETTINGS and CONTROLS open the
// title screen's settings panel (TitleSettings) over it. Escape again, or RESUME, closes it.
// It won't open over another screen that has Escape of its own (the map, crafting, a terminal, dialogue) or a cutscene,
// and in a build it opens by itself when the game loses focus.
// Built from code, one per level, the first time it's asked for: PlayerHealthHandler does, so every level with a player
// in it can be paused. PlayerController stands still while IsPaused.
public class PauseMenu : MonoBehaviour
{
    const int SortingOrder = 130;           // over everything: the HUD (80), the map (108), Pip (109), the death screen (120)
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // The painted panel and button, cut out of their pictures, in pixels of the original images.
    const string PanelArt = "Pause Screen 1";
    static readonly Vector2Int PanelArtSize = new Vector2Int(1536, 1024);
    static readonly RectInt PanelCrop = new RectInt(81, 40, 1376, 913);
    const string ButtonArt = "Pause Screen Buttons";
    static readonly Vector2Int ButtonArtSize = new Vector2Int(2172, 724);
    static readonly RectInt ButtonCrop = new RectInt(79, 162, 2015, 391);

    const float PanelWidth = 1080f;
    static float PanelHeight => PanelWidth * PanelCrop.height / PanelCrop.width;
    static readonly Vector2 ButtonSize = new Vector2(380f, 380f * 391f / 2015f);
    const float ButtonsLeft = 70f, ButtonsTop = 146f, ButtonSpacing = 90f;
    const float StatusLeft = 500f, StatusWidth = 500f;

    static readonly Color Bright = new Color(0.86f, 1f, 1f);
    static readonly Color Cyan = new Color(0.2f, 0.95f, 1f);
    static readonly Color Dim = new Color(0.36f, 0.62f, 0.66f);
    static readonly Color Warn = new Color(1f, 0.36f, 0.3f);

    const string Resume = "RESUME", Settings = "SETTINGS", Controls = "CONTROLS", ExitToMenu = "EXIT TO MENU", Quit = "QUIT";
    static readonly string[] Items = { Resume, Settings, Controls, ExitToMenu, Quit };
    const int ControlsPage = 3;             // TitleSettings' CONTROLS tab
    const string TitleScene = "Title";

    // Each level's name on the status side, and what the station AI is doing in it.
    static readonly (string scene, string chapter, string sonny, bool hostile)[] Chapters =
    {
        ("Tutorial", "PROLOGUE - THE FLARE", "HOSTILE", true),
        ("Chapter1", "CHAPTER 1 - ARRIVAL", "ONLINE", false),
        ("Chapter2", "CHAPTER 2 - A FEW HOURS LATER", "NOT RESPONDING", true),
    };

    public static bool IsPaused { get; private set; }

    static PauseMenu instance;

    class Button
    {
        public RectTransform rect, content;
        public Image frame, glow;
        public RectTransform sheen;
        public TextMeshProUGUI label;
        public float lit;
    }

    private RectTransform screen, panel;
    private CanvasGroup screenGroup, contentGroup;
    private RawImage dim, scanlines;
    private Image panelGlow;
    private readonly Button[] buttons = new Button[Items.Length];
    private TextMeshProUGUI chapterValue, roomValue, objectiveValue, vitalsValue, sonnyValue, hints;
    private VitalsLine heartbeat;
    private TitleSettings settings;
    private readonly MenuInput input = new MenuInput();
    private AudioSource sfx;
    private AudioClip moveClip, selectClip, backClip, openClip;

    private int selected;
    private bool open, closing, confirming, sonnyHostile;
    private float openedAt = -10f, closedAt = -10f, selectedAt = -10f, confirmAt = -10f;
    private bool settingsWasOpen;
    private bool blockedLastFrame = true;
    private float timeScaleWas = 1f;
    private bool cursorWasVisible;
    private CursorLockMode cursorWas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsPaused = false;

    public static PauseMenu Get()
    {
        if (instance == null) instance = FindAnyObjectByType<PauseMenu>();
        if (instance == null) instance = new GameObject("Pause Menu", typeof(RectTransform)).AddComponent<PauseMenu>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        Build();
        screen.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        // Leaving the level mid-pause (a scene load) mustn't leave the next one frozen.
        if (open) Unfreeze();
    }

    // --- Opening and closing ---

    // Whether something else on screen owns Escape just now, or the game's in a moment that shouldn't be paused over.
    static bool Blocked()
    {
        if (Time.timeScale <= 0f) return true;      // the map, crafting, Pip's log, a tip, the death screen
        if (DialogueBox.Busy || TutorialHud.ScreenCovered || InstallView.Playing || ArrivalCutscene.Playing || ControlRoomCutscene.Playing) return true;
        if (SignalTuner.AnyOpen || DoctrineScreen.AnyOpen || GeneratorScreen.AnyOpen) return true;
        return MapScreen.IsOpen || CraftingScreen.IsOpen || PipLog.IsOpen || TipCard.IsOpen;
    }

    static bool PausePressed() =>
        Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);

    void Update()
    {
        float now = Time.unscaledTime;
        if (!open)
        {
            // Judged by how things stood last frame, so the Escape that just closed the map doesn't also pause.
            if (!closing && PausePressed() && !blockedLastFrame) Open(now);
        }
        else Tick(now);
        Animate(now, Time.unscaledDeltaTime);
    }

    void LateUpdate() => blockedLastFrame = !open && Blocked();

    void OnApplicationFocus(bool focused)
    {
        if (!focused && !open && !Application.isEditor && !Blocked()) Open(Time.unscaledTime);
    }

    public void Open(float now)
    {
        open = true;
        closing = false;
        IsPaused = true;
        openedAt = now;
        confirming = false;
        selected = 0;
        selectedAt = now;

        timeScaleWas = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        cursorWasVisible = Cursor.visible;
        cursorWas = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshStatus();
        screen.gameObject.SetActive(true);
        input.Read(now);
        sfx.PlayOneShot(openClip, 0.5f);
    }

    public void Close(float now)
    {
        if (!open) return;
        open = false;
        closing = true;
        closedAt = now;
        Unfreeze();
        sfx.PlayOneShot(backClip, 0.4f);
    }

    void Unfreeze()
    {
        IsPaused = false;
        Time.timeScale = timeScaleWas > 0f ? timeScaleWas : 1f;
        AudioListener.pause = false;
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorWas;
    }

    // --- While it's open ---

    void Tick(float now)
    {
        input.Read(now);
        if (settings.IsOpen)
        {
            settings.Tick(now, input);
            settingsWasOpen = true;
            return;
        }
        // Not the press that just closed the settings panel.
        if (settingsWasOpen)
        {
            settingsWasOpen = false;
            return;
        }
        if (now - openedAt < 0.12f) return;

        if (PausePressed() || input.Cancel)
        {
            Close(now);
            return;
        }

        if (input.StepY != 0) Select((selected + input.StepY + Items.Length) % Items.Length, now);
        if (input.MouseMoved)
            for (int i = 0; i < Items.Length; i++)
                if (i != selected && MenuInput.Over(buttons[i].rect)) Select(i, now);

        bool clicked = input.Click && MenuInput.Over(buttons[selected].rect);
        if (input.Submit || Input.GetKeyDown(KeyCode.Space) || clicked) Choose(now);
        RefreshStatus();
    }

    void Select(int index, float now)
    {
        if (index == selected) return;
        selected = index;
        selectedAt = now;
        confirming = false;
        sfx.PlayOneShot(moveClip, 0.3f);
    }

    void Choose(float now)
    {
        string item = Items[selected];
        bool risky = item == ExitToMenu || item == Quit;
        if (risky && !confirming)
        {
            confirming = true;
            confirmAt = now;
            sfx.PlayOneShot(backClip, 0.45f);
            return;
        }
        sfx.PlayOneShot(selectClip, 0.45f);
        switch (item)
        {
            case Resume:
                Close(now);
                break;
            case Settings:
                settings.Open(now);
                break;
            case Controls:
                settings.Open(now, ControlsPage);
                break;
            case ExitToMenu:
                Unfreeze();
                open = false;
                SceneManager.LoadScene(TitleScene);
                break;
            case Quit:
                Unfreeze();
                open = false;
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                break;
        }
    }

    // Where the technician is and how they're doing, for the right side.
    void RefreshStatus()
    {
        string scene = SceneManager.GetActiveScene().name;
        string chapter = scene.ToUpperInvariant(), sonny = "UNKNOWN";
        sonnyHostile = false;
        foreach (var entry in Chapters)
            if (entry.scene == scene)
            {
                chapter = entry.chapter;
                sonny = entry.sonny;
                sonnyHostile = entry.hostile;
            }
        chapterValue.text = chapter;
        sonnyValue.text = "STATION AI: " + sonny;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        string room = "UNKNOWN";
        if (player != null && StationMap.Current != null && StationMap.Current.Locate(player.transform.position, out _, out StationMap.Room inRoom))
            room = inRoom.name.ToUpperInvariant();
        roomValue.text = room;

        TutorialHud hud = FindAnyObjectByType<TutorialHud>();
        string objective = hud != null ? hud.Objective : "";
        objectiveValue.text = string.IsNullOrEmpty(objective) ? "--" : objective.ToUpperInvariant();

        Health health = player != null ? player.GetComponent<Health>() : null;
        string vitals = health != null ? $"HP {Mathf.CeilToInt(health.CurrentHealth)}/{Mathf.CeilToInt(health.maxHealth)}" : "HP --";
        if (player != null) vitals += $"    STAMINA {Mathf.RoundToInt(100f * player.stamina / Mathf.Max(1f, player.maxStamina))}%";
        vitalsValue.text = vitals;
        if (health != null) heartbeat.SetHealth(health.CurrentHealth, health.maxHealth);
    }

    // --- Animation ---

    void Animate(float now, float dt)
    {
        if (!open && !closing) return;
        if (settings != null) settings.Animate(now);

        float shown = open
            ? TitleUI.Phase(now, openedAt, openedAt + 0.22f)
            : 1f - TitleUI.Phase(now, closedAt, closedAt + 0.16f);
        if (closing && shown <= 0f)
        {
            closing = false;
            screen.gameObject.SetActive(false);
            return;
        }

        dim.color = new Color(0f, 0.02f, 0.03f, 0.68f * TitleUI.EaseOut(shown));
        scanlines.uvRect = new Rect(0f, now * 0.06f, 1f, Screen.height / 4f);
        scanlines.color = new Color(1f, 1f, 1f, 0.06f * shown);

        // The panel opens out of a line: wide first, then tall.
        float wide = TitleUI.EaseOut(TitleUI.Phase(now, openedAt, openedAt + 0.14f));
        float tall = TitleUI.EaseOut(TitleUI.Phase(now, openedAt + 0.08f, openedAt + 0.3f));
        if (!open) wide = tall = TitleUI.EaseOut(shown);
        panel.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, wide), Mathf.Lerp(0.015f, 1f, tall), 1f);
        screenGroup.alpha = settings.IsOpen ? 1f : Mathf.Clamp01(shown * 1.5f);
        float content = open ? TitleUI.Phase(now, openedAt + 0.22f, openedAt + 0.4f) : shown;
        contentGroup.alpha = settings.IsOpen ? 0f : content;
        panel.gameObject.SetActive(!settings.IsOpen);
        panelGlow.color = TitleUI.Fade(Cyan, 0.1f + 0.06f * Mathf.Sin(now * 2f));

        float follow = 1f - Mathf.Exp(-dt * 16f);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            bool isSelected = i == selected;
            button.lit = Mathf.Lerp(button.lit, isSelected ? 1f : 0f, follow);

            // Each slides in from the left, one after another, its name decoding as it comes.
            float arrive = open ? TitleUI.EaseOut(TitleUI.Phase(now, openedAt + 0.24f + i * 0.05f, openedAt + 0.5f + i * 0.05f)) : 1f;
            button.rect.anchoredPosition = new Vector2(ButtonsLeft - 40f * (1f - arrive), -(ButtonsTop + i * ButtonSpacing));
            string text = Items[i];
            bool asking = isSelected && confirming;
            if (asking) text = "ARE YOU SURE?";
            button.label.text = open && arrive < 1f ? TitleUI.Decode(text, arrive, now) : text;

            float blink = asking ? (Mathf.Repeat((now - confirmAt) * 3f, 1f) < 0.6f ? 1f : 0.55f) : 1f;
            Color labelColor = asking ? Warn : Color.Lerp(Dim, Bright, button.lit);
            button.label.color = TitleUI.Fade(labelColor, blink * Mathf.Lerp(0.8f, 1f, button.lit));
            button.content.anchoredPosition = new Vector2(10f * button.lit, 0f);
            button.frame.color = Color.Lerp(new Color(0.42f, 0.6f, 0.64f, 0.75f), asking ? new Color(1f, 0.7f, 0.66f) : Color.white, button.lit);
            float breathe = 0.5f + 0.5f * Mathf.Sin(now * 4f);
            button.glow.color = TitleUI.Fade(asking ? Warn : Cyan, button.lit * (0.22f + 0.12f * breathe));
            button.rect.localScale = Vector3.one * (1f + 0.03f * button.lit);

            // A light runs across a button as it's picked.
            float sweep = isSelected ? TitleUI.Phase(now, selectedAt, selectedAt + 0.45f) : 1f;
            button.sheen.gameObject.SetActive(sweep < 1f);
            button.sheen.anchorMin = button.sheen.anchorMax = new Vector2(Mathf.Lerp(-0.2f, 1.2f, TitleUI.EaseInOut(sweep)), 0.5f);
        }

        // The station AI's line flickers when it's not on your side.
        if (sonnyHostile)
        {
            bool glitch = TitleUI.Hash(Mathf.FloorToInt(now * 12f)) > 0.86f;
            sonnyValue.color = glitch ? Color.white : Color.Lerp(Warn, new Color(0.6f, 0.1f, 0.08f), 0.5f + 0.5f * Mathf.Sin(now * 3f));
            sonnyValue.rectTransform.anchoredPosition = new Vector2(StatusLeft + (glitch ? Random.Range(-4f, 4f) : 0f), sonnyValue.rectTransform.anchoredPosition.y);
        }
        else sonnyValue.color = Cyan;
    }

    // --- Building ---

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.ignoreListenerPause = true;
        moveClip = TitleUI.Tone("Pause Move", 990f, 0.06f, 0.35f);
        selectClip = TitleUI.Tone("Pause Select", 1320f, 0.09f, 0.45f);
        backClip = TitleUI.Tone("Pause Back", 620f, 0.08f, 0.4f);
        openClip = TitleUI.Tone("Pause Open", 440f, 0.12f, 0.4f);

        screen = TitleUI.Stretch(TitleUI.NewRect("Pause Screen", transform));
        screenGroup = screen.gameObject.AddComponent<CanvasGroup>();
        dim = TitleUI.StretchedPicture("Dim", screen, Texture2D.whiteTexture);
        scanlines = TitleUI.StretchedPicture("Scanlines", screen, GameUI.Scanlines);

        TMP_FontAsset font = GameUI.Readout;
        panel = TitleUI.Place(TitleUI.NewRect("Panel", screen), new Vector2(0f, 10f), new Vector2(PanelWidth, PanelHeight));
        Sprite panelArt = GameUI.ArtPiece(PanelArt, PanelArtSize, PanelCrop);
        Image panelImage = GameUI.Fill(GameUI.NewRect("Panel Art", panel)).gameObject.AddComponent<Image>();
        panelImage.sprite = panelArt;
        panelImage.raycastTarget = false;
        if (panelArt == null) panelImage.color = new Color(0f, 0.08f, 0.1f, 0.95f);
        panelGlow = GameUI.Fill(GameUI.NewRect("Panel Glow", panel)).gameObject.AddComponent<Image>();
        panelGlow.sprite = panelArt;
        panelGlow.material = TitleUI.Additive;
        panelGlow.raycastTarget = false;
        panelGlow.enabled = panelArt != null;

        RectTransform contents = GameUI.Fill(GameUI.NewRect("Contents", panel));
        contentGroup = contents.gameObject.AddComponent<CanvasGroup>();

        Sprite buttonArt = GameUI.ArtPiece(ButtonArt, ButtonArtSize, ButtonCrop);
        for (int i = 0; i < Items.Length; i++) buttons[i] = NewButton(Items[i], contents, buttonArt, font);

        BuildStatus(contents, font);

        hints = Text("Hints", contents, font, 24f, Dim, new Vector2(ButtonsLeft, -652f), new Vector2(900f, 30f));
        hints.text = "[W/S] SELECT     [ENTER] CONFIRM     [ESC] RESUME";
        hints.characterSpacing = 4f;

        settings = new TitleSettings(screen, font, sfx, moveClip, selectClip, backClip);
    }

    Button NewButton(string item, RectTransform parent, Sprite art, TMP_FontAsset font)
    {
        var button = new Button();
        button.rect = TopLeft(GameUI.NewRect(item, parent), Vector2.zero, ButtonSize);
        button.rect.pivot = new Vector2(0f, 1f);
        button.rect.gameObject.AddComponent<RectMask2D>();

        button.frame = GameUI.Fill(GameUI.NewRect("Frame", button.rect)).gameObject.AddComponent<Image>();
        button.frame.sprite = art;
        button.frame.raycastTarget = false;
        if (art == null) button.frame.color = new Color(0f, 0.2f, 0.24f, 0.9f);
        button.glow = GameUI.Fill(GameUI.NewRect("Glow", button.rect)).gameObject.AddComponent<Image>();
        button.glow.sprite = art;
        button.glow.material = TitleUI.Additive;
        button.glow.raycastTarget = false;
        button.glow.enabled = art != null;

        button.sheen = GameUI.NewRect("Sheen", button.rect);
        button.sheen.sizeDelta = new Vector2(90f, ButtonSize.y * 1.4f);
        button.sheen.localRotation = Quaternion.Euler(0f, 0f, -18f);
        Image sheen = button.sheen.gameObject.AddComponent<Image>();
        sheen.color = new Color(0.7f, 1f, 1f, 0.22f);
        sheen.material = TitleUI.Additive;
        sheen.raycastTarget = false;

        button.content = GameUI.Fill(GameUI.NewRect("Content", button.rect));
        button.label = GameUI.Label("Label", button.content, item, 38f, Dim, TextAlignmentOptions.Center, font);
        GameUI.Fill(button.label.rectTransform);
        button.label.characterSpacing = 8f;
        button.label.fontSharedMaterial = TitleUI.GlowMaterial(font, TitleUI.Fade(Cyan, 0.45f), 0.7f, 0.25f);
        return button;
    }

    void BuildStatus(RectTransform parent, TMP_FontAsset font)
    {
        TextMeshProUGUI heading = Text("Status", parent, font, 30f, Cyan, new Vector2(StatusLeft, -ButtonsTop + 2f), new Vector2(StatusWidth, 34f));
        heading.text = "SUIT TELEMETRY";
        heading.characterSpacing = 10f;
        Rule(parent, -ButtonsTop - 38f);

        float y = -ButtonsTop - 54f;
        chapterValue = Row(parent, font, "CHAPTER", ref y, 1);
        roomValue = Row(parent, font, "LOCATION", ref y, 1);
        objectiveValue = Row(parent, font, "OBJECTIVE", ref y, 2);
        vitalsValue = Row(parent, font, "VITALS", ref y, 1);

        // A heartbeat in a cut-cornered box, and the station AI under it.
        float boxTop = y - 8f, boxHeight = 74f;
        RectTransform box = TopLeft(GameUI.NewRect("Heartbeat Box", parent), new Vector2(StatusLeft, boxTop), new Vector2(StatusWidth, boxHeight));
        var outline = box.gameObject.AddComponent<HudShape>();
        outline.raycastTarget = false;
        outline.color = TitleUI.Fade(Cyan, 0.7f);
        outline.thickness = 2f;
        outline.feather = 3f;
        const float cut = 12f;
        outline.SetPoints(new[]
        {
            new Vector2(cut, 0f), new Vector2(StatusWidth, 0f), new Vector2(StatusWidth, -boxHeight + cut),
            new Vector2(StatusWidth - cut, -boxHeight), new Vector2(0f, -boxHeight), new Vector2(0f, -cut),
        });
        RectTransform line = GameUI.Fill(GameUI.NewRect("Line", box), 8f);
        line.gameObject.AddComponent<RectMask2D>();
        heartbeat = GameUI.Fill(GameUI.NewRect("Heartbeat", line)).gameObject.AddComponent<VitalsLine>();
        heartbeat.lineColor = Cyan;
        heartbeat.lineThickness = 2f;

        sonnyValue = Text("Sonny", parent, font, 28f, Cyan, new Vector2(StatusLeft, boxTop - boxHeight - 12f), new Vector2(StatusWidth, 32f));
        sonnyValue.characterSpacing = 6f;
    }

    // A dim name over a bright value, moving y down past them.
    TextMeshProUGUI Row(RectTransform parent, TMP_FontAsset font, string name, ref float y, int lines)
    {
        TextMeshProUGUI label = Text(name, parent, font, 22f, Dim, new Vector2(StatusLeft, y), new Vector2(StatusWidth, 24f));
        label.text = name;
        label.characterSpacing = 6f;
        float height = 30f * lines;
        TextMeshProUGUI value = Text(name + " Value", parent, font, 30f, Bright, new Vector2(StatusLeft, y - 22f), new Vector2(StatusWidth, height));
        value.textWrappingMode = lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        value.overflowMode = TextOverflowModes.Ellipsis;
        value.alignment = TextAlignmentOptions.TopLeft;
        y -= 22f + height + 10f;
        return value;
    }

    void Rule(RectTransform parent, float y)
    {
        RectTransform rule = TopLeft(GameUI.NewRect("Rule", parent), new Vector2(StatusLeft, y), new Vector2(StatusWidth, 2f));
        Image image = rule.gameObject.AddComponent<Image>();
        image.color = TitleUI.Fade(Cyan, 0.5f);
        image.raycastTarget = false;
    }

    static TextMeshProUGUI Text(string textName, RectTransform parent, TMP_FontAsset font, float size, Color color, Vector2 position, Vector2 area)
    {
        TextMeshProUGUI label = GameUI.Label(textName, parent, "", size, color, TextAlignmentOptions.MidlineLeft, font);
        TopLeft(label.rectTransform, position, area);
        return label;
    }

    static RectTransform TopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
