using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// pip, the suit helper
public class SuitHelper : MonoBehaviour
{
    const int SortingOrder = 109;
    const float Margin = 40f;
    const float PortraitSize = 150f;
    const float BubbleWidth = 600f;
    const float BubbleGap = 18f;
    const float FacePixel = 6f;
    const float LettersPerSecond = 85f;
    const float HoldBase = 1.6f;
    const float HoldPerLetter = 0.05f;
    const float LastLineLinger = 2f;
    const float SwitchTime = 0.18f;
    const float ChirpFrequency = 880f;
    const float KeyLineHold = 2f;
    const float KeyLineMinimum = 5f;
    const float CenterSeconds = 0.45f;
    const float CenterHeight = 0.72f;
    const float CenterScale = 1.12f;
    const float DuckVolume = 0.7f;
    const float DamagedStatic = 0.3f;
    const float DamagedStall = 0.012f;
    const float FailingStall = 0.03f;
    const float FailingLost = 0.2f;
    const float FailingCut = 0.3f;

    public static readonly Color PipColor = new Color(0.45f, 1f, 0.78f);
    static readonly Color ScreenTop = new Color(0.06f, 0.24f, 0.2f);
    static readonly Color ScreenBottom = new Color(0.02f, 0.07f, 0.07f);
    const string KeyColorTag = "<color=#FFAE52>";

    enum Eyes { Open, Blink, Happy }
    enum Mouth { Closed, Open, Smile }

    public enum Condition { Fine, Damaged, Failing, Gone }
    public static Condition Wear { get; set; }

    public static void Worsen(Condition to)
    {
        if (to > Wear) Wear = to;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetWear() => Wear = Condition.Fine;

    const char CutMarker = '\u0003';

    static SuitHelper instance;

    public bool Online { get; private set; }
    public bool Talking => running;

    private bool built;
    private CanvasGroup rootGroup;
    private RectTransform root;
    private RectTransform portraitFrame;
    private RectTransform face;
    private Image faceImage;
    private RawImage noise;
    private Texture2D noiseTexture;
    private Image flash;
    private RectTransform nameTab;
    private CanvasGroup bubbleGroup;
    private RectTransform bubble;
    private TextMeshProUGUI lineLabel;
    private Outline keyEdge;
    private AudioSource voice;
    private AudioClip chirp;
    private AudioClip chime;
    private AudioClip crackle;

    private readonly Queue<string> queue = new Queue<string>();
    private int queuedCount;
    private int saidCount;
    private bool running;
    private bool hushed;
    private bool happy;
    private bool mouthOpen;
    private float blinkTimer = 2f;
    private float blinkLeft;
    private float shown;
    private bool present;
    private Coroutine hideRoutine;
    private float centered, centerGoal;
    private Vector2 homeOffset = new Vector2(-(PortraitSize + BubbleGap), 0f);
    private readonly Dictionary<int, Sprite> faces = new Dictionary<int, Sprite>();

    public static SuitHelper Get()
    {
        if (instance == null) instance = FindAnyObjectByType<SuitHelper>();
        if (instance == null) instance = new GameObject("SuitHelper", typeof(RectTransform)).AddComponent<SuitHelper>();
        instance.Build();
        PipLog.Ensure();
        return instance;
    }

    public static bool Exists => instance != null && instance.Online;

    void Awake()
    {
        if (instance == null) instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            SoundManager.DuckTo = 1f;
        }
        if (noiseTexture != null) Destroy(noiseTexture);
    }

    public void Tell(params string[] lines)
    {
        if (Wear == Condition.Gone) return;
        if (!Online) ComeOnline();
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            queue.Enqueue(line);
            queuedCount++;
        }
        if (queue.Count == 0) return;
        StopCoroutineSafe(ref hideRoutine);
        if (!running) StartCoroutine(Run());
    }

    public IEnumerator Say(params string[] lines)
    {
        Tell(lines);
        int until = queuedCount;
        while (saidCount < until) yield return null;
    }

    public static readonly List<string> History = new List<string>();
    public const int HistoryLimit = 200;
    public static event System.Action<string> Said;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHistory()
    {
        History.Clear();
        Said = null;
    }

    public const char TipMarker = '\u0002';

    public static void Note(string text)
    {
        History.Add(TipMarker + text);
        if (History.Count > HistoryLimit) History.RemoveAt(0);
        Said?.Invoke(text);
    }

    static void Remember(string line)
    {
        string shown = Styled(line);
        History.Add(shown);
        if (History.Count > HistoryLimit) History.RemoveAt(0);
        Said?.Invoke(shown);
    }

    public void Hush()
    {
        hushed = true;
        centerGoal = 0f;
        saidCount += queue.Count;
        queue.Clear();
    }

    public IEnumerator Boot(string bootLine = "PIP v2.3  //  BOOTING...")
    {
        if (Online) yield break;
        Online = true;
        present = true;
        root.gameObject.SetActive(true);
        noise.enabled = true;
        faceImage.enabled = false;
        nameTab.gameObject.SetActive(false);

        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float p = t / 0.18f;
            portraitFrame.localScale = new Vector3(1f, Mathf.Lerp(0.04f, 1f, p * p), 1f);
            flash.color = TitleUI.Fade(PipColor, 1f - p);
            yield return null;
        }
        portraitFrame.localScale = Vector3.one;
        flash.color = Color.clear;

        ShowBubble(true);
        lineLabel.color = GameUI.Dim;
        yield return TypeLine(bootLine, false);
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime) yield return null;

        noise.enabled = false;
        faceImage.enabled = true;
        nameTab.gameObject.SetActive(true);
        lineLabel.color = GameUI.Text;
        happy = true;
        if (voice != null) SoundManager.PlayOneShot(voice, "Pip Chime", fallback: chime);
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            flash.color = TitleUI.Fade(PipColor, 0.6f * (1f - t / 0.2f));
            yield return null;
        }
        flash.color = Color.clear;
        happy = false;
        if (!running) hideRoutine = StartCoroutine(HideWhenQuiet());
    }

    public IEnumerator GoOffline(bool instant = false)
    {
        if (!Online) yield break;
        Hush();
        if (!instant) while (running) yield return null;
        StopAllCoroutines();
        running = false;
        hideRoutine = null;
        keyEdge.enabled = false;
        if (!instant && present)
        {
            ShowBubble(false);
            yield return GlitchRoutine(0.5f);
            noise.enabled = true;
            faceImage.enabled = false;
            for (float t = 0f; t < SwitchTime * 2f; t += Time.unscaledDeltaTime)
            {
                float p = t / (SwitchTime * 2f);
                portraitFrame.localScale = new Vector3(Mathf.Lerp(1f, 0.02f, p * p), Mathf.Lerp(1f, 0.04f, p), 1f);
                yield return null;
            }
        }
        Online = false;
        present = false;
        noise.enabled = false;
        faceImage.enabled = true;
        flash.color = Color.clear;
        portraitFrame.localScale = new Vector3(1f, 0.04f, 1f);
        root.gameObject.SetActive(false);
    }

    public IEnumerator Die(string lastLine)
    {
        if (Wear == Condition.Gone) yield break;
        Hush();
        while (running) yield return null;
        Wear = Condition.Failing;
        if (!string.IsNullOrWhiteSpace(lastLine)) yield return Say(CutMarker + lastLine.TrimStart(CutMarker));
        Wear = Condition.Gone;
        yield return GoOffline();
    }

    public void ComeOnline()
    {
        if (Wear == Condition.Gone) return;
        Online = true;
        root.gameObject.SetActive(true);
        noise.enabled = false;
        faceImage.enabled = true;
        nameTab.gameObject.SetActive(true);
    }

    IEnumerator Run()
    {
        running = true;
        if (!present || portraitFrame.localScale.y < 1f) yield return SwitchOn();
        while (queue.Count > 0)
        {
            string line = queue.Dequeue();
            bool cut = line[0] == CutMarker;
            if (cut) line = line.Substring(1);
            if (line.StartsWith("~"))
            {
                line = line.Substring(1);
                yield return GlitchRoutine(0.28f);
            }
            else if (Wear >= Condition.Damaged && Random.value < DamagedStatic) yield return GlitchRoutine(Random.Range(0.2f, 0.45f));
            bool key = line.StartsWith("!");
            if (key) line = line.Substring(1);
            happy = line.StartsWith("^");
            if (happy) line = line.Substring(1).TrimStart();
            if (Wear >= Condition.Failing)
            {
                happy = false;
                float roll = key || cut ? 1f : Random.value;
                if (roll < FailingLost)
                {
                    ShowBubble(false);
                    yield return GlitchRoutine(Random.Range(0.6f, 1f));
                    saidCount++;
                    continue;
                }
                cut |= roll < FailingLost + FailingCut;
            }
            if (cut) line = CutOff(line);
            Remember(line);
            hushed = false;
            keyEdge.enabled = key;
            centerGoal = key ? 1f : 0f;
            if (key && voice != null) SoundManager.PlayOneShot(voice, "Pip Chime", fallback: chime);
            ShowBubble(true);
            yield return TypeLine(Styled(line), true);
            if (cut) yield return GlitchRoutine(0.5f);
            float hold = HoldBase + lineLabel.textInfo.characterCount * HoldPerLetter;
            if (key) yield return HoldKeyLine(Mathf.Max(hold * KeyLineHold, KeyLineMinimum));
            else
                for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;
            keyEdge.enabled = false;
            if (queue.Count == 0 || !queue.Peek().TrimStart(CutMarker, '~').StartsWith("!")) centerGoal = 0f;
            saidCount++;
        }
        happy = false;
        running = false;
        centerGoal = 0f;
        hideRoutine = StartCoroutine(HideWhenQuiet());
    }

    IEnumerator HoldKeyLine(float seconds)
    {
        for (float t = 0f; t < seconds && !hushed;)
        {
            bool covered = Time.timeScale <= 0f || DialogueBox.Busy || GeneratorScreen.AnyOpen || SignalTuner.AnyOpen
                           || DoctrineScreen.AnyOpen || CraftingScreen.IsOpen || PipLog.IsOpen || TipCard.IsOpen;
            if (!covered) t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static string CutOff(string line)
    {
        int at = Mathf.RoundToInt(line.Length * Random.Range(0.35f, 0.7f));
        int open = line.LastIndexOf('[', Mathf.Max(0, at - 1));
        if (open >= 0 && line.IndexOf(']', open) >= at) at = open;
        string kept = line.Substring(0, Mathf.Max(1, at)).TrimEnd(' ', ',', '.', '!', '?');
        return kept + "-";
    }

    public void Glitch(float seconds = 0.3f)
    {
        if (present) StartCoroutine(GlitchRoutine(seconds));
    }

    IEnumerator GlitchRoutine(float seconds, bool quiet = false)
    {
        if (crackle == null) crackle = TitleUI.Crackle("Pip Static", 0.4f, 0.5f);
        if (voice != null && !quiet) SoundManager.PlayOneShot(voice, "Pip Static", fallback: crackle);
        noise.enabled = true;
        faceImage.enabled = false;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            faceImage.enabled = Random.value < 0.2f;
            yield return null;
        }
        noise.enabled = false;
        faceImage.enabled = true;
    }

    IEnumerator HideWhenQuiet()
    {
        for (float t = 0f; t < LastLineLinger; t += Time.unscaledDeltaTime) yield return null;
        ShowBubble(false);
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) yield return null;

        if (voice != null) SoundManager.PlayOneShot(voice, "Pip Chirp", pitchScale: 0.6f, fallback: chirp);
        float from = portraitFrame.localScale.y;
        for (float t = 0f; t < SwitchTime; t += Time.unscaledDeltaTime)
        {
            float p = t / SwitchTime;
            portraitFrame.localScale = new Vector3(1f, Mathf.Lerp(from, 0.04f, p * p), 1f);
            flash.color = TitleUI.Fade(PipColor, p);
            yield return null;
        }
        present = false;
        flash.color = Color.clear;
        hideRoutine = null;
    }

    IEnumerator SwitchOn()
    {
        present = true;
        shown = TutorialHud.ScreenCovered ? 0f : 1f;
        rootGroup.alpha = shown;
        if (voice != null) SoundManager.PlayOneShot(voice, "Pip Chirp", pitchScale: 1.5f, fallback: chirp);
        float from = Mathf.Min(portraitFrame.localScale.y, 0.04f);
        for (float t = 0f; t < SwitchTime; t += Time.unscaledDeltaTime)
        {
            float p = t / SwitchTime;
            portraitFrame.localScale = new Vector3(1f, Mathf.Lerp(from, 1f, 1f - (1f - p) * (1f - p)), 1f);
            flash.color = TitleUI.Fade(PipColor, 1f - p);
            yield return null;
        }
        portraitFrame.localScale = Vector3.one;
        flash.color = Color.clear;
    }

    IEnumerator TypeLine(string text, bool chatter)
    {
        lineLabel.text = text;
        lineLabel.maxVisibleCharacters = 0;
        lineLabel.ForceMeshUpdate();
        int total = lineLabel.textInfo.characterCount;
        int shownLetters = 0;
        float mouthTimer = 0f;
        float stall = !chatter ? 0f : Wear >= Condition.Failing ? FailingStall : Wear >= Condition.Damaged ? DamagedStall : 0f;
        for (float typed = 0f; shownLetters < total; typed += LettersPerSecond * Time.unscaledDeltaTime)
        {
            int now = Mathf.Min(total, Mathf.FloorToInt(typed));
            bool stalled = false;
            for (int i = shownLetters; i < now; i++)
            {
                char c = lineLabel.textInfo.characterInfo[i].character;
                if (chatter && i % 2 == 0 && char.IsLetterOrDigit(c)) Chirp();
                if (Random.value < stall)
                {
                    now = i + 1;
                    stalled = true;
                    break;
                }
            }
            shownLetters = now;
            if (stalled)
            {
                lineLabel.maxVisibleCharacters = shownLetters;
                mouthOpen = false;
                yield return GlitchRoutine(Random.Range(0.12f, 0.35f), Random.value < 0.5f);
                typed = shownLetters;
                continue;
            }
            lineLabel.maxVisibleCharacters = shownLetters;
            mouthTimer += Time.unscaledDeltaTime;
            if (chatter && mouthTimer > 0.09f)
            {
                mouthTimer = 0f;
                mouthOpen = !mouthOpen;
            }
            yield return null;
        }
        lineLabel.maxVisibleCharacters = total;
        mouthOpen = false;
    }

    void Chirp()
    {
        if (voice == null) return;
        float low = Wear >= Condition.Failing ? 0.6f : Wear >= Condition.Damaged ? 0.8f : 0.9f;
        float high = Wear >= Condition.Failing ? 1.15f : 1.35f;
        SoundManager.PlayOneShot(voice, "Pip Chirp", pitchScale: Random.Range(low, high), fallback: chirp);
    }

    static string Styled(string line)
    {
        var styled = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            int close = line[i] == '[' ? line.IndexOf(']', i) : -1;
            if (close > i)
            {
                styled.Append(KeyColorTag).Append(line, i, close - i + 1).Append("</color>");
                i = close;
            }
            else styled.Append(line[i]);
        }
        return styled.ToString();
    }

    void LateUpdate()
    {
        if (bubble == null) return;
        centered = Mathf.MoveTowards(centered, centerGoal, Time.unscaledDeltaTime / CenterSeconds);
        float k = Mathf.SmoothStep(0f, 1f, centered);
        bubble.anchoredPosition = homeOffset;
        bubble.localScale = Vector3.one * Mathf.Lerp(1f, CenterScale, k);
        if (k <= 0f) return;
        Canvas canvas = bubble.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var screen = (RectTransform)canvas.rootCanvas.transform;
        Rect area = screen.rect;
        Vector3 middle = screen.TransformPoint(new Vector3(area.center.x, area.yMin + area.height * CenterHeight, 0f));
        Rect size = bubble.rect;
        Vector3 target = middle + bubble.TransformVector(new Vector3(size.width * 0.5f, size.height * 0.5f, 0f));
        bubble.position = Vector3.Lerp(bubble.position, target, k);
    }

    void ShowBubble(bool show)
    {
        StopCoroutineSafe(ref bubbleRoutine);
        bubbleRoutine = StartCoroutine(FadeBubble(show ? 1f : 0f));
    }

    private Coroutine bubbleRoutine;

    IEnumerator FadeBubble(float to)
    {
        float from = bubbleGroup.alpha;
        const float seconds = 0.2f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / seconds);
            bubbleGroup.alpha = Mathf.Lerp(from, to, p);
            homeOffset = new Vector2(-(PortraitSize + BubbleGap) + (1f - bubbleGroup.alpha) * 24f * (1f - centered), 0f);
            yield return null;
        }
        bubbleGroup.alpha = to;
        homeOffset = new Vector2(-(PortraitSize + BubbleGap) + (1f - to) * 24f * (1f - centered), 0f);
        bubbleRoutine = null;
    }

    void StopCoroutineSafe(ref Coroutine routine)
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    void Update()
    {
        SoundManager.DuckTo = Online && running && present ? DuckVolume : 1f;
        if (!Online) return;

        float wanted = present && !TutorialHud.ScreenCovered ? 1f : 0f;
        shown = Mathf.MoveTowards(shown, wanted, Time.unscaledDeltaTime * 6f);
        rootGroup.alpha = shown;

        float time = Time.unscaledTime;
        face.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Sin(time * 2.6f)) * FacePixel * 0.5f - 4f);

        blinkTimer -= Time.unscaledDeltaTime;
        if (blinkTimer <= 0f)
        {
            blinkLeft = 0.12f;
            blinkTimer = Random.Range(2.2f, 4.5f);
        }
        blinkLeft -= Time.unscaledDeltaTime;

        Eyes eyes = happy ? Eyes.Happy : blinkLeft > 0f ? Eyes.Blink : Eyes.Open;
        Mouth mouth = mouthOpen ? Mouth.Open : happy ? Mouth.Smile : Mouth.Closed;
        bool bulb = Wear >= Condition.Failing ? Random.value < 0.08f
            : Wear >= Condition.Damaged ? (Mathf.Repeat(time, 1.6f) < 0.9f || running) && Random.value > 0.15f
            : Mathf.Repeat(time, 1.6f) < 0.9f || running;
        faceImage.sprite = Face(eyes, mouth, bulb);

        float flicker = Wear >= Condition.Failing ? 0.5f : Wear >= Condition.Damaged ? 0.2f : 0f;
        if (present && !noise.enabled && Random.value < flicker * Time.unscaledDeltaTime)
            StartCoroutine(GlitchRoutine(Random.Range(0.05f, 0.14f), true));

        if (noise.enabled) Static();
    }

    void Static()
    {
        var pixels = new Color32[noiseTexture.width * noiseTexture.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(0, 255);
            pixels[i] = new Color32((byte)(v * 0.5f), v, (byte)(v * 0.8f), 255);
        }
        noiseTexture.SetPixels32(pixels);
        noiseTexture.Apply(false);
    }

    static readonly string[] FaceRows =
    {
        "       ####       ",
        "      #aaaa#      ",
        "       ####       ",
        "        ##        ",
        "   ############   ",
        "  #WWWWWWWWWWWW#  ",
        " #WWkkkkkkkkkkWW# ",
        " #WkkkkkkkkkkkkW# ",
        " #WkkkkkkkkkkkkW# ",
        " #WkkkkkkkkkkkkW# ",
        " #WkpkkkkkkkkpkW# ",
        " #WkkkkkkkkkkkkW# ",
        " #WWkkkkkkkkkkWW# ",
        " #sWWWWWWWWWWWWs# ",
        "  #ssssssssssss#  ",
        "   ############   ",
    };

    static readonly Dictionary<char, Color32> FacePalette = new Dictionary<char, Color32>
    {
        { '#', new Color32(7, 9, 13, 255) },
        { 'a', new Color32(120, 255, 205, 255) },
        { 'A', new Color32(40, 110, 90, 255) },
        { 'W', new Color32(228, 234, 242, 255) },
        { 's', new Color32(140, 150, 170, 255) },
        { 'k', new Color32(14, 32, 38, 255) },
        { 'e', new Color32(115, 255, 200, 255) },
        { 'h', new Color32(255, 255, 255, 255) },
        { 'p', new Color32(255, 120, 165, 255) },
        { 'o', new Color32(255, 105, 150, 255) },
    };

    Sprite Face(Eyes eyes, Mouth mouth, bool bulb)
    {
        int key = (int)eyes * 100 + (int)mouth * 10 + (bulb ? 1 : 0);
        if (faces.TryGetValue(key, out Sprite sprite)) return sprite;

        char[][] rows = System.Array.ConvertAll(FaceRows, row => row.ToCharArray());
        void Set(int x, int row, char c) => rows[row][x] = c;

        if (!bulb)
            for (int x = 7; x <= 10; x++) Set(x, 1, 'A');

        foreach (int left in new[] { 5, 11 })
        {
            switch (eyes)
            {
                case Eyes.Open:
                    Set(left, 8, 'e'); Set(left + 1, 8, 'h');
                    Set(left, 9, 'e'); Set(left + 1, 9, 'e');
                    break;
                case Eyes.Blink:
                    Set(left, 9, 'e'); Set(left + 1, 9, 'e');
                    break;
                case Eyes.Happy:
                    Set(left, 8, 'e'); Set(left + 1, 8, 'e');
                    Set(left - 1, 9, 'e'); Set(left + 2, 9, 'e');
                    break;
            }
        }

        switch (mouth)
        {
            case Mouth.Closed:
                Set(8, 10, 'e'); Set(9, 10, 'e');
                break;
            case Mouth.Open:
                Set(8, 10, 'e'); Set(9, 10, 'e');
                Set(8, 11, 'o'); Set(9, 11, 'o');
                break;
            case Mouth.Smile:
                Set(7, 10, 'e'); Set(10, 10, 'e');
                Set(8, 11, 'e'); Set(9, 11, 'e');
                break;
        }

        string[] drawn = System.Array.ConvertAll(rows, row => new string(row));
        sprite = PixelArt.FromText(drawn, FacePalette, 100f, new Vector2(0.5f, 0.5f));
        faces[key] = sprite;
        return sprite;
    }

    void Build()
    {
        if (built) return;
        built = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        root = GameUI.NewRect("Pip", transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-Margin, -Margin);
        root.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.interactable = rootGroup.blocksRaycasts = false;

        BuildPortrait();
        BuildBubble();

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.ignoreListenerPause = true;
        chirp = Chirp("Pip Chirp", ChirpFrequency, 0.045f);
        chime = Chime();

        root.gameObject.SetActive(false);
    }

    void BuildPortrait()
    {
        Image frame = GameUI.Sliced("Portrait", root, GameUI.PanelSprite, Color.white);
        portraitFrame = GameUI.Fill(frame.rectTransform);
        portraitFrame.pivot = new Vector2(0.5f, 0.5f);

        RectTransform window = GameUI.Fill(GameUI.NewRect("Screen", portraitFrame), 15f);
        window.gameObject.AddComponent<RectMask2D>();
        RawImage backdrop = GameUI.Fill(GameUI.NewRect("Backdrop", window)).gameObject.AddComponent<RawImage>();
        backdrop.texture = PixelArt.MakeTexture(1, 32, (x, y) => Color.Lerp(ScreenBottom, ScreenTop, y / 31f));
        backdrop.texture.filterMode = FilterMode.Bilinear;
        backdrop.raycastTarget = false;

        RawImage glow = GameUI.Anchor(GameUI.NewRect("Glow", window), 0.05f, 0.1f, 0.95f, 1f).gameObject.AddComponent<RawImage>();
        glow.texture = TitleUI.PatchTexture;
        glow.color = TitleUI.Fade(PipColor, 0.16f);
        glow.material = TitleUI.Additive;
        glow.raycastTarget = false;

        face = GameUI.NewRect("Face", window);
        face.sizeDelta = new Vector2(18f * FacePixel, 16f * FacePixel);
        faceImage = face.gameObject.AddComponent<Image>();
        faceImage.raycastTarget = false;
        faceImage.sprite = Face(Eyes.Open, Mouth.Closed, true);

        noiseTexture = PixelArt.MakeTexture(40, 40, (x, y) => new Color32(0, 0, 0, 255));
        noise = GameUI.Fill(GameUI.NewRect("Static", window)).gameObject.AddComponent<RawImage>();
        noise.texture = noiseTexture;
        noise.color = new Color(1f, 1f, 1f, 0.55f);
        noise.raycastTarget = false;
        noise.enabled = false;

        RawImage lines = GameUI.Fill(GameUI.NewRect("Scanlines", window)).gameObject.AddComponent<RawImage>();
        lines.texture = GameUI.Scanlines;
        lines.uvRect = new Rect(0f, 0f, 1f, (PortraitSize - 30f) / 3f);
        lines.raycastTarget = false;

        flash = GameUI.Fill(GameUI.NewRect("Flash", portraitFrame), 12f).gameObject.AddComponent<Image>();
        flash.material = TitleUI.Additive;
        flash.color = Color.clear;
        flash.raycastTarget = false;

        Image tab = GameUI.Sliced("Name Tab", root, GameUI.TabSprite, PipColor);
        nameTab = tab.rectTransform;
        nameTab.anchorMin = nameTab.anchorMax = new Vector2(0.5f, 0f);
        nameTab.pivot = new Vector2(0.5f, 0.5f);
        nameTab.anchoredPosition = new Vector2(0f, 2f);
        nameTab.sizeDelta = new Vector2(84f, 32f);
        TextMeshProUGUI name = GameUI.Label("Name", nameTab, "PIP", 20f, GameUI.Ink, TextAlignmentOptions.Center, GameUI.Heading);
        name.characterSpacing = 14f;
        GameUI.Fill(name.rectTransform).offsetMin = new Vector2(4f, 3f);
    }

    void BuildBubble()
    {
        bubble = GameUI.NewRect("Bubble", root);
        bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(1f, 1f);
        bubble.anchoredPosition = new Vector2(-(PortraitSize + BubbleGap), 0f);
        bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;

        Image back = bubble.gameObject.AddComponent<Image>();
        back.sprite = GameUI.PanelSprite;
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 1f / GameUI.PixelSize;
        back.raycastTarget = false;
        keyEdge = bubble.gameObject.AddComponent<Outline>();
        keyEdge.effectColor = GameUI.Amber;
        keyEdge.effectDistance = new Vector2(GameUI.PixelSize, -GameUI.PixelSize);
        keyEdge.enabled = false;
        var column = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(26, 26, 18, 22);
        column.spacing = 4f;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        var fit = bubble.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bubble.sizeDelta = new Vector2(BubbleWidth, 0f);
        var minimum = bubble.gameObject.AddComponent<LayoutElement>();
        minimum.minHeight = PortraitSize * 0.72f;

        TextMeshProUGUI heading = GameUI.Label("Heading", bubble, "PIP  <color=#9aa3ad><size=16>SUIT ASSISTANT</size></color>", 22f, PipColor,
            TextAlignmentOptions.Left, GameUI.Heading);
        heading.characterSpacing = 10f;

        lineLabel = GameUI.Label("Line", bubble, "", 32f, GameUI.Text, TextAlignmentOptions.TopLeft);
        lineLabel.fontSharedMaterial = GameUI.Shadow(lineLabel.font);
        lineLabel.textWrappingMode = TextWrappingModes.Normal;
        lineLabel.lineSpacing = -8f;

        Image pointer = GameUI.Sliced("Pointer", bubble, GameUI.ArrowSprite, new Color(0.66f, 0.72f, 0.78f));
        pointer.type = Image.Type.Simple;
        pointer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform p = pointer.rectTransform;
        p.anchorMin = p.anchorMax = new Vector2(1f, 1f);
        p.pivot = new Vector2(0.5f, 0.5f);
        p.sizeDelta = new Vector2(7f, 5f) * GameUI.PixelSize;
        p.anchoredPosition = new Vector2(GameUI.PixelSize * 3.5f, -PortraitSize * 0.5f);
        p.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    static AudioClip Chirp(string clipName, float frequency, float seconds)
    {
        int samples = Mathf.CeilToInt(seconds * TitleUI.SampleRate);
        var data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            phase += 2f * Mathf.PI * frequency * (1f + 0.6f * t) / TitleUI.SampleRate;
            float envelope = Mathf.Sin(t * Mathf.PI);
            data[i] = Mathf.Clamp(Mathf.Sin(phase) * 2.2f, -1f, 1f) * envelope * 0.5f;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Chime()
    {
        float[] notes = { 784f, 988f, 1318f };
        const float noteSeconds = 0.09f;
        int perNote = Mathf.CeilToInt(noteSeconds * TitleUI.SampleRate);
        var data = new float[perNote * notes.Length + perNote * 2];
        for (int n = 0; n < notes.Length; n++)
        {
            int length = n == notes.Length - 1 ? perNote * 3 : perNote;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)TitleUI.SampleRate;
                float envelope = Mathf.Exp(-t * (n == notes.Length - 1 ? 9f : 20f));
                data[n * perNote + i] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * envelope * 0.5f;
            }
        }
        AudioClip clip = AudioClip.Create("Pip Chime", data.Length, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
