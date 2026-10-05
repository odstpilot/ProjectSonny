using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The save icon in the bottom right corner, for a moment each time the game saves (SaveGame): a little data cartridge
// with its light blinking amber and "SAVING", then the light green and "SAVED", then it fades away. It waits for a fade
// or title card to clear first, so it's seen. Built from code the first time it's needed, like the tutorial HUD, and runs
// on unscaled time. One per scene: SaveIcon.Show().
public class SaveIcon : MonoBehaviour
{
    const int SortingOrder = 105;           // over the HUD and Pip, under screens that take over (110)
    const float Margin = 44f;
    const float IconPixel = 5f;             // canvas units per pixel of the icon
    const float FadeTime = 0.25f;
    const float SavingSeconds = 1.1f;
    const float SavedSeconds = 1.4f;
    const float MaxWaitForScreen = 8f;      // shown anyway after this long under a fade

    static readonly string[] CartridgeRows =
    {
        "..########..",
        ".#oooooooo#.",
        "#oo######oo#",
        "#o#wwwwww#o#",
        "#o#wwwwww#o#",
        "#o#wwwwww#o#",
        "#o########o#",
        "#oooooooooo#",
        "#oo.o.o.o.o#",
        "#oooooooooo#",
        "############",
    };

    static SaveIcon instance;

    private CanvasGroup group;
    private Image lamp;
    private TextMeshProUGUI label;
    private Coroutine showing;

    public static void Show()
    {
        if (instance == null) instance = new GameObject("SaveIcon", typeof(RectTransform)).AddComponent<SaveIcon>();
        if (instance.showing != null) instance.StopCoroutine(instance.showing);
        instance.showing = instance.StartCoroutine(instance.Run());
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

    IEnumerator Run()
    {
        for (float waited = 0f; TutorialHud.ScreenCovered && waited < MaxWaitForScreen; waited += Time.unscaledDeltaTime)
            yield return null;

        label.text = "SAVING";
        label.color = GameUI.Dim;
        yield return Fade(1f);
        for (float t = 0f; t < SavingSeconds; t += Time.unscaledDeltaTime)
        {
            lamp.color = Mathf.Repeat(t, 0.3f) < 0.15f ? GameUI.Amber : Color.Lerp(GameUI.Amber, Color.black, 0.7f);
            yield return null;
        }

        label.text = "SAVED";
        label.color = GameUI.Green;
        lamp.color = GameUI.Green;
        yield return GameUI.WaitUnscaled(SavedSeconds);
        yield return Fade(0f);
        showing = null;
    }

    IEnumerator Fade(float to)
    {
        float from = group.alpha;
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / FadeTime);
            yield return null;
        }
        group.alpha = to;
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

        RectTransform corner = GameUI.NewRect("Save", transform);
        corner.anchorMin = corner.anchorMax = corner.pivot = new Vector2(1f, 0f);
        corner.anchoredPosition = new Vector2(-Margin, Margin);
        corner.sizeDelta = new Vector2(260f, CartridgeRows.Length * IconPixel);
        group = corner.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var palette = new Dictionary<char, Color32>
        {
            { '#', new Color32(20, 24, 30, 255) },
            { 'o', new Color32(96, 112, 130, 255) },
            { 'w', new Color32(214, 222, 230, 255) },
        };
        Sprite cartridge = PixelArt.FromText(CartridgeRows, palette, 100f, new Vector2(1f, 0f));
        var icon = GameUI.NewRect("Cartridge", corner).gameObject.AddComponent<Image>();
        icon.sprite = cartridge;
        icon.raycastTarget = false;
        var iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(1f, 0f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(CartridgeRows[0].Length, CartridgeRows.Length) * IconPixel;

        // The activity light, on the cartridge's label.
        lamp = GameUI.NewRect("Light", iconRect).gameObject.AddComponent<Image>();
        lamp.raycastTarget = false;
        var lampRect = (RectTransform)lamp.transform;
        lampRect.anchorMin = lampRect.anchorMax = lampRect.pivot = new Vector2(0f, 0f);
        lampRect.anchoredPosition = new Vector2(7f, 5f) * IconPixel;
        lampRect.sizeDelta = new Vector2(2f, 2f) * IconPixel;
        lamp.color = GameUI.Amber;

        label = GameUI.Label("Label", corner, "SAVING", 26f, GameUI.Dim, TextAlignmentOptions.Right, GameUI.Heading);
        label.characterSpacing = 10f;
        label.fontSharedMaterial = GameUI.Shadow(label.font);
        var labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = new Vector2(-(CartridgeRows[0].Length * IconPixel + 16f), 0f);
    }
}
