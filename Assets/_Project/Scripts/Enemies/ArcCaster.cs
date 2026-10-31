using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// arc bot's lightning strikes
public class ArcCaster : MonoBehaviour
{
    const float PixelsPerUnit = 32f;
    const int FillFrames = 7;
    const float FloorSquash = 0.62f;
    static readonly Vector2 CoilTip = new Vector2(0f, 0.87f);
    static readonly Color32 Ring = new Color32(255, 44, 32, 255);
    static readonly Color32 RingDark = new Color32(120, 14, 10, 255);
    static readonly Color32 BoltCore = new Color32(255, 255, 255, 255);
    static readonly Color32 BoltGlow = new Color32(120, 220, 255, 255);
    static readonly Color ChargeColor = new Color(0.5f, 0.85f, 1f);

    [Tooltip("How far off it calls the lightning down, and how close it lets the player get before backing off.")]
    public float range = 7.5f;
    public float keepAway = 3.4f;
    [Tooltip("Seconds of warning (the rings filling), and between one cast and the next.")]
    public float telegraphSeconds = 1.05f;
    public float cooldown = 2.6f;
    [Tooltip("How many places it strikes at once, and how far round the player the extra ones land.")]
    public int strikes = 3;
    public float spread = 1.8f;
    [Tooltip("How wide each strike is, across, in world units.")]
    public float strikeRadius = 0.9f;
    public float damage = 1f;
    [SoundName] public string chargeSound = "Arc Charge";
    [SoundName] public string strikeSound = "Arc Strike";

    static readonly Dictionary<int, Sprite[]> fills = new Dictionary<int, Sprite[]>();
    static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();

    private readonly List<(GameObject root, SpriteRenderer ring, SpriteRenderer fill, Vector2 at)> marks =
        new List<(GameObject, SpriteRenderer, SpriteRenderer, Vector2)>();
    private Coroutine casting;
    private readonly List<SpriteRenderer> bolts = new List<SpriteRenderer>();
    private float nextReady;
    private Light2D coilLight;
    private SpriteRenderer body;
    private Transform player;
    private Health playerHealth;

    public bool Ready => casting == null && Time.time >= nextReady;
    public float CastSeconds => telegraphSeconds + 0.35f;

    void Awake()
    {
        body = GetComponentInChildren<SpriteRenderer>();
        var holder = new GameObject("Coil Light");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = CoilTip;
        coilLight = holder.AddComponent<Light2D>();
        coilLight.lightType = Light2D.LightType.Point;
        coilLight.color = ChargeColor;
        coilLight.pointLightOuterRadius = 2.2f;
        coilLight.falloffIntensity = 0.7f;
        coilLight.shadowsEnabled = false;
        coilLight.intensity = 0f;
        nextReady = Time.time + Random.Range(0.4f, 1.2f);
    }

    void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found == null) return;
        player = found.transform;
        playerHealth = found.GetComponent<Health>();
    }

    void OnDisable() => Interrupt();

    public void Cast(Vector2 target, Vector2 targetVelocity)
    {
        if (!Ready) return;
        Vector2 feet = target - new Vector2(0f, DepthSort.FeetToCenter);
        var points = new List<Vector2> { feet };
        Vector2 lead = Vector2.ClampMagnitude(targetVelocity * 0.7f, 2.4f);
        if (strikes > 1) points.Add(lead.sqrMagnitude > 0.25f ? feet + lead : feet + Random.insideUnitCircle.normalized * spread);
        for (int i = points.Count; i < strikes; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            points.Add(feet + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * FloorSquash) * spread);
        }
        casting = StartCoroutine(Run(points));
    }

    public void Interrupt()
    {
        if (casting != null) StopCoroutine(casting);
        casting = null;
        ClearMarks();
        ClearBolts();
        if (coilLight != null) coilLight.intensity = 0f;
        nextReady = Mathf.Max(nextReady, Time.time + cooldown * 0.6f);
    }

    void ClearBolts()
    {
        foreach (SpriteRenderer bolt in bolts)
        {
            if (bolt == null) continue;
            Destroy(bolt.sprite.texture);
            Destroy(bolt.gameObject);
        }
        bolts.Clear();
    }

    IEnumerator Run(List<Vector2> points)
    {
        foreach (Vector2 at in points) Mark(at);
        Sfx.At(chargeSound, transform.position, 0.5f);

        float sparkTimer = 0f;
        for (float t = 0f; t < telegraphSeconds; t += Time.deltaTime)
        {
            float k = t / telegraphSeconds;
            int frame = Mathf.Min(FillFrames - 1, Mathf.FloorToInt(k * FillFrames));
            bool blink = Mathf.Repeat(t * Mathf.Lerp(4f, 16f, k), 1f) < 0.5f;
            foreach (var mark in marks)
            {
                mark.fill.sprite = Fills()[frame];
                mark.fill.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 0.75f, k));
                mark.ring.color = blink ? Color.white : new Color(1f, 0.55f, 0.5f);
            }
            coilLight.intensity = Mathf.Lerp(0.2f, 1.6f, k) * Random.Range(0.7f, 1.1f);
            sparkTimer -= Time.deltaTime;
            if (sparkTimer <= 0f)
            {
                sparkTimer = Mathf.Lerp(0.16f, 0.04f, k);
                HitEffects.Sparks((Vector2)transform.position + CoilTip, Random.insideUnitCircle.normalized, 2, 2.5f, 360f, Color.white, ChargeColor);
            }
            yield return null;
        }

        Vector2 tip = (Vector2)transform.position + CoilTip;
        Sfx.At(strikeSound, transform.position, 0.6f);
        ClearBolts();
        foreach (var mark in marks)
        {
            SpriteRenderer bolt = Bolt(tip, mark.at);
            Destroy(bolt.sprite.texture, 1f);
            Destroy(bolt.gameObject, 1f);
            bolts.Add(bolt);
            HitEffects.Sparks(mark.at, Vector2.up, 10, 4f, 360f, Color.white, ChargeColor);
            HitEffects.Ring(mark.at, ChargeColor, strikeRadius * 1.6f);
            LightFlash.Spawn(mark.at, ChargeColor, 2.5f, 1.4f, 0.2f);
        }
        HurtPlayerIfStruck();
        ClearMarks();
        coilLight.intensity = 2f;
        for (int flick = 0; flick < 3; flick++)
        {
            yield return new WaitForSecondsRealtime(0.06f);
            foreach (SpriteRenderer bolt in bolts) if (bolt != null) bolt.enabled = flick % 2 == 1;
        }
        ClearBolts();
        coilLight.intensity = 0f;
        nextReady = Time.time + cooldown;
        casting = null;
    }

    void HurtPlayerIfStruck()
    {
        if (player == null || playerHealth == null || playerHealth.IsDead) return;
        Vector2 feet = (Vector2)player.position - new Vector2(0f, DepthSort.FeetToCenter);
        foreach (var mark in marks)
        {
            Vector2 off = feet - mark.at;
            float fx = off.x / strikeRadius, fy = off.y / (strikeRadius * FloorSquash);
            if (fx * fx + fy * fy > 1.15f) continue;
            playerHealth.TakeDamage(new DamageInfo(damage, off.sqrMagnitude > 0.001f ? off.normalized : Vector2.down, 0.6f, gameObject, feet));
            CameraShake.Shake(0.35f);
            return;
        }
    }

    void Mark(Vector2 at)
    {
        var root = new GameObject("Strike Mark");
        root.transform.position = new Vector3(at.x, at.y, 0f);
        SpriteRenderer fill = MarkLayer("Fill", root.transform, Fills()[0], 30);
        SpriteRenderer ring = MarkLayer("Ring", root.transform, RingSprite(), 31);
        marks.Add((root, ring, fill, at));
    }

    static SpriteRenderer MarkLayer(string layerName, Transform parent, Sprite sprite, int order)
    {
        var layer = new GameObject(layerName).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.sprite = sprite;
        layer.sortingLayerName = "FloorObject";
        layer.sortingOrder = order;
        if (CombatSprites.EffectMaterial != null) layer.sharedMaterial = CombatSprites.EffectMaterial;
        return layer;
    }

    void ClearMarks()
    {
        foreach (var mark in marks)
            if (mark.root != null) Destroy(mark.root);
        marks.Clear();
    }

    int RadiusPixels => Mathf.Max(6, Mathf.RoundToInt(strikeRadius * PixelsPerUnit));

    Sprite RingSprite()
    {
        int r = RadiusPixels;
        if (rings.TryGetValue(r, out Sprite made) && made != null) return made;
        return rings[r] = Ellipse(r, (d, edge) => edge < 1.6f ? Ring : edge < 2.8f ? RingDark : default);
    }

    Sprite[] Fills()
    {
        int r = RadiusPixels;
        if (fills.TryGetValue(r, out Sprite[] made) && made[0] != null) return made;
        made = new Sprite[FillFrames];
        for (int i = 0; i < FillFrames; i++)
        {
            float reach = (i + 1f) / FillFrames;
            made[i] = Ellipse(r, (d, edge) => d <= reach ? new Color32(255, 70, 50, (byte)(d > reach - 0.12f ? 255 : 150)) : default);
        }
        return fills[r] = made;
    }

    static Sprite Ellipse(int r, System.Func<float, float, Color32> paint)
    {
        int w = r * 2 + 1, h = Mathf.RoundToInt(r * FloorSquash) * 2 + 1;
        float rx = r + 0.5f, ry = h * 0.5f;
        return PixelArt.ToSprite(PixelArt.MakeTexture(w, h, (x, y) =>
        {
            float dx = (x - r) / rx, dy = (y - (h - 1) * 0.5f) / ry;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 1f) return default;
            return paint(d, (1f - d) * ry);
        }), PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    static SpriteRenderer Bolt(Vector2 from, Vector2 to)
    {
        var path = Jagged(from, to, 0.28f);
        var forks = new List<List<Vector2>>();
        for (int i = 0; i < 2; i++)
        {
            Vector2 start = path[Random.Range(1, Mathf.Max(2, path.Count - 1))];
            Vector2 end = start + Random.insideUnitCircle.normalized * Random.Range(0.4f, 0.9f);
            forks.Add(Jagged(start, end, 0.12f));
        }
        var all = new List<Vector2>(path);
        foreach (var fork in forks) all.AddRange(fork);
        Vector2 min = all[0], max = all[0];
        foreach (Vector2 p in all)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        const int Margin = 3;
        int w = Mathf.CeilToInt((max.x - min.x) * PixelsPerUnit) + Margin * 2 + 1;
        int h = Mathf.CeilToInt((max.y - min.y) * PixelsPerUnit) + Margin * 2 + 1;
        var pixels = new Color32[w * h];
        Vector2Int ToPixel(Vector2 p) => new Vector2Int(Mathf.RoundToInt((p.x - min.x) * PixelsPerUnit) + Margin,
                                                        Mathf.RoundToInt((p.y - min.y) * PixelsPerUnit) + Margin);
        void Stamp(Vector2Int c, int radius, Color32 color, bool over)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    if (!over && pixels[y * w + x].a > 0) continue;
                    pixels[y * w + x] = color;
                }
        }
        void Line(List<Vector2> points, int glow, int core)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    Vector2Int a = ToPixel(points[i]), b = ToPixel(points[i + 1]);
                    int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 1);
                    for (int s = 0; s <= steps; s++)
                    {
                        var c = Vector2Int.RoundToInt(Vector2.Lerp(a, b, s / (float)steps));
                        if (pass == 0) Stamp(c, glow, BoltGlow, false);
                        else Stamp(c, core, BoltCore, true);
                    }
                }
        }
        Line(path, 2, 1);
        foreach (var fork in forks) Line(fork, 1, 0);

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        var bolt = new GameObject("Bolt").AddComponent<SpriteRenderer>();
        bolt.sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), Vector2.zero, PixelsPerUnit);
        bolt.transform.position = new Vector3(min.x - Margin / PixelsPerUnit, min.y - Margin / PixelsPerUnit, 0f);
        bolt.sortingLayerName = "Top";
        bolt.sortingOrder = 5;
        if (CombatSprites.EffectMaterial != null) bolt.sharedMaterial = CombatSprites.EffectMaterial;
        return bolt;
    }

    static List<Vector2> Jagged(Vector2 from, Vector2 to, float wander)
    {
        Vector2 along = to - from;
        Vector2 side = along.sqrMagnitude > 0.0001f ? new Vector2(-along.y, along.x).normalized : Vector2.right;
        int segments = Mathf.Max(2, Mathf.CeilToInt(along.magnitude / 0.35f));
        var points = new List<Vector2>();
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float push = i == 0 || i == segments ? 0f : Random.Range(-wander, wander);
            points.Add(from + along * t + side * push);
        }
        return points;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, range);
        Gizmos.color = ChargeColor;
        Gizmos.DrawWireSphere(transform.position, keepAway);
    }
}
