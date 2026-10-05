using UnityEngine;

// The ducts' own noises, made in code, for VentNetwork: the metal round the player groaning and ticking, something
// skittering behind a panel, slow breathing that isn't theirs, knocking far off down the ducts, the rat, and the drone's
// servos and the shriek it gives when it locks on. Each is made once and kept. They're the scene's sounds by these
// names (SoundDefaults.Vents, with no clips), so giving one a clip in the SoundManager replaces the one made here.
public static class VentSounds
{
    const int Rate = TitleUI.SampleRate;

    static AudioClip creak, skitter, breath, knock, ratSqueal, ratScurry, servo, lockOn, whisper, bang;

    // Head first into a duct wall: a hard hit, the sheet metal booming and rattling, and the boom coming back off the
    // far end of the duct a moment later.
    public static AudioClip Bang => bang != null ? bang : (bang = Make("Vent Bang", 2f, 19, (t, rnd) =>
    {
        float Hit(float local, float loudness)
        {
            if (local < 0f) return 0f;
            float crack = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-local * 70f);
            float thump = Mathf.Sin(2f * Mathf.PI * 70f * local) * Mathf.Exp(-local * 10f);
            // The panel ringing: a handful of out-of-tune partials, the high ones dying first.
            float ring = Mathf.Sin(2f * Mathf.PI * 173f * local) * Mathf.Exp(-local * 3.2f) * 0.55f
                       + Mathf.Sin(2f * Mathf.PI * 289f * local) * Mathf.Exp(-local * 4.5f) * 0.4f
                       + Mathf.Sin(2f * Mathf.PI * 447f * local) * Mathf.Exp(-local * 6f) * 0.3f
                       + Mathf.Sin(2f * Mathf.PI * 731f * local) * Mathf.Exp(-local * 9f) * 0.2f;
            // A loose panel rattling for a moment after.
            float rattle = (Mathf.PerlinNoise(local * 140f, 2.5f) - 0.5f) * Mathf.Exp(-local * 7f) * (local > 0.05f ? 0.9f : 0f);
            return (crack * 0.8f + thump + ring + rattle) * loudness;
        }
        return Mathf.Clamp((Hit(t, 1f) + Hit(t - 0.28f, 0.35f) + Hit(t - 0.6f, 0.15f)) * 0.75f, -1f, 1f);
    }));

    // A long metal groan: a few out-of-tune partials bending slowly, swelling and dying.
    public static AudioClip Creak => creak != null ? creak : (creak = Make("Vent Creak", 2.2f, 11, (t, rnd) =>
    {
        float bend = 1f + 0.08f * Mathf.Sin(t * 2.1f) + 0.05f * t;
        float tone = Mathf.Sin(2f * Mathf.PI * 97f * bend * t) * 0.5f
                   + Mathf.Sin(2f * Mathf.PI * 233f * bend * t + Mathf.Sin(t * 13f)) * 0.3f
                   + Mathf.Sin(2f * Mathf.PI * 311f * bend * t) * 0.15f;
        float grit = (Mathf.PerlinNoise(t * 60f, 0.3f) - 0.5f) * 0.6f;
        float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2.2f)) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 7f, 1.7f));
        return (tone * (0.7f + grit)) * envelope * 0.55f;
    }));

    // Little claws on sheet metal, in bursts, somewhere behind a panel.
    public static AudioClip Skitter => skitter != null ? skitter : (skitter = Make("Vent Skitter", 1.4f, 23, Clicks(1.4f, 34f, 0.5f)));

    // Something breathing in the dark: filtered noise drawn in and let out, twice.
    public static AudioClip Breath => breath != null ? breath : (breath = Make("Vent Breath", 3.6f, 5, Filtered(3.6f, 0.06f, t =>
    {
        float cycle = Mathf.Repeat(t, 1.8f) / 1.8f;
        float inhale = cycle < 0.4f ? Mathf.Sin(Mathf.PI * cycle / 0.4f) * 0.7f : 0f;
        float exhale = cycle >= 0.45f ? Mathf.Sin(Mathf.PI * (cycle - 0.45f) / 0.55f) : 0f;
        return (inhale + exhale) * 0.5f;
    })));

    // Three knocks far off down the ducts, a pause between each, with a ringing tail.
    public static AudioClip Knock => knock != null ? knock : (knock = Make("Vent Knock", 2.6f, 7, (t, rnd) =>
    {
        float sum = 0f;
        foreach (float at in new[] { 0f, 0.55f, 1.35f })
        {
            float local = t - at;
            if (local < 0f) continue;
            float thump = Mathf.Sin(2f * Mathf.PI * 62f * local) * Mathf.Exp(-local * 9f);
            float ring = Mathf.Sin(2f * Mathf.PI * 410f * local) * Mathf.Exp(-local * 3.5f) * 0.18f;
            float hit = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-local * 60f) * 0.4f;
            sum += thump + ring + hit;
        }
        return sum * 0.6f;
    }));

    // A whisper, nearly words: breathy noise shaped into syllables, dropping off at the end.
    public static AudioClip Whisper => whisper != null ? whisper : (whisper = Make("Vent Whisper", 2.4f, 31, Filtered(2.4f, 0.35f, t =>
    {
        float syllable = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3.3f)) * Mathf.PerlinNoise(t * 4f, 9f);
        return syllable * Mathf.Clamp01(1f - (t - 1.8f) / 0.6f) * 0.45f;
    })));

    // The rat: a run of sharp, high squeals, loud.
    public static AudioClip RatSqueal => ratSqueal != null ? ratSqueal : (ratSqueal = Make("Rat Squeal", 1.1f, 3, (t, rnd) =>
    {
        float sum = 0f;
        foreach (float at in new[] { 0f, 0.18f, 0.42f, 0.62f })
        {
            float local = t - at;
            if (local < 0f || local > 0.22f) continue;
            float pitch = 3200f + 1400f * Mathf.Sin(local * 40f) - 2600f * local;
            float squeak = Mathf.Sin(2f * Mathf.PI * pitch * local);
            sum += squeak * Mathf.Sin(Mathf.PI * local / 0.22f);
        }
        return Mathf.Clamp(sum * 1.4f, -1f, 1f) * 0.8f;
    }));

    // Its feet on the metal, fast and close.
    public static AudioClip RatScurry => ratScurry != null ? ratScurry : (ratScurry = Make("Rat Scurry", 1.2f, 17, Clicks(1.2f, 70f, 0.9f)));

    // The drone turning to look: a short whine of servos.
    public static AudioClip Servo => servo != null ? servo : (servo = Make("Drone Servo", 0.45f, 2, (t, rnd) =>
    {
        float pitch = Mathf.Lerp(180f, 290f, t / 0.45f);
        float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * pitch * t)) * 0.35f + Mathf.Sin(2f * Mathf.PI * pitch * 2.01f * t) * 0.3f;
        return wave * Mathf.Sin(Mathf.PI * t / 0.45f) * 0.45f;
    }));

    // The drone locking on: a rising, tearing shriek with static under it.
    public static AudioClip LockOn => lockOn != null ? lockOn : (lockOn = Make("Drone Lock", 1f, 13, (t, rnd) =>
    {
        float pitch = Mathf.Lerp(260f, 1900f, t * t);
        float saw = 2f * Mathf.Repeat(pitch * t, 1f) - 1f;
        float second = Mathf.Sin(2f * Mathf.PI * pitch * 1.5f * t);
        float hiss = ((float)rnd.NextDouble() * 2f - 1f) * 0.45f;
        float sound = Mathf.Clamp((saw * 0.6f + second * 0.4f + hiss) * 1.6f, -1f, 1f);
        return sound * Mathf.Clamp01(t / 0.05f) * 0.75f;
    }));

    // The one made here for a sound's name, for SoundManager's fallback. Null for a name that isn't one of these.
    public static AudioClip Fallback(string soundName) => soundName switch
    {
        "Vent Bang" => Bang,
        "Vent Creak" => Creak,
        "Vent Skitter" => Skitter,
        "Vent Breath" => Breath,
        "Vent Knock" => Knock,
        "Vent Whisper" => Whisper,
        "Rat Squeal" => RatSqueal,
        "Rat Scurry" => RatScurry,
        "Drone Servo" => Servo,
        "Drone Lock" => LockOn,
        _ => null,
    };

    // --- Making them ---

    // Bursts of clicks: so many a second, each a tiny tick of noise, the bursts coming and going.
    static System.Func<float, System.Random, float> Clicks(float seconds, float perSecond, float loudness)
    {
        var at = new float[Mathf.CeilToInt(seconds * perSecond)];
        var dice = new System.Random(41);
        float time = 0f;
        for (int i = 0; i < at.Length; i++)
        {
            time += (float)dice.NextDouble() * 2f / perSecond;
            at[i] = time;
        }
        return (t, rnd) =>
        {
            float sum = 0f;
            foreach (float click in at)
            {
                float local = t - click;
                if (local < 0f || local > 0.012f) continue;
                sum += ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-local * 500f);
            }
            float bursts = 0.4f + 0.6f * Mathf.PerlinNoise(t * 3f, 4.2f);
            return sum * bursts * loudness;
        };
    }

    // Noise, low-passed (smoothing is how much each sample keeps of the last, 0 to 1), shaped by an envelope.
    static System.Func<float, System.Random, float> Filtered(float seconds, float smoothing, System.Func<float, float> envelope)
    {
        float last = 0f;
        float keep = Mathf.Clamp01(1f - smoothing);
        return (t, rnd) =>
        {
            float white = (float)rnd.NextDouble() * 2f - 1f;
            last = last * keep + white * (1f - keep);
            return last * 4f * envelope(t);
        };
    }

    static AudioClip Make(string clipName, float seconds, int seed, System.Func<float, System.Random, float> sample)
    {
        int samples = Mathf.CeilToInt(seconds * Rate);
        var data = new float[samples];
        var random = new System.Random(seed);
        for (int i = 0; i < samples; i++)
            data[i] = Mathf.Clamp(sample(i / (float)Rate, random), -1f, 1f);
        // A few milliseconds in and out, so nothing clicks at the ends.
        int fade = Mathf.Min(samples / 2, Rate / 200);
        for (int i = 0; i < fade; i++)
        {
            float ramp = i / (float)fade;
            data[i] *= ramp;
            data[samples - 1 - i] *= ramp;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
