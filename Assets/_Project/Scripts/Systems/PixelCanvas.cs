using UnityEngine;

// A small drawing surface for building pixel art in code: fill shapes, draw lines, then finish it the way pixel art is
// finished by hand, with light catching the top-left edges, shadow on the bottom-right, and a dark outline around it all.
// Coordinates are pixels with (0, 0) at the bottom left, the same as a texture. ToSprite turns it into a sprite.
// At DetailPixelsPerUnit its pixels match the ship tileset's, so code-drawn art sits in the level without looking chunky.
public class PixelCanvas
{
    public const float DetailPixelsPerUnit = 32f;

    public readonly int Width;
    public readonly int Height;
    private readonly Color32[] pixels;

    public PixelCanvas(int width, int height)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        pixels = new Color32[Width * Height];
    }

    public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public bool Filled(int x, int y) => Inside(x, y) && pixels[y * Width + x].a > 0;
    public Color32 Get(int x, int y) => Inside(x, y) ? pixels[y * Width + x] : default;

    public void Set(int x, int y, Color32 color)
    {
        if (Inside(x, y)) pixels[y * Width + x] = color;
    }

    // Only over what's already drawn.
    public void Paint(int x, int y, Color32 color)
    {
        if (Filled(x, y)) pixels[y * Width + x] = color;
    }

    public void Clear(int x, int y) => Set(x, y, default);

    public void FillRect(int x0, int y0, int x1, int y1, Color32 color)
    {
        for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
                Set(x, y, color);
    }

    // Any polygon, filled by the even-odd rule. color gives each pixel's colour from its position.
    public void FillPolygon(Vector2[] points, System.Func<int, int, Color32> color)
    {
        for (int y = 0; y < Height; y++)
        {
            float sampleY = y + 0.5f;
            for (int x = 0; x < Width; x++)
            {
                float sampleX = x + 0.5f;
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    Vector2 a = points[i], b = points[j];
                    if ((a.y > sampleY) != (b.y > sampleY) && sampleX < (b.x - a.x) * (sampleY - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
                if (inside) Set(x, y, color(x, y));
            }
        }
    }

    public void FillPolygon(Vector2[] points, Color32 color) => FillPolygon(points, (x, y) => color);

    public void FillEllipse(float centerX, float centerY, float radiusX, float radiusY, System.Func<int, int, Color32> color)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float dx = (x + 0.5f - centerX) / radiusX;
                float dy = (y + 0.5f - centerY) / radiusY;
                if (dx * dx + dy * dy <= 1f) Set(x, y, color(x, y));
            }
        }
    }

    // A straight line, thickness pixels wide.
    public void Line(float x0, float y0, float x1, float y1, Color32 color, int thickness = 1)
    {
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
        int reach = thickness / 2;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            for (int dy = -reach; dy <= thickness - 1 - reach; dy++)
                for (int dx = -reach; dx <= thickness - 1 - reach; dx++)
                    Set(x + dx, y + dy, color);
        }
    }

    // Copies another canvas on top of this one, with its bottom left at (atX, atY). Clear pixels are skipped.
    public void Stamp(PixelCanvas other, int atX, int atY)
    {
        for (int y = 0; y < other.Height; y++)
            for (int x = 0; x < other.Width; x++)
                if (other.Filled(x, y)) Set(atX + x, atY + y, other.Get(x, y));
    }

    // A little unevenness in everything drawn, so flat colour reads as a worn surface.
    public void Grain(System.Random dice, float amount)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0) continue;
            float shade = 1f + ((float)dice.NextDouble() * 2f - 1f) * amount;
            pixels[i] = Scale(pixels[i], shade);
        }
    }

    // Light from the top left: edge pixels facing up or left are lit, those facing down or right fall into shadow.
    public void Bevel(float light = 1.25f, float shadow = 0.7f)
    {
        var result = (Color32[])pixels.Clone();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!Filled(x, y)) continue;
                bool litEdge = !Filled(x, y + 1) || !Filled(x - 1, y);
                bool darkEdge = !Filled(x, y - 1) || !Filled(x + 1, y);
                if (litEdge && !darkEdge) result[y * Width + x] = Scale(pixels[y * Width + x], light);
                else if (darkEdge && !litEdge) result[y * Width + x] = Scale(pixels[y * Width + x], shadow);
            }
        }
        System.Array.Copy(result, pixels, pixels.Length);
    }

    // A one-pixel line around everything drawn, on the clear pixels touching it. Leave a pixel of margin for it.
    public void Outline(Color32 color)
    {
        var edge = new bool[pixels.Length];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (Filled(x, y)) continue;
                edge[y * Width + x] = Filled(x + 1, y) || Filled(x - 1, y) || Filled(x, y + 1) || Filled(x, y - 1);
            }
        }
        for (int i = 0; i < pixels.Length; i++)
            if (edge[i]) pixels[i] = color;
    }

    public Sprite ToSprite(Vector2 pivot, float pixelsPerUnit = DetailPixelsPerUnit)
    {
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return Sprite.Create(texture, new Rect(0f, 0f, Width, Height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // Lighter above 1, darker below it.
    public static Color32 Scale(Color32 color, float amount)
    {
        return new Color32(
            (byte)Mathf.Clamp(color.r * amount, 0f, 255f),
            (byte)Mathf.Clamp(color.g * amount, 0f, 255f),
            (byte)Mathf.Clamp(color.b * amount, 0f, 255f),
            color.a);
    }

    public static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

    // A made sprite owns its texture, which nothing else will clean up.
    public static void Destroy(Sprite sprite)
    {
        if (sprite == null) return;
        Object.Destroy(sprite.texture);
        Object.Destroy(sprite);
    }
}
