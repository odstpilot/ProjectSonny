using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "[E] Talk" tag over whatever the player can use right now: a small flat key cap and what it does, on a slim dark
// glass tag (PromptBadge). Objects call Show every frame they're usable and Hide when they're not; if two ask at once,
// the last one wins. Built from code the first time it's needed.
// Whatever has the prompt is the only thing E uses: objects ask Pressed(this) rather than reading the key themselves, so
// a door and a person side by side can't both answer the same press.
public class InteractPrompt : MonoBehaviour
{
    const int SortingOrder = 105;           // over the HUD, under terminal screens

    static InteractPrompt instance;

    private Canvas canvas;
    private PromptBadge badge;
    private Object owner;
    private Object shownFor;
    private Vector3 worldPoint;
    private string text;
    private int pressTaken = -1;

    // text is the key and what it does, two spaces apart, like "E  TUNE IN". Without the two spaces it's all label.
    // font is ignored: every prompt uses the game's own font, so they all match.
    public static void Show(Object owner, Vector3 worldPoint, string text, Font font = null)
    {
        if (instance == null)
        {
            instance = new GameObject("InteractPrompt", typeof(RectTransform)).AddComponent<InteractPrompt>();
            instance.Build();
        }

        instance.owner = owner;
        instance.worldPoint = worldPoint;
        if (instance.text != text)
        {
            instance.text = text;
            instance.badge.SetText(text);
        }
    }

    // True the frame E is pressed, for whatever the prompt was showing over when it was: only that, and only once.
    public static bool Pressed(Object owner, KeyCode key = KeyCode.E)
    {
        if (instance == null || owner == null || instance.shownFor != owner || !Input.GetKeyDown(key)) return false;
        if (instance.pressTaken == Time.frameCount) return false;
        instance.pressTaken = Time.frameCount;
        return true;
    }

    public static void Hide(Object owner)
    {
        if (instance != null && instance.owner == owner) instance.owner = null;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        // Nothing to use while the screen's faded out or a cutscene's playing.
        bool show = owner != null && cam != null && !TutorialHud.ScreenCovered && !ArrivalCutscene.Playing;
        if (!show)
        {
            shownFor = null;
            badge.Hide();
            return;
        }
        // Eases in each time it lands on something new.
        if (shownFor != owner)
        {
            shownFor = owner;
            badge.Restart();
        }
        badge.ShowAt(cam, worldPoint, canvas);
    }

    void Build()
    {
        canvas = PromptBadge.MakeCanvas(gameObject, SortingOrder);
        badge = new PromptBadge(transform, GameUI.Text, 0);
    }
}

// A small tag over something in the level: a flat key cap and a word or two, on slim dark glass with a thin edge. It
// fades in and rises a touch when it appears, sways very slightly while it's up, and fades out when it goes. Used for
// E (InteractPrompt) and Q (InspectTag), so they look alike. It keeps out of the way of the other boxes over the level
// (OverheadLayout): E's stays put and the rest make room for it; Q's rises above E's if they'd meet.
public class PromptBadge : OverheadLayout.IItem
{
    const float Glide = 16f;
    const float FadeIn = 0.16f, FadeOut = 0.12f;
    const float Rise = 8f;                  // canvas units it comes up by as it appears
    const float KeyHeight = 30f;
    const float TextSize = 24f;

    readonly RectTransform root;
    readonly CanvasGroup group;
    readonly Image keyCap;
    readonly LayoutElement keySize;
    readonly TextMeshProUGUI keyLabel;
    readonly TextMeshProUGUI label;
    float shownAt = -10f, alpha;
    bool showing, placedYet;
    readonly int priority;
    Camera cam;
    Canvas canvas;
    Vector3 worldPoint, screenPoint;
    float lift;                             // screen pixels up, where the layout's put it

    public static Canvas MakeCanvas(GameObject holder, int sortingOrder)
    {
        var canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = holder.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public PromptBadge(Transform parent, Color textColor, int priority)
    {
        this.priority = priority;
        root = GameUI.NewRect("Badge", parent);
        root.pivot = new Vector2(0.5f, 0f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        group.alpha = 0f;

        Image panel = GameUI.Sliced("Panel", root, GameUI.SoftPanelSprite, Color.white);
        panel.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        var row = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(8, 14, 6, 6);
        row.spacing = 9f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        keyCap = GameUI.Sliced("Key", panel.transform, GameUI.SoftKeyCapSprite, Color.white);
        keyCap.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        keySize = keyCap.gameObject.AddComponent<LayoutElement>();
        keySize.minHeight = keySize.preferredHeight = KeyHeight;
        keyLabel = GameUI.Label("Key", keyCap.transform, "E", 16f, GameUI.KeyText, TextAlignmentOptions.Center);
        GameUI.Anchor(keyLabel.rectTransform, 0f, 0f, 1f, 1f, 1f, 4f, 1f, 0f);

        label = GameUI.Label("Text", panel.transform, "", TextSize, textColor, TextAlignmentOptions.Left);
        label.characterSpacing = 1f;
        label.fontSharedMaterial = GameUI.Shadow(label.font);

        root.sizeDelta = new Vector2(10f, 10f);
        panelArea = panel.rectTransform;
        OverheadLayout.Add(this);
    }

    readonly RectTransform panelArea;     // the tag itself, for its size

    // "E  OPEN" or just "OPEN": the key, then what it does, in sentence case.
    public void SetText(string text)
    {
        int split = text.IndexOf("  ", System.StringComparison.Ordinal);
        string key = split > 0 && split <= 6 ? text.Substring(0, split).Trim() : "";
        string action = split > 0 && split <= 6 ? text.Substring(split).Trim() : text.Trim();
        keyCap.gameObject.SetActive(key.Length > 0);
        keyLabel.text = key;
        keyLabel.fontSize = key.Length > 1 ? 16f : 22f;
        keySize.minWidth = keySize.preferredWidth = key.Length > 1 ? 16f + key.Length * 11f : KeyHeight;
        label.text = GameUI.Sentence(action);
    }

    public void Restart() => shownAt = Time.unscaledTime;

    // Over a point in the world, easing in.
    public void ShowAt(Camera cam, Vector3 worldPoint, Canvas canvas)
    {
        this.cam = cam;
        this.canvas = canvas;
        this.worldPoint = worldPoint;
        if (!showing)
        {
            showing = true;
            shownAt = Time.unscaledTime;
        }
        float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeIn);
        alpha = Mathf.Max(alpha, t);
        group.alpha = alpha;
        float sway = Mathf.Sin(Time.unscaledTime * 2.2f) * 1.2f;
        screenPoint = cam.WorldToScreenPoint(worldPoint) + new Vector3(0f, sway - (1f - TitleUI.EaseOut(t)) * Rise, 0f);
        MoveTo(screenPoint + new Vector3(0f, lift, 0f));
        root.gameObject.SetActive(true);
    }

    void MoveTo(Vector3 point)
    {
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) root.position = point;
        else if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, point, canvas.worldCamera, out Vector3 onCanvas))
            root.position = onCanvas;
    }

    // --- Keeping out of the way (OverheadLayout) ---

    // World units per screen pixel, for the camera it's shown with.
    float WorldPerPixel => cam != null && cam.orthographic && cam.pixelHeight > 0 ? cam.orthographicSize * 2f / cam.pixelHeight : 0.01f;

    bool OverheadLayout.IItem.Alive => root != null;
    bool OverheadLayout.IItem.Visible => root != null && cam != null && canvas != null && alpha > 0f;
    int OverheadLayout.IItem.Priority => priority;
    float OverheadLayout.IItem.Since => shownAt;

    Rect OverheadLayout.IItem.Wanted
    {
        get
        {
            Vector2 size = panelArea.rect.size * canvas.scaleFactor * WorldPerPixel;
            return new Rect(worldPoint.x - size.x * 0.5f, worldPoint.y, size.x, size.y);
        }
    }

    void OverheadLayout.IItem.Place(Vector2 offset)
    {
        float target = offset.y / WorldPerPixel;
        lift = placedYet ? Mathf.Lerp(lift, target, 1f - Mathf.Exp(-Glide * Time.unscaledDeltaTime)) : target;
        placedYet = true;
        MoveTo(screenPoint + new Vector3(0f, lift, 0f));
    }

    public void Hide()
    {
        showing = false;
        if (alpha <= 0f)
        {
            placedYet = false;
            return;
        }
        alpha = Mathf.MoveTowards(alpha, 0f, Time.unscaledDeltaTime / FadeOut);
        group.alpha = alpha;
    }
}
