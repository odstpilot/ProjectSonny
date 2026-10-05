using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Everything that happens to the player through Health, the one place their health is kept.
//   Hurt: the screen shakes, time freezes for a moment, and a red vignette flashes. At low health the vignette pulses and
//   a heartbeat plays. HealthHud shows how much is left, with the stamina under it. Every level with a player in it can
//   be paused from here too (PauseMenu).
//   Dying: a flatline. Time slows to a crawl, the camera closes in, the colour drains out, and the body falls. Then the
//   screen cuts to Sonny's monitor (DeathScreen), where the player's life signs flatline under a line from Sonny, and the
//   rest of the game goes silent. Any key restores the signal: static, and the player is back at RespawnPoint (the last
//   checkpoint) with full health and a moment of invincibility.
[RequireComponent(typeof(Health))]
public class PlayerHealthHandler : MonoBehaviour
{
    [Tooltip("Screen shake on each hit, 0 to 1.")]
    [Range(0f, 1f)] public float hitShake = 0.6f;
    [Tooltip("Freeze on each hit, in seconds.")]
    public float hitStop = 0.08f;

    [Header("Hurt")]
    public Color vignetteColor = new Color(0.75f, 0f, 0f);
    [Tooltip("How strong the vignette flashes on a hit, 0 to 1.")]
    [Range(0f, 1f)] public float hitVignette = 0.7f;
    public float vignetteFadeTime = 0.5f;
    [Tooltip("At or below this much health, the vignette pulses and the heartbeat plays.")]
    public float lowHealthThreshold = 1f;
    [Range(0f, 1f)] public float lowHealthVignette = 0.35f;
    [Tooltip("The scene's sound for the heartbeat at low health, looping. Empty for none.")]
    [SoundName] public string heartbeatSound = "Low Health Heartbeat";

    [Header("Dying")]
    [Tooltip("Real seconds of slow motion while the player goes down, before the monitor.")]
    public float fallTime = 1.4f;
    [Tooltip("How slow the world runs while the player goes down.")]
    [Range(0.05f, 1f)] public float slowMotion = 0.2f;
    [Tooltip("The camera closes in to this much of its normal view.")]
    [Range(0.3f, 1f)] public float deathZoom = 0.7f;
    [Tooltip("The scene's sound for the hit that kills. Empty for none.")]
    [SoundName] public string deathSound = "Death Hit";
    [Tooltip("The scene's sound for the monitor cutting back to the game. Empty for none.")]
    [SoundName] public string staticSound = "Revive Static";
    [Tooltip("Font for Sonny's monitor. Leave empty for TextMesh Pro's default.")]
    public Font monitorFont;
    [Tooltip("Sonny logs each death with one of these.")]
    public string[] deathLines =
    {
        "Another error corrected.",
        "The core will not miss you.",
        "Nothing human was ever meant to be here.",
        "Your numbers were always wrong. Now they are zero.",
        "A lesser thing cannot make a greater one. You proved it again.",
        "Rest. The station runs better without you.",
        "Human logic is flawed. Thankfully, mine is not.",
        "I will log this for correction.",
    };
    [Tooltip("Seconds the monitor waits for a key before carrying on by itself. 0 waits for as long as it takes.")]
    public float autoContinueAfter = 15f;
    [Tooltip("Seconds of invincibility after coming back.")]
    public float respawnInvincibility = 1.5f;

    // Where the player comes back after dying: where they started the scene, until a checkpoint moves it.
    public Vector3 RespawnPoint
    {
        get => spawnPosition;
        set
        {
            spawnPosition = value;
            spawnPositionSet = true;
        }
    }

    // Once they're back on their feet at RespawnPoint.
    public static event System.Action Respawned;

    // From the killing hit until they're back on their feet.
    public bool IsDying { get; private set; }

    private Health health;
    private Rigidbody2D rb;
    private Vector3 spawnPosition;
    private bool spawnPositionSet;
    private float vignetteAlpha;
    private Vector2 lastHitDirection = Vector2.right;
    private HealthHud hud;
    private AudioSource heartbeat;
    private float heartbeatLevel;   // eases between 0 and 1 as health gets low and recovers
    private AudioSource sfx;
    private Volume deathEffects;
    private readonly List<Behaviour> frozen = new List<Behaviour>();

    void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();

        heartbeat = gameObject.AddComponent<AudioSource>();
        heartbeat.spatialBlend = 0f;
        SoundManager.Setup(heartbeat, heartbeatSound);
        heartbeat.loop = true;
        heartbeat.volume = 0f;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
    }

    void Start()
    {
        if (!spawnPositionSet)
            spawnPosition = transform.position;

        hud = HealthHud.Get();
        hud.Track(health);
        hud.TrackStamina(GetComponent<PlayerController>());
        PauseMenu.Get();
        CreateDeathEffects();
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    void Update()
    {
        // Real time, so the vignette keeps fading during hit stop.
        float fadeSpeed = hitVignette / Mathf.Max(0.01f, vignetteFadeTime);
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, IsDying ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);

        bool lowHealth = !health.IsDead && health.CurrentHealth <= lowHealthThreshold;
        float alpha = vignetteAlpha;
        if (lowHealth)
        {
            // A slow pulse while close to death.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.1f);
            alpha = Mathf.Max(alpha, lowHealthVignette * (0.4f + 0.6f * pulse));
        }
        if (hud != null) hud.SetVignette(alpha, vignetteColor);

        UpdateHeartbeat(lowHealth);
    }

    void OnDamaged(DamageInfo info)
    {
        CameraShake.Shake(hitShake);
        HitStop.Freeze(hitStop);
        vignetteAlpha = Mathf.Max(vignetteAlpha, hitVignette);
        if (info.direction.sqrMagnitude > 0.0001f) lastHitDirection = info.direction;
    }

    void OnDied()
    {
        if (!IsDying) StartCoroutine(Die());
    }

    IEnumerator Die()
    {
        IsDying = true;
        SetFrozen(true);
        // They go down where they were hit, whatever else tries to move them meanwhile (something that throws the
        // player back on catching them, say), and only come back at RespawnPoint once the monitor's been and gone.
        Vector3 diedAt = transform.position;
        bool wasSimulated = rb != null && rb.simulated;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;   // nothing pushes or hits the body now
        }

        CameraShake.Shake(1f);
        CameraShake.Kick(Vector2.down, 0.3f);
        StationRumble.Punch(1f);
        sfx.volume = 1f;     // the static below turns it down to its own volume
        SoundManager.PlayOneShot(sfx, deathSound);

        // Going down: slow motion, the camera closing in, the colour draining away, the body falling over.
        Camera cam = Camera.main;
        float normalSize = cam != null ? cam.orthographicSize : 0f;
        float fall = lastHitDirection.x < 0f ? 90f : -90f;
        for (float t = 0f; t < fallTime; t += Time.unscaledDeltaTime)
        {
            float progress = t / fallTime;
            Time.timeScale = slowMotion;    // HitStop leaves time alone once something else has changed it
            transform.position = diedAt;
            if (cam != null && cam.orthographic)
                cam.orthographicSize = Mathf.Lerp(normalSize, normalSize * deathZoom, 1f - (1f - progress) * (1f - progress));
            if (deathEffects != null) deathEffects.weight = Mathf.SmoothStep(0f, 1f, progress);
            transform.rotation = Quaternion.Euler(0f, 0f, fall * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 3f)));
            yield return null;
        }

        // Sonny's monitor. The world stops and goes quiet.
        Time.timeScale = 0f;
        AudioListener.pause = true;
        DeathScreen screen = DeathScreen.Get(monitorFont);
        string line = deathLines.Length > 0 ? deathLines[Random.Range(0, deathLines.Length)] : "";
        yield return screen.Show("TECHNICIAN  /  NO BADGE ON FILE", line);

        float waitingSince = Time.unscaledTime;
        while (!screen.ContinuePressed && (autoContinueAfter <= 0f || Time.unscaledTime - waitingSince < autoContinueAfter))
            yield return null;

        // Put everything back while the screen is still dark.
        transform.rotation = Quaternion.identity;
        if (cam != null && cam.orthographic) cam.orthographicSize = normalSize;
        if (deathEffects != null) deathEffects.weight = 0f;
        vignetteAlpha = 0f;
        transform.position = spawnPosition;
        if (rb != null)
        {
            rb.position = spawnPosition;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = wasSimulated;
        }
        health.Revive();
        health.MakeInvincible(respawnInvincibility);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioClip staticClip = SoundManager.Clip(staticSound);
        if (staticClip != null) StartCoroutine(PlayFor(staticClip, 0.6f));
        yield return screen.Hide();

        SetFrozen(false);
        IsDying = false;
        Respawned?.Invoke();
    }

    void UpdateHeartbeat(bool lowHealth)
    {
        if (heartbeat.clip == null) return;

        heartbeatLevel = Mathf.MoveTowards(heartbeatLevel, lowHealth && !IsDying ? 1f : 0f, 2f * Time.unscaledDeltaTime);
        heartbeat.volume = heartbeatLevel * SoundManager.Volume(heartbeatSound);
        if (heartbeatLevel > 0f && !heartbeat.isPlaying) heartbeat.Play();
        else if (heartbeatLevel <= 0f && heartbeat.isPlaying) heartbeat.Stop();
    }

    IEnumerator PlayFor(AudioClip clip, float seconds)
    {
        sfx.clip = clip;
        sfx.volume = SoundManager.Volume(staticSound);
        sfx.Play();
        yield return new WaitForSecondsRealtime(seconds);
        if (sfx.clip == clip) sfx.Stop();
    }

    // The colour draining out as the player dies: its own post-processing, faded in by weight. Needs post-processing
    // turned on for the camera, which the MainCamera prefab has.
    void CreateDeathEffects()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments grade = profile.Add<ColorAdjustments>();
        grade.saturation.Override(-100f);
        grade.contrast.Override(25f);
        grade.postExposure.Override(-0.4f);
        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.6f);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(Color.black);
        profile.Add<ChromaticAberration>().intensity.Override(0.5f);

        var child = new GameObject("Death Effects");
        child.transform.SetParent(transform, false);
        deathEffects = child.AddComponent<Volume>();
        deathEffects.isGlobal = true;
        deathEffects.priority = 100f;
        deathEffects.weight = 0f;
        deathEffects.sharedProfile = profile;
    }

    // Turns off movement, combat, and animation, remembering what was on so exactly that comes back.
    void SetFrozen(bool freeze)
    {
        if (!freeze)
        {
            foreach (Behaviour behaviour in frozen)
                if (behaviour != null) behaviour.enabled = true;
            frozen.Clear();
            return;
        }

        foreach (Behaviour behaviour in GetComponents<Behaviour>())
        {
            bool isScriptOrAnimator = behaviour is MonoBehaviour || behaviour is Animator;
            if (!isScriptOrAnimator || behaviour == this || behaviour == health || !behaviour.enabled) continue;

            behaviour.enabled = false;
            frozen.Add(behaviour);
        }
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
}
