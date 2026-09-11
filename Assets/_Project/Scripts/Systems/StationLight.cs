using UnityEngine;
using UnityEngine.Rendering.Universal;

// A lamp on the station: steady, flickering like a failing tube, or pulsing like an alarm. Every lamp stutters while
// the station rumbles (see StationRumble). Alarms usually start off and get switched on by the level with SetOn.
[RequireComponent(typeof(Light2D))]
public class StationLight : MonoBehaviour
{
    const float StutterStep = 0.06f;    // seconds between changes while stuttering, so it doesn't depend on frame rate

    public enum Mode { Steady, Flicker, Alarm }

    public Mode mode = Mode.Steady;
    public bool startOn = true;
    [Tooltip("Alarm: pulses per second.")]
    public float pulseRate = 1.2f;
    [Tooltip("Flicker: roughly how many times a second it cuts out.")]
    public float flickerRate = 0.6f;
    [Tooltip("How badly it stutters when the station rumbles, 0 to 1.")]
    [Range(0f, 1f)] public float rumbleStutter = 0.7f;
    [Tooltip("Optional sprite that brightens and dims with the light, like the lamp itself.")]
    public SpriteRenderer bulb;

    public bool IsOn { get; private set; }

    private Light2D lamp;
    private float baseIntensity;
    private Color bulbColor;
    private float nextCut;
    private float cutUntil;
    private float stutterUntil;
    private float nextStutterStep;
    private float stutterLevel = 1f;

    void Awake()
    {
        lamp = GetComponent<Light2D>();
        baseIntensity = lamp.intensity;
        if (bulb != null) bulbColor = bulb.color;
        IsOn = startOn;
        Apply(IsOn ? 1f : 0f);
    }

    void OnEnable()
    {
        StationRumble.Rumbled += OnRumble;
    }

    void OnDisable()
    {
        StationRumble.Rumbled -= OnRumble;
    }

    public void SetOn(bool on)
    {
        IsOn = on;
        if (!on) Apply(0f);
    }

    void OnRumble(float strength, float duration)
    {
        stutterUntil = Mathf.Max(stutterUntil, Time.time + duration * Mathf.Clamp01(strength * 1.5f));
    }

    void Update()
    {
        if (!IsOn) return;

        float level = 1f;
        if (mode == Mode.Flicker)
        {
            if (Time.time >= nextCut)
            {
                cutUntil = Time.time + Random.Range(0.03f, 0.2f);
                nextCut = Time.time + Random.Range(0.3f, 2f) / Mathf.Max(0.01f, flickerRate);
            }
            if (Time.time < cutUntil) level = Random.Range(0f, 0.3f);
        }
        else if (mode == Mode.Alarm)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseRate);
            level = 0.1f + 0.9f * wave * wave;
        }

        if (Time.time < stutterUntil)
        {
            if (Time.time >= nextStutterStep)
            {
                stutterLevel = Random.value < rumbleStutter * 0.5f ? Random.Range(0.05f, 0.5f) : 1f;
                nextStutterStep = Time.time + StutterStep;
            }
            level *= stutterLevel;
        }

        Apply(level);
    }

    void Apply(float level)
    {
        lamp.intensity = baseIntensity * level;
        if (bulb == null) return;

        Color color = bulbColor;
        color.a *= Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(level));
        bulb.color = color;
    }
}
