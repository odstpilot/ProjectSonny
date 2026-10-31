using UnityEngine;
using UnityEngine.Rendering.Universal;

// lead wall to hide behind in a surge
[RequireComponent(typeof(BoxCollider2D))]
public class RadiationShield : MonoBehaviour
{
    const float PixelsPerUnit = 32f;
    const int Rise = 30;

    static readonly Color32 Outline = new Color32(20, 18, 22, 255);
    static readonly Color32 Lead = new Color32(70, 74, 86, 255);
    static readonly Color32 LeadLit = new Color32(108, 112, 124, 255);
    static readonly Color32 LeadDark = new Color32(42, 44, 54, 255);
    static readonly Color32 Hazard = new Color32(236, 176, 40, 255);
    static readonly Color32 HazardDark = new Color32(34, 28, 28, 255);
    static readonly Color32 Sign = new Color32(240, 200, 50, 255);

    [Tooltip("How many cells it runs, and whether across the screen (true) or away up it.")]
    public int cells = 3;
    public bool across = true;

    private SpriteRenderer lamp;
    private Light2D lampLight;

    void Awake()
    {
        float w = across ? cells : 1f, d = across ? 1f : cells;
        var solid = GetComponent<BoxCollider2D>();
        solid.size = new Vector2(w * 0.96f, d * 0.96f);
        solid.offset = new Vector2(0f, d * 0.5f);

        var art = new GameObject("Art").transform;
        art.SetParent(transform, false);
        art.localPosition = new Vector3(0f, DepthSort.FeetToCenter, 0f);
        DepthSort.Group(art.gameObject);
        int pw = Mathf.RoundToInt(w * PixelsPerUnit), pd = Mathf.RoundToInt(d * PixelsPerUnit);
        var slab = new GameObject("Slab").AddComponent<SpriteRenderer>();
        slab.transform.SetParent(art, false);
        slab.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        slab.sprite = PixelArt.ToSprite(Draw(pw, pd), PixelsPerUnit, new Vector2(0.5f, 0f));
        slab.sortingLayerName = DepthSort.Layer;

        Vector2 lampAt = new Vector2(0f, (pd + Rise - 3f) / PixelsPerUnit);
        lamp = new GameObject("Lamp").AddComponent<SpriteRenderer>();
        lamp.transform.SetParent(art, false);
        lamp.transform.localPosition = (Vector3)lampAt - new Vector3(0f, DepthSort.FeetToCenter, 0f);
        lamp.sprite = PixelArt.ToSprite(PixelArt.MakeTexture(4, 2, (x, y) => new Color32(255, 255, 255, 255)), PixelsPerUnit, new Vector2(0.5f, 0.5f));
        lamp.sortingLayerName = DepthSort.Layer;
        lamp.sortingOrder = 1;
        if (CombatSprites.EffectMaterial != null) lamp.sharedMaterial = CombatSprites.EffectMaterial;
        var holder = new GameObject("Lamp Light");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = lampAt;
        lampLight = holder.AddComponent<Light2D>();
        lampLight.lightType = Light2D.LightType.Point;
        lampLight.color = new Color(0.4f, 1f, 0.5f);
        lampLight.pointLightOuterRadius = 2.4f;
        lampLight.falloffIntensity = 0.6f;
        lampLight.shadowsEnabled = false;
    }

    void Update()
    {
        float warning = ReactorSurge.Warning;
        float pulse = warning > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 14f) : 0f;
        lamp.color = Color.Lerp(new Color(0.25f, 0.6f, 0.3f), new Color(0.6f, 1f, 0.6f), pulse);
        lampLight.intensity = 0.15f + 1.4f * pulse * warning;
    }

    static Texture2D Draw(int pw, int pd)
    {
        int h = pd + Rise;
        return PixelArt.MakeTexture(pw, h, (x, y) =>
        {
            if (x == 0 || x == pw - 1 || y == 0 || y == h - 1) return Outline;
            if (y >= Rise)
            {
                if (y == Rise) return Outline;
                if (y <= Rise + 4) return (x / 4 + y) % 2 == 0 ? Hazard : HazardDark;
                if ((y - Rise) % 32 == 0 || x % 32 == 0) return LeadDark;
                return x <= 2 ? LeadLit : Lead;
            }
            if (y <= 2) return LeadDark;
            float cx = pw * 0.5f, cy = Rise * 0.5f + 1f;
            float dx = x - cx, dy = y - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r <= 9f)
            {
                if (r <= 2f) return Outline;
                float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 90f;
                bool blade = r >= 3.5f && r <= 8.5f && Mathf.Repeat(a, 120f) < 60f;
                return blade ? Outline : Sign;
            }
            return x <= 2 ? Lead : x >= pw - 3 ? LeadDark : (x + y) % 9 == 0 ? Lead : LeadDark;
        });
    }
}
