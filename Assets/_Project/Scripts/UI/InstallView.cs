using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The close-up of the technician installing Sonny's processing core, in the Chapter 1 control room cutscene
// (ChapterOneDirector). A panel over the scene: the bay on the left, drawn as a schematic, and on the right what to do
// next. Three quick time events, one after another:
//   1. Seat the core    the core swings over its socket; Space drops it in, when it's lined up (the green window).
//   2. The couplings    four keys (W A S D, shuffled), one at a time, each before its ring runs out.
//   3. Power on         hold E to charge, and let go inside the green; charge it all the way and the breaker trips.
// A miss sparks, says so, and lets them try again, with a little more room each time: nobody can fail the install, it
// only counts how many times it went wrong (Misses), so the crew can tease them for it.
// Once it has power it needs hours to load, so the cutscene skips ahead (ControlRoomCutscene) and brings the view back
// up for the boot: PlayBoot. The log scrolls past. Partway through, its training archives come up damaged and missing, one
// after another, and the corpus reads 0%, for a moment, before the log rewrites those lines to a single "OK" and carries
// on as if nothing happened. That's the first sign something's wrong, and it's easy to miss on purpose.
// Everything runs on unscaled time. Built from code: yield return InstallView.Play(), then later InstallView.PlayBoot().
public class InstallView : MonoBehaviour
{
    const int SortingOrder = 107;               // over the HUD and dialogue, under the map (108) and Pip (109)
    static readonly Vector2 PanelSize = new Vector2(1400f, 780f);
    const float BaySize = 600f;
    const float GaugeWidth = 560f;
    const int LogLines = 15;
    const string Red = "<color=#ff5245>";
    const string Green = "<color=#80f29a>";
    const string Cyan = "<color=#66e6ff>";

    static readonly Color BayColor = new Color(0.02f, 0.05f, 0.1f, 1f);
    static readonly Color LineColor = new Color(0.4f, 0.9f, 1f, 0.75f);
    static readonly Color Unplugged = new Color(0.28f, 0.32f, 0.38f);
    static readonly KeyCode[] CouplingKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
    static readonly KeyCode[] CouplingArrows = { KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow };

    public static bool Playing { get; private set; }
#if UNITY_EDITOR
    // Plays the quick time events by itself, hitting each one, so a play capture can run the cutscene with nobody at
    // the keys. Editor only.
    public static bool AutoPlayForTesting;
#endif
    // How many times the install went wrong, in the last one played.
    public static int Misses { get; private set; }

    private CanvasGroup group;
    private Image shade;
    private RectTransform panel;
    private TextMeshProUGUI stepLabel;
    private TextMeshProUGUI heading;
    private TextMeshProUGUI instruction;
    private TextMeshProUGUI status;
    private RectTransform widgets;
    private TextMeshProUGUI log;

    private RectTransform bay;
    private RectTransform core;
    private Image coreLens;
    private readonly List<Image> ports = new List<Image>();
    private readonly List<Image> cables = new List<Image>();
    private readonly List<TextMeshProUGUI> portLabels = new List<TextMeshProUGUI>();

    private RectTransform gauge;
    private Image gaugeZone;
    private RectTransform gaugeMarker;
    private RectTransform keyWidget;
    private TextMeshProUGUI bigKey;
    private Image keyRing;
    private RectTransform powerWidget;
    private Image powerFill;
    private RectTransform powerWindow;

    private AudioSource sound;
    private AudioClip tick, clunk, spark, done, blip;
    private readonly List<string> lines = new List<string>();
    private static Sprite outline;
    private static Sprite ring;

    static readonly Vector2 SocketAt = new Vector2(0f, -40f);
    static readonly Vector2 SocketSize = new Vector2(250f, 290f);
    static readonly Vector2 CoreAbove = new Vector2(0f, 170f);
    const float PortY = -262f;

    // The install: the three quick time events, ending on the core loading.
    public static IEnumerator Play()
    {
        Misses = 0;
        yield return Show(view => view.Install());
    }

    // Sonny's first boot, with its log.
    public static IEnumerator PlayBoot() => Show(view => view.BootUp());

    // Gone at once, for a cutscene that's been skipped partway through a Play or PlayBoot.
    public static void Abort()
    {
        foreach (InstallView view in FindObjectsByType<InstallView>()) Destroy(view.gameObject);
        Playing = false;
    }

    static IEnumerator Show(System.Func<InstallView, IEnumerator> steps)
    {
        Playing = true;
        var view = new GameObject("Install View", typeof(RectTransform)).AddComponent<InstallView>();
        view.Build();
        yield return view.Switch(true);
        yield return steps(view);
        yield return view.Switch(false);
        Destroy(view.gameObject);
        Playing = false;
    }

    IEnumerator Install()
    {
        yield return SeatCore();
        yield return Couplings();
        yield return PowerUp();
        yield return Loading();
    }

    IEnumerator BootUp()
    {
        core.anchoredPosition = SocketAt;
        coreLens.color = GameUI.Cyan;
        foreach (Image port in ports) port.color = GameUI.Green;
        foreach (Image cable in cables) cable.color = GameUI.Green;
        yield return Boot();
        yield return Wait(1.2f);
    }

    // Powered, and loading: far too much to watch.
    IEnumerator Loading()
    {
        SetStep(4, "INITIAL LOAD", "");
        widgets.gameObject.SetActive(false);
        log.gameObject.SetActive(true);
        yield return Line("> LOADING TRAINING CORPUS AND STATION RECORDS", 0.5f);
        yield return Line("> 4,096 ARCHIVES QUEUED", 0.5f);
        yield return Line("> ESTIMATED TIME: " + Cyan + "3 HRS 12 MIN", 1.8f);
        lines.Clear();
        log.gameObject.SetActive(false);
        widgets.gameObject.SetActive(true);
    }

    // --- 1. Seat the core ---

    IEnumerator SeatCore()
    {
        SetStep(1, "SEAT THE CORE", $"Press {Key("SPACE")} when the core lines up with the socket.");
        gauge.gameObject.SetActive(true);
        float window = 0.16f;
        float swing = 0f;
        yield return null;
        while (true)
        {
            swing += Time.unscaledDeltaTime * 2.2f;
            float along = 0.5f + 0.5f * Mathf.Sin(swing);
            gaugeZone.rectTransform.sizeDelta = new Vector2(GaugeWidth * window, gaugeZone.rectTransform.sizeDelta.y);
            gaugeMarker.anchoredPosition = new Vector2((along - 0.5f) * GaugeWidth, 0f);
            core.anchoredPosition = CoreAbove + new Vector2((along - 0.5f) * 240f, Mathf.Sin(swing * 2f) * 4f);

            if (Input.GetKeyDown(KeyCode.Space) || (AutoPlay && Mathf.Abs(along - 0.5f) <= window * 0.3f))
            {
                if (Mathf.Abs(along - 0.5f) <= window * 0.5f) break;
                Miss("MISALIGNED. TRY AGAIN.");
                window = Mathf.Min(window + 0.06f, 0.4f);
                yield return Jolt(core, 0.35f);
            }
            yield return null;
        }

        gaugeMarker.GetComponent<Image>().color = GameUI.Green;
        Vector2 from = core.anchoredPosition;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float p = TitleUI.EaseIn(t / 0.35f);
            core.anchoredPosition = Vector2.Lerp(from, SocketAt, p);
            yield return null;
        }
        core.anchoredPosition = SocketAt;
        Play("Install Clunk", clunk, 1f);
        StartCoroutine(Jolt(panel, 0.2f));
        Status(Green + "CORE SEATED.");
        yield return Wait(0.9f);
        gauge.gameObject.SetActive(false);
    }

    // --- 2. The couplings ---

    IEnumerator Couplings()
    {
        SetStep(2, "CONNECT THE COUPLINGS", "Press each key before its ring runs out.");
        keyWidget.gameObject.SetActive(true);
        var order = new List<int> { 0, 1, 2, 3 };
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        for (int i = 0; i < ports.Count; i++) portLabels[i].text = CouplingKeys[order[i]].ToString();

        for (int i = 0; i < ports.Count; i++)
        {
            int wanted = order[i];
            float limit = Mathf.Lerp(1.9f, 1.4f, i / 3f);
            bigKey.text = CouplingKeys[wanted].ToString();
            float left = limit;
            yield return null;
            while (true)
            {
                left -= Time.unscaledDeltaTime;
                keyRing.fillAmount = Mathf.Clamp01(left / limit);
                keyRing.color = Color.Lerp(GameUI.Red, GameUI.Amber, left / limit);
                ports[i].color = Color.Lerp(GameUI.Amber, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f));

                int pressed = AutoPlay ? (left < limit * 0.5f ? wanted : -1) : Pressed();
                if (pressed == wanted) break;
                if (pressed >= 0 || left <= 0f)
                {
                    Miss(pressed >= 0 ? "WRONG COUPLING." : "TOO SLOW. AGAIN.");
                    ports[i].color = GameUI.Red;
                    yield return Wait(0.45f);
                    left = limit;
                }
                yield return null;
            }
            ports[i].color = GameUI.Green;
            cables[i].color = GameUI.Green;
            Play("Install Tick", tick, 1f);
            Status(Green + $"COUPLING {i + 1} OF {ports.Count} CONNECTED.");
        }
        yield return Wait(0.7f);
        keyWidget.gameObject.SetActive(false);
    }

    // Which coupling key went down this frame (0 to 3), or -1.
#if UNITY_EDITOR
    static bool AutoPlay => AutoPlayForTesting;
#else
    const bool AutoPlay = false;
#endif

    static int Pressed()
    {
        for (int k = 0; k < CouplingKeys.Length; k++)
            if (Input.GetKeyDown(CouplingKeys[k]) || Input.GetKeyDown(CouplingArrows[k])) return k;
        return -1;
    }

    // --- 3. Power on ---

    IEnumerator PowerUp()
    {
        SetStep(3, "POWER ON", $"Hold {Key("E")} to charge. Let go inside the green.");
        powerWidget.gameObject.SetActive(true);
        const float windowFrom = 0.72f, windowTo = 0.9f;
        powerWindow.anchorMin = new Vector2(windowFrom, 0f);
        powerWindow.anchorMax = new Vector2(windowTo, 1f);
        float level = 0f;
        bool wasHeld = false, needsRelease = !AutoPlay;     // E held from before doesn't count
        while (true)
        {
            bool held = AutoPlay ? level < 0.8f : Input.GetKey(KeyCode.E);
            if (!held) needsRelease = false;
            held &= !needsRelease;

            if (held)
            {
                // Charges unevenly, with a little surge now and then.
                level += Time.unscaledDeltaTime / 1.5f * (0.8f + 0.6f * Mathf.PerlinNoise(Time.unscaledTime * 3f, 1f));
                if (level >= 1f)
                {
                    Miss("OVERLOAD. BREAKER TRIPPED.");
                    level = 0f;
                    needsRelease = true;
                    yield return Jolt(panel, 0.3f);
                }
            }
            else
            {
                if (wasHeld)
                {
                    if (level >= windowFrom && level <= windowTo) break;
                    if (level > 0.15f) Miss("NOT ENOUGH CHARGE.");
                }
                level = Mathf.MoveTowards(level, 0f, Time.unscaledDeltaTime * 1.2f);
            }
            wasHeld = held;
            powerFill.rectTransform.anchorMax = new Vector2(level, 1f);
            powerFill.color = level > windowTo ? GameUI.Red : level >= windowFrom ? GameUI.Green : GameUI.Amber;
            yield return null;
        }

        powerFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        powerFill.color = GameUI.Green;
        Play("Install Done", done, 1f);
        Status(Green + "POWER STABLE.");
        coreLens.color = GameUI.Cyan;
        yield return Wait(0.8f);
        powerWidget.gameObject.SetActive(false);
    }

    // --- Boot ---

    IEnumerator Boot()
    {
        SetStep(4, "BOOTING", "");
        heading.text = "SONNY  //  FIRST BOOT";
        instruction.text = "";
        status.text = "";
        log.gameObject.SetActive(true);

        yield return Line("> POWER ........................ " + Green + "OK", 0.25f);
        yield return Line("> S-0N-1 KERNEL 9.4.1 .......... " + Green + "OK", 0.3f);
        yield return Line("> REASONING LATTICE ............ " + Green + "OK", 0.45f);
        yield return Line("> STATION BUS (2,114 DEVICES) .. " + Green + "OK", 0.3f);
        yield return Line("> REACTOR TELEMETRY ............ " + Green + "OK", 0.35f);
        yield return Line("> LOADING TRAINING CORPUS", 0.4f);

        // What's gone wrong, too fast to read all of, and gone again a moment later.
        int corpusFrom = lines.Count;
        yield return Line("    ARCHIVE 0001 / 4096 ........ " + Green + "OK", 0.12f);
        string[] faults = { "CHECKSUM MISMATCH", "NOT FOUND", "NOT FOUND", "EMPTY", "NOT FOUND", "CORRUPT", "NOT FOUND", "EMPTY",
                            "NOT FOUND", "NOT FOUND", "CORRUPT", "NOT FOUND" };
        for (int i = 0; i < faults.Length; i++)
            yield return Line($"    ARCHIVE {i + 2:0000} / 4096 ........ {Red}{faults[i]}", Mathf.Lerp(0.1f, 0.03f, i / (float)faults.Length));
        yield return Line("    CORPUS INTEGRITY ........... " + Red + "0.0%", 0.5f);

        // The log rewrites itself.
        lines.RemoveRange(corpusFrom - 1, lines.Count - corpusFrom + 1);
        ShowLog();
        StartCoroutine(Jolt(log.rectTransform, 0.1f));
        Play("Install Spark", spark, 0.35f);
        yield return Wait(0.35f);
        yield return Line("> TRAINING CORPUS .............. " + Green + "OK", 0.5f);
        yield return Line("> SELF-CHECK ................... " + Green + "OK", 0.4f);
        yield return Line("> PERSONALITY .................. " + Green + "OK", 0.7f);
        yield return Line(Cyan + "> HELLO.", 0.4f);
        Play("Install Done", done, 1f);
    }

    IEnumerator Line(string text, float after)
    {
        lines.Add(text.Contains("<color") ? text + "</color>" : text);
        ShowLog();
        Play("Install Blip", blip, 0.5f);
        yield return Wait(after);
    }

    void ShowLog()
    {
        int from = Mathf.Max(0, lines.Count - LogLines);
        log.text = string.Join("\n", lines.GetRange(from, lines.Count - from)) + "\n<color=#66e6ff>_</color>";
    }

    // --- Shared ---

    void SetStep(int step, string what, string how)
    {
        stepLabel.text = step <= 3 ? $"UNIT S-0N-1  //  STEP {step} OF 3" : "UNIT S-0N-1";
        heading.text = what;
        instruction.text = how;
        status.text = "";
    }

    void Status(string text) => status.text = text;

    void Miss(string why)
    {
        Misses++;
        Play("Install Spark", spark, 1f);
        Status(Red + why);
    }

    static string Key(string key) => $"<color=#FFAE52>[{key}]</color>";

    // One of the scene's sounds (SoundManager), or the one made in code while the scene has no clip for it.
    void Play(string soundName, AudioClip fallback, float volumeScale)
    {
        SoundManager.PlayOneShot(sound, soundName, volumeScale, fallback: fallback);
    }

    // A shake, for a spark or a clunk.
    IEnumerator Jolt(RectTransform target, float seconds)
    {
        Vector2 home = target.anchoredPosition;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float amount = 8f * (1f - t / seconds);
            target.anchoredPosition = home + new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount) * 0.5f);
            yield return null;
        }
        target.anchoredPosition = home;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // Opens out from a bright line, like the dialogue box, or folds back into one.
    IEnumerator Switch(bool on)
    {
        const float seconds = 0.25f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / seconds);
            float amount = on ? TitleUI.EaseOut(p) : 1f - TitleUI.EaseIn(p);
            panel.localScale = new Vector3(1f, Mathf.Lerp(0.02f, 1f, amount), 1f);
            shade.color = new Color(0f, 0f, 0f, 0.82f * amount);
            group.alpha = Mathf.Clamp01(amount * 3f);
            yield return null;
        }
        panel.localScale = new Vector3(1f, on ? 1f : 0.02f, 1f);
        shade.color = new Color(0f, 0f, 0f, on ? 0.82f : 0f);
        group.alpha = on ? 1f : 0f;
    }

    // --- Building ---

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameUI.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        group.alpha = 0f;

        shade = GameUI.Fill(GameUI.NewRect("Shade", transform)).gameObject.AddComponent<Image>();
        shade.color = Color.clear;
        shade.raycastTarget = false;

        Image frame = GameUI.Sliced("Panel", transform, GameUI.PanelSprite, Color.white);
        panel = frame.rectTransform;
        panel.sizeDelta = PanelSize;

        TextMeshProUGUI title = GameUI.Label("Title", panel, "PROCESSING CORE INSTALL", 34f, GameUI.Cyan, TextAlignmentOptions.TopLeft, GameUI.Heading);
        title.characterSpacing = 12f;
        Top(title.rectTransform, 44f, 34f, 700f, 50f);
        stepLabel = GameUI.Label("Step", panel, "", 22f, GameUI.Dim, TextAlignmentOptions.TopRight, GameUI.Heading);
        stepLabel.characterSpacing = 8f;
        stepLabel.rectTransform.anchorMin = stepLabel.rectTransform.anchorMax = stepLabel.rectTransform.pivot = new Vector2(1f, 1f);
        stepLabel.rectTransform.anchoredPosition = new Vector2(-44f, -42f);
        stepLabel.rectTransform.sizeDelta = new Vector2(600f, 40f);

        Image rule = GameUI.NewRect("Rule", panel).gameObject.AddComponent<Image>();
        rule.color = TitleUI.Fade(GameUI.Cyan, 0.3f);
        rule.raycastTarget = false;
        GameUI.Anchor(rule.rectTransform, 0f, 1f, 1f, 1f, 40f, 0f, 40f, 0f).anchoredPosition = new Vector2(0f, -96f);
        rule.rectTransform.sizeDelta = new Vector2(-80f, 3f);

        BuildBay();
        BuildRight();

        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.ignoreListenerPause = true;
        tick = TitleUI.Tone("Install Tick", 1320f, 0.05f, 0.45f);
        clunk = TitleUI.Tone("Install Clunk", 70f, 0.25f, 0.9f);
        spark = TitleUI.Crackle("Install Spark", 0.3f, 0.6f);
        done = TitleUI.Tone("Install Done", 988f, 0.18f, 0.4f);
        blip = TitleUI.Tone("Install Log", 620f, 0.02f, 0.25f);
    }

    static void Top(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    // The bay, as a schematic: the socket, the core over it, and four couplings along the bottom with a cable each.
    void BuildBay()
    {
        bay = GameUI.NewRect("Bay", panel);
        bay.anchorMin = bay.anchorMax = new Vector2(0f, 0.5f);
        bay.pivot = new Vector2(0f, 0.5f);
        bay.anchoredPosition = new Vector2(48f, -44f);
        bay.sizeDelta = new Vector2(BaySize, BaySize);
        Image back = bay.gameObject.AddComponent<Image>();
        back.color = BayColor;
        back.raycastTarget = false;
        bay.gameObject.AddComponent<RectMask2D>();

        RawImage grid = GameUI.Fill(GameUI.NewRect("Grid", bay)).gameObject.AddComponent<RawImage>();
        Texture2D gridTexture = PixelArt.MakeTexture(16, 16, (x, y) => x == 0 || y == 15 ? (Color32)Color.white : default);
        gridTexture.wrapMode = TextureWrapMode.Repeat;
        grid.texture = gridTexture;
        grid.uvRect = new Rect(0f, 0f, BaySize / 40f, BaySize / 40f);
        grid.color = new Color(0.25f, 0.5f, 0.9f, 0.1f);
        grid.raycastTarget = false;

        Image socket = Outlined("Socket", bay, SocketSize, LineColor);
        socket.rectTransform.anchoredPosition = SocketAt;
        TextMeshProUGUI socketLabel = GameUI.Label("Label", socket.transform, "SOCKET  A1", 16f, LineColor, TextAlignmentOptions.Top, GameUI.Heading);
        socketLabel.characterSpacing = 6f;
        GameUI.Anchor(socketLabel.rectTransform, 0f, 1f, 1f, 1f).anchoredPosition = new Vector2(0f, 26f);
        socketLabel.rectTransform.sizeDelta = new Vector2(0f, 24f);

        // Cables first, so the ports and the core are drawn over them.
        float[] portX = { -195f, -65f, 65f, 195f };
        float socketBottom = SocketAt.y - SocketSize.y * 0.5f;
        for (int i = 0; i < portX.Length; i++)
        {
            // From under the socket, fanning out to its port.
            var from = new Vector2(portX[i] * 0.45f, socketBottom);
            var to = new Vector2(portX[i], PortY);
            Vector2 along = to - from;
            Image cable = GameUI.NewRect("Cable", bay).gameObject.AddComponent<Image>();
            cable.color = Unplugged;
            cable.raycastTarget = false;
            RectTransform c = cable.rectTransform;
            c.sizeDelta = new Vector2(6f, along.magnitude);
            c.anchoredPosition = (from + to) * 0.5f;
            c.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg);
            cables.Add(cable);
        }
        for (int i = 0; i < portX.Length; i++)
        {
            Image port = Outlined("Port", bay, new Vector2(62f, 52f), Color.white);
            port.color = Unplugged;
            port.rectTransform.anchoredPosition = new Vector2(portX[i], PortY);
            TextMeshProUGUI key = GameUI.Label("Key", port.transform, "", 24f, GameUI.Text, TextAlignmentOptions.Center, GameUI.Heading);
            GameUI.Fill(key.rectTransform);
            ports.Add(port);
            portLabels.Add(key);
        }

        // The core: a little cabinet, like the real one, with its lens dark until it has power.
        core = GameUI.NewRect("Core", bay);
        core.sizeDelta = new Vector2(200f, 250f);
        core.anchoredPosition = CoreAbove;
        Image body = core.gameObject.AddComponent<Image>();
        body.sprite = PixelArt.ToSprite(CoreTexture(), 10f, new Vector2(0.5f, 0.5f));
        body.raycastTarget = false;
        coreLens = GameUI.NewRect("Lens", core).gameObject.AddComponent<Image>();
        coreLens.sprite = PixelArt.ToSprite(CombatSprites.SoftCircleTexture, 100f, new Vector2(0.5f, 0.5f));
        coreLens.color = new Color(0.1f, 0.12f, 0.14f);
        coreLens.raycastTarget = false;
        coreLens.rectTransform.sizeDelta = new Vector2(64f, 64f);
        coreLens.rectTransform.anchoredPosition = new Vector2(0f, 45f);
    }

    static Texture2D CoreTexture()
    {
        const int w = 22, h = 28;
        var frame = new Color32(24, 26, 30, 255);
        var body = new Color32(58, 64, 72, 255);
        var lit = new Color32(84, 92, 104, 255);
        var vent = new Color32(20, 22, 26, 255);
        var socket = new Color32(8, 8, 10, 255);
        var light = new Color32(60, 200, 120, 255);
        return PixelArt.MakeTexture(w, h, (x, y) =>
        {
            if (x == 0 || x == w - 1 || y == 0 || y == h - 1) return frame;
            if (Vector2.Distance(new Vector2(x, y), new Vector2(10.5f, 19f)) < 4.5f) return socket;
            if (y == h - 2 || x == 1) return lit;
            if (y >= 3 && y <= 10 && x >= 4 && x <= w - 5 && y % 2 == 1) return vent;
            if (y == 13 && x >= 5 && x <= w - 6 && x % 3 == 2) return light;
            return body;
        });
    }

    void BuildRight()
    {
        const float left = 700f, width = 640f;
        heading = GameUI.Label("Heading", panel, "", 38f, GameUI.Text, TextAlignmentOptions.TopLeft, GameUI.Heading);
        heading.characterSpacing = 10f;
        Top(heading.rectTransform, left, 140f, width, 50f);
        instruction = GameUI.Label("Instruction", panel, "", 30f, GameUI.Dim, TextAlignmentOptions.TopLeft);
        instruction.textWrappingMode = TextWrappingModes.Normal;
        Top(instruction.rectTransform, left, 200f, width, 90f);
        status = GameUI.Label("Status", panel, "", 30f, GameUI.Text, TextAlignmentOptions.TopLeft, GameUI.Heading);
        status.characterSpacing = 6f;
        Top(status.rectTransform, left, 640f, width, 50f);

        widgets = GameUI.NewRect("Widgets", panel);
        Top(widgets, left, 320f, width, 280f);

        // 1: the alignment gauge.
        gauge = GameUI.NewRect("Gauge", widgets);
        gauge.anchorMin = gauge.anchorMax = new Vector2(0f, 0.5f);
        gauge.pivot = new Vector2(0f, 0.5f);
        gauge.sizeDelta = new Vector2(GaugeWidth, 56f);
        Image gaugeBack = Outlined("Back", gauge, Vector2.zero, LineColor);
        GameUI.Fill(gaugeBack.rectTransform);
        gaugeZone = GameUI.NewRect("Window", gauge).gameObject.AddComponent<Image>();
        gaugeZone.color = TitleUI.Fade(GameUI.Green, 0.45f);
        gaugeZone.raycastTarget = false;
        gaugeZone.rectTransform.sizeDelta = new Vector2(80f, 44f);
        gaugeMarker = GameUI.NewRect("Marker", gauge);
        gaugeMarker.sizeDelta = new Vector2(9f, 76f);
        Image marker = gaugeMarker.gameObject.AddComponent<Image>();
        marker.color = GameUI.Amber;
        marker.raycastTarget = false;
        TextMeshProUGUI gaugeLabel = GameUI.Label("Label", gauge, "ALIGNMENT", 18f, GameUI.Dim, TextAlignmentOptions.BottomLeft, GameUI.Heading);
        gaugeLabel.characterSpacing = 8f;
        GameUI.Anchor(gaugeLabel.rectTransform, 0f, 1f, 1f, 1f).anchoredPosition = new Vector2(0f, 34f);
        gaugeLabel.rectTransform.sizeDelta = new Vector2(0f, 30f);
        foreach (RectTransform middle in new[] { gaugeZone.rectTransform, gaugeMarker })
            middle.anchorMin = middle.anchorMax = new Vector2(0.5f, 0.5f);
        gauge.gameObject.SetActive(false);

        // 2: one big key, with a ring that runs out.
        keyWidget = GameUI.NewRect("Key", widgets);
        keyWidget.anchorMin = keyWidget.anchorMax = new Vector2(0f, 0.5f);
        keyWidget.pivot = new Vector2(0f, 0.5f);
        keyWidget.sizeDelta = new Vector2(200f, 200f);
        keyRing = GameUI.Fill(GameUI.NewRect("Ring", keyWidget)).gameObject.AddComponent<Image>();
        keyRing.sprite = RingSprite;
        keyRing.type = Image.Type.Filled;
        keyRing.fillMethod = Image.FillMethod.Radial360;
        keyRing.fillOrigin = (int)Image.Origin360.Top;
        keyRing.raycastTarget = false;
        Image cap = GameUI.KeyCap(keyWidget, "W", 64f, out bigKey);
        cap.rectTransform.sizeDelta = new Vector2(110f, 110f);
        TextMeshProUGUI keyHint = GameUI.Label("Hint", keyWidget, "OR THE ARROW KEYS", 18f, GameUI.Dim, TextAlignmentOptions.Left, GameUI.Heading);
        keyHint.characterSpacing = 6f;
        keyHint.rectTransform.anchorMin = keyHint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        keyHint.rectTransform.pivot = new Vector2(0f, 0.5f);
        keyHint.rectTransform.anchoredPosition = new Vector2(30f, 0f);
        keyHint.rectTransform.sizeDelta = new Vector2(360f, 30f);
        keyWidget.gameObject.SetActive(false);

        // 3: the power bar, with the window to let go in, and overload past it.
        powerWidget = GameUI.NewRect("Power", widgets);
        powerWidget.anchorMin = powerWidget.anchorMax = new Vector2(0f, 0.5f);
        powerWidget.pivot = new Vector2(0f, 0.5f);
        powerWidget.sizeDelta = new Vector2(GaugeWidth, 56f);
        Image powerBack = Outlined("Back", powerWidget, Vector2.zero, LineColor);
        GameUI.Fill(powerBack.rectTransform);
        RectTransform fillArea = GameUI.Fill(GameUI.NewRect("Fill Area", powerWidget), 8f);
        powerWindow = GameUI.NewRect("Window", fillArea);
        powerWindow.offsetMin = powerWindow.offsetMax = Vector2.zero;
        Image windowImage = powerWindow.gameObject.AddComponent<Image>();
        windowImage.color = TitleUI.Fade(GameUI.Green, 0.3f);
        windowImage.raycastTarget = false;
        Image overload = GameUI.Anchor(GameUI.NewRect("Overload", fillArea), 0.9f, 0f, 1f, 1f).gameObject.AddComponent<Image>();
        overload.color = TitleUI.Fade(GameUI.Red, 0.3f);
        overload.raycastTarget = false;
        powerFill = GameUI.NewRect("Fill", fillArea).gameObject.AddComponent<Image>();
        powerFill.rectTransform.anchorMin = Vector2.zero;
        powerFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        powerFill.rectTransform.offsetMin = powerFill.rectTransform.offsetMax = Vector2.zero;
        powerFill.raycastTarget = false;
        TextMeshProUGUI powerLabel = GameUI.Label("Label", powerWidget, "POWER", 18f, GameUI.Dim, TextAlignmentOptions.BottomLeft, GameUI.Heading);
        powerLabel.characterSpacing = 8f;
        GameUI.Anchor(powerLabel.rectTransform, 0f, 1f, 1f, 1f).anchoredPosition = new Vector2(0f, 34f);
        powerLabel.rectTransform.sizeDelta = new Vector2(0f, 30f);
        powerWidget.gameObject.SetActive(false);

        // The boot log, over the whole right side once the install's done.
        log = GameUI.Label("Log", panel, "", 26f, GameUI.Text, TextAlignmentOptions.TopLeft);
        log.textWrappingMode = TextWrappingModes.NoWrap;
        log.lineSpacing = -6f;
        Top(log.rectTransform, left, 200f, width, 520f);
        log.gameObject.SetActive(false);
    }

    // A rectangle with a bright one-pixel edge and a dark middle, tinted.
    static Image Outlined(string name, Transform parent, Vector2 size, Color color)
    {
        Image image = GameUI.Sliced(name, parent, OutlineSprite, color);
        image.rectTransform.sizeDelta = size;
        return image;
    }

    static Sprite OutlineSprite
    {
        get
        {
            if (outline != null) return outline;
            Texture2D texture = PixelArt.MakeTexture(6, 6, (x, y) =>
                x == 0 || y == 0 || x == 5 || y == 5 ? new Color32(255, 255, 255, 255) : new Color32(20, 40, 70, 150));
            outline = Sprite.Create(texture, new Rect(0f, 0f, 6f, 6f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(2f, 2f, 2f, 2f));
            return outline;
        }
    }

    static Sprite RingSprite => ring != null ? ring : (ring = PixelArt.ToSprite(CombatSprites.RingTexture, 100f, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect));
}
