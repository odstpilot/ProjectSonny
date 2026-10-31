using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// chapter 1 opening: pod docking
public class ArrivalCutscene : MonoBehaviour
{
    const int SortingOrder = 115;
    const float StationScale = 3f;
    const float PodScale = 3f;
    const float LetterboxHeight = 120f;
    const float FadeInEnd = 1.8f;
    const float ZoomStart = 4.5f;
    const float DockTime = 15.2f;
    const float FadeOutStart = 17.6f;
    const float FadeOutTime = 1.4f;
    const float EndZoom = 2.5f;
    const float LettersPerSecond = 38f;

    static readonly Vector2 PodStart = new Vector2(-1180f, 340f);
    static readonly Vector2 StationAt = new Vector2(380f, 30f);
    static readonly Color SunColor = new Color(1f, 0.72f, 0.38f);
    static readonly Color LogColor = new Color(0.55f, 0.88f, 0.95f);

    static readonly (float time, string text)[] Log =
    {
        (1.6f, "SUPPLY POD 7  //  PASSENGERS: 1"),
        (5.8f, "SOLAR FARMING STATION  //  0.06 AU FROM THE SUN"),
        (10.2f, "DOCKING PORT A  //  CLEAR TO APPROACH"),
        (15.5f, "CLAMPS ENGAGED. WELCOME ABOARD."),
    };

    static readonly string[] CollarArt =
    {
        "#####",
        "#ooo#",
        "##oo#",
        " #oo#",
        " #dd#",
        " #dd#",
        " #dd#",
        " #dd#",
        " #oo#",
        "##oo#",
        "#ooo#",
        "#####",
    };
    const int CollarLeft = 33, CollarBottom = 68;

    static readonly (int x, int y, bool red)[] Beacons = { (55, 170, true), (2, 100, false), (108, 100, false), (55, 1, true), (18, 157, false), (92, 157, false) };

    private RectTransform sky;
    private RectTransform world;
    private RectTransform pod;
    private RawImage engine;
    private RawImage retro;
    private RawImage beam;
    private readonly List<Image> portLights = new List<Image>();
    private readonly List<(RawImage image, float period, float phase, Color color)> beacons = new List<(RawImage, float, float, Color)>();
    private TextMeshProUGUI log;
    private Image black;
    private AudioSource rumble;
    private AudioSource effects;
    private AudioClip rumbleClip;
    private AudioClip clunk;
    private AudioClip hiss;
    private Vector2 dock;

    void OnDestroy() => Playing = false;

    public static bool Playing { get; private set; }

    public static IEnumerator Play()
    {
        Playing = true;
        var cutscene = new GameObject("Arrival Cutscene", typeof(RectTransform)).AddComponent<ArrivalCutscene>();
        cutscene.Build();
        yield return cutscene.Run();
        Destroy(cutscene.gameObject);
        Playing = false;
    }

    IEnumerator Run()
    {
        float end = FadeOutStart + FadeOutTime;
        bool docked = false;
        int logLine = -1;
        float fadeFrom = 0f;
        float fadeOutAt = FadeOutStart;
        SoundManager.Setup(rumble, "Pod Rumble", rumbleClip);
        rumble.loop = true;
        rumble.volume = 0f;
        rumble.Play();

        for (float t = 0f; t < end; t += Time.unscaledDeltaTime)
        {
            if (t < fadeOutAt && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
            {
                fadeOutAt = t;
                fadeFrom = black.color.a;
                end = t + 0.6f;
            }

            float podProgress = TitleUI.Phase(t, 1f, DockTime);
            UpdatePod(podProgress, t);
            UpdateView(t);
            UpdateLights(t, docked);

            if (!docked && t >= DockTime)
            {
                docked = true;
                SoundManager.PlayOneShot(effects, "Docking Clunk", fallback: clunk);
                SoundManager.PlayOneShot(effects, "Docking Hiss", fallback: hiss);
            }

            while (logLine + 1 < Log.Length && t >= Log[logLine + 1].time) logLine++;
            if (logLine >= 0)
            {
                string text = "> " + Log[logLine].text;
                int shown = Mathf.FloorToInt((t - Log[logLine].time) * LettersPerSecond) + 2;
                bool cursor = Mathf.Repeat(t, 0.8f) < 0.45f;
                log.text = shown < text.Length ? text.Substring(0, shown) + "_" : text + (cursor ? "_" : "");
            }

            float fade = t >= fadeOutAt ? Mathf.Lerp(fadeFrom, 1f, TitleUI.Phase(t, fadeOutAt, end))
                       : t < FadeInEnd ? 1f - TitleUI.EaseOut(t / FadeInEnd)
                       : 0f;
            black.color = new Color(0f, 0f, 0f, fade);
            rumble.volume = RumbleVolume(podProgress) * (1f - fade) * SoundManager.Volume("Pod Rumble");
            yield return null;
        }
        black.color = Color.black;
        rumble.Stop();
    }

    void UpdatePod(float progress, float t)
    {
        float along = 1f - Mathf.Pow(1f - progress, 2.2f);
        Vector2 end = dock - new Vector2(7f * PodScale, 0f);
        Vector2 bend = new Vector2(end.x - 520f, end.y + 10f);
        Vector2 at = Bezier(PodStart, bend, end, along);
        Vector2 heading = Bezier(PodStart, bend, end, Mathf.Min(along + 0.01f, 1f)) - at;
        pod.anchoredPosition = at;
        if (heading.sqrMagnitude > 0.001f) pod.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);

        float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(t * 18f, 0.3f);
        float thrust = progress < 0.7f ? 1f - progress * 0.6f : Mathf.Clamp01(1f - (progress - 0.7f) / 0.08f) * 0.6f;
        engine.color = TitleUI.Fade(new Color(1f, 0.6f, 0.28f), thrust * flicker);
        engine.rectTransform.localScale = new Vector3(0.6f + thrust * 0.6f, 1f, 1f);
        float braking = TitleUI.Smooth(0.72f, 0.78f, progress) * (1f - TitleUI.Smooth(0.93f, 0.99f, progress));
        retro.color = TitleUI.Fade(new Color(0.75f, 0.95f, 1f), braking * (0.6f + 0.4f * Mathf.PerlinNoise(t * 25f, 4.1f)));
    }

    static float RumbleVolume(float progress) => progress >= 1f ? 0f : 0.35f * (1f - progress * 0.7f);

    void UpdateView(float t)
    {
        float zoom = TitleUI.EaseInOut(TitleUI.Phase(t, ZoomStart, DockTime + 0.3f));
        float scale = Mathf.Lerp(1f, EndZoom, zoom);
        Vector2 focus = Vector2.Lerp(Vector2.zero, dock + new Vector2(-40f, 0f), zoom);
        Vector2 shake = Vector2.zero;
        float sinceDock = t - DockTime;
        if (sinceDock > 0f && sinceDock < 0.5f)
            shake = new Vector2(Mathf.PerlinNoise(t * 40f, 1f) - 0.5f, Mathf.PerlinNoise(2f, t * 40f) - 0.5f) * 22f * (1f - sinceDock / 0.5f);

        world.localScale = new Vector3(scale, scale, 1f);
        world.anchoredPosition = -focus * scale + shake;
        sky.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, zoom);
        sky.anchoredPosition = new Vector2(-t * 6f, 0f) - focus * 0.08f + shake * 0.3f;
    }

    void UpdateLights(float t, bool docked)
    {
        foreach (var beacon in beacons)
        {
            bool on = Mathf.Repeat(t + beacon.phase, beacon.period) < beacon.period * 0.25f;
            beacon.image.color = TitleUI.Fade(beacon.color, on ? 0.9f : 0f);
        }

        bool blink = Mathf.Repeat(t, 0.6f) < 0.3f;
        foreach (Image light in portLights)
            light.color = docked ? GameUI.Green : blink ? GameUI.Amber : new Color(0.3f, 0.2f, 0.1f);

        beam.color = TitleUI.Fade(GameUI.Cyan, 0.22f * (1f - TitleUI.Phase(t, DockTime - 1f, DockTime)) * TitleUI.Phase(t, 8f, 10f));
    }

    static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) => Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, c, t), t);

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        Image space = GameUI.Fill(GameUI.NewRect("Space", transform)).gameObject.AddComponent<Image>();
        space.color = new Color(0.01f, 0.015f, 0.025f);
        space.raycastTarget = false;

        sky = TitleUI.Place(GameUI.NewRect("Sky", transform), Vector2.zero, GameUI.ReferenceResolution);
        RawImage nebula = TitleUI.Picture("Nebula", sky, TitleArt.NebulaTexture(8.3f, false), Vector2.zero, new Vector2(2400f, 1350f));
        nebula.color = new Color(1f, 1f, 1f, 0.8f);
        TitleUI.Picture("Stars", sky, TitleArt.StarfieldTexture(), new Vector2(60f, 0f), new Vector2(2100f, 1220f));
        RawImage wisps = TitleUI.Picture("Wisps", sky, TitleArt.NebulaTexture(21.7f, true), new Vector2(-200f, -80f), new Vector2(2500f, 1400f));
        wisps.color = new Color(1f, 1f, 1f, 0.6f);

        TitleUI.Picture("Sun Glow", sky, CombatSprites.SoftCircleTexture, new Vector2(1180f, 220f), new Vector2(2300f, 2300f), glow: true).color = TitleUI.Fade(SunColor, 0.5f);
        TitleUI.Picture("Sun", sky, CombatSprites.SoftCircleTexture, new Vector2(1060f, 250f), new Vector2(760f, 760f), glow: true).color = TitleUI.Fade(new Color(1f, 0.93f, 0.8f), 0.95f);
        TitleUI.Picture("Glint", sky, TitleUI.SparkleTexture, new Vector2(930f, 260f), new Vector2(260f, 260f), glow: true).color = TitleUI.Fade(Color.white, 0.8f);

        world = TitleUI.Place(GameUI.NewRect("World", transform), Vector2.zero, GameUI.ReferenceResolution);
        BuildStation();
        BuildPod();

        Bar("Letterbox Top", 1f);
        RectTransform bottom = Bar("Letterbox Bottom", 0f);
        log = GameUI.Label("Log", bottom, "", 32f, LogColor, TextAlignmentOptions.Left);
        GameUI.Anchor(log.rectTransform, 0f, 0f, 1f, 1f, 90f, 0f, 400f, 0f);
        log.characterSpacing = 4f;
        TextMeshProUGUI skip = GameUI.Label("Skip", bottom, "SPACE  SKIP", 16f, TitleUI.Fade(GameUI.Dim, 0.7f), TextAlignmentOptions.Right);
        GameUI.Anchor(skip.rectTransform, 0f, 0f, 1f, 1f, 0f, 0f, 90f, 0f);
        skip.characterSpacing = 6f;

        black = GameUI.Fill(GameUI.NewRect("Fade", transform)).gameObject.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        rumble = gameObject.AddComponent<AudioSource>();
        rumbleClip = Rumble();
        effects = gameObject.AddComponent<AudioSource>();
        clunk = Clunk();
        hiss = TitleUI.Crackle("Hiss", 1.2f, 0.25f);
    }

    void BuildStation()
    {
        Texture2D texture = TitleArt.StationTexture();
        RawImage station = TitleUI.Picture("Station", world, texture, StationAt, new Vector2(texture.width, texture.height) * StationScale);
        Vector2 Pixel(float x, float y) => StationAt + new Vector2(x - texture.width * 0.5f, y - texture.height * 0.5f) * StationScale;

        var palette = new Dictionary<char, Color32>
        {
            { '#', new Color32(18, 32, 58, 255) },
            { 'o', new Color32(175, 200, 225, 255) },
            { 'd', new Color32(40, 58, 88, 255) },
        };
        Sprite collarSprite = PixelArt.FromText(CollarArt, palette, 100f, new Vector2(0.5f, 0.5f));
        int collarWidth = CollarArt[0].Length, collarHeight = CollarArt.Length;
        Image collar = GameUI.NewRect("Docking Port", world).gameObject.AddComponent<Image>();
        collar.sprite = collarSprite;
        collar.raycastTarget = false;
        TitleUI.Place(collar.rectTransform, Pixel(CollarLeft + collarWidth * 0.5f, CollarBottom + collarHeight * 0.5f),
                      new Vector2(collarWidth, collarHeight) * StationScale);
        dock = Pixel(CollarLeft, CollarBottom + collarHeight * 0.5f);

        foreach (float y in new[] { CollarBottom + collarHeight - 1.5f, CollarBottom + 1.5f })
        {
            Image light = TitleUI.Place(GameUI.NewRect("Port Light", world), Pixel(CollarLeft - 0.5f, y), Vector2.one * StationScale * 1.4f).gameObject.AddComponent<Image>();
            light.raycastTarget = false;
            portLights.Add(light);
        }

        beam = TitleUI.Picture("Guide Beam", world, TitleUI.StreakTexture, dock + new Vector2(-260f, 0f), new Vector2(520f, 22f), glow: true);
        beam.uvRect = new Rect(0f, 0f, 1f, 1f);

        foreach (var (x, y, red) in Beacons)
            beacons.Add((TitleUI.Picture("Beacon", world, CombatSprites.SoftCircleTexture, Pixel(x + 0.5f, y + 0.5f), new Vector2(26f, 26f), glow: true),
                         Random.Range(1.4f, 3f), Random.Range(0f, 3f), red ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 1f, 1f)));
        station.transform.SetSiblingIndex(0);
    }

    void BuildPod()
    {
        pod = TitleUI.Place(GameUI.NewRect("Pod", world), PodStart, new Vector2(14f, 9f) * PodScale);
        engine = TitleUI.Picture("Engine", pod, CombatSprites.SoftCircleTexture, Vector2.zero, new Vector2(46f, 24f), glow: true);
        engine.rectTransform.pivot = new Vector2(0.75f, 0.5f);
        engine.rectTransform.anchoredPosition = new Vector2(-6f * PodScale, 0f);
        retro = TitleUI.Picture("Retros", pod, CombatSprites.SoftCircleTexture, new Vector2(8f * PodScale, 0f), new Vector2(26f, 30f), glow: true);
        Image hull = GameUI.Fill(GameUI.NewRect("Hull", pod)).gameObject.AddComponent<Image>();
        hull.sprite = TitleArt.ShipSprite();
        hull.raycastTarget = false;
    }

    RectTransform Bar(string barName, float edge)
    {
        RectTransform bar = GameUI.NewRect(barName, transform);
        bar.anchorMin = new Vector2(0f, edge);
        bar.anchorMax = new Vector2(1f, edge);
        bar.pivot = new Vector2(0.5f, edge);
        bar.sizeDelta = new Vector2(0f, LetterboxHeight);
        Image image = bar.gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return bar;
    }

    static AudioClip Rumble()
    {
        int samples = TitleUI.SampleRate * 2;
        var data = new float[samples];
        var random = new System.Random(5);
        float low = 0f, lower = 0f;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < samples; i++)
            {
                low += ((float)random.NextDouble() * 2f - 1f - low) * 0.02f;
                lower += (low - lower) * 0.05f;
                if (pass == 1) data[i] = lower * 6f;
            }
        }
        AudioClip clip = AudioClip.Create("Pod Rumble", samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Clunk()
    {
        int samples = Mathf.CeilToInt(0.7f * TitleUI.SampleRate);
        var data = new float[samples];
        var random = new System.Random(9);
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)TitleUI.SampleRate;
            float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 45f, time / 0.7f) * time) * Mathf.Exp(-time * 7f);
            float knock = Mathf.Sin(2f * Mathf.PI * 410f * time) * Mathf.Exp(-time * 30f) * 0.35f;
            float grit = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-time * 40f) * 0.3f;
            data[i] = (thud + knock + grit) * 0.8f;
        }
        AudioClip clip = AudioClip.Create("Docking Clunk", samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
