using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The reactor core, in the middle of the reactor room on floor 2: a containment vessel taller than the room's wall, a
// ball of plasma churning behind its glass, magnetic rings wound in coils glowing on their undersides, coolant pipes
// arcing down to a plinth, and a hazard-ringed pad round it all with a moat of molten coolant. Everything is yellow and
// orange and too bright to look at for long, so the room feels hot. Placed by FloorOneBuilder (PlaceReactorCore).
// The art (Resources/Reactor, drawn at the tileset's 32 pixels to a cell): the body (28 cells wide, 24 tall) and the pad
// under it (34 by 14). Its lit parts are separate layers drawn unlit, so they shine in the dark: the plasma (frames, it
// churns), the coils and vents (they breathe), and the warning lights (two sets, blinking in turn). It throws a big warm
// light over the room, pulsing with the plasma, and embers drift up off the moat and out of the exhaust.
// This object sits at the middle of the plinth's foot, which is where it sorts from (DepthSort), and is solid over it,
// and over the machinery behind it too (RearHalfWidth, RearDepth), so there's no slipping through behind the reactor:
// the way past is round it.
public class ReactorCore : MonoBehaviour
{
    const float ArtPixelsPerUnit = 32f;
    const float FootBelowMiddle = 132f / ArtPixelsPerUnit;     // the body art's bottom edge, below the plinth's middle
    // The plinth's foot, a flat ellipse from the middle, solid so nobody walks into the reactor.
    const float PlinthRadiusX = 320f / ArtPixelsPerUnit;
    const float PlinthRadiusY = 128f / ArtPixelsPerUnit;
    static readonly Vector2 CoreAt = new Vector2(0f, (767f - 366f) / ArtPixelsPerUnit - FootBelowMiddle);     // the plasma
    static readonly Vector2 ExhaustAt = new Vector2(0f, (767f - 52f) / ArtPixelsPerUnit - FootBelowMiddle);   // the stack's mouth
    const float MoatRadiusX = 0.622f * 536f / ArtPixelsPerUnit;     // the molten ring on the pad
    const float MoatRadiusY = 0.622f * 214f / ArtPixelsPerUnit;
    const int CoreFrames = 6;
    // The solid block behind the plinth, in world units: this far either side of the middle, and from the middle this far
    // back (up the screen). FloorOneBuilder counts the cells under it as furniture.
    public const float RearHalfWidth = 9.5f;
    public const float RearDepth = 17f;
    const int Embers = 48;

    [Tooltip("Seconds for one slow breath of the coils and the light.")]
    public float pulseSeconds = 2.6f;
    [Tooltip("Plasma frames a second.")]
    public float coreFramesPerSecond = 9f;
    public Color heatColor = new Color(1f, 0.55f, 0.16f);
    public Color coreColor = new Color(1f, 0.82f, 0.4f);
    [Tooltip("The warm light thrown over the room, and how far it reaches, in cells.")]
    public float roomLightIntensity = 1.35f;
    public float roomLightRadius = 22f;

    private SpriteRenderer body, glow, lights, core, floorGlow, halo, floorHeat;
    private Sprite lightsA, lightsB;
    private readonly Sprite[] coreSprites = new Sprite[CoreFrames];
    private Light2D roomLight, coreLight;
    private float roomLightBase, coreLightBase;
    private float coreTimer, blinkTimer;
    private int coreFrame;
    private Transform art;

    class Ember
    {
        public SpriteRenderer sprite;
        public Vector2 from;
        public float age, life, rise, sway, phase;
    }
    private readonly List<Ember> embers = new List<Ember>();
    private static Sprite emberPixel;

    static Sprite Art(string name) => Resources.Load<Sprite>($"Reactor/Reactor_{name}");

    void Awake()
    {
        Sprite bodySprite = Art("Body");
        if (bodySprite == null)
        {
            Debug.LogWarning("Reactor core: its art isn't in Resources/Reactor.");
            return;
        }

        // The pad lies on the floor under everything; the reactor stands, sorting as one from FeetToCenter above its
        // middle, so it compares like for like with whoever walks round it.
        Layer("Pad", Art("Floor"), transform, "Floor", 5, false);
        floorGlow = Layer("Pad Glow", Art("FloorGlow"), transform, "Floor", 6, true);
        floorHeat = Layer("Pad Heat", CombatSprites.Glow, transform, "Floor", 7, true);
        floorHeat.transform.localScale = new Vector3(34f, 15f, 1f);

        art = new GameObject("Art").transform;
        art.SetParent(transform, false);
        art.localPosition = new Vector3(0f, DepthSort.FeetToCenter, 0f);
        DepthSort.Group(art.gameObject);
        Vector3 foot = new Vector3(0f, -FootBelowMiddle - DepthSort.FeetToCenter, 0f);
        halo = Layer("Halo", CombatSprites.Glow, art, DepthSort.Layer, -1, true);
        halo.transform.localPosition = (Vector3)CoreAt - new Vector3(0f, DepthSort.FeetToCenter, 0f);
        halo.transform.localScale = Vector3.one * 16f;
        body = Layer("Body", bodySprite, art, DepthSort.Layer, 0, false);
        core = Layer("Core", null, art, DepthSort.Layer, 1, true);
        glow = Layer("Glow", Art("Glow"), art, DepthSort.Layer, 2, true);
        lightsA = Art("Lights");
        lightsB = Art("Lights2");
        lights = Layer("Lights", lightsA, art, DepthSort.Layer, 3, true);
        foreach (SpriteRenderer layer in new[] { body, core, glow, lights }) layer.transform.localPosition = foot;
        for (int i = 0; i < CoreFrames; i++) coreSprites[i] = Art($"Core{i}");
        core.sprite = coreSprites[0];

        var solid = gameObject.AddComponent<PolygonCollider2D>();
        var points = new Vector2[32];
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * Mathf.PI * 2f / points.Length;
            points[i] = new Vector2(Mathf.Cos(a) * PlinthRadiusX, Mathf.Sin(a) * PlinthRadiusY);
        }
        solid.points = points;
        var rear = gameObject.AddComponent<BoxCollider2D>();
        rear.size = new Vector2(RearHalfWidth * 2f, RearDepth);
        rear.offset = new Vector2(0f, RearDepth * 0.5f);

        roomLight = NewLight("Room Light", Vector2.zero, heatColor, roomLightIntensity, roomLightRadius);
        coreLight = NewLight("Core Light", CoreAt, coreColor, 1.1f, 9f);
        roomLightBase = roomLight.intensity;
        coreLightBase = coreLight.intensity;

        if (emberPixel == null)
        {
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            emberPixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), ArtPixelsPerUnit);
        }
        for (int i = 0; i < Embers; i++)
        {
            var ember = new Ember { sprite = Layer("Ember", emberPixel, art, DepthSort.Layer, 4, true) };
            Respawn(ember);
            ember.age = Random.value * ember.life;      // already on their way up when the room's walked into
            embers.Add(ember);
        }
    }

    void Update()
    {
        if (body == null) return;
        float t = Time.time;
        // A slow breath, with a ripple of noise through it so it never looks like a metronome.
        float breath = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / pulseSeconds);
        float surge = Mathf.PerlinNoise(t * 1.7f, 0.31f);
        float heat = Mathf.Clamp01(0.55f * breath + 0.45f * surge);
        // A surge building (ReactorSurge): blazing brighter and brighter, flickering, then the flash as it goes.
        float building = ReactorSurge.Warning;
        float blaze = Mathf.Max(building * (0.75f + 0.25f * Mathf.Sin(t * 30f)), ReactorSurge.Flash);
        if (building > 0f) heat = Mathf.Max(heat, building);

        coreTimer -= Time.deltaTime;
        if (coreTimer <= 0f)
        {
            coreTimer = 1f / coreFramesPerSecond;
            coreFrame = (coreFrame + 1) % CoreFrames;
            if (coreSprites[coreFrame] != null) core.sprite = coreSprites[coreFrame];
        }
        core.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 1f, heat));
        glow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, heat));
        // The pad's channels light a beat after the core, as if the heat runs out along them.
        float later = 0.5f + 0.5f * Mathf.Sin((t - 0.4f) * Mathf.PI * 2f / pulseSeconds);
        floorGlow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, later));
        floorHeat.color = new Color(heatColor.r, heatColor.g, heatColor.b, Mathf.Lerp(0.14f, 0.24f, later));
        halo.color = new Color(heatColor.r, heatColor.g, heatColor.b, Mathf.Lerp(0.22f, 0.38f, heat) + 0.4f * blaze);
        halo.transform.localScale = Vector3.one * (Mathf.Lerp(15f, 17.5f, heat) * (1f + 0.7f * blaze));

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f && lightsA != null && lightsB != null)
        {
            blinkTimer = 0.55f;
            lights.sprite = lights.sprite == lightsA ? lightsB : lightsA;
        }

        roomLight.intensity = roomLightBase * Mathf.Lerp(0.8f, 1.1f, heat) * (1f + 1.6f * blaze);
        coreLight.intensity = coreLightBase * Mathf.Lerp(0.7f, 1.25f, heat) * (1f + 3f * blaze);

        foreach (Ember ember in embers)
        {
            ember.age += Time.deltaTime;
            if (ember.age >= ember.life) Respawn(ember);
            float k = ember.age / ember.life;
            // In whole pixels, so they stay crisp like the art.
            Vector2 at = ember.from + new Vector2(Mathf.Sin(ember.phase + ember.age * 2.3f) * ember.sway, ember.rise * ember.age);
            at = new Vector2(Mathf.Round(at.x * ArtPixelsPerUnit), Mathf.Round(at.y * ArtPixelsPerUnit)) / ArtPixelsPerUnit;
            ember.sprite.transform.localPosition = at - new Vector2(0f, DepthSort.FeetToCenter);
            Color c = Color.Lerp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.35f, 0.08f), k);
            ember.sprite.color = new Color(c.r, c.g, c.b, 1f - k * k);
        }
    }

    // Embers come off the moat round the pad most of the time, out of the exhaust stack the rest.
    void Respawn(Ember ember)
    {
        ember.age = 0f;
        if (Random.value < 0.7f)
        {
            float a = Random.value * Mathf.PI * 2f;
            ember.from = new Vector2(Mathf.Cos(a) * MoatRadiusX, Mathf.Sin(a) * MoatRadiusY);
            ember.life = Random.Range(1.2f, 2.6f);
            ember.rise = Random.Range(0.8f, 1.6f);
        }
        else
        {
            ember.from = ExhaustAt + new Vector2(Random.Range(-0.6f, 0.6f), 0f);
            ember.life = Random.Range(1.5f, 3f);
            ember.rise = Random.Range(1.2f, 2.2f);
        }
        ember.sway = Random.Range(0.05f, 0.3f);
        ember.phase = Random.value * 6.3f;
        ember.sprite.transform.localScale = Vector3.one * (Random.value < 0.3f ? 2f : 1f);
    }

    SpriteRenderer Layer(string childName, Sprite sprite, Transform parent, string sortingLayer, int order, bool glows)
    {
        var layer = new GameObject(childName).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.sprite = sprite;
        layer.sortingLayerName = sortingLayer;
        layer.sortingOrder = order;
        if (glows && CombatSprites.EffectMaterial != null) layer.sharedMaterial = CombatSprites.EffectMaterial;
        return layer;
    }

    Light2D NewLight(string lightName, Vector2 at, Color color, float intensity, float radius)
    {
        var holder = new GameObject(lightName);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = at;
        var light = holder.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightInnerRadius = radius * 0.2f;
        light.pointLightOuterRadius = radius;
        light.falloffIntensity = 0.6f;
        light.shadowsEnabled = false;
        return light;
    }

    // The art's only made in Play mode, so outline where it will be: the pad, the plinth's foot, and the body.
    void OnDrawGizmos()
    {
        Gizmos.color = heatColor;
        Vector3 at = transform.position;
        Gizmos.DrawWireCube(at, new Vector3(34f, 14f, 0f));
        Gizmos.DrawWireCube(at + new Vector3(0f, 12f - FootBelowMiddle, 0f), new Vector3(28f, 24f, 0f));
        Vector3 last = at + new Vector3(PlinthRadiusX, 0f, 0f);
        for (int i = 1; i <= 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            Vector3 next = at + new Vector3(Mathf.Cos(a) * PlinthRadiusX, Mathf.Sin(a) * PlinthRadiusY, 0f);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}
