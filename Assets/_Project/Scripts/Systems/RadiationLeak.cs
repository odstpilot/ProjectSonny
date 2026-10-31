using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// radiation puddle that slows the player
public class RadiationLeak : MonoBehaviour
{
    public enum Kind { Pool, Vent }

    const float PixelsPerUnit = 32f;
    const int Motes = 22;
    const int PlumeMotes = 34;

    public Kind kind = Kind.Pool;
    [Tooltip("How far it reaches across and up and down, in world units, from its middle: an ellipse on the floor.")]
    public Vector2 radius = new Vector2(2f, 1.2f);
    public float damage = 1f;
    [Tooltip("Seconds after stepping in before the first hit, then between hits while they stay in it.")]
    public float firstHitDelay = 0.8f;
    public float hitEvery = 2.2f;
    [Tooltip("How fast the player can go in it, as a fraction of their speed, and for how long after they're out.")]
    [Range(0.1f, 1f)] public float slowTo = 0.45f;
    public float slowLingers = 0.5f;
    [Tooltip("Vent: seconds quiet, warning, and bursting, and where in that cycle it starts (0 to 1).")]
    public float quietSeconds = 3.2f;
    public float warningSeconds = 1f;
    public float burstSeconds = 2.2f;
    [Range(0f, 1f)] public float phase;
    [Tooltip("How near the Geiger counter starts crackling, in world units.")]
    public float crackleRange = 3.5f;
    [SoundName] public string crackleSound = "Geiger Crackle";
    [SoundName] public string burstSound = "Radiation Burst";
    [Tooltip("What the technician says the first time any of it gets them.")]
    public string firstHurtLine = "Ahh damn, that stuff burns... and I can barely move in it";

    static readonly Color Glow = new Color(1f, 0.86f, 0.25f);
    static readonly Color32 Rim = new Color32(52, 38, 10, 255);
    static readonly Color32[] Ooze =
    {
        new Color32(122, 82, 10, 255), new Color32(186, 136, 16, 255), new Color32(236, 190, 30, 255),
        new Color32(255, 230, 96, 255), new Color32(255, 250, 196, 255),
    };
    static readonly Color32 Steel = new Color32(64, 60, 58, 255);
    static readonly Color32 SteelLit = new Color32(104, 98, 90, 255);
    static readonly Color32 SteelDark = new Color32(30, 28, 28, 255);
    static readonly Color32 Slot = new Color32(12, 12, 10, 255);
    static readonly Dictionary<string, Sprite> made = new Dictionary<string, Sprite>();
    static Sprite pixel;
    static bool saidFirstHurt;
    static float slowedUntil = float.NegativeInfinity;
    static float slowFactor = 1f;

    public static float SpeedFactor => Time.time < slowedUntil ? slowFactor : 1f;
    public static bool PlayerIrradiated => Time.time < slowedUntil;

    class Mote
    {
        public SpriteRenderer sprite;
        public Vector2 from;
        public float age, life, rise, sway, phase;
    }

    private Transform player;
    private Health playerHealth;
    private SpriteRenderer floorArt, glowArt, plume;
    private Sprite slotsLit;
    private Light2D glowLight;
    private readonly List<Mote> motes = new List<Mote>();
    private float inside = -1f;
    private float nextHit;
    private float nextCrackle;
    private bool wasBursting;

    public bool Hot => kind == Kind.Pool || Stage == 2;

    int Stage
    {
        get
        {
            float cycle = quietSeconds + warningSeconds + burstSeconds;
            float t = Mathf.Repeat(Time.time + phase * cycle, cycle);
            return t < quietSeconds ? 0 : t < quietSeconds + warningSeconds ? 1 : 2;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        saidFirstHurt = false;
        slowedUntil = float.NegativeInfinity;
        slowFactor = 1f;
    }

    void Awake()
    {
        int variant = Mathf.Abs(Mathf.RoundToInt(transform.position.x * 7f + transform.position.y * 13f)) % 3;
        if (kind == Kind.Pool)
        {
            floorArt = Layer("Pool", Cached($"pool{variant}", () => DrawPool(variant, false)), 8, false);
            glowArt = Layer("Pool Glow", Cached($"poolglow{variant}", () => DrawPool(variant, true)), 9, true);
        }
        else
        {
            floorArt = Layer("Vent", Cached("vent", () => DrawVent(false)), 8, false);
            slotsLit = Cached("ventlit", () => DrawVent(true));
            glowArt = Layer("Vent Glow", slotsLit, 9, true);
            plume = new GameObject("Plume").AddComponent<SpriteRenderer>();
            plume.transform.SetParent(transform, false);
            plume.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            plume.transform.localScale = new Vector3(2.4f, 3.4f, 1f);
            plume.sprite = CombatSprites.Glow;
            plume.sortingLayerName = DepthSort.Layer;
            plume.sortingOrder = 2;
            if (CombatSprites.EffectMaterial != null) plume.sharedMaterial = CombatSprites.EffectMaterial;
        }

        var holder = new GameObject("Glow Light");
        holder.transform.SetParent(transform, false);
        glowLight = holder.AddComponent<Light2D>();
        glowLight.lightType = Light2D.LightType.Point;
        glowLight.color = Glow;
        glowLight.pointLightInnerRadius = 0.2f;
        glowLight.pointLightOuterRadius = kind == Kind.Pool ? radius.x + 2f : 3.2f;
        glowLight.falloffIntensity = 0.6f;
        glowLight.shadowsEnabled = false;

        if (pixel == null)
        {
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }
        int count = kind == Kind.Pool ? Motes : PlumeMotes;
        for (int i = 0; i < count; i++)
        {
            var mote = new Mote { sprite = new GameObject("Mote").AddComponent<SpriteRenderer>() };
            mote.sprite.transform.SetParent(transform, false);
            mote.sprite.sprite = pixel;
            mote.sprite.sortingLayerName = DepthSort.Layer;
            mote.sprite.sortingOrder = 3;
            if (CombatSprites.EffectMaterial != null) mote.sprite.sharedMaterial = CombatSprites.EffectMaterial;
            Respawn(mote);
            mote.age = Random.value * mote.life;
            motes.Add(mote);
        }
    }

    void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found == null) return;
        player = found.transform;
        playerHealth = found.GetComponent<Health>();
    }

    void Update()
    {
        int stage = kind == Kind.Vent ? Stage : 2;
        bool bursting = stage == 2;
        float t = Time.time;

        float shimmer = 0.75f + 0.25f * Mathf.Sin(t * 3.1f + transform.position.x);
        if (kind == Kind.Pool)
        {
            glowArt.color = new Color(1f, 1f, 1f, shimmer);
            glowLight.intensity = 0.85f * shimmer;
        }
        else
        {
            float lit = stage == 0 ? 0.08f : stage == 1 ? (Mathf.Repeat(t * 7f, 1f) < 0.5f ? 0.9f : 0.15f) : 1f;
            glowArt.color = new Color(1f, 1f, 1f, lit);
            plume.color = new Color(Glow.r, Glow.g, Glow.b, bursting ? 0.35f * shimmer : 0f);
            glowLight.intensity = stage == 0 ? 0.1f : stage == 1 ? 0.5f * lit : 1.3f * shimmer;
            if (bursting && !wasBursting && player != null && Vector2.Distance(player.position, transform.position) < crackleRange * 2.5f)
                Sfx.At(burstSound, transform.position, 0.9f);
        }
        wasBursting = bursting;

        foreach (Mote mote in motes)
        {
            bool shown = kind == Kind.Pool || bursting;
            mote.age += Time.deltaTime * (shown ? 1f : 3f);
            if (mote.age >= mote.life) Respawn(mote);
            if (!shown)
            {
                mote.sprite.enabled = false;
                continue;
            }
            mote.sprite.enabled = true;
            float k = mote.age / mote.life;
            Vector2 at = mote.from + new Vector2(Mathf.Sin(mote.phase + mote.age * 3f) * mote.sway, mote.rise * mote.age);
            at = new Vector2(Mathf.Round(at.x * PixelsPerUnit), Mathf.Round(at.y * PixelsPerUnit)) / PixelsPerUnit;
            mote.sprite.transform.localPosition = at;
            Color c = Color.Lerp(new Color(1f, 0.98f, 0.7f), Glow, k);
            mote.sprite.color = new Color(c.r, c.g, c.b, 1f - k * k);
        }

        HurtPlayer(bursting || kind == Kind.Pool);
    }

    void HurtPlayer(bool hot)
    {
        if (player == null || playerHealth == null || playerHealth.IsDead || Teleporter.IsTravelling) return;
        Vector2 feet = (Vector2)player.position - new Vector2(0f, DepthSort.FeetToCenter);
        Vector2 off = feet - (Vector2)transform.position;
        float distance = off.magnitude;
        if (distance < crackleRange && Time.time >= nextCrackle)
        {
            float near = 1f - distance / crackleRange;
            nextCrackle = Time.time + Mathf.Lerp(0.6f, 0.12f, near) * Random.Range(0.7f, 1.3f);
            Sfx.Play(crackleSound, Mathf.Lerp(0.15f, 0.7f, near) * (hot ? 1f : 0.5f), Random.Range(0.9f, 1.15f));
        }

        float ex = off.x / radius.x, ey = off.y / radius.y;
        if (!hot || ex * ex + ey * ey > 1f)
        {
            inside = -1f;
            return;
        }
        if (inside < 0f)
        {
            inside = 0f;
            nextHit = Time.time + firstHitDelay;
        }
        slowFactor = Time.time < slowedUntil ? Mathf.Min(slowFactor, slowTo) : slowTo;
        slowedUntil = Time.time + slowLingers;
        inside += Time.deltaTime;
        if (Time.time < nextHit) return;
        nextHit = Time.time + hitEvery;
        Vector2 away = distance > 0.01f ? off / distance : Vector2.down;
        playerHealth.TakeDamage(new DamageInfo(damage, away, 0.3f, gameObject, feet));
        if (!saidFirstHurt && !string.IsNullOrEmpty(firstHurtLine))
        {
            saidFirstHurt = true;
            TechnicianVoice.Say(firstHurtLine);
        }
    }

    void Respawn(Mote mote)
    {
        mote.age = 0f;
        Vector2 spot = Random.insideUnitCircle;
        if (kind == Kind.Pool)
        {
            mote.from = new Vector2(spot.x * radius.x * 0.8f, spot.y * radius.y * 0.6f);
            mote.life = Random.Range(1f, 2.2f);
            mote.rise = Random.Range(0.5f, 1.1f);
            mote.sway = Random.Range(0.03f, 0.15f);
        }
        else
        {
            mote.from = new Vector2(spot.x * 0.5f, spot.y * 0.2f);
            mote.life = Random.Range(0.5f, 1.2f);
            mote.rise = Random.Range(2.5f, 4.5f);
            mote.sway = Random.Range(0.05f, 0.35f);
        }
        mote.phase = Random.value * 6.3f;
        mote.sprite.transform.localScale = Vector3.one * (Random.value < 0.3f ? 2f : 1f);
    }

    SpriteRenderer Layer(string layerName, Sprite sprite, int order, bool glows)
    {
        var layer = new GameObject(layerName).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(transform, false);
        layer.sprite = sprite;
        layer.sortingLayerName = "Floor";
        layer.sortingOrder = order;
        if (glows && CombatSprites.EffectMaterial != null) layer.sharedMaterial = CombatSprites.EffectMaterial;
        return layer;
    }

    static Sprite Cached(string key, System.Func<Sprite> draw)
    {
        if (made.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
        return made[key] = draw();
    }

    static Sprite DrawPool(int variant, bool glowOnly)
    {
        const int W = 136, H = 84;
        var dice = new System.Random(variant * 977 + 13);
        var lobes = new List<(float x, float y, float r)>();
        for (int i = 0; i < 7; i++)
            lobes.Add(((float)dice.NextDouble() * 64f - 32f, (float)dice.NextDouble() * 20f - 10f, 17f + (float)dice.NextDouble() * 14f));
        var bubbles = new List<(int x, int y)>();
        for (int i = 0; i < 12; i++) bubbles.Add((36 + dice.Next(64), 26 + dice.Next(32)));
        return PixelArt.ToSprite(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            float best = 0f;
            foreach ((float lx, float ly, float r) in lobes)
            {
                float dx = (x - W * 0.5f - lx) / r, dy = (y - H * 0.5f - ly) / (r * 0.6f);
                best = Mathf.Max(best, 1f - Mathf.Sqrt(dx * dx + dy * dy));
            }
            if (best <= 0f) return default;
            if (best < 0.08f) return glowOnly ? default : Rim;
            foreach ((int bx, int by) in bubbles)
                if (Mathf.Abs(x - bx) + Mathf.Abs(y - by) <= 1 && best > 0.3f) return Ooze[4];
            int band = Mathf.Clamp(Mathf.FloorToInt(best * 5.5f + ((x + y) % 2) * 0.35f), 0, 4);
            if (glowOnly && band < 2) return default;
            return Ooze[band];
        }), PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    static Sprite DrawVent(bool lit)
    {
        const int W = 52, H = 34;
        return PixelArt.ToSprite(PixelArt.MakeTexture(W, H, (x, y) =>
        {
            float dx = (x - W * 0.5f + 0.5f) / (W * 0.5f), dy = (y - H * 0.5f + 0.5f) / (H * 0.5f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 1f) return default;
            bool rim = d > 0.78f;
            bool slot = !rim && y % 4 < 2 && Mathf.Abs(dx) < 0.62f;
            if (lit) return slot ? (Color32)Color.Lerp(new Color32(236, 190, 30, 255), new Color32(255, 250, 196, 255), 1f - d) : default;
            if (d > 0.94f) return SteelDark;
            if (rim) return dy > 0f ? SteelLit : Steel;
            if (slot) return Slot;
            return SteelDark;
        }), PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Glow;
        Vector3 last = transform.position + new Vector3(radius.x, 0f, 0f);
        for (int i = 1; i <= 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y, 0f);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}
