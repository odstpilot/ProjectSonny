using UnityEngine;
using UnityEngine.Rendering.Universal;

// The suit's own light: a soft glow all round the technician, so they can see a little way around them where the
// power's out. It comes up as the room goes dark (DarkRooms sets Level) and fades away again in the light, and it's out
// while they're hidden in a locker or crawling through the ducts.
// In a blacked-out room (DarkRooms.SetBlackout) the suit's power is gone, all but a trickle: the glow shrinks to a faint
// halo right round them, and a faint rim of light traces their outline, so they can always tell where they are.
// It shows how the technician's doing, too, the way the suit's vitals would: as they're hurt it warms toward red, and
// near the end it sputters, dimming and catching. So even with the screens dead, a glance at the glow tells them.
// Added to the player by DarkRooms.
[RequireComponent(typeof(PlayerController))]
public class SuitLight : MonoBehaviour
{
    [Tooltip("How far the glow reaches: fully lit inside the first, fading out to the second.")]
    public float innerRadius = 0.8f;
    public float radius = 4.5f;
    public float intensity = 0.9f;
    public Color color = new Color(1f, 0.94f, 0.82f);

    [Header("Blacked out")]
    [Tooltip("What's left of the glow in a blacked-out room.")]
    public float blackoutRadius = 2.6f;
    public float blackoutIntensity = 0.3f;
    public Color blackoutColor = new Color(0.6f, 0.8f, 1f);
    [Tooltip("The rim of light round the technician's outline there.")]
    public Color outlineColor = new Color(0.55f, 0.85f, 1f, 0.4f);

    public static SuitLight Current { get; private set; }

    // How much of the glow is showing, 0 to 1: up in the dark, down in the light (DarkRooms).
    public float Level { get; set; }
    // In a blacked-out room (DarkRooms): the faint halo and outline instead.
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

    // How hurt they are, 0 untouched to 1 at nothing left.
    float Hurt => health == null || health.maxHealth <= 0f ? 0f : 1f - Mathf.Clamp01(health.CurrentHealth / health.maxHealth);

    // How much of the glow is getting through this moment (sputter), 0 to 1: all of it until they're badly hurt.
    void Sputter(float hurt)
    {
        float share = 1f - hurt;
        if (share > sputterBelow || health.IsDead)
        {
            sputter = 1f;
            return;
        }
        // Dips and catches, held a moment each, more often the closer to nothing.
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
        // Warming only past the first knock, so a scratch doesn't tint it.
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

    // The outline keeps to whatever frame the technician's sprite is showing.
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

    // Four copies of the body, unlit, a pixel out each way and behind it, so only their edges show.
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
