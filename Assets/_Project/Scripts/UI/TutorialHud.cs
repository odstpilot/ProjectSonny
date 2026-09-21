using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// On-screen text for the tutorial and its cutscenes, built from code the first time something asks for it, so there's
// no canvas to set up:
//  - a control prompt along the bottom: outlined key caps and what they do, with an optional line of detail under it.
//    It slides up into place, and once the player has done it the accent turns green and it drops away.
//  - the current objective in the top left corner
//  - captions for a voice over the station speakers, typed out a letter at a time over a soft dark backdrop
//  - letterbox bars, a fade to black, and title cards
// Everything runs on unscaled time, so hit stop can't stall it. One per scene: TutorialHud.Get().
public class TutorialHud : MonoBehaviour
{
    const int SortingOrder = 90;            // under LockerView (100), so the view from inside a locker covers it
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const float LetterboxHeight = 130f;
    const float LettersPerSecond = 40f;
    const float PromptFadeTime = 0.25f;
    const float PromptY = 72f;              // the prompt's resting height off the bottom of the screen
    const float PromptSlide = 14f;          // how far it slides in and out
    const float DoneHoldTime = 0.8f;
    const float Margin = 56f;

    static readonly Color PanelColor = new Color(0.03f, 0.035f, 0.045f, 0.62f);
    static readonly Color AccentColor = new Color(1f, 0.66f, 0.3f);
    static readonly Color KeyEdgeColor = new Color(0.9f, 0.9f, 0.88f, 0.9f);
    static readonly Color KeyFillColor = new Color(0.08f, 0.085f, 0.1f, 0.9f);
    static readonly Color TextColor = new Color(0.96f, 0.95f, 0.92f);
    static readonly Color DimTextColor = new Color(0.72f, 0.71f, 0.68f);
    static readonly Color DoneColor = new Color(0.5f, 0.95f, 0.6f);
    static readonly Color SpeakerColor = new Color(1f, 0.34f, 0.28f);
    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.55f);

    static TutorialHud instance;

    [Tooltip("Font for all the text. Leave empty for TextMesh Pro's default.")]
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
    private readonly List<Graphic> keyEdges = new List<Graphic>();
    private readonly List<TextMeshProUGUI> keyLabels = new List<TextMeshProUGUI>();
    private Coroutine promptRoutine;

    private CanvasGroup objectiveGroup;
    private TextMeshProUGUI objectiveText;
    private Coroutine objectiveRoutine;

    private CanvasGroup captionGroup;
    private TextMeshProUGUI captionSpeaker;
    private TextMeshProUGUI captionLine;

    private RectTransform letterboxTop;
    private RectTransform letterboxBottom;
    private readonly List<RawImage> barEdges = new List<RawImage>();
    private Image fade;
    private TextMeshProUGUI titleText;

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
        keyEdges.Clear();
        keyLabels.Clear();
        foreach (string key in keys) AddKey(key);
        promptKeys.gameObject.SetActive(keys.Length > 0);

        promptAction.text = action.ToUpperInvariant();
        promptHint.text = hint ?? "";
        promptHint.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        SetPromptColor(AccentColor, KeyEdgeColor, TextColor);

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

    void SetPromptColor(Color accent, Color keyEdge, Color text)
    {
        promptAccent.color = accent;
        foreach (Graphic edge in keyEdges)
            if (edge != null) edge.color = keyEdge;
        foreach (TextMeshProUGUI label in keyLabels)
            if (label != null) label.color = text;
        promptAction.color = text;
    }

    void StopPromptRoutine()
    {
        if (promptRoutine != null) StopCoroutine(promptRoutine);
        promptRoutine = null;
        PromptCompleting = false;
    }

    // An outlined key cap: a light edge around a dark face, as wide as its label needs.
    void AddKey(string key)
    {
        RectTransform edge = NewRect("Key", promptKeys);
        Image edgeImage = edge.gameObject.AddComponent<Image>();
        edgeImage.color = KeyEdgeColor;
        edgeImage.raycastTarget = false;
        HorizontalLayoutGroup edgeLayout = Row(edge.gameObject, new RectOffset(2, 2, 2, 2), 0f);
        edgeLayout.childForceExpandWidth = edgeLayout.childForceExpandHeight = true;
        var capSize = edge.gameObject.AddComponent<LayoutElement>();
        capSize.minWidth = 42f;
        capSize.minHeight = 42f;

        RectTransform face = NewRect("Face", edge);
        Image faceImage = face.gameObject.AddComponent<Image>();
        faceImage.color = KeyFillColor;
        faceImage.raycastTarget = false;
        Row(face.gameObject, new RectOffset(11, 11, 4, 4), 0f).childAlignment = TextAnchor.MiddleCenter;

        TextMeshProUGUI label = Label("Text", face, key, key.Length > 1 ? 16f : 21f, TextColor, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = key.Length > 1 ? 4f : 0f;

        keyEdges.Add(edgeImage);
        keyLabels.Add(label);
    }

    // --- Objective ---

    // Empty hides it.
    public void SetObjective(string text)
    {
        if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
        objectiveRoutine = StartCoroutine(ChangeObjective(text));
    }

    IEnumerator ChangeObjective(string text)
    {
        if (objectiveGroup.alpha > 0f) yield return FadeGroup(objectiveGroup, 0f, 0.2f);
        objectiveText.text = text;
        if (!string.IsNullOrEmpty(text)) yield return FadeGroup(objectiveGroup, 1f, 0.4f);
        objectiveRoutine = null;
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

        letterboxTop = Bar("Letterbox Top", 1f);
        letterboxBottom = Bar("Letterbox Bottom", 0f);

        // Fade and title cards, over everything else.
        fade = Stretch(NewRect("Fade", transform), 0f, 0f, 1f, 1f).gameObject.AddComponent<Image>();
        fade.raycastTarget = false;
        SetFade(0f);
        titleText = Label("Title", transform, "", 34f, TextColor, TextAlignmentOptions.Center);
        titleText.characterSpacing = 14f;
        titleText.alpha = 0f;
        Stretch(titleText.rectTransform, 0.1f, 0f, 0.9f, 1f);
    }

    // Top left, the corner the health readout would use; the tutorial hides that, since the player can't die in it.
    // A short amber rule, a small label, and the objective itself.
    void BuildObjective()
    {
        RectTransform objective = NewRect("Objective", transform);
        objective.anchorMin = objective.anchorMax = objective.pivot = new Vector2(0f, 1f);
        objective.anchoredPosition = new Vector2(Margin, -Margin * 0.8f);
        objective.sizeDelta = new Vector2(760f, 70f);
        objectiveGroup = objective.gameObject.AddComponent<CanvasGroup>();
        objectiveGroup.alpha = 0f;

        RectTransform rule = NewRect("Rule", objective);
        rule.anchorMin = rule.anchorMax = rule.pivot = new Vector2(0f, 1f);
        rule.anchoredPosition = Vector2.zero;
        rule.sizeDelta = new Vector2(3f, 62f);
        Image ruleImage = rule.gameObject.AddComponent<Image>();
        ruleImage.color = AccentColor;
        ruleImage.raycastTarget = false;

        TextMeshProUGUI label = Label("Label", objective, "OBJECTIVE", 15f, DimTextColor, TextAlignmentOptions.TopLeft);
        label.characterSpacing = 12f;
        Stretch(label.rectTransform, 0f, 0.62f, 1f, 1f).offsetMin = new Vector2(18f, 0f);
        objectiveText = Label("Text", objective, "", 26f, TextColor, TextAlignmentOptions.TopLeft);
        objectiveText.characterSpacing = 1f;
        Stretch(objectiveText.rectTransform, 0f, 0f, 1f, 0.62f).offsetMin = new Vector2(18f, 0f);
    }

    // Bottom middle, sized to fit whatever is in it: [accent] [keys] [ACTION / hint].
    void BuildPrompt()
    {
        prompt = NewRect("Prompt", transform);
        prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
        prompt.pivot = new Vector2(0.5f, 0f);
        prompt.anchoredPosition = new Vector2(0f, PromptY);
        Image back = prompt.gameObject.AddComponent<Image>();
        back.color = PanelColor;
        back.raycastTarget = false;
        HorizontalLayoutGroup row = Row(prompt.gameObject, new RectOffset(0, 28, 0, 0), 20f);
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childForceExpandHeight = true;      // so the accent runs the full height, with or without a hint
        var fit = prompt.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;

        // A strip down the left edge, the full height of the prompt.
        RectTransform accent = NewRect("Accent", prompt);
        promptAccent = accent.gameObject.AddComponent<Image>();
        promptAccent.raycastTarget = false;
        var accentSize = accent.gameObject.AddComponent<LayoutElement>();
        accentSize.minWidth = accentSize.preferredWidth = 4f;
        accentSize.minHeight = 66f;

        promptKeys = NewRect("Keys", prompt);
        Row(promptKeys.gameObject, new RectOffset(0, 0, 12, 12), 6f).childAlignment = TextAnchor.MiddleCenter;

        RectTransform words = NewRect("Words", prompt);
        var column = words.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(0, 0, 12, 12);
        column.spacing = 2f;
        column.childAlignment = TextAnchor.MiddleLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;

        promptAction = Label("Action", words, "", 24f, TextColor, TextAlignmentOptions.Left);
        promptAction.characterSpacing = 6f;
        promptHint = Label("Hint", words, "", 18f, DimTextColor, TextAlignmentOptions.Left);
        promptHint.characterSpacing = 1f;
    }

    // Above the prompt and the letterbox, over a soft dark pool so it reads against the sunlight.
    void BuildCaption()
    {
        RectTransform caption = NewRect("Caption", transform);
        caption.anchorMin = caption.anchorMax = new Vector2(0.5f, 0f);
        caption.pivot = new Vector2(0.5f, 0f);
        caption.anchoredPosition = new Vector2(0f, 200f);
        caption.sizeDelta = new Vector2(1300f, 120f);
        captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
        captionGroup.alpha = 0f;

        RawImage backdrop = Stretch(NewRect("Backdrop", caption), -0.15f, -0.5f, 1.15f, 1.4f).gameObject.AddComponent<RawImage>();
        backdrop.texture = CombatSprites.SoftCircleTexture;
        backdrop.color = BackdropColor;
        backdrop.raycastTarget = false;

        captionSpeaker = Label("Speaker", caption, "", 17f, SpeakerColor, TextAlignmentOptions.Bottom);
        captionSpeaker.characterSpacing = 16f;
        Stretch(captionSpeaker.rectTransform, 0f, 0.66f, 1f, 1f);
        captionLine = Label("Line", caption, "", 32f, TextColor, TextAlignmentOptions.Top);
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

    TextMeshProUGUI Label(string labelName, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var label = NewRect(labelName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
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
