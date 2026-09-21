using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A monitor Sonny can take over. While Sonny is speaking the screen stutters over to a deep red, pulses with the voice,
// throws a little red light on the floor, and stutters back to normal afterwards. SetAll switches every one in the scene.
// Needs nothing set up: it draws its red, in scanlines with Sonny's eye in the middle, over the rectangle given by
// screenCenter and screenSize.
public class SonnyScreen : MonoBehaviour
{
    const float StutterTime = 0.45f;
    static readonly Color Red = new Color(1f, 0.12f, 0.08f);

    static readonly List<SonnyScreen> screens = new List<SonnyScreen>();

    [Tooltip("The middle of the screen, relative to this object.")]
    public Vector2 screenCenter;
    public Vector2 screenSize = new Vector2(1.8f, 0.45f);
    [Range(0f, 1f)] public float strength = 0.85f;

    public bool IsOn { get; private set; }

    private SpriteRenderer face;
    private Light2D glow;
    private float level;             // 0 normal, 1 all Sonny
    private float switchedAt = -10f;
    private float seed;

    // Every screen in the scene over to Sonny, or back.
    public static void SetAll(bool on)
    {
        foreach (SonnyScreen screen in screens)
            if (screen != null) screen.Set(on);
    }

    public void Set(bool on)
    {
        if (on == IsOn) return;
        IsOn = on;
        switchedAt = Time.time;
    }

    void Awake()
    {
        seed = Random.value * 10f;

        face = new GameObject("Sonny Face").AddComponent<SpriteRenderer>();
        face.transform.SetParent(transform, false);
        face.transform.localPosition = screenCenter;
        face.sprite = DrawFace();
        face.sortingLayerName = DepthSort.Layer;
        face.sortingOrder = 10;     // over the monitor's own tiles, within its sorting group
        if (CombatSprites.EffectMaterial != null) face.sharedMaterial = CombatSprites.EffectMaterial;

        var glowObject = new GameObject("Sonny Glow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = screenCenter + new Vector2(0f, -1f);
        glow = glowObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = Red;
        glow.pointLightInnerRadius = 0.3f;
        glow.pointLightOuterRadius = 3f;
        glow.falloffIntensity = 0.7f;

        Apply(0f);
    }

    void OnDestroy() => PixelCanvas.Destroy(face != null ? face.sprite : null);

    // White, for the red to tint: scanlines at the tileset's pixel size, and Sonny's eye, a ring round a bright pupil,
    // in the middle of the screen.
    Sprite DrawFace()
    {
        int width = Mathf.Max(2, Mathf.RoundToInt(screenSize.x * PixelCanvas.DetailPixelsPerUnit));
        int height = Mathf.Max(2, Mathf.RoundToInt(screenSize.y * PixelCanvas.DetailPixelsPerUnit));
        var canvas = new PixelCanvas(width, height);
        Vector2 middle = new Vector2(width * 0.5f, height * 0.5f);
        float ring = height * 0.36f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), middle);
                bool eye = Mathf.Abs(distance - ring) < 0.9f || distance < ring * 0.35f;
                byte alpha = eye ? (byte)255 : y % 2 == 0 ? (byte)190 : (byte)110;
                canvas.Set(x, y, new Color32(255, 255, 255, alpha));
            }
        }
        return canvas.ToSprite(new Vector2(0.5f, 0.5f));
    }

    void OnEnable() => screens.Add(this);
    void OnDisable() => screens.Remove(this);

    void Update()
    {
        float target = IsOn ? 1f : 0f;
        float sinceSwitch = Time.time - switchedAt;

        // Stutters between old and new for a moment, like the signal fighting to take over.
        if (sinceSwitch < StutterTime)
            level = Random.value < sinceSwitch / StutterTime ? target : 1f - target;
        else
            level = target;

        Apply(level);
    }

    void Apply(float amount)
    {
        // Pulses as if with the voice, and rolls a faint band down the screen.
        float pulse = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, seed);
        float band = 0.9f + 0.1f * Mathf.Sin(Time.time * 9f + seed);
        Color color = Red;
        color.a = amount * strength * pulse * band;
        face.color = color;
        face.enabled = color.a > 0.01f;

        glow.intensity = amount * 0.9f * pulse;
        glow.enabled = glow.intensity > 0.01f;
    }
}
