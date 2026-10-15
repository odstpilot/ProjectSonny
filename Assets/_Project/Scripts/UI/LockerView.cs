using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The screen while hiding in a Locker: the inside of the locker door fills the screen, dark metal with a vent of
// horizontal slats at eye level. Through the gaps is a first-person view of the hallway outside (FirstPersonView),
// built from the level itself, with any robots walking past. Moving the mouse peeks around a little.
// Also draws the fade through black. (The "[E] Hide" tag over a locker is the shared one, InteractPrompt.)
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

    // Being pulled out (PullOut): a robot's head blocks the vent, the door shakes, then it's torn away and the robot
    // lunges in, claws out. Seconds for each part, and how hard the screen shakes.
    const float LoomSeconds = 0.75f;
    const float YankSeconds = 0.14f;
    const float LungeSeconds = 0.45f;
    public const float PullOutSeconds = LoomSeconds + YankSeconds + LungeSeconds;
    const float LoomShakePixels = 7f;
    const float LungeShakePixels = 34f;
    static readonly Color32 Shadow = new Color32(7, 8, 10, 255);
    static readonly Color32 ShadowEdge = new Color32(22, 25, 30, 255);
    static readonly Color32 EyeRed = new Color32(255, 46, 34, 255);
    static readonly Color32 EyeHot = new Color32(255, 196, 170, 255);

    static LockerView instance;

    private Locker openLocker;
    private bool beingPulled;
    private RawImage looming;       // behind the door, seen through the slats
    private RawImage lunging;       // in front of everything once the door's gone
    private Image flash;
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

        // Cut off mid-pull (the locker switched off, say): everything back as it was.
        beingPulled = false;
        looming.gameObject.SetActive(false);
        lunging.gameObject.SetActive(false);
        flash.enabled = false;
        door.gameObject.SetActive(true);
        door.localRotation = Quaternion.identity;
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
        if (openLocker != null && !beingPulled) UpdateInside();
    }

    // The jumpscare, from inside the locker, while a robot pulls the player out. The Locker puts them outside after.
    // Unscaled time, so a hit stop elsewhere can't stall it.
    public IEnumerator PullOut()
    {
        beingPulled = true;
        Vector2 doorRest = door.anchoredPosition;
        Sfx.Play("Robot Alert", 1f, 0.8f);

        // Something steps in front of the door: the light through the slats goes, a red eye glows in the gaps, and the
        // door starts to shake as it gets a grip.
        looming.gameObject.SetActive(true);
        for (float t = 0f; t < LoomSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / LoomSeconds;
            looming.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, k * 1.6f));
            looming.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.05f, k);
            float shake = LoomShakePixels * k * k;
            door.anchoredPosition = doorRest + Random.insideUnitCircle * shake;
            yield return null;
        }

        // Torn open: the door's gone in a blink, and there it is.
        Sfx.Play("Wall Breach", 1f, 1.15f);
        Sfx.Play("Grab Squeeze", 1f, 0.9f);
        for (float t = 0f; t < YankSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / YankSeconds;
            door.anchoredPosition = doorRest + new Vector2(-2600f * k * k, 120f * k);
            door.localRotation = Quaternion.Euler(0f, 0f, 14f * k);
            yield return null;
        }
        door.gameObject.SetActive(false);
        looming.gameObject.SetActive(false);

        // It lunges in, filling the screen, claws first, with a red flash and the whole view shaking.
        lunging.gameObject.SetActive(true);
        flash.enabled = true;
        RectTransform lungeRect = lunging.rectTransform;
        for (float t = 0f; t < LungeSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / LungeSeconds;
            lungeRect.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.45f, 1f - (1f - k) * (1f - k));
            lungeRect.anchoredPosition = Random.insideUnitCircle * LungeShakePixels * (1f - k * 0.5f);
            flash.color = new Color(0.9f, 0.08f, 0.05f, 0.55f * (1f - k));
            yield return null;
        }

        // Put back for next time.
        lunging.gameObject.SetActive(false);
        flash.enabled = false;
        door.gameObject.SetActive(true);
        door.anchoredPosition = doorRest;
        door.localRotation = Quaternion.identity;
        beingPulled = false;
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

        // A robot's head right up against the door, behind the slats: only there while it pulls the player out.
        looming = PixelArt("Looming", insideRect, LoomingTexture());
        RectTransform loomRect = looming.rectTransform;
        loomRect.anchorMin = new Vector2(Vent.xMin - 0.08f, Vent.yMin - 0.22f);
        loomRect.anchorMax = new Vector2(Vent.xMax + 0.08f, Vent.yMax + 0.2f);
        loomRect.offsetMin = loomRect.offsetMax = Vector2.zero;
        looming.gameObject.SetActive(false);

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

        // The robot lunging in once the door's torn away, over all of it, and the red flash with it.
        lunging = PixelArt("Lunging", insideRect, LungingTexture());
        Fill(lunging.rectTransform);
        lunging.rectTransform.offsetMin = new Vector2(-DoorOverhang, -DoorOverhang);
        lunging.rectTransform.offsetMax = new Vector2(DoorOverhang, DoorOverhang);
        lunging.gameObject.SetActive(false);
        flash = Fill(NewRect("Flash", insideRect)).gameObject.AddComponent<Image>();
        flash.raycastTarget = false;
        flash.enabled = false;

        // --- Fade, on top of everything ---
        fade = Fill(NewRect("Fade", transform)).gameObject.AddComponent<Image>();
        fade.raycastTarget = false;
        SetFade(0f);

        inside.SetActive(false);
    }

    // --- The robot, drawn as chunky pixel art so it matches the game ---

    static RawImage PixelArt(string name, Transform parent, Texture2D texture)
    {
        var image = NewRect(name, parent).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    static Texture2D NewCanvas(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(new Color32[width * height]);
        return texture;
    }

    // A box with its corners cut off by `corner` pixels. Rows count up from the bottom.
    static void Box(Texture2D texture, int x0, int y0, int x1, int y1, int corner, Color32 color)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int inX = Mathf.Min(x - x0, x1 - x), inY = Mathf.Min(y - y0, y1 - y);
                if (inX + inY < corner) continue;
                if (x >= 0 && y >= 0 && x < texture.width && y < texture.height) texture.SetPixel(x, y, color);
            }
        }
    }

    // A thick line, for arms and claws.
    static void Limb(Texture2D texture, Vector2 from, Vector2 to, float thickness, Color32 color)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
            int r = Mathf.CeilToInt(thickness * 0.5f);
            Box(texture, Mathf.RoundToInt(p.x) - r, Mathf.RoundToInt(p.y) - r, Mathf.RoundToInt(p.x) + r, Mathf.RoundToInt(p.y) + r, 1, color);
        }
    }

    // The visor: a red slot with a hot white middle, like the bots' own.
    static void Visor(Texture2D texture, int x0, int y0, int x1, int y1)
    {
        Box(texture, x0, y0, x1, y1, 1, EyeRed);
        int midX = (x0 + x1) / 2, midY = (y0 + y1) / 2, half = Mathf.Max(1, (x1 - x0) / 6);
        Box(texture, midX - half, midY - 1, midX + half, midY + 1, 0, EyeHot);
    }

    // Up against the door: a bot's head filling the vent, its visor at slat height so the red shows through the gaps.
    static Texture2D LoomingTexture()
    {
        Texture2D t = NewCanvas(64, 44);
        Box(t, 2, 0, 61, 41, 6, ShadowEdge);
        Box(t, 3, 0, 60, 40, 6, Shadow);
        Box(t, 29, 40, 34, 43, 0, Shadow);          // antenna
        Visor(t, 12, 22, 51, 28);
        t.Apply();
        return t;
    }

    // Lunging in: the head over the top of the view, visor blazing, both arms reaching out at the viewer from under it,
    // thickening as they come closer, claws spread wide at the bottom corners.
    static Texture2D LungingTexture()
    {
        Texture2D t = NewCanvas(96, 54);
        foreach (int side in new[] { -1, 1 })
        {
            // Shoulder to hand in steps, each a little thicker: nearer the eye, bigger.
            Vector2 shoulder = new Vector2(48 + side * 20, 30);
            Vector2 hand = new Vector2(48 + side * 36, 9);
            const int Segments = 6;
            for (int i = 0; i < Segments; i++)
            {
                Vector2 a = Vector2.Lerp(shoulder, hand, i / (float)Segments);
                Vector2 b = Vector2.Lerp(shoulder, hand, (i + 1) / (float)Segments);
                float thickness = Mathf.Lerp(5f, 11f, i / (float)(Segments - 1));
                Limb(t, a, b, thickness + 2f, ShadowEdge);
                Limb(t, a, b, thickness, Shadow);
            }
            // The hand, and four long hooked claws splayed toward the middle and down past the edge.
            Box(t, Mathf.RoundToInt(hand.x) - 7, Mathf.RoundToInt(hand.y) - 6, Mathf.RoundToInt(hand.x) + 7, Mathf.RoundToInt(hand.y) + 6, 3, Shadow);
            for (int c = 0; c < 4; c++)
            {
                float spread = Mathf.Lerp(-0.35f, 1.1f, c / 3f);       // radians from straight in toward the middle
                Vector2 toward = new Vector2(-side * Mathf.Cos(spread), -Mathf.Sin(spread));
                Vector2 knuckle = hand + toward * 6f;
                Vector2 tip = knuckle + toward * 9f + new Vector2(0f, -3f);
                Limb(t, hand, knuckle, 4f, Shadow);
                Limb(t, knuckle, tip, 2f, Shadow);
                t.SetPixel(Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y), ShadowEdge);
            }
        }
        // The head over the shoulders, a lit rim along its top so it stands off the dark.
        Box(t, 22, 27, 73, 58, 7, ShadowEdge);
        Box(t, 23, 28, 72, 56, 7, Shadow);
        Visor(t, 29, 38, 66, 45);
        t.Apply();
        return t;
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
        label.font = GameUI.Or(font);
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }
}
