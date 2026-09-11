using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A light on the station: a wall lamp, an alarm, or sunlight through a window.
//   Steady, Flicker: an ordinary lamp, or one with a failing tube.   Alarm: pulses.
//   Sunlight: breathes slowly and swells with SunBoost as the flare gets closer.
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
    const float FixturePixelsPerUnit = 16f;

    static readonly Color GlassLit = new Color(1f, 0.88f, 0.6f);
    static readonly Color GlassDark = new Color(0.2f, 0.16f, 0.12f);
    static readonly Color SunDeep = new Color(1f, 0.38f, 0.1f);
    static readonly Color SunWhite = new Color(1f, 0.9f, 0.62f);
    static readonly Color SparkBright = new Color(1f, 0.95f, 0.7f);
    static readonly Color SparkHot = new Color(1f, 0.5f, 0.12f);
    static readonly Color Shards = new Color(0.95f, 0.85f, 0.62f);

    static readonly Dictionary<char, Color32> FramePalette = new Dictionary<char, Color32>
    {
        { 'o', new Color32(30, 28, 26, 255) },
        { '#', new Color32(96, 90, 82, 255) },
        { '=', new Color32(58, 54, 50, 255) },
    };
    static readonly Dictionary<char, Color32> GlassPalette = new Dictionary<char, Color32>
    {
        { 'g', new Color32(255, 255, 255, 255) },
    };
    // A bulkhead lamp: a metal frame with cage bars over the glass.
    static readonly string[] FrameArt =
    {
        "..oooooooo..",
        ".o########o.",
        "o#..=..=..#o",
        "o#..=..=..#o",
        "o#..=..=..#o",
        ".o########o.",
        "..oooooooo..",
    };
    static readonly string[] GlassArt =
    {
        "............",
        "............",
        "..gg.gg.gg..",
        "..gg.gg.gg..",
        "..gg.gg.gg..",
        "............",
        "............",
    };
    // What's left of the glass.
    static readonly string[] BrokenGlassArt =
    {
        "............",
        "............",
        "..g......g..",
        "...g.g......",
        "..gg....g...",
        "............",
        "............",
    };

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

    [Header("Breaking")]
    public bool breakable = true;
    [Tooltip("Debris landing within this distance of the light can smash it.")]
    public float breakRadius = 2.2f;
    [Tooltip("Chance a hard rumble smashes it, when it's on screen.")]
    [Range(0f, 1f)] public float rumbleBreakChance = 0.15f;
    public bool sparkWhenBroken = true;
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
    private Coroutine routine;
    private float power = 1f;       // dips while the power is failing or coming back
    private float flash;            // the pop when it breaks, and sparks sputtering after
    private float nextSputter;
    private float nextCut;
    private float cutUntil;
    private float stutterUntil;
    private float nextStutterStep;
    private float stutterLevel = 1f;

    static Sprite FrameSprite => frameSprite != null ? frameSprite : (frameSprite = PixelArt.FromText(FrameArt, FramePalette, FixturePixelsPerUnit, new Vector2(0.5f, 0.5f)));
    static Sprite GlassSprite => glassSprite != null ? glassSprite : (glassSprite = PixelArt.FromText(GlassArt, GlassPalette, FixturePixelsPerUnit, new Vector2(0.5f, 0.5f)));
    static Sprite BrokenGlassSprite => brokenGlassSprite != null ? brokenGlassSprite : (brokenGlassSprite = PixelArt.FromText(BrokenGlassArt, GlassPalette, FixturePixelsPerUnit, new Vector2(0.5f, 0.5f)));
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
        lastRumbleBreak = -10f;
    }

    void Awake()
    {
        lamp = GetComponent<Light2D>();
        baseIntensity = lamp.intensity;
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
                return (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 0.35f, transform.position.x)) * SunBoost;

            default:
                return 1f;
        }
    }

    void Apply(float level)
    {
        lamp.intensity = baseIntensity * level;
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
        if (glare != null) glare.color = Color.Lerp(SunDeep, SunWhite, Mathf.Clamp01((level - 0.8f) * 0.9f));
    }

    IEnumerator Die()
    {
        Vector2 point = FixturePoint;
        HitEffects.Sparks(point, Vector2.down, 26, 6f, 220f, SparkBright, SparkHot);
        HitEffects.Dust(point, Shards, 0.35f);
        HitEffects.Ring(point, new Color(1f, 0.8f, 0.45f, 0.8f), 1.1f);
        StationRumble.PlayAt(breakClip, point, breakVolume);
        if (glass != null) glass.sprite = BrokenGlassSprite;

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

        NewSprite("Fixture", FrameSprite, "Collision", 3);
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
