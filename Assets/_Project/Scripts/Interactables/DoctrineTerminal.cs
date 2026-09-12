using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

// A relay node where Sonny checks that whoever is at the keyboard is one of its units. There's no password: it asks
// questions from its doctrine (DoctrineData), and the player has to answer as Sonny's units would, not as a human would.
// The answers are all out there in intercepted broadcasts, for players who've been listening. On screen they're
// scrambled, and decrypting them, hesitating, and answering wrong all draw Sonny's attention (see DoctrineScreen).
// Pass and it hands over its credential. Get traced and it raises the alarm and locks the player out for a while.
// Setup: a solid Collider2D on this object for its footprint, and a DoctrineData. The relay's art is made in code
// unless a sprite is set.
public class DoctrineTerminal : Terminal
{
    const float PixelsPerUnit = 20f;
    const int ArtWidth = 18;
    const int ArtHeight = 34;
    static readonly Vector2 LensPixel = new Vector2(8.5f, 25.5f);   // the lens's middle, from the art's bottom left

    static readonly Color Watching = new Color(1f, 0.16f, 0.1f);
    static readonly Color Trusting = new Color(1f, 0.66f, 0.3f);

    [Tooltip("Shown at the top of the check.")]
    public string displayName = "SONNY RELAY";
    public DoctrineData doctrine;

    [Header("Tracing")]
    [Tooltip("Trace gained each second the player spends on a question. The whole meter is 1.")]
    public float hesitationTrace = 0.03f;
    [Tooltip("Trace gained each second spent decrypting an answer.")]
    public float decryptTrace = 0.2f;
    [Tooltip("Trace gained for a wrong answer.")]
    public float wrongAnswerTrace = 0.4f;
    [Tooltip("Seconds to decrypt an answer all the way.")]
    public float decryptTime = 1.4f;
    [Tooltip("Seconds the player is locked out after being traced.")]
    public float lockoutTime = 20f;
    public AudioClip alarmClip;

    [Header("Look")]
    [Tooltip("Leave empty for the placeholder relay. Pivot at the bottom middle.")]
    public Sprite sprite;

    public UnityEvent onVerified = new UnityEvent();
    [Tooltip("When the player is traced. A good place to wake nearby robots.")]
    public UnityEvent onAlarm = new UnityEvent();
    public event System.Action Verified;
    public event System.Action Alarmed;

    public bool IsVerified { get; private set; }
    public float LockoutRemaining => Mathf.Max(0f, lockedUntil - Time.time);

    public override bool InUse => DoctrineScreen.Current != null && DoctrineScreen.Current.Terminal == this;
    protected override string PromptText =>
        LockoutRemaining > 0f ? $"LOCKED  {Mathf.CeilToInt(LockoutRemaining)}" : IsVerified ? "CONNECT" : "ANSWER";
    protected override Vector3 PromptPoint => transform.position + new Vector3(0f, ArtHeight / PixelsPerUnit + 0.2f, 0f);
    protected override bool Available => LockoutRemaining <= 0f;

    private float lockedUntil = -1f;
    private SpriteRenderer lens;
    private Light2D glow;
    private AudioSource speaker;

    protected override void Awake()
    {
        base.Awake();
        BuildArt();
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        speaker.spatialBlend = 0f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (InUse) DoctrineScreen.Current.Close();
    }

    protected override void Update()
    {
        UpdateLens();
        base.Update();
    }

    protected override void Use(Transform player)
    {
        DoctrineScreen.Get(screenFont).Open(this, player);
    }

    // Passed: hand over the credential and let the level know.
    public void Verify()
    {
        if (IsVerified) return;
        IsVerified = true;
        if (doctrine != null) AccessCredentials.Grant(doctrine.credential);
        Verified?.Invoke();
        onVerified.Invoke();
    }

    // Traced: lock the player out, sound the alarm, and let the level know.
    public void RaiseAlarm()
    {
        lockedUntil = Time.time + lockoutTime;
        if (alarmClip != null) speaker.PlayOneShot(alarmClip);
        Alarmed?.Invoke();
        onAlarm.Invoke();
    }

    // The lens watches with a slow red pulse, flashes while locked out, and settles to amber once it trusts the player.
    void UpdateLens()
    {
        float level;
        Color color;
        if (LockoutRemaining > 0f)
        {
            level = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f ? 1f : 0.2f;
            color = Watching;
        }
        else if (IsVerified)
        {
            level = 0.8f;
            color = Trusting;
        }
        else
        {
            level = 0.55f + 0.45f * Mathf.Sin(Time.time * 1.6f);
            color = Watching;
        }

        if (lens != null) lens.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.4f, 1f, level));
        if (glow != null)
        {
            glow.color = color;
            glow.intensity = Mathf.Lerp(0.3f, 1f, level);
        }
    }

    void BuildArt()
    {
        var body = new GameObject("Relay").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        body.sprite = sprite != null ? sprite : MakeRelay();
        body.sortingLayerName = "Player";

        if (sprite == null)
        {
            lens = new GameObject("Lens").AddComponent<SpriteRenderer>();
            lens.transform.SetParent(transform, false);
            lens.transform.localPosition = new Vector3((LensPixel.x + 0.5f - ArtWidth * 0.5f) / PixelsPerUnit, (LensPixel.y + 0.5f) / PixelsPerUnit, 0f);
            Texture2D ring = CombatSprites.RingTexture;
            lens.sprite = PixelArt.ToSprite(ring, ring.width / 0.36f, new Vector2(0.5f, 0.5f));
            lens.sortingLayerName = "Player";
            lens.sortingOrder = 1;
            if (CombatSprites.EffectMaterial != null) lens.sharedMaterial = CombatSprites.EffectMaterial;
        }

        var glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        glow = glowObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.pointLightInnerRadius = 0.2f;
        glow.pointLightOuterRadius = 2.6f;
        glow.falloffIntensity = 0.7f;
        glow.color = Watching;
    }

    // A narrow black pillar with a socket for the lens near the top, a slot of status lights, and cables at the base.
    static Sprite MakeRelay()
    {
        var frame = new Color32(12, 11, 12, 255);
        var body = new Color32(30, 28, 31, 255);
        var bodyLit = new Color32(48, 45, 50, 255);
        var socket = new Color32(4, 4, 5, 255);
        var rim = new Color32(90, 86, 92, 255);
        var slot = new Color32(70, 20, 16, 255);
        var cable = new Color32(22, 20, 22, 255);

        Texture2D texture = PixelArt.MakeTexture(ArtWidth, ArtHeight, (x, y) =>
        {
            float lensDistance = Vector2.Distance(new Vector2(x, y), LensPixel);
            if (lensDistance < 3.6f) return socket;
            if (lensDistance < 4.8f) return rim;
            if (y <= 2) return (x + y) % 3 == 0 ? cable : default;
            if (x == 0 || x == ArtWidth - 1 || y == 3 || y == ArtHeight - 1) return frame;
            if (y >= 10 && y <= 17 && x >= 7 && x <= 10) return y % 2 == 0 ? slot : frame;
            return x <= 2 ? bodyLit : body;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
