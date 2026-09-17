using System.Collections.Generic;
using UnityEngine;

// The title screen's art, painted in code: nebula, stars, rocks, the station, and the ship. The turning planet is
// SpinningPlanet, below.
public static class TitleArt
{
    public const int StationWidth = 110, StationHeight = 172;

    static readonly Color RockDark = new Color(0.05f, 0.055f, 0.065f);
    static readonly Color RockBright = new Color(0.68f, 0.69f, 0.72f);
    static readonly Vector3 RockLight = new Vector3(-0.55f, 0.6f, 0.58f).normalized;

    static readonly string[] ShipArt =
    {
        "    ##        ",
        "   #oo#       ",
        "  #oooo###    ",
        " ##oooowwoo## ",
        "#oooooowwooooo",
        " ##oooooooo## ",
        "  #oooo###    ",
        "   #oo#       ",
        "    ##        ",
    };

    // Teal smoke, warped so it swirls, brightest toward the upper left. The back layer is solid; the front one is wisps
    // on a clear ground.
    public static Texture2D NebulaTexture(float seed, bool wisps)
    {
        const int width = 384, height = 216;
        Texture2D texture = PixelArt.MakeTexture(width, height, (x, y) =>
        {
            float u = x / (float)width * 3.2f + seed, v = y / (float)height * 1.8f + seed;
            float warpX = Fbm(u + 5.2f, v + 1.3f, 3), warpY = Fbm(u + 1.7f, v + 9.2f, 3);
            float n = Fbm(u + 2.5f * warpX, v + 2.5f * warpY, 5);
            if (wisps)
            {
                Color wisp = Color.Lerp(new Color(0.1f, 0.42f, 0.46f), new Color(0.5f, 0.82f, 0.85f), TitleUI.Smooth(0.62f, 0.8f, n));
                wisp.a = TitleUI.Smooth(0.5f, 0.75f, n) * 0.45f;
                return wisp;
            }
            float focus = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x / (float)width, y / (float)height), new Vector2(0.36f, 0.6f)) * 1.4f);
            Color color = Color.Lerp(new Color(0.012f, 0.035f, 0.05f), new Color(0.05f, 0.16f, 0.19f), TitleUI.Smooth(0.38f, 0.72f, n));
            color = Color.Lerp(color, new Color(0.14f, 0.32f, 0.35f), TitleUI.Smooth(0.65f, 0.85f, n));
            color *= 0.55f + 0.65f * focus;
            color.a = 1f;
            return color;
        });
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    public static Texture2D StarfieldTexture()
    {
        const int width = 1050, height = 610;
        var pixels = new Color32[width * height];
        var random = new System.Random(12);
        for (int i = 0; i < 1700; i++)
        {
            byte value = (byte)(40f + 215f * Mathf.Pow((float)random.NextDouble(), 3f));
            double tint = random.NextDouble();
            pixels[random.Next(pixels.Length)] =
                tint < 0.15 ? new Color32((byte)(value * 0.7f), value, value, 255) :
                tint < 0.22 ? new Color32(value, (byte)(value * 0.9f), (byte)(value * 0.75f), 255) :
                new Color32(value, value, value, 255);
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    // A haze just outside a planet's edge, for a picture 1.18 times the planet's size. The planet covers the inside.
    public static Texture2D AtmosphereTexture()
    {
        const float edge = 1f / 1.18f;
        return CombatSprites.MakeRadialTexture(128, r => r < edge ? TitleUI.Smooth(0.6f, edge, r) : Mathf.Pow(1f - Mathf.Clamp01((r - edge) / (1f - edge)), 2.2f));
    }

    // A lumpy, pitted rock lit from the upper left.
    public static Texture2D RockTexture(int width, int height, int seed)
    {
        var random = new System.Random(seed);
        float offsetX = random.Next(100, 900) + 0.31f, offsetY = random.Next(100, 900) + 0.77f;
        var pits = new Vector3[8 + random.Next(10)];    // x, y, radius
        for (int i = 0; i < pits.Length; i++)
            pits[i] = new Vector3((float)random.NextDouble() * 1.3f - 0.65f, (float)random.NextDouble() * 1.3f - 0.65f, 0.05f + (float)random.NextDouble() * 0.16f);
        float pixel = 2.5f / Mathf.Min(width, height);
        float aspect = width / (float)height;

        Texture2D texture = PixelArt.MakeTexture(width, height, (x, y) =>
        {
            float nx = (x + 0.5f) / width * 2f - 1f, ny = (y + 0.5f) / height * 2f - 1f;
            float angle = Mathf.Atan2(ny, nx);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            float edge = 0.8f + 0.34f * (Mathf.PerlinNoise(cos * 0.9f + offsetX, sin * 0.9f + offsetY) - 0.5f)
                              + 0.12f * (Mathf.PerlinNoise(cos * 3f + offsetY, sin * 3f + offsetX) - 0.5f);
            float distance = Mathf.Sqrt(nx * nx + ny * ny);
            float alpha = Mathf.Clamp01((edge - distance) / pixel);
            if (alpha <= 0f) return default;

            float s = Mathf.Min(distance / edge, 1f);
            float nz = Mathf.Sqrt(1f - s * s);
            float bx = nx * aspect * 2.6f + offsetX, by = ny * 2.6f + offsetY;
            float bump = Fbm(bx, by, 4);
            float slopeX = (Fbm(bx + 0.06f, by, 4) - bump) * 9f, slopeY = (Fbm(bx, by + 0.06f, 4) - bump) * 9f;
            var normal = new Vector3(nx / edge - slopeX, ny / edge - slopeY, nz + 0.25f).normalized;
            float tone = (0.1f + 0.9f * Mathf.Clamp01(Vector3.Dot(normal, RockLight))) * (0.7f + 0.6f * bump);

            foreach (Vector3 pit in pits)
            {
                float dx = (nx - pit.x) * aspect, dy = ny - pit.y;
                float along = Mathf.Sqrt(dx * dx + dy * dy);
                float t = along / pit.z;
                if (t >= 1.15f) continue;
                if (t >= 1f)
                {
                    tone *= 1.12f;
                    continue;
                }
                // In shadow on the side toward the light, lit on the far wall.
                float side = (dx * RockLight.x + dy * RockLight.y) / Mathf.Max(0.0001f, along);
                tone *= Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01(0.5f - 0.5f * side * t));
            }
            tone *= 0.85f + 0.3f * Mathf.PerlinNoise(nx * 40f + offsetY, ny * 40f + offsetX);
            tone *= Mathf.Lerp(0.6f, 1f, Mathf.Clamp01((edge - distance) / (pixel * 3f)));

            Color color = Color.Lerp(RockDark, RockBright, Mathf.Clamp01(tone));
            color.a = alpha;
            return color;
        });
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    // A station of modules on a truss, with solar panels, in blue pixel art.
    public static Texture2D StationTexture()
    {
        const int width = StationWidth, height = StationHeight;
        var pixels = new Color32[width * height];
        var outline = new Color32(18, 32, 58, 255);
        var truss = new Color32(130, 175, 225, 255);
        var module = new Color32(105, 155, 215, 255);
        var moduleTop = new Color32(185, 220, 255, 255);
        var window = new Color32(230, 250, 255, 255);
        var panel = new Color32(38, 82, 160, 255);
        var panelLine = new Color32(100, 160, 235, 255);
        var radiator = new Color32(175, 200, 225, 255);

        void Fill(int x0, int y0, int x1, int y1, Color32 color)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(height - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(width - 1, x1); x++)
                    pixels[y * width + x] = color;
        }
        void Box(int x0, int y0, int x1, int y1, Color32 color)
        {
            Fill(x0, y0, x1, y1, outline);
            Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, color);
        }
        void Panel(int x0, int y0, int x1, int y1)
        {
            Box(x0, y0, x1, y1, panel);
            for (int x = x0 + 5; x < x1; x += 5) Fill(x, y0 + 1, x, y1 - 1, panelLine);
            for (int y = y0 + 4; y < y1; y += 4) Fill(x0 + 1, y, x1 - 1, y, panelLine);
        }
        void Module(int x0, int y0, int x1, int y1)
        {
            Box(x0, y0, x1, y1, module);
            Fill(x0 + 1, y1 - 1, x1 - 1, y1 - 1, moduleTop);
            int middle = (y0 + y1) / 2;
            for (int x = x0 + 3; x < x1 - 2; x += 4) Fill(x, middle, x + 1, middle, window);
        }

        Box(52, 0, 58, 164, truss);
        for (int y = 3; y < 164; y += 6) Fill(53, y, 57, y, outline);
        Fill(55, 165, 55, 171, truss);

        Fill(18, 30, 92, 31, truss);
        Box(10, 24, 34, 36, radiator);
        Box(76, 24, 100, 36, radiator);
        Fill(4, 99, 106, 100, truss);
        Panel(2, 88, 42, 111);
        Panel(68, 88, 108, 111);
        Fill(22, 149, 88, 150, truss);
        Panel(18, 142, 44, 157);
        Panel(66, 142, 92, 157);

        Module(41, 6, 69, 22);
        Module(44, 42, 66, 58);
        Module(38, 64, 72, 84);
        Module(46, 102, 64, 118);
        Module(42, 122, 68, 138);
        Module(47, 156, 63, 166);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    // A small grey ship pointing right, 14 by 9 pixels.
    public static Sprite ShipSprite()
    {
        var palette = new Dictionary<char, Color32>
        {
            { '#', new Color32(60, 70, 86, 255) },
            { 'o', new Color32(175, 185, 198, 255) },
            { 'w', new Color32(120, 205, 255, 255) }
        };
        return PixelArt.FromText(ShipArt, palette, 6f, new Vector2(0.5f, 0.5f));
    }

    public static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amplitude = 0.5f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amplitude * Mathf.PerlinNoise(x, y);
            total += amplitude;
            x = x * 2.03f + 17.1f;
            y = y * 2.03f + 9.7f;
            amplitude *= 0.5f;
        }
        return sum / total;
    }

    // Fbm across a map whose left and right edges meet, by blending into a copy one map-width over.
    public static float WrappedFbm(float u, float v, float scaleU, float scaleV, int octaves, float seed)
    {
        float here = Fbm(u * scaleU + seed, v * scaleV + seed, octaves);
        float over = Fbm((u - 1f) * scaleU + seed, v * scaleV + seed, octaves);
        return Mathf.Lerp(here, over, u);
    }
}

// A dark blue planet, lit from behind on the left, that slowly turns. Its map is painted once; then, for every pixel of
// the disc that can be seen on screen, where it looks on the map and how brightly it's lit are worked out once too, so
// each turn is only a lookup per pixel.
public class SpinningPlanet
{
    const int Size = 512;
    const int MapWidth = 512, MapHeight = 256;     // the width has to be a power of two
    const float Tilt = 0.35f;                       // radians
    const float Spin = 0.0035f;                     // turns a second
    static readonly Vector3 Light = new Vector3(-0.82f, 0.42f, 0.22f).normalized;

    static readonly Color OceanDeep = new Color(0.03f, 0.06f, 0.13f);
    static readonly Color OceanShallow = new Color(0.08f, 0.15f, 0.26f);
    static readonly Color LandLow = new Color(0.18f, 0.24f, 0.32f);
    static readonly Color LandHigh = new Color(0.35f, 0.39f, 0.46f);
    static readonly Color Ice = new Color(0.66f, 0.72f, 0.78f);
    static readonly Color Cloud = new Color(0.72f, 0.78f, 0.86f);

    readonly Color32[] map = new Color32[MapWidth * MapHeight];
    readonly byte[] cityLights = new byte[MapWidth * MapHeight];
    readonly Color32[] pixels = new Color32[Size * Size];
    readonly int[] pixelIndex, mapRow;
    readonly float[] longitude, lighting, haze;
    readonly byte[] alpha;
    float turn;

    public Texture2D Texture { get; }

    // center and radius are on the canvas; pixels outside visible, the most of the canvas the planet can ever show on,
    // are skipped.
    public SpinningPlanet(Vector2 center, float radius, Rect visible)
    {
        var random = new System.Random(21);
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                float u = x / (float)MapWidth, v = y / (float)MapHeight;
                float height = TitleArt.WrappedFbm(u, v, 5f, 2.5f, 5, 13.7f);
                float clouds = TitleArt.WrappedFbm(u, v, 11f, 5.5f, 4, 71.3f);
                float land = TitleUI.Smooth(0.5f, 0.54f, height);
                Color water = Color.Lerp(OceanDeep, OceanShallow, TitleUI.Smooth(0.3f, 0.5f, height));
                Color ground = Color.Lerp(LandLow, LandHigh, TitleUI.Smooth(0.54f, 0.7f, height));
                Color color = Color.Lerp(water, ground, land);
                float ice = TitleUI.Smooth(0.78f, 0.9f, Mathf.Abs(v - 0.5f) * 2f + (height - 0.5f) * 0.4f);
                color = Color.Lerp(color, Ice, ice);
                float cloud = TitleUI.Smooth(0.55f, 0.72f, clouds) * 0.7f;
                color = Color.Lerp(color, Cloud, cloud);

                int index = y * MapWidth + x;
                map[index] = color;
                if (land > 0.5f && ice < 0.1f && cloud < 0.3f && random.NextDouble() < 0.06)
                    cityLights[index] = (byte)random.Next(120, 256);
            }
        }

        float texel = radius * 2f / Size;
        float sinTilt = Mathf.Sin(Tilt), cosTilt = Mathf.Cos(Tilt);
        var indices = new List<int>();
        var rows = new List<int>();
        var longitudes = new List<float>();
        var lights = new List<float>();
        var hazes = new List<float>();
        var alphas = new List<byte>();
        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                if (!visible.Contains(center + new Vector2(px + 0.5f - Size * 0.5f, py + 0.5f - Size * 0.5f) * texel)) continue;

                float nx = (px + 0.5f) / Size * 2f - 1f, ny = (py + 0.5f) / Size * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = Mathf.Clamp01((1f - distance) * Size * 0.5f + 0.5f);
                if (edge <= 0f) continue;

                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - distance * distance));
                float tiltedY = ny * cosTilt + nz * sinTilt;
                float tiltedZ = nz * cosTilt - ny * sinTilt;
                float latitude = Mathf.Asin(Mathf.Clamp(tiltedY, -1f, 1f));
                float diffuse = nx * Light.x + ny * Light.y + nz * Light.z;

                indices.Add(py * Size + px);
                rows.Add(Mathf.Clamp(Mathf.FloorToInt((latitude / Mathf.PI + 0.5f) * MapHeight), 0, MapHeight - 1));
                longitudes.Add(Mathf.Atan2(nx, tiltedZ) / (2f * Mathf.PI) + 0.5f);
                lights.Add(TitleUI.Smooth(-0.12f, 0.8f, diffuse));
                hazes.Add(Mathf.Pow(1f - nz, 2.2f) * TitleUI.Smooth(-0.4f, 0.6f, diffuse) * 0.9f);
                alphas.Add((byte)(edge * 255f));
            }
        }
        pixelIndex = indices.ToArray();
        mapRow = rows.ToArray();
        longitude = longitudes.ToArray();
        lighting = lights.ToArray();
        haze = hazes.ToArray();
        alpha = alphas.ToArray();

        Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Turn(0f);
    }

    public void Turn(float dt)
    {
        turn = Mathf.Repeat(turn + Spin * dt, 1f);
        for (int i = 0; i < pixelIndex.Length; i++)
        {
            int column = (int)((longitude[i] + turn) * MapWidth) & (MapWidth - 1);
            int source = mapRow[i] * MapWidth + column;
            Color32 ground = map[source];
            float light = lighting[i];
            float dark = 1f - light;
            float air = haze[i];
            float city = cityLights[source] / 255f * dark * dark;
            float lit = 0.08f + 0.92f * light;
            pixels[pixelIndex[i]] = new Color32(
                ToByte(ground.r * lit + 90f * air + 255f * city + 3f * dark),
                ToByte(ground.g * lit + 165f * air + 170f * city + 6f * dark),
                ToByte(ground.b * lit + 255f * air + 90f * city + 14f * dark),
                alpha[i]);
        }
        Texture.SetPixels32(pixels);
        Texture.Apply(false);
    }

    static byte ToByte(float value) => (byte)Mathf.Min(255f, value);
}
