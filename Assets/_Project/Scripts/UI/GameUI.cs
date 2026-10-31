using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// shared look for in-game ui
public static class GameUI
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    public const float PixelSize = 3f;

    public static readonly Color Text = new Color(0.95f, 0.93f, 0.88f);
    public static readonly Color Dim = new Color(0.6f, 0.64f, 0.68f);
    public static readonly Color Amber = new Color(1f, 0.68f, 0.32f);
    public static readonly Color Cyan = new Color(0.4f, 0.9f, 1f);
    public static readonly Color Red = new Color(1f, 0.32f, 0.27f);
    public static readonly Color Green = new Color(0.5f, 0.95f, 0.6f);
    public static readonly Color KeyText = new Color(0.1f, 0.1f, 0.12f);
    public static readonly Color Ink = new Color(0.07f, 0.08f, 0.1f);

    const string BodyFontName = "m6x11";
    const string HeadingFontName = "ChakraPetch-SemiBold";

    static readonly string Printable = MakePrintable();

    static TMP_FontAsset body, heading;
    static readonly Dictionary<TMP_FontAsset, Material> shadows = new Dictionary<TMP_FontAsset, Material>();
    static Sprite panel, keyCap, tab, arrow, worldPanel, worldArrow, softPanel, softKeyCap, worldSoftPanel, worldSoftTail;
    static Texture2D scanlines;

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

    public static TMP_FontAsset Or(TMP_FontAsset font) => font != null ? font : Body;

    static TMP_FontAsset LoadFont(string fontName)
    {
        var font = Resources.Load<Font>(fontName);
        if (font == null) return fontName == BodyFontName ? TMP_Settings.defaultFontAsset : Body;
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024);
        asset.name = fontName + " (runtime)";
        asset.TryAddCharacters(Printable);
        return asset;
    }

    static string MakePrintable()
    {
        var letters = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) letters.Append(c);
        return letters.ToString();
    }

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

    public static string Sentence(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string lower = text.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    public static RectTransform NewRect(string rectName, Transform parent)
    {
        var rect = new GameObject(rectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

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
        if (font != null && font == heading) label.fontStyle |= FontStyles.UpperCase;
        return label;
    }

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

    public static Image KeyCap(Transform parent, string key, float size, out TextMeshProUGUI label)
    {
        Image cap = Sliced("Key " + key, parent, KeyCapSprite, Color.white);
        label = Label("Key", cap.transform, key, key.Length > 1 ? size * 0.8f : size, KeyText, TextAlignmentOptions.Center);
        Anchor(label.rectTransform, 0f, 0f, 1f, 1f, 8f, PixelSize * 3f, 8f, PixelSize);
        label.characterSpacing = key.Length > 1 ? 4f : 0f;
        return cap;
    }

    public static float KeyCapWidth(string key, float height) => Mathf.Max(height * 0.9f, 22f + key.Length * height * 0.36f);

    public static Sprite PanelSprite => panel != null ? panel : (panel = Slice(PanelRows, PanelBorder, 100f));

    public static Sprite WorldPanelSprite => worldPanel != null ? worldPanel : (worldPanel = Slice(PanelRows, PanelBorder, PixelCanvas.DetailPixelsPerUnit));

    public static Sprite KeyCapSprite => keyCap != null ? keyCap : (keyCap = Slice(KeyCapRows, new Vector4(1f, 3f, 1f, 2f), 100f));

    public static Sprite TabSprite => tab != null ? tab : (tab = Slice(TabRows, new Vector4(1f, 2f, 1f, 2f), 100f));

    public static Sprite ArrowSprite => arrow != null ? arrow : (arrow = Slice(ArrowRows, Vector4.zero, 100f));

    public static Sprite WorldArrowSprite => worldArrow != null ? worldArrow : (worldArrow = Slice(ArrowRows, Vector4.zero, PixelCanvas.DetailPixelsPerUnit));

    public const float SoftPixelSize = 2f;
    public static Sprite SoftPanelSprite => softPanel != null ? softPanel : (softPanel = Slice(SoftPanelRows, new Vector4(3f, 3f, 3f, 3f), 100f));

    public static Sprite WorldSoftPanelSprite => worldSoftPanel != null ? worldSoftPanel
        : (worldSoftPanel = Slice(SoftPanelRows, new Vector4(3f, 3f, 3f, 3f), PixelCanvas.DetailPixelsPerUnit));

    public static Sprite WorldSoftTailSprite => worldSoftTail != null ? worldSoftTail
        : (worldSoftTail = Slice(SoftTailRows, Vector4.zero, PixelCanvas.DetailPixelsPerUnit));

    public static Sprite SoftKeyCapSprite => softKeyCap != null ? softKeyCap : (softKeyCap = Slice(SoftKeyRows, new Vector4(2f, 2f, 2f, 2f), 100f));

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
        { '#', new Color32(7, 9, 13, 255) },
        { 'L', new Color32(170, 184, 198, 255) },
        { 'S', new Color32(104, 116, 132, 255) },
        { 'D', new Color32(56, 64, 78, 255) },
        { 'i', new Color32(17, 21, 28, 250) },
        { 'g', new Color32(28, 36, 46, 240) },
        { '.', new Color32(11, 14, 20, 244) },
        { 'W', new Color32(236, 231, 216, 255) },
        { 'w', new Color32(218, 211, 194, 255) },
        { 'B', new Color32(150, 142, 126, 255) },
        { 'b', new Color32(104, 98, 88, 255) },
        { 'H', new Color32(255, 255, 255, 255) },
        { 'd', new Color32(168, 168, 168, 255) },
        { 'e', new Color32(150, 164, 180, 110) },
        { ',', new Color32(14, 18, 25, 240) },
        { ';', new Color32(12, 15, 21, 232) },
        { 'K', new Color32(226, 222, 210, 255) },
        { 'q', new Color32(170, 164, 150, 255) },
        { 'k', new Color32(60, 66, 76, 200) },
    };

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

    static readonly Dictionary<string, Sprite> artPieces = new Dictionary<string, Sprite>();

    public static Sprite ArtPiece(string resource, Vector2Int sourceSize, RectInt pixels)
    {
        string key = $"{resource} {pixels}";
        if (artPieces.TryGetValue(key, out Sprite piece) && piece != null) return piece;
        var texture = Resources.Load<Texture2D>(resource);
        if (texture == null)
        {
            Debug.LogWarning($"GameUI: there's no picture called {resource} in a Resources folder.");
            return null;
        }
        float sx = texture.width / (float)sourceSize.x, sy = texture.height / (float)sourceSize.y;
        var rect = new Rect(pixels.x * sx, (sourceSize.y - pixels.yMax) * sy, pixels.width * sx, pixels.height * sy);
        piece = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        piece.name = $"{resource} piece";
        artPieces[key] = piece;
        return piece;
    }

    static TMP_FontAsset readout;

    public static TMP_FontAsset Readout => readout != null ? readout : (readout = LoadFont("VT323-Regular"));

    public static System.Collections.IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }
}
