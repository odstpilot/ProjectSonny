using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "E  USE" tag that bobs over whatever the player can use right now. Objects call Show every frame they're usable
// and Hide when they're not; if two ask at once, the last one wins. Built from code the first time it's needed.
public class InteractPrompt : MonoBehaviour
{
    const int SortingOrder = 105;           // over the HUD, under terminal screens
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    static readonly Color TextColor = new Color(1f, 0.8f, 0.5f);

    static InteractPrompt instance;
    static readonly Dictionary<Font, TMP_FontAsset> fonts = new Dictionary<Font, TMP_FontAsset>();

    private RectTransform badge;
    private TextMeshProUGUI label;
    private Object owner;
    private Vector3 worldPoint;

    public static void Show(Object owner, Vector3 worldPoint, string text, Font font = null)
    {
        if (instance == null)
        {
            instance = new GameObject("InteractPrompt", typeof(RectTransform)).AddComponent<InteractPrompt>();
            instance.Build();
        }

        instance.owner = owner;
        instance.worldPoint = worldPoint;
        if (instance.label.text != text) instance.label.text = text;
        TMP_FontAsset asset = FontFor(font);
        if (asset != null && instance.label.font != asset) instance.label.font = asset;
    }

    public static void Hide(Object owner)
    {
        if (instance != null && instance.owner == owner) instance.owner = null;
    }

    static TMP_FontAsset FontFor(Font font)
    {
        if (font == null) return TMP_Settings.defaultFontAsset;
        if (!fonts.TryGetValue(font, out TMP_FontAsset asset) || asset == null)
            fonts[font] = asset = TMP_FontAsset.CreateFontAsset(font);
        return asset;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        bool show = owner != null && cam != null;
        if (badge.gameObject.activeSelf != show) badge.gameObject.SetActive(show);
        if (!show) return;

        // An overlay canvas is laid out in screen pixels, so the screen point can be used as-is.
        float bob = Mathf.Sin(Time.unscaledTime * 4f) * 3f;
        badge.position = cam.WorldToScreenPoint(worldPoint) + new Vector3(0f, bob, 0f);
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        badge = new GameObject("Badge", typeof(RectTransform)).GetComponent<RectTransform>();
        badge.SetParent(transform, false);
        badge.pivot = new Vector2(0.5f, 0f);
        badge.sizeDelta = new Vector2(280f, 46f);
        var back = badge.gameObject.AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.7f);
        back.raycastTarget = false;

        label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(badge, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.fontSize = 28f;
        label.color = TextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        badge.gameObject.SetActive(false);
    }
}
