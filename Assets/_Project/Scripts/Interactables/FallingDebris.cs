using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A piece of the station coming down. Dust trickles from above and a shadow spreads on the floor where it will land;
// then it drops, and hits hard: the camera jolts and shakes, the picture warps for a moment, sparks and dust fly, and
// anything underneath (the player, and robots too) is hurt and knocked away. Lamps nearby can break (see StationLight).
// What's left lies on the floor as wreckage for a while, or stays for good as a solid pile when it's meant to block the way.
// It's whatever a station sheds as it shakes apart (a hull panel, a scrap of gold insulation foil, a coolant line, a truss
// section, a vent grate, a strip light, a torn-out cable bundle, an electronics module), drawn fresh by WreckageArt for
// each piece, so no two crashes look alike.
// Made from code: StationRumble drops these, and levels can call FallingDebris.Drop. Wreckage that's already on the floor
// when a level starts is this component with startLanded ticked. No prefab or art needed.
public class FallingDebris : MonoBehaviour
{
    public enum Kind { Any, Panel, Foil, Pipe, Truss, Grate, Fixture, Cables, Circuit, Scatter }

    const float FallHeight = 7f;
    const float FallTime = 0.35f;       // the end of the warning, spent dropping
    const float FadeTime = 1.5f;
    const float DustPerSecond = 30f;
    static readonly Color DustColor = new Color(0.72f, 0.74f, 0.78f, 0.8f);
    static readonly Color RubbleTint = new Color(0.85f, 0.85f, 0.85f);
    static readonly Color SparkBright = new Color(1f, 0.9f, 0.6f);
    static readonly Color SparkHot = new Color(1f, 0.5f, 0.15f);
    static readonly Color SparkElectric = new Color(0.7f, 0.85f, 1f);
    static readonly Color GlassColor = new Color(1f, 0.95f, 0.82f, 0.9f);

    // How each kind is drawn, the colour of the chips it throws, how hard it hits, and whether it's long and thin (lies
    // at an angle, casts a long shadow).
    class Look
    {
        public System.Func<System.Random, PixelCanvas> draw;
        public Color chips;
        public float weight;
        public bool lengthwise;
    }

    static readonly Dictionary<Kind, Look> Looks = new Dictionary<Kind, Look>
    {
        { Kind.Panel, new Look { draw = WreckageArt.Panel, chips = WreckageArt.PanelChips, weight = 1f } },
        { Kind.Foil, new Look { draw = WreckageArt.Foil, chips = WreckageArt.FoilChips, weight = 0.45f } },
        { Kind.Pipe, new Look { draw = WreckageArt.Pipe, chips = WreckageArt.MetalChips, weight = 0.9f, lengthwise = true } },
        { Kind.Truss, new Look { draw = WreckageArt.Truss, chips = WreckageArt.MetalChips, weight = 1.5f, lengthwise = true } },
        { Kind.Grate, new Look { draw = WreckageArt.Grate, chips = WreckageArt.MetalChips, weight = 0.8f } },
        { Kind.Fixture, new Look { draw = WreckageArt.Fixture, chips = WreckageArt.GlassChips, weight = 0.6f } },
        { Kind.Cables, new Look { draw = WreckageArt.Cables, chips = WreckageArt.WireChips, weight = 0.6f, lengthwise = true } },
        { Kind.Circuit, new Look { draw = WreckageArt.Circuit, chips = WreckageArt.BoardChips, weight = 0.8f } },
        { Kind.Scatter, new Look { draw = WreckageArt.Scatter, chips = WreckageArt.MetalChips, weight = 0.5f } },
    };

    // Mostly panels off the walls and ceiling; now and then something heavier or stranger.
    static readonly Kind[] FallingKinds =
    {
        Kind.Panel, Kind.Panel, Kind.Panel, Kind.Foil, Kind.Foil, Kind.Pipe, Kind.Pipe,
        Kind.Truss, Kind.Grate, Kind.Fixture, Kind.Cables, Kind.Circuit,
    };
    static readonly Kind[] LyingKinds =
    {
        Kind.Panel, Kind.Foil, Kind.Pipe, Kind.Grate, Kind.Cables, Kind.Circuit, Kind.Scatter, Kind.Scatter, Kind.Truss,
    };

    [Tooltip("Damage to anything underneath when it lands.")]
    public float damage = 1f;
    public float knockback = 0.7f;
    [Tooltip("What comes down. Any picks one, at random for falling debris and by position for wreckage already on the floor.")]
    public Kind kind = Kind.Any;
    [Tooltip("How heavy a hit it is, and how wide: 1 is about a tile. The art keeps its own size, so its pixels match the level's.")]
    public float size = 1f;
    [Tooltip("Seconds the shadow shows before it lands.")]
    public float warnTime = 1.1f;
    [Tooltip("Lands as a solid pile that blocks the way, for good.")]
    public bool solid;
    [Tooltip("Seconds loose wreckage stays before fading away. 0 keeps it. Solid piles always stay.")]
    public float rubbleLifetime = 10f;
    [Tooltip("Already lying on the floor when the scene starts: no shadow, no fall, no damage.")]
    public bool startLanded;

    public event System.Action Landed;
    // (where, size) whenever any piece of debris lands.
    public static event System.Action<Vector2, float> Impact;
    public bool HasLanded { get; private set; }

    static Sprite shadowSprite;

    private Look look;
    // Everything about the piece comes from this: seeded by position for wreckage placed in the level, so it looks the
    // same every time the level loads, and at random for anything falling.
    private System.Random dice;
    private readonly List<Sprite> madeSprites = new List<Sprite>();

    static Sprite ShadowSprite
    {
        get
        {
            if (shadowSprite == null)
            {
                Texture2D texture = CombatSprites.SoftCircleTexture;
                shadowSprite = PixelArt.ToSprite(texture, texture.width, new Vector2(0.5f, 0.5f));
            }
            return shadowSprite;
        }
    }

    public static FallingDebris Drop(Vector2 point, float damage = 1f, float warnTime = 1.1f, float size = 1f, bool solid = false, Kind kind = Kind.Any)
    {
        var debris = new GameObject(solid ? "Debris Pile" : "Falling Debris").AddComponent<FallingDebris>();
        debris.transform.position = new Vector3(point.x, point.y, 0f);
        debris.damage = damage;
        debris.warnTime = warnTime;
        debris.size = size;
        debris.solid = solid;
        debris.kind = kind;
        return debris;
    }

    Sprite Draw(PixelCanvas canvas, Vector2 pivot)
    {
        Sprite sprite = canvas.ToSprite(pivot);
        madeSprites.Add(sprite);
        return sprite;
    }

    void OnDestroy()
    {
        foreach (Sprite sprite in madeSprites) PixelCanvas.Destroy(sprite);
        madeSprites.Clear();
    }

    IEnumerator Start()
    {
        dice = new System.Random(startLanded ? PositionSeed(transform.position) : Random.Range(int.MinValue, int.MaxValue));

        Kind picked = kind;
        if (picked == Kind.Any) picked = Pick(startLanded ? LyingKinds : FallingKinds);
        // Only wreckage already on the floor is ever odds and ends; a scatter can't fall as one piece.
        if (picked == Kind.Scatter && !startLanded) picked = Kind.Panel;
        kind = picked;
        look = Looks[kind];

        SpriteRenderer chunk = CreateRenderer("Chunk", Draw(look.draw(dice), new Vector2(0.5f, 0.5f)), "Top", 10);
        chunk.flipX = Range(0f, 1f) < 0.5f;
        chunk.flipY = !look.lengthwise && Range(0f, 1f) < 0.3f;
        // Only long pieces lie at an angle: turning pixel art by a few degrees makes its pixels uneven.
        float landedAngle = look.lengthwise ? Mathf.Round(Range(-60f, 60f) / 15f) * 15f : 0f;

        if (startLanded)
        {
            HasLanded = true;
            Settle(chunk, landedAngle);
            yield break;
        }

        // Long pieces cast a long shadow.
        float shadowWidth = Mathf.Max(0.9f, chunk.sprite.bounds.size.x * 0.9f);
        SpriteRenderer shadow = CreateRenderer("Shadow", ShadowSprite, "FloorObject", 5);
        shadow.color = Color.clear;
        shadow.transform.localRotation = Quaternion.Euler(0f, 0f, landedAngle);
        chunk.enabled = false;
        float spin = Random.Range(-220f, 220f) / look.weight;
        float fallStart = Mathf.Max(0f, warnTime - FallTime);
        Vector2 point = transform.position;
        float gritOwed = 0f;

        for (float t = 0f; t < warnTime; t += Time.deltaTime)
        {
            float warning = t / warnTime;
            shadow.transform.localScale = new Vector3(shadowWidth, 0.6f, 1f) * Mathf.Lerp(0.35f, 1f, warning);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.1f, 0.65f, warning));

            // Dust shaking loose from the ceiling above, more and more of it.
            gritOwed += DustPerSecond * warning * Time.deltaTime;
            while (gritOwed >= 1f)
            {
                gritOwed -= 1f;
                Vector2 from = point + new Vector2(Random.Range(-0.35f, 0.35f) * shadowWidth, Random.Range(1.5f, 2.6f));
                HitEffects.Speck(from, new Vector2(0f, -7f), Random.Range(0.25f, 0.4f), Random.Range(0.03f, 0.05f), DustColor);
            }

            if (t >= fallStart)
            {
                float fall = (t - fallStart) / Mathf.Max(0.01f, warnTime - fallStart);
                chunk.enabled = true;
                chunk.transform.localPosition = new Vector3(0f, FallHeight * (1f - fall * fall), 0f);
                chunk.transform.localRotation = Quaternion.Euler(0f, 0f, landedAngle + spin * (1f - fall));
            }
            yield return null;
        }

        Destroy(shadow.gameObject);
        Land(chunk, landedAngle);
    }

    void Land(SpriteRenderer chunk, float angle)
    {
        HasLanded = true;
        Vector2 point = transform.position;
        float near = NearCamera(point);
        float weight = size * look.weight * (solid ? 1.5f : 1f);

        HitEffects.Dust(point, look.chips, weight);
        HitEffects.Ring(point, new Color(1f, 0.92f, 0.8f, 0.45f), 1.4f * weight);
        HitEffects.Sparks(point, Vector2.up, Mathf.RoundToInt(10 * weight), 5f, 160f, SparkBright, SparkHot);

        // A light fitting bursts into a spray of diffuser and sparks; live wiring and electronics short out.
        if (kind == Kind.Fixture)
        {
            HitEffects.Sparks(point, Vector2.up, 18, 7f, 200f, SparkBright, GlassColor);
            for (int i = 0; i < 14; i++)
                HitEffects.Speck(point, Random.insideUnitCircle.normalized * Random.Range(1.5f, 4f), Random.Range(0.3f, 0.6f), Random.Range(0.03f, 0.06f), GlassColor);
        }
        else if (kind == Kind.Cables || kind == Kind.Circuit)
        {
            HitEffects.Sparks(point, Vector2.up, 16, 6f, 220f, Color.white, SparkElectric);
        }

        // The thud: a hard jolt down, shake on top, and the picture warping for a moment.
        CameraShake.Kick(Vector2.down, 0.14f * weight * near);
        CameraShake.Shake(0.4f * weight * near);
        StationRumble.Punch(0.4f * weight * near);
        StationRumble.PlayImpact(point);
        HurtWhatsUnderneath(point, chunk);

        Settle(chunk, angle);
        Landed?.Invoke();
        Impact?.Invoke(point, size);
    }

    // Leaves it lying on the floor: a solid pile, or loose wreckage that fades after a while.
    void Settle(SpriteRenderer rubble, float angle)
    {
        rubble.enabled = true;
        rubble.transform.localPosition = Vector3.zero;
        rubble.sortingLayerName = "FloorObject";
        rubble.sortingOrder = 6;
        rubble.color = RubbleTint;

        if (solid)
        {
            rubble.sprite = Draw(WreckageArt.Pile(dice), new Vector2(0.5f, 0.35f));
            rubble.transform.localRotation = Quaternion.identity;
            rubble.flipY = false;
            Vector2 footprint = rubble.sprite.bounds.size;
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(footprint.x * 0.8f, footprint.y * 0.6f);
            box.offset = new Vector2(0f, footprint.y * 0.05f);
            return;
        }

        rubble.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (rubbleLifetime > 0f) StartCoroutine(FadeAway(rubble));
    }

    void HurtWhatsUnderneath(Vector2 point, SpriteRenderer chunk)
    {
        float radius = 0.45f * Mathf.Max(0.8f, chunk.bounds.size.x * 0.6f);
        var alreadyHit = new HashSet<IDamageable>();
        foreach (Collider2D col in Physics2D.OverlapCircleAll(point, radius))
        {
            IDamageable target = Damageable.FromCollider(col);
            if (target == null || !alreadyHit.Add(target)) continue;

            Vector2 away = (Vector2)col.bounds.center - point;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle.normalized;
            target.TakeDamage(new DamageInfo(damage, away.normalized, knockback, gameObject, col.ClosestPoint(point)));
        }
    }

    IEnumerator FadeAway(SpriteRenderer rubble)
    {
        yield return new WaitForSeconds(rubbleLifetime);

        Color start = rubble.color;
        for (float t = 0f; t < FadeTime; t += Time.deltaTime)
        {
            rubble.color = new Color(start.r, start.g, start.b, 1f - t / FadeTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    float Range(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)dice.NextDouble());
    }

    Kind Pick(Kind[] kinds)
    {
        return kinds[Mathf.Min(kinds.Length - 1, Mathf.FloorToInt(Range(0f, kinds.Length)))];
    }

    static int PositionSeed(Vector2 position)
    {
        unchecked
        {
            return Mathf.RoundToInt(position.x * 4f) * 73856093 ^ Mathf.RoundToInt(position.y * 4f) * 19349663;
        }
    }

    // 1 on screen, fading to 0 well off it, so far-off crashes shake less.
    static float NearCamera(Vector2 point)
    {
        Camera cam = Camera.main;
        return cam == null ? 1f : Mathf.Clamp01(1.5f - Vector2.Distance(cam.transform.position, point) / 10f);
    }

    SpriteRenderer CreateRenderer(string childName, Sprite sprite, string sortingLayer, int order)
    {
        var child = new GameObject(childName).AddComponent<SpriteRenderer>();
        child.transform.SetParent(transform, false);
        child.sprite = sprite;
        child.sortingLayerName = sortingLayer;
        child.sortingOrder = order;
        return child;
    }

    // Placed rubble is only drawn in Play mode, so outline it in the Scene view.
    void OnDrawGizmos()
    {
        if (Application.isPlaying || !startLanded) return;
        Gizmos.color = solid ? new Color(0.9f, 0.6f, 0.3f) : new Color(0.65f, 0.65f, 0.65f);
        Gizmos.DrawWireCube(transform.position, new Vector3(1.1f, 0.7f, 0f) * size);
    }
}
