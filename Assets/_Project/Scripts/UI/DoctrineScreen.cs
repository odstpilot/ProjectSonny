using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The doctrine check on a DoctrineTerminal: Sonny's red lens over a question, and the answers under it, scrambled.
// Pick an answer with W/S or the mouse, and give it with E, Enter, or a click. Holding Space or the right mouse button
// decrypts the picked answer a letter at a time, but Sonny notices: the TRACE meter fills while decrypting, creeps up
// while the player hesitates, and jumps for a wrong answer, which is then struck off. Answer every question right to be
// verified and handed the terminal's credential. Let the trace fill and it's HUMAN DETECTED: the alarm goes off and the
// terminal locks the player out.
// Scrambled answers keep their spaces, so the shape of the words is a clue, and the answers are reshuffled every time.
// Built from code the first time a terminal needs it. Getting hurt closes it; Esc leaves, and progress is lost.
public class DoctrineScreen : MonoBehaviour
{
    const int SortingOrder = 110;           // over the HUD and prompts, under the death screen
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const int MaxAnswers = 4;
    const float LettersPerSecond = 40f;
    const float ScrambleInterval = 0.07f;
    const float RejectShowTime = 1.6f;
    const float DetectedShowTime = 1.8f;
    const string Glyphs = "#%&@$*?!/|=+0123456789ABCDEFXZ";
    const string SonnyPrefix = "SONNY  >  ";
    const int SampleRate = 44100;

    static readonly Color Red = new Color(1f, 0.22f, 0.16f);
    static readonly Color RedDim = new Color(0.56f, 0.18f, 0.14f);
    static readonly Color LineColor = new Color(0.96f, 0.84f, 0.78f);
    static readonly Color Good = new Color(1f, 0.7f, 0.34f);
    static readonly Color RowColor = new Color(0.12f, 0.05f, 0.045f, 0.9f);
    static readonly Color RowPicked = new Color(0.32f, 0.08f, 0.06f, 0.95f);
    static readonly Color RowStruck = new Color(0.05f, 0.035f, 0.035f, 0.8f);
    static readonly Color PanelColor = new Color(0.045f, 0.03f, 0.03f, 0.97f);
    static readonly Color PanelEdge = new Color(0.32f, 0.08f, 0.06f);
    static readonly Color TraceEmpty = new Color(0.15f, 0.05f, 0.04f);
    const string RealTextColor = "#F5D6C7";
    const string ScrambledColor = "#9E4A3E";

    enum Phase { Closed, Greeting, Asking, Judging, Verified, Detected }

    static DoctrineScreen instance;

    public static DoctrineScreen Current => instance;

    public bool IsOpen { get; private set; }
    public DoctrineTerminal Terminal { get; private set; }
    // 0 to 1. At 1 the player is caught.
    public float Trace { get; private set; }
    public int QuestionIndex { get; private set; }
    public int AnswerCount => answers.Length;
    public bool WaitingForAnswer => phase == Phase.Asking;
    public bool IsVerified => phase == Phase.Verified;
    public bool Detected => phase == Phase.Detected;
    // Which answer on screen is right. For tests and debugging.
    public int CorrectIndex => correct;

    private Phase phase = Phase.Closed;
    private List<Behaviour> frozenControls;
    private Health playerHealth;
    private int openedFrame;
    private string[] answers = new string[0];
    private char[][] scrambles = new char[0][];
    private float[] decrypted = new float[0];
    private bool[] struck = new bool[0];
    private int correct;
    private int picked;
    private bool scriptDecrypting;
    private float nextScramble;
    private float rejectUntil;
    private float nextChatter;
    private Vector3 lastMouse;

    private bool built;
    private TMP_FontAsset font;
    private CanvasGroup device;
    private TextMeshProUGUI header;
    private TextMeshProUGUI sonnyLine;
    private TextMeshProUGUI reject;
    private TextMeshProUGUI verdict;
    private TextMeshProUGUI credentialLine;
    private TextMeshProUGUI tracePercent;
    private TextMeshProUGUI hints;
    private Image lensGlow;
    private Image lensRing;
    private RectTransform traceFill;
    private Image traceFillImage;
    private readonly Image[] rows = new Image[MaxAnswers];
    private readonly TextMeshProUGUI[] rowLabels = new TextMeshProUGUI[MaxAnswers];
    private readonly RectTransform[] rowBars = new RectTransform[MaxAnswers];
    private AudioSource sfx;
    private AudioClip blipClip;
    private AudioClip buzzClip;
    private AudioClip chimeClip;

    public static DoctrineScreen Get(Font screenFont = null)
    {
        if (instance == null)
        {
            instance = new GameObject("DoctrineScreen", typeof(RectTransform)).AddComponent<DoctrineScreen>();
            instance.Build(screenFont);
        }
        return instance;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Open(DoctrineTerminal terminal, Transform player)
    {
        if (IsOpen || terminal == null || player == null) return;

        Terminal = terminal;
        IsOpen = true;
        openedFrame = Time.frameCount;
        frozenControls = PlayerControls.Freeze(player);
        playerHealth = player.GetComponent<Health>();
        if (playerHealth != null) playerHealth.Damaged += OnPlayerHurt;

        device.alpha = 1f;
        header.text = terminal.displayName + "  //  DOCTRINE CHECK";
        sonnyLine.text = reject.text = verdict.text = credentialLine.text = "";
        Trace = 0f;
        ShowAnswers(0);
        StartCoroutine(terminal.IsVerified ? ShowVerified(false) : RunCheck());
    }

    public void Close()
    {
        if (!IsOpen) return;

        StopAllCoroutines();
        IsOpen = false;
        phase = Phase.Closed;
        scriptDecrypting = false;
        if (playerHealth != null) playerHealth.Damaged -= OnPlayerHurt;
        PlayerControls.Release(frozenControls);
        device.alpha = 0f;
        Terminal = null;
    }

    // For scripts and tests, the same as the keys and mouse.
    public void Pick(int index)
    {
        if (phase != Phase.Asking || index < 0 || index >= answers.Length || struck[index]) return;
        if (index != picked) sfx.PlayOneShot(blipClip, 0.3f);
        picked = index;
    }

    public void SetDecrypting(bool decrypting)
    {
        scriptDecrypting = decrypting;
    }

    public void Answer(int index)
    {
        if (phase != Phase.Asking || index < 0 || index >= answers.Length || struck[index]) return;

        if (index == correct)
        {
            phase = Phase.Judging;
            sfx.PlayOneShot(chimeClip, 0.5f);
            decrypted[index] = 1f;
            reject.text = "";
            return;
        }

        struck[index] = true;
        decrypted[index] = 1f;
        sfx.PlayOneShot(buzzClip, 0.6f);
        reject.text = Terminal.doctrine.rejectedLine;
        rejectUntil = Time.unscaledTime + RejectShowTime;
        CameraShake.Shake(0.2f);
        AddTrace(Terminal.wrongAnswerTrace);
        if (phase == Phase.Asking && struck[picked]) picked = FirstOpenAnswer();
    }

    void Update()
    {
        if (!IsOpen || Time.timeScale == 0f) return;

        bool firstFrame = Time.frameCount == openedFrame;
        if (!firstFrame && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (phase == Phase.Asking) HandleAnswering(firstFrame);
        else if (phase == Phase.Verified && !firstFrame &&
                 (Input.GetKeyDown(DoctrineTerminal.InteractKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
        {
            Close();
            return;
        }

        if (reject.text.Length > 0 && Time.unscaledTime > rejectUntil) reject.text = "";
        UpdateLens();
        UpdateTraceMeter();
        UpdateAnswers();
    }

    // --- The check ---

    IEnumerator RunCheck()
    {
        DoctrineData doctrine = Terminal.doctrine;
        if (doctrine == null || doctrine.questions == null || doctrine.questions.Length == 0)
        {
            yield return Say("THIS RELAY HAS NOTHING TO ASK.");
            yield return ShowVerified(true);
            yield break;
        }

        phase = Phase.Greeting;
        hints.text = "";
        yield return Say(doctrine.greeting);
        yield return WaitReal(1f);

        for (QuestionIndex = 0; QuestionIndex < doctrine.questions.Length; QuestionIndex++)
        {
            SetUpQuestion(doctrine.questions[QuestionIndex]);
            yield return Say(doctrine.questions[QuestionIndex].asks);
            ShowAnswers(answers.Length);
            hints.text = $"W S  PICK      HOLD SPACE  DECRYPT      {DoctrineTerminal.InteractKey}  ANSWER";
            phase = Phase.Asking;

            while (phase == Phase.Asking) yield return null;
            if (phase != Phase.Judging) yield break;
            yield return WaitReal(0.8f);
        }

        yield return ShowVerified(true);
    }

    void HandleAnswering(bool firstFrame)
    {
        AddTrace(Terminal.hesitationTrace * Time.unscaledDeltaTime);
        if (phase != Phase.Asking) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Pick(NextOpenAnswer(-1));
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Pick(NextOpenAnswer(1));

        // Pointing at an answer picks it, but only when the mouse actually moves, so it doesn't fight the keys.
        Vector3 mouse = Input.mousePosition;
        int hovered = -1;
        for (int i = 0; i < answers.Length; i++)
            if (!struck[i] && RectTransformUtility.RectangleContainsScreenPoint(rows[i].rectTransform, mouse, null)) hovered = i;
        if (hovered >= 0 && mouse != lastMouse) Pick(hovered);
        lastMouse = mouse;

        bool decrypting = scriptDecrypting || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(1);
        if (decrypting && !struck[picked] && decrypted[picked] < 1f)
        {
            decrypted[picked] = Mathf.MoveTowards(decrypted[picked], 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, Terminal.decryptTime));
            AddTrace(Terminal.decryptTrace * Time.unscaledDeltaTime);
            if (Time.unscaledTime >= nextChatter)
            {
                sfx.PlayOneShot(blipClip, 0.12f);
                nextChatter = Time.unscaledTime + 0.06f;
            }
        }
        if (phase != Phase.Asking || firstFrame) return;

        if (Input.GetKeyDown(DoctrineTerminal.InteractKey) || Input.GetKeyDown(KeyCode.Return)) Answer(picked);
        else if (Input.GetMouseButtonDown(0) && hovered >= 0) Answer(hovered);
    }

    void SetUpQuestion(DoctrineData.Question question)
    {
        var options = new List<string> { question.rightAnswer };
        if (question.wrongAnswers != null)
        {
            foreach (string wrong in question.wrongAnswers)
                if (!string.IsNullOrEmpty(wrong) && options.Count < MaxAnswers) options.Add(wrong);
        }

        // Shuffle, keeping track of where the right one ends up.
        int[] order = new int[options.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        answers = new string[options.Count];
        scrambles = new char[options.Count][];
        for (int i = 0; i < order.Length; i++)
        {
            answers[i] = options[order[i]];
            scrambles[i] = new char[answers[i].Length];
            if (order[i] == 0) correct = i;
        }
        decrypted = new float[answers.Length];
        struck = new bool[answers.Length];
        picked = 0;
        Rescramble();
    }

    void AddTrace(float amount)
    {
        if (amount <= 0f || phase == Phase.Detected || phase == Phase.Verified) return;
        Trace = Mathf.Min(1f, Trace + amount);
        if (Trace >= 1f)
        {
            StopAllCoroutines();
            StartCoroutine(HumanDetected());
        }
    }

    IEnumerator HumanDetected()
    {
        phase = Phase.Detected;
        ShowAnswers(0);
        reject.text = hints.text = "";
        sonnyLine.maxVisibleCharacters = 99999;
        sonnyLine.text = FormatSonny(Terminal.doctrine != null ? Terminal.doctrine.detectedLine : "");
        verdict.color = Red;
        Terminal.RaiseAlarm();
        CameraShake.Shake(0.5f);

        for (float t = 0f; t < DetectedShowTime; t += Time.unscaledDeltaTime)
        {
            verdict.text = Mathf.Repeat(t * 4f, 1f) < 0.6f ? "HUMAN DETECTED" : "";
            yield return null;
        }
        Close();
    }

    IEnumerator ShowVerified(bool justNow)
    {
        phase = Phase.Verified;
        ShowAnswers(0);
        reject.text = "";
        hints.text = $"{DoctrineTerminal.InteractKey}  CLOSE";
        verdict.color = Good;
        verdict.text = "VERIFIED";
        if (justNow)
        {
            Terminal.Verify();
            sfx.PlayOneShot(chimeClip, 0.7f);
        }

        DoctrineData doctrine = Terminal.doctrine;
        if (doctrine != null)
        {
            yield return Say(doctrine.verifiedLine);
            if (!string.IsNullOrEmpty(doctrine.credential))
                credentialLine.text = "CREDENTIAL:  " + (string.IsNullOrEmpty(doctrine.credentialName) ? doctrine.credential : doctrine.credentialName);
        }
    }

    void OnPlayerHurt(DamageInfo info)
    {
        Close();
    }

    int FirstOpenAnswer()
    {
        for (int i = 0; i < answers.Length; i++)
            if (!struck[i]) return i;
        return 0;
    }

    int NextOpenAnswer(int step)
    {
        for (int tries = 1; tries <= answers.Length; tries++)
        {
            int index = ((picked + step * tries) % answers.Length + answers.Length) % answers.Length;
            if (!struck[index]) return index;
        }
        return picked;
    }

    // --- Drawing ---

    void UpdateAnswers()
    {
        if (Time.unscaledTime >= nextScramble)
        {
            Rescramble();
            nextScramble = Time.unscaledTime + ScrambleInterval;
        }

        var text = new StringBuilder();
        for (int i = 0; i < answers.Length && i < MaxAnswers; i++)
        {
            string answer = answers[i];
            int shown = Mathf.FloorToInt(decrypted[i] * answer.Length);
            text.Clear();
            if (struck[i]) text.Append("<s>");
            text.Append("<color=").Append(RealTextColor).Append('>').Append(answer, 0, shown).Append("</color>");
            text.Append("<color=").Append(ScrambledColor).Append('>').Append(scrambles[i], shown, answer.Length - shown).Append("</color>");
            if (struck[i]) text.Append("</s>");
            rowLabels[i].text = text.ToString();
            rowLabels[i].alpha = struck[i] ? 0.45f : 1f;

            bool isPicked = i == picked && phase == Phase.Asking;
            rows[i].color = struck[i] ? RowStruck : isPicked ? RowPicked : (phase == Phase.Judging && i == correct ? new Color(0.3f, 0.18f, 0.06f) : RowColor);
            rowBars[i].anchorMax = new Vector2(decrypted[i], rowBars[i].anchorMax.y);
        }
    }

    // New junk for every letter not yet decrypted. Spaces stay spaces.
    void Rescramble()
    {
        for (int i = 0; i < answers.Length; i++)
        {
            for (int c = 0; c < answers[i].Length; c++)
                scrambles[i][c] = answers[i][c] == ' ' ? ' ' : Glyphs[Random.Range(0, Glyphs.Length)];
        }
    }

    void ShowAnswers(int count)
    {
        for (int i = 0; i < MaxAnswers; i++)
            rows[i].gameObject.SetActive(i < count);
    }

    void UpdateLens()
    {
        // Faster and brighter the closer Sonny is to seeing through the player.
        float rate = Mathf.Lerp(0.6f, 4f, Trace);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * rate);
        Color color = phase == Phase.Verified ? Good : Red;
        lensGlow.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.2f, 0.6f, pulse) * Mathf.Lerp(0.6f, 1f, Trace));
        lensGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.15f, pulse);
        lensRing.color = color;
    }

    void UpdateTraceMeter()
    {
        traceFill.anchorMax = new Vector2(Trace, 1f);
        float warning = Trace > 0.7f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f) : 1f;
        traceFillImage.color = Color.Lerp(RedDim, Red, Trace) * new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, warning));
        tracePercent.text = $"{Mathf.RoundToInt(Trace * 100f)}%";
    }

    static string FormatSonny(string line)
    {
        return $"<color=#FF5A45>SONNY</color>  >  {line}";
    }

    IEnumerator Say(string line)
    {
        sonnyLine.text = FormatSonny(line);
        sonnyLine.ForceMeshUpdate();
        int total = sonnyLine.textInfo.characterCount;
        for (float shown = SonnyPrefix.Length; shown < total; shown += LettersPerSecond * Time.unscaledDeltaTime)
        {
            sonnyLine.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        sonnyLine.maxVisibleCharacters = 99999;
    }

    static IEnumerator WaitReal(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
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
        Stretch(Block("Backdrop", deviceRoot, new Color(0f, 0f, 0f, 0.65f)).rectTransform);

        Image panel = Block("Panel", deviceRoot, PanelColor);
        At(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 860f));
        var edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = PanelEdge;
        edge.effectDistance = new Vector2(5f, -5f);
        RectTransform body = panel.rectTransform;

        header = Label("Header", body, "", 26f, RedDim, TextAlignmentOptions.TopLeft);
        At(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -26f), new Vector2(800f, 40f));
        TextMeshProUGUI leave = Label("Leave", body, "ESC  LEAVE", 26f, RedDim, TextAlignmentOptions.TopRight);
        At(leave.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -26f), new Vector2(300f, 40f));

        // Sonny's lens: a soft glow behind a ring.
        RectTransform lens = NewRect("Lens", body);
        At(lens, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(150f, 150f));
        lensGlow = Block("Glow", lens, Red);
        Stretch(lensGlow.rectTransform);
        lensGlow.rectTransform.offsetMin = new Vector2(-50f, -50f);
        lensGlow.rectTransform.offsetMax = new Vector2(50f, 50f);
        lensGlow.sprite = PixelArt.ToSprite(CombatSprites.SoftCircleTexture, CombatSprites.SoftCircleTexture.width, new Vector2(0.5f, 0.5f));
        lensRing = Block("Ring", lens, Red);
        Stretch(lensRing.rectTransform);
        lensRing.sprite = PixelArt.ToSprite(CombatSprites.RingTexture, CombatSprites.RingTexture.width, new Vector2(0.5f, 0.5f));
        Image pupil = Block("Pupil", lens, new Color(0.02f, 0.01f, 0.01f));
        At(pupil.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
        pupil.sprite = lensGlow.sprite;

        sonnyLine = Label("Sonny", body, "", 34f, LineColor, TextAlignmentOptions.Top);
        sonnyLine.textWrappingMode = TextWrappingModes.Normal;
        At(sonnyLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1060f, 100f));

        reject = Label("Rejected", body, "", 26f, Red, TextAlignmentOptions.Center);
        At(reject.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -355f), new Vector2(1060f, 36f));

        for (int i = 0; i < MaxAnswers; i++)
        {
            Image row = Block("Answer", body, RowColor);
            At(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f - i * 84f), new Vector2(920f, 70f));
            rows[i] = row;

            rowLabels[i] = Label("Text", row.transform, "", 32f, LineColor, TextAlignmentOptions.MidlineLeft);
            Stretch(rowLabels[i].rectTransform);
            rowLabels[i].rectTransform.offsetMin = new Vector2(28f, 0f);
            rowLabels[i].rectTransform.offsetMax = new Vector2(-20f, 0f);

            Image bar = Block("Decrypted", row.transform, Red);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 0.06f);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            rowBars[i] = bar.rectTransform;
        }

        verdict = Label("Verdict", body, "", 80f, Good, TextAlignmentOptions.Center);
        verdict.characterSpacing = 10f;
        At(verdict.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(1060f, 110f));

        credentialLine = Label("Credential", body, "", 30f, Good, TextAlignmentOptions.Center);
        At(credentialLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(1060f, 40f));

        TextMeshProUGUI traceLabel = Label("Trace", body, "TRACE", 26f, RedDim, TextAlignmentOptions.Left);
        At(traceLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50f, 66f), new Vector2(150f, 34f));
        Image traceBack = Block("Trace Bar", body, TraceEmpty);
        At(traceBack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(190f, 72f), new Vector2(800f, 22f));
        traceFillImage = Block("Fill", traceBack.transform, Red);
        traceFill = traceFillImage.rectTransform;
        traceFill.anchorMin = Vector2.zero;
        traceFill.anchorMax = new Vector2(0f, 1f);
        traceFill.offsetMin = traceFill.offsetMax = Vector2.zero;
        tracePercent = Label("Percent", body, "0%", 26f, RedDim, TextAlignmentOptions.Right);
        At(tracePercent.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 66f), new Vector2(140f, 34f));

        hints = Label("Hints", body, "", 22f, RedDim, TextAlignmentOptions.Center);
        At(hints.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1100f, 30f));

        ShowAnswers(0);

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        blipClip = Tone("Blip", 1400f, 0.03f, 0.4f, false);
        buzzClip = Tone("Buzz", 110f, 0.35f, 0.5f, true);
        chimeClip = Chime();
    }

    // A sine blip, or a harsh square buzz.
    static AudioClip Tone(string clipName, float frequency, float seconds, float volume, bool square)
    {
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)SampleRate;
            float wave = Mathf.Sin(2f * Mathf.PI * frequency * time);
            if (square) wave = Mathf.Sign(wave) * 0.6f;
            float envelope = Mathf.Clamp01(time / 0.004f) * Mathf.Clamp01((seconds - time) / 0.02f);
            data[i] = wave * volume * envelope;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Two soft notes, falling: accepted.
    static AudioClip Chime()
    {
        const float seconds = 0.4f;
        int samples = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)SampleRate;
            float frequency = time < seconds * 0.4f ? 784f : 523f;
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float envelope = Mathf.Clamp01(time / 0.01f) * Mathf.Exp(-time * 5f);
            data[i] = Mathf.Sin(phase) * 0.5f * envelope;
        }
        AudioClip clip = AudioClip.Create("Accepted", samples, 1, SampleRate, false);
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
