using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The tuning screen for hacking a HackingTerminal. An oscilloscope shows two waves: the station's broadcast (dim and
// dashed) and the player's own signal (bright). Three knobs shape the player's wave:
//   AMP    how tall it is
//   FREQ   how many waves fit across the screen
//   PHASE  where it sits sideways (this knob turns forever)
// The player's wave is buried in static that clears as the two line up. Hold all three matched and the signal locks:
// the player's wave settles onto the broadcast, the transmission types out line by line, and any credential it carries
// is recovered.
// Controls: A/D, 1-3, or clicking picks a knob; W/S, the scroll wheel, or dragging up and down turns it, with Shift for
// fine tuning; Esc or E leaves. The world keeps running while tuning, and getting hurt knocks the player off.
// Built from code the first time a terminal needs it; every terminal shares it.
public class SignalTuner : MonoBehaviour
{
    const int SortingOrder = 110;           // over the HUD and the tutorial's text, under the death screen
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const int ScopeWidth = 480;
    const int ScopeHeight = 170;
    const float LockHoldTime = 0.8f;        // seconds all three have to stay matched
    const float KeyTurnTime = 2.5f;         // seconds for the keys to turn a knob through its whole range
    const float ScrollStep = 0.025f;
    const float FineTuning = 0.25f;
    const float KnobSweep = 135f;           // degrees each side of straight up
    const float LettersPerSecond = 45f;
    const int VisibleLines = 4;
    const int MeterCells = 16;
    const int SampleRate = 44100;

    // Knob values run 0 to 1 and map onto these.
    const float MinAmp = 0.15f;
    const float MaxAmp = 1f;
    const float MinFreq = 1f;
    const float MaxFreq = 6f;
    static readonly string[] KnobNames = { "AMP", "FREQ", "PHASE" };
    // How close each knob has to be to lock, as a fraction of its range, before the terminal's precision.
    static readonly float[] Tolerance = { 0.07f, 0.03f, 0.055f };

    static readonly Color Amber = new Color(1f, 0.72f, 0.3f);
    static readonly Color AmberDim = new Color(0.6f, 0.4f, 0.2f);
    static readonly Color HotWave = new Color(1f, 0.95f, 0.78f);
    static readonly Color LockedColor = new Color(0.55f, 1f, 0.6f);
    static readonly Color32 BroadcastWave = new Color32(255, 84, 46, 210);
    static readonly Color PanelColor = new Color(0.07f, 0.065f, 0.06f, 0.97f);
    static readonly Color PanelEdge = new Color(0.24f, 0.2f, 0.16f);
    static readonly Color ScreenColor = new Color(0.03f, 0.024f, 0.018f);
    static readonly Color CellOff = new Color(0.18f, 0.13f, 0.09f);
    static readonly Color LineColor = new Color(0.95f, 0.86f, 0.74f);

    static SignalTuner instance;

    public static SignalTuner Current => instance;

    public bool IsOpen { get; private set; }
    public bool IsLocked { get; private set; }
    public bool ShowingTransmission { get; private set; }
    public HackingTerminal Terminal { get; private set; }

    private readonly float[] values = new float[3];
    private readonly float[] targets = new float[3];
    private readonly float[] turnedSinceTick = new float[3];
    private List<Behaviour> frozenControls;
    private int selected;
    private float lockHeld;
    private float match;            // 0 to 1, how well the waves line up
    private int dragging = -1;
    private float dragStartY;
    private float dragStartValue;
    private float lastTick;
    private int openedFrame;
    private int lineIndex;
    private bool lineFinished;
    private Coroutine typing;
    private Health playerHealth;

    private bool built;
    private TMP_FontAsset font;
    private CanvasGroup device;
    private Texture2D scopeTexture;
    private Color32[] scopePixels;
    private RawImage noise;
    private TextMeshProUGUI header;
    private TextMeshProUGUI status;
    private readonly Image[] meter = new Image[MeterCells];
    private readonly RectTransform[] knobAreas = new RectTransform[3];
    private readonly RectTransform[] dials = new RectTransform[3];
    private readonly Image[] rings = new Image[3];
    private readonly TextMeshProUGUI[] readouts = new TextMeshProUGUI[3];
    private GameObject tuningView;
    private GameObject transmissionView;
    private TextMeshProUGUI transmissionTitle;
    private TextMeshProUGUI transmissionBody;
    private TextMeshProUGUI credentialLine;
    private TextMeshProUGUI transmissionHint;
    private AudioSource staticSource;
    private AudioSource sfx;
    private AudioClip tickClip;
    private AudioClip lockClip;

    public static SignalTuner Get(Font screenFont = null)
    {
        if (instance == null)
        {
            instance = new GameObject("SignalTuner", typeof(RectTransform)).AddComponent<SignalTuner>();
            instance.Build(screenFont);
        }
        return instance;
    }

    // How far apart two knob positions are, 0 to 1. PHASE wraps around, so its furthest is half a turn.
    public static float KnobDistance(int knob, float a, float b)
    {
        return knob == 2 ? Mathf.Abs(Mathf.Repeat(a - b + 0.5f, 1f) - 0.5f) : Mathf.Abs(a - b);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Open(HackingTerminal terminal, Transform player)
    {
        if (IsOpen || terminal == null || player == null) return;

        Terminal = terminal;
        IsOpen = true;
        openedFrame = Time.frameCount;
        frozenControls = PlayerControls.Freeze(player);
        playerHealth = player.GetComponent<Health>();
        if (playerHealth != null) playerHealth.Damaged += OnPlayerHurt;

        device.alpha = 1f;
        header.text = "COMMS INTERCEPT  //  " + terminal.displayName;
        staticSource.clip = terminal.staticClip;

        if (terminal.IsHacked)
        {
            // Already cracked: straight to the log.
            terminal.GetSignal(targets, values);
            System.Array.Copy(targets, values, 3);
            IsLocked = true;
            match = 1f;
            ShowTransmission();
        }
        else
        {
            StartTuning();
        }
    }

    public void Close()
    {
        if (!IsOpen) return;

        StopAllCoroutines();
        typing = null;
        IsOpen = false;
        IsLocked = false;
        ShowingTransmission = false;
        dragging = -1;
        staticSource.Stop();
        sfx.Stop();
        if (playerHealth != null) playerHealth.Damaged -= OnPlayerHurt;
        PlayerControls.Release(frozenControls);
        device.alpha = 0f;
        Terminal = null;
    }

    // Sets a knob directly, 0 to 1. For scripts and tests.
    public void SetKnob(int knob, float value)
    {
        float next = knob == 2 ? Mathf.Repeat(value, 1f) : Mathf.Clamp01(value);
        turnedSinceTick[knob] += KnobDistance(knob, values[knob], next);
        values[knob] = next;
    }

    public float GetKnob(int knob) => values[knob];
    public float GetTarget(int knob) => targets[knob];

    // What E does on the transmission: finish the line typing out, or go to the next one.
    public void Advance()
    {
        if (!ShowingTransmission) return;
        if (!lineFinished) FinishLine();
        else NextLine();
    }

    void Update()
    {
        if (!IsOpen || Time.timeScale == 0f) return;

        if (ShowingTransmission)
        {
            bool pressed = Input.GetKeyDown(HackingTerminal.InteractKey) || Input.GetKeyDown(KeyCode.Space) ||
                           Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
            if (Time.frameCount != openedFrame)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Close();
                else if (pressed) Advance();
            }
            if (IsOpen) DrawScope();
            return;
        }

        if (Time.frameCount != openedFrame && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(HackingTerminal.InteractKey)))
        {
            Close();
            return;
        }

        if (!IsLocked) HandleKnobInput();
        UpdateSignal();
        UpdateKnobs();
        DrawScope();
    }

    // --- Tuning ---

    void StartTuning()
    {
        ShowingTransmission = false;
        IsLocked = false;
        lockHeld = 0f;
        match = 0f;
        Select(0);
        tuningView.SetActive(true);
        transmissionView.SetActive(false);
        Terminal.GetSignal(targets, values);
        status.text = "SEARCHING";
        if (staticSource.clip != null) staticSource.Play();
    }

    void HandleKnobInput()
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Select(selected + 2);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Select(selected + 1);
        for (int i = 0; i < 3; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);

        float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? FineTuning : 1f;
        Vector2 mouse = Input.mousePosition;
        int hovered = -1;
        for (int i = 0; i < 3; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(knobAreas[i], mouse, null)) hovered = i;

        // Dragging up and down turns the knob: a drag of 40% of the screen's height is the whole range.
        if (Input.GetMouseButtonDown(0) && hovered >= 0)
        {
            Select(hovered);
            dragging = hovered;
            dragStartY = mouse.y;
            dragStartValue = values[hovered];
        }
        if (!Input.GetMouseButton(0)) dragging = -1;
        if (dragging >= 0)
            SetKnob(dragging, dragStartValue + (mouse.y - dragStartY) / (Screen.height * 0.4f) * speed);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            int knob = hovered >= 0 ? hovered : selected;
            Select(knob);
            SetKnob(knob, values[knob] + Mathf.Sign(scroll) * ScrollStep * speed);
        }

        float keys = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                   - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        if (keys != 0f)
            SetKnob(selected, values[selected] + keys * speed * Time.unscaledDeltaTime / KeyTurnTime);
    }

    void Select(int knob)
    {
        selected = (knob % 3 + 3) % 3;
    }

    void UpdateSignal()
    {
        if (Terminal.phaseDrift != 0f && !IsLocked)
            targets[2] = Mathf.Repeat(targets[2] + Terminal.phaseDrift * Time.unscaledDeltaTime, 1f);

        bool allMatched = true;
        float closeness = 1f;
        for (int i = 0; i < 3; i++)
        {
            float tolerance = Tolerance[i] * Terminal.precision;
            float distance = KnobDistance(i, values[i], targets[i]);
            if (distance > tolerance) allMatched = false;
            closeness *= 1f - Mathf.Clamp01((distance - tolerance) / (tolerance * 5f));
        }
        match = Mathf.MoveTowards(match, IsLocked ? 1f : closeness, 3f * Time.unscaledDeltaTime);

        if (!IsLocked)
        {
            lockHeld = allMatched ? lockHeld + Time.unscaledDeltaTime : 0f;
            status.text = allMatched ? "LOCKING" : match > 0.5f ? "CLOSE" : "SEARCHING";
            status.color = allMatched ? LockedColor : Amber;
            Terminal.RememberKnobs(values);
            if (lockHeld >= LockHoldTime) StartCoroutine(Lock());
        }

        staticSource.volume = IsLocked ? 0f : Mathf.Lerp(0.5f, 0.03f, match);
        noise.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 0f, match));
        noise.uvRect = new Rect(Random.value, Random.value, 1.3f, 0.5f);

        int lit = Mathf.RoundToInt(match * MeterCells);
        for (int i = 0; i < MeterCells; i++)
            meter[i].color = i < lit ? (IsLocked ? LockedColor : Color.Lerp(AmberDim, Amber, i / (float)MeterCells)) : CellOff;
    }

    void UpdateKnobs()
    {
        for (int i = 0; i < 3; i++)
        {
            float angle = i == 2 ? -values[i] * 360f : Mathf.Lerp(KnobSweep, -KnobSweep, values[i]);
            dials[i].localEulerAngles = new Vector3(0f, 0f, angle);
            rings[i].color = i == selected ? new Color(Amber.r, Amber.g, Amber.b, 0.9f) : new Color(AmberDim.r, AmberDim.g, AmberDim.b, 0.25f);
        }
        readouts[0].text = $"{Mathf.Lerp(MinAmp, MaxAmp, values[0]):0.00}";
        readouts[1].text = $"{Mathf.Lerp(MinFreq, MaxFreq, values[1]):0.0} HZ";
        readouts[2].text = $"{values[2] * 360f:000} DEG";

        // A click from the knob as it turns.
        for (int i = 0; i < 3; i++)
        {
            if (turnedSinceTick[i] < 0.02f) continue;
            turnedSinceTick[i] = 0f;
            if (Time.unscaledTime - lastTick < 0.035f) continue;
            lastTick = Time.unscaledTime;
            sfx.PlayOneShot(tickClip, 0.25f);
        }
    }

    IEnumerator Lock()
    {
        IsLocked = true;
        status.text = "LOCKED";
        status.color = LockedColor;
        sfx.PlayOneShot(lockClip, 0.7f);
        Terminal.MarkHacked();

        // The player's wave settles exactly onto the broadcast.
        var from = (float[])values.Clone();
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / 0.4f);
            for (int i = 0; i < 3; i++)
            {
                float delta = i == 2 ? Mathf.Repeat(targets[i] - from[i] + 0.5f, 1f) - 0.5f : targets[i] - from[i];
                values[i] = i == 2 ? Mathf.Repeat(from[i] + delta * progress, 1f) : from[i] + delta * progress;
            }
            yield return null;
        }
        System.Array.Copy(targets, values, 3);

        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            yield return null;
        ShowTransmission();
    }

    // --- The transmission ---

    void ShowTransmission()
    {
        ShowingTransmission = true;
        tuningView.SetActive(false);
        transmissionView.SetActive(true);
        staticSource.Stop();

        TransmissionData data = Terminal.transmission;
        transmissionTitle.text = data != null ? data.title : "NO TRANSMISSION ON THIS NODE";
        transmissionBody.text = "";
        credentialLine.text = "";
        if (data != null && data.recording != null)
        {
            sfx.clip = data.recording;
            sfx.Play();
        }

        lineIndex = -1;
        NextLine();
    }

    void NextLine()
    {
        TransmissionData data = Terminal.transmission;
        int count = data != null && data.lines != null ? data.lines.Length : 0;
        lineIndex++;

        if (lineIndex < count)
        {
            // The last few lines, the earlier ones dimmed, and the newest typing out.
            var earlier = new System.Text.StringBuilder();
            for (int i = Mathf.Max(0, lineIndex - VisibleLines + 1); i < lineIndex; i++)
                earlier.Append("<alpha=#77>").Append(FormatLine(data.lines[i])).Append("\n");
            string current = "<alpha=#FF>" + FormatLine(data.lines[lineIndex]);

            transmissionBody.maxVisibleCharacters = 99999;
            transmissionBody.text = earlier.ToString();
            transmissionBody.ForceMeshUpdate();
            int start = transmissionBody.textInfo.characterCount;
            transmissionBody.text = earlier + current;
            transmissionBody.ForceMeshUpdate();
            int end = transmissionBody.textInfo.characterCount;

            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(TypeLine(start, end));
            transmissionHint.text = $"{HackingTerminal.InteractKey}  NEXT";
        }
        else if (lineIndex == count)
        {
            lineFinished = true;
            if (data != null && !string.IsNullOrEmpty(data.credential))
            {
                string credentialName = string.IsNullOrEmpty(data.credentialName) ? data.credential : data.credentialName;
                credentialLine.text = "CREDENTIAL RECOVERED:  " + credentialName;
            }
            transmissionHint.text = $"{HackingTerminal.InteractKey}  CLOSE";
        }
        else
        {
            Close();
        }
    }

    static string FormatLine(TransmissionData.Line line)
    {
        return string.IsNullOrEmpty(line.speaker)
            ? line.text
            : $"<color=#FFB35A>{line.speaker}</color>  >  {line.text}";
    }

    IEnumerator TypeLine(int start, int end)
    {
        lineFinished = false;
        for (float shown = start; shown < end; shown += LettersPerSecond * Time.unscaledDeltaTime)
        {
            transmissionBody.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        FinishLine();
    }

    void FinishLine()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null;
        transmissionBody.maxVisibleCharacters = 99999;
        lineFinished = true;
    }

    void OnPlayerHurt(DamageInfo info)
    {
        Close();
    }

    // --- Drawing ---

    void DrawScope()
    {
        System.Array.Clear(scopePixels, 0, scopePixels.Length);
        DrawWave(targets, BroadcastWave, true, 1, 0f);

        Color playerWave = IsLocked ? LockedColor : Color.Lerp(Amber, HotWave, match);
        DrawWave(values, playerWave, false, 2, IsLocked ? 0f : (1f - match) * 0.14f);

        scopeTexture.SetPixels32(scopePixels);
        scopeTexture.Apply(false);
    }

    // noise shakes the line up and down a little in every column, fresh every frame, like a weak signal.
    void DrawWave(float[] knobs, Color32 color, bool dashed, int thickness, float noiseAmount)
    {
        float amplitude = Mathf.Lerp(MinAmp, MaxAmp, knobs[0]);
        float frequency = Mathf.Lerp(MinFreq, MaxFreq, knobs[1]);
        float phase = knobs[2] * Mathf.PI * 2f;
        int middle = ScopeHeight / 2;
        float scale = ScopeHeight * 0.42f;
        int previousY = middle;

        for (int x = 0; x < ScopeWidth; x++)
        {
            float t = x / (float)(ScopeWidth - 1);
            float value = amplitude * Mathf.Sin(t * frequency * Mathf.PI * 2f + phase);
            if (noiseAmount > 0f) value += (Random.value * 2f - 1f) * noiseAmount;
            int y = Mathf.Clamp(middle + Mathf.RoundToInt(value * scale), 0, ScopeHeight - thickness);
            if (x == 0) previousY = y;

            if (!dashed || (x / 6) % 2 == 0)
            {
                int low = Mathf.Min(previousY, y);
                int high = Mathf.Min(Mathf.Max(previousY, y) + thickness - 1, ScopeHeight - 1);
                for (int row = low; row <= high; row++)
                    scopePixels[row * ScopeWidth + x] = color;
            }
            previousY = y;
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
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform deviceRoot = Stretch(NewRect("Device", transform));
        device = deviceRoot.gameObject.AddComponent<CanvasGroup>();
        device.alpha = 0f;
        device.blocksRaycasts = false;
        device.interactable = false;
        Stretch(Block("Backdrop", deviceRoot, new Color(0f, 0f, 0f, 0.6f)).rectTransform);

        Image panel = Block("Panel", deviceRoot, PanelColor);
        At(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 820f));
        var edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = PanelEdge;
        edge.effectDistance = new Vector2(5f, -5f);
        RectTransform body = panel.rectTransform;

        header = Label("Header", body, "", 26f, AmberDim, TextAlignmentOptions.TopLeft);
        At(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -26f), new Vector2(800f, 40f));
        TextMeshProUGUI leave = Label("Leave", body, "ESC  LEAVE", 26f, AmberDim, TextAlignmentOptions.TopRight);
        At(leave.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -26f), new Vector2(300f, 40f));

        BuildScope(body);
        BuildTuning(body);
        BuildTransmission(body);

        staticSource = gameObject.AddComponent<AudioSource>();
        staticSource.playOnAwake = false;
        staticSource.loop = true;
        staticSource.spatialBlend = 0f;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        tickClip = Click();
        lockClip = Chirp();
    }

    void BuildScope(RectTransform body)
    {
        Image screen = Block("Scope", body, ScreenColor);
        At(screen.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1080f, 380f));
        var glowEdge = screen.gameObject.AddComponent<Outline>();
        glowEdge.effectColor = new Color(0.45f, 0.26f, 0.1f, 0.6f);
        glowEdge.effectDistance = new Vector2(3f, -3f);
        RectTransform scope = screen.rectTransform;

        Texture2D gridTexture = PixelArt.MakeTexture(20, 20, (x, y) => x == 0 || y == 0 ? new Color32(255, 170, 80, 36) : default);
        gridTexture.wrapMode = TextureWrapMode.Repeat;
        Picture("Grid", scope, gridTexture).uvRect = new Rect(0f, 0f, 1080f / 40f, 380f / 40f);

        scopeTexture = new Texture2D(ScopeWidth, ScopeHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        scopePixels = new Color32[ScopeWidth * ScopeHeight];
        RawImage wave = Picture("Waves", scope, scopeTexture);
        wave.rectTransform.offsetMin = new Vector2(20f, 20f);
        wave.rectTransform.offsetMax = new Vector2(-20f, -20f);

        Texture2D noiseTexture = PixelArt.MakeTexture(192, 72, (x, y) =>
        {
            byte shade = (byte)Random.Range(40, 256);
            return new Color32(shade, (byte)(shade * 0.8f), (byte)(shade * 0.6f), 255);
        });
        noiseTexture.wrapMode = TextureWrapMode.Repeat;
        noise = Picture("Static", scope, noiseTexture);

        Texture2D scanTexture = PixelArt.MakeTexture(1, 4, (x, y) => y == 0 ? new Color32(0, 0, 0, 120) : default);
        scanTexture.wrapMode = TextureWrapMode.Repeat;
        Picture("Scanlines", scope, scanTexture).uvRect = new Rect(0f, 0f, 1f, 380f / 4f);
    }

    void BuildTuning(RectTransform body)
    {
        RectTransform view = Stretch(NewRect("Tuning", body));
        tuningView = view.gameObject;

        TextMeshProUGUI signalLabel = Label("Signal", view, "SIGNAL", 26f, AmberDim, TextAlignmentOptions.Left);
        At(signalLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -478f), new Vector2(160f, 34f));
        for (int i = 0; i < MeterCells; i++)
        {
            Image cell = Block("Cell", view, CellOff);
            At(cell.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f + i * 44f, -484f), new Vector2(36f, 22f));
            meter[i] = cell;
        }
        status = Label("Status", view, "", 26f, Amber, TextAlignmentOptions.Right);
        At(status.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -478f), new Vector2(220f, 34f));

        Texture2D dialTexture = MakeDialTexture();
        for (int i = 0; i < 3; i++)
        {
            Vector2 center = new Vector2((i - 1) * 300f, -620f);

            RectTransform area = NewRect(KnobNames[i], view);
            At(area, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), center, new Vector2(160f, 160f));
            knobAreas[i] = area;

            rings[i] = area.gameObject.AddComponent<Image>();
            rings[i].sprite = PixelArt.ToSprite(CombatSprites.RingTexture, CombatSprites.RingTexture.width, new Vector2(0.5f, 0.5f));
            rings[i].raycastTarget = false;

            RawImage dial = Picture("Dial", area, dialTexture);
            dial.rectTransform.offsetMin = new Vector2(22f, 22f);
            dial.rectTransform.offsetMax = new Vector2(-22f, -22f);
            dials[i] = dial.rectTransform;

            TextMeshProUGUI knobName = Label("Name", view, KnobNames[i], 28f, Amber, TextAlignmentOptions.Center);
            At(knobName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), center + new Vector2(0f, -100f), new Vector2(220f, 34f));
            readouts[i] = Label("Readout", view, "", 22f, AmberDim, TextAlignmentOptions.Center);
            At(readouts[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), center + new Vector2(0f, -128f), new Vector2(220f, 30f));
        }

        TextMeshProUGUI hints = Label("Hints", view, "A D  CHOOSE      W S  SCROLL  DRAG  TURN      SHIFT  FINE", 22f, AmberDim, TextAlignmentOptions.Center);
        At(hints.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1100f, 30f));
    }

    void BuildTransmission(RectTransform body)
    {
        RectTransform view = Stretch(NewRect("Transmission", body));
        transmissionView = view.gameObject;

        transmissionTitle = Label("Title", view, "", 30f, LockedColor, TextAlignmentOptions.Left);
        At(transmissionTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -478f), new Vector2(1080f, 40f));

        transmissionBody = Label("Body", view, "", 30f, LineColor, TextAlignmentOptions.TopLeft);
        transmissionBody.textWrappingMode = TextWrappingModes.Normal;
        transmissionBody.lineSpacing = 12f;
        At(transmissionBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -530f), new Vector2(1080f, 220f));

        credentialLine = Label("Credential", view, "", 28f, LockedColor, TextAlignmentOptions.Left);
        At(credentialLine.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50f, 20f), new Vector2(800f, 36f));

        transmissionHint = Label("Hint", view, "", 26f, AmberDim, TextAlignmentOptions.Right);
        At(transmissionHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 20f), new Vector2(300f, 36f));

        transmissionView.SetActive(false);
    }

    // A ridged knob with a bright pointer notch, drawn pointing straight up.
    static Texture2D MakeDialTexture()
    {
        const int size = 64;
        var rim = new Color32(96, 88, 78, 255);
        var face = new Color32(46, 42, 38, 255);
        var ridge = new Color32(30, 27, 24, 255);
        var pointer = new Color32(255, 184, 76, 255);
        Texture2D texture = PixelArt.MakeTexture(size, size, (x, y) =>
        {
            Vector2 offset = new Vector2(x - 31.5f, y - 31.5f);
            float radius = offset.magnitude;
            if (radius > 30f) return default;
            if (Mathf.Abs(offset.x) < 2.5f && offset.y > 6f && offset.y < 26f) return pointer;
            if (radius > 27f)
            {
                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                return Mathf.Repeat(angle, 15f) < 5f ? ridge : rim;
            }
            return face;
        });
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    static AudioClip Click()
    {
        int samples = Mathf.CeilToInt(0.02f * SampleRate);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
            data[i] = (Random.value * 2f - 1f) * 0.4f * (1f - i / (float)samples);
        AudioClip clip = AudioClip.Create("Knob Click", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Two rising notes: locked on.
    static AudioClip Chirp()
    {
        const float seconds = 0.32f;
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)SampleRate;
            float frequency = time < seconds * 0.45f ? 660f : 990f;
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float envelope = Mathf.Clamp01(time / 0.01f) * Mathf.Clamp01((seconds - time) / 0.06f);
            data[i] = Mathf.Sin(phase) * 0.45f * envelope;
        }
        AudioClip clip = AudioClip.Create("Signal Lock", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // --- UI helpers ---

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

    static void At(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static Image Block(string blockName, Transform parent, Color color)
    {
        var image = NewRect(blockName, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static RawImage Picture(string pictureName, Transform parent, Texture texture)
    {
        var picture = Stretch(NewRect(pictureName, parent)).gameObject.AddComponent<RawImage>();
        picture.texture = texture;
        picture.raycastTarget = false;
        return picture;
    }

    TextMeshProUGUI Label(string labelName, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var label = NewRect(labelName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }
}
