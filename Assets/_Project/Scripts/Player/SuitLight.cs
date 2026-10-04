using UnityEngine;
using UnityEngine.Rendering.Universal;

// The suit's own light: a soft glow all round the technician, so they can see a little way around them where the
// power's out. It comes up as the room goes dark (DarkRooms sets Level) and fades away again in the light, and it's out
// while they're hidden in a locker or crawling through the ducts.
// In a blacked-out room (DarkRooms.SetBlackout) the suit's power is gone, all but a trickle: the glow shrinks to a faint
// halo right round them, and a faint rim of light traces their outline, so they can always tell where they are.
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
    public float blackoutRadius = 1.5f;
    public float blackoutIntensity = 0.22f;
    public Color blackoutColor = new Color(0.6f, 0.8f, 1f);
    [Tooltip("The rim of light round the technician's outline there.")]
    public Color outlineColor = new Color(0.55f, 0.85f, 1f, 0.4f);

    public static SuitLight Current { get; private set; }

    // How much of the glow is showing, 0 to 1: up in the dark, down in the light (DarkRooms).
    public float Level { get; set; }
    // In a blacked-out room (DarkRooms): the faint halo and outline instead.
    public bool Blackout { get; set; }

    static readonly Vector2[] OutlineSteps = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

    private Light2D glow;
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
        BuildOutline();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        bool hidden = Locker.IsPlayerHidden;
        if (Blackout)
        {
            glow.enabled = !hidden;
            glow.color = blackoutColor;
            glow.pointLightInnerRadius = 0f;
            glow.pointLightOuterRadius = blackoutRadius;
            glow.intensity = blackoutIntensity;
        }
        else
        {
            float power = hidden ? 0f : Level;
            glow.enabled = power > 0.001f;
            glow.color = color;
            glow.pointLightInnerRadius = innerRadius;
            glow.pointLightOuterRadius = radius;
            glow.intensity = intensity * power;
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
            rim.color = outlineColor;
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
