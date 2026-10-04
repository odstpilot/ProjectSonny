using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

// A backup generator, dead, in a room with the power out (the comms ring, CommsRing). In the pitch dark it gives off no
// light of its own, but an EMP's flash shows it up: caught in a pulse's cone (EmpPulse.Swept), it crackles and its coil
// lamps blink amber for a few seconds, so it can be found by firing into the dark. Walk up and press E to start it
// (GeneratorScreen): bring its coils up one at a time by engaging each as the rotor comes round into sync. A miss
// backfires, loud enough to bring the bots (Backfired), and knocks a coil back down. All of them up and it starts.
// Coils already up stay up between tries.
// Setup: a solid Collider2D on this object for its footprint. The art is made in code unless a sprite is set.
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
    // A missed sync: a bang, and a cloud of sparks. A good place to bring the bots.
    public event System.Action Backfired;

    public bool IsRunning { get; private set; }
    // Up so far; kept between tries.
    public int CoilsLit { get; set; }
    public override bool InUse => GeneratorScreen.Current != null && GeneratorScreen.Current.Generator == this;
    protected override string PromptText => IsRunning ? "RUNNING" : "START GENERATOR";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, ArtHeight / PixelsPerUnit + 0.2f, 0f);
    protected override bool Available => !IsRunning;

    // The sync window for this coil, as a fraction of the gauge, and the needle's speed.
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

    // Caught in an EMP's flash: a crackle over it, and its lamps blinking for a while.
    void OnSwept(Vector2 origin, Vector2 direction, float radius, float halfAngle)
    {
        if (IsRunning) return;
        Vector2 middle = (Vector2)transform.position + new Vector2(0f, ArtHeight * 0.5f / PixelsPerUnit);
        if (!EmpPulse.InCone(middle, origin, direction, radius, halfAngle)) return;
        pingUntil = Time.time + pingSeconds;
        HitEffects.Sparks(middle, Vector2.up, 14, 3.5f, 360f, SparkWhite, SparkBlue);
        LightFlash.Spawn(middle, Pinged, 2.4f, 1.2f, 0.5f);
    }

    // All the coils up: it starts, with a shudder through the floor. quietly: picking up from a save, no show.
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

    // A missed sync (GeneratorScreen): a bang, sparks, and a coil knocked back down.
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

    // Dark while it's dead, blinking amber after an EMP's caught it, the lit coils steady while it's being started, and
    // all green once it runs.
    void UpdateLamps()
    {
        bool pinged = Time.time < pingUntil;
        bool blinkOn = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
        for (int i = 0; i < lamps.Length; i++)
        {
            Color color = Color.clear;
            if (IsRunning) color = Running;
            else if (i < CoilsLit) color = Pinged;
            else if (pinged && blinkOn) color = Pinged * 0.8f;
            lamps[i].color = color;
        }
        if (glow == null) return;
        glow.enabled = IsRunning || pinged || CoilsLit > 0;
        glow.color = IsRunning ? Running : Pinged;
        glow.intensity = IsRunning ? 0.9f : pinged ? (blinkOn ? 0.8f : 0.3f) : 0.35f;
    }

    void BuildArt()
    {
        var body = new GameObject("Generator").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        body.sprite = sprite != null ? sprite : MakeGenerator();
        body.sortingLayerName = "Player";

        // A lamp per coil, in a row along the top of the housing.
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

    // A squat housing: a big round fan on the left, a grille and a panel for the coil lamps on the right, feet below.
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
