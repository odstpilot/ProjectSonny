using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The screen while crawling through a VentNetwork. A first-person view down the duct is ray cast every frame into a
// low-resolution texture, Wolfenstein style like FirstPersonView, but the only light is the player's flashlight, and it
// gives out a couple of cells ahead. Past that it's black, except where light from the rooms below comes up through a
// grate, which shows from much further off. A rib at every cell boundary is the only way to count how far you've come.
// Dented panels are crumpled floor plates, the way into a tight squeeze is a narrow collar with hazard stripes, and a
// drone hatch is a square in the ceiling with a red rim.
// Once the drone is out, its red lens shows in the dark from far away, and it throws red light on the duct around it.
// Over the view: the noise meter (the StealthMeter's colours, "?" and "!"), the walls closing in and the key to press
// during a squeeze, a hint line, and the fade through black.
// Built from code the first time a vent needs it, so there is no canvas to set up.
[DefaultExecutionOrder(100)] // draws after VentNetwork has moved the camera for the frame
public class VentView : MonoBehaviour
{
    const int SortingOrder = 100;           // over the HUD like LockerView, under the death screen (120)
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // The duct, in cells: each is one unit across and the duct is one unit tall. The player's eyes are in the middle.
    const float DuctHeight = 1f;
    const float EyeHeight = 0.5f;
    const float MaxDistance = 14f;
    const float NearClip = 0.05f;
    const float TexelSize = 8f;             // canvas pixels per texel: chunky, like the rest of the art
    const float FocalPerHeight = 0.75f;     // a wide view, about 100 degrees across a 16:9 screen
    const float ShakeMargin = 40f;          // the view runs this far past the screen edges, more than it shakes
    const float ShakePixels = 26f;

    // The flashlight.
    const float FlashReach = 2.6f;          // cells: nothing further is lit by it at all
    const float FlashStrength = 1.3f;
    const float BeamInner = 0.3f;           // how far from the middle of the screen the beam is full strength, in focal lengths
    const float BeamOuter = 1.1f;           // and where it has faded down to its spill
    const float BeamSpill = 0.3f;
    const float Ambient = 0.012f;

    // Light from the rooms below, coming up through a grate.
    const float GrateHalf = 0.36f;          // half the grate's width, as a fraction of its cell
    const float GrateBars = 7f;
    const float GrateReach = 1.5f;
    const float GrateStrength = 0.8f;
    const float GlowFog = 0.14f;            // light a grate gives off dims with distance, so far grates are faint but there

    // Surfaces.
    const float SeamWidth = 0.03f;          // the joins between floor and ceiling plates
    const float RibHalf = 0.06f;            // half the width of the rib at each cell boundary on the walls
    const float DentRadius = 0.42f;
    const float HatchHalf = 0.3f;

    // A squeeze's collar: the hole through it, across and up, as fractions of the duct.
    const float CollarGapMin = 0.3f;
    const float CollarGapMax = 0.7f;
    const float CollarBottom = 0.22f;
    const float CollarTop = 0.7f;

    // The drone, in cells.
    const float DroneHeight = 0.5f;         // hovers in the middle of the duct
    const float DroneBodyWidth = 0.34f;
    const float DroneBodyHeight = 0.14f;
    const float DroneRotorWidth = 0.46f;
    const float DroneRotorRise = 0.1f;      // how far above the body's middle the rotors spin
    const float DroneLensSize = 0.07f;
    const float DroneLightReach = 1.6f;
    const float DroneLightStrength = 0.9f;

    // The overlay.
    const float VignetteAlpha = 0.85f;
    const float MeterWidth = 620f;
    const float MeterHeight = 8f;
    const float MeterKickHeight = 14f;      // extra thickness for a moment after a noise
    const float MeterY = 0.07f;             // as a fraction of screen height
    const float SignPixels = 6f;            // canvas pixels per pixel of the "?" and "!" art
    const float SignAt = 0.35f;             // how full the meter has to be before a "?" shows
    const float SignPopTime = 0.18f;
    const float SqueezeSide = 0.3f;         // how far in the walls close from each side during a squeeze, as a fraction of the screen
    const float SqueezeTopBottom = 0.14f;

    static readonly Color32 Black = new Color32(0, 0, 0, 255);
    static readonly Color WallColor = new Color32(104, 110, 118, 255);
    static readonly Color RibColor = new Color32(132, 138, 146, 255);
    static readonly Color RivetColor = new Color32(176, 180, 186, 255);
    static readonly Color FloorColor = new Color32(86, 90, 96, 255);
    static readonly Color CeilingColor = new Color32(70, 74, 80, 255);
    static readonly Color SeamColor = new Color32(38, 41, 46, 255);
    static readonly Color DentColor = new Color32(52, 54, 58, 255);
    static readonly Color CreaseColor = new Color32(186, 190, 196, 255);
    static readonly Color CollarColor = new Color32(58, 56, 52, 255);
    static readonly Color CollarStripeColor = new Color32(176, 124, 36, 255);    // hazard paint round a squeeze
    static readonly Color HatchColor = new Color32(22, 22, 24, 255);
    static readonly Color HatchRimColor = new Color32(150, 36, 30, 255);
    static readonly Color GrateBarColor = new Color32(24, 24, 26, 255);
    static readonly Color GrateLightColor = new Color(0.95f, 0.66f, 0.38f);    // the station's amber lamps, from below
    static readonly Color FlashTint = new Color(1f, 0.96f, 0.86f);            // a warm torch
    static readonly Color EyeColor = new Color32(235, 240, 205, 255);
    static readonly Color DroneBodyColor = new Color32(70, 74, 80, 255);
    static readonly Color DroneRotorColor = new Color32(120, 126, 134, 255);
    static readonly Color DroneLensColor = new Color(1f, 0.24f, 0.16f);
    static readonly Color DroneLightColor = new Color(1f, 0.12f, 0.08f);

    // The StealthMeter's colours, so noise reads the same in here as being seen does out there.
    static readonly Color CalmColor = new Color(0.85f, 0.92f, 1f);
    static readonly Color WarningColor = new Color(1f, 0.75f, 0.25f);
    static readonly Color AlarmColor = new Color(1f, 0.25f, 0.15f);
    static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color KeyBoxColor = new Color(0.03f, 0.03f, 0.04f, 0.8f);
    static readonly Color MistakeColor = new Color(0.45f, 0.05f, 0.03f, 0.9f);
    static readonly Color KeyColor = new Color(1f, 0.8f, 0.5f);                // InteractPrompt's amber
    static readonly Color SqueezeWallColor = new Color32(6, 7, 9, 255);

    // The StealthMeter's signs. '#' is filled; a black outline is added around them.
    static readonly string[] AlertArt = { "###", "###", "###", "###", "###", "...", "###", "###" };
    static readonly string[] SearchArt = { ".###.", "##.##", "...##", "..##.", "..#..", "..#..", ".....", "..#.." };

    // How worked up the drone is, for the meter's sign and its lens.
    public enum Alert { None, Searching, Chasing }

    static VentView instance;

    private GameObject inside;
    private RectTransform viewRect;
    private RawImage viewImage;
    private Texture2D texture;
    private Color32[] pixels;
    private float[] beam;                   // how much of the flashlight's beam falls on each texel
    private float[] columnDepth;            // how far away the wall is in each column, for hiding things behind it
    private int width;
    private int height;
    private float focal;                    // texels
    private float horizon;                  // texel row at eye level

    private VentCell[,] cells;
    private Vector2 eye;
    private float angle;
    private float bob;
    private Vector2 forward;
    private Vector2 right;
    private float eyesDepth;
    private float torch = 1f;
    private float flickerTimer;
    private bool panic;
    private float shake;

    private bool droneOut;
    private Vector2 dronePosition;
    private float droneHover;
    private float droneGlow;                // how bright its lens is right now, 0 to 1
    private Alert alert;

    private Image noiseBar;
    private Image noiseSign;
    private Sprite searchSign;
    private Sprite alertSign;
    private float noise;
    private float shownNoise;
    private float noiseKick;
    private float signAge;

    private Image[] squeezeWalls;           // left, right, top, bottom
    private bool tight;
    private float squeezeClose;
    private RectTransform keyBox;
    private Image keyBack;
    private TextMeshProUGUI keyLetter;
    private Image keyTimeLeft;
    private bool showingKey;
    private float keyPop;
    private float mistakeFlash;

    private TextMeshProUGUI hint;
    private string steadyHint;
    private string flashHint;
    private float flashHintTimer;

    private Image fade;

    public static VentView Get(TMP_FontAsset font)
    {
        if (instance == null)
        {
            instance = new GameObject("VentView", typeof(RectTransform)).AddComponent<VentView>();
            instance.Build(font);
        }
        return instance;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (texture != null) Destroy(texture);
        DestroySign(searchSign);
        DestroySign(alertSign);
    }

    // Switches to the view from inside these ducts.
    public void Open(VentNetwork network)
    {
        cells = network.Cells;
        inside.SetActive(true);
        noise = shownNoise = noiseKick = 0f;
        tight = showingKey = panic = droneOut = false;
        alert = Alert.None;
        squeezeClose = mistakeFlash = shake = eyesDepth = bob = 0f;
        steadyHint = flashHint = null;
        flashHintTimer = 0f;
        torch = 1f;
        flickerTimer = Random.Range(2f, 5f);
        noiseSign.enabled = false;
        noiseSign.sprite = null;
        keyBox.gameObject.SetActive(false);

        // The texture is sized from the screen, so make sure the layout is current first.
        Canvas.ForceUpdateCanvases();
        Resize();
    }

    // Back to the normal view.
    public void Close()
    {
        cells = null;
        inside.SetActive(false);
    }

    // Where the player's eyes are on the map, in cells from its bottom left corner; which way they're looking, in
    // degrees clockwise from up the map; and how far the crawl has dipped their head.
    public void SetCamera(Vector2 eyePosition, float angleDegrees, float headBob)
    {
        eye = eyePosition;
        angle = angleDegrees;
        bob = headBob;
    }

    // Something in the dark looking back, this many cells straight ahead. 0 for nothing.
    public void SetEyes(float depth) => eyesDepth = depth;

    // The drone, if it's out, and where it is on the map.
    public void SetDrone(bool isOut, Vector2 position)
    {
        droneOut = isOut;
        dronePosition = position;
    }

    public void SetAlert(Alert level) => alert = level;

    // The flashlight stutters badly while something is coming.
    public void SetPanic(bool on)
    {
        panic = on;
        flickerTimer = 0f;
    }

    public void Shake(float strength) => shake = Mathf.Clamp01(Mathf.Max(shake, strength));

    // 0 to 1: how close the noise is to giving the player away.
    public void SetNoise(float amount) => noise = Mathf.Clamp01(amount);

    // The meter thickens for a moment, so a bang is felt even when it only adds a little.
    public void NoiseSpike(float strength) => noiseKick = Mathf.Max(noiseKick, strength);

    // The walls close in around the view while the player is wedged in a squeeze.
    public void SetTight(bool on) => tight = on;

    // The key to press next during a squeeze, and how much of its time is left, 0 to 1.
    public void ShowKey(char key, float timeLeft01)
    {
        showingKey = true;
        string letter = key.ToString();
        if (keyLetter.text != letter)
        {
            keyLetter.text = letter;
            keyPop = 1f;
        }
        keyTimeLeft.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(timeLeft01), 1f);
    }

    public void HideKey() => showingKey = false;

    // The key box flashes red and jolts.
    public void KeyMistake()
    {
        mistakeFlash = 1f;
        Shake(1f);
    }

    // A hint that stays while it's set, like how to climb out. Null for none.
    public void SetHint(string text) => steadyHint = text;

    // A hint that shows over the steady one for a few seconds.
    public void FlashHint(string text, float seconds)
    {
        flashHint = text;
        flashHintTimer = seconds;
    }

    // Unscaled time, so a pause or hit stop can't stall it.
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

    public void SetFade(float alpha)
    {
        fade.color = new Color(0f, 0f, 0f, alpha);
        fade.enabled = alpha > 0f;
    }

    void LateUpdate()
    {
        if (cells == null) return;

        float deltaTime = Time.unscaledDeltaTime;
        UpdateTorch(deltaTime);
        UpdateDroneLook();
        Resize();
        Render();
        DrawEyes();
        DrawDrone();
        texture.SetPixels32(pixels);
        texture.Apply(false);

        UpdateShake(deltaTime);
        UpdateNoiseMeter(deltaTime);
        UpdateSqueeze(deltaTime);
        UpdateHint(deltaTime);
    }

    // --- The duct ---

    // Makes the texture match the screen, and works out where the flashlight's beam falls on it.
    void Resize()
    {
        Vector2 size = viewRect.rect.size;
        int newWidth = Mathf.Max(16, Mathf.RoundToInt(size.x / TexelSize));
        int newHeight = Mathf.Max(9, Mathf.RoundToInt(size.y / TexelSize));
        if (texture != null && newWidth == width && newHeight == height) return;

        if (texture != null) Destroy(texture);
        width = newWidth;
        height = newHeight;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        pixels = new Color32[width * height];
        beam = new float[width * height];
        columnDepth = new float[width];
        viewImage.texture = texture;
        focal = FocalPerHeight * height;
        horizon = height * 0.5f;

        // A round beam from the middle of the screen, the same at every distance, since it comes from the player's eyes.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float across = (x + 0.5f - width * 0.5f) / focal;
                float up = (y + 0.5f - horizon) / focal;
                float full = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BeamOuter, BeamInner, Mathf.Sqrt(across * across + up * up)));
                beam[y * width + x] = Mathf.Lerp(BeamSpill, 1f, full);
            }
        }
    }

    void Render()
    {
        float radians = angle * Mathf.Deg2Rad;
        forward = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        right = new Vector2(forward.y, -forward.x);
        float eyeHeight = EyeHeight + bob;

        for (int x = 0; x < width; x++)
        {
            // The ray moves one unit forward per unit along it, so distance along it is depth straight ahead: no fisheye.
            Vector2 ray = forward + right * ((x + 0.5f - width * 0.5f) / focal);
            float depth = Cast(ray, out Vector2 hit, out bool facesX, out float collarDepth, out Vector2 collarHit, out bool collarFacesX);
            columnDepth[x] = depth;
            float wallTop = horizon + (DuctHeight - eyeHeight) / depth * focal;
            float wallBottom = horizon - eyeHeight / depth * focal;

            bool collar = collarDepth < depth;
            bool besideHole = false;
            if (collar)
            {
                float across = Mathf.Repeat(collarFacesX ? collarHit.y : collarHit.x, 1f);
                besideHole = across < CollarGapMin || across > CollarGapMax;
            }

            for (int y = 0; y < height; y++)
            {
                int index = y * width + x;
                float row = y + 0.5f;
                float light = beam[index] * torch;

                // The collar is nearer than anything behind it, except the hole through it.
                if (collar)
                {
                    float collarHeight = eyeHeight + (row - horizon) * collarDepth / focal;
                    if (collarHeight >= 0f && collarHeight <= DuctHeight
                        && (besideHole || collarHeight < CollarBottom || collarHeight > CollarTop))
                    {
                        pixels[index] = CollarPixel(collarHit, collarFacesX, collarHeight, collarDepth, light);
                        continue;
                    }
                }

                if (row < wallBottom)
                {
                    float distance = eyeHeight * focal / (horizon - row);
                    pixels[index] = PlatePixel(eye + ray * distance, distance, light, false);
                }
                else if (row > wallTop)
                {
                    float distance = (DuctHeight - eyeHeight) * focal / (row - horizon);
                    pixels[index] = PlatePixel(eye + ray * distance, distance, light, true);
                }
                else if (depth >= MaxDistance)
                {
                    pixels[index] = Black;
                }
                else
                {
                    float wallHeight = eyeHeight + (row - horizon) * depth / focal;
                    pixels[index] = WallPixel(hit, facesX, wallHeight, depth, light);
                }
            }
        }
    }

    // Steps the ray from cell to cell until it enters a solid one. Returns the depth, or MaxDistance if nothing is that
    // close. Also finds the first place it goes from open duct into a squeeze, where the collar stands.
    float Cast(Vector2 ray, out Vector2 hit, out bool facesX, out float collarDepth, out Vector2 collarHit, out bool collarFacesX)
    {
        int cx = Mathf.FloorToInt(eye.x);
        int cy = Mathf.FloorToInt(eye.y);
        int stepX = ray.x < 0f ? -1 : 1;
        int stepY = ray.y < 0f ? -1 : 1;
        float deltaX = ray.x != 0f ? Mathf.Abs(1f / ray.x) : Mathf.Infinity;
        float deltaY = ray.y != 0f ? Mathf.Abs(1f / ray.y) : Mathf.Infinity;
        float nextX = ray.x == 0f ? Mathf.Infinity : (ray.x < 0f ? eye.x - cx : cx + 1f - eye.x) * deltaX;
        float nextY = ray.y == 0f ? Mathf.Infinity : (ray.y < 0f ? eye.y - cy : cy + 1f - eye.y) * deltaY;

        bool inSqueeze = CellAt(cx, cy) == VentCell.Squeeze;
        collarDepth = Mathf.Infinity;
        collarHit = Vector2.zero;
        collarFacesX = false;

        float t = 0f;
        facesX = false;
        while (t < MaxDistance)
        {
            if (nextX < nextY)
            {
                t = nextX;
                nextX += deltaX;
                cx += stepX;
                facesX = true;
            }
            else
            {
                t = nextY;
                nextY += deltaY;
                cy += stepY;
                facesX = false;
            }

            VentCell cell = CellAt(cx, cy);
            if (cell == VentCell.Solid)
            {
                t = Mathf.Clamp(t, NearClip, MaxDistance);
                hit = eye + ray * t;
                return t;
            }

            bool squeeze = cell == VentCell.Squeeze;
            if (squeeze && !inSqueeze && float.IsInfinity(collarDepth))
            {
                collarDepth = Mathf.Max(t, NearClip);
                collarHit = eye + ray * t;
                collarFacesX = facesX;
            }
            inSqueeze = squeeze;
        }

        hit = eye + ray * MaxDistance;
        return MaxDistance;
    }

    VentCell CellAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= cells.GetLength(0) || y >= cells.GetLength(1)) return VentCell.Solid;
        return cells[x, y];
    }

    // A floor or ceiling plate at this point on the map.
    Color32 PlatePixel(Vector2 point, float distance, float light, bool ceiling)
    {
        int cx = Mathf.FloorToInt(point.x);
        int cy = Mathf.FloorToInt(point.y);
        float u = point.x - cx;
        float v = point.y - cy;
        VentCell cell = CellAt(cx, cy);
        float across = Mathf.Abs(u - 0.5f);
        float along = Mathf.Abs(v - 0.5f);

        // A grate in the floor: bars, with the lamps of the room below between them. It glows torch or no torch.
        if (!ceiling && cell == VentCell.Grate && across < GrateHalf && along < GrateHalf)
        {
            bool frame = across > GrateHalf - 0.04f || along > GrateHalf - 0.04f;
            if (frame || Mathf.Repeat(u * GrateBars, 1f) < 0.4f)
                return Shade(GrateBarColor, point, 0f, distance, light);

            Color below = GrateLightColor * Mathf.Exp(-GlowFog * distance);
            below.a = 1f;
            return below;
        }

        Color surface = ceiling ? CeilingColor : FloorColor;
        if (u < SeamWidth || v < SeamWidth || u > 1f - SeamWidth || v > 1f - SeamWidth)
            surface = SeamColor;
        else if (ceiling && cell == VentCell.Hatch && across < HatchHalf && along < HatchHalf)
            surface = across > HatchHalf - 0.05f || along > HatchHalf - 0.05f ? HatchRimColor : HatchColor;
        else if (!ceiling && cell == VentCell.Dent)
            surface = DentPixel(u, v);
        return Shade(surface, point, ceiling ? DuctHeight : 0f, distance, light);
    }

    // A crumpled plate: sunk and dark in the middle, with bright creases where the metal folded.
    static Color DentPixel(float u, float v)
    {
        float du = u - 0.5f;
        float dv = v - 0.5f;
        float r = Mathf.Sqrt(du * du + dv * dv);
        if (r > DentRadius) return FloorColor;

        float crease = Mathf.Min(Mathf.Abs(du - dv * 0.4f), Mathf.Abs(dv + du * 0.7f + 0.08f));
        if (crease < 0.015f + 0.03f * (1f - r / DentRadius)) return CreaseColor;
        return Color.Lerp(DentColor, FloorColor, r / DentRadius);
    }

    Color32 WallPixel(Vector2 hit, bool facesX, float wallHeight, float distance, float light)
    {
        float along = Mathf.Repeat(facesX ? hit.y : hit.x, 1f);
        Color surface = WallColor;
        if (wallHeight < 0.03f || wallHeight > DuctHeight - 0.03f)
            surface = SeamColor;    // the corners where the wall meets the floor and ceiling
        else if (along < RibHalf || along > 1f - RibHalf)
            surface = Mathf.Abs(Mathf.Repeat(wallHeight, 0.25f) - 0.125f) < 0.02f ? RivetColor : RibColor;
        else if (Mathf.Abs(wallHeight - 0.5f) < 0.01f)
            surface = SeamColor;

        // Walls facing along x are a little darker, so corners read.
        if (facesX) surface *= 0.85f;
        return Shade(surface, hit, wallHeight, distance, light);
    }

    Color32 CollarPixel(Vector2 at, bool facesX, float collarHeight, float distance, float light)
    {
        float across = Mathf.Repeat(facesX ? at.y : at.x, 1f);
        // How far outside the hole this is, so there's a dark lip right around it.
        float fromHole = Mathf.Max(Mathf.Max(CollarGapMin - across, across - CollarGapMax),
            Mathf.Max(CollarBottom - collarHeight, collarHeight - CollarTop));
        Color surface = fromHole < 0.035f ? SeamColor
            : Mathf.Repeat((across + collarHeight) * 4f, 1f) < 0.5f ? CollarStripeColor
            : CollarColor;
        return Shade(surface, at, collarHeight, distance, light);
    }

    // Lit by the flashlight, if it's near enough, by any grate close by, and by the drone's lens.
    Color32 Shade(Color surface, Vector2 point, float surfaceHeight, float distance, float light)
    {
        float flash = 0f;
        if (distance < FlashReach)
        {
            float falloff = 1f - distance / FlashReach;
            flash = FlashStrength * light * falloff * falloff;
        }
        float fog = Mathf.Exp(-GlowFog * distance);
        float glow = GrateGlow(point, surfaceHeight) * fog;

        Color color = surface * (FlashTint * flash + GrateLightColor * glow) + surface * Ambient;
        float red = DroneLight(point, surfaceHeight);
        if (red > 0f) color += surface * DroneLightColor * (red * fog);
        color.a = 1f;
        return color;
    }

    // Light coming up through the grates around a point: strongest on the floor right over one.
    float GrateGlow(Vector2 point, float surfaceHeight)
    {
        int cx = Mathf.FloorToInt(point.x);
        int cy = Mathf.FloorToInt(point.y);
        float glow = 0f;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (CellAt(cx + dx, cy + dy) != VentCell.Grate) continue;
                float ox = point.x - (cx + dx + 0.5f);
                float oy = point.y - (cy + dy + 0.5f);
                float reach = Mathf.Clamp01(1f - Mathf.Sqrt(ox * ox + oy * oy + surfaceHeight * surfaceHeight * 0.5f) / GrateReach);
                glow += reach * reach;
            }
        }
        return glow * GrateStrength;
    }

    // Red light from the drone's lens. It gets through the seams, so it can show on a wall before the drone comes round.
    float DroneLight(Vector2 point, float surfaceHeight)
    {
        if (!droneOut) return 0f;
        float dx = point.x - dronePosition.x;
        float dy = point.y - dronePosition.y;
        float dz = surfaceHeight - (DroneHeight + droneHover);
        float reach = 1f - Mathf.Sqrt(dx * dx + dy * dy + dz * dz) / DroneLightReach;
        return reach > 0f ? DroneLightStrength * reach * reach * droneGlow : 0f;
    }

    // Something in the dark looking back: two eyes that shine without any light on them.
    void DrawEyes()
    {
        if (eyesDepth <= NearClip || eyesDepth > columnDepth[width / 2]) return;

        float scale = focal / eyesDepth;
        float middle = width * 0.5f;
        Color32 glint = Color.Lerp(Color.black, EyeColor, Mathf.Exp(-GlowFog * eyesDepth * 0.5f));
        float eyeRow = horizon + (0.52f - (EyeHeight + bob)) * scale;
        float gap = 0.12f * scale;
        float radius = Mathf.Max(0.5f, 0.025f * scale);
        Ellipse(middle - gap, eyeRow, radius, radius, glint, eyesDepth);
        Ellipse(middle + gap, eyeRow, radius, radius, glint, eyesDepth);
    }

    // The lens pulses while the drone searches and strobes while it chases; it bobs as it hovers.
    void UpdateDroneLook()
    {
        float time = Time.unscaledTime;
        droneHover = Mathf.Sin(time * 4f) * 0.03f;
        droneGlow = alert == Alert.Chasing ? (Mathf.Repeat(time * 7f, 1f) < 0.6f ? 1f : 0.55f)
            : 0.55f + 0.45f * Mathf.Sin(time * 3f);
    }

    // A small maintenance drone: a flat body, a blur of rotors over it, and a red lens that shines by itself.
    void DrawDrone()
    {
        if (!droneOut) return;

        Vector2 toDrone = dronePosition - eye;
        float depth = Vector2.Dot(toDrone, forward);
        if (depth < NearClip || depth > MaxDistance) return;

        float scale = focal / depth;
        float middle = width * 0.5f + Vector2.Dot(toDrone, right) * scale;
        float row = horizon + (DroneHeight + droneHover - (EyeHeight + bob)) * scale;

        float flash = 0f;
        if (depth < FlashReach)
        {
            float falloff = 1f - depth / FlashReach;
            flash = FlashStrength * torch * falloff * falloff;
        }
        float fog = Mathf.Exp(-GlowFog * depth);
        Color lit = FlashTint * flash + DroneLightColor * (0.35f * droneGlow * fog);

        Color body = DroneBodyColor * lit + DroneBodyColor * Ambient;
        body.a = 1f;
        Color rotor = DroneRotorColor * lit + DroneRotorColor * Ambient;
        rotor.a = 1f;
        Color lens = Color.Lerp(DroneLensColor * 0.3f, DroneLensColor, droneGlow) * fog;
        lens.a = 1f;

        float spin = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 40f));
        Ellipse(middle, row + DroneRotorRise * scale, DroneRotorWidth * 0.5f * scale * spin, 0.015f * scale, rotor, depth);
        Ellipse(middle, row, DroneBodyWidth * 0.5f * scale, DroneBodyHeight * 0.5f * scale, body, depth);
        Ellipse(middle, row - 0.01f * scale, DroneLensSize * 0.5f * scale, DroneLensSize * 0.5f * scale, lens, depth);
    }

    // A filled ellipse at this depth, hidden in any column where a wall is nearer.
    void Ellipse(float centerX, float centerY, float radiusX, float radiusY, Color32 color, float depth)
    {
        radiusX = Mathf.Max(0.5f, radiusX);
        radiusY = Mathf.Max(0.5f, radiusY);
        int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radiusX));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centerX + radiusX));
        int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radiusY));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centerY + radiusY));
        for (int x = minX; x <= maxX; x++)
        {
            if (columnDepth[x] < depth) continue;
            float dx = (x + 0.5f - centerX) / radiusX;
            for (int y = minY; y <= maxY; y++)
            {
                float dy = (y + 0.5f - centerY) / radiusY;
                if (dx * dx + dy * dy <= 1f) pixels[y * width + x] = color;
            }
        }
    }

    // The flashlight stutters now and then: a short dip, sometimes twice in a row. Much worse while something is coming.
    void UpdateTorch(float deltaTime)
    {
        flickerTimer -= deltaTime;
        if (flickerTimer > 0f) return;

        if (torch < 1f)
        {
            torch = 1f;
            flickerTimer = panic ? Random.Range(0.03f, 0.12f) : Random.value < 0.35f ? 0.07f : Random.Range(3f, 8f);
        }
        else
        {
            torch = panic ? Random.Range(0f, 0.3f) : Random.Range(0.25f, 0.55f);
            flickerTimer = Random.Range(0.04f, 0.1f);
        }
    }

    // --- The overlay ---

    void UpdateShake(float deltaTime)
    {
        shake = Mathf.MoveTowards(shake, 0f, 3f * deltaTime);
        float strength = shake * shake * ShakePixels;
        viewRect.anchoredPosition = strength > 0f ? Random.insideUnitCircle * strength : Vector2.zero;
    }

    void UpdateNoiseMeter(float deltaTime)
    {
        shownNoise = Mathf.MoveTowards(shownNoise, noise, 3f * deltaTime);
        noiseKick = Mathf.MoveTowards(noiseKick, 0f, 3f * deltaTime);

        // Pale while it's quiet, amber as it fills, red once it's full - and pulsing while it's full or the drone is chasing.
        bool alarmed = noise >= 1f || alert == Alert.Chasing;
        Color color = shownNoise < 0.5f
            ? Color.Lerp(CalmColor, WarningColor, shownNoise * 2f)
            : Color.Lerp(WarningColor, AlarmColor, (shownNoise - 0.5f) * 2f);
        if (alert == Alert.Chasing) color = AlarmColor;
        float pulse = alarmed ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 12f) : 1f;
        color.a = Mathf.Lerp(0.5f, 1f, shownNoise) * pulse;
        noiseBar.color = color;
        noiseBar.rectTransform.sizeDelta = new Vector2(MeterWidth * shownNoise, MeterHeight + MeterKickHeight * noiseKick);

        // A "!" while it's full or the drone is chasing, a "?" while it searches or the noise is getting up there.
        // Each pops in a little too big whenever it changes.
        Sprite wanted = alarmed ? alertSign
            : alert == Alert.Searching || shownNoise >= SignAt ? searchSign
            : null;
        if (noiseSign.sprite != wanted)
        {
            noiseSign.sprite = wanted;
            noiseSign.enabled = wanted != null;
            signAge = 0f;
            if (wanted != null) noiseSign.rectTransform.sizeDelta = new Vector2(wanted.rect.width, wanted.rect.height) * SignPixels;
        }
        if (wanted == null) return;

        signAge += deltaTime;
        float pop = signAge / SignPopTime;
        float scale = pop >= 1f ? 1f
            : pop < 0.5f ? Mathf.Lerp(0.4f, 1.35f, pop * 2f)
            : Mathf.Lerp(1.35f, 1f, (pop - 0.5f) * 2f);
        noiseSign.rectTransform.localScale = Vector3.one * scale;
        Color signColor = alert == Alert.Searching && !alarmed ? WarningColor : color;
        signColor.a = Mathf.Clamp01(Mathf.Max(color.a, 0.6f) * 1.6f);
        noiseSign.color = signColor;
    }

    void UpdateSqueeze(float deltaTime)
    {
        squeezeClose = Mathf.MoveTowards(squeezeClose, tight ? 1f : 0f, 3f * deltaTime);
        float close = Mathf.SmoothStep(0f, 1f, squeezeClose);
        squeezeWalls[0].rectTransform.anchorMax = new Vector2(SqueezeSide * close, 1f);
        squeezeWalls[1].rectTransform.anchorMin = new Vector2(1f - SqueezeSide * close, 0f);
        squeezeWalls[2].rectTransform.anchorMin = new Vector2(0f, 1f - SqueezeTopBottom * close);
        squeezeWalls[3].rectTransform.anchorMax = new Vector2(1f, SqueezeTopBottom * close);
        foreach (Image wall in squeezeWalls)
            wall.enabled = close > 0f;

        if (keyBox.gameObject.activeSelf != showingKey) keyBox.gameObject.SetActive(showingKey);
        if (!showingKey) return;

        mistakeFlash = Mathf.MoveTowards(mistakeFlash, 0f, 3f * deltaTime);
        keyPop = Mathf.MoveTowards(keyPop, 0f, 8f * deltaTime);
        keyBack.color = Color.Lerp(KeyBoxColor, MistakeColor, mistakeFlash);
        keyLetter.color = Color.Lerp(KeyColor, AlarmColor, mistakeFlash);
        float jolt = mistakeFlash * 14f;
        keyBox.anchoredPosition = jolt > 0f ? Random.insideUnitCircle * jolt : Vector2.zero;
        keyBox.localScale = Vector3.one * (1f + 0.25f * keyPop);
    }

    void UpdateHint(float deltaTime)
    {
        if (flashHintTimer > 0f) flashHintTimer -= deltaTime;
        string text = flashHintTimer > 0f ? flashHint : steadyHint;
        hint.enabled = !string.IsNullOrEmpty(text);
        if (hint.enabled && hint.text != text) hint.text = text;
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

        RectTransform insideRect = Fill(NewRect("Inside", transform));
        inside = insideRect.gameObject;

        // Solid black under everything, so the level never shows around the edges whatever the screen size.
        Block("Backing", insideRect, 0f, 0f, 1f, 1f, Color.black);

        viewRect = Fill(NewRect("Duct", insideRect));
        viewRect.offsetMin = new Vector2(-ShakeMargin, -ShakeMargin);
        viewRect.offsetMax = new Vector2(ShakeMargin, ShakeMargin);
        viewImage = viewRect.gameObject.AddComponent<RawImage>();
        viewImage.raycastTarget = false;

        // The walls of a squeeze, closed right back to the screen edges until one is needed.
        squeezeWalls = new[]
        {
            Block("Squeeze Left", insideRect, 0f, 0f, 0f, 1f, SqueezeWallColor),
            Block("Squeeze Right", insideRect, 1f, 0f, 1f, 1f, SqueezeWallColor),
            Block("Squeeze Top", insideRect, 0f, 1f, 1f, 1f, SqueezeWallColor),
            Block("Squeeze Bottom", insideRect, 0f, 0f, 1f, 0f, SqueezeWallColor),
        };

        var vignette = Fill(NewRect("Vignette", insideRect)).gameObject.AddComponent<RawImage>();
        vignette.texture = CombatSprites.VignetteTexture;
        vignette.color = new Color(0f, 0f, 0f, VignetteAlpha);
        vignette.raycastTarget = false;

        // The noise meter along the bottom: a bar that grows out from the middle, with its sign above.
        Image track = Block("Noise Track", insideRect, 0.5f, MeterY, 0.5f, MeterY, TrackColor);
        track.rectTransform.sizeDelta = new Vector2(MeterWidth, MeterHeight);
        noiseBar = Block("Noise", insideRect, 0.5f, MeterY, 0.5f, MeterY, CalmColor);
        noiseBar.rectTransform.sizeDelta = new Vector2(0f, MeterHeight);
        noiseSign = Block("Noise Sign", insideRect, 0.5f, MeterY, 0.5f, MeterY, Color.white);
        noiseSign.rectTransform.pivot = new Vector2(0.5f, 0f);
        noiseSign.rectTransform.anchoredPosition = new Vector2(0f, 24f);
        noiseSign.enabled = false;
        searchSign = MakeSign(SearchArt);
        alertSign = MakeSign(AlertArt);

        // Letters and spaces only: the game's HUD font has no punctuation.
        TextMeshProUGUI meterLabel = Label("Noise Label", insideRect, "NOISE", 24f, font);
        meterLabel.color = new Color(0.8f, 0.84f, 0.9f, 0.45f);
        meterLabel.rectTransform.anchorMin = meterLabel.rectTransform.anchorMax = new Vector2(0.5f, MeterY);
        meterLabel.rectTransform.sizeDelta = new Vector2(120f, 40f);
        meterLabel.rectTransform.anchoredPosition = new Vector2(-MeterWidth * 0.5f - 70f, 0f);

        // The key to press during a squeeze, with how long is left to press it underneath.
        keyBox = NewRect("Squeeze Key", insideRect);
        keyBox.anchorMin = keyBox.anchorMax = new Vector2(0.5f, 0.3f);
        keyBox.sizeDelta = new Vector2(170f, 170f);
        keyBack = keyBox.gameObject.AddComponent<Image>();
        keyBack.color = KeyBoxColor;
        keyBack.raycastTarget = false;
        keyLetter = Label("Letter", keyBox, "W", 120f, font);
        Fill(keyLetter.rectTransform);
        keyLetter.color = KeyColor;
        Image timeTrack = Block("Time Track", keyBox, 0f, 0f, 1f, 0f, TrackColor);
        timeTrack.rectTransform.offsetMin = new Vector2(0f, -24f);
        timeTrack.rectTransform.offsetMax = new Vector2(0f, -12f);
        keyTimeLeft = Block("Time Left", timeTrack.transform, 0f, 0f, 1f, 1f, KeyColor);
        keyBox.gameObject.SetActive(false);

        hint = Label("Hint", insideRect, "", 30f, font);
        hint.color = new Color(0.8f, 0.84f, 0.9f, 0.6f);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.14f);
        hint.rectTransform.sizeDelta = new Vector2(1000f, 50f);
        hint.enabled = false;

        // Fade, on top of everything.
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

    static TextMeshProUGUI Label(string name, Transform parent, string text, float size, TMP_FontAsset font)
    {
        var label = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // White where the art is filled, with a black outline so it reads on anything. The Image colour tints it.
    static Sprite MakeSign(string[] rows)
    {
        int artWidth = 0;
        foreach (string row in rows)
            artWidth = Mathf.Max(artWidth, row.Length);
        int signWidth = artWidth + 2;
        int signHeight = rows.Length + 2;

        bool Filled(int x, int y) => y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length && rows[y][x] == '#';

        var art = new Color32[signWidth * signHeight];
        for (int y = 0; y < signHeight; y++)
        {
            for (int x = 0; x < signWidth; x++)
            {
                int artX = x - 1;
                int artY = y - 1;
                Color32 color = default;
                if (Filled(artX, artY))
                {
                    color = new Color32(255, 255, 255, 255);
                }
                else
                {
                    for (int dy = -1; dy <= 1 && color.a == 0; dy++)
                        for (int dx = -1; dx <= 1 && color.a == 0; dx++)
                            if (Filled(artX + dx, artY + dy)) color = new Color32(0, 0, 0, 255);
                }
                art[(signHeight - 1 - y) * signWidth + x] = color; // row 0 of the art is the top
            }
        }

        var signTexture = new Texture2D(signWidth, signHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        signTexture.SetPixels32(art);
        signTexture.Apply(false);
        return Sprite.Create(signTexture, new Rect(0f, 0f, signWidth, signHeight), new Vector2(0.5f, 0.5f), signHeight);
    }

    static void DestroySign(Sprite sign)
    {
        if (sign == null) return;
        Destroy(sign.texture);
        Destroy(sign);
    }
}
