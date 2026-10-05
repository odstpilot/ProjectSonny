using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Starting a Generator: bring its coils up one at a time. A needle sweeps back and forth across the rotor gauge, and
// there's a green sync window somewhere along it; engage (Space, E, Enter, or a click) while the needle's in the window
// and that coil comes up, the generator's hum climbing. Each coil after is quicker and its window narrower. Engage out of
// sync and it backfires (Generator.Backfire): a bang the bots hear, and the last coil up drops again. Every coil up, and
// it starts. Esc leaves, keeping the coils that are up. The world keeps running meanwhile, and getting hurt knocks the
// player off it.
// Built from code the first time a generator needs it; every generator shares it.
public class GeneratorScreen : MonoBehaviour
{
    const int SortingOrder = 110;           // over the HUD and prompts, under the death screen
    const float GaugeWidth = 900f;
    const float GaugeHeight = 70f;
    const float ArmTime = 0.6f;             // seconds before the needle moves, on opening and after each coil
    const float AfterBackfire = 0.9f;
    const float IgnitionHold = 1.4f;
    const int SampleRate = 44100;

    static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.055f, 0.97f);
    static readonly Color PanelEdge = new Color(0.18f, 0.24f, 0.2f);
    static readonly Color GaugeColor = new Color(0.025f, 0.03f, 0.028f);
    static readonly Color WindowColor = new Color(0.35f, 0.95f, 0.5f, 0.55f);
    static readonly Color NeedleColor = new Color(1f, 0.95f, 0.8f);
    static readonly Color CoilOff = new Color(0.12f, 0.13f, 0.12f);
    static readonly Color CoilOn = new Color(1f, 0.68f, 0.3f);
    static readonly Color CoilRunning = new Color(0.5f, 0.95f, 0.6f);

    enum Phase { Closed, Arming, Sweeping, Result }

    static GeneratorScreen instance;

    public static GeneratorScreen Current => instance;
    public static bool AnyOpen => instance != null && instance.IsOpen;

    public bool IsOpen { get; private set; }
    public Generator Generator { get; private set; }
    // Where the needle is, 0 to 1 across the gauge, and the sync window's middle. For tests and debugging.
    public float Needle => needle;
    public float WindowCenter => windowCenter;

    private Phase phase = Phase.Closed;
    private List<Behaviour> frozenControls;
    private Health playerHealth;
    private int openedFrame;
    private float needle;
    private float needleDirection = 1f;
    private float windowCenter;
    private float windowWidth;
    private float phaseUntil;
    private float shakeUntil;

    private bool built;
    private TMP_FontAsset font;
    private CanvasGroup device;
    private RectTransform panel;
    private TextMeshProUGUI header;
    private TextMeshProUGUI status;
    private TextMeshProUGUI result;
    private RectTransform syncWindow;
    private RectTransform needleRect;
    private Image[] coilImages = new Image[0];
    private TextMeshProUGUI[] coilLabels = new TextMeshProUGUI[0];
    private RectTransform coilRow;
    private AudioSource hum;
    private AudioSource sfx;
    private AudioClip tickClip, engageClip, bangClip, ignitionClip;

    public static GeneratorScreen Get(Font screenFont = null)
    {
        if (instance == null)
        {
            instance = new GameObject("GeneratorScreen", typeof(RectTransform)).AddComponent<GeneratorScreen>();
            instance.Build(screenFont);
        }
        return instance;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Open(Generator generator, Transform player)
    {
        if (IsOpen || generator == null || player == null) return;
        Generator = generator;
        IsOpen = true;
        openedFrame = Time.frameCount;
        frozenControls = PlayerControls.Freeze(player);
        playerHealth = player.GetComponent<Health>();
        if (playerHealth != null) playerHealth.Damaged += OnPlayerHurt;

        header.text = "POWER PLANT  //  " + generator.displayName;
        BuildCoils(generator.coils);
        device.alpha = 1f;
        result.text = "";
        hum.Play();
        Arm();
    }

    public void Close()
    {
        if (!IsOpen) return;
        StopAllCoroutines();
        IsOpen = false;
        phase = Phase.Closed;
        hum.Stop();
        if (playerHealth != null) playerHealth.Damaged -= OnPlayerHurt;
        PlayerControls.Release(frozenControls);
        device.alpha = 0f;
        panel.anchoredPosition = Vector2.zero;
        Generator = null;
    }

    void OnPlayerHurt(DamageInfo damage) => Close();

    // Engages now, wherever the needle is. The player normally presses Space; this is for scripts and tests.
    public void Engage()
    {
        if (phase != Phase.Sweeping) return;
        if (Mathf.Abs(needle - windowCenter) <= windowWidth * 0.5f) Synced();
        else StartCoroutine(Backfired());
    }

    void Update()
    {
        if (!IsOpen || Time.timeScale == 0f) return;
        if (Time.frameCount != openedFrame)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(Terminal.InteractKey) || Input.GetKeyDown(KeyCode.Return)
                || Input.GetMouseButtonDown(0))
                Engage();
        }

        if (phase == Phase.Arming && Time.time >= phaseUntil)
        {
            phase = Phase.Sweeping;
            status.text = $"COIL {Generator.CoilsLit + 1} OF {Generator.coils}  //  ENGAGE IN SYNC";
        }
        if (phase == Phase.Sweeping) Sweep();

        needleRect.anchoredPosition = new Vector2((needle - 0.5f) * GaugeWidth, 0f);
        float lit = Generator.CoilsLit / (float)Mathf.Max(1, Generator.coils);
        hum.pitch = Mathf.Lerp(0.55f, 1.25f, lit) + (phase == Phase.Sweeping ? Mathf.Sin(Time.time * 20f) * 0.01f : 0f);
        hum.volume = Mathf.Lerp(0.12f, 0.3f, lit);
        panel.anchoredPosition = Time.time < shakeUntil ? Random.insideUnitCircle * 10f : Vector2.zero;
        UpdateCoils();
    }

    // Back and forth across the gauge, ticking at each end.
    void Sweep()
    {
        needle += needleDirection * Generator.SpeedFor(Generator.CoilsLit) * Time.deltaTime;
        if (needle > 1f || needle < 0f)
        {
            needle = Mathf.Clamp01(needle);
            needleDirection = -needleDirection;
            sfx.PlayOneShot(tickClip, 0.5f);
        }
    }

    // A fresh sync window for the coil coming up, somewhere the needle isn't, and a moment before it moves.
    void Arm()
    {
        int coil = Generator.CoilsLit;
        windowWidth = Generator.WindowFor(coil);
        float half = windowWidth * 0.5f;
        do windowCenter = Random.Range(half + 0.05f, 1f - half - 0.05f);
        while (Mathf.Abs(windowCenter - needle) < 0.25f && Random.value < 0.9f);
        syncWindow.sizeDelta = new Vector2(windowWidth * GaugeWidth, GaugeHeight);
        syncWindow.anchoredPosition = new Vector2((windowCenter - 0.5f) * GaugeWidth, 0f);
        status.text = "ARMING...";
        phase = Phase.Arming;
        phaseUntil = Time.time + ArmTime;
    }

    void Synced()
    {
        Generator.CoilsLit++;
        sfx.PlayOneShot(engageClip, 0.8f);
        if (Generator.CoilsLit >= Generator.coils) StartCoroutine(Ignition());
        else
        {
            ShowResult("SYNCED", CoilOn);
            Arm();
        }
    }

    IEnumerator Backfired()
    {
        phase = Phase.Result;
        Generator.Backfire();
        sfx.PlayOneShot(bangClip, 1f);
        shakeUntil = Time.time + 0.35f;
        ShowResult("BACKFIRE", GameUI.Red);
        status.text = "OUT OF SYNC";
        for (float t = 0f; t < AfterBackfire; t += Time.deltaTime) yield return null;
        if (IsOpen) Arm();
    }

    IEnumerator Ignition()
    {
        phase = Phase.Result;
        sfx.PlayOneShot(ignitionClip, 0.9f);
        ShowResult("IGNITION", CoilRunning);
        status.text = "ALL COILS UP";
        for (float t = 0f; t < IgnitionHold; t += Time.deltaTime)
        {
            hum.pitch = Mathf.Lerp(1.25f, 1.6f, t / IgnitionHold);
            yield return null;
        }
        Generator generator = Generator;
        Close();
        generator.MarkStarted();
    }

    void ShowResult(string text, Color color)
    {
        result.text = text;
        result.color = color;
        StartCoroutine(FadeResult());
    }

    IEnumerator FadeResult()
    {
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            result.alpha = 1f - Mathf.Clamp01((t - 0.4f) / 0.4f);
            yield return null;
        }
        result.alpha = 0f;
    }

    void UpdateCoils()
    {
        for (int i = 0; i < coilImages.Length; i++)
        {
            bool on = i < Generator.CoilsLit;
            bool next = i == Generator.CoilsLit && phase == Phase.Sweeping;
            Color color = Generator.CoilsLit >= Generator.coils ? CoilRunning : on ? CoilOn : CoilOff;
            if (next) color = Color.Lerp(CoilOff, CoilOn, 0.2f + 0.15f * Mathf.Sin(Time.time * 8f));
            coilImages[i].color = color;
            coilLabels[i].color = on ? GameUI.Ink : GameUI.Dim;
        }
    }

    // --- Building ---

    void Build(Font screenFont)
    {
        if (built) return;
        built = true;
        if (screenFont != null) font = TMP_FontAsset.CreateFontAsset(screenFont);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = GameUI.Fill(GameUI.NewRect("Device", transform));
        device = root.gameObject.AddComponent<CanvasGroup>();
        device.alpha = 0f;
        device.blocksRaycasts = false;
        device.interactable = false;
        GameUI.Fill(Block("Backdrop", root, new Color(0f, 0f, 0f, 0.6f)).rectTransform);

        Image body = Block("Panel", root, PanelColor);
        panel = body.rectTransform;
        At(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 560f));
        var edge = body.gameObject.AddComponent<Outline>();
        edge.effectColor = PanelEdge;
        edge.effectDistance = new Vector2(5f, -5f);

        header = Label("Header", panel, "", 26f, GameUI.Dim, TextAlignmentOptions.TopLeft);
        At(header.rectTransform, new Vector2(0f, 1f), new Vector2(50f + 400f, -46f), new Vector2(800f, 40f));
        TextMeshProUGUI leave = Label("Leave", panel, "ESC  LEAVE", 26f, GameUI.Dim, TextAlignmentOptions.TopRight);
        At(leave.rectTransform, new Vector2(1f, 1f), new Vector2(-200f, -46f), new Vector2(300f, 40f));

        status = Label("Status", panel, "", 34f, GameUI.Amber, TextAlignmentOptions.Center);
        At(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1000f, 50f));

        // The rotor gauge: tick marks, the sync window, and the needle.
        Image gauge = Block("Gauge", panel, GaugeColor);
        At(gauge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(GaugeWidth, GaugeHeight));
        var gaugeEdge = gauge.gameObject.AddComponent<Outline>();
        gaugeEdge.effectColor = new Color(0.3f, 0.4f, 0.32f, 0.6f);
        gaugeEdge.effectDistance = new Vector2(3f, -3f);
        for (int i = 0; i <= 20; i++)
        {
            Image tick = Block($"Tick {i}", gauge.rectTransform, new Color(0.5f, 0.6f, 0.5f, i % 5 == 0 ? 0.5f : 0.2f));
            At(tick.rectTransform, new Vector2(0.5f, 0f), new Vector2((i / 20f - 0.5f) * GaugeWidth, i % 5 == 0 ? 10f : 6f),
                new Vector2(3f, i % 5 == 0 ? 20f : 12f));
        }
        syncWindow = Block("Sync Window", gauge.rectTransform, WindowColor).rectTransform;
        At(syncWindow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, GaugeHeight));
        needleRect = Block("Needle", gauge.rectTransform, NeedleColor).rectTransform;
        At(needleRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, GaugeHeight + 30f));

        result = Label("Result", panel, "", 54f, CoilOn, TextAlignmentOptions.Center);
        At(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(800f, 70f));

        coilRow = GameUI.NewRect("Coils", panel);
        At(coilRow, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 60f));

        TextMeshProUGUI hint = Label("Hint", panel, "SPACE / E / CLICK  ENGAGE WHEN THE NEEDLE'S IN THE GREEN", 24f, GameUI.Dim,
            TextAlignmentOptions.Center);
        At(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1000f, 40f));

        hum = gameObject.AddComponent<AudioSource>();
        hum.playOnAwake = false;
        hum.loop = true;
        hum.spatialBlend = 0f;
        hum.clip = Hum();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        tickClip = Noise("Gauge Tick", 0.015f, 0.3f);
        bangClip = Noise("Backfire", 0.45f, 0.9f);
        engageClip = Tone("Coil Up", 0.22f, 440f, 660f);
        ignitionClip = Tone("Ignition", 0.6f, 330f, 990f);
    }

    // A box per coil, in a row, made again when a generator with a different number opens it.
    void BuildCoils(int count)
    {
        if (coilImages.Length == count) return;
        foreach (Transform child in coilRow) Destroy(child.gameObject);
        coilImages = new Image[count];
        coilLabels = new TextMeshProUGUI[count];
        const float Width = 160f, Gap = 30f;
        float start = -(count * Width + (count - 1) * Gap) * 0.5f + Width * 0.5f;
        for (int i = 0; i < count; i++)
        {
            Image box = Block($"Coil {i + 1}", coilRow, CoilOff);
            At(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(start + i * (Width + Gap), 0f), new Vector2(Width, 50f));
            coilImages[i] = box;
            coilLabels[i] = Label("Label", box.rectTransform, $"COIL {i + 1}", 26f, GameUI.Dim, TextAlignmentOptions.Center);
            GameUI.Fill(coilLabels[i].rectTransform);
        }
    }

    static void At(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static Image Block(string blockName, Transform parent, Color color)
    {
        var image = GameUI.NewRect(blockName, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    TextMeshProUGUI Label(string labelName, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = GameUI.Label(labelName, parent, text, size, color, alignment, font);
        return label;
    }

    // --- Sounds, made in code ---

    // A low, buzzing drone, looped; its pitch rises as the coils come up.
    static AudioClip Hum()
    {
        const float frequency = 55f;
        int samples = Mathf.RoundToInt(SampleRate / frequency) * 40;    // a whole number of cycles, so it loops cleanly
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float phase = i * frequency / SampleRate;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            data[i] = (saw * 0.5f + Mathf.Sin(phase * 2f * Mathf.PI * 2f) * 0.3f) * 0.5f;
        }
        AudioClip clip = AudioClip.Create("Generator Hum", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Noise(string clipName, float seconds, float loudness)
    {
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        float low = 0f;
        for (int i = 0; i < samples; i++)
        {
            float fade = 1f - i / (float)samples;
            low = Mathf.Lerp(low, Random.value * 2f - 1f, 0.25f);      // a little muffled, so it booms
            data[i] = low * loudness * fade * fade;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A tone sliding from one pitch to another.
    static AudioClip Tone(string clipName, float seconds, float from, float to)
    {
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)SampleRate;
            phase += 2f * Mathf.PI * Mathf.Lerp(from, to, time / seconds) / SampleRate;
            float envelope = Mathf.Clamp01(time / 0.01f) * Mathf.Clamp01((seconds - time) / 0.08f);
            data[i] = Mathf.Sin(phase) * 0.4f * envelope;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
