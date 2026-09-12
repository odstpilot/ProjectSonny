using UnityEngine;
using UnityEngine.Rendering.Universal;

// A crackling ball of energy: a soft glow, a bright core, a light so it brightens the room, and sparks that jump off
// it. The blaster grows one at its muzzle while charging (WeaponVisual) and launches another as its shot (Projectile).
public class EnergyOrb : MonoBehaviour
{
    const float PulseSpeed = 14f;
    const float CoreScale = 0.6f;      // the core's size, as a fraction of Size
    const float HaloScale = 1.8f;      // the glow's size, as a multiple of Size
    const float SpecksPerSecond = 40f;

    // Across the ball, in world units.
    public float Size { get; set; }
    // 0 to 1: how bright it glows, how far its light reaches, and how often sparks jump off it.
    public float Strength { get; set; } = 1f;
    // Flickers and throws off more sparks, for a ball that's full and ready to go.
    public bool Unstable { get; set; }
    // Pulls specks of energy in from the air around it, for a ball that's still charging.
    public bool DrawingIn { get; set; }

    private BlasterWeaponData data;
    private SpriteRenderer halo;
    private SpriteRenderer core;
    private Light2D glow;
    private TrailRenderer trail;
    private float sparkTimer;
    private float specksOwed;
    private float seed;

    public static EnergyOrb Create(string orbName, Transform parent, BlasterWeaponData data, SpriteRenderer sortingSource, bool withTrail)
    {
        var orb = new GameObject(orbName).AddComponent<EnergyOrb>();
        orb.transform.SetParent(parent, false);
        orb.data = data;
        orb.Size = data.ballSize;
        orb.seed = Random.value * 100f;

        Material material = CombatSprites.EffectMaterial;
        if (material == null && sortingSource != null) material = sortingSource.sharedMaterial;

        orb.halo = NewSprite("Halo", orb.transform, CombatSprites.Glow, material);
        orb.core = NewSprite("Core", orb.transform, data.projectileSprite != null ? data.projectileSprite : CombatSprites.Disc, material);

        if (withTrail)
        {
            orb.trail = orb.gameObject.AddComponent<TrailRenderer>();
            orb.trail.time = 0.12f;
            orb.trail.minVertexDistance = 0.03f;
            orb.trail.widthMultiplier = data.ballSize * 0.8f;
            orb.trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            orb.trail.startColor = WithAlpha(data.energyColor, 0.55f);
            orb.trail.endColor = WithAlpha(data.energyColor, 0f);
            orb.trail.sharedMaterial = material;
        }

        if (data.glowRadius > 0f)
        {
            orb.glow = orb.gameObject.AddComponent<Light2D>();
            orb.glow.lightType = Light2D.LightType.Point;
            orb.glow.pointLightInnerRadius = 0f;
            orb.glow.falloffIntensity = 0.6f;
            orb.glow.color = data.energyColor;
        }

        if (sortingSource != null)
            orb.SetSorting(sortingSource.sortingLayerID, sortingSource.sortingOrder + 1);
        orb.Refresh();
        return orb;
    }

    public void SetSorting(int sortingLayerID, int order)
    {
        halo.sortingLayerID = sortingLayerID;
        core.sortingLayerID = sortingLayerID;
        halo.sortingOrder = order;
        core.sortingOrder = order + 1;
        if (trail != null)
        {
            trail.sortingLayerID = sortingLayerID;
            trail.sortingOrder = order;
        }
    }

    // Call after moving the ball somewhere instantly, so the trail doesn't streak across the jump.
    public void ClearTrail()
    {
        if (trail != null) trail.Clear();
    }

    void Update()
    {
        Refresh();
        EmitSparks();
        if (DrawingIn) EmitSpecks();
    }

    void Refresh()
    {
        float time = Time.time * PulseSpeed + seed;
        float wobble = 1f + 0.08f * Mathf.Sin(time);
        if (Unstable) wobble += Random.Range(-0.1f, 0.1f);

        core.transform.localScale = Vector3.one * (Size * CoreScale * wobble);
        halo.transform.localScale = Vector3.one * (Size * (HaloScale + 0.2f * Mathf.Sin(time * 0.53f)));
        halo.color = WithAlpha(data.energyColor, 0.7f * Strength);
        core.color = WithAlpha(data.coreColor, Mathf.Clamp01(Strength * 2f));

        if (glow != null)
        {
            glow.intensity = data.glowIntensity * Strength * wobble;
            glow.pointLightOuterRadius = Mathf.Max(0.01f, data.glowRadius * Mathf.Lerp(0.3f, 1f, Strength));
        }
    }

    void EmitSparks()
    {
        sparkTimer -= Time.deltaTime;
        if (sparkTimer > 0f || Size < 0.05f) return;

        // Stronger balls throw sparks more often; full ones twice as often again.
        sparkTimer = Random.Range(0.04f, 0.12f) / Mathf.Max(0.25f, Strength) * (Unstable ? 0.5f : 1f);
        Vector2 direction = RandomDirection();
        Vector2 surface = (Vector2)transform.position + direction * (Size * 0.35f);
        HitEffects.Sparks(surface, direction, Unstable ? 3 : 2, 2f + 3f * Strength, 50f, data.coreColor, data.energyColor);
    }

    // Motes drift in from a ring around the ball, like it's pulling energy out of the air.
    void EmitSpecks()
    {
        specksOwed += Time.deltaTime * SpecksPerSecond;
        while (specksOwed >= 1f)
        {
            specksOwed -= 1f;
            Vector2 direction = RandomDirection();
            float distance = Random.Range(0.5f, 0.9f);
            float lifetime = Random.Range(0.18f, 0.28f);
            Vector2 from = (Vector2)transform.position + direction * distance;
            HitEffects.Speck(from, -direction * (distance / lifetime), lifetime, Random.Range(0.03f, 0.05f), data.energyColor);
        }
    }

    static Vector2 RandomDirection()
    {
        float angle = Random.value * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    static SpriteRenderer NewSprite(string spriteName, Transform parent, Sprite sprite, Material material)
    {
        var spriteRenderer = new GameObject(spriteName).AddComponent<SpriteRenderer>();
        spriteRenderer.transform.SetParent(parent, false);
        spriteRenderer.sprite = sprite;
        if (material != null) spriteRenderer.sharedMaterial = material;
        return spriteRenderer;
    }
}
