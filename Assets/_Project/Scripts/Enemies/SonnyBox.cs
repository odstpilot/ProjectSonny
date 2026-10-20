using UnityEngine;
using UnityEngine.Rendering.Universal;

// Sonny, as the player sees it: the processing box in the control room, humming, with a lens that watches.
// While dormant the lens breathes slowly; Awaken makes it swell and pulse faster, for the moment the player walks in.
// Put a Light2D in glow to have the room pulse with it.
// In Chapter 1 it starts unpowered (startPowered off), dark and silent, until the technician installs it and PowerOn
// brings it up; Flicker shows another color in the lens for a moment (Sonny's first slip).
// The art (Resources/Sonny, drawn at the tileset's 32 pixels to a cell, 7 cells wide and 10 tall): a cube of circuit
// blocks floating over a column of hologram diamonds, over a chip, ringed by gate pylons and control consoles cabled to
// it (the rig). Layers, so the cube bobs and the beam powers up and pulses, with the lens in the beam. Lit parts are drawn
// unlit, so it shines in the dark. Three looks: Installed (blue, Chapter 1), Rogue (red, from Chapter 2 on, after it's
// taken the station: coming apart, and twitching), and Shadow (the tutorial: only a dark shape, no rig, traced round
// with a faint red rim so its outline reads in the dark, standing in a pool of shadow). Auto picks one
// from the scene. Before it's installed (unpowered), Installed is dead: the cube slumped broken on a grey chip, the rig's
// lights out; switching on, it flickers between broken and whole, then the cube rises into place.
// Without that art, or with boxSprite set, it's the old single-sprite box.
public class SonnyBox : MonoBehaviour
{
    public enum Look { Auto, Installed, Rogue, Shadow }

    const float PixelsPerUnit = 20f;
    const float ArtPixelsPerUnit = 32f;
    const float WakeTime = 1.2f;
    const float BobPixels = 2f;
    const float BobSeconds = 3.2f;
    const float RestPixels = 100f;      // how far below its place the cube sits while it's dead, so switching on it rises
    static readonly Vector2 ArtLens = new Vector2(0f, 3.9f);            // the middle of the beam, from the pivot
    // The chip's footprint on the floor, from Sonny's feet: a diamond, the foot of its slab. Solid, exactly that shape, so
    // nobody stands in it (the pins fanning out round it are flat on the floor).
    static readonly Vector2[] ChipFootprint = { new Vector2(0f, 0.44f), new Vector2(2.5f, 1.69f), new Vector2(0f, 2.94f), new Vector2(-2.5f, 1.69f) };
    // Where Sonny's drawn in front of or behind whoever's near it from (DepthSort): the middle of that footprint, as high
    // above it as a character's sorting point is above their feet. Everything of Sonny's sorts together from there.
    const float ArtSortHeight = 1.69f + DepthSort.FeetToCenter;
    // The rig, from Sonny's feet in cells, where the floor art runs its cables to: pylons in a ring, consoles in front
    // and to the sides, monitor banks and server racks behind. The left side and the strip in front stay clear: the
    // technician walks in from the control room's door on the left, then along the front to the install spot.
    static readonly Vector2[] PylonSpots = { new Vector2(-4.6f, 0.6f), new Vector2(4.6f, 0.6f), new Vector2(-3.6f, 4.6f), new Vector2(3.6f, 4.6f) };
    static readonly Vector2[] ConsoleSpots = { new Vector2(-6.4f, -0.6f), new Vector2(6.4f, -0.6f), new Vector2(-8.8f, 1.8f), new Vector2(8.8f, 1.8f) };
    static readonly Vector2[] MonitorSpots = { new Vector2(-8.6f, 5f), new Vector2(8.6f, 5f) };
    static readonly Vector2[] RackSpots =
    {
        new Vector2(-6.9f, 7.6f), new Vector2(-5.6f, 7.6f), new Vector2(5.6f, 7.6f), new Vector2(6.9f, 7.6f),
        new Vector2(-9.9f, 7.9f), new Vector2(9.9f, 7.9f),
    };
    static readonly Vector2 PylonFootprint = new Vector2(0.8f, 0.4f);       // its foot plate
    static readonly Vector2 ConsoleFootprint = new Vector2(2f, 0.7f);
    static readonly Vector2 MonitorFootprint = new Vector2(3.6f, 0.6f);
    static readonly Vector2 RackFootprint = new Vector2(1.1f, 0.5f);
    static readonly Vector2 FloorSpot = new Vector2(0f, -3f);   // the floor art's foot: the ring round it all, and the cables
    const float PylonEmitterHeight = 3.3f;
    const float ShineIntensity = 2.4f;  // how much brighter the room light is with the art
    const float ShineRadius = 1.8f;
    const int Width = 30;
    const int Height = 40;
    static readonly Vector2Int LensCenter = new Vector2Int(15, 27);   // in pixels from the bottom left of the box

    [Tooltip("Which art: Installed (blue), Rogue (red), Shadow (a dark shape). Auto: Shadow in the tutorial, Installed in Chapter 1, Rogue after.")]
    public Look look = Look.Auto;
    [Tooltip("Leave empty for Sonny's art (by look). A sprite here replaces it. Pivot at the bottom middle.")]
    public Sprite boxSprite;
    [Tooltip("Where the lens sits, relative to this object. Only needed with a custom sprite.")]
    public Vector2 lensOffset = new Vector2(0f, LensCenter.y / PixelsPerUnit);
    public Color lensColor = new Color(1f, 0.16f, 0.1f);
    [Tooltip("Optional light that pulses with the lens.")]
    public Light2D glow;
    [Tooltip("Pulses a second while dormant, then once awake.")]
    public float dormantPulseRate = 0.3f;
    public float awakePulseRate = 1.5f;
    [Tooltip("The scene's sound for the hum, looping while it's powered. Empty for none.")]
    [SoundName] public string humSound = "Sonny Hum";
    [Tooltip("The scene's sound for it powering on and waking. Empty for none.")]
    [SoundName] public string awakenSound = "Sonny Awaken";
    [Tooltip("Off leaves it dark and silent until PowerOn.")]
    public bool startPowered = true;

    public bool IsAwake { get; private set; }
    public bool IsPowered { get; private set; }

    private SpriteRenderer lens;
    private SpriteRenderer halo;
    private AudioSource hum;
    private AudioSource voice;
    private float humPitch = 1f;
    private float glowIntensity;
    private float awake;        // eases from 0 to 1 after Awaken
    private float phase;
    private float power;        // eases from 0 to 1 after PowerOn
    private Color flickerColor;
    private float flickerUntil = -1f;
    private SpriteRenderer artBase, artBeam, artCube;   // the layered art, if it's there
    private Transform artRoot;                          // they sort together from here (ArtSortHeight)
    private Sprite baseSprite, deadBaseSprite, deadCubeSprite;
    private Look resolvedLook;
    private bool shines;            // the art's lit parts glow: everything but the shadow
    // The shadow's rim: copies of the cube and the base drawn solid red, a pixel or two out each way and behind them.
    private readonly System.Collections.Generic.List<(SpriteRenderer rim, SpriteRenderer of)> rims =
        new System.Collections.Generic.List<(SpriteRenderer, SpriteRenderer)>();
    static readonly Color RimColor = new Color(1f, 0.16f, 0.1f, 0.55f);
    static readonly Vector2[] RimSteps =
    {
        Vector2.left, Vector2.right, Vector2.up, Vector2.down,
        new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
    };
    private float bobTime;

    // One piece of the rig: its body, its lights (racks blink between two sets), what it looks like dead, and the light
    // it throws (pylons).
    class RigPiece
    {
        public SpriteRenderer body, lights;
        public Sprite live, dead, lightsA, lightsB;
        public Light2D light;
        public float lightIntensity;
        public float flickerSeed;
        public float nextBlink;
    }
    private readonly System.Collections.Generic.List<RigPiece> rig = new System.Collections.Generic.List<RigPiece>();
    // Unhinged (Rogue, Shadow): it twitches. Now and then the cube jerks a few pixels and flashes its glitch frame.
    private bool unhinged;
    private Sprite cubeSprite, glitchSprite;
    private float nextTwitch, twitchUntil;
    private Vector2 twitchOffset;
    private bool twitchGlitch;

    void Awake()
    {
        if (boxSprite != null || !BuildArt())
        {
            var body = new GameObject("Box").AddComponent<SpriteRenderer>();
            body.transform.SetParent(transform, false);
            body.sprite = boxSprite != null ? boxSprite : MakeBox();
            body.sortingLayerName = "Player";
            if (boxSprite == null) lensOffset = new Vector2((LensCenter.x + 0.5f - Width * 0.5f) / PixelsPerUnit, (LensCenter.y + 0.5f) / PixelsPerUnit);
        }

        // Behind the box, so it glows around it rather than washing over it. Much bigger round Sonny's art.
        halo = CreateGlowSprite("Halo", CombatSprites.SoftCircleTexture, shines ? 4.5f : 1.4f, -1);
        lens = CreateGlowSprite("Lens", CombatSprites.RingTexture, shines ? 0.7f : 0.38f, 12);

        if (glow != null) glowIntensity = glow.intensity;

        hum = gameObject.AddComponent<AudioSource>();
        hum.spatialBlend = 0f;
        SoundManager.Setup(hum, humSound);
        hum.loop = true;
        humPitch = hum.pitch;
        hum.volume = SoundManager.Volume(humSound);
        if (hum.clip != null) hum.Play();

        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0f;
        voice.playOnAwake = false;

        IsPowered = startPowered;
        power = startPowered ? 1f : 0f;
        if (!startPowered) hum.Stop();
    }

    public void PowerOn()
    {
        if (IsPowered) return;
        IsPowered = true;
        if (hum.clip != null) hum.Play();
        SoundManager.PlayOneShot(voice, awakenSound);
    }

    // The lens goes this color for a moment, then back. Unscaled, so a cutscene can time it to a line.
    public void Flicker(Color color, float seconds)
    {
        flickerColor = color;
        flickerUntil = Time.unscaledTime + seconds;
    }

    public void Awaken()
    {
        if (IsAwake) return;
        IsAwake = true;
        SoundManager.PlayOneShot(voice, awakenSound);
    }

    void Update()
    {
        if (IsAwake) awake = Mathf.MoveTowards(awake, 1f, Time.deltaTime / WakeTime);
        power = Mathf.MoveTowards(power, IsPowered ? 1f : 0f, Time.deltaTime / WakeTime);

        phase += Time.deltaTime * Mathf.PI * 2f * Mathf.Lerp(dormantPulseRate, awakePulseRate, awake);
        float pulse = 0.5f + 0.5f * Mathf.Sin(phase);
        // Sonny's art shines once it's on, even before it's fully awake.
        float awakeness = shines ? Mathf.Max(awake, 0.65f) : awake;
        float brightness = Mathf.Lerp(0.35f, 1f, awakeness) * Mathf.Lerp(0.55f, 1f, pulse);
        // Powering up stutters on, like a tube catching.
        float lit = power >= 1f ? 1f : power * (Mathf.PerlinNoise(Time.time * 18f, 0f) > 0.35f ? 1f : 0.2f);
        bool flickering = Time.unscaledTime < flickerUntil;
        Color color = flickering ? flickerColor : lensColor;
        if (flickering) brightness = 1f;

        lens.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.5f, 1f, brightness) * lit);
        halo.color = new Color(color.r, color.g, color.b, 0.35f * brightness * lit);
        halo.transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, awakeness) * Mathf.Lerp(0.9f, 1.1f, pulse);
        if (glow != null)
        {
            glow.intensity = glowIntensity * Mathf.Lerp(0.5f, 1.8f, awakeness) * Mathf.Lerp(0.6f, 1f, pulse) * lit;
            glow.color = color;
        }
        if (hum.clip != null)
        {
            hum.pitch = humPitch * Mathf.Lerp(0.8f, 1.1f, awake) * (flickering ? 0.7f : 1f);
            hum.volume = SoundManager.Volume(humSound);
        }
        UpdateArt(lit, brightness, flickering ? flickerColor : (Color?)null);
    }

    // --- The layered art ---

    Look ResolvedLook()
    {
        if (look != Look.Auto) return look;
        if (FindAnyObjectByType<TutorialDirector>() != null) return Look.Shadow;
        if (FindAnyObjectByType<ChapterOneDirector>() != null) return Look.Installed;
        return Look.Rogue;
    }

    static Sprite Art(string name) => Resources.Load<Sprite>($"Sonny/Sonny_{name}");

    // The base, the beam, the cube, and the rig round them. False if the art isn't there.
    bool BuildArt()
    {
        resolvedLook = ResolvedLook();
        string name = resolvedLook.ToString();
        baseSprite = Art($"{name}_Base");
        Sprite beamSprite = Art($"{name}_Beam");
        cubeSprite = Art($"{name}_Cube");
        if (baseSprite == null || beamSprite == null || cubeSprite == null) return false;
        unhinged = resolvedLook == Look.Rogue || resolvedLook == Look.Shadow;
        shines = resolvedLook != Look.Shadow;
        glitchSprite = Art($"{name}_Glitch");
        deadBaseSprite = Art($"{name}_DeadBase");
        deadCubeSprite = Art($"{name}_Dead");

        artRoot = new GameObject("Art").transform;
        artRoot.SetParent(transform, false);
        artRoot.localPosition = new Vector3(0f, ArtSortHeight, 0f);
        DepthSort.Group(artRoot.gameObject);
        artBase = ArtLayer("Base", baseSprite, 0, shines, artRoot);
        artBeam = ArtLayer("Beam", beamSprite, 1, true, artRoot);
        artCube = ArtLayer("Cube", cubeSprite, 2, shines, artRoot);
        artBase.transform.localPosition = artBeam.transform.localPosition = new Vector3(0f, -ArtSortHeight, 0f);
        lensOffset = ArtLens;
        if (glow != null)
        {
            if (glow.transform.parent == transform) glow.transform.localPosition = ArtLens;   // the room lights from the beam
            if (shines)
            {
                glow.intensity *= ShineIntensity;
                glow.pointLightOuterRadius *= ShineRadius;
            }
        }

        // Solid over the chip, the shape of it, so nobody walks through it. (The scene's box is for the old cabinet.)
        if (TryGetComponent(out BoxCollider2D box)) box.enabled = false;
        var chip = gameObject.AddComponent<PolygonCollider2D>();
        chip.points = ChipFootprint;

        if (resolvedLook == Look.Shadow)
        {
            AddRim(artCube);
            AddRim(artBase);
            AddShadowPool();
        }

        if (shines)
        {
            AddRigPiece("Floor", name, FloorSpot, Vector2.zero, false, "Floor");
            foreach (Vector2 spot in PylonSpots) AddRigPiece("Pylon", name, spot, PylonFootprint, true);
            foreach (Vector2 spot in ConsoleSpots) AddRigPiece("Console", name, spot, ConsoleFootprint, false);
            foreach (Vector2 spot in MonitorSpots) AddRigPiece("Monitor", name, spot, MonitorFootprint, false);
            foreach (Vector2 spot in RackSpots) AddRigPiece("Rack", name, spot, RackFootprint, false);
        }
        return true;
    }

    // A thin red rim round a layer: copies of it, solid red and unlit, a couple of pixels out each way, behind it.
    void AddRim(SpriteRenderer of)
    {
        Material solid = CombatSprites.FlashMaterial != null ? CombatSprites.FlashMaterial : CombatSprites.EffectMaterial;
        foreach (Vector2 step in RimSteps)
        {
            var rim = new GameObject("Rim").AddComponent<SpriteRenderer>();
            rim.transform.SetParent(of.transform, false);
            rim.transform.localPosition = step * (2f / ArtPixelsPerUnit);
            rim.sortingLayerID = of.sortingLayerID;
            rim.sortingOrder = of.sortingOrder - 1;
            rim.spriteSortPoint = SpriteSortPoint.Pivot;
            if (solid != null) rim.sharedMaterial = solid;
            rims.Add((rim, of));
        }
    }

    // A wide pool of shadow on the floor round it, darkest under it, fading out across the room.
    void AddShadowPool()
    {
        var pool = new GameObject("Shadow Pool").AddComponent<SpriteRenderer>();
        pool.transform.SetParent(transform, false);
        pool.transform.localPosition = new Vector3(0f, 2f, 0f);
        pool.transform.localScale = new Vector3(18f, 11f, 1f);
        pool.sprite = CombatSprites.Glow;
        pool.color = new Color(0f, 0f, 0f, 0.85f);
        pool.sortingLayerName = "Floor";
        pool.sortingOrder = 20;
    }

    // The rims keep to whatever frame their layer is showing.
    void LateUpdate()
    {
        foreach ((SpriteRenderer rim, SpriteRenderer of) in rims)
        {
            if (rim == null || of == null) continue;
            rim.sprite = of.sprite;
            rim.enabled = of.enabled && of.sprite != null;
            // Breathing with the lens, a little stronger once it's awake.
            rim.color = new Color(RimColor.r, RimColor.g, RimColor.b, RimColor.a * Mathf.Lerp(0.55f, 1f, awake) * (0.8f + 0.2f * Mathf.Sin(phase)));
        }
    }

    SpriteRenderer ArtLayer(string childName, Sprite sprite, int order, bool glows, Transform parent = null)
    {
        var layer = new GameObject(childName).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent != null ? parent : transform, false);
        layer.sprite = sprite;
        layer.sortingLayerName = "Player";
        layer.sortingOrder = order;
        layer.spriteSortPoint = SpriteSortPoint.Pivot;     // sorts from its foot, like everyone standing about
        if (glows && CombatSprites.EffectMaterial != null) layer.sharedMaterial = CombatSprites.EffectMaterial;
        return layer;
    }

    // A piece of the rig standing at spot (cells from Sonny's feet), solid over its foot (none for the floor art, which
    // lies on the Floor sorting layer under everything). A standing piece sorts like furniture: as one, from
    // FeetToCenter above its foot, so it compares like for like with whoever walks round it.
    void AddRigPiece(string kind, string look, Vector2 spot, Vector2 foot, bool throwsLight, string sortingLayer = null)
    {
        Sprite live = Art($"{look}_{kind}");
        if (live == null) return;
        bool stands = sortingLayer == null;
        float lift = stands ? DepthSort.FeetToCenter : 0f;
        var holder = new GameObject(kind).transform;
        holder.SetParent(transform, false);
        holder.localPosition = spot + new Vector2(0f, lift);
        if (stands) DepthSort.Group(holder.gameObject);

        var piece = new RigPiece
        {
            live = live,
            dead = Art($"{look}_Dead{kind}"),
            body = ArtLayer("Body", live, sortingLayer != null ? 6 : 0, false, holder),
            flickerSeed = Random.value * 100f,
            lightsA = Art($"{look}_{kind}Lights"),
            lightsB = Art($"{look}_{kind}Lights2"),
        };
        if (piece.lightsA != null) piece.lights = ArtLayer("Lights", piece.lightsA, sortingLayer != null ? 7 : 1, true, holder);
        piece.body.transform.localPosition = new Vector3(0f, -lift, 0f);
        if (piece.lights != null) piece.lights.transform.localPosition = new Vector3(0f, -lift, 0f);
        if (sortingLayer != null)
        {
            piece.body.sortingLayerName = sortingLayer;
            if (piece.lights != null) piece.lights.sortingLayerName = sortingLayer;
        }
        if (throwsLight)
        {
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(holder, false);
            lightObject.transform.localPosition = new Vector3(0f, PylonEmitterHeight - lift, 0f);
            piece.light = lightObject.AddComponent<Light2D>();
            piece.light.lightType = Light2D.LightType.Point;
            piece.light.color = lensColor;
            piece.light.pointLightOuterRadius = 3f;
            piece.light.pointLightInnerRadius = 0.3f;
            piece.lightIntensity = 0.9f;
        }

        if (foot != Vector2.zero)
        {
            var solid = holder.gameObject.AddComponent<BoxCollider2D>();
            solid.size = foot;
            solid.offset = new Vector2(0f, foot.y * 0.5f - lift);
        }
        rig.Add(piece);
    }

    // Dead (unpowered, Chapter 1 before the install): the broken cube slumped on the grey chip, the rig grey and dark.
    // Switching on, it flickers between broken and whole as the power catches, then the whole cube rises from where the
    // broken one sat. Powered: the beam pulses with the lens, the cube floats and bobs, in whole pixels so it stays
    // crisp, and the rig's lights glow (the unhinged rig's sputter). A slip turns the beam the slip's color. Unhinged, the
    // hover is uneven, and every so often it twitches: a jerk of a few pixels, a flash of the glitch frame, the beam
    // stuttering, sometimes twice in a row.
    void UpdateArt(float lit, float brightness, Color? slip)
    {
        if (artCube == null) return;
        bool dead = lit < 0.5f && deadCubeSprite != null;
        Color body = dead ? new Color(0.85f, 0.85f, 0.9f) : Color.Lerp(new Color(0.42f, 0.42f, 0.46f), Color.white, lit);
        artBase.sprite = dead && deadBaseSprite != null ? deadBaseSprite : baseSprite;
        artBase.color = body;
        artCube.color = body;
        Color beam = slip ?? Color.white;
        bool twitching = unhinged && lit > 0.5f && Twitching();
        float beamAlpha = lit * Mathf.Lerp(0.6f, 1f, brightness) * (twitching && Random.value < 0.5f ? 0.2f : 1f);
        artBeam.color = new Color(beam.r, beam.g, beam.b, beamAlpha);

        if (lit > 0f) bobTime += Time.deltaTime;
        if (dead)
        {
            artCube.sprite = deadCubeSprite;
            artCube.transform.localPosition = new Vector3(0f, -ArtSortHeight, 0f);     // the broken cube's drawn where it lies
        }
        else
        {
            artCube.sprite = twitching && twitchGlitch && glitchSprite != null ? glitchSprite : cubeSprite;
            float wave = unhinged
                ? (Mathf.PerlinNoise(bobTime * 0.9f, 0.37f) - 0.5f) * 2.6f      // a hover that never settles into a rhythm
                : Mathf.Sin(bobTime * Mathf.PI * 2f / BobSeconds);
            float bob = Mathf.Round(wave * BobPixels * lit);
            float rise = Mathf.Round(-RestPixels * (1f - power));
            Vector2 jerk = twitching ? twitchOffset : Vector2.zero;
            artCube.transform.localPosition = new Vector3(jerk.x / ArtPixelsPerUnit, (bob + rise + jerk.y) / ArtPixelsPerUnit - ArtSortHeight, 0f);
        }

        foreach (RigPiece piece in rig)
        {
            piece.body.sprite = dead && piece.dead != null ? piece.dead : piece.live;
            // The unhinged rig sputters: each piece now and then drops out for a moment.
            float sputter = unhinged && Mathf.PerlinNoise(Time.time * 7f, piece.flickerSeed) < 0.28f ? 0.15f : 1f;
            float on = dead ? 0f : lit * Mathf.Lerp(0.75f, 1f, brightness) * sputter;
            if (piece.lights != null)
            {
                piece.lights.color = new Color(1f, 1f, 1f, on);
                // Server racks blink, each to its own beat.
                if (piece.lightsB != null && Time.time >= piece.nextBlink)
                {
                    piece.lights.sprite = piece.lights.sprite == piece.lightsA ? piece.lightsB : piece.lightsA;
                    piece.nextBlink = Time.time + Random.Range(0.25f, 0.9f);
                }
            }
            if (piece.light != null)
            {
                piece.light.intensity = piece.lightIntensity * on;
                piece.light.color = slip ?? lensColor;
            }
        }
    }

    // True for the moment of a twitch. Picks the next one as it goes: mostly a second or two apart, now and then right
    // after the last, so it never falls into a pattern.
    bool Twitching()
    {
        if (Time.time >= nextTwitch)
        {
            twitchUntil = Time.time + Random.Range(0.05f, 0.18f);
            twitchOffset = new Vector2(Random.Range(1, 5) * (Random.value < 0.5f ? -1f : 1f), Random.Range(-2, 3));
            twitchGlitch = Random.value < 0.6f;
            nextTwitch = twitchUntil + (Random.value < 0.25f ? Random.Range(0.05f, 0.2f) : Random.Range(0.6f, 2.8f));
        }
        return Time.time < twitchUntil;
    }

    // The box is only made in Play mode, so outline where it will be (Sonny's art: 7 cells wide, 10 tall, and its rig).
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = lensColor;
        Vector3 size = boxSprite != null ? new Vector3(Width, Height, 0f) / PixelsPerUnit : new Vector3(7f, 10f, 0f);
        if (boxSprite == null)
            foreach (Vector2 spot in PylonSpots) Gizmos.DrawWireCube(transform.position + (Vector3)spot + new Vector3(0f, 1.8f, 0f), new Vector3(1f, 3.6f, 0f));
        Gizmos.DrawWireCube(transform.position + new Vector3(0f, size.y * 0.5f, 0f), size);
    }

    SpriteRenderer CreateGlowSprite(string childName, Texture2D texture, float worldSize, int order)
    {
        var sprite = new GameObject(childName).AddComponent<SpriteRenderer>();
        // With Sonny's art, inside its sorting group, so the glow sorts with it.
        sprite.transform.SetParent(artRoot != null ? artRoot : transform, false);
        sprite.transform.localPosition = artRoot != null ? lensOffset - new Vector2(0f, ArtSortHeight) : lensOffset;
        sprite.sprite = PixelArt.ToSprite(texture, texture.width / worldSize, new Vector2(0.5f, 0.5f));
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = order;
        if (CombatSprites.EffectMaterial != null) sprite.sharedMaterial = CombatSprites.EffectMaterial;
        return sprite;
    }

    // A tall cabinet: a dark frame, vent slits below, a row of status lights, and a black socket for the lens.
    static Sprite MakeBox()
    {
        var frame = new Color32(24, 26, 30, 255);
        var body = new Color32(50, 55, 62, 255);
        var bodyLit = new Color32(70, 76, 85, 255);
        var vent = new Color32(18, 19, 22, 255);
        var socket = new Color32(8, 8, 10, 255);
        var socketRim = new Color32(96, 102, 112, 255);
        var statusLight = new Color32(60, 200, 120, 255);

        Texture2D texture = PixelArt.MakeTexture(Width, Height, (x, y) =>
        {
            if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1) return frame;
            float lensDistance = Vector2.Distance(new Vector2(x, y), LensCenter);
            if (lensDistance < 4.5f) return socket;
            if (lensDistance < 6f) return socketRim;
            if (y == Height - 2 || x == 1) return bodyLit;
            if (y >= 4 && y <= 14 && x >= 4 && x <= Width - 5 && y % 2 == 0) return vent;
            if (y == 18 && x >= 6 && x <= Width - 7 && x % 4 == 2) return statusLight;
            if (y == 2 || y == 20) return frame;
            return body;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
