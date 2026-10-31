using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// old tv on the wall
public class StationTV : MonoBehaviour
{
    const float PixelsPerUnit = 16f;
    const int CaseWidth = 23, CaseHeight = 17;
    static readonly RectInt Screen = new RectInt(2, 3, 16, 12);
    const float HearFrom = 14f;

    public static readonly List<StationTV> All = new List<StationTV>();

    static readonly Color CalmColor = new Color(0.45f, 0.82f, 1f);
    static readonly Color RogueColor = new Color(1f, 0.16f, 0.1f);

    [Tooltip("The room it hangs in, by its marker in the layout.")]
    public char room;
    [Tooltip("Sonny's taken the station: no feed, just the dark, static, and its eye.")]
    public bool rogue;
    [Tooltip("Seconds between it waking to watch the technician, at least and at most, while they're in its room.")]
    public Vector2 watchGap = new Vector2(14f, 32f);
    [Tooltip("Chance it wakes to watch, each time the gap's up.")]
    [Range(0f, 1f)] public float watchChance = 0.35f;

    enum Show { Off, Feed, Static, Lens, Word }

    public bool Broadcasting { get; private set; }

    private Show show = Show.Off;
    private SpriteRenderer screen, iris, pupil, scan;
    private TextMeshPro word;
    private Light2D glow;
    private AudioSource voice;
    private Transform player;
    private float showUntil, nextWatch, nextFrame;
    private int frame;
    private Color tint;

    static Sprite casing, ring, dot, lines, feedSprite;
    static Sprite[] noise;
    static AudioClip hiss, tone;

    void Awake()
    {
        All.Add(this);
        Build();
    }

    void OnDestroy() => All.Remove(this);

    void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null) player = found.transform;
        Set(rogue ? (Random.value < 0.2f ? Show.Static : Show.Off) : Show.Feed);
        nextWatch = Time.time + Random.Range(watchGap.x * 0.3f, watchGap.y);
    }

    public void BeginBroadcast()
    {
        Broadcasting = true;
        Set(Show.Static);
        Play("TV Static", hiss, 0.7f);
    }

    public void ShowWord(string text, bool hostile)
    {
        tint = hostile ? RogueColor : CalmColor;
        word.text = text;
        word.color = hostile ? new Color(1f, 0.85f, 0.8f) : new Color(0.85f, 0.97f, 1f);
        Set(Show.Word);
    }

    public void BetweenWords() => Set(Show.Static);

    public void EndBroadcast(float watchFor)
    {
        tint = rogue ? RogueColor : CalmColor;
        Set(Show.Lens);
        showUntil = Time.time + watchFor;
        Broadcasting = false;
    }

    public void PlayTone(float pitch) => Play("Sonny Word", tone, 0.8f, pitch);

    void Update()
    {
        float now = Time.time;
        if (!Broadcasting)
        {
            if ((show == Show.Lens || show == Show.Static) && showUntil > 0f && now >= showUntil)
            {
                showUntil = 0f;
                Set(rogue ? (Random.value < 0.8f ? Show.Off : Show.Static) : Show.Feed);
                if (rogue) Play("TV Static", hiss, 0.25f);
            }
            else if (rogue && show != Show.Lens && now >= nextWatch && PlayerInRoom())
            {
                nextWatch = now + Random.Range(watchGap.x, watchGap.y);
                if (Random.value < watchChance) Watch(Random.Range(3f, 6f));
            }
        }
        Animate(now);
    }

    void Watch(float seconds)
    {
        tint = RogueColor;
        Play("TV Static", hiss, 0.35f);
        Set(Show.Lens);
        showUntil = Time.time + seconds;
    }

    bool PlayerInRoom()
    {
        if (player == null) return false;
        StationMap map = StationMap.Current;
        return map != null && map.Locate(player.position, out _, out StationMap.Room here) && here.marker == room;
    }

    void Set(Show next)
    {
        show = next;
        if (next != Show.Lens && next != Show.Static) showUntil = 0f;
        bool on = next != Show.Off;
        screen.enabled = on;
        scan.enabled = on;
        iris.enabled = pupil.enabled = next == Show.Lens;
        word.gameObject.SetActive(next == Show.Word);
        glow.enabled = on;
        if (next == Show.Feed) screen.sprite = feedSprite;
    }

    void Animate(float now)
    {
        switch (show)
        {
            case Show.Static:
                if (now >= nextFrame)
                {
                    nextFrame = now + 0.05f;
                    frame = (frame + 1) % noise.Length;
                    screen.sprite = noise[frame];
                }
                screen.color = new Color(0.75f, 0.75f, 0.8f);
                glow.color = new Color(0.7f, 0.75f, 0.85f);
                glow.intensity = Random.Range(0.25f, 0.45f);
                break;
            case Show.Feed:
                screen.color = CalmColor;
                glow.color = CalmColor;
                glow.intensity = 0.35f + 0.05f * Mathf.Sin(now * 3f);
                break;
            case Show.Lens:
            {
                screen.sprite = noise[0];
                screen.color = new Color(tint.r * 0.25f, tint.g * 0.25f, tint.b * 0.25f);
                iris.color = tint;
                pupil.color = Color.Lerp(tint, Color.white, 0.5f);
                Vector2 look = player != null ? (Vector2)(player.position - transform.position) : Vector2.down;
                Vector2 toward = look.sqrMagnitude > 0.01f ? look.normalized : Vector2.down;
                pupil.transform.localPosition = ScreenCenter + new Vector3(toward.x * 2.5f, toward.y * 2f, 0f) / PixelsPerUnit;
                float breathe = 1f + 0.08f * Mathf.Sin(now * 2.2f);
                iris.transform.localScale = new Vector3(breathe, breathe, 1f);
                glow.color = tint;
                glow.intensity = 0.55f + 0.1f * Mathf.Sin(now * 2.2f);
                break;
            }
            case Show.Word:
                screen.sprite = noise[0];
                screen.color = new Color(tint.r * 0.3f, tint.g * 0.3f, tint.b * 0.3f);
                glow.color = tint;
                glow.intensity = 0.7f;
                word.transform.localPosition = ScreenCenter + (tint == RogueColor && Random.value < 0.15f
                    ? (Vector3)(Random.insideUnitCircle * 0.02f) : Vector3.zero);
                break;
        }
        if (scan.enabled) scan.color = new Color(0f, 0f, 0f, 0.3f + 0.08f * Mathf.Sin(now * 31f));
    }

    void Play(string soundName, AudioClip fallback, float volume, float pitch = 1f)
    {
        if (voice == null || player == null) return;
        Vector2 offset = transform.position - player.position;
        float near = 1f - Mathf.Clamp01(offset.magnitude / HearFrom);
        if (near <= 0f) return;
        voice.panStereo = Mathf.Clamp(offset.x / 8f, -0.8f, 0.8f);
        SoundManager.PlayOneShot(voice, soundName, volume * near, pitch, fallback);
    }

    static Vector3 ScreenCenter => new Vector3(Screen.center.x - CaseWidth * 0.5f, Screen.center.y - CaseHeight * 0.5f, 0f) / PixelsPerUnit;

    void Build()
    {
        MakeArt();
        Part("Casing", casing, 3, Color.white, false);
        screen = Part("Screen", noise[0], 4, Color.white, true);
        screen.transform.localPosition = ScreenCenter;
        iris = Part("Iris", ring, 5, RogueColor, true);
        iris.transform.localPosition = ScreenCenter;
        pupil = Part("Pupil", dot, 6, Color.white, true);
        pupil.transform.localPosition = ScreenCenter;
        scan = Part("Scanlines", lines, 7, new Color(0f, 0f, 0f, 0.35f), true);
        scan.transform.localPosition = ScreenCenter;

        word = new GameObject("Word").AddComponent<TextMeshPro>();
        word.transform.SetParent(transform, false);
        word.transform.localPosition = ScreenCenter;
        word.rectTransform.sizeDelta = new Vector2(Screen.width - 0.5f, Screen.height - 2f) / PixelsPerUnit;
        word.margin = Vector4.zero;
        word.font = GameUI.Heading;
        word.enableAutoSizing = true;
        word.fontSizeMin = 0.6f;
        word.fontSizeMax = 5f;
        word.alignment = TextAlignmentOptions.Center;
        word.textWrappingMode = TextWrappingModes.NoWrap;
        word.characterSpacing = 1f;
        word.sortingLayerID = SortingLayer.NameToID("Collision");
        word.sortingOrder = 8;
        word.gameObject.SetActive(false);

        glow = new GameObject("Glow").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = ScreenCenter + new Vector3(0f, -0.6f, 0f);
        glow.lightType = Light2D.LightType.Point;
        glow.pointLightOuterRadius = 2.6f;
        glow.pointLightInnerRadius = 0f;
        glow.falloffIntensity = 0.7f;
        glow.shadowsEnabled = false;
        glow.enabled = false;

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        if (hiss == null) hiss = Hiss();
        if (tone == null) tone = Tone();
    }

    SpriteRenderer Part(string partName, Sprite sprite, int order, Color color, bool glowing)
    {
        var part = new GameObject(partName).AddComponent<SpriteRenderer>();
        part.transform.SetParent(transform, false);
        part.sprite = sprite;
        part.sortingLayerName = "Collision";
        part.sortingOrder = order;
        part.color = color;
        if (glowing && CombatSprites.EffectMaterial != null) part.sharedMaterial = CombatSprites.EffectMaterial;
        return part;
    }

    static void MakeArt()
    {
        if (casing != null) return;
        var shell = new Color32(70, 72, 78, 255);
        var light = new Color32(104, 106, 114, 255);
        var dark = new Color32(40, 41, 46, 255);
        var outline = new Color32(18, 18, 22, 255);
        var glass = new Color32(10, 14, 16, 255);
        var knob = new Color32(150, 140, 120, 255);
        var arm = new Color32(55, 56, 60, 255);
        Texture2D body = PixelArt.MakeTexture(CaseWidth, CaseHeight, (x, y) =>
        {
            if (y == CaseHeight - 1) return x >= 9 && x <= 12 ? arm : default;
            bool edge = x == 0 || x == CaseWidth - 1 || y == 0 || y == CaseHeight - 2;
            bool corner = (x == 0 || x == CaseWidth - 1) && (y == 0 || y == CaseHeight - 2);
            if (corner) return default;
            if (edge) return outline;
            bool inScreen = x >= Screen.xMin && x < Screen.xMax && y >= Screen.yMin && y < Screen.yMax;
            bool screenCorner = (x == Screen.xMin || x == Screen.xMax - 1) && (y == Screen.yMin || y == Screen.yMax - 1);
            if (inScreen && !screenCorner) return glass;
            if (inScreen) return outline;
            if (x == 20 && (y == 4 || y == 7)) return knob;
            if (x >= 19 && x <= 21 && y >= 9 && y <= 12 && (x + y) % 2 == 0) return dark;
            return y >= CaseHeight - 4 ? light : y <= 1 ? dark : shell;
        });
        casing = PixelArt.ToSprite(body, PixelsPerUnit, new Vector2(0.5f, 0.5f));

        noise = new Sprite[4];
        for (int i = 0; i < noise.Length; i++)
        {
            var random = new System.Random(31 + i);
            Texture2D snow = PixelArt.MakeTexture(Screen.width, Screen.height, (x, y) =>
            {
                bool corner = (x == 0 || x == Screen.width - 1) && (y == 0 || y == Screen.height - 1);
                if (corner) return default;
                byte v = (byte)random.Next(40, 255);
                return new Color32(v, v, v, 255);
            });
            noise[i] = PixelArt.ToSprite(snow, PixelsPerUnit, new Vector2(0.5f, 0.5f));
        }

        Texture2D feed = PixelArt.MakeTexture(Screen.width, Screen.height, (x, y) =>
        {
            bool corner = (x == 0 || x == Screen.width - 1) && (y == 0 || y == Screen.height - 1);
            if (corner) return default;
            byte bg = 40;
            bool text = y >= 5 && y % 2 == 1 && x >= 1 && x <= 7 && (x * 7 + y * 3) % 5 != 0;
            bool bar = y <= 3 && y >= 1 && x >= 1 && x < 11 && y <= 1 + (x * 5 % 3);
            bool mark = x >= 9 && x <= 10 && y >= 6 && y <= 7;
            byte v = text || bar || mark ? (byte)230 : bg;
            return new Color32(v, v, v, 255);
        });
        feedSprite = PixelArt.ToSprite(feed, PixelsPerUnit, new Vector2(0.5f, 0.5f));

        Texture2D iris = PixelArt.MakeTexture(9, 9, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(4f, 4f));
            return d <= 4.2f && d >= 2.9f ? (Color32)Color.white : default;
        });
        ring = PixelArt.ToSprite(iris, PixelsPerUnit, new Vector2(0.5f, 0.5f));
        Texture2D point = PixelArt.MakeTexture(3, 3, (x, y) => (Color32)Color.white);
        dot = PixelArt.ToSprite(point, PixelsPerUnit, new Vector2(0.5f, 0.5f));
        Texture2D rows = PixelArt.MakeTexture(Screen.width, Screen.height, (x, y) =>
        {
            bool corner = (x == 0 || x == Screen.width - 1) && (y == 0 || y == Screen.height - 1);
            return y % 2 == 0 && !corner ? (Color32)Color.white : default;
        });
        lines = PixelArt.ToSprite(rows, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    static AudioClip Hiss()
    {
        int samples = Mathf.CeilToInt(0.3f * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(12);
        for (int i = 0; i < samples; i++)
        {
            float p = i / (float)samples;
            data[i] = ((float)random.NextDouble() * 2f - 1f) * (1f - p) * 0.35f;
        }
        return Clip("TV Static", data);
    }

    static AudioClip Tone()
    {
        int samples = Mathf.CeilToInt(0.16f * TitleUI.SampleRate);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            float p = i / (float)samples;
            float f = p < 0.45f ? 330f : 247f;
            float wave = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * f * t) * 3f, -1f, 1f);
            float envelope = Mathf.Clamp01(p * 40f) * Mathf.Clamp01((1f - p) * 8f);
            data[i] = wave * envelope * 0.22f;
        }
        return Clip("Sonny Word", data);
    }

    static AudioClip Clip(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
