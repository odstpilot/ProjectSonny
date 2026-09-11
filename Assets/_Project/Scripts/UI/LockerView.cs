using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The screen while hiding in a Locker: the inside of the locker door fills the screen, dark metal with a vent of
// horizontal slats at eye level. Through the gaps is a first-person view of the hallway outside (FirstPersonView),
// built from the level itself, with any robots walking past. Moving the mouse peeks around a little.
// Also draws the "Press E to hide" prompt over the locker the player is next to, and the fade through black.
// Built from code the first time a locker needs it, so there is no canvas to set up. Every locker shares it.
public class LockerView : MonoBehaviour
{
    const int SortingOrder = 100; // above the HUD
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // The vent as fractions of the screen: left, bottom, right, top. A little above center, at eye level.
    static readonly Rect Vent = Rect.MinMaxRect(0.2f, 0.4f, 0.8f, 0.74f);
    const int SlatCount = 6;
    const float GapFraction = 0.55f;    // how much of each slat's row is open to see through
    const float DoorOverhang = 200f;    // the door runs this far past the screen edges, so peeking never shows its end
    const float PeekParallax = 28f;     // pixels the slats shift when peeking
    const float PeekSmoothing = 6f;
    const float BreathPixels = 5f;
    const float BreathSeconds = 3.6f;
    const float VignetteAlpha = 0.8f;

    // The view outside.
    const float EyeLevel = 0.69f;               // as a fraction of screen height: a little under the top of the vent, so heads and the floor both show
    const float FocalPerScreenHeight = 0.65f;   // a wide view, about 110 degrees across a 16:9 screen
    const float LookPixels = 120f;              // how far the view outside pans with the mouse at the screen edge
    const float LookMargin = 150f;              // the view outside runs this far past the screen edges, more than it pans

    static readonly Color Metal = new Color32(27, 31, 36, 255);
    static readonly Color PanelLine = new Color32(16, 18, 21, 255);
    static readonly Color VentRim = new Color32(44, 50, 57, 255);
    static readonly Color SlatBody = new Color32(35, 40, 46, 255);
    static readonly Color SlatLit = new Color32(72, 80, 90, 255);     // top lip of a slat, catching light from outside
    static readonly Color SlatShadow = new Color32(10, 11, 13, 255);
    static readonly Color Rivet = new Color32(58, 65, 74, 255);

    static LockerView instance;

    private Locker promptOwner;
    private Locker openLocker;
    private RectTransform prompt;
    private GameObject inside;
    private RectTransform door;
    private RectTransform outsideRect;
    private FirstPersonView outside;
    private RawImage vignette;
    private Image fade;
    private Vector2 peek;

    public static LockerView Get(TMP_FontAsset font)
    {
        if (instance == null)
        {
            instance = new GameObject("LockerView", typeof(RectTransform)).AddComponent<LockerView>();
            instance.Build(font);
        }
        return instance;
    }

    // Shows the prompt over this locker, or hides it if this locker was the one showing it.
    public static void SetPrompt(Locker locker, bool show, TMP_FontAsset font)
    {
        if (show)
            Get(font).promptOwner = locker;
        else if (instance != null && instance.promptOwner == locker)
            instance.promptOwner = null;
    }

    // For a locker switched off with the player inside: back to the normal view right away.
    public static void ForceClose()
    {
        if (instance == null) return;
        instance.Close();
        instance.SetFade(0f);
    }

    // Switches to the view from inside.
    public void Open(Locker locker)
    {
        openLocker = locker;
        peek = Vector2.zero;
        door.anchoredPosition = Vector2.zero;
        outsideRect.anchoredPosition = Vector2.zero;
        inside.SetActive(true);

        // The view outside is sized from the screen, so make sure the layout is current first.
        Canvas.ForceUpdateCanvases();
        float screenHeight = ((RectTransform)transform).rect.height;
        float eyeLevel = (EyeLevel * screenHeight + LookMargin) / (screenHeight + 2f * LookMargin);
        int standingLayer = locker.Body != null ? locker.Body.sortingLayerID : 0;
        outside.Show(locker.EyePoint, locker.DoorDirection, eyeLevel, FocalPerScreenHeight * screenHeight, standingLayer, locker.transform);
    }

    // Back to the normal view.
    public void Close()
    {
        if (openLocker == null) return;
        openLocker = null;
        outside.Hide();
        inside.SetActive(false);
    }

    // Unscaled time, so hit stop can't stall it.
    public IEnumerator FadeTo(float alpha, float duration)
    {
        float start = fade.color.a;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(start, alpha, t / duration));
            yield return null;
        }
        SetFade(alpha);
    }

    void LateUpdate()
    {
        UpdatePrompt();
        if (openLocker != null) UpdateInside();
    }

    void UpdatePrompt()
    {
        Camera main = Camera.main;
        bool show = promptOwner != null && openLocker == null && main != null;
        if (prompt.gameObject.activeSelf != show) prompt.gameObject.SetActive(show);
        if (!show) return;

        // An overlay canvas is laid out in screen pixels, so the screen point can be used as-is.
        float bob = Mathf.Sin(Time.unscaledTime * 4f) * 3f;
        prompt.position = main.WorldToScreenPoint(promptOwner.PromptPoint) + new Vector3(0f, bob, 0f);
    }

    void UpdateInside()
    {
        // Mouse from the middle of the screen, -1 to 1 on each axis, eased so the view drifts instead of snapping.
        Vector2 mouse = new Vector2(
            Mathf.Clamp(Input.mousePosition.x / Screen.width * 2f - 1f, -1f, 1f),
            Mathf.Clamp(Input.mousePosition.y / Screen.height * 2f - 1f, -1f, 1f));
        peek = Vector2.Lerp(peek, mouse, 1f - Mathf.Exp(-PeekSmoothing * Time.unscaledDeltaTime));

        // Slow breathing: the door rises and falls a few pixels and the dark closes in a little on each breath.
        float breath = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BreathSeconds);
        door.anchoredPosition = -peek * PeekParallax + new Vector2(0f, breath * BreathPixels);
        outsideRect.anchoredPosition = -peek * LookPixels;
        vignette.color = new Color(0f, 0f, 0f, VignetteAlpha + 0.05f * breath);
    }

    void SetFade(float alpha)
    {
        fade.color = new Color(0f, 0f, 0f, alpha);
        fade.enabled = alpha > 0f;
    }

    void Build(TMP_FontAsset font)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        // --- Inside the locker ---
        RectTransform insideRect = Fill(NewRect("Inside", transform));
        inside = insideRect.gameObject;

        // Solid black under everything, so the level never shows around the edges whatever the screen size.
        Block("Backing", insideRect, 0f, 0f, 1f, 1f, Color.black);

        outsideRect = Fill(NewRect("Outside", insideRect));
        outsideRect.offsetMin = new Vector2(-LookMargin, -LookMargin);
        outsideRect.offsetMax = new Vector2(LookMargin, LookMargin);
        outside = outsideRect.gameObject.AddComponent<FirstPersonView>();

        // Everything on the door moves together when peeking and breathing.
        door = Fill(NewRect("Door", insideRect));

        // The door around the vent, in four pieces so the vent itself stays open.
        Overhang(Block("Above", door, 0f, Vent.yMax, 1f, 1f, Metal), -DoorOverhang, 0f, DoorOverhang, DoorOverhang);
        Overhang(Block("Below", door, 0f, 0f, 1f, Vent.yMin, Metal), -DoorOverhang, -DoorOverhang, DoorOverhang, 0f);
        Overhang(Block("Left", door, 0f, Vent.yMin, Vent.xMin, Vent.yMax, Metal), -DoorOverhang, 0f, 0f, 0f);
        Overhang(Block("Right", door, Vent.xMax, Vent.yMin, 1f, Vent.yMax, Metal), 0f, 0f, DoorOverhang, 0f);

        // A pressed panel outline, like the inside of a real locker door.
        Line(door, 0.07f, 0.06f, 0.93f, 0.06f, 4f, PanelLine);
        Line(door, 0.07f, 0.94f, 0.93f, 0.94f, 4f, PanelLine);
        Line(door, 0.07f, 0.06f, 0.07f, 0.94f, 4f, PanelLine);
        Line(door, 0.93f, 0.06f, 0.93f, 0.94f, 4f, PanelLine);

        // Slats fill the vent from the bottom up, each with a gap above it to see through.
        float row = Vent.height / SlatCount;
        for (int i = 0; i < SlatCount; i++)
        {
            float bottom = Vent.yMin + row * i;
            Image slat = Block("Slat", door, Vent.xMin, bottom, Vent.xMax, bottom + row * (1f - GapFraction), SlatBody);
            Block("Lit Edge", slat.transform, 0f, 0.75f, 1f, 1f, SlatLit);
            Block("Shadow", slat.transform, 0f, 0f, 1f, 0.2f, SlatShadow);
        }

        // A rim around the vent and a rivet at each corner.
        Line(door, Vent.xMin, Vent.yMin, Vent.xMax, Vent.yMin, 10f, VentRim);
        Line(door, Vent.xMin, Vent.yMax, Vent.xMax, Vent.yMax, 10f, VentRim);
        Line(door, Vent.xMin, Vent.yMin, Vent.xMin, Vent.yMax, 10f, VentRim);
        Line(door, Vent.xMax, Vent.yMin, Vent.xMax, Vent.yMax, 10f, VentRim);
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                var corner = new Vector2(x == 0 ? Vent.xMin : Vent.xMax, y == 0 ? Vent.yMin : Vent.yMax);
                Dot(door, corner, new Vector2(x == 0 ? -26f : 26f, y == 0 ? -26f : 26f), 10f, Rivet);
            }
        }

        // The vignette stays put while the door moves.
        vignette = Fill(NewRect("Vignette", insideRect)).gameObject.AddComponent<RawImage>();
        vignette.texture = CombatSprites.VignetteTexture;
        vignette.color = new Color(0f, 0f, 0f, VignetteAlpha);
        vignette.raycastTarget = false;

        // Letters and spaces only: the game's HUD font has no punctuation.
        TextMeshProUGUI hint = Label("Hint", insideRect, $"Press {Locker.InteractKey} to step out", 30f, font);
        hint.color = new Color(0.8f, 0.84f, 0.9f, 0.55f);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.1f);
        hint.rectTransform.sizeDelta = new Vector2(600f, 50f);

        // --- Prompt over the locker ---
        prompt = NewRect("Prompt", transform);
        prompt.pivot = new Vector2(0.5f, 0f);
        prompt.sizeDelta = new Vector2(250f, 46f);
        Image promptBack = prompt.gameObject.AddComponent<Image>();
        promptBack.color = new Color(0f, 0f, 0f, 0.7f);
        promptBack.raycastTarget = false;
        Fill(Label("Text", prompt, $"Press {Locker.InteractKey} to hide", 28f, font).rectTransform);
        prompt.gameObject.SetActive(false);

        // --- Fade, on top of everything ---
        fade = Fill(NewRect("Fade", transform)).gameObject.AddComponent<Image>();
        fade.raycastTarget = false;
        SetFade(0f);

        inside.SetActive(false);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static RectTransform Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // A solid block covering part of its parent, given as fractions of the parent.
    static Image Block(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Grows a block past its anchors by this many pixels on each side.
    static void Overhang(Image block, float left, float bottom, float right, float top)
    {
        block.rectTransform.offsetMin = new Vector2(left, bottom);
        block.rectTransform.offsetMax = new Vector2(right, top);
    }

    // A straight line a fixed number of pixels thick. Horizontal when y0 == y1, vertical when x0 == x1.
    static void Line(Transform parent, float x0, float y0, float x1, float y1, float thickness, Color color)
    {
        RectTransform rect = Block("Line", parent, x0, y0, x1, y1, color).rectTransform;
        Vector2 half = (x0 == x1 ? Vector2.right : Vector2.up) * thickness * 0.5f;
        rect.offsetMin = -half;
        rect.offsetMax = half;
    }

    static void Dot(Transform parent, Vector2 anchor, Vector2 pixelOffset, float size, Color color)
    {
        RectTransform rect = Block("Rivet", parent, anchor.x, anchor.y, anchor.x, anchor.y, color).rectTransform;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = pixelOffset;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string text, float size, TMP_FontAsset font)
    {
        var label = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }
}
