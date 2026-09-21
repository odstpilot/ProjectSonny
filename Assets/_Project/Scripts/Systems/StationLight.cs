using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A light on the station: a wall lamp, an alarm, or sunlight through a window.
//   Steady, Flicker: an ordinary lamp, or one with a failing tube.   Alarm: pulses.
//   Sunlight: breathes slowly and swells with SunBoost as the flare gets closer. FlareProgress (0 to 1) turns every
//   window from orange toward white hot over the whole level, so the flare works as a countdown without a timer.
// A lamp with swing sways on its bracket when the station rumbles, its pool of light swinging across the floor. One with
// dropOnBreak can come away when it's smashed and fall as a light fitting (FallingDebris).
// Every light stutters while the station rumbles. Breakable lights can be smashed, by debris landing under them, by a hard
// rumble, or by Break() from a script: they pop, spray sparks and glass, stutter out, and keep sputtering sparks after.
// PowerOff puts a light out with a flicker instead, like the power failing.
// With drawFixture ticked a light draws placeholder art for itself: a caged lamp that goes dark and cracked when broken,
// or, for sunlight, the glare that shows through the window glass.
[RequireComponent(typeof(Light2D))]
public class StationLight : MonoBehaviour
{
    const float StutterStep = 0.06f;    // seconds between changes while stuttering, so it doesn't depend on frame rate
    const float DyingTime = 0.9f;       // a smashed light stutters this long before it's dark
    const float PowerFlickerTime = 0.6f;

    static readonly Color GlassLit = new Color(1f, 0.88f, 0.6f);
    static readonly Color GlassDark = new Color(0.2f, 0.16f, 0.12f);
    static readonly Color SunDeep = new Color(1f, 0.38f, 0.1f);
    static readonly Color SunWhite = new Color(1f, 0.9f, 0.62f);
    static readonly Color FlareWhite = new Color(1f, 0.97f, 0.9f);
    static readonly Color SparkBright = new Color(1f, 0.95f, 0.7f);
    static readonly Color SparkHot = new Color(1f, 0.5f, 0.12f);
    static readonly Color Shards = new Color(0.95f, 0.85f, 0.62f);

    // The bulkhead lamp, drawn at the tileset's pixel size: a steel housing bolted to the wall, a cage of bars over its
    // window, and the glass behind them.
    const int FixtureWidth = 24;
    const int FixtureHeight = 14;
    static readonly Color32 Housing = new Color32(74, 80, 88, 255);
    static readonly Color32 Rim = new Color32(150, 156, 164, 255);
    static readonly Color32 Bars = new Color32(118, 124, 132, 255);
    static readonly Color32 Bolt = new Color32(46, 50, 56, 255);
    static readonly Color32 Ink = new Color32(18, 20, 26, 255);

    public enum Mode { Steady, Flicker, Alarm, Sunlight }

    public Mode mode = Mode.Steady;
    public bool startOn = true;
    [Tooltip("Already smashed when the level starts: dark, and spitting sparks now and then.")]
    public bool startBroken;
    [Tooltip("Alarm: pulses per second.")]
    public float pulseRate = 1.2f;
    [Tooltip("Flicker: roughly how many times a second it cuts out.")]
    public float flickerRate = 0.6f;
    [Tooltip("How badly it stutters when the station rumbles, 0 to 1.")]
    [Range(0f, 1f)] public float rumbleStutter = 0.7f;
    [Tooltip("Optional sprite that brightens and dims with the light, like the lamp itself.")]
    public SpriteRenderer bulb;

    [Tooltip("How far the light swings to each side when the station rumbles, in world units. 0 keeps it still.")]
    public float swing;
    [Tooltip("Swings per second.")]
    public float swingRate = 0.9f;

    [Header("Breaking")]
    public bool breakable = true;
    [Tooltip("Debris landing within this distance of the light can smash it.")]
    public float breakRadius = 2.2f;
    [Tooltip("Chance a hard rumble smashes it, when it's on screen.")]
    [Range(0f, 1f)] public float rumbleBreakChance = 0.15f;
    public bool sparkWhenBroken = true;
    [Tooltip("Chance a smashed lamp comes off the wall and falls to the floor below.")]
    [Range(0f, 1f)] public float dropOnBreak;
    public AudioClip breakClip;
    [Range(0f, 1f)] public float breakVolume = 0.8f;

    [Header("Placeholder Art")]
    [Tooltip("Draw a lamp fixture (or, for sunlight, the glare through the window) at fixtureOffset.")]
    public bool drawFixture;
    [Tooltip("Where the fixture or window is, relative to the light.")]
    public Vector2 fixtureOffset;
    [Tooltip("Sunlight: the size of the window the glare shows through.")]
    public Vector2 windowSize = new Vector2(3f, 3f);

    // Sunlight is multiplied by this: 1 normally, higher as the flare arrives.
    public static float SunBoost { get; set; } = 1f;
    // 0 to 1: how close the flare is. Sunlight whitens and brightens with it. Set by the level.
    public static float FlareProgress { get; set; }

    public bool IsOn { get; private set; }
    public bool IsBroken { get; private set; }
    public Vector2 FixturePoint => (Vector2)transform.position + fixtureOffset;

    static float lastRumbleBreak = -10f;
    static Sprite frameSprite;
    static Sprite glassSprite;
    static Sprite brokenGlassSprite;
    static Sprite haloSprite;
    static Sprite glareSprite;

    private Light2D lamp;
    private float baseIntensity;
    private Color bulbColor;
    private SpriteRenderer glass;
    private SpriteRenderer halo;
    private SpriteRenderer glare;
    private SpriteRenderer frame;
    private Color baseColor;
    private Vector3 home;
    private float swingEnergy;      // 0 still, 1 swinging as hard as it goes
    private float swingPhase;
    private Coroutine routine;
    private float power = 1f;       // dips while the power is failing or coming back
    private float flash;            // the pop when it breaks, and sparks sputtering after
    private float nextSputter;
    private float nextCut;
    private float cutUntil;
    private float stutterUntil;
    private float nextStutterStep;
    private float stutterLevel = 1f;

    static Sprite FrameSprite => frameSprite != null ? frameSprite : (frameSprite = DrawFrame().ToSprite(new Vector2(0.5f, 0.5f)));
    static Sprite GlassSprite => glassSprite != null ? glassSprite : (glassSprite = DrawGlass(false).ToSprite(new Vector2(0.5f, 0.5f)));
    static Sprite BrokenGlassSprite => brokenGlassSprite != null ? brokenGlassSprite : (brokenGlassSprite = DrawGlass(true).ToSprite(new Vector2(0.5f, 0.5f)));

    static bool InWindow(int x, int y) => x >= 4 && x <= FixtureWidth - 5 && y >= 4 && y <= FixtureHeight - 5;
    static bool OnBar(int x, int y) => (x - 4) % 5 == 4 || y == FixtureHeight / 2;

    static PixelCanvas DrawFrame()
    {
        var canvas = new PixelCanvas(FixtureWidth, FixtureHeight);
        canvas.FillRect(1, 1, FixtureWidth - 2, FixtureHeight - 2, Housing);
        canvas.FillRect(2, FixtureHeight - 2, FixtureWidth - 3, FixtureHeight - 2, Rim);
        foreach (Vector2Int corner in new[] { new Vector2Int(1, 1), new Vector2Int(FixtureWidth - 2, 1), new Vector2Int(1, FixtureHeight - 2), new Vector2Int(FixtureWidth - 2, FixtureHeight - 2) })
            canvas.Clear(corner.x, corner.y);
        foreach (Vector2Int bolt in new[] { new Vector2Int(2, 2), new Vector2Int(FixtureWidth - 3, 2), new Vector2Int(2, FixtureHeight - 4), new Vector2Int(FixtureWidth - 3, FixtureHeight - 4) })
            canvas.Set(bolt.x, bolt.y, Bolt);

        canvas.Bevel(1.2f, 0.75f);
        canvas.Outline(Ink);

        // The window, cut after the outline so it isn't inked in: open, but for the cage bars across it.
        for (int y = 0; y < FixtureHeight; y++)
            for (int x = 0; x < FixtureWidth; x++)
                if (InWindow(x, y)) canvas.Set(x, y, OnBar(x, y) ? Bars : default);
        return canvas;
    }

    // White, so the light's colour can tint it. Broken, only a few shards are left round the edge of the window.
    static PixelCanvas DrawGlass(bool broken)
    {
        var canvas = new PixelCanvas(FixtureWidth, FixtureHeight);
        var white = new Color32(255, 255, 255, 255);
        var shards = new System.Random(7);
        for (int y = 0; y < FixtureHeight; y++)
        {
            for (int x = 0; x < FixtureWidth; x++)
            {
                if (!InWindow(x, y) || OnBar(x, y)) continue;
                bool edge = x == 4 || x == FixtureWidth - 5 || y == 4 || y == FixtureHeight - 5;
                if (!broken || (edge && shards.NextDouble() < 0.45)) canvas.Set(x, y, white);
            }
        }
        return canvas;
    }
    static Sprite HaloSprite
    {
        get
        {
            if (haloSprite == null)
            {
                Texture2D texture = CombatSprites.SoftCircleTexture;
                haloSprite = PixelArt.ToSprite(texture, texture.width, new Vector2(0.5f, 0.5f));
            }
            return haloSprite;
        }
    }

    // Brightest at the bottom of the window, darkening toward the top.
    static Sprite GlareSprite
    {
        get
        {
            if (glareSprite == null)
            {
                Texture2D texture = PixelArt.MakeTexture(4, 16, (x, y) =>
                {
                    byte shade = (byte)Mathf.Lerp(255f, 110f, y / 15f);
                    return new Color32(shade, shade, shade, 255);
                });
                texture.filterMode = FilterMode.Bilinear;
                glareSprite = PixelArt.ToSprite(texture, 16f, new Vector2(0.5f, 0.5f));
            }
            return glareSprite;
        }
    }

    // Statics outlive a play session in the Editor, so start each one fresh.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        SunBoost = 1f;
        FlareProgress = 0f;
        lastRumbleBreak = -10f;
    }

    void Awake()
    {
        lamp = GetComponent<Light2D>();
        baseIntensity = lamp.intensity;
        baseColor = lamp.color;
        home = transform.position;
        swingPhase = Random.value * Mathf.PI * 2f;
        if (bulb != null) bulbColor = bulb.color;
        IsOn = startOn;
        IsBroken = startBroken;
        nextSputter = Time.time + Random.Range(0.5f, 3f);
        if (drawFixture) BuildArt();
        Apply(IsOn && !IsBroken ? 1f : 0f);
    }

    void OnEnable()
    {
        StationRumble.Rumbled += OnRumble;
        FallingDebris.Impact += OnDebrisLanded;
    }

    void OnDisable()
    {
        StationRumble.Rumbled -= OnRumble;
        FallingDebris.Impact -= OnDebrisLanded;
    }

    // Straight on or off, no flicker.
    public void SetOn(bool on)
    {
        StopRoutine();
        power = 1f;
        IsOn = on;
    }

    // Smashes it: a pop, sparks and glass, a dying stutter, then dark for good.
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;
        StopRoutine();
        routine = StartCoroutine(Die());
    }

    // Out with a stutter, like the power failing. It isn't broken, so PowerOn brings it back.
    public void PowerOff()
    {
        if (!IsOn || IsBroken)
        {
            IsOn = false;
            return;
        }
        StopRoutine();
        routine = StartCoroutine(SwitchPower(false));
    }

    public void PowerOn()
    {
        if (IsOn && routine == null) return;
        StopRoutine();
        routine = StartCoroutine(SwitchPower(true));
    }

    void Update()
    {
        float level = IsOn && !IsBroken ? ModeLevel() * power : 0f;

        if (level > 0f && Time.time < stutterUntil)
        {
            if (Time.time >= nextStutterStep)
            {
                stutterLevel = Random.value < rumbleStutter * 0.5f ? Random.Range(0.05f, 0.5f) : 1f;
                nextStutterStep = Time.time + StutterStep;
            }
            level *= stutterLevel;
        }

        if (IsBroken && sparkWhenBroken && routine == null && Time.time >= nextSputter)
            Sputter();

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 10f);
        Apply(Mathf.Max(level, flash));
        Swing();
    }

    // The light sways and the fixture stays put on the wall, so the pool of light swings across the floor under it.
    void Swing()
    {
        if (swing <= 0f || mode == Mode.Sunlight) return;
        swingEnergy = Mathf.MoveTowards(swingEnergy, 0f, Time.deltaTime * 0.35f);
        float offset = swing * swingEnergy * Mathf.Sin(Time.time * swingRate * Mathf.PI * 2f + swingPhase);
        transform.position = home + new Vector3(offset, 0f, 0f);
        foreach (Transform art in transform)
            art.localPosition = fixtureOffset - new Vector2(offset, 0f);
    }

    float ModeLevel()
    {
        switch (mode)
        {
            case Mode.Flicker:
                if (Time.time >= nextCut)
                {
                    cutUntil = Time.time + Random.Range(0.03f, 0.2f);
                    nextCut = Time.time + Random.Range(0.3f, 2f) / Mathf.Max(0.01f, flickerRate);
                }
                return Time.time < cutUntil ? Random.Range(0f, 0.3f) : 1f;

            case Mode.Alarm:
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseRate);
                return 0.1f + 0.9f * wave * wave;

            case Mode.Sunlight:
                return (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 0.35f, transform.position.x)) * SunBoost * (1f + 0.6f * FlareProgress);

            default:
                return 1f;
        }
    }

    void Apply(float level)
    {
        lamp.intensity = baseIntensity * level;
        if (mode == Mode.Sunlight) lamp.color = Color.Lerp(baseColor, FlareWhite, FlareProgress * 0.8f);
        float glow = Mathf.Clamp01(level);

        if (bulb != null)
        {
            Color color = bulbColor;
            color.a *= Mathf.Lerp(0.25f, 1f, glow);
            bulb.color = color;
        }
        if (glass != null) glass.color = Color.Lerp(GlassDark, GlassLit, glow);
        if (halo != null)
        {
            Color tint = lamp.color;
            tint.a = 0.45f * glow;
            halo.color = tint;
        }
        // The sky through the window runs from deep orange to white hot as the flare builds.
        if (glare != null)
        {
            Color sky = Color.Lerp(SunDeep, SunWhite, Mathf.Clamp01((level - 0.8f) * 0.9f));
            glare.color = Color.Lerp(sky, FlareWhite, FlareProgress);
        }
    }

    IEnumerator Die()
    {
        Vector2 point = FixturePoint;
        HitEffects.Sparks(point, Vector2.down, 26, 6f, 220f, SparkBright, SparkHot);
        HitEffects.Dust(point, Shards, 0.35f);
        HitEffects.Ring(point, new Color(1f, 0.8f, 0.45f, 0.8f), 1.1f);
        StationRumble.PlayAt(breakClip, point, breakVolume);
        if (glass != null) glass.sprite = BrokenGlassSprite;

        // Torn off the wall: the fitting falls to the floor under it, and only the bracket is left.
        if (dropOnBreak > 0f && Random.value < dropOnBreak)
        {
            FallingDebris.Drop(home, 0f, 0.45f, 0.85f, false, FallingDebris.Kind.Fixture);
            if (frame != null) frame.enabled = false;
            if (glass != null) glass.enabled = false;
        }

        flash = 2.2f;
        yield return new WaitForSeconds(0.06f);
        for (float t = 0f; t < DyingTime; t += 0.05f)
        {
            float life = 1f - t / DyingTime;
            flash = Random.value < life * 0.7f ? Random.Range(0.3f, 1.1f) * life : 0f;
            yield return new WaitForSeconds(0.05f);
        }

        flash = 0f;
        nextSputter = Time.time + Random.Range(0.8f, 2.5f);
        routine = null;
    }

    IEnumerator SwitchPower(bool on)
    {
        IsOn = true;
        for (float t = 0f; t < PowerFlickerTime; t += StutterStep)
        {
            float progress = t / PowerFlickerTime;
            power = Random.value < (on ? progress : 1f - progress) ? 1f : Random.Range(0f, 0.15f);
            yield return new WaitForSeconds(StutterStep);
        }
        power = 1f;
        IsOn = on;
        routine = null;
    }

    // A broken light spitting a few sparks, with a flicker of light. Only near the camera, where it can be seen.
    void Sputter()
    {
        nextSputter = Time.time + Random.Range(1.2f, 4.5f);
        Camera cam = Camera.main;
        if (cam == null || Vector2.Distance(cam.transform.position, FixturePoint) > 14f) return;

        HitEffects.Sparks(FixturePoint, Vector2.down, Random.Range(4, 9), 3.5f, 70f, SparkBright, SparkHot);
        flash = Random.Range(0.35f, 0.8f);
    }

    void OnRumble(float strength, float duration)
    {
        swingEnergy = Mathf.Clamp01(Mathf.Max(swingEnergy, strength * 1.6f));
        stutterUntil = Mathf.Max(stutterUntil, Time.time + duration * Mathf.Clamp01(strength * 1.5f));

        // A hard rumble can shake a lamp loose, but only one every few seconds, and only where the player can see it.
        if (!breakable || IsBroken || !IsOn || strength < 0.45f || Time.time - lastRumbleBreak < 3f) return;
        Camera cam = Camera.main;
        if (cam == null || Vector2.Distance(cam.transform.position, FixturePoint) > 8f) return;
        if (Random.value >= rumbleBreakChance * strength * 2f) return;

        lastRumbleBreak = Time.time;
        StartCoroutine(BreakAfter(Random.Range(0.2f, duration * 0.6f)));
    }

    IEnumerator BreakAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Break();
    }

    // Debris coming down under the lamp can take it with it; anything landing nearby makes it stutter.
    void OnDebrisLanded(Vector2 point, float size)
    {
        float distance = Vector2.Distance(point, transform.position);
        float reach = breakRadius * size;
        if (breakable && IsOn && !IsBroken && distance < reach && Random.value < 0.85f * (1f - distance / reach))
        {
            Break();
            return;
        }
        if (distance < 6f) stutterUntil = Mathf.Max(stutterUntil, Time.time + 0.3f);
    }

    void StopRoutine()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        power = 1f;
    }

    void BuildArt()
    {
        if (mode == Mode.Sunlight)
        {
            // Behind the walls, so it only shows through the window's glass.
            glare = NewSprite("Window Glare", GlareSprite, "Default", -10);
            Vector2 spriteSize = GlareSprite.bounds.size;
            glare.transform.localScale = new Vector3(windowSize.x / spriteSize.x, windowSize.y / spriteSize.y, 1f);
            return;
        }

        frame = NewSprite("Fixture", FrameSprite, "Collision", 3);
        glass =NewSprite("Glass", IsBroken ? BrokenGlassSprite : GlassSprite, "Collision", 4);
        halo = NewSprite("Halo", HaloSprite, "Collision", 5);
        halo.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
        if (CombatSprites.EffectMaterial != null)
        {
            glass.sharedMaterial = CombatSprites.EffectMaterial;
            halo.sharedMaterial = CombatSprites.EffectMaterial;
        }
    }

    SpriteRenderer NewSprite(string childName, Sprite sprite, string sortingLayer, int order)
    {
        var child = new GameObject(childName).AddComponent<SpriteRenderer>();
        child.transform.SetParent(transform, false);
        child.transform.localPosition = fixtureOffset;
        child.sprite = sprite;
        child.sortingLayerName = sortingLayer;
        child.sortingOrder = order;
        if (mode == Mode.Sunlight && CombatSprites.EffectMaterial != null) child.sharedMaterial = CombatSprites.EffectMaterial;
        return child;
    }

    // The art is only made in Play mode, so mark where it goes.
    void OnDrawGizmos()
    {
        if (Application.isPlaying || !drawFixture) return;
        Gizmos.color = mode == Mode.Sunlight ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.85f, 0.4f);
        Vector3 size = mode == Mode.Sunlight ? (Vector3)windowSize : new Vector3(0.75f, 0.45f, 0f);
        Gizmos.DrawWireCube(FixturePoint, size);
        Gizmos.DrawLine(FixturePoint, transform.position);
    }
}
