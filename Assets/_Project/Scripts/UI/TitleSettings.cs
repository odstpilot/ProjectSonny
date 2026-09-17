using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The settings panel: a HUD window over the dimmed title screen, in four tabs (AUDIO, DISPLAY, GAMEPLAY, CONTROLS).
// Up and down pick a setting, left and right change it, Q and E (or the shoulder buttons) change tab, and the mouse can
// hover, click, and drag everything. A line under the list says what the selected setting does, and key hints and the
// RESET DEFAULTS and BACK buttons run along the bottom. Changes apply straight away; BACK or cancel closes the panel and
// saves them. RESET DEFAULTS asks for a second press. The panel opens out of a line and closes back into one.
public class TitleSettings
{
    enum Kind { Slider, Choice, Toggle }

    // A setting, and how to read and change it.
    class Setting
    {
        public Kind kind;
        public string name, help, unavailableHelp;
        public System.Func<bool> available;      // null means always
        public System.Func<float> value;         // a slider's, 0 to 1
        public System.Action<float> setValue;
        public System.Func<string[]> options;    // a choice's
        public System.Func<int> index;
        public System.Action<int> setIndex;
        public System.Func<bool> on;             // a toggle's
        public System.Action<bool> setOn;

        public bool Available => available == null || available();
    }

    // A setting's row on its page.
    class Row
    {
        public Setting setting;
        public RectTransform rect;
        public CanvasGroup group;
        public TextMeshProUGUI label, value;
        public RectTransform track;
        public HudShape[] segments;
        public HudShape leftArrow, rightArrow;
        public RectTransform leftHit, rightHit;
        public HudShape[] pips;
        public HudShape offFill, onFill;
        public TextMeshProUGUI offText, onText;
        public RectTransform offHit, onHit;
        public float changedAt = -10f;
        public int changedStep;
    }

    class Page
    {
        public string name;
        public RectTransform rect, tab;
        public CanvasGroup group;
        public HudShape tabFill, tabEdge;
        public TextMeshProUGUI tabLabel;
        public readonly List<Row> rows = new List<Row>();
    }

    class Button
    {
        public string text;
        public RectTransform hit;
        public HudShape fill, edge;
        public TextMeshProUGUI label;
        public float pressedAt = -10f;
    }

    const float HalfWidth = 700f, HalfHeight = 430f, Chamfer = 60f;
    const float TabY = 258f, TabWidth = 250f, TabHeight = 56f, TabSpacing = 270f;
    const float FirstRowY = 150f, RowSpacing = 74f, RowHalfWidth = 630f, ControlX = 330f;
    const float HelpY = -292f, FooterY = -372f;
    const int SliderSegments = 20, MaxPips = 8;
    const float ConfirmSeconds = 3f;

    static readonly string[] DisplayModes = { "FULLSCREEN", "BORDERLESS", "WINDOWED" };
    static readonly FullScreenMode[] DisplayModeValues = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

    // What the controls tab lists: an action and the keys for it.
    static readonly string[] BindingActions = { "MOVE", "SPRINT", "CROUCH", "ATTACK", "INTERACT", "FLASHLIGHT", "SWITCH WEAPON", "CONFIRM / BACK" };
    static readonly string[][] BindingKeys =
    {
        new[] { "W", "A", "S", "D" }, new[] { "SHIFT" }, new[] { "C", "CTRL" }, new[] { "MOUSE 1" },
        new[] { "E" }, new[] { "F" }, new[] { "1-9", "WHEEL" }, new[] { "ENTER", "ESC" }
    };

    static readonly Color Cyan = new Color(0.25f, 0.95f, 0.95f);
    static readonly Color Teal = new Color(0.22f, 0.62f, 0.6f);
    static readonly Color Bright = new Color(0.88f, 1f, 0.98f);
    static readonly Color Dim = new Color(0.45f, 0.68f, 0.68f);
    static readonly Color Glass = new Color(0.015f, 0.05f, 0.06f, 0.95f);
    static readonly Color CapFill = new Color(0.03f, 0.1f, 0.11f, 0.9f);
    static readonly Color TabIdle = new Color(0.03f, 0.1f, 0.11f, 0.9f);
    static readonly Color TabHover = new Color(0.06f, 0.2f, 0.21f, 0.95f);
    static readonly Color TabActive = new Color(0.12f, 0.5f, 0.48f, 0.95f);
    static readonly Color SegmentLit = new Color(0.16f, 0.6f, 0.56f);
    static readonly Color SegmentBright = new Color(0.55f, 1f, 0.94f);
    static readonly Color SegmentUnlit = new Color(0.4f, 0.45f, 0.46f, 0.5f);
    static readonly Color Warning = new Color(1f, 0.55f, 0.35f);

    readonly RectTransform root, panel, tabs, tabUnderline, highlight;
    readonly CanvasGroup group, highlightGroup;
    readonly RawImage stripes;
    readonly TextMeshProUGUI header, subtitle, helpLabel;
    readonly HudShape chevron;
    readonly List<Page> pages = new List<Page>();
    readonly Button resetButton, backButton;
    readonly AudioSource sfx;
    readonly AudioClip moveClip, selectClip, backClip;

    int pageIndex, selected, pageDirection;
    bool dragging;
    float openedAt = -10f, closedAt = -10f, pageChangedAt = -10f, confirmUntil, lastAnimated;
    float highlightY = FirstRowY, underlineX;
    string lastHelp = "";
    float helpChangedAt;

    List<Vector2Int> resolutions = new List<Vector2Int>();
    string[] resolutionNames = { "" };
    int resolutionIndex, displayModeIndex;

    public bool IsOpen { get; private set; }

    public TitleSettings(RectTransform screen, TMP_FontAsset font, AudioSource sfx, AudioClip moveClip, AudioClip selectClip, AudioClip backClip)
    {
        this.sfx = sfx;
        this.moveClip = moveClip;
        this.selectClip = selectClip;
        this.backClip = backClip;

        root = TitleUI.Stretch(TitleUI.NewRect("Settings", screen));
        group = root.gameObject.AddComponent<CanvasGroup>();
        TitleUI.StretchedPicture("Dim", root, Texture2D.whiteTexture).color = new Color(0f, 0f, 0f, 0.72f);

        panel = TitleUI.Place(TitleUI.NewRect("Panel", root), Vector2.zero, new Vector2(HalfWidth * 2f, HalfHeight * 2f));
        Vector2[] outline =
        {
            new Vector2(-HalfWidth + Chamfer, HalfHeight), new Vector2(HalfWidth, HalfHeight), new Vector2(HalfWidth, -HalfHeight + Chamfer),
            new Vector2(HalfWidth - Chamfer, -HalfHeight), new Vector2(-HalfWidth, -HalfHeight), new Vector2(-HalfWidth, HalfHeight - Chamfer)
        };
        HudShape glass = TitleUI.Shape("Glass", panel, Glass, outline, filled: true);
        glass.gameObject.AddComponent<Mask>();
        RawImage grid = TitleUI.Picture("Grid", glass.rectTransform, GridTexture(), Vector2.zero, new Vector2(HalfWidth * 2f, HalfHeight * 2f));
        grid.color = new Color(0.3f, 0.9f, 0.9f, 0.05f);
        grid.uvRect = new Rect(0f, 0f, HalfWidth * 2f / 40f, HalfHeight * 2f / 40f);
        TitleUI.Shape("Glow", panel, TitleUI.Fade(Cyan, 0.22f), outline, thickness: 4f, feather: 20f);
        TitleUI.Shape("Edge", panel, Cyan, outline, thickness: 3f, feather: 2f);

        header = TitleUI.Label("Header", panel, font, 66f, Bright, new Vector2(-330f, 370f), new Vector2(600f, 90f), TextAlignmentOptions.Left);
        header.characterSpacing = 16f;
        header.fontSharedMaterial = TitleUI.GlowMaterial(font, TitleUI.Fade(Cyan, 0.55f), 0.85f, 0.4f);
        subtitle = TitleUI.Label("Subtitle", panel, font, 26f, Dim, new Vector2(-330f, 322f), new Vector2(600f, 36f), TextAlignmentOptions.Left);
        subtitle.characterSpacing = 4f;
        stripes = TitleUI.Picture("Stripes", panel, StripeTexture(), new Vector2(440f, 372f), new Vector2(380f, 22f));
        stripes.color = TitleUI.Fade(Teal, 0.55f);

        tabs = TitleUI.Place(TitleUI.NewRect("Tabs", panel), Vector2.zero, new Vector2(HalfWidth * 2f, HalfHeight * 2f));
        KeyCap(panel, font, "Q", -600f, TabY, true);
        KeyCap(panel, font, "E", 600f, TabY, false);
        TitleUI.Shape("Tab Line", panel, TitleUI.Fade(Cyan, 0.3f), new[] { new Vector2(-650f, TabY - 44f), new Vector2(650f, TabY - 44f) }, thickness: 2f, feather: 2f, closed: false);
        tabUnderline = TitleUI.Shape("Tab Underline", panel, Cyan, new[] { new Vector2(-100f, 0f), new Vector2(100f, 0f) }, thickness: 4f, feather: 8f, closed: false).rectTransform;

        highlight = TitleUI.Place(TitleUI.NewRect("Highlight", panel), new Vector2(0f, FirstRowY), new Vector2(RowHalfWidth * 2f, 60f));
        highlightGroup = highlight.gameObject.AddComponent<CanvasGroup>();
        TitleUI.Shape("Strip", highlight, TitleUI.Fade(Cyan, 0.1f), TitleUI.Box(-RowHalfWidth, RowHalfWidth, 29f, -29f), filled: true);
        TitleUI.Shape("Accent", highlight, Cyan, TitleUI.Box(-RowHalfWidth, -RowHalfWidth + 8f, 29f, -29f), filled: true);
        chevron = TitleUI.Shape("Chevron", highlight, Cyan,
            new[] { new Vector2(-RowHalfWidth - 32f, 12f), new Vector2(-RowHalfWidth - 18f, 0f), new Vector2(-RowHalfWidth - 32f, -12f) },
            thickness: 3f, feather: 3f, closed: false);

        Page audio = AddPage(font, "AUDIO");
        AddSlider(audio, font, "MASTER VOLUME", "HOW LOUD EVERYTHING IS.", () => GameSettings.MasterVolume, GameSettings.SetMasterVolume);
        AddSlider(audio, font, "MUSIC", "MUSIC AND BACKGROUND AMBIENCE.", () => GameSettings.MusicVolume, GameSettings.SetMusicVolume);
        AddSlider(audio, font, "EFFECTS", "SOUND EFFECTS AND MENU SOUNDS.", () => GameSettings.EffectsVolume, GameSettings.SetEffectsVolume);
        AddToggle(audio, font, "MUTE IN BACKGROUND", "SILENCE THE GAME WHILE ITS WINDOW ISN'T IN FOCUS.", () => GameSettings.MuteInBackground, GameSettings.SetMuteInBackground);

        Page display = AddPage(font, "DISPLAY");
        AddChoice(display, font, "DISPLAY MODE", "FULLSCREEN, A BORDERLESS WINDOW THAT FILLS THE SCREEN, OR A WINDOW.",
            () => DisplayModes, () => displayModeIndex, i =>
            {
                displayModeIndex = i;
                GameSettings.SetDisplayMode(DisplayModeValues[i]);
            });
        AddChoice(display, font, "RESOLUTION", "HOW MANY PIXELS THE GAME DRAWS.", () => resolutionNames, () => resolutionIndex, i =>
        {
            resolutionIndex = i;
            GameSettings.SetResolution(resolutions[i]);
        });
        AddToggle(display, font, "V-SYNC", "MATCH THE MONITOR'S REFRESH RATE, SO THE PICTURE DOESN'T TEAR.", () => GameSettings.VSync, GameSettings.SetVSync);
        Setting limit = AddChoice(display, font, "FRAME RATE LIMIT", "THE MOST FRAMES DRAWN EACH SECOND.",
            () => GameSettings.FrameRateNames, () => GameSettings.FrameRateIndex, GameSettings.SetFrameRateIndex);
        limit.available = () => !GameSettings.VSync;
        limit.unavailableHelp = "TURN V-SYNC OFF TO SET YOUR OWN LIMIT.";

        Page gameplay = AddPage(font, "GAMEPLAY");
        AddSlider(gameplay, font, "SCREEN SHAKE", "HOW HARD THE SCREEN SHAKES FROM HITS AND EXPLOSIONS.", () => GameSettings.ScreenShake, GameSettings.SetScreenShake);
        AddToggle(gameplay, font, "HIT STOP", "A SPLIT-SECOND FREEZE WHEN A HIT LANDS, SO IT FEELS HEAVY.", () => GameSettings.FreezeOnHit, GameSettings.SetFreezeOnHit);
        AddToggle(gameplay, font, "SCREEN EFFECTS", "SCANLINES AND FILM GRAIN ON SCREENS AND MENUS.", () => GameSettings.ScreenEffects, GameSettings.SetScreenEffects);
        AddToggle(gameplay, font, "SHOW FPS", "A FRAME RATE COUNTER IN THE TOP LEFT CORNER.", () => GameSettings.ShowFps, GameSettings.SetShowFps);

        BuildControls(AddPage(font, "CONTROLS"), font);

        TitleUI.Shape("Help Line", panel, TitleUI.Fade(Cyan, 0.3f), new[] { new Vector2(-650f, HelpY + 36f), new Vector2(650f, HelpY + 36f) }, thickness: 2f, feather: 2f, closed: false);
        TitleUI.Shape("Help Marker", panel, Cyan, new[] { new Vector2(-650f, HelpY + 9f), new Vector2(-636f, HelpY), new Vector2(-650f, HelpY - 9f) }, filled: true);
        helpLabel = TitleUI.Label("Help", panel, font, 30f, Dim, new Vector2(-10f, HelpY), new Vector2(1240f, 44f), TextAlignmentOptions.Left);
        helpLabel.characterSpacing = 3f;

        BuildFooterHints(font);
        resetButton = MakeButton(font, "RESET DEFAULTS", 330f, 300f);
        backButton = MakeButton(font, "BACK", 565f, 170f);

        root.gameObject.SetActive(false);
    }

    public void Open(float now)
    {
        IsOpen = true;
        openedAt = now;

        resolutions = GameSettings.Resolutions();
        resolutionNames = new string[resolutions.Count];
        for (int i = 0; i < resolutions.Count; i++) resolutionNames[i] = $"{resolutions[i].x} x {resolutions[i].y}";
        resolutionIndex = resolutions.FindIndex(size => size.x == Screen.width && size.y == Screen.height);
        if (resolutionIndex < 0) resolutionIndex = resolutions.Count - 1;
        displayModeIndex = System.Array.IndexOf(DisplayModeValues, Screen.fullScreenMode);
        if (displayModeIndex < 0) displayModeIndex = DisplayModes.Length - 1;

        ShowPage(0, now, 0);
        highlightY = FirstRowY;
        underlineX = pages[0].tab.anchoredPosition.x;
        root.gameObject.SetActive(true);
    }

    void Close(float now)
    {
        IsOpen = false;
        closedAt = now;
        dragging = false;
        GameSettings.Save();
        sfx.PlayOneShot(backClip, 0.4f);
    }

    public void Tick(float now, MenuInput input)
    {
        if (!IsOpen || now - openedAt < 0.2f) return;   // let the press that opened it pass

        int tabStep = TabStep();
        if (tabStep != 0) SwitchPage((pageIndex + tabStep + pages.Count) % pages.Count, tabStep, now);
        if (input.Click && !dragging)
        {
            for (int i = 0; i < pages.Count; i++)
                if (i != pageIndex && MenuInput.Over(pages[i].tab)) SwitchPage(i, i > pageIndex ? 1 : -1, now);
        }

        Page page = pages[pageIndex];
        int rowCount = page.rows.Count;
        int resetIndex = rowCount, backIndex = rowCount + 1;

        if (input.StepY != 0) Select(Vertical(selected, input.StepY, rowCount));
        if (input.MouseMoved && !dragging)
        {
            for (int i = 0; i < rowCount; i++)
                if (i != selected && MenuInput.Over(page.rows[i].rect)) Select(i);
            if (selected != resetIndex && MenuInput.Over(resetButton.hit)) Select(resetIndex);
            if (selected != backIndex && MenuInput.Over(backButton.hit)) Select(backIndex);
        }

        if (selected < rowCount)
        {
            UseRow(page.rows[selected], input, now);
        }
        else
        {
            if (input.StepX != 0) Select(selected == resetIndex ? backIndex : resetIndex);
            if (input.Submit)
            {
                if (selected == resetIndex) PressReset(now);
                else
                {
                    Close(now);
                    return;
                }
            }
        }

        if (input.Click && MenuInput.Over(resetButton.hit)) PressReset(now);
        if ((input.Click && MenuInput.Over(backButton.hit)) || input.Cancel) Close(now);
    }

    // Down past the last row reaches RESET DEFAULTS; up past the first reaches BACK.
    static int Vertical(int from, int step, int rowCount)
    {
        bool onFooter = from >= rowCount;
        if (step > 0) return onFooter ? (rowCount > 0 ? 0 : from) : from + 1;
        return onFooter ? (rowCount > 0 ? rowCount - 1 : from) : from > 0 ? from - 1 : rowCount + 1;
    }

    void UseRow(Row row, MenuInput input, float now)
    {
        Setting setting = row.setting;
        if (!setting.Available)
        {
            if (input.StepX != 0 || input.Submit) sfx.PlayOneShot(backClip, 0.2f);
            return;
        }

        switch (setting.kind)
        {
            case Kind.Slider:
                if (input.StepX != 0) SetSlider(row, setting.value() + input.StepX / (float)SliderSegments, input.StepX, now);
                if (input.Click && MenuInput.Over(row.track)) dragging = true;
                if (dragging && !input.MouseHeld) dragging = false;
                if (dragging) SetSlider(row, MenuInput.Across(row.track), 0, now);
                break;
            case Kind.Choice:
                if (input.StepX != 0) StepChoice(row, input.StepX, now);
                else if (input.Submit) StepChoice(row, 1, now);
                else if (input.Click && MenuInput.Over(row.leftHit)) StepChoice(row, -1, now);
                else if (input.Click && MenuInput.Over(row.rightHit)) StepChoice(row, 1, now);
                break;
            case Kind.Toggle:
                if (input.StepX != 0) SetToggle(row, input.StepX > 0, now);
                else if (input.Submit) SetToggle(row, !setting.on(), now);
                else if (input.Click && MenuInput.Over(row.offHit)) SetToggle(row, false, now);
                else if (input.Click && MenuInput.Over(row.onHit)) SetToggle(row, true, now);
                break;
        }
    }

    void SetSlider(Row row, float value, int step, float now)
    {
        float snapped = Mathf.Clamp01(Mathf.Round(value * SliderSegments) / SliderSegments);
        if (Mathf.Approximately(snapped, row.setting.value())) return;
        row.setting.setValue(snapped);
        Changed(row, step, now, moveClip);
    }

    void StepChoice(Row row, int step, float now)
    {
        int count = row.setting.options().Length;
        if (count < 2) return;
        row.setting.setIndex((row.setting.index() + step + count) % count);
        Changed(row, step, now, selectClip);
    }

    void SetToggle(Row row, bool on, float now)
    {
        if (row.setting.on() == on) return;
        row.setting.setOn(on);
        Changed(row, on ? 1 : -1, now, selectClip);
    }

    void Changed(Row row, int step, float now, AudioClip clip)
    {
        row.changedAt = now;
        row.changedStep = step;
        sfx.PlayOneShot(clip, 0.3f);
    }

    void PressReset(float now)
    {
        if (now >= confirmUntil)
        {
            confirmUntil = now + ConfirmSeconds;
            sfx.PlayOneShot(moveClip, 0.35f);
            return;
        }
        confirmUntil = 0f;
        GameSettings.ResetToDefaults();
        resetButton.pressedAt = now;
        foreach (Page page in pages)
            foreach (Row row in page.rows)
                row.changedAt = now;
        sfx.PlayOneShot(selectClip, 0.45f);
    }

    void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        dragging = false;
        sfx.PlayOneShot(moveClip, 0.3f);
    }

    void SwitchPage(int index, int direction, float now)
    {
        sfx.PlayOneShot(selectClip, 0.35f);
        ShowPage(index, now, direction);
    }

    void ShowPage(int index, float now, int direction)
    {
        pageIndex = index;
        pageDirection = direction;
        pageChangedAt = now;
        selected = 0;
        highlightY = FirstRowY;
        dragging = false;
        confirmUntil = 0f;
        for (int i = 0; i < pages.Count; i++)
            pages[i].rect.gameObject.SetActive(i == index);
    }

    static int TabStep()
    {
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.JoystickButton4)) return -1;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.JoystickButton5)) return 1;
        return 0;
    }

    // --- Animation ---

    public void Animate(float now)
    {
        float dt = Mathf.Clamp(now - lastAnimated, 0f, 0.05f);
        lastAnimated = now;
        if (!root.gameObject.activeSelf) return;

        float opening = TitleUI.Phase(now, openedAt, openedAt + 0.3f);
        float closing = IsOpen ? 0f : TitleUI.Phase(now, closedAt, closedAt + 0.2f);
        if (!IsOpen && closing >= 1f)
        {
            root.gameObject.SetActive(false);
            return;
        }
        group.alpha = TitleUI.Phase(now, openedAt, openedAt + 0.15f) * (1f - closing);
        float width = TitleUI.EaseOut(TitleUI.Phase(opening, 0f, 0.4f)) * (1f - TitleUI.EaseIn(TitleUI.Phase(closing, 0.5f, 1f)));
        float height = TitleUI.EaseOut(TitleUI.Phase(opening, 0.25f, 1f)) * (1f - TitleUI.EaseIn(TitleUI.Phase(closing, 0f, 0.6f)));
        panel.localScale = new Vector3(Mathf.Lerp(0.04f, 1f, width), Mathf.Lerp(0.01f, 1f, height), 1f);

        header.text = TitleUI.Decode("SETTINGS", TitleUI.Phase(now, openedAt + 0.2f, openedAt + 0.5f), now);
        subtitle.text = TitleUI.Decode("SONNY OS  //  SYSTEM CONFIGURATION", TitleUI.Phase(now, openedAt + 0.3f, openedAt + 0.8f), now);
        stripes.uvRect = new Rect(-now * 0.35f, 0f, 380f / 22f, 1f);
        float follow = 1f - Mathf.Exp(-dt * 18f);

        AnimateTabs(now, follow);

        Page page = pages[pageIndex];
        float enter = TitleUI.EaseOut(TitleUI.Phase(now, pageChangedAt, pageChangedAt + 0.25f));
        float slide = pageDirection * 60f * (1f - enter);
        page.rect.anchoredPosition = new Vector2(slide, 0f);
        page.group.alpha = enter;

        int rowCount = page.rows.Count;
        bool onRow = selected < rowCount;
        highlightY = Mathf.Lerp(highlightY, FirstRowY - Mathf.Min(selected, Mathf.Max(rowCount - 1, 0)) * RowSpacing, follow);
        highlight.anchoredPosition = new Vector2(slide, highlightY);
        highlightGroup.alpha = Mathf.MoveTowards(highlightGroup.alpha, onRow ? 1f : 0f, dt * 8f);
        chevron.rectTransform.anchoredPosition = new Vector2(4f * Mathf.Sin(now * 6f), 0f);

        float rowsFrom = Mathf.Max(openedAt + 0.3f, pageChangedAt);
        for (int i = 0; i < rowCount; i++)
            AnimateRow(page.rows[i], i == selected, rowsFrom + i * 0.05f, now);

        string help;
        if (onRow)
        {
            Setting setting = page.rows[selected].setting;
            help = setting.Available || setting.unavailableHelp == null ? setting.help : setting.unavailableHelp;
        }
        else if (selected == rowCount)
        {
            help = now < confirmUntil ? "PRESS AGAIN TO PUT EVERY SETTING BACK." : "PUT EVERY SETTING BACK TO HOW IT STARTED.";
        }
        else
        {
            help = "SAVE YOUR CHANGES AND GO BACK.";
        }
        if (help != lastHelp)
        {
            lastHelp = help;
            helpChangedAt = now;
        }
        helpLabel.text = TitleUI.Decode(help, TitleUI.Phase(now, helpChangedAt, helpChangedAt + 0.25f), now);

        bool confirming = now < confirmUntil;
        AnimateButton(resetButton, selected == rowCount, confirming ? "CONFIRM?" : resetButton.text, confirming, now);
        AnimateButton(backButton, selected == rowCount + 1, backButton.text, false, now);
    }

    void AnimateTabs(float now, float follow)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            Page page = pages[i];
            bool active = i == pageIndex;
            bool hover = !active && MenuInput.Over(page.tab);
            float start = openedAt + 0.2f + i * 0.05f;
            float appear = TitleUI.EaseOut(TitleUI.Phase(now, start, start + 0.3f));
            page.tab.anchoredPosition = new Vector2((i - 1.5f) * TabSpacing, TabY + 20f * (1f - appear));
            page.tabLabel.text = TitleUI.Decode(page.name, appear, now);
            page.tabFill.color = active ? TabActive : hover ? TabHover : TabIdle;
            page.tabEdge.color = active ? Cyan : TitleUI.Fade(Cyan, hover ? 0.8f : 0.4f);
            page.tabLabel.color = active || hover ? Bright : Dim;
        }
        underlineX = Mathf.Lerp(underlineX, (pageIndex - 1.5f) * TabSpacing, follow);
        tabUnderline.anchoredPosition = new Vector2(underlineX, TabY - 44f);
    }

    void AnimateRow(Row row, bool isSelected, float start, float now)
    {
        Setting setting = row.setting;
        row.label.text = TitleUI.Decode(setting.name, TitleUI.Phase(now, start, start + 0.3f), now);
        row.label.color = isSelected ? Bright : Dim;
        row.group.alpha = setting.Available ? 1f : 0.35f;
        float bump = Mathf.Exp(-(now - row.changedAt) * 12f);

        switch (setting.kind)
        {
            case Kind.Slider:
            {
                float value = setting.value();
                int lit = Mathf.RoundToInt(value * SliderSegments);
                float pulse = isSelected ? 0.5f + 0.5f * Mathf.Sin(now * 6f) : 0f;
                for (int s = 0; s < SliderSegments; s++)
                {
                    Color color = s < lit ? (isSelected ? Color.Lerp(SegmentLit, SegmentBright, 0.35f) : SegmentLit) : SegmentUnlit;
                    if (s == lit - 1) color = Color.Lerp(Color.Lerp(color, SegmentBright, pulse), Color.white, bump);
                    row.segments[s].color = color;
                }
                row.value.text = $"{Mathf.RoundToInt(value * 100f)}%";
                row.value.color = Color.Lerp(isSelected ? Bright : Dim, Color.white, bump);
                break;
            }
            case Kind.Choice:
            {
                string[] options = setting.options();
                int count = options.Length;
                int index = Mathf.Clamp(setting.index(), 0, Mathf.Max(count - 1, 0));
                row.value.text = count > 0 ? options[index] : "";
                row.value.color = Color.Lerp(isSelected ? Bright : Dim, Color.white, bump);

                Color arrow = isSelected ? Cyan : TitleUI.Fade(Dim, 0.6f);
                row.leftArrow.color = row.changedStep < 0 ? Color.Lerp(arrow, Color.white, bump) : arrow;
                row.rightArrow.color = row.changedStep > 0 ? Color.Lerp(arrow, Color.white, bump) : arrow;
                float push = 8f * bump;
                row.leftArrow.rectTransform.anchoredPosition = new Vector2(row.changedStep < 0 ? -push : 0f, 0f);
                row.rightArrow.rectTransform.anchoredPosition = new Vector2(row.changedStep > 0 ? push : 0f, 0f);

                bool showPips = count <= MaxPips;
                for (int p = 0; p < MaxPips; p++)
                {
                    HudShape pip = row.pips[p];
                    pip.enabled = showPips && p < count;
                    if (!pip.enabled) continue;
                    pip.rectTransform.anchoredPosition = new Vector2(ControlX + (p - (count - 1) * 0.5f) * 26f, -24f);
                    pip.color = p == index ? Cyan : TitleUI.Fade(Dim, 0.35f);
                }
                break;
            }
            case Kind.Toggle:
            {
                bool on = setting.on();
                Color lit = Color.Lerp(isSelected ? Color.Lerp(TabActive, SegmentBright, 0.25f) : TabActive, Color.white, bump * 0.6f);
                row.onFill.color = on ? lit : TabIdle;
                row.offFill.color = on ? TabIdle : lit;
                row.onText.color = on ? Bright : TitleUI.Fade(Dim, 0.7f);
                row.offText.color = on ? TitleUI.Fade(Dim, 0.7f) : Bright;
                break;
            }
        }
    }

    static void AnimateButton(Button button, bool isSelected, string text, bool warning, float now)
    {
        bool hover = MenuInput.Over(button.hit);
        float bump = Mathf.Exp(-(now - button.pressedAt) * 10f);
        Color accent = warning ? Warning : Cyan;
        if (warning && Mathf.Repeat(now * 3f, 1f) < 0.5f) accent = Color.Lerp(accent, Color.white, 0.4f);
        button.label.text = text;
        button.fill.color = Color.Lerp(isSelected ? TitleUI.Fade(warning ? Warning * 0.45f : TabActive, 0.95f) : hover ? TabHover : TabIdle, Color.white, bump);
        button.edge.color = isSelected || hover || warning ? accent : TitleUI.Fade(Cyan, 0.4f);
        button.label.color = isSelected || hover ? Bright : warning ? accent : Dim;
    }

    // --- Building ---

    Page AddPage(TMP_FontAsset font, string name)
    {
        int i = pages.Count;
        var page = new Page { name = name };
        page.rect = TitleUI.Place(TitleUI.NewRect(name, panel), Vector2.zero, new Vector2(HalfWidth * 2f, HalfHeight * 2f));
        page.group = page.rect.gameObject.AddComponent<CanvasGroup>();

        page.tab = TitleUI.Place(TitleUI.NewRect(name + " Tab", tabs), new Vector2((i - 1.5f) * TabSpacing, TabY), new Vector2(TabWidth, TabHeight));
        Vector2[] shape = TitleUI.Slant(Vector2.zero, TabWidth, TabHeight, 12f);
        page.tabFill = TitleUI.Shape("Fill", page.tab, TabIdle, shape, filled: true);
        page.tabEdge = TitleUI.Shape("Edge", page.tab, TitleUI.Fade(Cyan, 0.4f), shape, thickness: 2f, feather: 2f);
        page.tabLabel = TitleUI.Label("Label", page.tab, font, 36f, Dim, new Vector2(0f, 2f), new Vector2(TabWidth, TabHeight));
        page.tabLabel.characterSpacing = 8f;

        pages.Add(page);
        return page;
    }

    Row AddRow(Page page, TMP_FontAsset font, Setting setting)
    {
        float y = FirstRowY - page.rows.Count * RowSpacing;
        var row = new Row { setting = setting };
        row.rect = TitleUI.Place(TitleUI.NewRect(setting.name, page.rect), new Vector2(0f, y), new Vector2(RowHalfWidth * 2f, 62f));
        row.group = row.rect.gameObject.AddComponent<CanvasGroup>();
        row.label = TitleUI.Label("Label", row.rect, font, 40f, Dim, new Vector2(-330f, 0f), new Vector2(560f, 60f), TextAlignmentOptions.Left);
        row.label.characterSpacing = 8f;
        TitleUI.Shape("Rule", row.rect, TitleUI.Fade(Teal, 0.18f), new[] { new Vector2(-RowHalfWidth, -34f), new Vector2(RowHalfWidth, -34f) }, thickness: 1.5f, closed: false);
        page.rows.Add(row);
        return row;
    }

    void AddSlider(Page page, TMP_FontAsset font, string name, string help, System.Func<float> value, System.Action<float> setValue)
    {
        Row row = AddRow(page, font, new Setting { kind = Kind.Slider, name = name, help = help, value = value, setValue = setValue });

        // The track doubles as the area the mouse clicks and drags along.
        row.track = TitleUI.Place(TitleUI.NewRect("Track", row.rect), new Vector2(ControlX - 40f, 0f), new Vector2(400f, 44f));
        row.segments = new HudShape[SliderSegments];
        float slot = 400f / SliderSegments;
        for (int s = 0; s < SliderSegments; s++)
        {
            var center = new Vector2(-200f + slot * (s + 0.5f), 0f);
            row.segments[s] = TitleUI.Shape($"Segment {s + 1}", row.track, SegmentUnlit, TitleUI.Slant(center, 12f, 32f, 5f), filled: true);
        }
        TitleUI.Shape("Readout", row.rect, TitleUI.Fade(Teal, 0.7f), TitleUI.Box(ControlX + 190f, ControlX + 310f, 22f, -22f), thickness: 2f, feather: 1f);
        row.value = TitleUI.Label("Value", row.rect, font, 34f, Dim, new Vector2(ControlX + 250f, 2f), new Vector2(120f, 44f));
    }

    Setting AddChoice(Page page, TMP_FontAsset font, string name, string help, System.Func<string[]> options, System.Func<int> index, System.Action<int> setIndex)
    {
        var setting = new Setting { kind = Kind.Choice, name = name, help = help, options = options, index = index, setIndex = setIndex };
        Row row = AddRow(page, font, setting);

        row.value = TitleUI.Label("Value", row.rect, font, 36f, Dim, new Vector2(ControlX, 6f), new Vector2(420f, 44f));
        row.value.characterSpacing = 4f;
        row.leftArrow = TitleUI.Shape("Left", row.rect, Cyan,
            new[] { new Vector2(ControlX - 250f, 6f), new Vector2(ControlX - 232f, 20f), new Vector2(ControlX - 232f, -8f) }, filled: true);
        row.rightArrow = TitleUI.Shape("Right", row.rect, Cyan,
            new[] { new Vector2(ControlX + 250f, 6f), new Vector2(ControlX + 232f, -8f), new Vector2(ControlX + 232f, 20f) }, filled: true);
        row.leftHit = TitleUI.Place(TitleUI.NewRect("Left Half", row.rect), new Vector2(ControlX - 135f, 0f), new Vector2(270f, 62f));
        row.rightHit = TitleUI.Place(TitleUI.NewRect("Right Half", row.rect), new Vector2(ControlX + 135f, 0f), new Vector2(270f, 62f));
        row.pips = new HudShape[MaxPips];
        for (int p = 0; p < MaxPips; p++)
            row.pips[p] = TitleUI.Shape("Pip", row.rect, Dim, TitleUI.Box(-9f, 9f, 2.5f, -2.5f), filled: true);
        return setting;
    }

    void AddToggle(Page page, TMP_FontAsset font, string name, string help, System.Func<bool> on, System.Action<bool> setOn)
    {
        Row row = AddRow(page, font, new Setting { kind = Kind.Toggle, name = name, help = help, on = on, setOn = setOn });

        var offCenter = new Vector2(ControlX - 72f, 0f);
        var onCenter = new Vector2(ControlX + 72f, 0f);
        row.offFill = TitleUI.Shape("Off", row.rect, TabIdle, TitleUI.Slant(offCenter, 130f, 44f, 6f), filled: true);
        row.onFill = TitleUI.Shape("On", row.rect, TabIdle, TitleUI.Slant(onCenter, 130f, 44f, 6f), filled: true);
        TitleUI.Shape("Frame", row.rect, TitleUI.Fade(Teal, 0.8f), TitleUI.Box(ControlX - 150f, ControlX + 150f, 26f, -26f), thickness: 2f, feather: 1f);
        row.offText = TitleUI.Label("Off Text", row.rect, font, 32f, Dim, offCenter + new Vector2(0f, 2f), new Vector2(130f, 44f));
        row.offText.text = "OFF";
        row.onText = TitleUI.Label("On Text", row.rect, font, 32f, Dim, onCenter + new Vector2(0f, 2f), new Vector2(130f, 44f));
        row.onText.text = "ON";
        row.offHit = TitleUI.Place(TitleUI.NewRect("Off Hit", row.rect), offCenter, new Vector2(144f, 56f));
        row.onHit = TitleUI.Place(TitleUI.NewRect("On Hit", row.rect), onCenter, new Vector2(144f, 56f));
    }

    // Two columns of actions and their keys. Nothing here can be changed yet, so the page has no rows to select.
    void BuildControls(Page page, TMP_FontAsset font)
    {
        for (int i = 0; i < BindingActions.Length; i++)
        {
            bool leftColumn = i < 4;
            float left = leftColumn ? -630f : 30f, right = leftColumn ? -30f : 630f;
            float y = FirstRowY - (i % 4) * RowSpacing;

            TextMeshProUGUI action = TitleUI.Label(BindingActions[i], page.rect, font, 36f, Dim, new Vector2(left + 250f, y), new Vector2(500f, 56f), TextAlignmentOptions.Left);
            action.text = BindingActions[i];
            action.characterSpacing = 6f;
            float x = right;
            string[] keys = BindingKeys[i];
            for (int k = keys.Length - 1; k >= 0; k--)
                x = KeyCap(page.rect, font, keys[k], x, y, true) - 8f;
            TitleUI.Shape("Rule", page.rect, TitleUI.Fade(Teal, 0.18f), new[] { new Vector2(left, y - 34f), new Vector2(right, y - 34f) }, thickness: 1.5f, closed: false);
        }
        TitleUI.Shape("Divider", page.rect, TitleUI.Fade(Cyan, 0.2f), new[] { new Vector2(0f, FirstRowY + 30f), new Vector2(0f, FirstRowY - 3f * RowSpacing - 30f) }, thickness: 2f, closed: false);

        TextMeshProUGUI note = TitleUI.Label("Note", page.rect, font, 28f, TitleUI.Fade(Dim, 0.7f), new Vector2(0f, FirstRowY - 4f * RowSpacing + 6f), new Vector2(1260f, 40f));
        note.text = "KEYS CAN'T BE CHANGED YET.";
        note.characterSpacing = 4f;
    }

    void BuildFooterHints(TMP_FontAsset font)
    {
        float x = -650f;
        x = ArrowCap(panel, x, FooterY, Vector2.up) + 6f;
        x = ArrowCap(panel, x, FooterY, Vector2.down) + 10f;
        x = Hint(font, "SELECT", x) + 28f;
        x = ArrowCap(panel, x, FooterY, Vector2.left) + 6f;
        x = ArrowCap(panel, x, FooterY, Vector2.right) + 10f;
        x = Hint(font, "CHANGE", x) + 28f;
        x = KeyCap(panel, font, "Q", x, FooterY, false) + 6f;
        x = KeyCap(panel, font, "E", x, FooterY, false) + 10f;
        x = Hint(font, "TAB", x) + 28f;
        x = KeyCap(panel, font, "ESC", x, FooterY, false) + 10f;
        Hint(font, "BACK", x);
    }

    float Hint(TMP_FontAsset font, string text, float left)
    {
        TextMeshProUGUI label = TitleUI.Label("Hint " + text, panel, font, 26f, Dim, Vector2.zero, new Vector2(300f, 40f), TextAlignmentOptions.Left);
        label.text = text;
        label.characterSpacing = 3f;
        float width = label.preferredWidth;
        label.rectTransform.sizeDelta = new Vector2(width + 4f, 40f);
        label.rectTransform.anchoredPosition = new Vector2(left + width * 0.5f + 2f, FooterY + 2f);
        return left + width + 4f;
    }

    Button MakeButton(TMP_FontAsset font, string text, float centerX, float width)
    {
        var center = new Vector2(centerX, FooterY);
        var button = new Button { text = text };
        button.hit = TitleUI.Place(TitleUI.NewRect(text + " Button", panel), center, new Vector2(width + 10f, 58f));
        Vector2[] shape = TitleUI.Slant(center, width, 50f, 10f);
        button.fill = TitleUI.Shape("Fill", panel, TabIdle, shape, filled: true);
        button.edge = TitleUI.Shape("Edge", panel, TitleUI.Fade(Cyan, 0.4f), shape, thickness: 2f, feather: 2f);
        button.label = TitleUI.Label("Label", panel, font, 34f, Dim, center + new Vector2(0f, 2f), new Vector2(width, 50f));
        button.label.characterSpacing = 6f;
        button.label.text = text;
        return button;
    }

    // A keycap with text on it, from one edge: to the left of edge when fromRight, else to the right. Returns its far edge.
    static float KeyCap(Transform parent, TMP_FontAsset font, string text, float edge, float y, bool fromRight)
    {
        TextMeshProUGUI label = TitleUI.Label("Key " + text, parent, font, 28f, Bright, Vector2.zero, new Vector2(300f, 44f));
        label.text = text;
        float width = Mathf.Max(44f, label.preferredWidth + 22f);
        float center = fromRight ? edge - width * 0.5f : edge + width * 0.5f;
        DrawCap(parent, center, y, width);
        label.rectTransform.sizeDelta = new Vector2(width, 44f);
        label.rectTransform.anchoredPosition = new Vector2(center, y + 2f);
        label.transform.SetAsLastSibling();
        return fromRight ? edge - width : edge + width;
    }

    // A keycap with an arrow on it, from its left edge. Returns its right edge.
    static float ArrowCap(Transform parent, float left, float y, Vector2 direction)
    {
        const float width = 44f;
        var center = new Vector2(left + width * 0.5f, y + 2f);
        DrawCap(parent, center.x, y, width);
        var side = new Vector2(-direction.y, direction.x);
        TitleUI.Shape("Arrow", parent, Bright, new[] { center + direction * 9f, center - direction * 7f + side * 9f, center - direction * 7f - side * 9f }, filled: true);
        return left + width;
    }

    static void DrawCap(Transform parent, float center, float y, float width)
    {
        Vector2[] box = TitleUI.Box(center - width * 0.5f, center + width * 0.5f, y + 21f, y - 21f);
        TitleUI.Shape("Cap", parent, CapFill, box, filled: true);
        TitleUI.Shape("Cap Edge", parent, TitleUI.Fade(Cyan, 0.55f), box, thickness: 2f, feather: 1f);
        TitleUI.Shape("Cap Lip", parent, TitleUI.Fade(Cyan, 0.35f),
            new[] { new Vector2(center - width * 0.5f + 3f, y - 25f), new Vector2(center + width * 0.5f - 3f, y - 25f) }, thickness: 3f, closed: false);
    }

    static Texture2D GridTexture()
    {
        Texture2D texture = PixelArt.MakeTexture(40, 40, (x, y) => x == 0 || y == 0 ? new Color32(255, 255, 255, 255) : default);
        texture.wrapMode = TextureWrapMode.Repeat;
        return texture;
    }

    static Texture2D StripeTexture()
    {
        Texture2D texture = PixelArt.MakeTexture(30, 30, (x, y) => ((x - y) % 30 + 30) % 30 < 12 ? new Color32(255, 255, 255, 255) : default);
        texture.wrapMode = TextureWrapMode.Repeat;
        return texture;
    }
}
