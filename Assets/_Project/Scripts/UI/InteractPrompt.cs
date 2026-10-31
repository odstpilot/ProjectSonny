using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "[e] use" tag over things
public class InteractPrompt : MonoBehaviour
{
    const int SortingOrder = 105;

    static InteractPrompt instance;

    private Canvas canvas;
    private PromptBadge badge;
    private Object owner;
    private Object shownFor;
    private Vector3 worldPoint;
    private string text;
    private int pressTaken = -1;
    private int claimedFrame = -1;
    private float claimedDistance;
    private Transform player;

    public static void Show(Object owner, Vector3 worldPoint, string text, Font font = null)
    {
        if (instance == null)
        {
            instance = new GameObject("InteractPrompt", typeof(RectTransform)).AddComponent<InteractPrompt>();
            instance.Build();
        }

        float distance = instance.DistanceTo(owner);
        if (instance.claimedFrame == Time.frameCount && instance.owner != owner && distance > instance.claimedDistance) return;
        instance.claimedFrame = Time.frameCount;
        instance.claimedDistance = distance;

        instance.owner = owner;
        instance.worldPoint = worldPoint;
        if (instance.text != text)
        {
            instance.text = text;
            instance.badge.SetText(text);
        }
    }

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

    float DistanceTo(Object asker)
    {
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player == null || !(asker is Component component)) return 0f;
        return Vector2.Distance(player.position, component.transform.position);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        bool show = owner != null && cam != null && !TutorialHud.ScreenCovered && !ArrivalCutscene.Playing;
        if (!show)
        {
            shownFor = null;
            badge.Hide();
            return;
        }
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

public class PromptBadge : OverheadLayout.IItem
{
    const float Glide = 16f;
    const float FadeIn = 0.16f, FadeOut = 0.12f;
    const float Rise = 8f;
    const float KeyHeight = 42f;
    const float TextSize = 30f;

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
    float lift;

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
        row.padding = new RectOffset(10, 18, 8, 8);
        row.spacing = 12f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        keyCap = GameUI.Sliced("Key", panel.transform, GameUI.SoftKeyCapSprite, Color.white);
        keyCap.pixelsPerUnitMultiplier = 1f / GameUI.SoftPixelSize;
        keySize = keyCap.gameObject.AddComponent<LayoutElement>();
        keySize.minHeight = keySize.preferredHeight = KeyHeight;
        keyLabel = GameUI.Label("Key", keyCap.transform, "E", 30f, GameUI.KeyText, TextAlignmentOptions.Center);
        GameUI.Anchor(keyLabel.rectTransform, 0f, 0f, 1f, 1f, 1f, 4f, 1f, 0f);

        label = GameUI.Label("Text", panel.transform, "", TextSize, textColor, TextAlignmentOptions.Left);
        label.characterSpacing = 1f;
        label.fontSharedMaterial = GameUI.Shadow(label.font);

        root.sizeDelta = new Vector2(10f, 10f);
        panelArea = panel.rectTransform;
        OverheadLayout.Add(this);
    }

    readonly RectTransform panelArea;

    public void SetText(string text)
    {
        int split = text.IndexOf("  ", System.StringComparison.Ordinal);
        string key = split > 0 && split <= 6 ? text.Substring(0, split).Trim() : "";
        string action = split > 0 && split <= 6 ? text.Substring(split).Trim() : text.Trim();
        keyCap.gameObject.SetActive(key.Length > 0);
        keyLabel.text = key;
        keyLabel.fontSize = key.Length > 1 ? 22f : 30f;
        keySize.minWidth = keySize.preferredWidth = key.Length > 1 ? 22f + key.Length * 15f : KeyHeight;
        label.text = GameUI.Sentence(action);
    }

    public void Restart() => shownAt = Time.unscaledTime;

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
        screenPoint = CameraZoom.ToScreen(cam, worldPoint) + new Vector3(0f, sway - (1f - TitleUI.EaseOut(t)) * Rise, 0f);
        MoveTo(screenPoint + new Vector3(0f, lift, 0f));
        root.gameObject.SetActive(true);
    }

    void MoveTo(Vector3 point)
    {
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) root.position = point;
        else if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, point, canvas.worldCamera, out Vector3 onCanvas))
            root.position = onCanvas;
    }

    float WorldPerPixel => cam != null && cam.orthographic && cam.pixelHeight > 0
        ? cam.orthographicSize * 2f / cam.pixelHeight * (cam.TryGetComponent(out CameraZoom zoom) ? zoom.Factor : 1f) : 0.01f;

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
