using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// On-screen text for the tutorial and its cutscenes, built from code the first time something asks for it, so there's
// no canvas to set up:
//  - a control prompt along the bottom: key caps and what they do, turning green once the player has done it
//  - the current objective in the top right corner
//  - captions for a voice over the station speakers, typed out a letter at a time
//  - letterbox bars, a fade to black, and title cards
// Everything runs on unscaled time, so hit stop can't stall it. One per scene: TutorialHud.Get().
public class TutorialHud : MonoBehaviour
{
    const int SortingOrder = 90;            // under LockerView (100), so the view from inside a locker covers it
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const float LetterboxHeight = 130f;
    const float LettersPerSecond = 40f;
    const float PromptFadeTime = 0.25f;
    const float DoneHoldTime = 0.9f;

    static readonly Color PanelColor = new Color(0.02f, 0.03f, 0.04f, 0.78f);
    static readonly Color KeyColor = new Color(0.88f, 0.9f, 0.93f);
    static readonly Color KeyTextColor = new Color(0.07f, 0.08f, 0.09f);
    static readonly Color DoneColor = new Color(0.45f, 1f, 0.55f);
    static readonly Color DimTextColor = new Color(0.7f, 0.74f, 0.8f);
    static readonly Color SpeakerColor = new Color(1f, 0.32f, 0.26f);

    static TutorialHud instance;

    [Tooltip("Font for all the text. Leave empty for TextMesh Pro's default.")]
    public TMP_FontAsset font;

    public bool PromptShowing { get; private set; }
    // True while a finished prompt is still showing its green tick.
    public bool PromptCompleting { get; private set; }

    private bool built;
    private CanvasGroup promptGroup;
    private RectTransform promptRow;
    private readonly List<Graphic> promptKeys = new List<Graphic>();
    private TextMeshProUGUI promptAction;
    private Coroutine promptRoutine;

    private CanvasGroup objectiveGroup;
    private TextMeshProUGUI objectiveText;
    private Coroutine objectiveRoutine;

    private CanvasGroup captionGroup;
    private TextMeshProUGUI captionSpeaker;
    private TextMeshProUGUI captionLine;

    private RectTransform letterboxTop;
    private RectTransform letterboxBottom;
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
        StopPromptRoutine();

        var oldChildren = new List<Transform>();
        foreach (Transform child in promptRow) oldChildren.Add(child);
        foreach (Transform child in oldChildren)
        {
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        promptKeys.Clear();
        foreach (string key in keys)
        {
            RectTransform cap = NewRect("Key", promptRow);
            Image capBack = cap.gameObject.AddComponent<Image>();
            capBack.color = KeyColor;
            capBack.raycastTarget = false;
            HorizontalLayoutGroup capLayout = Row(cap.gameObject, new RectOffset(14, 14, 4, 4), 0f);
            capLayout.childAlignment = TextAnchor.MiddleCenter;
            var capSize = cap.gameObject.AddComponent<LayoutElement>();
            capSize.minWidth = 50f;
            capSize.minHeight = 50f;
            Label("Text", cap, key, 26f, KeyTextColor, TextAlignmentOptions.Center);
            promptKeys.Add(capBack);
        }
        promptAction = Label("Action", promptRow, action, 32f, Color.white, TextAlignmentOptions.Left);

        PromptShowing = true;
        promptRow.localScale = Vector3.one;
        promptRoutine = StartCoroutine(FadeGroup(promptGroup, 1f, PromptFadeTime));
    }

    // Ticks the prompt off: it turns green, pops, and fades away.
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
        promptRoutine = StartCoroutine(FadeGroup(promptGroup, 0f, PromptFadeTime));
    }

    IEnumerator CompleteRoutine()
    {
        if (promptAction != null) promptAction.color = DoneColor;
        foreach (Graphic key in promptKeys)
            if (key != null) key.color = DoneColor;

        const float popTime = 0.15f;
        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            promptRow.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t / popTime * Mathf.PI));
            yield return null;
        }
        promptRow.localScale = Vector3.one;

        yield return WaitUnscaled(DoneHoldTime);
        yield return FadeGroup(promptGroup, 0f, PromptFadeTime);
        PromptCompleting = false;
        promptRoutine = null;
    }

    void StopPromptRoutine()
    {
        if (promptRoutine != null) StopCoroutine(promptRoutine);
        promptRoutine = null;
        PromptCompleting = false;
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
        captionSpeaker.text = speaker;
        captionLine.text = line;
        captionLine.maxVisibleCharacters = 0;
        captionGroup.alpha = 1f;

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
            titleText.text = line;
            yield return FadeText(titleText, 1f, 0.6f);
            yield return WaitUnscaled(holdSeconds);
            yield return FadeText(titleText, 0f, 0.6f);
        }
    }

    void SetLetterbox(float height)
    {
        letterboxTop.sizeDelta = new Vector2(0f, height);
        letterboxBottom.sizeDelta = new Vector2(0f, height);
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

        // Objective, top right, clear of the health readout in the top left.
        RectTransform objective = NewRect("Objective", transform);
        objective.anchorMin = objective.anchorMax = objective.pivot = new Vector2(1f, 1f);
        objective.anchoredPosition = new Vector2(-48f, -40f);
        objective.sizeDelta = new Vector2(700f, 90f);
        objectiveGroup = objective.gameObject.AddComponent<CanvasGroup>();
        objectiveGroup.alpha = 0f;
        TextMeshProUGUI objectiveLabel = Label("Label", objective, "OBJECTIVE", 20f, DimTextColor, TextAlignmentOptions.TopRight);
        objectiveLabel.characterSpacing = 8f;
        Stretch(objectiveLabel.rectTransform, 0f, 0.6f, 1f, 1f);
        objectiveText = Label("Text", objective, "", 30f, Color.white, TextAlignmentOptions.TopRight);
        Stretch(objectiveText.rectTransform, 0f, 0f, 1f, 0.62f);

        // Control prompt, bottom middle, sized to fit whatever is in it.
        promptRow = NewRect("Prompt", transform);
        promptRow.anchorMin = promptRow.anchorMax = new Vector2(0.5f, 0f);
        promptRow.pivot = new Vector2(0.5f, 0f);
        promptRow.anchoredPosition = new Vector2(0f, 90f);
        Image promptBack = promptRow.gameObject.AddComponent<Image>();
        promptBack.color = PanelColor;
        promptBack.raycastTarget = false;
        Row(promptRow.gameObject, new RectOffset(26, 30, 14, 14), 12f).childAlignment = TextAnchor.MiddleCenter;
        var fit = promptRow.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        promptGroup = promptRow.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;

        // Captions, above the prompt and the letterbox.
        RectTransform caption = NewRect("Caption", transform);
        caption.anchorMin = caption.anchorMax = new Vector2(0.5f, 0f);
        caption.pivot = new Vector2(0.5f, 0f);
        caption.anchoredPosition = new Vector2(0f, 220f);
        caption.sizeDelta = new Vector2(1400f, 130f);
        captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
        captionGroup.alpha = 0f;
        captionSpeaker = Label("Speaker", caption, "", 24f, SpeakerColor, TextAlignmentOptions.Bottom);
        captionSpeaker.characterSpacing = 10f;
        Stretch(captionSpeaker.rectTransform, 0f, 0.62f, 1f, 1f);
        captionLine = Label("Line", caption, "", 36f, Color.white, TextAlignmentOptions.Top);
        captionLine.textWrappingMode = TextWrappingModes.Normal;
        Stretch(captionLine.rectTransform, 0f, 0f, 1f, 0.6f);

        letterboxTop = Bar("Letterbox Top", 1f);
        letterboxBottom = Bar("Letterbox Bottom", 0f);

        // Fade and title cards, over everything else.
        fade = Stretch(NewRect("Fade", transform), 0f, 0f, 1f, 1f).gameObject.AddComponent<Image>();
        fade.raycastTarget = false;
        SetFade(0f);
        titleText = Label("Title", transform, "", 46f, Color.white, TextAlignmentOptions.Center);
        titleText.characterSpacing = 6f;
        titleText.alpha = 0f;
        Stretch(titleText.rectTransform, 0.1f, 0f, 0.9f, 1f);
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
