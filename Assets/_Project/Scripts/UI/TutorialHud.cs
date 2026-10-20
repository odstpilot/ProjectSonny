using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// On-screen text for the tutorial and its cutscenes, built from code the first time something asks for it, so there's
// no canvas to set up:
// It all shares the game's look (GameUI): the terminal font, pixel-framed glass panels, and light key caps.
//  - a control prompt along the bottom: key caps and what they do, with an optional line of detail under it, on the
//    same slim glass tag as the prompts over the level (PromptBadge), a thin amber strip down its left edge.
//    It slides up into place, and once the player has done it the accent turns green and it drops away.
//  - the current objective in the top left corner. Moving on from one objective means it's done, and saves the game
//    (SaveGame), with the save icon in the bottom right.
//  - captions for a voice over the station speakers, typed out a letter at a time over a soft dark backdrop
//  - letterbox bars, a fade to black, and title cards
// Everything runs on unscaled time, so hit stop can't stall it. One per scene: TutorialHud.Get().
public class TutorialHud : MonoBehaviour
{
    const int SortingOrder = 90;            // under LockerView (100), so the view from inside a locker covers it
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const float LetterboxHeight = 130f;
    const float LettersPerSecond = 34f;
    const float PromptFadeTime = 0.25f;
    const float PromptY = 72f;              // the prompt's resting height off the bottom of the screen
    const float PromptSlide = 14f;          // how far it slides in and out
    const float DoneHoldTime = 0.8f;
    const float Margin = 56f;
    const float KeyHeight = 54f;

    static readonly Color AccentColor = GameUI.Amber;
    static readonly Color KeyColor = Color.white;
    static readonly Color TextColor = GameUI.Text;
    static readonly Color DimTextColor = GameUI.Dim;
    static readonly Color DoneColor = GameUI.Green;
    static readonly Color SpeakerColor = GameUI.Red;
    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.75f);

    static TutorialHud instance;

    [Tooltip("Font for the text people read. Leave empty for the game's own (m6x11). Headings always use SdAsteroid.")]
    public TMP_FontAsset font;

    public bool PromptShowing { get; private set; }
    // True while a finished prompt is still showing its green tick.
    public bool PromptCompleting { get; private set; }

    private bool built;
    private CanvasGroup promptGroup;
    private RectTransform prompt;
    private Image promptAccent;
    private RectTransform promptKeys;
    private TextMeshProUGUI promptAction;
    private TextMeshProUGUI promptHint;
    private readonly List<Graphic> keyCaps = new List<Graphic>();
    private readonly List<TextMeshProUGUI> keyLabels = new List<TextMeshProUGUI>();
    private Coroutine promptRoutine;

    private RectTransform objective;
    private CanvasGroup objectiveGroup;
    private TextMeshProUGUI objectiveText;
    private TextMeshProUGUI objectiveStep;
    private LayoutElement objectiveTextSize, objectiveStepSize;
    private Image objectivePanel, objectiveAccent;
    private RawImage objectiveScanlines;
    private float objectiveSlide;           // canvas units it's still to slide in from the left
    private float objectiveFlash;           // 1 just after it changes, settling to 0
    private Coroutine objectiveRoutine;
    private string currentObjective = "";

    private CanvasGroup captionGroup;
    private TextMeshProUGUI captionSpeaker;
    private TextMeshProUGUI captionLine;

    private RectTransform letterboxTop;
    private RectTransform letterboxBottom;
    private readonly List<RawImage> barEdges = new List<RawImage>();
    private Image fade;
    private RawImage vignette;
    private TextMeshProUGUI titleText;

    // True while the screen is mostly faded to black, for anything that shouldn't show over a fade or a title card.
    public static bool ScreenCovered => instance != null && instance.fade != null && instance.fade.enabled && instance.fade.color.a > 0.5f;

    public static TutorialHud Get()
    {
        if (instance == null) instance = FindAnyObjectByType<TutorialHud>();
        if (instance == null) instance = new GameObject("TutorialHud", typeof(RectTransform)).AddComponent<TutorialHud>();
        instance.Build();
        return instance;
    }

    void Awake()
    {
        if (instance == null) instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // --- Control prompt ---

    // For example ShowPrompt("Move", "W", "A", "S", "D"). Replaces whatever prompt is up.
    public void ShowPrompt(string action, params string[] keys)
    {
        ShowPromptWithHint(action, null, keys);
    }

    // The same, with a smaller line of detail under the action, like ("Swing", "Hold to charge a heavy swing", "LEFT CLICK").
    public void ShowPromptWithHint(string action, string hint, params string[] keys)
    {
        StopPromptRoutine();

        var oldKeys = new List<Transform>();
        foreach (Transform child in promptKeys) oldKeys.Add(child);
        foreach (Transform child in oldKeys)
        {
            child.SetParent(null);
            Destroy(child.gameObject);
        }
        keyCaps.Clear();
        keyLabels.Clear();
        foreach (string key in keys) AddKey(key);
        promptKeys.gameObject.SetActive(keys.Length > 0);

        promptAction.text = GameUI.Sentence(action);
        promptHint.text = hint ?? "";
        promptHint.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        SetPromptColor(AccentColor, KeyColor, TextColor);

        PromptShowing = true;
        promptRoutine = StartCoroutine(SlidePrompt(1f, PromptY - PromptSlide, PromptY));
    }

    // Ticks the prompt off: it turns green, pops, and drops away.
    public void CompletePrompt()
    {
        if (!PromptShowing) return;
        StopPromptRoutine();
        PromptShowing = false;
        PromptCompleting = true;
        promptRoutine = StartCoroutine(CompleteRoutine());
    }

    public void HidePrompt()
    {
        if (!PromptShowing && !PromptCompleting) return;
        StopPromptRoutine();
        PromptShowing = false;
        promptRoutine = StartCoroutine(SlidePrompt(0f, prompt.anchoredPosition.y, PromptY - PromptSlide));
    }

    IEnumerator CompleteRoutine()
    {
        SetPromptColor(DoneColor, DoneColor, DoneColor);

        const float popTime = 0.15f;
        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            prompt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t / popTime * Mathf.PI));
            yield return null;
        }
        prompt.localScale = Vector3.one;

        yield return WaitUnscaled(DoneHoldTime);
        yield return SlidePrompt(0f, PromptY, PromptY - PromptSlide);
        PromptCompleting = false;
        promptRoutine = null;
    }

    IEnumerator SlidePrompt(float alpha, float fromY, float toY)
    {
        prompt.localScale = Vector3.one;
        float startAlpha = promptGroup.alpha;
        for (float t = 0f; t < PromptFadeTime; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / PromptFadeTime);
            promptGroup.alpha = Mathf.Lerp(startAlpha, alpha, progress);
            prompt.anchoredPosition = new Vector2(0f, Mathf.Lerp(fromY, toY, progress));
            yield return null;
        }
        promptGroup.alpha = alpha;
        prompt.anchoredPosition = new Vector2(0f, toY);
    }

    void SetPromptColor(Color accent, Color key, Color text)
    {
        promptAccent.color = accent;
        foreach (Graphic cap in keyCaps)
            if (cap != null) cap.color = key;
        promptAction.color = text;
    }

    void StopPromptRoutine()
    {
        if (promptRoutine != null) StopCoroutine(promptRoutine);
        promptRoutine = null;
        PromptCompleting = false;
    }

    // A key cap, as wide as its label needs.
    void AddKey(string key)
    {
        Image cap = GameUI.Sliced("Key " + key, promptKeys, GameUI.SoftKeyCapSprite, Color.white);
        cap.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        TextMeshProUGUI label = GameUI.Label("Key", cap.transform, key, key.Length > 1 ? 24f : 34f, GameUI.KeyText, TextAlignmentOptions.Center);
        GameUI.Anchor(label.rectTransform, 0f, 0f, 1f, 1f, 4f, 4f, 4f, 0f);
        var capSize = cap.gameObject.AddComponent<LayoutElement>();
        capSize.minHeight = capSize.preferredHeight = KeyHeight;
        capSize.minWidth = capSize.preferredWidth = GameUI.KeyCapWidth(key, KeyHeight);
        keyCaps.Add(cap);
        keyLabels.Add(label);
    }

    // --- Objective ---

    // Below the health readout, when there is one in the corner.
    void LateUpdate()
    {
        float top = HealthHud.CellsShowing ? HealthHud.Bottom + 22f : Margin * 0.8f;
        float dt = Time.unscaledDeltaTime;
        objectiveSlide = Mathf.MoveTowards(objectiveSlide, 0f, dt * 160f);
        objective.anchoredPosition = new Vector2(Margin - objectiveSlide, -top);

        // A new objective lights the panel up amber for a moment, then it settles back to dark glass.
        objectiveFlash = Mathf.MoveTowards(objectiveFlash, 0f, dt * 1.4f);
        float flash = objectiveFlash * objectiveFlash;
        objectivePanel.color = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.55f), flash);
        objectiveAccent.color = Color.Lerp(AccentColor, Color.white, flash * 0.6f);
        objectiveAccent.rectTransform.sizeDelta = new Vector2(GameUI.PixelSize * (1f + flash * 1.5f) * 2f, -GameUI.PixelSize * 6f);
        Rect area = objective.rect;
        objectiveScanlines.uvRect = new Rect(0f, Time.unscaledTime * 0.15f, 1f, area.height / 6f);
    }

    // What's showing now, empty for nothing.
    public string Objective => currentObjective ?? "";

    // Empty hides it. Whatever it was before, if anything, is done, and the game saves.
    public void SetObjective(string text)
    {
        string was = currentObjective;
        currentObjective = text ?? "";
        if (!string.IsNullOrEmpty(was) && was != currentObjective) SaveGame.ObjectiveFinished(was, currentObjective);
        if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
        objectiveRoutine = StartCoroutine(ChangeObjective(text));
    }

    IEnumerator ChangeObjective(string text)
    {
        if (objectiveGroup.alpha > 0f) yield return FadeGroup(objectiveGroup, 0f, 0.2f);
        objectiveText.text = text;
        ShowStep(ObjectiveGuide.StepFor(text));
        if (!string.IsNullOrEmpty(text))
        {
            objectiveSlide = 26f;
            objectiveFlash = 1f;
            yield return FadeGroup(objectiveGroup, 1f, 0.4f);
        }
        objectiveRoutine = null;
    }

    // The line under the objective saying how to go about it, [X] picking out a key. Empty hides it. ObjectiveGuide
    // gives one for every objective it knows; this is for a level that wants to say something else for a while.
    public void SetStep(string text) => ShowStep(text);

    void ShowStep(string text)
    {
        bool any = !string.IsNullOrEmpty(text);
        objectiveStep.gameObject.SetActive(any);
        objectiveStep.text = any ? "<color=#5FD7F0>></color>  " + ObjectiveGuide.Keys(text) : "";
        // As wide as the longer of the two needs, up to a limit, so a short objective gets a small panel.
        const float MaxWidth = 640f, MinWidth = 300f;
        float width = Mathf.Max(objectiveText.GetPreferredValues(objectiveText.text).x, any ? objectiveStep.GetPreferredValues(objectiveStep.text).x : 0f);
        width = Mathf.Clamp(width + 4f, MinWidth, MaxWidth);
        objectiveTextSize.preferredWidth = objectiveStepSize.preferredWidth = width;
    }

    // --- Captions ---

    // Types the line out under the speaker's name, holds it, then fades it out.
    public IEnumerator Say(string speaker, string line, float holdSeconds = 2.5f)
    {
        captionSpeaker.text = speaker.ToUpperInvariant();
        captionLine.text = line;
        captionLine.maxVisibleCharacters = 0;
        if (captionGroup.alpha < 1f) yield return FadeGroup(captionGroup, 1f, 0.2f);

        for (float shown = 0f; shown < line.Length; shown += LettersPerSecond * Time.unscaledDeltaTime)
        {
            captionLine.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        captionLine.maxVisibleCharacters = line.Length;

        yield return WaitUnscaled(holdSeconds);
        yield return FadeGroup(captionGroup, 0f, 0.4f);
    }

    // --- Fades, letterbox, and title cards ---

    public void SetFade(float alpha)
    {
        fade.color = new Color(0f, 0f, 0f, alpha);
        fade.enabled = alpha > 0f;
    }

    public IEnumerator FadeTo(float alpha, float seconds)
    {
        float start = fade.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(start, alpha, t / seconds));
            yield return null;
        }
        SetFade(alpha);
    }

    public IEnumerator Letterbox(bool show, float seconds)
    {
        float start = letterboxTop.sizeDelta.y;
        float end = show ? LetterboxHeight : 0f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetLetterbox(Mathf.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        SetLetterbox(end);
    }

    // Darkens the edges of the picture to this much (0 none, 1 black at the corners), over seconds.
    public IEnumerator Vignette(float amount, float seconds)
    {
        float start = vignette.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            vignette.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, amount, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        vignette.color = new Color(0f, 0f, 0f, amount);
    }

    // Clear in the middle, black at the edges, wider than tall like the screen.
    static Texture2D VignetteTexture()
    {
        const int W = 96, H = 54;
        var texture = PixelArt.MakeTexture(W, H, (x, y) =>
        {
            float dx = (x - (W - 1) * 0.5f) / (W * 0.5f), dy = (y - (H - 1) * 0.5f) / (H * 0.5f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d));
            return new Color32(0, 0, 0, (byte)(255 * a));
        });
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    // Lines of text on black, one after another, each fading in and out. Fades to black first if it isn't already.
    public IEnumerator TitleCard(string[] lines, float holdSeconds = 2f)
    {
        if (fade.color.a < 1f) yield return FadeTo(1f, 0.6f);

        foreach (string line in lines)
        {
            titleText.text = line.ToUpperInvariant();
            yield return FadeText(titleText, 1f, 0.7f);
            yield return WaitUnscaled(holdSeconds);
            yield return FadeText(titleText, 0f, 0.6f);
        }
    }

    // Clears a caption or title card left halfway, for a cutscene that's been skipped (its coroutine stopped with the
    // cutscene's). The fade's left as it is.
    public void CutAway()
    {
        captionGroup.alpha = 0f;
        titleText.alpha = 0f;
    }

    void SetLetterbox(float height)
    {
        letterboxTop.sizeDelta = new Vector2(0f, height);
        letterboxBottom.sizeDelta = new Vector2(0f, height);
        foreach (RawImage edge in barEdges)
        {
            edge.enabled = height > 0.5f;
            edge.color = new Color(0f, 0f, 0f, Mathf.Clamp01(height / 60f));
        }
    }

    // --- Building ---

    void Build()
    {
        if (built) return;
        built = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        BuildObjective();
        BuildPrompt();
        BuildCaption();
        gameObject.AddComponent<ObjectiveGuide>();     // the how-to under the objective, and Pip's nudges when stuck

        letterboxTop = Bar("Letterbox Top", 1f);
        letterboxBottom = Bar("Letterbox Bottom", 0f);

        // The edges of the picture sinking into shadow, for a moment that wants it (Vignette). Under the letterbox.
        vignette = Stretch(NewRect("Vignette", transform), 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
        vignette.texture = VignetteTexture();
        vignette.raycastTarget = false;
        vignette.color = new Color(0f, 0f, 0f, 0f);
        vignette.transform.SetSiblingIndex(0);

        // Fade and title cards, over everything else.
        fade = Stretch(NewRect("Fade", transform), 0f, 0f, 1f, 1f).gameObject.AddComponent<Image>();
        fade.raycastTarget = false;
        SetFade(0f);
        titleText = Label("Title", transform, "", 60f, TextColor, TextAlignmentOptions.Center, GameUI.Heading);
        titleText.characterSpacing = 20f;
        titleText.fontSharedMaterial = TitleUI.GlowMaterial(titleText.font, TitleUI.Fade(GameUI.Cyan, 0.35f), 0.85f, 0.35f);
        titleText.alpha = 0f;
        Stretch(titleText.rectTransform, 0.1f, 0f, 0.9f, 1f);
    }

    // Top left, the corner the health readout would use; the tutorial hides that, since the player can't die in it.
    // A short amber rule, a small heading, and the objective itself, shadowed so it reads over a bright room.
    // A pane of dark glass in the suit's style, its top right corner cut off, with an amber edge down the left and faint
    // scanlines rolling over it: OBJECTIVE, what to do, and under that how (ObjectiveGuide), the pane sized to fit.
    void BuildObjective()
    {
        objective = NewRect("Objective", transform);
        objective.anchorMin = objective.anchorMax = objective.pivot = new Vector2(0f, 1f);
        objective.anchoredPosition = new Vector2(Margin, -Margin * 0.8f);
        objectiveGroup = objective.gameObject.AddComponent<CanvasGroup>();
        objectiveGroup.alpha = 0f;

        objectivePanel = objective.gameObject.AddComponent<Image>();
        objectivePanel.sprite = ObjectivePanelSprite;
        objectivePanel.type = Image.Type.Sliced;
        objectivePanel.pixelsPerUnitMultiplier = 1f / GameUI.PixelSize;
        objectivePanel.raycastTarget = false;
        var column = objective.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(30, 34, 14, 16);
        column.spacing = 4f;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        var fit = objective.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Faint scanlines over the glass, drifting slowly down.
        objectiveScanlines = Stretch(NewRect("Scanlines", objective), 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
        Texture2D lines = PixelArt.MakeTexture(1, 6, (x, y) => y == 0 ? new Color32(140, 200, 255, 14) : default);
        lines.wrapMode = TextureWrapMode.Repeat;
        objectiveScanlines.texture = lines;
        objectiveScanlines.raycastTarget = false;
        objectiveScanlines.rectTransform.offsetMin = new Vector2(GameUI.PixelSize * 2f, GameUI.PixelSize * 2f);
        objectiveScanlines.rectTransform.offsetMax = new Vector2(-GameUI.PixelSize * 2f, -GameUI.PixelSize * 2f);
        objectiveScanlines.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // The amber edge down the left, just inside the frame.
        RectTransform accent = NewRect("Accent", objective);
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.anchoredPosition = new Vector2(GameUI.PixelSize * 2f, 0f);
        accent.offsetMin = new Vector2(accent.offsetMin.x, GameUI.PixelSize * 3f);
        accent.offsetMax = new Vector2(accent.offsetMax.x, -GameUI.PixelSize * 3f);
        accent.sizeDelta = new Vector2(GameUI.PixelSize * 3f, accent.sizeDelta.y);
        objectiveAccent = accent.gameObject.AddComponent<Image>();
        objectiveAccent.color = AccentColor;
        objectiveAccent.raycastTarget = false;
        accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // OBJECTIVE, with a small diamond before it and a rule trailing off after it.
        RectTransform header = NewRect("Header", objective);
        HorizontalLayoutGroup headerRow = Row(header.gameObject, new RectOffset(0, 0, 0, 2), 10f);
        headerRow.childAlignment = TextAnchor.MiddleLeft;
        Image diamond = NewRect("Diamond", header).gameObject.AddComponent<Image>();
        diamond.color = AccentColor;
        diamond.raycastTarget = false;
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var diamondSize = diamond.gameObject.AddComponent<LayoutElement>();
        diamondSize.minWidth = diamondSize.preferredWidth = diamondSize.minHeight = diamondSize.preferredHeight = 10f;
        TextMeshProUGUI label = Label("Label", header, "OBJECTIVE", 22f, AccentColor, TextAlignmentOptions.MidlineLeft, GameUI.Heading);
        label.characterSpacing = 12f;
        label.fontSharedMaterial = GameUI.Shadow(label.font);
        RawImage rule = NewRect("Rule", header).gameObject.AddComponent<RawImage>();
        rule.texture = PixelArt.MakeTexture(32, 1, (x, y) => new Color32(255, 174, 82, (byte)(150 * (1f - x / 31f))));
        rule.raycastTarget = false;
        var ruleSize = rule.gameObject.AddComponent<LayoutElement>();
        ruleSize.minWidth = ruleSize.preferredWidth = 140f;
        ruleSize.minHeight = ruleSize.preferredHeight = GameUI.PixelSize;

        objectiveText = Label("Text", objective, "", 34f, TextColor, TextAlignmentOptions.TopLeft);
        objectiveText.characterSpacing = 1f;
        objectiveText.textWrappingMode = TextWrappingModes.Normal;
        objectiveText.fontSharedMaterial = GameUI.Shadow(objectiveText.font);
        objectiveTextSize = objectiveText.gameObject.AddComponent<LayoutElement>();

        objectiveStep = Label("Step", objective, "", 26f, new Color(0.78f, 0.88f, 0.94f), TextAlignmentOptions.TopLeft);
        objectiveStep.textWrappingMode = TextWrappingModes.Normal;
        objectiveStep.fontSharedMaterial = GameUI.Shadow(objectiveStep.font);
        objectiveStepSize = objectiveStep.gameObject.AddComponent<LayoutElement>();
        objectiveStep.gameObject.SetActive(false);
    }

    // Dark glass, a pixel-art pane: a dark rim, a lighter bevel along the top, and the top right corner cut off and
    // edged in amber. Sliced, so it stretches to any size.
    static Sprite objectivePanelSprite;
    static Sprite ObjectivePanelSprite
    {
        get
        {
            if (objectivePanelSprite != null) return objectivePanelSprite;
            const int Size = 20, Cut = 6;
            var rim = new Color32(6, 9, 14, 235);
            var bevel = new Color32(70, 96, 122, 235);
            var edge = new Color32(36, 50, 66, 235);
            var glass = new Color32(10, 16, 26, 200);
            var glassTop = new Color32(16, 24, 38, 210);
            var amber = new Color32(255, 174, 82, 255);
            Texture2D texture = PixelArt.MakeTexture(Size, Size, (x, y) =>
            {
                int fromRight = Size - 1 - x, fromTop = Size - 1 - y;
                int corner = fromRight + fromTop;           // how far in from the cut corner
                if (corner < Cut) return default;
                if (corner == Cut) return amber;
                if (corner == Cut + 1) return rim;
                if (x == 0 || y == 0 || fromRight == 0 || fromTop == 0) return rim;
                if (fromTop == 1) return bevel;
                if (x == 1 || y == 1 || fromRight == 1) return edge;
                return fromTop < 6 ? glassTop : glass;
            });
            return objectivePanelSprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
        }
    }

    // Bottom middle, sized to fit whatever is in it: [accent] [keys] [ACTION / hint].
    void BuildPrompt()
    {
        prompt = NewRect("Prompt", transform);
        prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
        prompt.pivot = new Vector2(0.5f, 0f);
        prompt.anchoredPosition = new Vector2(0f, PromptY);
        Image back = prompt.gameObject.AddComponent<Image>();
        back.sprite = GameUI.SoftPanelSprite;
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        back.raycastTarget = false;
        HorizontalLayoutGroup row = Row(prompt.gameObject, new RectOffset(16, 28, 12, 12), 16f);
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childForceExpandHeight = true;      // so the accent runs the full height, with or without a hint
        var fit = prompt.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;

        // A strip down the left edge, just inside the frame.
        RectTransform accent = NewRect("Accent", prompt);
        promptAccent = accent.gameObject.AddComponent<Image>();
        promptAccent.raycastTarget = false;
        var accentSize = accent.gameObject.AddComponent<LayoutElement>();
        accentSize.minWidth = accentSize.preferredWidth = GameUI.SoftPixelSize * 1.5f;
        accentSize.minHeight = 56f;

        promptKeys = NewRect("Keys", prompt);
        Row(promptKeys.gameObject, new RectOffset(0, 0, 2, 2), 8f).childAlignment = TextAnchor.MiddleCenter;

        RectTransform words = NewRect("Words", prompt);
        var column = words.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(0, 0, 0, 0);
        column.spacing = -2f;
        column.childAlignment = TextAnchor.MiddleLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;

        promptAction = Label("Action", words, "", 40f, TextColor, TextAlignmentOptions.Left);
        promptAction.characterSpacing = 1f;
        promptAction.fontSharedMaterial = GameUI.Shadow(promptAction.font);
        promptHint = Label("Hint", words, "", 24f, DimTextColor, TextAlignmentOptions.Left);
        promptHint.characterSpacing = 1f;
        promptHint.fontSharedMaterial = GameUI.Shadow(promptHint.font);
    }

    // Above the prompt and the letterbox, over a soft dark pool so it reads against the sunlight.
    void BuildCaption()
    {
        RectTransform caption = NewRect("Caption", transform);
        caption.anchorMin = caption.anchorMax = new Vector2(0.5f, 0f);
        caption.pivot = new Vector2(0.5f, 0f);
        caption.anchoredPosition = new Vector2(0f, 200f);
        caption.sizeDelta = new Vector2(1500f, 180f);
        captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
        captionGroup.alpha = 0f;

        RawImage backdrop = Stretch(NewRect("Backdrop", caption), -0.15f, -0.5f, 1.15f, 1.4f).gameObject.AddComponent<RawImage>();
        backdrop.texture = CombatSprites.SoftCircleTexture;
        backdrop.color = BackdropColor;
        backdrop.raycastTarget = false;

        captionSpeaker = Label("Speaker", caption, "", 30f, SpeakerColor, TextAlignmentOptions.Bottom, GameUI.Heading);
        captionSpeaker.characterSpacing = 16f;
        captionSpeaker.fontSharedMaterial = GameUI.Shadow(captionSpeaker.font);
        Stretch(captionSpeaker.rectTransform, 0f, 0.66f, 1f, 1f);
        captionLine = Label("Line", caption, "", 46f, TextColor, TextAlignmentOptions.Top);
        captionLine.fontSharedMaterial = GameUI.Shadow(captionLine.font);
        captionLine.textWrappingMode = TextWrappingModes.Normal;
        Stretch(captionLine.rectTransform, 0f, 0f, 1f, 0.6f);
    }

    RectTransform Bar(string barName, float edge)
    {
        RectTransform bar = NewRect(barName, transform);
        bar.anchorMin = new Vector2(0f, edge);
        bar.anchorMax = new Vector2(1f, edge);
        bar.pivot = new Vector2(0.5f, edge);
        bar.sizeDelta = Vector2.zero;
        Image image = bar.gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        // A soft fade off the bar's inner edge, so it doesn't cut the picture off hard.
        RectTransform edgeRect = NewRect("Soft Edge", bar);
        edgeRect.anchorMin = new Vector2(0f, 1f - edge);
        edgeRect.anchorMax = new Vector2(1f, 1f - edge);
        edgeRect.pivot = new Vector2(0.5f, edge);
        edgeRect.sizeDelta = new Vector2(0f, 140f);
        RawImage fadeEdge = edgeRect.gameObject.AddComponent<RawImage>();
        // Solid where it meets the bar, clear at the far end.
        Texture2D gradient = PixelArt.MakeTexture(1, 32, (x, y) =>
        {
            float along = edge > 0.5f ? y / 31f : 1f - y / 31f;
            return new Color32(255, 255, 255, (byte)(255f * along * along));
        });
        gradient.filterMode = FilterMode.Bilinear;
        fadeEdge.texture = gradient;
        fadeEdge.raycastTarget = false;
        fadeEdge.enabled = false;
        barEdges.Add(fadeEdge);
        return bar;
    }

    static HorizontalLayoutGroup Row(GameObject target, RectOffset padding, float spacing)
    {
        var row = target.AddComponent<HorizontalLayoutGroup>();
        row.padding = padding;
        row.spacing = spacing;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        return row;
    }

    static RectTransform NewRect(string rectName, Transform parent)
    {
        var rect = new GameObject(rectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    // Anchors a rect to part of its parent, given as fractions of the parent.
    static RectTransform Stretch(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // In the HUD's font, or in face if one's given (a heading).
    TextMeshProUGUI Label(string labelName, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment,
                          TMP_FontAsset face = null)
    {
        var label = NewRect(labelName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = face != null ? face : GameUI.Or(font);
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    static IEnumerator FadeGroup(CanvasGroup group, float alpha, float seconds)
    {
        float start = group.alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, alpha, t / seconds);
            yield return null;
        }
        group.alpha = alpha;
    }

    static IEnumerator FadeText(TMP_Text text, float alpha, float seconds)
    {
        float start = text.alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            text.alpha = Mathf.Lerp(start, alpha, t / seconds);
            yield return null;
        }
        text.alpha = alpha;
    }

    static IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }
}
