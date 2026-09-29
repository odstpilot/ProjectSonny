using UnityEngine;
using UnityEngine.Rendering.Universal;

// Sonny, as the player sees it: the processing box in the control room, humming, with a red lens that watches.
// While dormant the lens breathes slowly; Awaken makes it swell and pulse faster, for the moment the player walks in.
// The box art is made in code unless a sprite is set. Put a Light2D in glow to have the room pulse with it.
// In Chapter 1 it starts unpowered (startPowered off), dark and silent, until the technician installs it and PowerOn
// brings it up; Flicker shows another color in the lens for a moment (Sonny's first slip).
public class SonnyBox : MonoBehaviour
{
    const float PixelsPerUnit = 20f;
    const float WakeTime = 1.2f;
    const int Width = 30;
    const int Height = 40;
    static readonly Vector2Int LensCenter = new Vector2Int(15, 27);   // in pixels from the bottom left of the box

    [Tooltip("Leave empty for the placeholder box. Pivot at the bottom middle.")]
    public Sprite boxSprite;
    [Tooltip("Where the lens sits, relative to this object. Only needed with a custom sprite.")]
    public Vector2 lensOffset = new Vector2(0f, LensCenter.y / PixelsPerUnit);
    public Color lensColor = new Color(1f, 0.16f, 0.1f);
    [Tooltip("Optional light that pulses with the lens.")]
    public Light2D glow;
    [Tooltip("Pulses a second while dormant, then once awake.")]
    public float dormantPulseRate = 0.3f;
    public float awakePulseRate = 1.5f;
    [Tooltip("The scene's sound for the hum, looping while it's powered. Empty for none.")]
    [SoundName] public string humSound = "Sonny Hum";
    [Tooltip("The scene's sound for it powering on and waking. Empty for none.")]
    [SoundName] public string awakenSound = "Sonny Awaken";
    [Tooltip("Off leaves it dark and silent until PowerOn.")]
    public bool startPowered = true;

    public bool IsAwake { get; private set; }
    public bool IsPowered { get; private set; }

    private SpriteRenderer lens;
    private SpriteRenderer halo;
    private AudioSource hum;
    private AudioSource voice;
    private float humPitch = 1f;
    private float glowIntensity;
    private float awake;        // eases from 0 to 1 after Awaken
    private float phase;
    private float power;        // eases from 0 to 1 after PowerOn
    private Color flickerColor;
    private float flickerUntil = -1f;

    void Awake()
    {
        var body = new GameObject("Box").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        body.sprite = boxSprite != null ? boxSprite : MakeBox();
        body.sortingLayerName = "Player";
        if (boxSprite == null) lensOffset = new Vector2((LensCenter.x + 0.5f - Width * 0.5f) / PixelsPerUnit, (LensCenter.y + 0.5f) / PixelsPerUnit);

        // Behind the box, so it glows around it rather than washing over it.
        halo = CreateGlowSprite("Halo", CombatSprites.SoftCircleTexture, 1.4f, -1);
        lens = CreateGlowSprite("Lens", CombatSprites.RingTexture, 0.38f, 12);

        if (glow != null) glowIntensity = glow.intensity;

        hum = gameObject.AddComponent<AudioSource>();
        hum.spatialBlend = 0f;
        SoundManager.Setup(hum, humSound);
        hum.loop = true;
        humPitch = hum.pitch;
        hum.volume = SoundManager.Volume(humSound);
        if (hum.clip != null) hum.Play();

        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0f;
        voice.playOnAwake = false;

        IsPowered = startPowered;
        power = startPowered ? 1f : 0f;
        if (!startPowered) hum.Stop();
    }

    public void PowerOn()
    {
        if (IsPowered) return;
        IsPowered = true;
        if (hum.clip != null) hum.Play();
        SoundManager.PlayOneShot(voice, awakenSound);
    }

    // The lens goes this color for a moment, then back. Unscaled, so a cutscene can time it to a line.
    public void Flicker(Color color, float seconds)
    {
        flickerColor = color;
        flickerUntil = Time.unscaledTime + seconds;
    }

    public void Awaken()
    {
        if (IsAwake) return;
        IsAwake = true;
        SoundManager.PlayOneShot(voice, awakenSound);
    }

    void Update()
    {
        if (IsAwake) awake = Mathf.MoveTowards(awake, 1f, Time.deltaTime / WakeTime);
        power = Mathf.MoveTowards(power, IsPowered ? 1f : 0f, Time.deltaTime / WakeTime);

        phase += Time.deltaTime * Mathf.PI * 2f * Mathf.Lerp(dormantPulseRate, awakePulseRate, awake);
        float pulse = 0.5f + 0.5f * Mathf.Sin(phase);
        float brightness = Mathf.Lerp(0.35f, 1f, awake) * Mathf.Lerp(0.55f, 1f, pulse);
        // Powering up stutters on, like a tube catching.
        float lit = power >= 1f ? 1f : power * (Mathf.PerlinNoise(Time.time * 18f, 0f) > 0.35f ? 1f : 0.2f);
        bool flickering = Time.unscaledTime < flickerUntil;
        Color color = flickering ? flickerColor : lensColor;
        if (flickering) brightness = 1f;

        lens.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.5f, 1f, brightness) * lit);
        halo.color = new Color(color.r, color.g, color.b, 0.35f * brightness * lit);
        halo.transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, awake) * Mathf.Lerp(0.9f, 1.1f, pulse);
        if (glow != null)
        {
            glow.intensity = glowIntensity * Mathf.Lerp(0.5f, 1.8f, awake) * Mathf.Lerp(0.6f, 1f, pulse) * lit;
            glow.color = color;
        }
        if (hum.clip != null)
        {
            hum.pitch = humPitch * Mathf.Lerp(0.8f, 1.1f, awake) * (flickering ? 0.7f : 1f);
            hum.volume = SoundManager.Volume(humSound);
        }
    }

    // The box is only made in Play mode, so outline where it will be.
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = lensColor;
        Gizmos.DrawWireCube(transform.position + new Vector3(0f, Height * 0.5f / PixelsPerUnit, 0f), new Vector3(Width, Height, 0f) / PixelsPerUnit);
    }

    SpriteRenderer CreateGlowSprite(string childName, Texture2D texture, float worldSize, int order)
    {
        var sprite = new GameObject(childName).AddComponent<SpriteRenderer>();
        sprite.transform.SetParent(transform, false);
        sprite.transform.localPosition = lensOffset;
        sprite.sprite = PixelArt.ToSprite(texture, texture.width / worldSize, new Vector2(0.5f, 0.5f));
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = order;
        if (CombatSprites.EffectMaterial != null) sprite.sharedMaterial = CombatSprites.EffectMaterial;
        return sprite;
    }

    // A tall cabinet: a dark frame, vent slits below, a row of status lights, and a black socket for the lens.
    static Sprite MakeBox()
    {
        var frame = new Color32(24, 26, 30, 255);
        var body = new Color32(50, 55, 62, 255);
        var bodyLit = new Color32(70, 76, 85, 255);
        var vent = new Color32(18, 19, 22, 255);
        var socket = new Color32(8, 8, 10, 255);
        var socketRim = new Color32(96, 102, 112, 255);
        var statusLight = new Color32(60, 200, 120, 255);

        Texture2D texture = PixelArt.MakeTexture(Width, Height, (x, y) =>
        {
            if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1) return frame;
            float lensDistance = Vector2.Distance(new Vector2(x, y), LensCenter);
            if (lensDistance < 4.5f) return socket;
            if (lensDistance < 6f) return socketRim;
            if (y == Height - 2 || x == 1) return bodyLit;
            if (y >= 4 && y <= 14 && x >= 4 && x <= Width - 5 && y % 2 == 0) return vent;
            if (y == 18 && x >= 6 && x <= Width - 7 && x % 4 == 2) return statusLight;
            if (y == 2 || y == 20) return frame;
            return body;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
