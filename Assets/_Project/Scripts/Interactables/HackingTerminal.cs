using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

// A comms terminal wired into the station's broadcasts. Walk up and press E to tune in (see SignalTuner): match the
// broadcast's wave with three knobs to intercept its transmission, recovering any credential it carries. Once it's
// hacked, pressing E again replays the transmission.
// The puzzle is made up the first time and then kept, knob positions included, so getting chased off the terminal
// doesn't throw away the player's progress.
// Setup: a solid Collider2D on this object for the terminal's footprint, and a TransmissionData. The console art is
// made in code unless a sprite is set.
public class HackingTerminal : Terminal
{
    const float PixelsPerUnit = 20f;
    const int ArtWidth = 22;
    const int ArtHeight = 30;

    static readonly Color IdleScreen = new Color(1f, 0.62f, 0.25f);
    static readonly Color HackedScreen = new Color(0.45f, 1f, 0.55f);

    [Tooltip("Shown at the top of the tuning screen.")]
    public string displayName = "COMMS NODE";
    public TransmissionData transmission;

    [Header("Tuning")]
    [Tooltip("How forgiving the knobs are: above 1 is easier, below 1 is harder.")]
    [Range(0.4f, 2.5f)] public float precision = 1f;
    [Tooltip("The broadcast slides sideways by itself, in turns a second, so its phase has to be chased. 0 holds it still.")]
    public float phaseDrift;

    [Header("Look and Sound")]
    [Tooltip("Leave empty for the placeholder console. Pivot at the bottom middle.")]
    public Sprite sprite;
    [Tooltip("Loops while tuning, clearing as the signal lines up.")]
    public AudioClip staticClip;

    public UnityEvent onHacked = new UnityEvent();
    public event System.Action Hacked;

    public bool IsHacked { get; private set; }
    public override bool InUse => SignalTuner.Current != null && SignalTuner.Current.Terminal == this;
    protected override string PromptText => IsHacked ? "REPLAY" : "TUNE IN";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, ArtHeight / PixelsPerUnit + 0.2f, 0f);

    private SpriteRenderer screen;
    private Light2D glow;
    private float glowIntensity;
    private float[] signal;     // the broadcast's AMP, FREQ, and PHASE, 0 to 1
    private float[] knobs;      // where the player left the knobs

    protected override void Awake()
    {
        base.Awake();
        BuildArt();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (InUse) SignalTuner.Current.Close();
    }

    protected override void Update()
    {
        Flicker();
        base.Update();
    }

    protected override void Use(Transform player)
    {
        SignalTuner.Get(screenFont).Open(this, player);
    }

    // The broadcast to match, and where the knobs start. Made up the first time and kept after.
    public void GetSignal(float[] targetsOut, float[] knobsOut)
    {
        if (signal == null)
        {
            signal = new float[3];
            knobs = new float[3];
            for (int i = 0; i < 3; i++)
            {
                signal[i] = i == 2 ? Random.value : Random.Range(0.1f, 0.9f);
                // Start well away from the answer.
                float start;
                do start = Random.value;
                while (SignalTuner.KnobDistance(i, start, signal[i]) < (i == 2 ? 0.2f : 0.3f));
                knobs[i] = start;
            }
        }
        signal.CopyTo(targetsOut, 0);
        knobs.CopyTo(knobsOut, 0);
    }

    public void RememberKnobs(float[] values)
    {
        if (knobs != null) values.CopyTo(knobs, 0);
    }

    // The signal locked: recover the credential and let the level know.
    public void MarkHacked()
    {
        if (IsHacked) return;
        IsHacked = true;
        if (transmission != null) AccessCredentials.Grant(transmission.credential);
        Hacked?.Invoke();
        onHacked.Invoke();
    }

    void Flicker()
    {
        float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, transform.position.x);
        Color color = IsHacked ? HackedScreen : IdleScreen;
        if (screen != null) screen.color = color * flicker;
        if (glow != null)
        {
            glow.color = color;
            glow.intensity = glowIntensity * flicker;
        }
    }

    void BuildArt()
    {
        var body = new GameObject("Console").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        body.sprite = sprite != null ? sprite : MakeConsole();
        body.sortingLayerName = "Player";

        if (sprite == null)
        {
            screen = new GameObject("Screen").AddComponent<SpriteRenderer>();
            screen.transform.SetParent(transform, false);
            screen.transform.localPosition = new Vector3(0f, 19.5f / PixelsPerUnit, 0f);
            screen.sprite = MakeScreen();
            screen.sortingLayerName = "Player";
            screen.sortingOrder = 1;
            if (CombatSprites.EffectMaterial != null) screen.sharedMaterial = CombatSprites.EffectMaterial;
        }

        var glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 1f, 0f);
        glow = glowObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.pointLightInnerRadius = 0.2f;
        glow.pointLightOuterRadius = 2.2f;
        glow.falloffIntensity = 0.7f;
        glow.intensity = glowIntensity = 0.7f;
        glow.color = IdleScreen;
    }

    // A standing console: dark frame, a screen, three knobs, and a vent at the bottom.
    static Sprite MakeConsole()
    {
        var frame = new Color32(26, 24, 22, 255);
        var body = new Color32(62, 58, 54, 255);
        var bodyLit = new Color32(84, 80, 74, 255);
        var bezel = new Color32(14, 13, 12, 255);
        var knob = new Color32(122, 114, 102, 255);
        var vent = new Color32(30, 28, 26, 255);

        Texture2D texture = PixelArt.MakeTexture(ArtWidth, ArtHeight, (x, y) =>
        {
            if (x == 0 || x == ArtWidth - 1 || y == 0 || y == ArtHeight - 1) return frame;
            if (x >= 2 && x <= 19 && y >= 12 && y <= 27) return bezel;
            if ((new Vector2(x, y) - new Vector2(5.5f, 7f)).sqrMagnitude < 2.6f ||
                (new Vector2(x, y) - new Vector2(10.5f, 7f)).sqrMagnitude < 2.6f ||
                (new Vector2(x, y) - new Vector2(15.5f, 7f)).sqrMagnitude < 2.6f) return knob;
            if (y >= 2 && y <= 3 && x % 2 == 0) return vent;
            return x == 1 || y == ArtHeight - 2 ? bodyLit : body;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }

    // White, tinted by the screen color: a faint glow with a wave across it.
    static Sprite MakeScreen()
    {
        const int width = 16;
        const int height = 14;
        Texture2D texture = PixelArt.MakeTexture(width, height, (x, y) =>
        {
            int wave = Mathf.RoundToInt(height * 0.5f - 0.5f + Mathf.Sin(x / (float)width * Mathf.PI * 4f) * 3.5f);
            byte alpha = (byte)(y == wave ? 255 : 70);
            return new Color32(255, 255, 255, alpha);
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }
}
