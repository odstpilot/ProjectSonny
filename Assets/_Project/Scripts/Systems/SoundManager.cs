using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// On a string field that names a sound: the inspector offers the scene's sounds to pick from.
public class SoundNameAttribute : PropertyAttribute { }

// What a sound is for. Music and Ambience follow the Music volume in settings (its slider says "music and background
// ambience"); everything else follows the Effects volume.
public enum SoundCategory { Music, Ambience, SoundEffect, UI, Voice }

// One sound in a scene's SoundManager: what it is, the clips for it, and the settings of the audio source it plays on.
// The settings are the same as an AudioSource's, so anything that can be set there can be set here.
[Serializable]
public class Sound
{
    [Tooltip("What scripts call it by, as in SoundManager.Current.Play(\"Door Open\"). Unique in the scene.")]
    public string name = "New Sound";
    public SoundCategory category = SoundCategory.SoundEffect;
    [Tooltip("For whoever finds the audio: what it should sound like, and when it plays.")]
    [TextArea(2, 5)] public string description = "";
    [Tooltip("Where the audio came from and its license, e.g. \"freesound.org/s/316847 - lalks - CC0\".")]
    public string credit = "";

    [Tooltip("One of these is picked at random each time it plays. Leave empty while the audio is still being found.")]
    public AudioClip[] clips = new AudioClip[0];
    [Tooltip("Optional. It plays from here (for 3D sounds); leave empty to play from the SoundManager.")]
    public Transform playFrom;

    // --- The same as an AudioSource's ---
    public AudioMixerGroup output;
    public bool mute;
    public bool bypassEffects;
    public bool bypassListenerEffects;
    public bool bypassReverbZones;
    [Tooltip("Starts when the scene starts.")]
    public bool playOnAwake;
    public bool loop;
    [Tooltip("0 is the most important, 256 the least. Unity drops the least important sounds when too many play.")]
    [Range(0, 256)] public int priority = 128;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(-3f, 3f)] public float pitch = 1f;
    [Range(-1f, 1f)] public float stereoPan;
    [Tooltip("0 is 2D (the same wherever the player is), 1 is 3D (quieter the further it is from the camera).")]
    [Range(0f, 1f)] public float spatialBlend;
    [Range(0f, 1.1f)] public float reverbZoneMix = 1f;

    // --- 3D Sound Settings, only heard when Spatial Blend is above 0 ---
    [Range(0f, 5f)] public float dopplerLevel = 1f;
    [Range(0f, 360f)] public float spread;
    public AudioRolloffMode volumeRolloff = AudioRolloffMode.Logarithmic;
    public float minDistance = 1f;
    public float maxDistance = 500f;

    // --- Variation, so repeated sounds (footsteps, hits) don't all sound the same ---
    [Tooltip("Each play is up to this much quieter, at random.")]
    [Range(0f, 1f)] public float volumeVariation;
    [Tooltip("Each play is up to this much higher or lower, at random.")]
    [Range(0f, 1f)] public float pitchVariation;

    // --- Repeat, for sounds that come back now and then: distant groans, music with silence between tracks ---
    [Tooltip("Once started, plays again and again, with a random wait between plays, until stopped. Ignored when it loops.")]
    public bool repeat;
    [Tooltip("The shortest and longest wait, in seconds, from the end of one play to the start of the next.")]
    public Vector2 repeatWait = new Vector2(5f, 20f);

    public bool HasClip
    {
        get
        {
            foreach (AudioClip clip in clips) if (clip != null) return true;
            return false;
        }
    }

    public AudioClip RandomClip()
    {
        if (clips.Length == 0) return null;
        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        return clip != null ? clip : Array.Find(clips, c => c != null);
    }

    // Gives the source every setting here except volume, which SoundManager keeps up to date with the settings menu.
    public void ApplyTo(AudioSource source)
    {
        source.outputAudioMixerGroup = output;
        source.mute = mute;
        source.bypassEffects = bypassEffects;
        source.bypassListenerEffects = bypassListenerEffects;
        source.bypassReverbZones = bypassReverbZones;
        source.playOnAwake = false;     // SoundManager starts Play On Awake sounds itself, so it can pick the clip
        source.loop = loop;
        source.priority = priority;
        source.pitch = pitch;
        source.panStereo = stereoPan;
        source.spatialBlend = spatialBlend;
        source.reverbZoneMix = reverbZoneMix;
        source.dopplerLevel = dopplerLevel;
        source.spread = spread;
        source.rolloffMode = volumeRolloff;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
    }
}

// Every sound one scene uses, listed in one place, by category. Put one in each scene (GameObject > Audio > Sound
// Manager) and list the scene's music, ambience, sound effects, UI sounds and voices on it; sounds still waiting on
// audio can be listed with no clip and a description, and "Copy List" hands the whole list to whoever finds the audio.
// Scripts play them by name: SoundManager.Current.Play("Door Open"). UnityEvents can call Play and Stop too.
// Each sound gets its own AudioSource (a child object, or under Play From), set up from its settings. Changing the
// settings while playing applies straight away, so sounds can be tuned in play mode (copy the component to keep them).
// Scripts that need their own AudioSource (to fade it, move it, or bend its pitch) use the static helpers below
// instead, which still take the clip and settings from here. Either way, every sound follows the Music or Effects
// volume in settings, by its category.
public class SoundManager : MonoBehaviour
{
    private static SoundManager current;

    // The scene's SoundManager. Null when the scene doesn't have one.
    public static SoundManager Current => current != null ? current : (current = FindAnyObjectByType<SoundManager>());

    public List<Sound> sounds = new List<Sound>();

    // --- For scripts that play on their own AudioSource ---

    // The scene's sound by that name, or null when the scene doesn't list it.
    public static Sound Get(string soundName)
    {
        SoundManager manager = Current;
        if (manager == null || string.IsNullOrEmpty(soundName)) return null;
        foreach (Sound sound in manager.sounds)
            if (sound.name == soundName) return sound;
        manager.WarnOnce(soundName, $"SoundManager: there's no sound called \"{soundName}\" in this scene.");
        return null;
    }

    // One of the sound's clips, at random. The fallback when the scene doesn't list it or it has no clip yet, for the
    // placeholder sounds made in code.
    public static AudioClip Clip(string soundName, AudioClip fallback = null)
    {
        AudioClip clip = Get(soundName)?.RandomClip();
        return clip != null ? clip : fallback;
    }

    // The sound's volume times the Music or Effects volume in settings. Multiply a source's volume by this every time
    // it's set, so the settings reach it.
    public static float Volume(string soundName)
    {
        Sound sound = Get(soundName);
        return sound != null ? sound.volume * CategoryVolume(sound.category) : GameSettings.EffectsVolume;
    }

    // Gives the source the sound's settings and a clip (the fallback when it has none), ready to Play. Its volume is
    // left to the script, times Volume(soundName).
    public static void Setup(AudioSource source, string soundName, AudioClip fallback = null)
    {
        Sound sound = Get(soundName);
        if (sound != null) sound.ApplyTo(source);
        source.clip = Clip(soundName, fallback);
    }

    // Plays the sound once on the source, at its volume times volumeScale, with its pitch times pitchScale.
    public static void PlayOneShot(AudioSource source, string soundName, float volumeScale = 1f, float pitchScale = 1f, AudioClip fallback = null)
    {
        Sound sound = Get(soundName);
        AudioClip clip = sound != null ? sound.RandomClip() : null;
        if (clip == null) clip = fallback;
        if (source == null || clip == null || volumeScale <= 0f) return;
        if (sound != null) source.pitch = RandomPitch(sound) * pitchScale;
        else source.pitch = pitchScale;
        float volume = sound != null ? sound.volume * RandomVolume(sound) * CategoryVolume(sound.category) : GameSettings.EffectsVolume;
        source.PlayOneShot(clip, volume * volumeScale);
    }

    private readonly Dictionary<string, Playing> byName = new Dictionary<string, Playing>();
    private readonly List<Playing> all = new List<Playing>();
    private readonly HashSet<string> warned = new HashSet<string>();

    private class Playing
    {
        public Sound sound;
        public AudioSource source;
        public float playVolume = 1f;   // this play's share of the volume, after variation
        public float fade = 1f;
        public float fadeTarget = 1f;
        public float fadeSpeed;         // fade per second; 0 when not fading
        public float nextPlay = -1f;    // when a repeating sound plays next (unscaled time); -1 when it isn't repeating
    }

    void Awake()
    {
        foreach (Sound sound in sounds)
        {
            if (string.IsNullOrEmpty(sound.name) || byName.ContainsKey(sound.name))
            {
                Debug.LogWarning($"SoundManager: \"{sound.name}\" is listed twice or has no name, so it can't be played.", this);
                continue;
            }

            var holder = new GameObject("Sound: " + sound.name);
            holder.transform.SetParent(sound.playFrom != null ? sound.playFrom : transform, false);
            var playing = new Playing { sound = sound, source = holder.AddComponent<AudioSource>() };
            sound.ApplyTo(playing.source);
            byName.Add(sound.name, playing);
            all.Add(playing);
        }
        UpdateVolumes();
    }

    void OnEnable()
    {
        current = this;
    }

    void OnDisable()
    {
        if (current == this) current = null;
    }

    void Start()
    {
        foreach (Playing playing in all)
            if (playing.sound.playOnAwake) Play(playing);
    }

    void Update()
    {
        foreach (Playing playing in all)
        {
            if (playing.nextPlay >= 0f && Time.unscaledTime >= playing.nextPlay) PlayClip(playing);
            if (playing.fadeSpeed <= 0f) continue;
            playing.fade = Mathf.MoveTowards(playing.fade, playing.fadeTarget, playing.fadeSpeed * Time.unscaledDeltaTime);
            if (playing.fade != playing.fadeTarget) continue;
            playing.fadeSpeed = 0f;
            if (playing.fadeTarget <= 0f) Stop(playing);
        }
        UpdateVolumes();
    }

    // Changes made in the inspector while playing take effect straight away.
    void OnValidate()
    {
        foreach (Playing playing in all) playing.sound.ApplyTo(playing.source);
    }

    // Plays the sound. A looping sound that's already playing carries on; anything else plays over itself.
    public void Play(string soundName)
    {
        if (Find(soundName, out Playing playing)) Play(playing);
    }

    // Plays the sound once at a point in the world, for 3D sounds that come from somewhere other than their source.
    public void PlayAt(string soundName, Vector3 position)
    {
        if (!Find(soundName, out Playing playing)) return;
        AudioClip clip = playing.sound.RandomClip();
        if (clip == null) return;

        var source = new GameObject("[Sound] " + soundName).AddComponent<AudioSource>();
        source.transform.position = position;
        playing.sound.ApplyTo(source);
        source.loop = false;
        source.pitch = RandomPitch(playing.sound);
        source.volume = playing.sound.volume * RandomVolume(playing.sound) * CategoryVolume(playing.sound.category);
        source.PlayOneShot(clip);
        Destroy(source.gameObject, clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch)) + 0.1f);
    }

    public void Stop(string soundName)
    {
        if (Find(soundName, out Playing playing)) Stop(playing);
    }

    public void StopAll()
    {
        foreach (Playing playing in all) Stop(playing);
    }

    public void StopCategory(SoundCategory category)
    {
        foreach (Playing playing in all)
            if (playing.sound.category == category) Stop(playing);
    }

    // Starts the sound silent and brings it up to its volume.
    public void FadeIn(string soundName, float seconds)
    {
        if (!Find(soundName, out Playing playing)) return;
        if (!playing.source.isPlaying)
        {
            Play(playing);
            playing.fade = 0f;
            UpdateVolume(playing);
        }
        FadeTo(playing, 1f, seconds);
    }

    // Brings the sound down to silence, then stops it.
    public void FadeOut(string soundName, float seconds)
    {
        if (Find(soundName, out Playing playing)) FadeTo(playing, 0f, seconds);
    }

    public bool IsPlaying(string soundName)
    {
        return byName.TryGetValue(soundName, out Playing playing) && playing.source.isPlaying;
    }

    // The sound's AudioSource, for anything not covered here. Its volume is set every frame, so change the sound's instead.
    public AudioSource Source(string soundName)
    {
        return Find(soundName, out Playing playing) ? playing.source : null;
    }

    void Play(Playing playing)
    {
        Sound sound = playing.sound;
        playing.fade = 1f;
        playing.fadeSpeed = 0f;
        if (sound.loop && playing.source.isPlaying) return;
        PlayClip(playing);
    }

    void PlayClip(Playing playing)
    {
        Sound sound = playing.sound;
        playing.nextPlay = -1f;
        AudioClip clip = sound.RandomClip();
        if (clip == null)
        {
            WarnOnce(sound.name, $"SoundManager: \"{sound.name}\" has no clip yet.");
            return;
        }

        playing.source.pitch = RandomPitch(sound);
        playing.playVolume = RandomVolume(sound);
        UpdateVolume(playing);
        if (sound.loop)
        {
            playing.source.clip = clip;
            playing.source.Play();
        }
        else
        {
            playing.source.PlayOneShot(clip);
            if (sound.repeat)
            {
                float length = clip.length / Mathf.Max(0.01f, Mathf.Abs(playing.source.pitch));
                playing.nextPlay = Time.unscaledTime + length + UnityEngine.Random.Range(sound.repeatWait.x, sound.repeatWait.y);
            }
        }
    }

    void Stop(Playing playing)
    {
        playing.source.Stop();
        playing.fadeSpeed = 0f;
        playing.nextPlay = -1f;
    }

    void FadeTo(Playing playing, float target, float seconds)
    {
        playing.fadeTarget = target;
        playing.fadeSpeed = seconds > 0f ? 1f / seconds : float.MaxValue;
    }

    bool Find(string soundName, out Playing playing)
    {
        if (byName.TryGetValue(soundName, out playing)) return true;
        WarnOnce(soundName, $"SoundManager: there's no sound called \"{soundName}\" in this scene.");
        return false;
    }

    void WarnOnce(string key, string message)
    {
        if (warned.Add(key)) Debug.LogWarning(message, this);
    }

    // Follows the Music and Effects volumes in settings.
    void UpdateVolumes()
    {
        foreach (Playing playing in all) UpdateVolume(playing);
    }

    void UpdateVolume(Playing playing)
    {
        playing.source.volume = playing.sound.volume * playing.playVolume * playing.fade * CategoryVolume(playing.sound.category);
    }

    static float CategoryVolume(SoundCategory category)
    {
        return category == SoundCategory.Music || category == SoundCategory.Ambience ? GameSettings.MusicVolume : GameSettings.EffectsVolume;
    }

    static float RandomVolume(Sound sound)
    {
        return 1f - UnityEngine.Random.Range(0f, sound.volumeVariation);
    }

    static float RandomPitch(Sound sound)
    {
        return sound.pitch + UnityEngine.Random.Range(-sound.pitchVariation, sound.pitchVariation);
    }
}
