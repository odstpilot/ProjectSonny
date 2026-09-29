using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pip, the little helper built into every crew suit ("Personal Integrated Pal"). The quartermaster switches the
// technician's on when they come aboard, and from then on Pip pops up in the top right corner of the screen whenever it
// has something to say: a round little face on a green screen that bobs, blinks, and chatters, with a speech bubble of
// its own beside it. Once it's said its piece it switches itself off again, until it's needed next. Pip shows
// the technician whatever the tutorial didn't, as they need it (the map first; the inventory and pausing later), and
// unlocks each thing in the suit as it does (SuitFeatures).
//
// It talks on its own, a line at a time, and doesn't stop the player: each line is typed out with a chirp every other
// letter, held long enough to read, then the next. Tell(lines) queues lines and carries on; yield return Say(lines)
// waits until they've been said. A line starting with ^ is said happily (eyes squeezed shut, big smile), one starting
// with ~ opens with a burst of static over Pip's face (something interfering), and a key in square brackets, like [M],
// is picked out in amber. Both can go together: "~^".
// Glitch() does the static on its own, if Pip's showing.
//
// Built from code the first time something asks for it, like the tutorial HUD. It draws over the map (MapScreen), so it
// can talk the technician through it, and stays out of the way of fades and title cards. Everything runs on unscaled
// time, since the map pauses the game. One per scene: SuitHelper.Get().
public class SuitHelper : MonoBehaviour
{
    const int SortingOrder = 109;               // over the map (108), under fades and screens that take over (110)
    const float Margin = 40f;
    const float PortraitSize = 150f;
    const float BubbleWidth = 600f;
    const float BubbleGap = 18f;
    const float FacePixel = 6f;                 // canvas units per pixel of Pip's face
    const float LettersPerSecond = 38f;
    const float HoldBase = 1.6f;                // how long a line stays up once it's typed, plus a little per letter
    const float HoldPerLetter = 0.055f;
    const float LastLineLinger = 1.6f;          // how long Pip stays after the last line in the queue, before switching off
    const float SwitchTime = 0.18f;             // switching on or off, like an old screen
    const float ChirpFrequency = 880f;

    public static readonly Color PipColor = new Color(0.45f, 1f, 0.78f);
    static readonly Color ScreenTop = new Color(0.06f, 0.24f, 0.2f);
    static readonly Color ScreenBottom = new Color(0.02f, 0.07f, 0.07f);
    const string KeyColorTag = "<color=#FFAE52>";

    enum Eyes { Open, Blink, Happy }
    enum Mouth { Closed, Open, Smile }

    static SuitHelper instance;

    public bool Online { get; private set; }
    // True while there's something being said, or waiting to be.
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
    private AudioSource voice;
    private AudioClip chirp;
    private AudioClip chime;
    private AudioClip crackle;

    private readonly Queue<string> queue = new Queue<string>();
    private int queuedCount;
    private int saidCount;
    private bool running;
    private bool happy;
    private bool mouthOpen;
    private float blinkTimer = 2f;
    private float blinkLeft;
    private float shown;                        // how visible the whole thing is, 0 to 1, faded out over title cards
    private bool present;                       // switched on, and in the corner; only while it's got something to say
    private Coroutine hideRoutine;
    private readonly Dictionary<int, Sprite> faces = new Dictionary<int, Sprite>();

    public static SuitHelper Get()
    {
        if (instance == null) instance = FindAnyObjectByType<SuitHelper>();
        if (instance == null) instance = new GameObject("SuitHelper", typeof(RectTransform)).AddComponent<SuitHelper>();
        instance.Build();
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
        if (instance == this) instance = null;
        if (noiseTexture != null) Destroy(noiseTexture);
    }

    // --- Talking ---

    // Queues the lines and carries on.
    public void Tell(params string[] lines)
    {
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

    // Queues the lines and waits until they've all been said.
    public IEnumerator Say(params string[] lines)
    {
        Tell(lines);
        int until = queuedCount;
        while (saidCount < until) yield return null;
    }

    // Drops whatever hasn't been said yet; the line being said finishes.
    public void Hush()
    {
        saidCount += queue.Count;
        queue.Clear();
    }

    // Switches Pip on for the first time: static, a start-up line, then its face, and a little chime.
    public IEnumerator Boot(string bootLine = "PIP v2.3  //  BOOTING...")
    {
        if (Online) yield break;
        Online = true;
        present = true;
        root.gameObject.SetActive(true);
        noise.enabled = true;
        faceImage.enabled = false;
        nameTab.gameObject.SetActive(false);

        // The screen pops on from a bright line.
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

    // On without the start-up, for a scene that picks up after Pip's already been switched on. It stays out of sight
    // until it's told something.
    public void ComeOnline()
    {
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
            if (line.StartsWith("~"))
            {
                line = line.Substring(1);
                yield return GlitchRoutine(0.45f);
            }
            happy = line.StartsWith("^");
            if (happy) line = line.Substring(1).TrimStart();
            ShowBubble(true);
            yield return TypeLine(Styled(line), true);
            float hold = HoldBase + lineLabel.textInfo.characterCount * HoldPerLetter;
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;
            saidCount++;
        }
        happy = false;
        running = false;
        hideRoutine = StartCoroutine(HideWhenQuiet());
    }

    // Static over Pip's face for a moment, with a crackle, as if something else were on the line.
    public void Glitch(float seconds = 0.3f)
    {
        if (present) StartCoroutine(GlitchRoutine(seconds));
    }

    IEnumerator GlitchRoutine(float seconds)
    {
        if (crackle == null) crackle = TitleUI.Crackle("Pip Static", 0.4f, 0.5f);
        if (voice != null) SoundManager.PlayOneShot(voice, "Pip Static", fallback: crackle);
        noise.enabled = true;
        faceImage.enabled = false;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            // A frame of face now and then through the static.
            faceImage.enabled = Random.value < 0.2f;
            yield return null;
        }
        noise.enabled = false;
        faceImage.enabled = true;
    }

    // A moment to finish reading, then the bubble goes and Pip switches off. Anything new to say cancels it (Tell).
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

    // Pops back into the corner: out from a bright line to the full screen, with a little chirp.
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
        for (float typed = 0f; shownLetters < total; typed += LettersPerSecond * Time.unscaledDeltaTime)
        {
            int now = Mathf.Min(total, Mathf.FloorToInt(typed));
            for (int i = shownLetters; i < now; i++)
            {
                char c = lineLabel.textInfo.characterInfo[i].character;
                if (chatter && i % 2 == 0 && char.IsLetterOrDigit(c)) Chirp();
            }
            shownLetters = now;
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
        SoundManager.PlayOneShot(voice, "Pip Chirp", pitchScale: Random.Range(0.9f, 1.35f), fallback: chirp);
    }

    // [M] picked out in amber.
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
            bubble.anchoredPosition = new Vector2(-(PortraitSize + BubbleGap) + (1f - bubbleGroup.alpha) * 24f, 0f);
            yield return null;
        }
        bubbleGroup.alpha = to;
        bubble.anchoredPosition = new Vector2(-(PortraitSize + BubbleGap) + (1f - to) * 24f, 0f);
        bubbleRoutine = null;
    }

    void StopCoroutineSafe(ref Coroutine routine)
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    // --- Every frame: bobbing, blinking, static, and keeping out of the way ---

    void Update()
    {
        if (!Online) return;

        // Only there while it's got something to say, and faded out over title cards and fades to black.
        float wanted = present && !TutorialHud.ScreenCovered ? 1f : 0f;
        shown = Mathf.MoveTowards(shown, wanted, Time.unscaledDeltaTime * 6f);
        rootGroup.alpha = shown;

        float time = Time.unscaledTime;
        // A pixel up and down, like it's floating.
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
        bool bulb = Mathf.Repeat(time, 1.6f) < 0.9f || running;
        faceImage.sprite = Face(eyes, mouth, bulb);

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

    // --- Pip's face ---

    // 18 by 16: an antenna with a bulb, a round white shell, and a dark screen with the face on it. The eyes, mouth, and
    // bulb are drawn in afterwards (Face), so they can change.
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
        { 'a', new Color32(120, 255, 205, 255) },   // antenna bulb, lit
        { 'A', new Color32(40, 110, 90, 255) },     // and dark
        { 'W', new Color32(228, 234, 242, 255) },   // shell
        { 's', new Color32(140, 150, 170, 255) },   // shell, in shadow
        { 'k', new Color32(14, 32, 38, 255) },      // screen
        { 'e', new Color32(115, 255, 200, 255) },   // eyes and mouth
        { 'h', new Color32(255, 255, 255, 255) },   // a glint in each eye
        { 'p', new Color32(255, 120, 165, 255) },   // cheeks
        { 'o', new Color32(255, 105, 150, 255) },   // inside an open mouth
    };

    Sprite Face(Eyes eyes, Mouth mouth, bool bulb)
    {
        int key = (int)eyes * 100 + (int)mouth * 10 + (bulb ? 1 : 0);
        if (faces.TryGetValue(key, out Sprite sprite)) return sprite;

        char[][] rows = System.Array.ConvertAll(FaceRows, row => row.ToCharArray());
        void Set(int x, int row, char c) => rows[row][x] = c;

        if (!bulb)
            for (int x = 7; x <= 10; x++) Set(x, 1, 'A');

        // Two eyes: columns 5-6 and 11-12, rows 8-9.
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
                case Eyes.Happy:            // ^ ^
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

    // --- Building ---

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

        // A little name tab hanging off the bottom of the screen.
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

    // To the left of the portrait: a panel with a pointer toward Pip, a heading, and the line.
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

        // Pointing right, at Pip.
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

    // --- Sounds ---

    // A short upward chirp: Pip's voice, pitched a little differently each time.
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
            // A square-ish wave, for a little electronic buzz.
            data[i] = Mathf.Clamp(Mathf.Sin(phase) * 2.2f, -1f, 1f) * envelope * 0.5f;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Three notes going up: Pip's switched on.
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
