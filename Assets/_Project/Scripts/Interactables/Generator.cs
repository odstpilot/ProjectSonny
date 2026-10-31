using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

// backup generator to start
public class Generator : Terminal
{
    const float PixelsPerUnit = 20f;
    const int ArtWidth = 40;
    const int ArtHeight = 30;

    static readonly Color Pinged = new Color(1f, 0.66f, 0.25f);
    static readonly Color Running = new Color(0.45f, 1f, 0.55f);
    static readonly Color SparkBlue = new Color(0.3f, 0.8f, 1f);
    static readonly Color SparkWhite = new Color(0.85f, 0.97f, 1f);

    [Tooltip("Shown at the top of the start-up screen.")]
    public string displayName = "BACKUP GENERATOR";

    [Header("Starting it")]
    [Tooltip("Coils to bring up, one at a time.")]
    [Range(1, 5)] public int coils = 3;
    [Tooltip("How fast the rotor's needle crosses the gauge, in gauges a second, for the first coil and each one after.")]
    public float needleSpeed = 0.6f;
    public float needleSpeedUp = 0.2f;
    [Tooltip("How wide the sync window is, as a fraction of the gauge, for the first coil and how much narrower each one after.")]
    [Range(0.05f, 0.5f)] public float window = 0.2f;
    [Range(0f, 0.1f)] public float windowShrink = 0.04f;

    [Header("Showing up in the dark")]
    [Tooltip("Seconds its lamps blink after an EMP's flash catches it.")]
    public float pingSeconds = 4f;

    [Header("Look")]
    [Tooltip("Leave empty for the placeholder generator. Pivot at the bottom middle.")]
    public Sprite sprite;

    public UnityEvent onStarted = new UnityEvent();
    public event System.Action Started;
    public event System.Action Backfired;

    public bool IsRunning { get; private set; }
    [System.NonSerialized] public bool locked;
    public int CoilsLit { get; set; }
    public override bool InUse => GeneratorScreen.Current != null && GeneratorScreen.Current.Generator == this;
    protected override string PromptText => IsRunning ? "RUNNING" : locked ? "OFFLINE" : "START GENERATOR";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, ArtHeight / PixelsPerUnit + 0.2f, 0f);
    protected override bool Available => !IsRunning && !locked;

    public float WindowFor(int coil) => Mathf.Max(0.04f, window - windowShrink * coil);
    public float SpeedFor(int coil) => needleSpeed + needleSpeedUp * coil;

    private SpriteRenderer[] lamps;
    private Light2D glow;
    private float pingUntil = -1f;

    protected override void Awake()
    {
        base.Awake();
        BuildArt();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EmpPulse.Swept += OnSwept;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        EmpPulse.Swept -= OnSwept;
        if (InUse) GeneratorScreen.Current.Close();
    }

    protected override void Update()
    {
        UpdateLamps();
        base.Update();
    }

    protected override void Use(Transform player)
    {
        GeneratorScreen.Get(screenFont).Open(this, player);
    }

    void OnSwept(Vector2 origin, Vector2 direction, float radius, float halfAngle)
    {
        if (IsRunning) return;
        Vector2 middle = (Vector2)transform.position + new Vector2(0f, ArtHeight * 0.5f / PixelsPerUnit);
        if (!EmpPulse.InCone(middle, origin, direction, radius, halfAngle)) return;
        pingUntil = Time.time + pingSeconds;
        HitEffects.Sparks(middle, Vector2.up, 14, 3.5f, 360f, SparkWhite, SparkBlue);
        LightFlash.Spawn(middle, Pinged, 2.4f, 1.2f, 0.5f);
    }

    public void MarkStarted(bool quietly = false)
    {
        if (IsRunning) return;
        IsRunning = true;
        CoilsLit = coils;
        if (!quietly)
        {
            Vector2 middle = (Vector2)transform.position + new Vector2(0f, 0.7f);
            CameraShake.Shake(0.5f);
            StationRumble.Punch(0.6f);
            HitEffects.Sparks(middle, Vector2.up, 24, 5f, 160f, SparkWhite, Running);
            LightFlash.Spawn(middle, Running, 4f, 1.6f, 0.6f);
        }
        Started?.Invoke();
        onStarted.Invoke();
    }

    public void Backfire()
    {
        CoilsLit = Mathf.Max(0, CoilsLit - 1);
        Vector2 middle = (Vector2)transform.position + new Vector2(0f, 0.7f);
        CameraShake.Shake(0.35f);
        StationRumble.PlayImpact(middle);
        HitEffects.Sparks(middle, Vector2.up, 20, 6f, 200f, Pinged, Color.white);
        LightFlash.Spawn(middle, Pinged, 3f, 1.4f, 0.25f);
        Backfired?.Invoke();
    }

    void UpdateLamps()
    {
        bool pinged = Time.time < pingUntil;
        bool blinkOn = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
        for (int i = 0; i < lamps.Length; i++)
        {
            Color color;
            if (IsRunning) color = Running;
            else if (i < CoilsLit) color = Pinged;
            else if (pinged) color = blinkOn ? Pinged * 0.7f : Pinged * 0.25f;
            else color = Pinged * (0.1f + 0.12f * pulse);
            color.a = 1f;
            lamps[i].color = color;
        }
        if (glow == null) return;
        glow.enabled = true;
        glow.color = IsRunning ? Running : Pinged;
        glow.pointLightOuterRadius = IsRunning ? 3.6f : pinged ? 2.4f : 1.3f;
        glow.intensity = IsRunning ? 1.1f + 0.08f * Mathf.Sin(Time.time * 20f)
            : pinged ? (blinkOn ? 0.55f : 0.2f)
            : CoilsLit > 0 ? 0.3f
            : 0.06f + 0.06f * pulse;
    }

    void BuildArt()
    {
        var body = new GameObject("Generator").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        body.sprite = sprite != null ? sprite : MakeGenerator();
        body.sortingLayerName = "Player";

        Texture2D white = PixelArt.MakeTexture(3, 2, (x, y) => new Color32(255, 255, 255, 255));
        Sprite lampSprite = PixelArt.ToSprite(white, PixelsPerUnit, new Vector2(0.5f, 0.5f));
        lamps = new SpriteRenderer[coils];
        for (int i = 0; i < coils; i++)
        {
            var lamp = new GameObject($"Coil Lamp {i + 1}").AddComponent<SpriteRenderer>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3((27f + i * 4f - ArtWidth * 0.5f + 1.5f) / PixelsPerUnit, 24f / PixelsPerUnit, 0f);
            lamp.sprite = lampSprite;
            lamp.sortingLayerName = "Player";
            lamp.sortingOrder = 1;
            if (CombatSprites.EffectMaterial != null) lamp.sharedMaterial = CombatSprites.EffectMaterial;
            lamp.color = Color.clear;
            lamps[i] = lamp;
        }

        var glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 1f, 0f);
        glow = glowObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.pointLightInnerRadius = 0.2f;
        glow.pointLightOuterRadius = 2.4f;
        glow.falloffIntensity = 0.7f;
        glow.enabled = false;
    }

    static Sprite MakeGenerator()
    {
        var frame = new Color32(20, 19, 18, 255);
        var body = new Color32(58, 62, 56, 255);
        var bodyLit = new Color32(80, 86, 76, 255);
        var hazard = new Color32(176, 140, 40, 255);
        var fanRim = new Color32(30, 30, 30, 255);
        var blade = new Color32(96, 98, 92, 255);
        var grille = new Color32(34, 34, 32, 255);
        var panel = new Color32(14, 14, 14, 255);
        var fan = new Vector2(11.5f, 14.5f);

        Texture2D texture = PixelArt.MakeTexture(ArtWidth, ArtHeight, (x, y) =>
        {
            if (y <= 1) return x == 3 || x == 4 || x == ArtWidth - 4 || x == ArtWidth - 5 ? frame : default;
            if (x == 0 || x == ArtWidth - 1 || y == 2 || y == ArtHeight - 1) return frame;
            if (y == 3 || y == 4) return (x / 3) % 2 == 0 ? hazard : frame;
            float d = Vector2.Distance(new Vector2(x, y), fan);
            if (d < 8.5f)
            {
                if (d > 7.3f) return fanRim;
                if (d < 1.6f) return frame;
                float angle = Mathf.Atan2(y - fan.y, x - fan.x) * Mathf.Rad2Deg;
                return Mathf.Repeat(angle + d * 12f, 90f) < 30f ? blade : fanRim;
            }
            if (x >= 25 && x <= 37 && y >= 22 && y <= 26) return panel;
            if (x >= 25 && x <= 37 && y >= 7 && y <= 19) return y % 2 == 0 ? grille : body;
            return x <= 2 || y == ArtHeight - 2 ? bodyLit : body;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
