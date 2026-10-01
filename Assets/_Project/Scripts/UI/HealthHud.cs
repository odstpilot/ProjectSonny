using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The player's health in the top left corner. Either drives health segment Images placed by hand in the scene
// (customSegments), or builds its own: a row of cells, one for each point of health, on a small framed panel (GameUI's)
// under a VITALS heading. A lost cell flashes and goes dark, and when only one is left it pulses. Also draws the red
// hurt vignette across the screen.
// Built from code the first time it's needed; PlayerHealthHandler hooks it up to the player's Health.
public class HealthHud : MonoBehaviour
{
    const int SortingOrder = 80;            // under the tutorial's text (90) and the death screen (120)
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const float CellWidth = 30f;
    const float CellHeight = 18f;
    const float CellGap = 6f;
    const float Padding = 18f;
    const float HeadingHeight = 26f;
    // How far down the screen the panel reaches, from its top; TutorialHud keeps the objective below it.
    public const float Height = Padding * 2f + HeadingHeight + CellHeight;
    const float LoseTime = 0.45f;

    // Cyan to match the new HUD art.
    static readonly Color Full = new Color(0f, 0.95f, 1f);
    static readonly Color LastCell = new Color(0f, 0.45f, 0.5f);
    static readonly Color Empty = new Color(0.03f, 0.15f, 0.17f, 0.8f);
    static readonly Color Flash = Color.white;
    static readonly Color Frame = new Color(0.02f, 0.02f, 0.03f, 0.9f);
    static readonly Color Shine = new Color(1f, 1f, 1f, 0.3f);

    static HealthHud instance;

    [Header("Custom Health HUD")]
    [Tooltip("Assign the 10 health segment Images here. Leave empty to use the generated HUD.")]
    public Image[] customSegments;
    public TMP_Text hpText;
    public VitalsLine vitalsLine;

    private Health tracked;
    private RectTransform row;
    private RectTransform panel;
    private readonly List<Image> cells = new List<Image>();
    private readonly HashSet<Image> losing = new HashSet<Image>();
    private RawImage vignette;
    private int lit = -1;
    private bool built;

    bool UsesCustom => customSegments != null && customSegments.Length > 0;

    // Whether the cells are up in the corner, for anything else that wants that corner.
    public static bool CellsShowing => instance != null && instance.row != null && instance.row.gameObject.activeInHierarchy && instance.lit >= 0;

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
        lit = -1;
        if (tracked == null) return;

        tracked.HealthChanged += OnHealthChanged;
        OnHealthChanged(tracked.CurrentHealth, tracked.maxHealth);
    }

    // Hides the cells but keeps the hurt vignette, for when health doesn't matter (the tutorial).
    public void SetCellsVisible(bool visible)
    {
        if (row != null) row.gameObject.SetActive(visible);
        if (customSegments == null) return;
        foreach (Image segment in customSegments)
            if (segment != null) segment.gameObject.SetActive(visible);
    }

    public void SetVignette(float alpha, Color color)
    {
        if (vignette == null) return;
        vignette.color = new Color(color.r, color.g, color.b, alpha);
        vignette.enabled = alpha > 0.001f;
    }

    void OnHealthChanged(float current, float max)
    {
        if (hpText != null) hpText.text = Mathf.CeilToInt(current).ToString();
        if (vitalsLine != null) vitalsLine.SetHealth(current, max);

        if (UsesCustom)
        {
            // The hand-placed segments each stand for a share of max health, however many there are.
            int segments = customSegments.Length;
            float share = max > 0f ? current / max : 0f;
            Light(customSegments, segments, Mathf.Clamp(Mathf.CeilToInt(share * segments), 0, segments));
            return;
        }

        int count = Mathf.Max(1, Mathf.CeilToInt(max));
        while (cells.Count < count) cells.Add(NewCell(cells.Count));
        for (int i = 0; i < cells.Count; i++) cells[i].transform.parent.gameObject.SetActive(i < count);
        panel.sizeDelta = new Vector2(Padding * 2f + count * CellWidth + (count - 1) * CellGap, Height);

        Light(cells, count, Mathf.Clamp(Mathf.CeilToInt(current), 0, count));
    }

    void Light(IList<Image> list, int count, int nowLit)
    {
        for (int i = 0; i < count; i++)
        {
            if (list[i] == null) continue;
            bool wasLit = lit < 0 || i < lit;
            if (lit >= 0 && wasLit && i >= nowLit) StartCoroutine(Lose(list[i]));
            else if (!losing.Contains(list[i])) list[i].color = i < nowLit ? Full : Empty;
        }
        lit = nowLit;
    }

    void Update()
    {
        // The last cell pulses when it's all that's left.
        Image first = UsesCustom ? customSegments[0] : cells.Count > 0 ? cells[0] : null;
        if (lit != 1 || first == null || losing.Contains(first)) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.1f);
        first.color = Color.Lerp(LastCell, Full, pulse);
    }

    IEnumerator Lose(Image cell)
    {
        losing.Add(cell);
        RectTransform rect = cell.rectTransform;
        for (float t = 0f; t < LoseTime; t += Time.unscaledDeltaTime)
        {
            float progress = t / LoseTime;
            cell.color = progress < 0.25f ? Flash : Color.Lerp(Flash, Empty, (progress - 0.25f) / 0.75f);
            rect.localScale = Vector3.one * (1f + 0.35f * (1f - progress));
            yield return null;
        }
        rect.localScale = Vector3.one;
        losing.Remove(cell);
        int index = CellIndex(cell);
        cell.color = index >= 0 && index < lit ? Full : Empty;
    }

    int CellIndex(Image cell)
    {
        if (customSegments != null)
            for (int i = 0; i < customSegments.Length; i++)
                if (customSegments[i] == cell) return i;
        return cells.IndexOf(cell);
    }

    Image NewCell(int index)
    {
        var rect = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(row, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(CellWidth, CellHeight);
        rect.anchoredPosition = new Vector2(Padding + index * (CellWidth + CellGap), -(Padding + HeadingHeight));

        var back = rect.gameObject.AddComponent<Image>();
        back.color = Frame;
        back.raycastTarget = false;

        var fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
        fill.SetParent(rect, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        var image = fill.gameObject.AddComponent<Image>();
        image.color = Full;
        image.raycastTarget = false;

        // A lit strip along the top of the fill, so each cell reads as a little lamp rather than a flat block.
        var shine = new GameObject("Shine", typeof(RectTransform)).GetComponent<RectTransform>();
        shine.SetParent(fill, false);
        shine.anchorMin = new Vector2(0f, 1f);
        shine.anchorMax = Vector2.one;
        shine.pivot = new Vector2(0.5f, 1f);
        shine.sizeDelta = new Vector2(0f, 3f);
        var shineImage = shine.gameObject.AddComponent<Image>();
        shineImage.color = Shine;
        shineImage.raycastTarget = false;
        return image;
    }

    void Build()
    {
        if (built) return;
        built = true;

        // The custom HUD already sits on a Canvas in the scene; it only needs the vignette.
        if (UsesCustom)
        {
            if (GetComponentInParent<Canvas>() != null) BuildVignette();
            return;
        }

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        BuildVignette();

        row = new GameObject("Health", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(transform, false);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(44f, -36f);
        row.sizeDelta = new Vector2(400f, Height);

        Image back = GameUI.Sliced("Panel", row, GameUI.PanelSprite, Color.white);
        panel = back.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(Padding * 2f + CellWidth, Height);

        TextMeshProUGUI heading = GameUI.Label("Heading", row, "VITALS", 22f, GameUI.Dim, TextAlignmentOptions.TopLeft, GameUI.Heading);
        heading.characterSpacing = 10f;
        heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = heading.rectTransform.pivot = new Vector2(0f, 1f);
        heading.rectTransform.anchoredPosition = new Vector2(Padding + 2f, -Padding + 2f);
        heading.rectTransform.sizeDelta = new Vector2(300f, HeadingHeight);
    }

    void BuildVignette()
    {
        var vignetteRect = new GameObject("Hurt Vignette", typeof(RectTransform)).GetComponent<RectTransform>();
        // On the Canvas rather than inside a small HUD, so it covers the whole screen, and behind the HUD's own images.
        Canvas canvas = GetComponentInParent<Canvas>();
        vignetteRect.SetParent(canvas != null ? canvas.transform : transform, false);
        vignetteRect.SetAsFirstSibling();
        vignetteRect.anchorMin = Vector2.zero;
        vignetteRect.anchorMax = Vector2.one;
        vignetteRect.offsetMin = vignetteRect.offsetMax = Vector2.zero;
        vignette = vignetteRect.gameObject.AddComponent<RawImage>();
        vignette.texture = CombatSprites.VignetteTexture;
        vignette.raycastTarget = false;
        vignette.enabled = false;
    }
}
