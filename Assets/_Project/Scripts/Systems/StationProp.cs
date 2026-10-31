using System.Collections.Generic;
using UnityEngine;

// room props drawn in code
public class StationProp : MonoBehaviour
{
    public enum Kind { ServerRack, SignalDish, Crates, GasTanks, Drum, Toolbox, CableRun, WallScreen, Pegboard }

    const float PixelsPerUnit = 16f;

    public Kind kind;

    private readonly List<(SpriteRenderer lamp, Color color, float period, float phase, float onFraction)> lamps =
        new List<(SpriteRenderer, Color, float, float, float)>();
    private SpriteRenderer screen;
    private Sprite[] screenFrames;
    private float frameTimer;
    private int frame;

    static readonly Color32 Outline = new Color32(20, 22, 28, 255);
    static readonly Color32 Steel = new Color32(104, 110, 122, 255);
    static readonly Color32 SteelLit = new Color32(150, 158, 170, 255);
    static readonly Color32 SteelDark = new Color32(60, 64, 74, 255);
    static readonly Color32 Cabinet = new Color32(38, 42, 50, 255);
    static readonly Color32 CabinetLit = new Color32(58, 64, 74, 255);
    static readonly Color32 Slot = new Color32(16, 18, 22, 255);
    static readonly Color32 Hazard = new Color32(214, 170, 50, 255);
    static readonly Color32 Off = new Color32(34, 40, 36, 255);

    static readonly Dictionary<string, Sprite> made = new Dictionary<string, Sprite>();
    static Sprite pixel;

    void Awake()
    {
        var dice = new System.Random(Mathf.RoundToInt(transform.position.x * 8f) * 73856093 ^ Mathf.RoundToInt(transform.position.y * 8f) * 19349663
            ^ (int)kind * 83492791);
        switch (kind)
        {
            case Kind.ServerRack: BuildServerRack(dice); break;
            case Kind.SignalDish: BuildSignalDish(dice); break;
            case Kind.Crates: Standing(Cached($"crates{dice.Next(4)}", () => DrawCrates(dice))); break;
            case Kind.GasTanks: Standing(Cached($"tanks{dice.Next(4)}", () => DrawTanks(dice))); break;
            case Kind.Drum: Standing(Cached($"drum{dice.Next(3)}", () => DrawDrum(dice))); break;
            case Kind.Toolbox: Standing(Cached("toolbox", DrawToolbox)); break;
            case Kind.CableRun: Flat(Cached($"cables{dice.Next(4)}", () => DrawCables(dice))); break;
            case Kind.WallScreen: BuildWallScreen(dice); break;
            case Kind.Pegboard: OnWall(Cached($"pegboard{dice.Next(3)}", () => DrawPegboard(dice))); break;
        }
    }

    void Update()
    {
        float now = Time.time;
        foreach (var (lamp, color, period, phase, onFraction) in lamps)
        {
            if (lamp == null) continue;
            bool on = Mathf.Repeat(now / period + phase, 1f) < onFraction;
            lamp.color = on ? color : new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 1f);
        }
        if (screen == null || screenFrames == null) return;
        frameTimer -= Time.deltaTime;
        if (frameTimer > 0f) return;
        frameTimer = 0.14f;
        frame = (frame + 1) % screenFrames.Length;
        screen.sprite = screenFrames[frame];
    }

    SpriteRenderer Standing(Sprite sprite)
    {
        DepthSort.Group(gameObject);
        var art = new GameObject("Art").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        art.sprite = sprite;
        return art;
    }

    SpriteRenderer OnWall(Sprite sprite)
    {
        var art = gameObject.AddComponent<SpriteRenderer>();
        art.sprite = sprite;
        art.sortingLayerName = "On the wall";
        art.sortingOrder = 7;
        return art;
    }

    void Flat(Sprite sprite)
    {
        var art = gameObject.AddComponent<SpriteRenderer>();
        art.sprite = sprite;
        art.sortingLayerName = "FloorObject";
        art.sortingOrder = 3;
    }

    void Lamp(Transform parent, Vector2 pixelAt, Color color, float period, float onFraction, int width = 1, string layer = null, int order = 1)
    {
        var lamp = new GameObject("Lamp").AddComponent<SpriteRenderer>();
        lamp.transform.SetParent(parent, false);
        lamp.transform.localPosition = new Vector3((pixelAt.x + width * 0.5f) / PixelsPerUnit, (pixelAt.y + 0.5f) / PixelsPerUnit, 0f);
        lamp.transform.localScale = new Vector3(width, 1f, 1f);
        lamp.sprite = Pixel;
        if (CombatSprites.EffectMaterial != null) lamp.sharedMaterial = CombatSprites.EffectMaterial;
        if (layer != null) lamp.sortingLayerName = layer;
        else
        {
            var under = parent.GetComponent<SpriteRenderer>();
            if (under != null) lamp.sortingLayerID = under.sortingLayerID;
        }
        lamp.sortingOrder = order;
        lamps.Add((lamp, color, period, Random.value, onFraction));
    }

    void Glow(Vector2 pixelAt, Color color, float radius, float intensity, BlinkLight.Pattern pattern, float period)
    {
        var holder = new GameObject("Glow");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = pixelAt / PixelsPerUnit;
        holder.SetActive(false);
        var light = holder.AddComponent<BlinkLight>();
        light.color = color;
        light.pattern = pattern;
        light.period = period;
        light.phase = Random.value;
        light.radius = radius;
        light.intensity = intensity;
        holder.SetActive(true);
    }

    void BuildServerRack(System.Random dice)
    {
        SpriteRenderer art = Standing(Cached($"server{dice.Next(3)}", () => DrawServerRack(dice)));
        Color[] colors = { new Color(0.35f, 1f, 0.45f), new Color(0.35f, 1f, 0.45f), new Color(1f, 0.7f, 0.2f), new Color(0.35f, 0.85f, 1f) };
        for (int row = 0; row < 7; row++)
        {
            float y = 4 + row * 3;
            Lamp(art.transform, new Vector2(10f - 7f, y), colors[dice.Next(colors.Length)], 0.2f + (float)dice.NextDouble() * 1.4f, 0.3f + (float)dice.NextDouble() * 0.5f);
            if (dice.NextDouble() < 0.6)
                Lamp(art.transform, new Vector2(8f - 7f, y), colors[dice.Next(colors.Length)], 0.3f + (float)dice.NextDouble() * 2f, 0.5f);
        }
        Glow(new Vector2(0f, 10f - DepthSort.FeetToCenter * PixelsPerUnit), new Color(0.35f, 1f, 0.55f), 1.1f, 0.25f, BlinkLight.Pattern.Flicker, 1.2f);
    }

    void BuildSignalDish(System.Random dice)
    {
        SpriteRenderer art = Standing(Cached($"dish{dice.Next(2)}", () => DrawSignalDish(dice)));
        Lamp(art.transform, new Vector2(13f - 10f, 20f), new Color(1f, 0.2f, 0.15f), 1.6f, 0.2f);
        Glow(new Vector2(3f, 20f - DepthSort.FeetToCenter * PixelsPerUnit), new Color(1f, 0.2f, 0.15f), 0.9f, 0.35f, BlinkLight.Pattern.Blink, 1.6f);
    }

    void BuildWallScreen(System.Random dice)
    {
        int style = dice.Next(3);
        screen = OnWall(Cached($"screen{style}-0", () => DrawScreen(style, 0)));
        screenFrames = new Sprite[6];
        for (int i = 0; i < screenFrames.Length; i++)
        {
            int f = i;
            screenFrames[i] = Cached($"screen{style}-{f}", () => DrawScreen(style, f));
        }
        frame = dice.Next(screenFrames.Length);
        Color tint = style == 0 ? new Color(0.35f, 1f, 0.55f) : style == 1 ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.7f, 0.25f);
        Glow(Vector2.zero, tint, 1.4f, 0.35f, BlinkLight.Pattern.Flicker, 2f);
    }

    static Sprite Pixel => pixel != null ? pixel
        : (pixel = PixelArt.ToSprite(PixelArt.MakeTexture(1, 1, (x, y) => new Color32(255, 255, 255, 255)), PixelsPerUnit, new Vector2(0.5f, 0.5f)));

    static Sprite Cached(string key, System.Func<Sprite> draw)
    {
        if (made.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
        return made[key] = draw();
    }

    static Sprite Standing(Texture2D texture) => PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));

    static Sprite DrawServerRack(System.Random dice)
    {
        const int W = 14, H = 30;
        int vents = dice.Next(2);
        return Standing(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            if (x == 0 || x == W - 1 || y == H - 1 || y == 0) return Outline;
            if (y == H - 2) return CabinetLit;
            if (x == 1) return CabinetLit;
            if (y >= H - 6 && y <= H - 4) return (x + vents) % 2 == 0 ? Slot : Cabinet;
            if (x >= 2 && x <= 7 && y >= 3 && y <= H - 8)
            {
                int bay = (y - 3) % 3;
                if (bay == 2) return Slot;
                return x == 2 ? SteelDark : x == 7 ? Steel : CabinetLit;
            }
            if (x >= 8 && x <= 11 && y >= 3 && y <= H - 8) return (y - 3) % 3 == 2 ? Slot : Off;
            return Cabinet;
        }));
    }

    static Sprite DrawSignalDish(System.Random dice)
    {
        const int W = 20, H = 24;
        var dish = new Vector2(9f, 15f);
        bool tilted = dice.Next(2) == 0;
        return Standing(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            bool Leg(float x0, float x1) { float t = y / 9f; return y <= 9 && Mathf.Abs(x - Mathf.Lerp(x0, x1, t)) < 0.7f; }
            if (Leg(3f, 9f) || Leg(15f, 9f) || (x == 9 && y <= 10)) return x == 9 ? Steel : SteelDark;
            float dx = (x - dish.x) / 8.5f, dy = (y - dish.y) / (tilted ? 5.5f : 4.5f);
            float d = dx * dx + dy * dy;
            if (d <= 1f)
            {
                if (d > 0.72f) return y > dish.y ? SteelLit : Steel;
                return y > dish.y + 1 ? new Color32(196, 202, 210, 255) : new Color32(160, 166, 176, 255);
            }
            if (Mathf.Abs((y - 15f) - (x - 9f) * 1.2f) < 0.8f && x >= 9 && x <= 13 && y >= 15) return SteelDark;
            if (x >= 12 && x <= 14 && y >= 19 && y <= 21) return Outline;
            return default;
        }));
    }

    static Sprite DrawCrates(System.Random dice)
    {
        const int W = 16, H = 18;
        Color32[] colors = { new Color32(164, 120, 74, 255), new Color32(74, 108, 150, 255), new Color32(96, 132, 92, 255), new Color32(112, 116, 126, 255) };
        Color32 a = colors[dice.Next(colors.Length)], b = colors[dice.Next(colors.Length)];
        bool third = dice.Next(2) == 0;
        return Standing(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            if (y <= 9 && x >= 1 && x <= 11) return Box(x - 1, y, 11, 10, a);
            if (y >= 10 && y <= 16 && x >= 3 && x <= 9) return Box(x - 3, y - 10, 7, 7, b);
            if (third && y <= 5 && x >= 12 && x <= 15) return Box(x - 12, y, 4, 6, b);
            return default;
        }));
    }

    static Color32 Box(int x, int y, int w, int h, Color32 color)
    {
        if (x == 0 || x == w - 1 || y == 0 || y == h - 1) return Outline;
        if (y == h - 2) return Lighter(color);
        if (y == h / 2) return Darker(color, 0.7f);
        return x == 1 ? Darker(color, 0.8f) : color;
    }

    static Sprite DrawTanks(System.Random dice)
    {
        const int W = 16, H = 22;
        Color32[] colors = { new Color32(200, 104, 52, 255), new Color32(70, 140, 92, 255), new Color32(70, 110, 170, 255), new Color32(170, 170, 176, 255) };
        int count = 2 + dice.Next(2);
        var tanks = new List<(int left, int height, Color32 color)>();
        for (int i = 0; i < count; i++) tanks.Add((1 + i * 5 - (count == 3 ? 0 : -2), 14 + dice.Next(6), colors[dice.Next(colors.Length)]));
        return Standing(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            for (int i = tanks.Count - 1; i >= 0; i--)
            {
                var (left, height, color) = tanks[i];
                int col = x - left;
                if (col < 0 || col > 4) continue;
                if (y > height + 2) continue;
                if (y > height) return col >= 1 && col <= 3 ? (y == height + 2 ? Outline : SteelDark) : default;
                if (col == 0 || col == 4 || y == 0) return Outline;
                if (y == height || y == height - 1) return col == 1 ? Lighter(color) : Darker(color, 0.85f);
                if (y == height / 2 || y == height / 2 + 1) return (col + y) % 2 == 0 ? Hazard : Outline;
                return col == 1 ? Lighter(color) : col == 3 ? Darker(color, 0.75f) : color;
            }
            return default;
        }));
    }

    static Sprite DrawDrum(System.Random dice)
    {
        const int W = 12, H = 15;
        Color32[] colors = { new Color32(196, 156, 52, 255), new Color32(70, 110, 170, 255), new Color32(150, 60, 50, 255) };
        Color32 color = colors[dice.Next(colors.Length)];
        return Standing(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            if (x == 0 || x == W - 1 || y == 0) return Outline;
            if (y >= H - 3) return y == H - 1 ? Outline : (x % 4 == 1 ? Outline : Darker(color, 0.7f));
            if (y == 4 || y == 9) return Darker(color, 0.6f);
            return x <= 2 ? Lighter(color) : x >= W - 3 ? Darker(color, 0.75f) : color;
        }));
    }

    static Sprite DrawToolbox()
    {
        const int W = 12, H = 9;
        var red = new Color32(176, 52, 44, 255);
        return Standing(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            if (y >= 7) return x >= 4 && x <= 7 ? (y == 8 ? Outline : (x == 4 || x == 7 ? Outline : default)) : default;
            if (x == 0 || x == W - 1 || y == 0 || y == 6) return Outline;
            if (y == 4) return SteelLit;
            return y == 5 ? Lighter(red) : red;
        }));
    }

    static Sprite DrawCables(System.Random dice)
    {
        const int W = 32, H = 16;
        Color32[] colors = { new Color32(30, 32, 36, 255), new Color32(150, 50, 44, 255), new Color32(60, 96, 160, 255), new Color32(196, 156, 52, 255) };
        int count = 2 + dice.Next(2);
        var cables = new List<(float y, float wave, float phase, Color32 color)>();
        for (int i = 0; i < count; i++)
            cables.Add((5f + i * 3f + (float)dice.NextDouble(), 1.5f + (float)dice.NextDouble() * 2f, (float)dice.NextDouble() * 6f, colors[dice.Next(colors.Length)]));
        return PixelArt.ToSprite(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            foreach (var (baseY, wave, phase, color) in cables)
            {
                float at = baseY + Mathf.Sin(x * 0.28f + phase) * wave;
                if (Mathf.Abs(y - at) < 0.75f) return color;
                if (Mathf.Abs(y - at + 1f) < 0.5f) return new Color32(0, 0, 0, 90);
            }
            if (x >= 15 && x <= 16 && y >= 3 && y <= 13) return SteelDark;
            return default;
        }), PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    static Sprite DrawScreen(int style, int frame)
    {
        const int W = 22, H = 14;
        Color32 trace = style == 0 ? new Color32(90, 255, 140, 255) : style == 1 ? new Color32(90, 220, 255, 255) : new Color32(255, 180, 70, 255);
        Color32 dim = new Color32((byte)(trace.r / 4), (byte)(trace.g / 4), (byte)(trace.b / 4), 255);
        return PixelArt.ToSprite(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            if (x == 0 || x == W - 1 || y == 0 || y == H - 1) return Outline;
            if (x == 1 || x == W - 2 || y == 1 || y == H - 2) return SteelDark;
            int sx = x - 2, sy = y - 2;
            if (sy % 4 == 0 && sx % 2 == 0) return dim;
            switch (style)
            {
                case 0:
                {
                    float at = 5f + Mathf.Sin((sx + frame * 3) * 0.6f) * 3f * Mathf.Sin((sx + frame * 3) * 0.13f);
                    if (Mathf.Abs(sy - at) < 0.8f) return trace;
                    break;
                }
                case 1:
                {
                    int bar = sx / 2;
                    int height = 1 + Mathf.Abs((bar * 7 + frame * 5 + bar * bar) % 9);
                    if (sx % 2 == 0 && sy < height) return sy == height - 1 ? trace : new Color32((byte)(trace.r * 0.6f), (byte)(trace.g * 0.6f), (byte)(trace.b * 0.6f), 255);
                    break;
                }
                default:
                {
                    var middle = new Vector2(8.5f, 4.5f);
                    Vector2 d = new Vector2(sx, sy) - middle;
                    if (d.magnitude > 5f) break;
                    float sweep = frame * 60f;
                    float angle = Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - sweep, 360f);
                    if (angle < 18f) return trace;
                    if (Mathf.Abs(d.magnitude - 4.5f) < 0.5f) return dim;
                    break;
                }
            }
            return new Color32(8, 12, 12, 255);
        }), PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    static Sprite DrawPegboard(System.Random dice)
    {
        const int W = 26, H = 16;
        var board = new Color32(120, 92, 62, 255);
        int layout = dice.Next(3);
        return PixelArt.ToSprite(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            if (x == 0 || x == W - 1 || y == 0 || y == H - 1) return Outline;
            int t = (x + layout * 7) % W;
            if (t >= 2 && t <= 3 && y >= 3 && y <= 12) return y >= 10 ? SteelLit : Steel;
            if (t >= 1 && t <= 4 && y >= 11 && y <= 12) return SteelLit;
            if (t >= 8 && t <= 9 && y >= 3 && y <= 10) return new Color32(150, 90, 50, 255);
            if (t >= 6 && t <= 11 && y >= 10 && y <= 12) return SteelDark;
            if (t >= 15 && t <= 15 && y >= 3 && y <= 8) return Steel;
            if (t >= 14 && t <= 16 && y >= 8 && y <= 12) return new Color32(200, 160, 50, 255);
            float dx = t - 21f, dy = y - 8f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r > 2f && r < 3.6f) return new Color32(180, 100, 50, 255);
            return x % 3 == 1 && y % 3 == 1 ? Darker(board, 0.55f) : board;
        }), PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    static Color32 Darker(Color32 c, float by) => new Color32((byte)(c.r * by), (byte)(c.g * by), (byte)(c.b * by), 255);
    static Color32 Lighter(Color32 c) => new Color32((byte)Mathf.Min(255, c.r + 40), (byte)Mathf.Min(255, c.g + 40), (byte)Mathf.Min(255, c.b + 40), 255);
}
