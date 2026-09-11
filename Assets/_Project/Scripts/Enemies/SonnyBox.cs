using UnityEngine;
using UnityEngine.Rendering.Universal;

// Sonny, as the player sees it: the processing box in the control room, humming, with a red lens that watches.
// While dormant the lens breathes slowly; Awaken makes it swell and pulse faster, for the moment the player walks in.
// The box art is made in code unless a sprite is set. Put a Light2D in glow to have the room pulse with it.
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
    public AudioClip humLoop;
    public AudioClip awakenClip;
    [Range(0f, 1f)] public float volume = 0.6f;

    public bool IsAwake { get; private set; }

    private SpriteRenderer lens;
    private SpriteRenderer halo;
    private AudioSource hum;
    private AudioSource voice;
    private float glowIntensity;
    private float awake;        // eases from 0 to 1 after Awaken
    private float phase;

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
        hum.loop = true;
        hum.clip = humLoop;
        hum.volume = volume * 0.5f;
        if (humLoop != null) hum.Play();

        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0f;
        voice.playOnAwake = false;
    }

    public void Awaken()
    {
        if (IsAwake) return;
        IsAwake = true;
        if (awakenClip != null) voice.PlayOneShot(awakenClip, volume);
    }

    void Update()
    {
        if (IsAwake) awake = Mathf.MoveTowards(awake, 1f, Time.deltaTime / WakeTime);

        phase += Time.deltaTime * Mathf.PI * 2f * Mathf.Lerp(dormantPulseRate, awakePulseRate, awake);
        float pulse = 0.5f + 0.5f * Mathf.Sin(phase);
        float brightness = Mathf.Lerp(0.35f, 1f, awake) * Mathf.Lerp(0.55f, 1f, pulse);

        lens.color = new Color(lensColor.r, lensColor.g, lensColor.b, Mathf.Lerp(0.5f, 1f, brightness));
        halo.color = new Color(lensColor.r, lensColor.g, lensColor.b, 0.35f * brightness);
        halo.transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, awake) * Mathf.Lerp(0.9f, 1.1f, pulse);
        if (glow != null) glow.intensity = glowIntensity * Mathf.Lerp(0.5f, 1.8f, awake) * Mathf.Lerp(0.6f, 1f, pulse);
        if (humLoop != null) hum.pitch = Mathf.Lerp(0.8f, 1.1f, awake);
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
