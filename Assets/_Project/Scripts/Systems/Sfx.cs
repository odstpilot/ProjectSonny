using System.Collections.Generic;
using UnityEngine;

// Sound effects played at a point in the level, for anything that hasn't its own AudioSource: the weapons, the EMP, the
// robots, the wall breaches, the wall grab. Each is played by its name in the scene's SoundManager (SoundDefaults.Combat
// lists them), quieter the further it is from the camera, its pitch varied a little so repeats don't drone. A sound the
// scene doesn't list, or lists with no clip yet, plays its placeholder, made in code here (Placeholder).
// Robots being hit and blowing up are heard from here too, whatever hit them (Health.AnyDamaged, Health.AnyDied).
public static class Sfx
{
    const int Voices = 16;              // how many sounds can overlap
    const float SameSoundGap = 0.04f;   // the same sound no closer together than this, so a volley doesn't stack up
    const float HearingRange = 18f;     // world units from the camera where a sound has faded out

    static AudioSource[] voices;
    static int nextVoice;
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    static readonly Dictionary<string, AudioClip> placeholders = new Dictionary<string, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        voices = null;
        lastPlayed.Clear();
        Health.AnyDamaged -= OnAnyDamaged;
        Health.AnyDied -= OnAnyDied;
        Health.AnyDamaged += OnAnyDamaged;
        Health.AnyDied += OnAnyDied;
    }

    // At a point in the level: fainter the further it is from the camera, silent past hearingRange.
    public static void At(string soundName, Vector2 point, float volume = 1f, float pitch = 1f, float hearingRange = HearingRange)
    {
        Camera cam = Camera.main;
        float nearness = cam == null ? 1f : 1f - Mathf.Clamp01(Vector2.Distance(cam.transform.position, point) / hearingRange);
        if (nearness <= 0f) return;
        Play(soundName, volume * nearness * nearness, pitch);
    }

    // Wherever the player is: for what's happening to them.
    public static void Play(string soundName, float volume = 1f, float pitch = 1f)
    {
        if (volume <= 0f || string.IsNullOrEmpty(soundName)) return;
        if (lastPlayed.TryGetValue(soundName, out float last) && Time.unscaledTime - last < SameSoundGap) return;
        lastPlayed[soundName] = Time.unscaledTime;
        AudioSource voice = NextVoice();
        if (voice == null) return;
        SoundManager.PlayOneShot(voice, soundName, volume, pitch * Random.Range(0.94f, 1.06f), Placeholder(soundName));
    }

    static AudioSource NextVoice()
    {
        if (voices == null || voices[0] == null)
        {
            var host = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(host);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = host.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
        }
        nextVoice = (nextVoice + 1) % Voices;
        return voices[nextVoice];
    }

    // --- Robots, however they're hurt ---

    static bool IsRobot(Health health) =>
        health != null && (health.GetComponent<PlaceholderRobot>() != null || health.GetComponent<Warden>() != null || health.GetComponent<MaintenanceBot>() != null);

    static void OnAnyDamaged(Health health, DamageInfo info)
    {
        if (!IsRobot(health) || health.IsDead) return;
        At("Robot Hit", health.transform.position);
    }

    static void OnAnyDied(Health health)
    {
        if (!IsRobot(health)) return;
        At("Robot Explode", health.transform.position, 1f, 1f, HearingRange * 1.4f);
    }

    // --- Placeholders, made in code ---

    const int Rate = TitleUI.SampleRate;

    // The placeholder for a sound, made the first time it's wanted. Null for a name with none.
    public static AudioClip Placeholder(string soundName)
    {
        if (placeholders.TryGetValue(soundName, out AudioClip made) && made != null) return made;
        System.Func<float, float> wave;
        float seconds;
        var noise = new System.Random(soundName.GetHashCode());
        float Noise() => (float)noise.NextDouble() * 2f - 1f;
        float filtered = 0f;
        // White noise through a one-pole low-pass: smooth 0 (dull) to 1 (bright).
        float Lowpass(float smooth)
        {
            filtered += (Noise() - filtered) * smooth;
            return filtered;
        }
        float Sine(float frequency, float t) => Mathf.Sin(2f * Mathf.PI * frequency * t);
        float Square(float frequency, float t) => Mathf.Sign(Sine(frequency, t));
        float Attack(float t, float rise) => Mathf.Clamp01(t / rise);

        switch (soundName)
        {
            // The weapons.
            case "Melee Swing":     // a whoosh, brightening then dying
                seconds = 0.24f;
                wave = t => Lowpass(0.05f + 0.35f * Mathf.Sin(Mathf.PI * t / 0.24f)) * 2.2f * Mathf.Sin(Mathf.PI * t / 0.24f);
                break;
            case "Melee Hit":       // a dull metal clang
                seconds = 0.35f;
                wave = t => (Sine(220f, t) * 0.5f + Sine(563f, t) * 0.3f + Sine(1170f, t) * 0.2f + Noise() * Mathf.Exp(-t * 80f)) * Mathf.Exp(-t * 12f);
                break;
            case "Gun Shot":        // a crack and a thump
                seconds = 0.22f;
                wave = t => (Noise() * Mathf.Exp(-t * 35f) * 0.8f + Sine(90f - 40f * t, t) * Mathf.Exp(-t * 18f) * 0.7f) * Attack(t, 0.002f);
                break;
            case "Blaster Charge":  // a rising whine
                seconds = 0.7f;
                wave = t => Sine(200f + 900f * t * t, t) * 0.4f * Attack(t, 0.05f) * (1f - Mathf.Clamp01((t - 0.62f) / 0.08f));
                break;
            case "Blaster Fire":    // a falling zap
                seconds = 0.3f;
                wave = t => (Square(900f - 2200f * t, t) * 0.25f + Noise() * 0.15f) * Mathf.Exp(-t * 9f) * Attack(t, 0.003f);
                break;
            case "Blaster Blast":   // an energy blast
            case "Robot Explode":   // a bang, a deep boom, and debris rattling down
                seconds = 1f;
                wave = t => (Lowpass(0.25f) * 1.6f * Mathf.Exp(-t * 4f) + Sine(70f - 35f * t, t) * Mathf.Exp(-t * 5f) * 0.9f
                    + (Noise() * 0.5f * (Noise() > 0.97f ? 1f : 0f)) * Mathf.Exp(-t * 2.5f)) * Attack(t, 0.003f);
                break;
            case "Projectile Impact":   // a small clank
                seconds = 0.15f;
                wave = t => (Sine(1400f, t) * 0.3f + Noise() * 0.5f) * Mathf.Exp(-t * 40f);
                break;
            case "EMP Fire":        // a buzzing pulse sweeping down, crackling out
                seconds = 0.8f;
                wave = t => (Square(160f - 90f * t, t) * 0.25f * Mathf.Exp(-t * 3f) + Sine(1800f - 1500f * t, t) * 0.2f * Mathf.Exp(-t * 6f)
                    + Noise() * 0.35f * (Noise() > 0.85f ? 1f : 0.15f) * Mathf.Exp(-t * 3.5f)) * Attack(t, 0.004f);
                break;
            case "EMP Ready":       // two rising blips
                seconds = 0.26f;
                wave = t => Sine(t < 0.11f ? 880f : 1320f, t) * 0.35f * Mathf.Exp(-(t % 0.13f) * 30f);
                break;
            // The robots.
            case "Robot Hit":       // a crunch of metal and a crackle
                seconds = 0.25f;
                wave = t => (Noise() * 0.6f * Mathf.Exp(-t * 25f) + Square(140f, t) * 0.25f * Mathf.Exp(-t * 15f)) * Attack(t, 0.002f);
                break;
            case "Robot Alert":     // it's seen them: a sharp two-tone beep, rising
                seconds = 0.32f;
                wave = t => Square(t < 0.14f ? 740f : 1100f, t) * 0.22f * (t % 0.16f < 0.12f ? 1f : 0f);
                break;
            case "Robot Attack Windup":     // a servo winding up
                seconds = 0.5f;
                wave = t => (Square(120f + 500f * t, t) * 0.18f + Sine(60f + 250f * t, t) * 0.3f) * Attack(t, 0.04f);
                break;
            case "Robot Attack Lunge":      // a lunge: a whoosh and a snap
                seconds = 0.3f;
                wave = t => (Lowpass(0.3f) * 1.4f * Mathf.Sin(Mathf.PI * t / 0.3f) + Sine(320f, t) * 0.4f * Mathf.Exp(-t * 30f)) * Attack(t, 0.003f);
                break;
            case "Robot Beep":      // chatter: a few quick beeps
                seconds = 0.36f;
                wave = t =>
                {
                    int note = (int)(t / 0.09f);
                    float[] pitches = { 1200f, 900f, 1500f, 1050f };
                    return Sine(pitches[note % pitches.Length], t) * 0.25f * ((t % 0.09f) < 0.06f ? 1f : 0f);
                };
                break;
            case "Robot Shorted":   // fizzing and sputtering out
                seconds = 0.7f;
                wave = t => (Noise() * (Noise() > 0.6f ? 0.6f : 0.1f) + Square(220f - 150f * t, t) * 0.15f) * Mathf.Exp(-t * 3f) * Attack(t, 0.003f);
                break;
            // The set pieces.
            case "Wall Breach":     // a wall bursting in: a slam, and rubble pouring
                seconds = 1.3f;
                wave = t => (Sine(55f, t) * Mathf.Exp(-t * 6f) * 0.9f + Lowpass(0.35f) * 1.8f * Mathf.Exp(-t * 2.2f)) * Attack(t, 0.002f);
                break;
            case "Wall Grab":       // metal hands clamping on
                seconds = 0.4f;
                wave = t => (Sine(180f, t) * 0.5f + Sine(410f, t) * 0.3f + Noise() * 0.6f * Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 9f);
                break;
            case "Grab Struggle":   // a strain against it: metal grinding
                seconds = 0.18f;
                wave = t => (Lowpass(0.6f) * 0.8f + Square(95f, t) * 0.2f) * Mathf.Sin(Mathf.PI * t / 0.18f);
                break;
            case "Grab Squeeze":    // servos crushing down
                seconds = 0.45f;
                wave = t => (Square(70f, t) * 0.25f + Sine(140f - 60f * t, t) * 0.4f + Lowpass(0.2f) * 0.5f) * Mathf.Sin(Mathf.PI * t / 0.45f);
                break;
            case "Break Free":      // torn loose: a heavy clang
                seconds = 0.55f;
                wave = t => (Sine(130f, t) * 0.5f + Sine(347f, t) * 0.35f + Sine(905f, t) * 0.2f + Noise() * Mathf.Exp(-t * 50f)) * Mathf.Exp(-t * 7f);
                break;
            default:
                placeholders[soundName] = null;
                return null;
        }

        int samples = Mathf.CeilToInt(seconds * Rate);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
            data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f);
        AudioClip clip = AudioClip.Create(soundName, samples, 1, Rate, false);
        clip.SetData(data, 0);
        placeholders[soundName] = clip;
        return clip;
    }
}
