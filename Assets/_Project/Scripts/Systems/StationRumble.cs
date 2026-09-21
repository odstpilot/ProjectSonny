using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// The station shaking itself apart. Every so often the screen shakes and warps, a low rumble plays, the lights brown
// out, dust drifts down, and debris falls near the player: a shadow spreads on the floor first, so there's time to step
// aside (see FallingDebris). Between rumbles a few warm motes of dust hang in the air. Levels can set a rumble off on cue
// with Rumble, and raise intensity so they come stronger and more often. Heavy impacts call Punch to jolt the picture.
// Lamps with StationLight listen for Rumbled and stutter along.
// One per scene. Put it on an empty object; it needs nothing else.
public class StationRumble : MonoBehaviour
{
    const float DustPerSecond = 70f;    // specks at full strength
    const float MotesPerSecond = 6f;
    const float LightDip = 0.5f;        // how far the global light dims at the peak of a rumble
    const float PunchRecovery = 2.5f;   // a full punch fades in 0.4 seconds
    const string DustSortingLayer = "Top";

    [Header("Rumbles On Their Own")]
    public bool rumbleOnItsOwn = true;
    [Tooltip("Seconds between rumbles at intensity 1.")]
    public Vector2 secondsBetween = new Vector2(9f, 16f);
    [Tooltip("Screen shake, 0 to 1.")]
    public Vector2 strength = new Vector2(0.2f, 0.4f);
    public Vector2 duration = new Vector2(1.2f, 2.4f);
    [Tooltip("Above 1, rumbles come stronger and more often; below 1, gentler. The level raises it as the flare gets closer.")]
    public float intensity = 1f;

    [Header("Falling Debris")]
    public Vector2Int debrisPerRumble = new Vector2Int(1, 3);
    [Tooltip("Debris lands between these distances from the player.")]
    public float debrisMinDistance = 1.5f;
    public float debrisMaxDistance = 5f;
    [Tooltip("Chance that one piece of each rumble comes down right where the player is standing.")]
    [Range(0f, 1f)] public float aimAtPlayerChance = 0.35f;
    [Tooltip("While the player is on the move, the chance a piece comes down somewhere ahead of them, where they'll see it, " +
             "rather than anywhere around them.")]
    [Range(0f, 1f)] public float aheadChance = 0f;
    public float debrisDamage = 1f;
    [Tooltip("Debris only lands on this tilemap's tiles, so it never falls into walls or empty space. Leave empty to allow anywhere that isn't solid.")]
    public Tilemap floor;

    [Header("Lights and Screen")]
    [Tooltip("Dims and turns toward the emergency color during rumbles. Usually the global light.")]
    public Light2D globalLight;
    public Color emergencyColor = new Color(1f, 0.45f, 0.25f);
    [Tooltip("How much the picture warps, fringes, and darkens at the height of a rumble or a heavy impact, 0 to 1.")]
    [Range(0f, 1f)] public float screenDistortion = 0.6f;

    [Header("Sound")]
    public AudioClip[] rumbleClips = new AudioClip[0];
    [Tooltip("Played when debris lands.")]
    public AudioClip[] impactClips = new AudioClip[0];
    [Tooltip("Loops quietly the whole time, like the station's machinery.")]
    public AudioClip ambientLoop;
    [Range(0f, 1f)] public float ambientVolume = 0.35f;
    [Range(0f, 1f)] public float volume = 0.8f;

    public static StationRumble Instance { get; private set; }
    // (strength, duration) as each rumble starts.
    public static event System.Action<float, float> Rumbled;

    public bool IsRumbling => Time.time < rumbleEnd;
    public float BaseLightIntensity => lightIntensity;
    public Color BaseLightColor => lightColor;

    private Transform player;
    private Collider2D playerCollider;
    private Rigidbody2D playerBody;
    private AudioSource sfx;
    private ParticleSystem dust;
    private Volume screenEffects;
    private float dustOwed;
    private float motesOwed;
    private float punch;
    private float nextRumble;
    private float rumbleStart;
    private float rumbleEnd;
    private float rumbleStrength;
    private float lightIntensity;
    private Color lightColor;

    void Awake()
    {
        Instance = this;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        if (ambientLoop != null)
        {
            AudioSource ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = ambientLoop;
            ambience.loop = true;
            ambience.volume = ambientVolume;
            ambience.spatialBlend = 0f;
            ambience.Play();
        }

        if (globalLight != null)
        {
            lightIntensity = globalLight.intensity;
            lightColor = globalLight.color;
        }

        CreateScreenEffects();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            playerCollider = found.GetComponent<Collider2D>();
            playerBody = found.GetComponent<Rigidbody2D>();
        }
        ScheduleNext();
    }

    // Shakes the station now. shake is 0 to 1; debris falls around the player over the first part of it.
    public void Rumble(float shake, float seconds, int debrisCount)
    {
        rumbleStrength = Mathf.Clamp01(shake);
        rumbleStart = Time.time;
        rumbleEnd = Time.time + Mathf.Max(0.1f, seconds);
        PlayRandom(rumbleClips, Mathf.Lerp(0.5f, 1f, rumbleStrength));
        Rumbled?.Invoke(rumbleStrength, seconds);

        if (debrisCount > 0)
            StartCoroutine(DropDebrisDuring(debrisCount, seconds));
        ScheduleNext();
    }

    // A sudden jolt to the picture (warp, color fringing, darker edges) that fades in a moment. amount is 0 to 1.
    public static void Punch(float amount)
    {
        if (Instance != null) Instance.punch = Mathf.Clamp01(Mathf.Max(Instance.punch, amount));
    }

    // For the level to change the lighting it goes back to between rumbles, like turning everything red near the end.
    public void SetLightBase(float intensityValue, Color color)
    {
        lightIntensity = intensityValue;
        lightColor = color;
    }

    public void PlaySound(AudioClip clip, float volumeScale = 1f)
    {
        if (clip != null) sfx.PlayOneShot(clip, volume * volumeScale);
    }

    // A sound from somewhere in the level, quieter the further it is from the camera.
    public static void PlayAt(AudioClip clip, Vector2 point, float volumeScale = 1f)
    {
        if (Instance == null || clip == null) return;
        float nearness = Nearness(point);
        if (nearness > 0f) Instance.sfx.PlayOneShot(clip, Instance.volume * volumeScale * nearness);
    }

    // For FallingDebris: a crash, quieter the further it lands from the camera.
    public static void PlayImpact(Vector2 point)
    {
        if (Instance == null) return;
        Instance.PlayRandom(Instance.impactClips, Nearness(point));
    }

    void Update()
    {
        if (rumbleOnItsOwn && !IsRumbling && Time.time >= nextRumble)
        {
            float scale = Mathf.Clamp(intensity, 0.25f, 2f);
            int debris = Mathf.RoundToInt(Random.Range(debrisPerRumble.x, debrisPerRumble.y + 1) * scale);
            Rumble(Random.Range(strength.x, strength.y) * Mathf.Min(scale, 1.6f), Random.Range(duration.x, duration.y), debris);
        }

        float amount = Envelope() * rumbleStrength;
        if (amount > 0f)
            CameraShake.ShakeAtLeast(amount);

        punch = Mathf.MoveTowards(punch, 0f, PunchRecovery * Time.unscaledDeltaTime);
        if (screenEffects != null)
            screenEffects.weight = Mathf.Clamp01(amount * 1.3f + punch) * screenDistortion;

        UpdateLight(amount);
        EmitDust(amount);
    }

    void ScheduleNext()
    {
        nextRumble = Time.time + Random.Range(secondsBetween.x, secondsBetween.y) / Mathf.Max(0.1f, intensity);
    }

    // 0 to 1 over a rumble: swells in over the first fifth, then dies away.
    float Envelope()
    {
        if (!IsRumbling) return 0f;
        float t = Mathf.InverseLerp(rumbleStart, rumbleEnd, Time.time);
        return t < 0.2f ? t / 0.2f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.8f);
    }

    void UpdateLight(float amount)
    {
        if (globalLight == null) return;

        // An uneven browning out rather than a smooth fade.
        float flicker = Mathf.PerlinNoise(Time.time * 14f, 0.3f);
        globalLight.intensity = lightIntensity * (1f - LightDip * amount * (0.5f + flicker));
        globalLight.color = Color.Lerp(lightColor, emergencyColor, Mathf.Clamp01(amount * 1.5f));
    }

    // Its own post-processing, layered over the level's and faded in by weight: lens warp, color fringing, darker edges.
    void CreateScreenEffects()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.Add<ChromaticAberration>().intensity.Override(0.8f);
        profile.Add<LensDistortion>().intensity.Override(-0.18f);
        profile.Add<ColorAdjustments>().postExposure.Override(-0.35f);
        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.55f);
        vignette.smoothness.Override(0.6f);
        vignette.color.Override(new Color(0.12f, 0.03f, 0f));

        var child = new GameObject("Rumble Screen Effects");
        child.transform.SetParent(transform, false);
        screenEffects = child.AddComponent<Volume>();
        screenEffects.isGlobal = true;
        screenEffects.priority = 50f;
        screenEffects.weight = 0f;
        screenEffects.sharedProfile = profile;
    }

    // --- Debris ---

    IEnumerator DropDebrisDuring(int count, float seconds)
    {
        bool aimed = false;
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(Random.Range(0.1f, seconds * 0.6f / count));

            bool aim = !aimed && Random.value < aimAtPlayerChance;
            if (!TryPickDebrisPoint(aim, out Vector2 point)) continue;

            FallingDebris.Drop(point, debrisDamage);
            aimed |= aim;
        }
    }

    // Somewhere clear on the floor near the player, or right at their feet if aimAtPlayer.
    public bool TryPickDebrisPoint(bool aimAtPlayer, out Vector2 point)
    {
        point = default;
        if (player == null || Locker.IsPlayerHidden) return false;

        Vector2 feet = player.position;
        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;
            feet = new Vector2(bounds.center.x, bounds.min.y + 0.4f);
        }

        // Heading somewhere: most pieces come down in front, within a cone either side of the way they're going.
        Vector2 velocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
        bool ahead = velocity.sqrMagnitude > 0.25f && Random.value < aheadChance;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 direction = ahead
                ? (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f)) * velocity.normalized)
                : Random.insideUnitCircle.normalized;
            point = aimAtPlayer && attempt < 3
                ? feet + Random.insideUnitCircle * 0.3f
                : feet + direction * Random.Range(debrisMinDistance, debrisMaxDistance);
            if (IsClearFloor(point)) return true;
        }
        return false;
    }

    // On a floor tile, with nothing solid there but characters.
    public bool IsClearFloor(Vector2 point)
    {
        if (floor != null && !floor.HasTile(floor.WorldToCell(point))) return false;

        foreach (Collider2D col in Physics2D.OverlapCircleAll(point, 0.35f))
        {
            if (col.isTrigger) continue;
            Rigidbody2D body = col.attachedRigidbody;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic) continue;
            return false;
        }
        return true;
    }

    // --- Dust and sound ---

    // Specks drifting down across the whole view while it rumbles, and a few warm motes hanging in the air all the time.
    void EmitDust(float amount)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (dust == null) dust = CreateDust();
        if (dust == null) return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector2 center = cam.transform.position;
        var particle = new ParticleSystem.EmitParams();

        dustOwed += DustPerSecond * amount * Time.deltaTime;
        while (dustOwed >= 1f)
        {
            dustOwed -= 1f;
            particle.position = center + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight));
            particle.velocity = new Vector2(Random.Range(-0.1f, 0.1f), -Random.Range(0.4f, 1.2f));
            particle.startLifetime = Random.Range(0.6f, 1.4f);
            particle.startSize = Random.Range(0.03f, 0.07f);
            particle.startColor = new Color(0.85f, 0.74f, 0.6f, Random.Range(0.2f, 0.5f));
            dust.Emit(particle, 1);
        }

        motesOwed += MotesPerSecond * Time.deltaTime;
        while (motesOwed >= 1f)
        {
            motesOwed -= 1f;
            particle.position = center + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight));
            particle.velocity = Random.insideUnitCircle * 0.12f + new Vector2(0f, -0.04f);
            particle.startLifetime = Random.Range(3f, 6f);
            particle.startSize = Random.Range(0.02f, 0.045f);
            particle.startColor = new Color(1f, 0.8f, 0.52f, Random.Range(0.12f, 0.3f));
            dust.Emit(particle, 1);
        }
    }

    ParticleSystem CreateDust()
    {
        Material material = CombatSprites.EffectMaterial;
        if (material == null) return null;

        var child = new GameObject("Dust");
        child.transform.SetParent(transform, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Particles only come from Emit calls.
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = fade;

        var particleRenderer = child.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = new Material(material) { mainTexture = CombatSprites.Square.texture };
        particleRenderer.sortingLayerName = DustSortingLayer;
        particleRenderer.sortingOrder = 20;

        system.Play();
        return system;
    }

    static float Nearness(Vector2 point)
    {
        Camera cam = Camera.main;
        float distance = cam != null ? Vector2.Distance(cam.transform.position, point) : 0f;
        return Mathf.Clamp01(1f - distance / 14f);
    }

    void PlayRandom(AudioClip[] clips, float volumeScale)
    {
        if (clips == null || clips.Length == 0 || volumeScale <= 0f) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) sfx.PlayOneShot(clip, volume * volumeScale);
    }
}
