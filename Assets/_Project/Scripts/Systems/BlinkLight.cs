using UnityEngine;
using UnityEngine.Rendering.Universal;

// small blinking light
public class BlinkLight : MonoBehaviour
{
    public enum Pattern { Blink, Pulse, Flicker }

    public Pattern pattern = Pattern.Blink;
    public Color color = new Color(1f, 0.2f, 0.15f);
    [Tooltip("How far its glow reaches, and how bright it gets.")]
    public float radius = 1.5f;
    public float intensity = 0.6f;
    [Tooltip("Seconds for one blink or pulse.")]
    public float period = 1.2f;
    [Tooltip("Blink: how much of each period it's on.")]
    [Range(0.05f, 0.95f)] public float onFraction = 0.35f;
    [Tooltip("Where in its pattern it starts, 0 to 1.")]
    [Range(0f, 1f)] public float phase;

    private Light2D glow;
    private float dropUntil;
    private float nextDrop;

    void Awake()
    {
        glow = gameObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.falloffIntensity = 0.7f;
        glow.shadowsEnabled = false;
        glow.pointLightInnerRadius = 0f;
        glow.pointLightOuterRadius = radius;
        glow.color = color;
    }

    void Update()
    {
        float level = Level(Time.time / Mathf.Max(0.05f, period) + phase);
        glow.intensity = intensity * level;
        glow.enabled = level > 0.01f;
    }

    float Level(float cycles)
    {
        switch (pattern)
        {
            case Pattern.Pulse:
                return 0.5f + 0.5f * Mathf.Sin(cycles * 2f * Mathf.PI);
            case Pattern.Flicker:
                if (Time.time >= nextDrop)
                {
                    dropUntil = Time.time + Random.Range(0.05f, 0.3f);
                    nextDrop = dropUntil + Random.Range(0.4f, 3f) * period;
                }
                if (Time.time < dropUntil) return Random.value < 0.3f ? 0.4f : 0f;
                return 0.6f + 0.4f * Mathf.PerlinNoise(cycles * 3f, phase * 10f);
            default:
                return Mathf.Repeat(cycles, 1f) < onFraction ? 1f : 0f;
        }
    }
}
