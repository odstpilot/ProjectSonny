using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sonny's monitor, shown when the player dies. Black glass with scanlines and a faint grid; a life-sign trace sweeps
// across, beats twice (beeping), and flatlines into a long tone; then a log entry types out underneath, ending with a
// line from Sonny. Waits for any key, then Hide cuts back through a burst of static.
// Built from code the first time it's needed, beeps and tone included, so there's nothing to set up. PlayerHealthHandler
// runs it; Show and Hide use real time, so it works with the game paused.
public class DeathScreen : MonoBehaviour
{
    const int SortingOrder = 120;           // over the HUD, the tutorial's text, and the view from a locker
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const int TraceWidth = 320;
    const int TraceHeight = 90;
    const float SweepSeconds = 2.4f;        // for the trace to cross the monitor once
    const int GapColumns = 14;              // blank columns ahead of the trace, like a real monitor's sweep
    const float LettersPerSecond = 40f;
    const int SampleRate = 44100;

    static readonly Color32 TraceBright = new Color32(255, 100, 76, 255);
    static readonly Color32 TraceGlow = new Color32(255, 70, 50, 90);
    static readonly Color32 GridLine = new Color32(255, 80, 60, 28);
    static readonly Color32 ScanLine = new Color32(0, 0, 0, 110);
    static readonly Color HeaderColor = new Color(1f, 0.36f, 0.28f);
    static readonly Color LineColor = new Color(0.96f, 0.8f, 0.72f);
    static readonly Color DimColor = new Color(0.62f, 0.34f, 0.28f);

    // The last two heartbeats, the second one weaker, in seconds from the start.
    static readonly float[] BeatTimes = { 0.35f, 1.15f };
    static readonly float[] BeatStrengths = { 1f, 0.55f };

    static DeathScreen instance;

    public bool IsShowing { get; private set; }
    public bool WaitingForKey { get; private set; }
    public bool ContinuePressed { get; private set; }

    private CanvasGroup group;
    private RawImage noise;
    private Texture2D traceTexture;
    private Color32[] tracePixels;
    private TextMeshProUGUI header;
    private TextMeshProUGUI subject;
    private TextMeshProUGUI quote;
    private TextMeshProUGUI prompt;
    private AudioSource beeper;
    private AudioSource tone;
    private AudioClip beepClip;
    private TMP_FontAsset font;
    private Coroutine traceRoutine;

    public static DeathScreen Get(Font monitorFont = null)
    {
        if (instance == null)
        {
            instance = new GameObject("DeathScreen", typeof(RectTransform)).AddComponent<DeathScreen>();
            instance.Build(monitorFont);
        }
        return instance;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Cuts to the monitor and plays it out. Returns once it's waiting for a key: then watch ContinuePressed.
    public IEnumerator Show(string subjectText, string sonnyLine)
    {
        IsShowing = true;
        WaitingForKey = false;
        ContinuePressed = false;
        group.alpha = 1f;
        noise.enabled = false;
        header.text = subject.text = quote.text = prompt.text = "";

        ClearTrace();
        if (traceRoutine != null) StopCoroutine(traceRoutine);
        traceRoutine = StartCoroutine(DrawTrace());

        yield return WaitReal(1.9f);
        yield return TypeOut(header, "LIFE SIGNS LOST");
        yield return WaitReal(0.3f);
        yield return TypeOut(subject, subjectText);
        if (!string.IsNullOrEmpty(sonnyLine))
        {
            yield return WaitReal(0.7f);
            yield return TypeOut(quote, "SONNY  >  " + sonnyLine);
        }
        yield return WaitReal(0.8f);

        prompt.text = "PRESS ANY KEY TO RESTORE SIGNAL";
        WaitingForKey = true;
    }

    // What a key press does. Scripts can call it too.
    public void Continue()
    {
        if (!IsShowing) return;
        WaitingForKey = false;
        ContinuePressed = true;
    }

    // A burst of static, then the game fades back up underneath.
    public IEnumerator Hide()
    {
        WaitingForKey = false;
        if (traceRoutine != null) StopCoroutine(traceRoutine);
        traceRoutine = null;
        tone.Stop();

        noise.enabled = true;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            noise.uvRect = new Rect(Random.value, Random.value, 1.5f, 0.85f);
            noise.color = new Color(1f, 1f, 1f, Random.Range(0.4f, 0.85f));
            yield return null;
        }
        noise.enabled = false;
        header.text = subject.text = quote.text = prompt.text = "";

        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.5f;
            yield return null;
        }
        group.alpha = 0f;
        IsShowing = false;
    }

    void Update()
    {
        if (!IsShowing) return;

        if (prompt.text.Length > 0)
            prompt.alpha = Mathf.Repeat(Time.unscaledTime, 1.1f) < 0.7f ? 1f : 0.2f;
        if (WaitingForKey && Input.anyKeyDown)
            Continue();
    }

    // --- The trace ---

    IEnumerator DrawTrace()
    {
        float elapsed = 0f;
        int lastColumn = -1;
        float lastValue = 0f;
        int nextBeep = 0;
        bool flatlining = false;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            int column = Mathf.FloorToInt(elapsed / SweepSeconds * TraceWidth);
            for (int c = lastColumn + 1; c <= column; c++)
            {
                float time = c / (float)TraceWidth * SweepSeconds;
                float value = 0f;
                for (int b = 0; b < BeatTimes.Length; b++)
                    value += BeatStrengths[b] * Heartbeat(time - BeatTimes[b]);
                DrawColumn(c % TraceWidth, lastValue, value);
                lastValue = value;
            }
            lastColumn = column;
            traceTexture.SetPixels32(tracePixels);
            traceTexture.Apply(false);

            // A beep on each spike, then the long tone once it's gone flat.
            if (nextBeep < BeatTimes.Length && elapsed >= BeatTimes[nextBeep] + 0.16f)
            {
                beeper.PlayOneShot(beepClip, 0.6f * BeatStrengths[nextBeep]);
                nextBeep++;
            }
            if (!flatlining && elapsed >= BeatTimes[BeatTimes.Length - 1] + 0.7f)
            {
                flatlining = true;
                tone.volume = 0.3f;
                tone.Play();
            }
            if (flatlining) tone.volume = Mathf.Max(0.12f, tone.volume - 0.03f * Time.unscaledDeltaTime);

            yield return null;
        }
    }

    // One heartbeat's shape on a monitor: a small bump, a sharp spike and dip, and a slower bump. t in seconds from its start.
    static float Heartbeat(float t)
    {
        if (t < 0f || t > 0.45f) return 0f;
        return 0.12f * Bump(t, 0.05f, 0.025f)
             - 0.15f * Bump(t, 0.13f, 0.01f)
             + 1f * Bump(t, 0.16f, 0.013f)
             - 0.32f * Bump(t, 0.19f, 0.012f)
             + 0.22f * Bump(t, 0.32f, 0.045f);
    }

    static float Bump(float t, float center, float width)
    {
        float offset = (t - center) / width;
        return Mathf.Exp(-0.5f * offset * offset);
    }

    void DrawColumn(int x, float fromValue, float toValue)
    {
        for (int gap = 0; gap < GapColumns; gap++)
        {
            int gapX = (x + gap) % TraceWidth;
            for (int y = 0; y < TraceHeight; y++)
                tracePixels[y * TraceWidth + gapX] = default;
        }

        int middle = TraceHeight / 2;
        float amplitude = TraceHeight * 0.42f;
        int fromY = Mathf.Clamp(middle + Mathf.RoundToInt(fromValue * amplitude), 1, TraceHeight - 2);
        int toY = Mathf.Clamp(middle + Mathf.RoundToInt(toValue * amplitude), 1, TraceHeight - 2);
        int low = Mathf.Min(fromY, toY);
        int high = Mathf.Max(fromY, toY);
        for (int y = low - 1; y <= high + 1; y++)
            tracePixels[y * TraceWidth + x] = y >= low && y <= high ? TraceBright : TraceGlow;
    }

    void ClearTrace()
    {
        for (int i = 0; i < tracePixels.Length; i++)
            tracePixels[i] = default;
        traceTexture.SetPixels32(tracePixels);
        traceTexture.Apply(false);
    }

    // --- Building ---

    void Build(Font monitorFont)
    {
        if (monitorFont != null) font = TMP_FontAsset.CreateFontAsset(monitorFont);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image glass = Stretch(NewRect("Glass", transform)).gameObject.AddComponent<Image>();
        glass.color = new Color(0.012f, 0.01f, 0.01f);
        glass.raycastTarget = false;

        Texture2D gridTexture = PixelArt.MakeTexture(16, 16, (x, y) => x == 0 || y == 0 ? GridLine : default);
        gridTexture.wrapMode = TextureWrapMode.Repeat;
        RawImage grid = Picture("Grid", gridTexture);
        grid.uvRect = new Rect(0f, 0f, ReferenceResolution.x / 64f, ReferenceResolution.y / 64f);

        traceTexture = new Texture2D(TraceWidth, TraceHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tracePixels = new Color32[TraceWidth * TraceHeight];
        RawImage trace = Picture("Trace", traceTexture);
        Place(trace.rectTransform, 0.5f, 0.64f, new Vector2(1500f, 420f));

        header = Label("Header", "", 78f, HeaderColor, 0.5f, 0.39f, new Vector2(1600f, 100f));
        header.characterSpacing = 14f;
        subject = Label("Subject", "", 30f, DimColor, 0.5f, 0.32f, new Vector2(1600f, 50f));
        subject.characterSpacing = 6f;
        quote = Label("Quote", "", 38f, LineColor, 0.5f, 0.22f, new Vector2(1400f, 120f));
        quote.textWrappingMode = TextWrappingModes.Normal;
        prompt = Label("Prompt", "", 26f, DimColor, 0.5f, 0.08f, new Vector2(1200f, 40f));
        prompt.characterSpacing = 6f;

        Texture2D scanTexture = PixelArt.MakeTexture(1, 4, (x, y) => y == 0 ? ScanLine : default);
        scanTexture.wrapMode = TextureWrapMode.Repeat;
        RawImage scanlines = Picture("Scanlines", scanTexture);
        scanlines.uvRect = new Rect(0f, 0f, 1f, ReferenceResolution.y / 4f);

        RawImage vignette = Picture("Vignette", CombatSprites.VignetteTexture);
        vignette.color = new Color(0f, 0f, 0f, 0.85f);

        Texture2D noiseTexture = PixelArt.MakeTexture(192, 108, (x, y) =>
        {
            byte shade = (byte)Random.Range(0, 256);
            return new Color32(shade, shade, shade, 255);
        });
        noiseTexture.wrapMode = TextureWrapMode.Repeat;
        noise = Picture("Static", noiseTexture);
        noise.enabled = false;

        // Its own sound, which carries on while the rest of the game is paused and silent.
        beeper = gameObject.AddComponent<AudioSource>();
        beeper.playOnAwake = false;
        beeper.spatialBlend = 0f;
        beeper.ignoreListenerPause = true;
        tone = gameObject.AddComponent<AudioSource>();
        tone.playOnAwake = false;
        tone.spatialBlend = 0f;
        tone.loop = true;
        tone.ignoreListenerPause = true;
        tone.clip = Tone("Flatline", 880f, 1f, 0.5f, false);
        beepClip = Tone("Beep", 1000f, 0.09f, 0.6f, true);
    }

    // A sine tone. A whole number of cycles in a whole second loops without a click, so the flatline skips the fade.
    static AudioClip Tone(string clipName, float frequency, float seconds, float volume, bool fadeEdges)
    {
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)SampleRate;
            float envelope = fadeEdges ? Mathf.Clamp01(time / 0.005f) * Mathf.Clamp01((seconds - time) / 0.02f) : 1f;
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * volume * envelope;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    RawImage Picture(string pictureName, Texture texture)
    {
        RawImage picture = Stretch(NewRect(pictureName, transform)).gameObject.AddComponent<RawImage>();
        picture.texture = texture;
        picture.raycastTarget = false;
        return picture;
    }

    TextMeshProUGUI Label(string labelName, string text, float size, Color color, float anchorX, float anchorY, Vector2 area)
    {
        var label = NewRect(labelName, transform).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        Place(label.rectTransform, anchorX, anchorY, area);
        return label;
    }

    static RectTransform NewRect(string rectName, Transform parent)
    {
        var rect = new GameObject(rectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static void Place(RectTransform rect, float anchorX, float anchorY, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, anchorY);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    static IEnumerator TypeOut(TMP_Text label, string text)
    {
        label.text = text;
        label.maxVisibleCharacters = 0;
        for (float shown = 0f; shown < text.Length; shown += LettersPerSecond * Time.unscaledDeltaTime)
        {
            label.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        label.maxVisibleCharacters = text.Length;
    }

    static IEnumerator WaitReal(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }
}
