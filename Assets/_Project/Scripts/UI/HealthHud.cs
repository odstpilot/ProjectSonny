using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The player's vitals in the top left corner, in the painted HUD frame (Art/UI/Resources/HealthHUD): VITALS, with a
// heartbeat (VitalsLine) in the box beside it, the health bar along the middle, and the number in the HP box. Under it,
// a slimmer stamina bar drawn to match.
//  - Health eases to where it is now. Damage leaves a red trail that hangs a moment and then drains away, the frame
//    shakes and the bar flashes; healing flashes it green. A light runs along the bar every so often. With little left,
//    the bar and the frame pulse red, and the heartbeat races.
//  - Stamina fills amber. While sprinting, stripes run along it; run it dry and it flashes red. Full and unused for a
//    moment, it dims out of the way.
// Also draws the red hurt vignette across the screen.
// Built from code the first time it's needed; PlayerHealthHandler hooks it up to the player's Health and stamina.
public class HealthHud : MonoBehaviour
{
    const int SortingOrder = 80;            // under the tutorial's text (90) and the death screen (120)
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // The painted frame, and where its parts are, in pixels of the frame cut out of the picture.
    const string FrameArt = "HealthHUD";
    static readonly Vector2Int FrameArtSize = new Vector2Int(2048, 768);
    static readonly RectInt FrameCrop = new RectInt(38, 242, 1973, 292);
    static readonly Rect BarSlot = new Rect(494f, 106f, 1246f, 104f);
    static readonly Rect HeartBox = new Rect(66f, 77f, 153f, 147f);
    static readonly Rect HpBox = new Rect(1790f, 134f, 126f, 80f);

    // Where it all goes on the 1920x1080 canvas.
    const float FrameWidth = 600f;
    static float Scale => FrameWidth / FrameCrop.width;
    static readonly Vector2 Corner = new Vector2(28f, -20f);
    const float StaminaGap = 8f;
    const float StaminaHeight = 16f;
    // How far down the screen the readout reaches, from the top; TutorialHud keeps the objective below it.
    public static float Bottom => -Corner.y + FrameCrop.height * Scale + StaminaGap + StaminaHeight;

    static readonly Color Cyan = new Color(0.1f, 0.95f, 1f);
    static readonly Color DeepCyan = new Color(0f, 0.5f, 0.6f);
    static readonly Color Danger = new Color(1f, 0.22f, 0.2f);
    static readonly Color Trail = new Color(1f, 0.42f, 0.32f);
    static readonly Color Heal = new Color(0.55f, 1f, 0.6f);
    static readonly Color Slot = new Color(0.01f, 0.06f, 0.07f, 0.92f);
    static readonly Color Backing = new Color(0f, 0.035f, 0.045f, 0.72f);
    static readonly Color StaminaColor = new Color(1f, 0.72f, 0.24f);
    const float LowHealth = 0.3f;           // at or below this share of health, everything pulses red
    const float LowStamina = 0.25f;
    const float TrailHold = 0.45f;          // how long the damage trail hangs before it drains
    const float SheenEvery = 3.2f;          // seconds between lights along the bar

    static HealthHud instance;

    private Health tracked;
    private PlayerController runner;
    private bool built;

    private RectTransform root, frameRoot;
    private Image frame, frameGlow;
    private RectTransform bar, fill, trail, edge, sheen;
    private RawImage fillImage, trailImage;
    private readonly List<RectTransform> dividers = new List<RectTransform>();
    private TextMeshProUGUI hpText;
    private VitalsLine heartbeat;
    private RawImage vignette;

    private RectTransform staminaRoot, staminaFill;
    private CanvasGroup staminaGroup;
    private RawImage staminaImage, staminaStripes;
    private HudShape staminaEdge;
    private TextMeshProUGUI staminaLabel;

    private float current, max = 1f;
    private float shown = -1f, trailAt, trailHoldUntil;
    private float hitAt = -10f, healAt = -10f, bootAt;
    private float staminaShown = 1f, emptiedAt = -10f, lastUsedAt = -10f;
    private bool wasEmpty;
    private int dividerCount = -1;
    private float hpPunch;

    // Whether the readout is up in the corner, for anything else that wants that corner.
    public static bool CellsShowing => instance != null && instance.root != null && instance.root.gameObject.activeInHierarchy && instance.tracked != null;

    public static HealthHud Get()
    {
        if (instance == null) instance = FindAnyObjectByType<HealthHud>();
        if (instance == null) instance = new GameObject("HealthHud", typeof(RectTransform)).AddComponent<HealthHud>();
        instance.Build();
        return instance;
    }

    void OnDestroy()
    {
        if (tracked != null) tracked.HealthChanged -= OnHealthChanged;
        if (instance == this) instance = null;
    }

    public void Track(Health health)
    {
        if (tracked != null) tracked.HealthChanged -= OnHealthChanged;
        tracked = health;
        shown = -1f;
        if (tracked == null) return;

        tracked.HealthChanged += OnHealthChanged;
        OnHealthChanged(tracked.CurrentHealth, tracked.maxHealth);
    }

    // The player whose stamina the bar under the health shows.
    public void TrackStamina(PlayerController player)
    {
        runner = player;
        if (runner != null) staminaShown = Mathf.Clamp01(runner.stamina / Mathf.Max(1f, runner.maxStamina));
    }

    // Hides the readout but keeps the hurt vignette, for when health doesn't matter (the tutorial).
    public void SetCellsVisible(bool visible)
    {
        if (root != null) root.gameObject.SetActive(visible);
    }

    public void SetVignette(float alpha, Color color)
    {
        if (vignette == null) return;
        vignette.color = new Color(color.r, color.g, color.b, alpha);
        vignette.enabled = alpha > 0.001f;
    }

    void OnHealthChanged(float now, float most)
    {
        float t = Time.unscaledTime;
        if (shown < 0f)
        {
            // Coming online: the bar sweeps up from empty.
            bootAt = t;
            shown = 0f;
            trailAt = 0f;
        }
        else if (now < current)
        {
            hitAt = t;
            trailHoldUntil = t + TrailHold;
            trailAt = Mathf.Max(trailAt, shown);
        }
        else if (now > current) healAt = t;
        if (Mathf.CeilToInt(now) != Mathf.CeilToInt(current)) hpPunch = 1f;

        current = now;
        max = Mathf.Max(0.01f, most);
        if (hpText != null) hpText.text = Mathf.Max(0, Mathf.CeilToInt(current)).ToString("00");
        if (heartbeat != null) heartbeat.SetHealth(current, max);
        LayDividers(Mathf.CeilToInt(max));
    }

    void Update()
    {
        if (!built || root == null) return;
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (tracked != null) AnimateHealth(now, dt);
        AnimateStamina(now, dt);
    }

    // --- Health ---

    void AnimateHealth(float now, float dt)
    {
        float target = Mathf.Clamp01(current / max);
        float boot = Mathf.Clamp01((now - bootAt) / 0.9f);
        float aim = target * TitleUI.EaseOut(boot);
        shown = Mathf.Lerp(Mathf.Max(0f, shown), aim, 1f - Mathf.Exp(-dt * 12f));

        // The trail hangs where the health was, then drains down to it.
        if (trailAt < shown) trailAt = shown;
        else if (now > trailHoldUntil) trailAt = Mathf.MoveTowards(trailAt, shown, dt * 0.7f);
        SetFraction(fill, shown);
        SetFraction(trail, trailAt);
        edge.anchorMin = edge.anchorMax = new Vector2(shown, 0.5f);
        edge.gameObject.SetActive(shown > 0.005f && shown < 0.995f);

        bool low = target <= LowHealth && current > 0f;
        float beat = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f * 1.6f);
        float hit = 1f - Mathf.Clamp01((now - hitAt) / 0.35f);
        float healed = 1f - Mathf.Clamp01((now - healAt) / 0.5f);

        Color bar = low ? Color.Lerp(new Color(0.55f, 0.06f, 0.06f), Danger, beat) : Color.Lerp(DeepCyan, Cyan, 0.88f + 0.12f * beat);
        bar = Color.Lerp(bar, Heal, healed * 0.8f);
        bar = Color.Lerp(bar, Color.white, hit * 0.85f);
        fillImage.color = bar;
        trailImage.color = TitleUI.Fade(Trail, 0.9f);
        hpText.color = low ? Color.Lerp(Danger, Color.white, beat * 0.4f) : Color.Lerp(Cyan, Color.white, 0.35f + hit * 0.65f);

        Color tint = low ? Color.Lerp(Color.white, new Color(1f, 0.45f, 0.45f), beat) : Color.white;
        frame.color = Color.Lerp(tint, new Color(1f, 0.6f, 0.6f), hit);
        frameGlow.color = TitleUI.Fade(low ? Danger : Cyan, 0.16f + 0.1f * Mathf.Sin(now * 2.2f) + hit * 0.4f + (low ? beat * 0.2f : 0f));

        // A light along the bar every so often.
        float sweep = Mathf.Repeat(now - bootAt, SheenEvery) / 0.9f;
        sheen.gameObject.SetActive(sweep <= 1f);
        sheen.anchorMin = sheen.anchorMax = new Vector2(Mathf.Lerp(-0.15f, 1.15f, sweep), 0.5f);

        // A hit knocks the frame about.
        float shake = hit * hit * 7f;
        frameRoot.anchoredPosition = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));

        hpPunch = Mathf.MoveTowards(hpPunch, 0f, dt * 4f);
        hpText.rectTransform.localScale = Vector3.one * (1f + 0.35f * TitleUI.EaseIn(hpPunch));
    }

    static void SetFraction(RectTransform rect, float fraction)
    {
        rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        rect.offsetMax = Vector2.zero;
    }

    // A thin line between each point of health, so the bar still reads as so many hits left.
    void LayDividers(int count)
    {
        if (count == dividerCount || bar == null) return;
        dividerCount = count;
        foreach (RectTransform old in dividers) Destroy(old.gameObject);
        dividers.Clear();
        if (count < 2 || count > 24) return;
        for (int i = 1; i < count; i++)
        {
            RectTransform line = GameUI.NewRect("Divider", bar);
            line.anchorMin = new Vector2(i / (float)count, 0f);
            line.anchorMax = new Vector2(i / (float)count, 1f);
            line.sizeDelta = new Vector2(2f, -4f);
            var image = line.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0.04f, 0.05f, 0.85f);
            image.raycastTarget = false;
            dividers.Add(line);
        }
        edge.SetAsLastSibling();
    }

    // --- Stamina ---

    void AnimateStamina(float now, float dt)
    {
        if (staminaRoot == null) return;
        staminaRoot.gameObject.SetActive(runner != null);
        if (runner == null) return;

        float target = Mathf.Clamp01(runner.stamina / Mathf.Max(1f, runner.maxStamina));
        bool sprinting = runner.IsSprinting && !runner.IsScripted;
        if (sprinting || target < 0.999f) lastUsedAt = now;
        staminaShown = Mathf.Lerp(staminaShown, target, 1f - Mathf.Exp(-dt * 16f));
        SetFraction(staminaFill, staminaShown);

        bool empty = target <= 0.001f;
        if (empty && !wasEmpty) emptiedAt = now;
        wasEmpty = empty;

        float emptied = 1f - Mathf.Clamp01((now - emptiedAt) / 0.8f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f * 2.2f);
        bool low = target <= LowStamina;
        Color color = low ? Color.Lerp(StaminaColor, Danger, 0.35f + 0.5f * pulse) : StaminaColor;
        bool flash = emptied > 0f && Mathf.Repeat(emptied * 6f, 1f) < 0.5f;
        staminaImage.color = flash ? Color.white : color;
        staminaEdge.color = TitleUI.Fade(flash || (low && pulse > 0.5f) ? Danger : Cyan, 0.85f);
        staminaLabel.color = low ? Color.Lerp(Cyan, Danger, pulse) : TitleUI.Fade(Cyan, 0.85f);
        staminaLabel.rectTransform.anchoredPosition = new Vector2(Random.Range(-1f, 1f) * emptied * 3f, 0f);

        // Stripes run along it while sprinting.
        Rect uv = staminaStripes.uvRect;
        uv.x -= dt * (sprinting ? 2.4f : 0.3f);
        staminaStripes.uvRect = uv;
        staminaStripes.color = TitleUI.Fade(Color.white, Mathf.MoveTowards(staminaStripes.color.a, sprinting ? 0.35f : 0.08f, dt * 3f));

        // Full and left alone for a moment, it dims out of the way.
        float wanted = now - lastUsedAt > 2f ? 0.6f : 1f;
        staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, wanted, dt * (wanted > staminaGroup.alpha ? 6f : 1.2f));
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

        BuildVignette();

        root = TopLeft(GameUI.NewRect("Vitals", transform), Corner, new Vector2(FrameWidth, -Bottom + Corner.y));
        frameRoot = TopLeft(GameUI.NewRect("Frame", root), Vector2.zero, new Vector2(FrameWidth, FrameCrop.height * Scale));

        // Dark glass behind the painted lines, which are all that's opaque in the art.
        float s = Scale;
        HudShape backing = Polygon("Backing", frameRoot, Backing, true, Chamfered(new Rect(40f, 24f, 1895f, 240f), 46f, s, true));
        backing.raycastTarget = false;

        bar = TopLeft(GameUI.NewRect("Health Bar", frameRoot), new Vector2(BarSlot.x * s, -BarSlot.y * s), BarSlot.size * s);
        Image slot = GameUI.Fill(GameUI.NewRect("Slot", bar)).gameObject.AddComponent<Image>();
        slot.color = Slot;
        slot.raycastTarget = false;
        trail = Strip("Trail", bar, out trailImage);
        fill = Strip("Fill", bar, out fillImage);
        fill.gameObject.AddComponent<RectMask2D>();
        sheen = GameUI.NewRect("Sheen", fill);
        sheen.sizeDelta = new Vector2(70f, BarSlot.height * s * 2f);
        RawImage sheenImage = sheen.gameObject.AddComponent<RawImage>();
        sheenImage.texture = SheenTexture;
        sheenImage.color = new Color(1f, 1f, 1f, 0.55f);
        sheenImage.material = TitleUI.Additive;
        sheenImage.raycastTarget = false;
        edge = GameUI.NewRect("Leading Edge", bar);
        edge.sizeDelta = new Vector2(3f, BarSlot.height * s - 4f);
        Image edgeImage = edge.gameObject.AddComponent<Image>();
        edgeImage.color = new Color(0.85f, 1f, 1f, 0.95f);
        edgeImage.raycastTarget = false;

        RectTransform heart = TopLeft(GameUI.NewRect("Heartbeat", frameRoot), new Vector2(HeartBox.x * s + 3f, -HeartBox.y * s - 3f), HeartBox.size * s - new Vector2(6f, 6f));
        heart.gameObject.AddComponent<RectMask2D>();
        RectTransform line = GameUI.Fill(GameUI.NewRect("Line", heart));
        heartbeat = line.gameObject.AddComponent<VitalsLine>();
        heartbeat.lineColor = Cyan;
        heartbeat.lineThickness = 2f;

        RectTransform hpBox = TopLeft(GameUI.NewRect("HP", frameRoot), new Vector2(HpBox.x * s, -HpBox.y * s), HpBox.size * s);
        hpText = GameUI.Label("Number", hpBox, "--", 30f, Cyan, TextAlignmentOptions.Center, GameUI.Readout);
        GameUI.Fill(hpText.rectTransform);
        hpText.fontSharedMaterial = TitleUI.GlowMaterial(hpText.font, TitleUI.Fade(Cyan, 0.5f), 0.6f, 0.2f);

        Sprite art = GameUI.ArtPiece(FrameArt, FrameArtSize, FrameCrop);
        frame = Picture("Frame Art", frameRoot, art);
        frameGlow = Picture("Frame Glow", frameRoot, art);
        frameGlow.material = TitleUI.Additive;

        BuildStamina();
    }

    void BuildStamina()
    {
        float s = Scale;
        float top = -(FrameCrop.height * s + StaminaGap);
        staminaRoot = TopLeft(GameUI.NewRect("Stamina", root), new Vector2(0f, top), new Vector2(FrameWidth, StaminaHeight));
        staminaGroup = staminaRoot.gameObject.AddComponent<CanvasGroup>();
        staminaGroup.interactable = staminaGroup.blocksRaycasts = false;

        staminaLabel = GameUI.Label("Label", staminaRoot, "STAMINA", 22f, Cyan, TextAlignmentOptions.MidlineLeft, GameUI.Readout);
        staminaLabel.characterSpacing = 6f;
        RectTransform labelRect = staminaLabel.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(140f, StaminaHeight + 8f);
        RectTransform labelHolder = TopLeft(GameUI.NewRect("Label Holder", staminaRoot), new Vector2(HeartBox.x * s, 0f), new Vector2(140f, StaminaHeight));
        labelRect.SetParent(labelHolder, false);
        labelRect.anchoredPosition = Vector2.zero;

        float left = BarSlot.x * s, width = BarSlot.width * s;
        RectTransform slotRect = TopLeft(GameUI.NewRect("Bar", staminaRoot), new Vector2(left, 0f), new Vector2(width, StaminaHeight));
        Image slot = GameUI.Fill(GameUI.NewRect("Slot", slotRect)).gameObject.AddComponent<Image>();
        slot.color = Slot;
        slot.raycastTarget = false;
        staminaFill = Strip("Fill", slotRect, out staminaImage);
        staminaImage.color = StaminaColor;
        staminaFill.gameObject.AddComponent<RectMask2D>();
        // Stripes over the fill, sized to the whole bar so they don't stretch as it empties.
        RectTransform stripes = TopLeft(GameUI.NewRect("Stripes", staminaFill), Vector2.zero, new Vector2(width, StaminaHeight));
        staminaStripes = stripes.gameObject.AddComponent<RawImage>();
        staminaStripes.texture = StripeTexture;
        staminaStripes.uvRect = new Rect(0f, 0f, width / StaminaHeight * 0.5f, 1f);
        staminaStripes.color = new Color(1f, 1f, 1f, 0.08f);
        staminaStripes.raycastTarget = false;

        // An outline cut at two corners, like the painted frame above it.
        var edgePoints = Chamfered(new Rect(-3f, -3f, width + 6f, StaminaHeight + 6f), 7f, 1f);
        staminaEdge = Polygon("Edge", slotRect, Cyan, false, edgePoints);
        staminaEdge.thickness = 2f;
        staminaEdge.feather = 3f;
        // Three notches off its end, after the frame's.
        for (int i = 0; i < 3; i++)
        {
            var notch = Polygon("Notch", slotRect, TitleUI.Fade(Cyan, 0.7f), true,
                TitleUI.Slant(new Vector2(width + 16f + i * 11f, -StaminaHeight * 0.5f), 6f, StaminaHeight - 4f, 3f));
            notch.raycastTarget = false;
        }
    }

    // A strip anchored along the left of its parent, as wide as SetFraction makes it, filled with the bar's shading.
    static RectTransform Strip(string stripName, Transform parent, out RawImage image)
    {
        RectTransform rect = GameUI.NewRect(stripName, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(0f, 2f);
        rect.offsetMax = new Vector2(0f, -2f);
        image = rect.gameObject.AddComponent<RawImage>();
        image.texture = ShadeTexture;
        image.raycastTarget = false;
        return rect;
    }

    static RectTransform TopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    Image Picture(string pictureName, RectTransform parent, Sprite sprite)
    {
        Image image = GameUI.Fill(GameUI.NewRect(pictureName, parent)).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    // A shape whose points are in its parent's top-left space (y down is negative).
    static HudShape Polygon(string shapeName, RectTransform parent, Color color, bool filled, Vector2[] points)
    {
        RectTransform rect = TopLeft(GameUI.NewRect(shapeName, parent), Vector2.zero, parent.rect.size);
        HudShape shape = rect.gameObject.AddComponent<HudShape>();
        shape.raycastTarget = false;
        shape.color = color;
        shape.filled = filled;
        shape.SetPoints(points);
        return shape;
    }

    // A rect (x right, y down from the top left) with its top-left and bottom-right corners cut (or all four), scaled,
    // in canvas units.
    static Vector2[] Chamfered(Rect r, float cut, float scale, bool allCorners = false)
    {
        Vector2 P(float x, float y) => new Vector2(x * scale, -y * scale);
        float other = allCorners ? cut : 0f;
        var points = new List<Vector2> { P(r.xMin + cut, r.yMin), P(r.xMax - other, r.yMin) };
        if (allCorners) points.Add(P(r.xMax, r.yMin + other));
        points.Add(P(r.xMax, r.yMax - cut));
        points.Add(P(r.xMax - cut, r.yMax));
        points.Add(P(r.xMin + other, r.yMax));
        if (allCorners) points.Add(P(r.xMin, r.yMax - other));
        points.Add(P(r.xMin, r.yMin + cut));
        return points.ToArray();
    }

    void BuildVignette()
    {
        var vignetteRect = new GameObject("Hurt Vignette", typeof(RectTransform)).GetComponent<RectTransform>();
        vignetteRect.SetParent(transform, false);
        vignetteRect.SetAsFirstSibling();
        GameUI.Fill(vignetteRect);
        vignette = vignetteRect.gameObject.AddComponent<RawImage>();
        vignette.texture = CombatSprites.VignetteTexture;
        vignette.raycastTarget = false;
        vignette.enabled = false;
    }

    // --- Textures made in code ---

    static Texture2D shade, sheenTexture, stripes;

    // Lighter along the top, darker toward the bottom, with a bright line at the very top: a lit tube of light.
    static Texture2D ShadeTexture => shade != null ? shade : (shade = MakeTexture(1, 32, (x, y) =>
    {
        float v = y / 31f;
        float level = y >= 29 ? 1f : Mathf.Lerp(0.62f, 0.97f, v * v);
        return new Color(level, level, level, 1f);
    }, TextureWrapMode.Clamp));

    // A soft upright band, bright in the middle, for the light that runs along the bar.
    static Texture2D SheenTexture => sheenTexture != null ? sheenTexture : (sheenTexture = MakeTexture(32, 1, (x, y) =>
    {
        float d = Mathf.Abs(x - 15.5f) / 15.5f;
        return new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2f));
    }, TextureWrapMode.Clamp));

    // Diagonal stripes that tile, for the stamina bar while sprinting.
    static Texture2D StripeTexture => stripes != null ? stripes : (stripes = MakeTexture(16, 16, (x, y) =>
        Mathf.Repeat(x + y, 16f) < 7f ? Color.white : new Color(1f, 1f, 1f, 0f), TextureWrapMode.Repeat));

    static Texture2D MakeTexture(int width, int height, System.Func<int, int, Color> color, TextureWrapMode wrap)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, color(x, y));
        texture.Apply();
        return texture;
    }
}
