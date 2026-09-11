using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A piece of the ceiling coming down. Grit trickles from above and a shadow spreads on the floor where it will land;
// then it drops, and hits hard: the camera jolts and shakes, the picture warps for a moment, sparks and dust fly, and
// anything underneath (the player, and robots too) is hurt and knocked away. Lamps nearby can break (see StationLight).
// What's left lies on the floor as rubble for a while, or stays for good as a solid pile when it's meant to block the way.
// Made from code: StationRumble drops these, and levels can call FallingDebris.Drop. Rubble that's already on the floor
// when a level starts is this component with startLanded ticked. No prefab or art needed.
public class FallingDebris : MonoBehaviour
{
    const float FallHeight = 7f;
    const float FallTime = 0.35f;       // the end of the warning, spent dropping
    const float FadeTime = 1.5f;
    const float GritPerSecond = 30f;
    const float PixelsPerUnit = 12f;
    static readonly Color ChipColor = new Color32(150, 154, 160, 255);
    static readonly Color GritColor = new Color(0.8f, 0.72f, 0.6f, 0.8f);
    static readonly Color RubbleTint = new Color(0.8f, 0.8f, 0.8f);
    static readonly Color SparkBright = new Color(1f, 0.9f, 0.6f);
    static readonly Color SparkHot = new Color(1f, 0.5f, 0.15f);

    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { 'o', new Color32(52, 55, 60, 255) },
        { '#', new Color32(160, 164, 170, 255) },
        { '=', new Color32(122, 126, 132, 255) },
        { 'r', new Color32(96, 100, 106, 255) },
    };
    // A bent ceiling panel with its corner torn off.
    static readonly string[] ChunkArt =
    {
        "oooooooooo....",
        "o########o....",
        "o#r####r##o...",
        "o##########o..",
        "o###=======#o.",
        "o###########o.",
        "o#r#######r#o.",
        ".o#########oo.",
        "..o######oo...",
        "...oooooo.....",
    };
    // Panels and beams heaped up.
    static readonly string[] PileArt =
    {
        "......oo........",
        "....oo##o..oo...",
        "...o###=#oo##o..",
        "..o##r#==###r#o.",
        ".o#=####o##=###o",
        "o###oo###o#####o",
        "o##o##r###==##ro",
        "o#=######r####oo",
        ".oo##=####oo##o.",
        "...ooooooo..oo..",
    };

    [Tooltip("Damage to anything underneath when it lands.")]
    public float damage = 1f;
    public float knockback = 0.7f;
    [Tooltip("1 is about a tile across.")]
    public float size = 1f;
    [Tooltip("Seconds the shadow shows before it lands.")]
    public float warnTime = 1.1f;
    [Tooltip("Lands as a solid pile that blocks the way, for good.")]
    public bool solid;
    [Tooltip("Seconds loose rubble stays before fading away. 0 keeps it. Solid piles always stay.")]
    public float rubbleLifetime = 10f;
    [Tooltip("Already lying on the floor when the scene starts: no shadow, no fall, no damage.")]
    public bool startLanded;

    public event System.Action Landed;
    // (where, size) whenever any piece of debris lands.
    public static event System.Action<Vector2, float> Impact;
    public bool HasLanded { get; private set; }

    static Sprite chunkSprite;
    static Sprite pileSprite;
    static Sprite shadowSprite;

    static Sprite ChunkSprite => chunkSprite != null ? chunkSprite : (chunkSprite = PixelArt.FromText(ChunkArt, Palette, PixelsPerUnit, new Vector2(0.5f, 0.5f)));
    static Sprite PileSprite => pileSprite != null ? pileSprite : (pileSprite = PixelArt.FromText(PileArt, Palette, PixelsPerUnit, new Vector2(0.5f, 0.4f)));
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

    public static FallingDebris Drop(Vector2 point, float damage = 1f, float warnTime = 1.1f, float size = 1f, bool solid = false)
    {
        var debris = new GameObject(solid ? "Debris Pile" : "Falling Debris").AddComponent<FallingDebris>();
        debris.transform.position = new Vector3(point.x, point.y, 0f);
        debris.damage = damage;
        debris.warnTime = warnTime;
        debris.size = size;
        debris.solid = solid;
        return debris;
    }

    IEnumerator Start()
    {
        SpriteRenderer chunk = CreateRenderer("Chunk", ChunkSprite, "Top", 10);
        chunk.transform.localScale = Vector3.one * size;
        // The same tilt every time for placed rubble, so a level looks the same each time it loads.
        float landedAngle = startLanded ? Mathf.Repeat(transform.position.x * 37f + transform.position.y * 61f, 50f) - 25f : Random.Range(-25f, 25f);

        if (startLanded)
        {
            HasLanded = true;
            Settle(chunk, landedAngle);
            yield break;
        }

        SpriteRenderer shadow = CreateRenderer("Shadow", ShadowSprite, "FloorObject", 5);
        shadow.color = Color.clear;
        chunk.enabled = false;
        float spin = Random.Range(-220f, 220f);
        float fallStart = Mathf.Max(0f, warnTime - FallTime);
        Vector2 point = transform.position;
        float gritOwed = 0f;

        for (float t = 0f; t < warnTime; t += Time.deltaTime)
        {
            float warning = t / warnTime;
            shadow.transform.localScale = new Vector3(1.3f, 0.75f, 1f) * size * Mathf.Lerp(0.35f, 1f, warning);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.1f, 0.65f, warning));

            // Grit shaking loose from the ceiling above, more and more of it.
            gritOwed += GritPerSecond * warning * Time.deltaTime;
            while (gritOwed >= 1f)
            {
                gritOwed -= 1f;
                Vector2 from = point + new Vector2(Random.Range(-0.35f, 0.35f) * size, Random.Range(1.5f, 2.6f));
                HitEffects.Speck(from, new Vector2(0f, -7f), Random.Range(0.25f, 0.4f), Random.Range(0.03f, 0.06f), GritColor);
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
        float weight = size * (solid ? 1.5f : 1f);

        HitEffects.Dust(point, ChipColor, weight);
        HitEffects.Ring(point, new Color(1f, 0.92f, 0.8f, 0.45f), 1.4f * weight);
        HitEffects.Sparks(point, Vector2.up, Mathf.RoundToInt(10 * weight), 5f, 160f, SparkBright, SparkHot);

        // The thud: a hard jolt down, shake on top, and the picture warping for a moment.
        CameraShake.Kick(Vector2.down, 0.14f * weight * near);
        CameraShake.Shake(0.4f * weight * near);
        StationRumble.Punch(0.4f * weight * near);
        StationRumble.PlayImpact(point);
        HurtWhatsUnderneath(point);

        Settle(chunk, angle);
        Landed?.Invoke();
        Impact?.Invoke(point, size);
    }

    // Leaves it lying on the floor: a solid pile, or loose rubble that fades after a while.
    void Settle(SpriteRenderer rubble, float angle)
    {
        rubble.enabled = true;
        rubble.transform.localPosition = Vector3.zero;
        rubble.sortingLayerName = "FloorObject";
        rubble.sortingOrder = 6;
        rubble.color = RubbleTint;

        if (solid)
        {
            rubble.sprite = PileSprite;
            rubble.transform.localRotation = Quaternion.identity;
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.1f, 0.7f) * size;
            return;
        }

        rubble.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (rubbleLifetime > 0f) StartCoroutine(FadeAway(rubble));
    }

    void HurtWhatsUnderneath(Vector2 point)
    {
        var alreadyHit = new HashSet<IDamageable>();
        foreach (Collider2D col in Physics2D.OverlapCircleAll(point, 0.55f * size))
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
