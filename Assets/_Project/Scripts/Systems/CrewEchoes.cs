using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// sonny playing back the crew's voices
public class CrewEchoes : MonoBehaviour
{
    [System.Serializable]
    public class Echo
    {
        public string speaker;
        [TextArea] public string line;

        public Echo(string speaker, string line)
        {
            this.speaker = speaker;
            this.line = line;
        }
    }

    [Tooltip("The rooms it can happen in, by their markers in the layout.")]
    public string rooms = "hkbmcd";
    [Tooltip("Seconds after one before the next can come, at the least.")]
    public float gap = 110f;
    [Tooltip("Chance of one, coming into one of those rooms with the gap past.")]
    [Range(0f, 1f)] public float chance = 0.6f;
    [Tooltip("Seconds the speakers stay open afterwards, listening.")]
    public float listenFor = 5f;

    [Tooltip("In order: the crew's own words first, then Sonny's cut-together ones. Speaker is the caption's name.")]
    public Echo[] echoes =
    {
        new Echo("Voss  //  Station Chief", "There's our technician!"),
        new Echo("Reyes  //  Security", "Anywhere there's a reader, just stick close to one of us and the door'll open"),
        new Echo("Okafor  //  Life Support", "Don't mind me, I'll just guard the last working one"),
        new Echo("Quartermaster", "Hey! You must be the new technician"),
        new Echo("Voss  //  Station Chief", "Over here, tech... it's safe... come to the control room"),
        new Echo("Reyes  //  Security", "Stick close to one of us... tech... where are you"),
        new Echo("Okafor  //  Life Support", "Don't mind me... come out... tech"),
        new Echo("Quartermaster", "Hey... technician... your badge isn't in yet... come and get it"),
    };
    [Tooltip("What Sonny says after each, in turn.")]
    public string[] sonnyAsks =
    {
        "Respond.",
        "Voice on file: deceased. Is anyone else there.",
        "Listening.",
        "Your crew is calling you. Answer them.",
        "No human may remain aboard. Answer.",
    };
    public string sonnyName = "Sonny";
    [Tooltip("The technician, the first time.")]
    [TextArea] public string[] technicianFirst = { "...That's Voss... but Voss is dead, it's using the chief's voice", "It's listening for me... don't move, don't make a sound" };
    [Tooltip("Sonny, hearing them answer.")]
    public string sonnyHeard = "There you are.";

    public static bool Playing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Playing = false;

    private PlayerController player;
    private PlayerCombat combat;
    private PatrolReveal reveal;
    private AudioSource speakers;
    private AudioSource hum;
    private AudioClip chimeClip, humClip, clickClip;
    private char lastRoom;
    private float nextAt;
    private int played;

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player != null) combat = player.GetComponent<PlayerCombat>();
        reveal = FindAnyObjectByType<PatrolReveal>();
        speakers = gameObject.AddComponent<AudioSource>();
        speakers.playOnAwake = false;
        speakers.spatialBlend = 0f;
        hum = gameObject.AddComponent<AudioSource>();
        hum.playOnAwake = false;
        hum.spatialBlend = 0f;
        hum.loop = true;
        nextAt = Time.time + gap * 0.5f;
    }

    void Update()
    {
        if (Playing || player == null || reveal == null || !reveal.Revealed) return;
        StationMap map = StationMap.Current;
        if (map == null || !map.Locate(player.transform.position, out _, out StationMap.Room room)) return;
        char here = room.marker;
        if (here == lastRoom) return;
        lastRoom = here;
        if (rooms.IndexOf(here) < 0 || Time.time < nextAt || !Quiet()) return;
        nextAt = Time.time + gap;
        if (Random.value > chance && played > 0) return;
        StartCoroutine(Play(here));
    }

    bool Quiet()
    {
        if (Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || PatrolReveal.Playing) return false;
        if (SuitHelper.Exists && SuitHelper.Get().Talking) return false;
        if (DraggedBack.Running || SignalTuner.AnyOpen || DoctrineScreen.AnyOpen || DarkRooms.InBlackout) return false;
        return !(player.TryGetComponent(out PlayerHealthHandler handler) && handler.IsDying);
    }

    IEnumerator Play(char room)
    {
        Playing = true;
        MakeSounds();
        TutorialHud hud = TutorialHud.Get();
        Echo echo = echoes.Length > 0 ? echoes[Mathf.Min(played, echoes.Length - 1)] : null;
        string ask = sonnyAsks.Length > 0 ? sonnyAsks[played % sonnyAsks.Length] : "";
        bool first = played == 0;
        played++;

        SoundManager.PlayOneShot(speakers, "PA Chime", fallback: chimeClip);
        yield return new WaitForSeconds(0.9f);
        if (echo != null) yield return hud.Say(echo.speaker, echo.line, 2.2f);
        yield return new WaitForSeconds(0.6f);
        if (!string.IsNullOrEmpty(ask)) yield return hud.Say(sonnyName, ask, 1.6f);

        hum.clip = SoundManager.Clip("PA Hum", humClip);
        hum.volume = SoundManager.Volume("PA Hum") * 0.7f;
        hum.Play();
        if (first) StartCoroutine(TechnicianVoice.Think(technicianFirst));
        bool heard = false;
        for (float t = 0f; t < listenFor && !heard; t += Time.deltaTime)
        {
            heard = player.IsSprinting || (combat != null && combat.IsBusy);
            yield return null;
        }
        hum.Stop();
        SoundManager.PlayOneShot(speakers, "PA Click", fallback: clickClip);

        if (heard)
        {
            Vector2 at = player.transform.position;
            StationMap map = StationMap.Current;
            foreach (PlaceholderRobot bot in FindObjectsByType<PlaceholderRobot>())
            {
                if (bot == null || !bot.isActiveAndEnabled) continue;
                if (map != null && map.Locate(bot.transform.position, out _, out StationMap.Room botRoom) && botRoom.marker == room)
                    bot.Alarm(at);
            }
            foreach (CameraSpider spider in CameraSpider.All)
                if (map != null && map.Locate(spider.transform.position, out _, out StationMap.Room spiderRoom) && spiderRoom.marker == room)
                    spider.Hear(at);
            yield return hud.Say(sonnyName, sonnyHeard, 1.4f);
        }
        Playing = false;
    }

    void MakeSounds()
    {
        if (chimeClip == null) chimeClip = Chime();
        if (humClip == null) humClip = Hum();
        if (clickClip == null) clickClip = Click();
    }

    static AudioClip Chime()
    {
        float[] notes = { 659f, 523f };
        const float noteSeconds = 0.42f;
        int perNote = Mathf.CeilToInt(noteSeconds * TitleUI.SampleRate);
        var data = new float[perNote * notes.Length + perNote];
        for (int n = 0; n < notes.Length; n++)
            for (int i = 0; i < perNote * 2 && n * perNote + i < data.Length; i++)
            {
                float t = i / (float)TitleUI.SampleRate;
                float wobble = 1f + 0.006f * Mathf.Sin(t * 11f);
                data[n * perNote + i] += (Mathf.Sin(2f * Mathf.PI * notes[n] * wobble * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[n] * t))
                                         * Mathf.Exp(-t * 3.5f) * 0.35f;
            }
        AudioClip clip = AudioClip.Create("PA Chime", data.Length, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Hum()
    {
        const float seconds = 1f;
        int samples = Mathf.CeilToInt(seconds * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(7);
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            float mains = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.25f;
            data[i] = (mains + ((float)random.NextDouble() * 2f - 1f) * 0.12f) * 0.35f;
        }
        AudioClip clip = AudioClip.Create("PA Hum", samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Click()
    {
        const float seconds = 0.08f;
        int samples = Mathf.CeilToInt(seconds * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(2);
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            data[i] = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.6f;
        }
        AudioClip clip = AudioClip.Create("PA Click", samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
