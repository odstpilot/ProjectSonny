using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// The look every in-game screen shares, so the HUD, prompts, speech, and dialogue read as one station interface:
//  - two fonts, loaded from Art/Fonts/Resources: m6x11 (Daniel Linssen), a pixel face, for everything people read, and
//    SdAsteroid, all capitals, for names and headings. SdAsteroid has no punctuation, so it falls back to m6x11 for
//    anything it's missing. m6x11 is drawn on a 16 pixel em, so it's sharpest at 16, 32 and 48 points.
//  - one palette: warm white text, amber for what the player can do, cyan for the station, red for danger.
//  - pixel-art pieces drawn in code and nine-sliced, so they stretch to any size and keep crisp pixel edges: a dark
//    glass panel in a steel frame, a light key cap, a name tab and an arrow to tint; and, for the small things that
//    come and go over the level (what E does, what Q looks at, what people say), a lighter look: a slim glass tag with
//    a thin edge and a flat little key cap (SoftPanelSprite, SoftKeyCapSprite, PromptBadge).
//  - text with a thin dark outline and a firm shadow (Shadow), so every letter stays defined over a busy level.
// Positions and sizes are canvas units on the 1920x1080 reference canvas.
public static class GameUI
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    // How many canvas units one pixel of the frame art covers.
    public const float PixelSize = 3f;

    public static readonly Color Text = new Color(0.95f, 0.93f, 0.88f);
    public static readonly Color Dim = new Color(0.6f, 0.64f, 0.68f);
    public static readonly Color Amber = new Color(1f, 0.68f, 0.32f);
    public static readonly Color Cyan = new Color(0.4f, 0.9f, 1f);
    public static readonly Color Red = new Color(1f, 0.32f, 0.27f);
    public static readonly Color Green = new Color(0.5f, 0.95f, 0.6f);
    public static readonly Color KeyText = new Color(0.1f, 0.1f, 0.12f);
    public static readonly Color Ink = new Color(0.07f, 0.08f, 0.1f);    // dark text on a light tab

    const string BodyFontName = "m6x11";
    const string HeadingFontName = "SdAsteroidB612-wo3e2";

    // Every printable ASCII character.
    static readonly string Printable = MakePrintable();

    static TMP_FontAsset body, heading;
    static readonly Dictionary<TMP_FontAsset, Material> shadows = new Dictionary<TMP_FontAsset, Material>();
    static Sprite panel, keyCap, tab, arrow, worldPanel, worldArrow, softPanel, softKeyCap, worldSoftPanel, worldSoftTail;
    static Texture2D scanlines;

    // --- Fonts ---

    public static TMP_FontAsset Body
    {
        get
        {
            if (body == null) body = LoadFont(BodyFontName);
            return body;
        }
    }

    public static TMP_FontAsset Heading
    {
        get
        {
            if (heading == null)
            {
                heading = LoadFont(HeadingFontName);
                if (heading != Body && heading != null)
                    heading.fallbackFontAssetTable = new List<TMP_FontAsset> { Body };
            }
            return heading;
        }
    }

    // The font, or the body font if none was given.
    public static TMP_FontAsset Or(TMP_FontAsset font) => font != null ? font : Body;

    static TMP_FontAsset LoadFont(string fontName)
    {
        var font = Resources.Load<Font>(fontName);
        if (font == null) return fontName == BodyFontName ? TMP_Settings.defaultFontAsset : Body;
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024);
        asset.name = fontName + " (runtime)";
        // Every letter up front, so the shadow and glow materials copied from it (which keep the atlas they were copied
        // with) have every letter too.
        asset.TryAddCharacters(Printable);
        return asset;
    }

    static string MakePrintable()
    {
        var letters = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) letters.Append(c);
        return letters.ToString();
    }

    // The font's material with a thin dark outline round each letter and a firm dark copy just below it, so text stays
    // defined over a bright level or a busy one, and the pixel font's edges come out sharp.
    public static Material Shadow(TMP_FontAsset font)
    {
        font = Or(font);
        if (font == null) return null;
        if (!shadows.TryGetValue(font, out Material material) || material == null)
        {
            material = TitleUI.GlowMaterial(font, new Color(0f, 0f, 0f, 0.7f), 0.08f, 0.12f, new Vector2(0.5f, -0.5f));
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.03f, 0.05f, 0.9f));
            if (material.HasProperty(ShaderUtilities.ID_Sharpness)) material.SetFloat(ShaderUtilities.ID_Sharpness, 0.35f);
            shadows[font] = material;
        }
        return material;
    }

    // Sentence case, for prompts: "OPEN DOOR" reads "Open door".
    public static string Sentence(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string lower = text.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    // --- Objects ---

    public static RectTransform NewRect(string rectName, Transform parent)
    {
        var rect = new GameObject(rectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    // Anchors a rect to part of its parent, given as fractions of the parent, inset by so many canvas units.
    public static RectTransform Anchor(RectTransform rect, float xMin, float yMin, float xMax, float yMax,
                                       float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static RectTransform Fill(RectTransform rect, float inset = 0f) => Anchor(rect, 0f, 0f, 1f, 1f, inset, inset, inset, inset);

    public static TextMeshProUGUI Label(string labelName, Transform parent, string text, float size, Color color,
                                        TextAlignmentOptions alignment = TextAlignmentOptions.Left, TMP_FontAsset font = null)
    {
        var label = NewRect(labelName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Or(font);
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // A nine-sliced pixel-art image, its pixels PixelSize canvas units across however big it's stretched.
    public static Image Sliced(string imageName, Transform parent, Sprite sprite, Color color)
    {
        Image image = NewRect(imageName, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / PixelSize;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // A key cap with its key on it, sized by a layout group or by hand. Returns the cap; the label is its child.
    public static Image KeyCap(Transform parent, string key, float size, out TextMeshProUGUI label)
    {
        Image cap = Sliced("Key " + key, parent, KeyCapSprite, Color.white);
        label = Label("Key", cap.transform, key, key.Length > 1 ? size * 0.8f : size, KeyText, TextAlignmentOptions.Center);
        // Over the face, clear of the lip along the bottom.
        Anchor(label.rectTransform, 0f, 0f, 1f, 1f, 8f, PixelSize * 3f, 8f, PixelSize);
        label.characterSpacing = key.Length > 1 ? 4f : 0f;
        return cap;
    }

    public static float KeyCapWidth(string key, float height) => Mathf.Max(height * 0.9f, 22f + key.Length * height * 0.36f);

    // --- Pixel art ---

    // Dark glass in a steel frame, lit from the top left.
    public static Sprite PanelSprite => panel != null ? panel : (panel = Slice(PanelRows, PanelBorder, 100f));

    // The same panel for the world (speech over people's heads), at the tileset's pixel size.
    public static Sprite WorldPanelSprite => worldPanel != null ? worldPanel : (worldPanel = Slice(PanelRows, PanelBorder, PixelCanvas.DetailPixelsPerUnit));

    // A light key cap with a darker lip along its bottom.
    public static Sprite KeyCapSprite => keyCap != null ? keyCap : (keyCap = Slice(KeyCapRows, new Vector4(1f, 3f, 1f, 2f), 100f));

    // White with shading and a dark edge: tint it to color it.
    public static Sprite TabSprite => tab != null ? tab : (tab = Slice(TabRows, new Vector4(1f, 2f, 1f, 2f), 100f));

    // A small arrow pointing down, white to tint.
    public static Sprite ArrowSprite => arrow != null ? arrow : (arrow = Slice(ArrowRows, Vector4.zero, 100f));

    public static Sprite WorldArrowSprite => worldArrow != null ? worldArrow : (worldArrow = Slice(ArrowRows, Vector4.zero, PixelCanvas.DetailPixelsPerUnit));

    // A slim dark glass tag with a thin, faint edge and rounded corners, for small things over the level. Its pixels are
    // SoftPixelSize canvas units across (set pixelsPerUnitMultiplier to 1 / SoftPixelSize).
    public const float SoftPixelSize = 2f;
    public static Sprite SoftPanelSprite => softPanel != null ? softPanel : (softPanel = Slice(SoftPanelRows, new Vector4(3f, 3f, 3f, 3f), 100f));

    // The same over people's heads, in the world.
    public static Sprite WorldSoftPanelSprite => worldSoftPanel != null ? worldSoftPanel
        : (worldSoftPanel = Slice(SoftPanelRows, new Vector4(3f, 3f, 3f, 3f), PixelCanvas.DetailPixelsPerUnit));

    // A little notch under a speech tag, pointing at whoever's talking, in the tag's glass.
    public static Sprite WorldSoftTailSprite => worldSoftTail != null ? worldSoftTail
        : (worldSoftTail = Slice(SoftTailRows, Vector4.zero, PixelCanvas.DetailPixelsPerUnit));

    // A small flat key cap: light, with a slightly darker bottom edge.
    public static Sprite SoftKeyCapSprite => softKeyCap != null ? softKeyCap : (softKeyCap = Slice(SoftKeyRows, new Vector4(2f, 2f, 2f, 2f), 100f));

    // Faint dark lines, three canvas pixels apart. Tile it with a RawImage's uvRect.
    public static Texture2D Scanlines
    {
        get
        {
            if (scanlines == null)
            {
                scanlines = PixelArt.MakeTexture(1, 3, (x, y) => new Color32(0, 0, 0, (byte)(y == 0 ? 90 : 0)));
                scanlines.wrapMode = TextureWrapMode.Repeat;
            }
            return scanlines;
        }
    }

    static readonly Vector4 PanelBorder = new Vector4(4f, 3f, 4f, 5f);

    static readonly string[] PanelRows =
    {
        " ############## ",
        "#LLLLLLLLLLLLLS#",
        "#LSSSSSSSSSSSSD#",
        "#LSiiiiiiiiiiSD#",
        "#LSiggggggggiSD#",
        "#LSi........iSD#",
        "#LSi........iSD#",
        "#LSi........iSD#",
        "#LSi........iSD#",
        "#LSi........iSD#",
        "#LSi........iSD#",
        "#LSi........iSD#",
        "#SSiiiiiiiiiiSD#",
        "#SDDDDDDDDDDDDD#",
        " ############## ",
    };

    static readonly string[] SoftPanelRows =
    {
        "  eeeeeeee  ",
        " e,,,,,,,,e ",
        "e,;;;;;;;;,e",
        "e;;;;;;;;;;e",
        "e;;;;;;;;;;e",
        "e;;;;;;;;;;e",
        "e;;;;;;;;;;e",
        "e;;;;;;;;;;e",
        "e,;;;;;;;;,e",
        " e,,,,,,,,e ",
        "  eeeeeeee  ",
    };

    static readonly string[] SoftTailRows =
    {
        ";;;;;",
        " ;;; ",
        "  ;  ",
    };

    static readonly string[] SoftKeyRows =
    {
        " kkkkkk ",
        "kKKKKKKk",
        "kKKKKKKk",
        "kKKKKKKk",
        "kKKKKKKk",
        "kqqqqqqk",
        " kkkkkk ",
    };

    static readonly string[] KeyCapRows =
    {
        " ######## ",
        "#WWWWWWWW#",
        "#wwwwwwww#",
        "#wwwwwwww#",
        "#wwwwwwww#",
        "#wwwwwwww#",
        "#wwwwwwww#",
        "#BBBBBBBB#",
        "#bbbbbbbb#",
        " ######## ",
    };

    static readonly string[] TabRows =
    {
        " ######## ",
        "#HHHHHHHH#",
        "#WWWWWWWW#",
        "#WWWWWWWW#",
        "#WWWWWWWW#",
        "#WWWWWWWW#",
        "#dddddddd#",
        " ######## ",
    };

    static readonly string[] ArrowRows =
    {
        "#######",
        "#WWWWW#",
        " #WWW# ",
        "  #W#  ",
        "   #   ",
    };

    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { '#', new Color32(7, 9, 13, 255) },        // outline
        { 'L', new Color32(170, 184, 198, 255) },   // steel, lit
        { 'S', new Color32(104, 116, 132, 255) },   // steel
        { 'D', new Color32(56, 64, 78, 255) },      // steel, in shadow
        { 'i', new Color32(17, 21, 28, 250) },      // the glass's inner edge
        { 'g', new Color32(28, 36, 46, 240) },      // light catching the top of the glass
        { '.', new Color32(11, 14, 20, 244) },      // glass
        { 'W', new Color32(236, 231, 216, 255) },   // key face
        { 'w', new Color32(218, 211, 194, 255) },
        { 'B', new Color32(150, 142, 126, 255) },   // key lip
        { 'b', new Color32(104, 98, 88, 255) },
        { 'H', new Color32(255, 255, 255, 255) },   // tab highlight
        { 'd', new Color32(168, 168, 168, 255) },   // tab shadow
        { 'e', new Color32(150, 164, 180, 110) },   // a soft tag's edge
        { ',', new Color32(14, 18, 25, 205) },      // just inside it
        { ';', new Color32(12, 15, 21, 196) },      // a soft tag's glass, a touch see-through
        { 'K', new Color32(226, 222, 210, 255) },   // soft key face
        { 'q', new Color32(170, 164, 150, 255) },   // its bottom edge
        { 'k', new Color32(60, 66, 76, 200) },      // round it
    };

    // Rows as in PixelArt.FromText, with a nine-slice border (left, bottom, right, top) in pixels.
    static Sprite Slice(string[] rows, Vector4 border, float pixelsPerUnit)
    {
        int width = rows[0].Length, height = rows.Length;
        Texture2D texture = PixelArt.MakeTexture(width, height, (x, y) =>
        {
            string row = rows[height - 1 - y];
            return x < row.Length && Palette.TryGetValue(row[x], out Color32 color) ? color : default;
        });
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
                             SpriteMeshType.FullRect, border);
    }

    // --- Timing ---

    public static System.Collections.IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }
}
