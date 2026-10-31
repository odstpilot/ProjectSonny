using UnityEngine;
using UnityEngine.Rendering.Universal;

// soft light around the player
[RequireComponent(typeof(PlayerController))]
public class SuitLight : MonoBehaviour
{
    [Tooltip("How far the glow reaches: fully lit inside the first, fading out to the second.")]
    public float innerRadius = 0.8f;
    public float radius = 4.5f;
    public float intensity = 0.6f;
    public Color color = new Color(1f, 0.94f, 0.82f);

    [Header("Blacked out")]
    [Tooltip("What's left of the glow in a blacked-out room.")]
    public float blackoutRadius = 2.6f;
    public float blackoutIntensity = 0.22f;
    public Color blackoutColor = new Color(0.6f, 0.8f, 1f);
    [Tooltip("The rim of light round the technician's outline there.")]
    public Color outlineColor = new Color(0.55f, 0.85f, 1f, 0.4f);

    public static SuitLight Current { get; private set; }

    public float Level { get; set; }
    public bool Blackout { get; set; }

    [Header("Hurt")]
    [Tooltip("What the glow warms toward as health runs out.")]
    public Color hurtColor = new Color(1f, 0.22f, 0.12f);
    [Tooltip("At or below this share of health, the glow sputters.")]
    [Range(0f, 1f)] public float sputterBelow = 0.3f;

    static readonly Vector2[] OutlineSteps = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

    private Light2D glow;
    private Health health;
    private float sputter = 1f, nextSputter;
    private SpriteRenderer body;
    private SpriteRenderer[] outline;

    void Awake()
    {
        Current = this;
        glow = new GameObject("Suit Glow").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        glow.lightType = Light2D.LightType.Point;
        glow.color = color;
        glow.pointLightInnerRadius = innerRadius;
        glow.pointLightOuterRadius = radius;
        glow.falloffIntensity = 0.6f;
        glow.shadowsEnabled = false;
        glow.enabled = false;
        health = GetComponent<Health>();
        BuildOutline();
    }

    float Hurt => health == null || health.maxHealth <= 0f ? 0f : 1f - Mathf.Clamp01(health.CurrentHealth / health.maxHealth);

    void Sputter(float hurt)
    {
        float share = 1f - hurt;
        if (share > sputterBelow || health.IsDead)
        {
            sputter = 1f;
            return;
        }
        if (Time.time < nextSputter) return;
        nextSputter = Time.time + Random.Range(0.05f, 0.25f);
        sputter = Random.value < Mathf.Lerp(0.15f, 0.45f, Mathf.InverseLerp(sputterBelow, 0f, share)) ? Random.Range(0.15f, 0.5f) : 1f;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        bool hidden = Locker.IsPlayerHidden;
        float hurt = Hurt;
        if (health != null) Sputter(hurt);
        float warm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.85f, hurt));
        if (Blackout)
        {
            glow.enabled = !hidden;
            glow.color = Color.Lerp(blackoutColor, hurtColor, warm);
            glow.pointLightInnerRadius = 0f;
            glow.pointLightOuterRadius = blackoutRadius;
            glow.intensity = blackoutIntensity * sputter;
        }
        else
        {
            float power = hidden ? 0f : Level;
            glow.enabled = power > 0.001f;
            glow.color = Color.Lerp(color, hurtColor, warm);
            glow.pointLightInnerRadius = innerRadius;
            glow.pointLightOuterRadius = radius;
            glow.intensity = intensity * power * sputter;
        }
    }

    void LateUpdate()
    {
        if (outline == null) return;
        bool show = Blackout && !Locker.IsPlayerHidden && body.enabled && body.sprite != null;
        float pixel = body.sprite != null ? 1f / body.sprite.pixelsPerUnit : 0.05f;
        for (int i = 0; i < outline.Length; i++)
        {
            SpriteRenderer rim = outline[i];
            rim.enabled = show;
            if (!show) continue;
            rim.sprite = body.sprite;
            rim.flipX = body.flipX;
            rim.flipY = body.flipY;
            Color rimColor = Color.Lerp(outlineColor, hurtColor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.85f, Hurt)));
            rim.color = new Color(rimColor.r, rimColor.g, rimColor.b, outlineColor.a * sputter);
            rim.transform.localPosition = OutlineSteps[i] * pixel;
        }
    }

    void BuildOutline()
    {
        body = GetComponent<SpriteRenderer>();
        if (body == null) return;
        Material unlit = CombatSprites.EffectMaterial;
        outline = new SpriteRenderer[OutlineSteps.Length];
        for (int i = 0; i < OutlineSteps.Length; i++)
        {
            var rim = new GameObject("Suit Outline").AddComponent<SpriteRenderer>();
            rim.transform.SetParent(body.transform, false);
            if (unlit != null) rim.sharedMaterial = unlit;
            rim.sortingLayerID = body.sortingLayerID;
            rim.sortingOrder = body.sortingOrder - 1;
            rim.enabled = false;
            outline[i] = rim;
        }
    }
}
