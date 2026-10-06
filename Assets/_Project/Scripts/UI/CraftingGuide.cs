using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A panel down the left of the screen on how crafting works, up from arriving on the upper maintenance deck until the
// workbench is first used (Workshop), when the bench's own card takes over (TipCard). It doesn't stop anything: it's
// just there to read while scavenging. Built from code the first time it's shown.
public class CraftingGuide : MonoBehaviour
{
    const int SortingOrder = 85;            // over the health readout (80), under the tutorial's text (90)
    const float Width = 460f;
    const float FadeSeconds = 0.35f;

    static readonly (string key, string text)[] Rows =
    {
        ("", "Scrap glows blue. Walk over it to pick it up."),
        ("", "Every piece is a part: a metal sheet, a circuit board, a battery, or wire."),
        ("E", "At the workbench, make things from your parts."),
        ("", "Start with the EMP. It shorts out bots and cameras."),
    };

    static CraftingGuide instance;

    private CanvasGroup group;
    private Coroutine fading;

    public static void Show() => Get().FadeTo(1f);

    public static void Hide()
    {
        if (instance != null) instance.FadeTo(0f);
    }

    static CraftingGuide Get()
    {
        if (instance == null)
        {
            instance = new GameObject("CraftingGuide", typeof(RectTransform)).AddComponent<CraftingGuide>();
            instance.Build();
        }
        return instance;
    }

    void FadeTo(float alpha)
    {
        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Fade(alpha));
    }

    IEnumerator Fade(float alpha)
    {
        float start = group.alpha;
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, alpha, t / FadeSeconds);
            yield return null;
        }
        group.alpha = alpha;
        fading = null;
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

        // The panel, sized to fit what's in it.
        Image panel = GameUI.Sliced("Panel", transform, GameUI.PanelSprite, new Color(1f, 1f, 1f, 0.92f));
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(56f, 40f);
        var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(24, 24, 18, 20);
        column.spacing = 10f;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rect.sizeDelta = new Vector2(Width, 0f);
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        TextMeshProUGUI heading = GameUI.Label("Heading", panel.transform, "CRAFTING", 26f, GameUI.Amber, TextAlignmentOptions.Left, GameUI.Heading);
        heading.characterSpacing = 10f;

        foreach ((string key, string text) in Rows)
        {
            RectTransform row = GameUI.NewRect("Row", panel.transform);
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 12f;
            line.childAlignment = TextAnchor.UpperLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;

            // A key cap where there's a key to press, a small cyan mark where there isn't.
            if (!string.IsNullOrEmpty(key))
            {
                Image cap = GameUI.KeyCap(row, key, 22f, out _);
                var capSize = cap.gameObject.AddComponent<LayoutElement>();
                capSize.minWidth = capSize.preferredWidth = GameUI.KeyCapWidth(key, 32f);
                capSize.minHeight = capSize.preferredHeight = 32f;
            }
            else
            {
                TextMeshProUGUI mark = GameUI.Label("Mark", row, ">", 24f, GameUI.Cyan, TextAlignmentOptions.Center);
                var markSize = mark.gameObject.AddComponent<LayoutElement>();
                markSize.minWidth = markSize.preferredWidth = GameUI.KeyCapWidth("E", 32f);
            }

            TextMeshProUGUI words = GameUI.Label("Text", row, text, 22f, GameUI.Text, TextAlignmentOptions.TopLeft);
            words.textWrappingMode = TextWrappingModes.Normal;
            var wordsSize = words.gameObject.AddComponent<LayoutElement>();
            wordsSize.flexibleWidth = 1f;
            wordsSize.preferredWidth = Width - 48f - GameUI.KeyCapWidth("E", 32f) - 12f;
        }
    }
}
