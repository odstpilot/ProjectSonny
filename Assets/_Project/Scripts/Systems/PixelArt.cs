using System.Collections.Generic;
using UnityEngine;

// Placeholder sprites made in code, so a level works before its real art exists (like CombatSprites does for combat).
// Draw a sprite as text, one character per pixel, or fill a texture pixel by pixel from a function.
public static class PixelArt
{
    // rows[0] is the top row. Characters missing from the palette are see-through.
    public static Sprite FromText(string[] rows, IDictionary<char, Color32> palette, float pixelsPerUnit, Vector2 pivot)
    {
        int width = 0;
        foreach (string row in rows)
            width = Mathf.Max(width, row.Length);
        int height = rows.Length;

        Texture2D texture = MakeTexture(width, height, (x, y) =>
        {
            string row = rows[height - 1 - y];
            return x < row.Length && palette.TryGetValue(row[x], out Color32 color) ? color : default;
        });
        return ToSprite(texture, pixelsPerUnit, pivot);
    }

    // pixel(x, y) gives each pixel's color, with (0, 0) at the bottom left.
    public static Texture2D MakeTexture(int width, int height, System.Func<int, int, Color32> pixel)
    {
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    // Tiled sprite renderers need SpriteMeshType.FullRect.
    public static Sprite ToSprite(Texture2D texture, float pixelsPerUnit, Vector2 pivot, SpriteMeshType meshType = SpriteMeshType.Tight)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, pixelsPerUnit, 0, meshType);
    }
}
