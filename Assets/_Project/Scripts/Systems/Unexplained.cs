using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// creepy sounds with nothing there
public class Unexplained : MonoBehaviour
{
    [Tooltip("The rooms it happens in, by their markers in the layout.")]
    public string rooms = "hkbmcd";
    [Tooltip("Seconds between one sound and the next, at least and at most.")]
    public Vector2 gap = new Vector2(40f, 80f);
    [Tooltip("Chance the technician says something about one.")]
    [Range(0f, 1f)] public float remarkChance = 0.2f;
    [TextArea] public string[] remarks =
    {
        "...What was that",
        "...Hello?",
        "Just the hull... it's just the hull",
        "Something's in the walls... great",
        "Don't think about it, just keep moving",
    };

    [Header("Lamps")]
    [Tooltip("Chance a working lamp goes out behind them as they pass it.")]
    [Range(0f, 1f)] public float lampChance = 0.2f;
    [Tooltip("Seconds between one lamp going and the next, at least.")]
    public float lampGap = 45f;
    [Tooltip("Chance it comes back on later.")]
    [Range(0f, 1f)] public float lampReturns = 0.5f;

    enum Kind { Drag, Knocks, Skitter, Creak, Thud, Steps }

    private PlayerController player;
    private AudioSource near, far;
    private float nextAt, nextRemarkAt, nextLampAt;
    private Kind? last, beforeLast;
    private readonly List<StationLight> lamps = new List<StationLight>();
    private readonly HashSet<StationLight> passing = new HashSet<StationLight>();

    static AudioClip drag, knock, tick, skitter, creak, thud;

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        near = Source();
        far = Source();
        far.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 900f;
        nextAt = Time.time + Random.Range(gap.x, gap.y);
        nextLampAt = Time.time + lampGap * 0.5f;
        foreach (StationLight lamp in FindObjectsByType<StationLight>())
            if (lamp.mode == StationLight.Mode.Steady || lamp.mode == StationLight.Mode.Flicker) lamps.Add(lamp);
    }

    AudioSource Source()
    {
        var holder = new GameObject("Unexplained Sound");
        holder.transform.SetParent(transform, false);
        var source = holder.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    void Update()
    {
        if (player == null || !InQuietRoom(out char room)) return;
        if (Time.time >= nextAt && Quiet())
        {
            nextAt = Time.time + Random.Range(gap.x, gap.y);
            StartCoroutine(Play(Pick(room)));
        }
        WatchLamps();
    }

    bool InQuietRoom(out char room)
    {
        room = '\0';
        StationMap map = StationMap.Current;
        if (map == null || !map.Locate(player.transform.position, out _, out StationMap.Room here)) return false;
        room = here.marker;
        return rooms.IndexOf(room) >= 0;
    }

    bool Quiet()
    {
        if (Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || PatrolReveal.Playing || CrewEchoes.Playing) return false;
        if (DraggedBack.Running || SignalTuner.AnyOpen || DoctrineScreen.AnyOpen || MapScreen.IsOpen) return false;
        if (SuitHelper.Exists && SuitHelper.Get().Talking) return false;
        foreach (PlaceholderRobot bot in PlaceholderRobot.All)
            if (bot != null && bot.isActiveAndEnabled && bot.IsHunting) return false;
        foreach (CameraSpider spider in CameraSpider.All)
            if (spider.IsHunting) return false;
        return !(player.TryGetComponent(out PlayerHealthHandler handler) && handler.IsDying);
    }

    Kind Pick(char room)
    {
        var options = new List<Kind>();
        foreach (Kind kind in System.Enum.GetValues(typeof(Kind)))
        {
            if (kind == last || kind == beforeLast) continue;
            if (kind == Kind.Skitter && SpiderIn(room)) continue;
            options.Add(kind);
        }
        Kind picked = options.Count > 0 ? options[Random.Range(0, options.Count)] : Kind.Creak;
        beforeLast = last;
        last = picked;
        return picked;
    }

    static bool SpiderIn(char room)
    {
        StationMap map = StationMap.Current;
        foreach (CameraSpider spider in CameraSpider.All)
            if (map != null && map.Locate(spider.transform.position, out _, out StationMap.Room here) && here.marker == room) return true;
        return false;
    }

    IEnumerator Play(Kind kind)
    {
        MakeSounds();
        float side = Random.value < 0.5f ? -1f : 1f;
        switch (kind)
        {
            case Kind.Drag:
                yield return Sound(far, "Unexplained Drag", drag, 0.5f, Random.Range(0.8f, 1f), side * 0.7f, side * 0.4f);
                break;
            case Kind.Knocks:
                for (int i = 0; i < 3; i++)
                {
                    PlayOnce(near, "Unexplained Knock", knock, 0.75f, Random.Range(0.9f, 1.05f), side * 0.85f);
                    yield return new WaitForSeconds(i == 1 ? Random.Range(0.5f, 0.75f) : Random.Range(0.3f, 0.42f));
                }
                break;
            case Kind.Skitter:
                yield return Sound(far, "Unexplained Skitter", skitter, 0.65f, Random.Range(0.9f, 1.15f), side * 0.9f, -side * 0.9f);
                break;
            case Kind.Creak:
                yield return Sound(far, "Hull Creak", creak, 0.6f, Random.Range(0.75f, 1f), side * 0.3f, -side * 0.2f);
                break;
            case Kind.Thud:
                CameraShake.Shake(0.05f);
                yield return Sound(far, "Distant Thud", thud, 0.7f, Random.Range(0.85f, 1.05f), side * 0.6f, side * 0.6f);
                break;
            case Kind.Steps:
                for (int i = 0; i < 5; i++)
                {
                    float pan = Mathf.Lerp(side * 0.8f, -side * 0.8f, i / 4f);
                    PlayOnce(far, "Overhead Steps", thud, 0.45f, Random.Range(1.5f, 1.7f), pan);
                    yield return new WaitForSeconds(Random.Range(0.6f, 0.8f));
                }
                break;
        }
        if (Time.time >= nextRemarkAt && remarks.Length > 0 && Random.value < remarkChance)
        {
            nextRemarkAt = Time.time + 60f;
            yield return new WaitForSeconds(0.6f);
            TechnicianVoice.Say(remarks[Random.Range(0, remarks.Length)]);
        }
    }

    IEnumerator Sound(AudioSource source, string soundName, AudioClip fallback, float volume, float pitch, float panFrom, float panTo)
    {
        AudioClip clip = SoundManager.Clip(soundName, fallback);
        if (clip == null) yield break;
        source.clip = clip;
        source.volume = volume * SoundManager.Volume(soundName);
        source.pitch = pitch;
        source.panStereo = panFrom;
        source.Play();
        float seconds = clip.length / Mathf.Max(0.1f, pitch);
        for (float t = 0f; t < seconds && source.isPlaying; t += Time.deltaTime)
        {
            source.panStereo = Mathf.Lerp(panFrom, panTo, t / seconds);
            yield return null;
        }
    }

    static void PlayOnce(AudioSource source, string soundName, AudioClip fallback, float volume, float pitch, float pan)
    {
        source.panStereo = pan;
        SoundManager.PlayOneShot(source, soundName, volume, pitch, fallback);
    }

    void WatchLamps()
    {
        Vector2 at = player.transform.position;
        foreach (StationLight lamp in lamps)
        {
            if (lamp == null || !lamp.IsOn || lamp.IsBroken) continue;
            float distance = Vector2.Distance(at, lamp.transform.position);
            if (distance < 2.5f) passing.Add(lamp);
            else if (passing.Remove(lamp) && distance < 4.5f && Time.time >= nextLampAt && Quiet() && Random.value < lampChance)
            {
                nextLampAt = Time.time + lampGap;
                lamp.PowerOff();
                if (Random.value < lampReturns) StartCoroutine(BackOn(lamp, Random.Range(15f, 40f)));
            }
        }
    }

    static IEnumerator BackOn(StationLight lamp, float after)
    {
        yield return new WaitForSeconds(after);
        if (lamp != null) lamp.PowerOn();
    }

    static void MakeSounds()
    {
        if (drag == null) drag = Drag();
        if (knock == null) knock = Knock();
        if (tick == null) tick = Tick();
        if (skitter == null) skitter = Skitter();
        if (creak == null) creak = Creak();
        if (thud == null) thud = Thud();
    }

    static AudioClip Clip(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Drag()
    {
        int samples = Mathf.CeilToInt(2.6f * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(21);
        float low = 0f;
        for (int i = 0; i < samples; i++)
        {
            float p = i / (float)samples;
            float pull = Mathf.Max(0f, Mathf.Sin(p * Mathf.PI * 2f));
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.08f);
            float rattle = Mathf.Sin(p * 380f) > 0.92f ? ((float)random.NextDouble() * 2f - 1f) * 0.5f : 0f;
            data[i] = (low * 1.4f + rattle) * pull * 0.7f;
        }
        return Clip("Unexplained Drag", data);
    }

    static AudioClip Knock()
    {
        int samples = Mathf.CeilToInt(0.3f * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(8);
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            float thump = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 28f);
            float click = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 400f);
            float ring = Mathf.Sin(2f * Mathf.PI * 410f * t) * Mathf.Exp(-t * 35f) * 0.25f;
            data[i] = (thump * 0.8f + click * 0.4f + ring) * 0.8f;
        }
        return Clip("Unexplained Knock", data);
    }

    static AudioClip Tick()
    {
        int samples = Mathf.CeilToInt(0.04f * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(6);
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            data[i] = (((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 300f) * 0.6f
                       + Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 110f) * 0.4f) * 0.8f;
        }
        return Clip("Tick", data);
    }

    static AudioClip Skitter()
    {
        int samples = Mathf.CeilToInt(1.3f * TitleUI.SampleRate);
        var data = new float[samples];
        var source = new float[tick.samples];
        tick.GetData(source, 0);
        for (float at = 0.02f; at < 1.2f; at += Random.Range(0.025f, 0.07f))
        {
            int start = Mathf.FloorToInt(at * TitleUI.SampleRate);
            float loud = Random.Range(0.5f, 1f) * Mathf.Sin(at / 1.2f * Mathf.PI);
            for (int i = 0; i < source.Length && start + i < samples; i++) data[start + i] += source[i] * loud;
        }
        return Clip("Unexplained Skitter", data);
    }

    static AudioClip Creak()
    {
        int samples = Mathf.CeilToInt(3f * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(17);
        float a = 0f, b = 0f, low = 0f;
        for (int i = 0; i < samples; i++)
        {
            float p = i / (float)samples;
            float bend = 1f + 0.25f * Mathf.Sin(p * Mathf.PI * 1.5f);
            a += 2f * Mathf.PI * 58f * bend / TitleUI.SampleRate;
            b += 2f * Mathf.PI * 640f * bend * (1f + 0.02f * Mathf.Sin(p * 90f)) / TitleUI.SampleRate;
            float saw = (a / (2f * Mathf.PI)) % 1f * 2f - 1f;
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.05f);
            float envelope = Mathf.Sin(p * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(p * 23f));
            data[i] = (saw * 0.5f + Mathf.Sin(b) * 0.12f + low * 0.5f) * envelope * 0.6f;
        }
        return Clip("Hull Creak", data);
    }

    static AudioClip Thud()
    {
        int samples = Mathf.CeilToInt(1.2f * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(19);
        float low = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.1f);
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(60f, 38f, t) * t) * Mathf.Exp(-t * 4f);
            data[i] = (boom * 0.9f + low * Mathf.Exp(-t * 10f) * 0.6f) * 0.8f;
        }
        return Clip("Distant Thud", data);
    }
}
