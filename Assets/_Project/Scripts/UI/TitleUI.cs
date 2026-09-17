using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Building blocks shared by the title screen and its menus: UI objects made in code, easing curves, and generated
// effect textures and sounds. Positions are pixels on the 1920x1080 canvas, measured from the middle of the parent.
public static class TitleUI
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    public const int SampleRate = 44100;
    const string ScrambleGlyphs = "#%&@$*/<>=+?!0123456789ABCDEFXZ";
    const string FontCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#%&@$*/<>=+?!.,:;'-[]()x ";

    static Material additive;
    static Texture2D sparkle, streak, patch;

    // Adds light instead of covering what's behind. Null if the Sonny/UI Additive shader is missing, which leaves the
    // normal blending.
    public static Material Additive
    {
        get
        {
            if (additive == null)
            {
                Shader shader = Shader.Find("Sonny/UI Additive");
                if (shader != null) additive = new Material(shader);
            }
            return additive;
        }
    }

    // --- Objects ---

    public static RectTransform NewRect(string rectName, Transform parent)
    {
        var rect = new GameObject(rectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // Anchored to the middle of its parent.
    public static RectTransform Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    public static RawImage Picture(string pictureName, Transform parent, Texture texture, Vector2 position, Vector2 size, bool glow = false)
    {
        RawImage picture = Place(NewRect(pictureName, parent), position, size).gameObject.AddComponent<RawImage>();
        picture.texture = texture;
        picture.raycastTarget = false;
        if (glow) picture.material = Additive;
        return picture;
    }

    public static RawImage StretchedPicture(string pictureName, Transform parent, Texture texture)
    {
        RawImage picture = Stretch(NewRect(pictureName, parent)).gameObject.AddComponent<RawImage>();
        picture.texture = texture;
        picture.raycastTarget = false;
        return picture;
    }

    public static HudShape Shape(string shapeName, Transform parent, Color color, Vector2[] points,
                                 bool filled = false, float thickness = 2f, float feather = 0f, bool closed = true, bool glow = false)
    {
        HudShape shape = Place(NewRect(shapeName, parent), Vector2.zero, ReferenceResolution).gameObject.AddComponent<HudShape>();
        shape.raycastTarget = false;
        shape.color = color;
        shape.filled = filled;
        shape.thickness = thickness;
        shape.feather = feather;
        shape.closed = closed;
        if (glow) shape.material = Additive;
        shape.SetPoints(points);
        return shape;
    }

    public static Vector2[] Box(float left, float right, float top, float bottom)
    {
        return new[] { new Vector2(left, top), new Vector2(right, top), new Vector2(right, bottom), new Vector2(left, bottom) };
    }

    // A slanted bar segment, like the notches on the loading bar, centered on a point.
    public static Vector2[] Slant(Vector2 center, float width, float height, float lean)
    {
        float halfWidth = width * 0.5f, halfHeight = height * 0.5f;
        return new[]
        {
            center + new Vector2(-halfWidth - lean, -halfHeight), center + new Vector2(halfWidth - lean, -halfHeight),
            center + new Vector2(halfWidth + lean, halfHeight), center + new Vector2(-halfWidth + lean, halfHeight)
        };
    }

    public static TextMeshProUGUI Label(string labelName, Transform parent, TMP_FontAsset font, float size, Color color,
                                        Vector2 position, Vector2 area, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = Place(NewRect(labelName, parent), position, area).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = "";
        return label;
    }

    public static TMP_FontAsset MakeFontAsset(Font font)
    {
        if (font == null) return TMP_Settings.defaultFontAsset;
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
        asset.TryAddCharacters(FontCharacters);    // up front, so glow materials copied from it see every letter
        return asset;
    }

    // The font's material with a soft copy of each letter underneath: a glow, or a drop shadow when it's offset.
    public static Material GlowMaterial(TMP_FontAsset font, Color color, float softness, float dilate, Vector2 offset = default)
    {
        var material = new Material(font.material);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, color);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset.x);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offset.y);
        return material;
    }

    // --- Timing ---

    public static float Phase(float time, float start, float end) => Mathf.Clamp01((time - start) / (end - start));
    public static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
    public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    public static float EaseIn(float t) => t * t * t;
    public static float EaseInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    public static Color Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

    // 0 to 1, pausing at a third and two thirds of the way.
    public static float Stutter(float progress) => progress - Mathf.Sin(progress * 6f * Mathf.PI) / (6f * Mathf.PI);

    public static float Hash(int n)
    {
        n = (n << 13) ^ n;
        return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
    }

    // The text with the letters not yet reached shown as shuffling symbols, the same length throughout so a monospaced
    // font doesn't shift.
    public static string Decode(string text, float progress, float now)
    {
        if (progress >= 1f) return text;
        if (progress <= 0f) return "";
        int shown = Mathf.FloorToInt(progress * text.Length);
        int tick = Mathf.FloorToInt(now * 20f);
        var letters = new char[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            bool scrambled = i >= shown && text[i] != ' ';
            letters[i] = scrambled ? ScrambleGlyphs[Mathf.FloorToInt(Hash(i * 31 + tick) * ScrambleGlyphs.Length) % ScrambleGlyphs.Length] : text[i];
        }
        return new string(letters);
    }

    // --- Textures ---

    // A soft dot with four thin rays.
    public static Texture2D SparkleTexture => sparkle != null ? sparkle : (sparkle = MakeSparkle());

    // A streak, white at the head on the right, thinning into a teal tail.
    public static Texture2D StreakTexture => streak != null ? streak : (streak = MakeStreak());

    // Solid in the middle, fading out toward every edge. Stretch it over something to hide it softly.
    public static Texture2D PatchTexture => patch != null ? patch : (patch = MakePatch());

    public static Texture2D NoiseTexture(int width, int height)
    {
        Texture2D texture = PixelArt.MakeTexture(width, height, (x, y) =>
        {
            byte shade = (byte)Random.Range(0, 256);
            return new Color32(shade, shade, shade, 255);
        });
        texture.wrapMode = TextureWrapMode.Repeat;
        return texture;
    }

    static Texture2D MakeSparkle()
    {
        Texture2D texture = PixelArt.MakeTexture(17, 17, (x, y) =>
        {
            float dx = Mathf.Abs(x - 8) / 8f, dy = Mathf.Abs(y - 8) / 8f;
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
            float rays = Mathf.Max(Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy * 7f), Mathf.Clamp01(1f - dy) * Mathf.Clamp01(1f - dx * 7f));
            return new Color32(255, 255, 255, (byte)(Mathf.Clamp01(core + rays * rays * 0.9f) * 255f));
        });
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    static Texture2D MakeStreak()
    {
        Texture2D texture = PixelArt.MakeTexture(256, 32, (x, y) =>
        {
            float along = x / 255f;
            float width = 1.2f + 6f * along * along;
            float across = Mathf.Abs(y - 15.5f) / width;
            Color color = Color.Lerp(new Color(0.15f, 0.8f, 0.8f), Color.white, Mathf.Pow(along, 8f));
            color.a = Mathf.Pow(along, 2.4f) * Mathf.Exp(-across * across * 2f);
            return color;
        });
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    static Texture2D MakePatch()
    {
        Texture2D texture = PixelArt.MakeTexture(64, 64, (x, y) =>
        {
            float edge = Mathf.Min(Mathf.Min(x, 63 - x), Mathf.Min(y, 63 - y)) / 16f;
            return new Color32(255, 255, 255, (byte)(Smooth(0f, 1f, edge) * 255f));
        });
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    // --- Sound ---

    // A short beep that dies away.
    public static AudioClip Tone(string clipName, float frequency, float seconds, float volume)
    {
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)SampleRate;
            float envelope = Mathf.Clamp01(time / 0.004f) * Mathf.Exp(-time * 50f);
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * volume * envelope;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A burst of white noise that dies away.
    public static AudioClip Crackle(string clipName, float seconds, float volume)
    {
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        var random = new System.Random(3);
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)SampleRate;
            float envelope = Mathf.Clamp01(time / 0.005f) * Mathf.Exp(-time * 9f);
            data[i] = ((float)random.NextDouble() * 2f - 1f) * volume * envelope;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
