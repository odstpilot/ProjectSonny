using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "HOLD [SPACE] TO SKIP" in the bottom right corner while a cutscene can be skipped, with a bar under it that fills as
// Space is held; let go and it drains. Escape skips at once. Space only counts once it's been let go since the prompt
// came up, so a key already held for something else (the install's quick time events) doesn't skip by accident.
// A cutscene calls Show, watches Wanted, and calls Hide. Built from code the first time it's needed.
public class SkipPrompt : MonoBehaviour
{
    const int SortingOrder = 126;           // over the install view (107), the fade, and the death screen (120)
    const float HoldSeconds = 0.7f;
    const float BarWidth = 210f;
    static readonly Color Cyan = new Color(0.25f, 0.95f, 1f);

    static SkipPrompt instance;

    private CanvasGroup group;
    private RectTransform fill;
    private bool shown, armed;
    private float held, shownAt;

    // True once the player has asked to skip, until Hide.
    public static bool Wanted => instance != null && instance.shown && instance.wanted;
    private bool wanted;

    public static void Show()
    {
        if (instance == null) instance = new GameObject("Skip Prompt", typeof(RectTransform)).AddComponent<SkipPrompt>();
        instance.shown = true;
        instance.wanted = false;
        instance.armed = !Input.GetKey(KeyCode.Space);
        instance.held = 0f;
        instance.shownAt = Time.unscaledTime;
    }

    public static void Hide()
    {
        if (instance == null) return;
        instance.shown = false;
        instance.wanted = false;
    }

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = GameUI.NewRect("Skip", transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-48f, 44f);
        root.sizeDelta = new Vector2(BarWidth, 46f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;

        TextMeshProUGUI label = GameUI.Label("Label", root, "HOLD [SPACE] TO SKIP", 26f, Cyan, TextAlignmentOptions.BottomRight, GameUI.Readout);
        GameUI.Anchor(label.rectTransform, 0f, 0f, 1f, 1f, 0f, 10f, 0f, 0f);
        label.characterSpacing = 4f;

        RectTransform track = GameUI.NewRect("Track", root);
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(1f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.sizeDelta = new Vector2(0f, 4f);
        Image back = track.gameObject.AddComponent<Image>();
        back.color = new Color(0f, 0.15f, 0.18f, 0.8f);
        back.raycastTarget = false;

        fill = GameUI.NewRect("Fill", track);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        Image bar = fill.gameObject.AddComponent<Image>();
        bar.color = Cyan;
        bar.raycastTarget = false;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        // In a moment after it's asked for, so it doesn't flash up for a cutscene that's about to end anyway.
        float target = shown && Time.unscaledTime - shownAt > 0.4f ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, dt * 4f);
        if (!shown || wanted) return;

        if (!Input.GetKey(KeyCode.Space)) armed = true;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            wanted = true;
            return;
        }
        held = armed && Input.GetKey(KeyCode.Space) ? held + dt : Mathf.Max(0f, held - dt * 2f);
        fill.anchorMax = new Vector2(Mathf.Clamp01(held / HoldSeconds), 1f);
        if (held >= HoldSeconds) wanted = true;
    }
}
