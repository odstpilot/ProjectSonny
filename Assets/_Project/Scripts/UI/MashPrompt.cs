using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The button-mashing prompt for being grabbed (WallGrab, TutorialDirector's grab): big shaking words over the middle of
// the screen, a key cap that punches on every press, a bar that fills toward breaking free, and red pulsing in at the
// edges. Slack off and it flashes FASTER at them; get squeezed and the screen flushes red. Built from code the first
// time it's shown, so there is nothing to set up.
public class MashPrompt : MonoBehaviour
{
    const int SortingOrder = 106;           // over the HUD and interact prompts, under screens that take over (110)
    const float KeySize = 150f;
    const float BarWidth = 560f, BarHeight = 22f;
    const float SlackSeconds = 0.55f;       // this long without a press, and it's telling them to go faster

    static readonly Color Hot = new Color(1f, 0.28f, 0.2f);
    static readonly Color Bright = new Color(1f, 0.92f, 0.75f);
    static readonly Color Free = new Color(0.5f, 0.95f, 0.6f);

    static MashPrompt instance;

    private CanvasGroup group;
    private RectTransform block, keyRect;
    private TextMeshProUGUI words, keyLabel, nag;
    private RawImage edges;
    private Image fill;
    private float shown, target, pressPunch, squeezeFlash, progress, lastPress;

    public static void Show(string action, string key)
    {
        MashPrompt prompt = Get();
        prompt.words.text = action;
        prompt.keyLabel.text = key;
        prompt.progress = 0f;
        prompt.pressPunch = 0f;
        prompt.lastPress = Time.unscaledTime;
        prompt.target = 1f;
    }

    // A press, and how far toward breaking free they've got now (0 to 1).
    public static void Press(float done)
    {
        if (instance == null) return;
        instance.progress = Mathf.Clamp01(done);
        instance.pressPunch = 1f;
        instance.lastPress = Time.unscaledTime;
    }

    // Squeezed for not fighting it.
    public static void Squeeze()
    {
        if (instance != null) instance.squeezeFlash = 1f;
    }

    public static void Hide()
    {
        if (instance != null) instance.target = 0f;
    }

    static MashPrompt Get()
    {
        if (instance == null)
        {
            instance = new GameObject("MashPrompt", typeof(RectTransform)).AddComponent<MashPrompt>();
            instance.Build();
        }
        return instance;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.MoveTowards(shown, target, dt * (target > shown ? 8f : 3f));
        group.alpha = shown;
        if (shown <= 0f) return;

        float now = Time.unscaledTime;
        pressPunch = Mathf.MoveTowards(pressPunch, 0f, dt * 6f);
        squeezeFlash = Mathf.MoveTowards(squeezeFlash, 0f, dt * 2.5f);
        bool slack = now - lastPress > SlackSeconds && target > 0f;

        // The words shaking, harder the further along (and while they're slacking), and swelling on each press.
        float shake = 5f + 9f * progress + (slack ? 8f : 0f);
        block.anchoredPosition = new Vector2(Random.Range(-shake, shake), 150f + Random.Range(-shake, shake));
        float beat = 1f + 0.06f * Mathf.Sin(now * Mathf.PI * 2f * 4f);
        block.localScale = Vector3.one * (beat + 0.3f * pressPunch);
        block.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1f, 1f) * (2f + 3f * pressPunch));
        words.color = Color.Lerp(Hot, Bright, pressPunch);

        // The key cap squashes on a press.
        keyRect.localScale = new Vector3(1f + 0.25f * pressPunch, 1f - 0.2f * pressPunch, 1f);

        // Slacking: FASTER, flashing.
        nag.enabled = slack && Mathf.Repeat(now * 6f, 1f) < 0.6f;

        // The bar, red going to green as it fills.
        fill.rectTransform.anchorMax = new Vector2(progress, 1f);
        fill.color = Color.Lerp(Hot, Free, progress);

        // Red at the edges, pulsing, and flushing in when it squeezes.
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f * 2.2f);
        edges.color = new Color(0.75f, 0.05f, 0.03f, Mathf.Clamp01(0.35f + 0.25f * pulse + 0.5f * squeezeFlash));
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        edges = GameUI.Fill(GameUI.NewRect("Edges", transform)).gameObject.AddComponent<RawImage>();
        edges.texture = CombatSprites.VignetteTexture;
        edges.raycastTarget = false;

        // Everything that shakes together, a little under the middle of the screen.
        block = GameUI.NewRect("Block", transform);
        block.anchorMin = block.anchorMax = new Vector2(0.5f, 0.5f);
        block.pivot = new Vector2(0.5f, 0.5f);
        block.sizeDelta = new Vector2(900f, 420f);
        block.anchoredPosition = new Vector2(0f, 150f);

        words = GameUI.Label("Words", block, "", 84f, Hot, TextAlignmentOptions.Center, GameUI.Heading);
        words.characterSpacing = 14f;
        words.fontSharedMaterial = GameUI.Shadow(words.font);
        Place(words.rectTransform, new Vector2(0f, 150f), new Vector2(900f, 110f));

        Image cap = GameUI.KeyCap(block, "E", 96f, out keyLabel);
        keyRect = cap.rectTransform;
        Place(keyRect, new Vector2(0f, 10f), new Vector2(KeySize, KeySize));

        nag = GameUI.Label("Faster", block, "FASTER", 44f, Bright, TextAlignmentOptions.Center, GameUI.Heading);
        nag.characterSpacing = 18f;
        nag.fontSharedMaterial = GameUI.Shadow(nag.font);
        Place(nag.rectTransform, new Vector2(0f, -110f), new Vector2(600f, 60f));
        nag.enabled = false;

        Image track = GameUI.Sliced("Bar", block, GameUI.PanelSprite, Color.white);
        Place(track.rectTransform, new Vector2(0f, -170f), new Vector2(BarWidth + 18f, BarHeight + 18f));
        RectTransform inside = GameUI.Fill(GameUI.NewRect("Inside", track.transform), 9f);
        fill = GameUI.NewRect("Fill", inside).gameObject.AddComponent<Image>();
        fill.raycastTarget = false;
        GameUI.Anchor(fill.rectTransform, 0f, 0f, 0f, 1f);
    }

    static void Place(RectTransform rect, Vector2 at, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
    }
}
