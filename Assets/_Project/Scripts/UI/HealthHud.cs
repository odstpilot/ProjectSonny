using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The player's health in the top left corner: a row of cells, one for each point of health. A lost cell flashes and goes
// dark, and when only one is left it pulses. Also draws the red hurt vignette across the screen.
// Built from code the first time it's needed; PlayerHealthHandler hooks it up to the player's Health.
public class HealthHud : MonoBehaviour
{
    const int SortingOrder = 80;            // under the tutorial's text (90) and the death screen (120)
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const float CellWidth = 34f;
    const float CellHeight = 16f;
    const float CellGap = 6f;
    const float LoseTime = 0.45f;

    static readonly Color Full = new Color(1f, 0.44f, 0.3f);
    static readonly Color LastCell = new Color(0.55f, 0.16f, 0.1f);
    static readonly Color Empty = new Color(0.2f, 0.09f, 0.07f, 0.8f);
    static readonly Color Flash = new Color(1f, 0.96f, 0.88f);
    static readonly Color Frame = new Color(0f, 0f, 0f, 0.55f);

    static HealthHud instance;

    private Health tracked;
    private RectTransform row;
    private readonly List<Image> cells = new List<Image>();
    private readonly HashSet<Image> losing = new HashSet<Image>();
    private RawImage vignette;
    private int lit = -1;

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

    // Hides the row of cells but keeps the hurt vignette, for when health doesn't matter (the tutorial).
    public void SetCellsVisible(bool visible)
    {
        if (row != null) row.gameObject.SetActive(visible);
    }

    public void SetVignette(float alpha, Color color)
    {
        if (vignette == null) return;
        vignette.color = new Color(color.r, color.g, color.b, alpha);
        vignette.enabled = alpha > 0.001f;
    }

    void OnHealthChanged(float current, float max)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(max));
        while (cells.Count < count) cells.Add(NewCell(cells.Count));
        for (int i = 0; i < cells.Count; i++) cells[i].gameObject.SetActive(i < count);

        int nowLit = Mathf.Clamp(Mathf.CeilToInt(current), 0, count);
        for (int i = 0; i < count; i++)
        {
            bool wasLit = lit < 0 || i < lit;
            if (lit >= 0 && wasLit && i >= nowLit) StartCoroutine(Lose(cells[i]));
            else if (!losing.Contains(cells[i])) cells[i].color = i < nowLit ? Full : Empty;
        }
        lit = nowLit;
    }

    void Update()
    {
        // The last cell pulses when it's all that's left.
        if (lit != 1 || cells.Count == 0 || losing.Contains(cells[0])) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.1f);
        cells[0].color = Color.Lerp(LastCell, Full, pulse);
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
        cell.color = cells.IndexOf(cell) < lit ? Full : Empty;
    }

    Image NewCell(int index)
    {
        var rect = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(row, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(CellWidth, CellHeight);
        rect.anchoredPosition = new Vector2(index * (CellWidth + CellGap), 0f);

        var back = rect.gameObject.AddComponent<Image>();
        back.color = Frame;
        back.raycastTarget = false;

        var fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
        fill.SetParent(rect, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
        var image = fill.gameObject.AddComponent<Image>();
        image.color = Full;
        image.raycastTarget = false;
        return image;
    }

    private bool built;

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

        var vignetteRect = new GameObject("Hurt Vignette", typeof(RectTransform)).GetComponent<RectTransform>();
        vignetteRect.SetParent(transform, false);
        vignetteRect.anchorMin = Vector2.zero;
        vignetteRect.anchorMax = Vector2.one;
        vignetteRect.offsetMin = vignetteRect.offsetMax = Vector2.zero;
        vignette = vignetteRect.gameObject.AddComponent<RawImage>();
        vignette.texture = CombatSprites.VignetteTexture;
        vignette.raycastTarget = false;
        vignette.enabled = false;

        row = new GameObject("Health", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(transform, false);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(44f, -40f);
        row.sizeDelta = new Vector2(400f, CellHeight);
    }
}
