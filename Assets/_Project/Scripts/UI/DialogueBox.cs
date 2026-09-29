using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A conversation with someone aboard, opened by walking up to them and pressing E (CrewMember). Built from code the first
// time it's needed, so there's no canvas to set up. It's drawn as the station's own voice log, the same one Sonny will
// later be listening in on:
//  - a portrait window on the left with the speaker's live sprite in it (breathing, turning), on a CRT with scanlines
//    and a line of interference rolling down it
//  - their name on a tab in their own color, with their job beside it, and a voice meter that jumps while they talk
//  - the line typed out a letter at a time, with a blip per letter pitched to their voice, pausing on punctuation
//  - E (or Space or Enter) finishes the line, then moves on; a counter shows how far through they are
//  - now and then the technician gets to answer: a few replies to pick from (W and S, or 1 to 3, then E), and the
//    technician says the one picked, in their own voice and portrait, before the other goes on
// It opens like an old monitor switching on, from a bright line out to full height, and closes the same way. The player
// stands still while it's up, and it's cut off if they end up away from whoever they were talking to. Everything runs on
// unscaled time. One per scene: DialogueBox.Get().
//
// A script is a list of lines, each one box:
//   Some words.                        said by whoever the player is talking to
//   > Some words.                      said by the technician
//   * A reply. = An answer. = More.    one reply the player can pick. Lines in a row starting with * are one choice
//                                      (three at most); after = come the other one's answers to it, a box each.
public class DialogueBox : MonoBehaviour
{
    public class Speaker
    {
        public string name = "";
        public string role = "";
        public Color color = GameUI.Amber;          // their name tab
        public SpriteRenderer portrait;             // shown live in the window; null leaves it empty
        public Color trim = new Color(0f, 0f, 0f, 0f);  // swapped in for the art's red trim, as on the crew
        public float voicePitch = 1f;
        [System.NonSerialized] public Transform anchor;  // where they stand; the talk's cut off if the player ends up far away
    }

    // One box of a script, or a choice of replies.
    class Beat
    {
        public bool technician;
        public string text;
        public List<Reply> replies;             // not null for a choice
    }

    class Reply
    {
        public string said;
        public List<string> answers = new List<string>();
    }

    const int SortingOrder = 95;                // over the tutorial HUD (90), under lockers and interact prompts
    const float Width = 1360f;
    const float Height = 262f;
    const float PortraitSize = 262f;
    const float Gap = 16f;
    const float TextPanelHeight = 226f;
    const float LettersPerSecond = 40f;
    const float OpenTime = 0.18f;
    const float CloseTime = 0.14f;
    const float BlipFrequency = 540f;
    const int MaxReplies = 3;
    const float ChoiceRowHeight = 38f;
    const float CutOffDistance = 4f;            // how far from the speaker the player can end up before the talk's cut off
    static readonly Color TechnicianColor = new Color(0.86f, 0.42f, 0.33f);

    static readonly Color PortraitTop = new Color(0.07f, 0.2f, 0.25f);
    static readonly Color PortraitBottom = new Color(0.02f, 0.05f, 0.07f);

    static DialogueBox instance;
    static int closedFrame = -10;

    // Open, or closed so recently that the key that closed it is still down this frame. Anything else that answers to E
    // should wait while this is true, so one press doesn't close a conversation and open something else.
    public static bool Busy => (instance != null && instance.open) || Time.frameCount <= closedFrame + 1;

    private bool built;
    private bool open;
    private int openedFrame;
    private CanvasGroup group;
    private RectTransform box;
    private RawImage shade;
    private Image flash;
    private RawImage portrait;
    private RectTransform portraitWindow;
    private RectTransform interference;
    private Image nameTab;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI roleLabel;
    private TextMeshProUGUI lineLabel;
    private TextMeshProUGUI counter;
    private TextMeshProUGUI nextLabel;
    private RectTransform next;
    private RectTransform nextArrow;
    private Image recDot;
    private readonly List<RectTransform> meterBars = new List<RectTransform>();
    private Material portraitMaterial;
    private AudioSource voice;
    private AudioClip blip;

    private readonly TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[MaxReplies];
    private readonly Image[] choiceBars = new Image[MaxReplies];
    private AudioClip tick;

    private Speaker crew;
    private Speaker technician;
    private Speaker current;
    private List<Beat> beats;
    private bool hasChoices;
    private int spoken;
    private int spokenTotal;
    private System.Action<bool> closed;
    private bool switchedOn;
    private bool typing;
    private bool skipTyping;
    private bool cutOff;
    private PlayerController player;

    public static DialogueBox Get()
    {
        if (instance == null) instance = FindAnyObjectByType<DialogueBox>();
        if (instance == null) instance = new GameObject("DialogueBox", typeof(RectTransform)).AddComponent<DialogueBox>();
        instance.Build();
        return instance;
    }

    public static bool IsOpen => instance != null && instance.open;
    // Which reply was picked at the last choice in the conversation open now or last closed, counted from 0; -1 if it
    // had none. For a script that goes one way or another depending on what the technician said.
    public static int LastPick { get; private set; } = -1;

    void Awake()
    {
        if (instance == null) instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (portraitMaterial != null) Destroy(portraitMaterial);
        if (open && player != null) player.ClearScriptedInput();
    }

    // Plays the script (see the top of this file) with who, and calls onClosed when it's over: true if it was seen to the
    // end, false if it was cut off. Does nothing if it's already open.
    public void Open(Speaker who, IList<string> script, System.Action<bool> onClosed = null)
    {
        if (open || script == null) return;
        List<Beat> parsed = Parse(script);
        if (parsed.Count == 0) return;
        open = true;
        cutOff = false;
        switchedOn = false;
        openedFrame = Time.frameCount;
        crew = who ?? new Speaker();
        beats = parsed;
        closed = onClosed;
        hasChoices = beats.Exists(beat => beat.replies != null);
        spoken = 0;
        spokenTotal = beats.Count;
        LastPick = -1;

        GameObject found = GameObject.FindWithTag("Player");
        player = found != null ? found.GetComponent<PlayerController>() : null;
        if (player != null) player.SetScriptedInput(Vector2.zero, false);
        technician = new Speaker
        {
            name = "Technician",
            role = "You",
            color = TechnicianColor,
            portrait = found != null ? found.GetComponentInChildren<SpriteRenderer>() : null,
            voicePitch = 1.05f,
        };

        ShowSpeaker(crew);
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    static List<Beat> Parse(IList<string> script)
    {
        var parsed = new List<Beat>();
        Beat choice = null;
        foreach (string raw in script)
        {
            string line = raw != null ? raw.Trim() : "";
            if (line.Length == 0) continue;
            if (line.StartsWith("*"))
            {
                if (choice == null || choice.replies.Count == MaxReplies)
                {
                    if (choice != null) Debug.LogWarning($"DialogueBox: a choice can have {MaxReplies} replies at most; \"{line}\" starts another.");
                    choice = new Beat { replies = new List<Reply>() };
                    parsed.Add(choice);
                }
                string[] parts = line.Substring(1).Split('=');
                var reply = new Reply { said = parts[0].Trim() };
                for (int i = 1; i < parts.Length; i++)
                    if (parts[i].Trim().Length > 0) reply.answers.Add(parts[i].Trim());
                choice.replies.Add(reply);
                continue;
            }
            choice = null;
            if (line.StartsWith(">")) parsed.Add(new Beat { technician = true, text = line.Substring(1).Trim() });
            else parsed.Add(new Beat { text = line });
        }
        return parsed;
    }

    IEnumerator Run()
    {
        for (int i = 0; i < beats.Count; i++)
        {
            Beat beat = beats[i];
            bool lastBeat = i == beats.Count - 1;
            spoken = i + 1;
            if (beat.replies == null)
            {
                yield return Say(beat.technician ? technician : crew, beat.text, lastBeat);
                continue;
            }

            int picked = 0;
            yield return Choose(beat.replies, chosen => picked = chosen);
            LastPick = picked;
            Reply reply = beat.replies[picked];
            yield return Say(technician, reply.said, lastBeat && reply.answers.Count == 0);
            for (int a = 0; a < reply.answers.Count; a++)
                yield return Say(crew, reply.answers[a], lastBeat && a == reply.answers.Count - 1);
        }
        yield return Switch(false);
        Finish(true);
    }

    IEnumerator Say(Speaker who, string text, bool last)
    {
        ShowSpeaker(who);
        lineLabel.text = text;
        lineLabel.maxVisibleCharacters = 0;
        counter.text = hasChoices ? "" : $"{spoken:00}/{spokenTotal:00}";
        nextLabel.text = last ? "END" : "NEXT";
        next.gameObject.SetActive(false);
        if (!switchedOn)
        {
            switchedOn = true;
            yield return Switch(true);
        }
        yield return TypeLine();
        yield return WaitForAdvance();
    }

    // The technician's turn: their replies, one picked, until E says it.
    IEnumerator Choose(List<Reply> replies, System.Action<int> chosen)
    {
        ShowSpeaker(technician);
        lineLabel.text = "";
        counter.text = "W S  CHOOSE";
        nextLabel.text = "SAY";
        int picked = 0;
        for (int i = 0; i < MaxReplies; i++)
        {
            bool used = i < replies.Count;
            choiceLabels[i].gameObject.SetActive(used);
            choiceBars[i].gameObject.SetActive(used);
        }
        ShowChoices(replies, picked);
        next.gameObject.SetActive(true);
        if (!switchedOn)
        {
            switchedOn = true;
            yield return Switch(true);
        }

        // The press that got here shouldn't also pick.
        yield return null;
        while (true)
        {
            int was = picked;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) picked = (picked + replies.Count - 1) % replies.Count;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) picked = (picked + 1) % replies.Count;
            bool numbered = false;
            for (int i = 0; i < replies.Count; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
                picked = i;
                numbered = true;
            }
            if (picked != was)
            {
                ShowChoices(replies, picked);
                if (voice != null) SoundManager.PlayOneShot(voice, "Dialogue Tick", fallback: tick);
            }
            if (numbered || AdvancePressed()) break;
            yield return null;
        }

        for (int i = 0; i < MaxReplies; i++)
        {
            choiceLabels[i].gameObject.SetActive(false);
            choiceBars[i].gameObject.SetActive(false);
        }
        chosen(picked);
        // So the same press doesn't skip the line it's about to say.
        yield return null;
    }

    void ShowChoices(List<Reply> replies, int picked)
    {
        for (int i = 0; i < replies.Count; i++)
        {
            bool on = i == picked;
            choiceLabels[i].text = $"{(on ? ">" : " ")} {i + 1}. {replies[i].said}";
            choiceLabels[i].color = on ? GameUI.Amber : GameUI.Dim;
            choiceBars[i].enabled = on;
        }
    }

    void ShowSpeaker(Speaker who)
    {
        current = who;
        nameLabel.text = who.name.ToUpperInvariant();
        roleLabel.text = who.role.ToUpperInvariant();
        nameTab.color = who.color;
        if (portraitMaterial != null) portraitMaterial.SetColor("_TrimColor", who.trim);
        UpdatePortrait();
    }

    IEnumerator TypeLine()
    {
        typing = true;
        skipTyping = false;
        lineLabel.ForceMeshUpdate();
        TMP_TextInfo info = lineLabel.textInfo;
        int total = info.characterCount;
        float shown = 0f;
        int visible = 0;
        while (visible < total && !skipTyping)
        {
            shown += LettersPerSecond * Time.unscaledDeltaTime;
            int reached = Mathf.Min(Mathf.FloorToInt(shown), total);
            bool blipped = false;
            float pause = 0f;
            while (visible < reached)
            {
                char letter = info.characterInfo[visible].character;
                visible++;
                if (!blipped && !char.IsWhiteSpace(letter) && visible % 2 == 1)
                {
                    Blip();
                    blipped = true;
                }
                // Stop after the end of a sentence or a clause, as someone speaking would.
                pause = PauseAfter(letter);
                if (pause > 0f) break;
            }
            lineLabel.maxVisibleCharacters = visible;

            if (pause > 0f && visible < total)
            {
                shown = visible;
                for (float t = 0f; t < pause && !skipTyping; t += Time.unscaledDeltaTime) yield return null;
            }
            else
            {
                yield return null;
            }
        }
        lineLabel.maxVisibleCharacters = int.MaxValue;
        typing = false;
        next.gameObject.SetActive(true);
    }

    static float PauseAfter(char letter)
    {
        if (letter == '.' || letter == '!' || letter == '?') return 0.22f;
        if (letter == ',' || letter == ';' || letter == ':') return 0.09f;
        return 0f;
    }

    IEnumerator WaitForAdvance()
    {
        // The press that finished the typing shouldn't also skip the line.
        yield return null;
        while (!AdvancePressed()) yield return null;
    }

    bool AdvancePressed()
    {
        if (Time.frameCount == openedFrame) return false;
        return Input.GetKeyDown(Terminal.InteractKey) || Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    void Blip()
    {
        if (voice == null || current == null) return;
        SoundManager.PlayOneShot(voice, "Dialogue Blip", pitchScale: current.voicePitch * Random.Range(0.94f, 1.06f), fallback: blip);
    }

    // Folds away without seeing the rest, and tells whoever opened it that it was cut off.
    void CutOff()
    {
        if (!open || cutOff) return;
        cutOff = true;
        StopAllCoroutines();
        typing = false;
        StartCoroutine(FoldAway());
    }

    IEnumerator FoldAway()
    {
        yield return Switch(false);
        Finish(false);
    }

    void Finish(bool completed)
    {
        open = false;
        closedFrame = Time.frameCount;
        for (int i = 0; i < MaxReplies; i++)
        {
            choiceLabels[i].gameObject.SetActive(false);
            choiceBars[i].gameObject.SetActive(false);
        }
        if (player != null) player.ClearScriptedInput();
        System.Action<bool> done = closed;
        closed = null;
        done?.Invoke(completed);
    }

    // Like an old monitor: a bright line across the middle opens out to the full box, or the box folds back into it.
    IEnumerator Switch(bool on)
    {
        float seconds = on ? OpenTime : CloseTime;
        float startShade = shade.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(t / seconds);
            float amount = on ? TitleUI.EaseOut(progress) : 1f - TitleUI.EaseIn(progress);
            SetOpenness(amount);
            shade.color = new Color(0f, 0f, 0f, Mathf.Lerp(startShade, on ? 0.75f : 0f, progress));
            yield return null;
        }
        SetOpenness(on ? 1f : 0f);
        shade.color = new Color(0f, 0f, 0f, on ? 0.75f : 0f);
    }

    void SetOpenness(float amount)
    {
        group.alpha = Mathf.Clamp01(amount * 4f);
        box.localScale = new Vector3(Mathf.Lerp(0.96f, 1f, amount), Mathf.Lerp(0.02f, 1f, amount), 1f);
        flash.color = TitleUI.Fade(GameUI.Cyan, (1f - amount) * 0.9f);
        flash.enabled = amount < 1f && amount > 0f;
        shade.enabled = group.alpha > 0f || shade.color.a > 0f;
    }

    void Update()
    {
        if (!open) return;

        // Through a door, or anywhere else away from them: the conversation's over.
        if (!cutOff && player != null && crew.anchor != null &&
            (Teleporter.IsTravelling || ((Vector2)player.transform.position - (Vector2)crew.anchor.position).sqrMagnitude > CutOffDistance * CutOffDistance))
        {
            CutOff();
            return;
        }

        if (typing && AdvancePressed()) skipTyping = true;

        float now = Time.unscaledTime;
        UpdatePortrait();

        // The interference line rolling down the portrait, every couple of seconds.
        float roll = Mathf.Repeat(now * 0.45f, 1f);
        interference.anchorMin = new Vector2(0f, 1f - roll);
        interference.anchorMax = new Vector2(1f, 1f - roll);

        // The voice meter jumps while they're talking and settles when they stop.
        for (int i = 0; i < meterBars.Count; i++)
        {
            float target = typing ? 0.25f + 0.75f * TitleUI.Hash(i * 17 + Mathf.FloorToInt(now * 14f)) : 0.15f;
            Vector2 size = meterBars[i].sizeDelta;
            size.y = Mathf.Lerp(size.y, 20f * target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 25f));
            meterBars[i].sizeDelta = size;
        }

        recDot.enabled = Mathf.Repeat(now, 1.2f) < 0.8f;
        nextArrow.anchoredPosition = new Vector2(nextArrow.anchoredPosition.x, -2f - Mathf.Abs(Mathf.Sin(now * 5f)) * 5f);
    }

    // Whoever's speaking's current sprite, cropped to their head and shoulders: a square from the top of the art.
    void UpdatePortrait()
    {
        if (current == null || current.portrait == null || current.portrait.sprite == null)
        {
            portrait.enabled = false;
            return;
        }
        Sprite sprite = current.portrait.sprite;
        Rect rect = sprite.textureRect;
        float side = Mathf.Min(rect.width, rect.height);
        var crop = new Rect(rect.x + (rect.width - side) * 0.5f, rect.yMax - side, side, side);
        Texture texture = sprite.texture;
        var uv = new Rect(crop.x / texture.width, crop.y / texture.height, crop.width / texture.width, crop.height / texture.height);
        if (current.portrait.flipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
        portrait.texture = texture;
        portrait.uvRect = uv;
        portrait.enabled = true;
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

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.ignoreListenerPause = true;
        blip = TitleUI.Tone("Dialogue Blip", BlipFrequency, 0.05f, 0.5f);
        tick = TitleUI.Tone("Dialogue Tick", BlipFrequency * 1.5f, 0.03f, 0.4f);

        // A dark wash up from the bottom of the screen, so the box sits in shadow whatever's behind it.
        RectTransform shadeRect = GameUI.Anchor(GameUI.NewRect("Shade", transform), 0f, 0f, 1f, 0f);
        shadeRect.pivot = new Vector2(0.5f, 0f);
        shadeRect.sizeDelta = new Vector2(0f, 460f);
        shade = shadeRect.gameObject.AddComponent<RawImage>();
        shade.texture = PixelArt.MakeTexture(1, 32, (x, y) => new Color32(255, 255, 255, (byte)(255f * Mathf.Pow(1f - y / 31f, 1.6f))));
        shade.texture.filterMode = FilterMode.Bilinear;
        shade.color = new Color(0f, 0f, 0f, 0f);
        shade.raycastTarget = false;

        box = GameUI.NewRect("Box", transform);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(Width, Height);
        box.anchoredPosition = new Vector2(0f, 44f + Height * 0.5f);
        group = box.gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;

        BuildPortrait();
        BuildTextPanel();

        // The bright line it opens out of.
        flash = GameUI.NewRect("Flash", box).gameObject.AddComponent<Image>();
        GameUI.Fill(flash.rectTransform);
        flash.material = TitleUI.Additive;
        flash.raycastTarget = false;

        SetOpenness(0f);
    }

    void BuildPortrait()
    {
        Image frame = GameUI.Sliced("Portrait Frame", box, GameUI.PanelSprite, Color.white);
        GameUI.Anchor(frame.rectTransform, 0f, 0f, 0f, 0f);
        frame.rectTransform.pivot = Vector2.zero;
        frame.rectTransform.sizeDelta = new Vector2(PortraitSize, PortraitSize);

        portraitWindow = GameUI.Fill(GameUI.NewRect("Window", frame.transform), 18f);
        portraitWindow.gameObject.AddComponent<RectMask2D>();

        RawImage backdrop = GameUI.Fill(GameUI.NewRect("Backdrop", portraitWindow)).gameObject.AddComponent<RawImage>();
        backdrop.texture = PixelArt.MakeTexture(1, 32, (x, y) => Color.Lerp(PortraitBottom, PortraitTop, y / 31f));
        backdrop.texture.filterMode = FilterMode.Bilinear;
        backdrop.raycastTarget = false;

        // A soft glow behind their head, like the screen's own light.
        RawImage glow = GameUI.Anchor(GameUI.NewRect("Glow", portraitWindow), 0.1f, 0.25f, 0.9f, 1.05f).gameObject.AddComponent<RawImage>();
        glow.texture = TitleUI.PatchTexture;
        glow.color = TitleUI.Fade(GameUI.Cyan, 0.12f);
        glow.material = TitleUI.Additive;
        glow.raycastTarget = false;

        portrait = GameUI.Anchor(GameUI.NewRect("Portrait", portraitWindow), 0f, 0f, 1f, 1f, 8f, 4f, 8f, 12f).gameObject.AddComponent<RawImage>();
        portrait.raycastTarget = false;
        Shader shader = Shader.Find("Sonny/UI Crew Portrait");
        if (shader != null) portrait.material = portraitMaterial = new Material(shader);

        RawImage lines = GameUI.Fill(GameUI.NewRect("Scanlines", portraitWindow)).gameObject.AddComponent<RawImage>();
        lines.texture = GameUI.Scanlines;
        lines.uvRect = new Rect(0f, 0f, 1f, (PortraitSize - 36f) / 3f);
        lines.raycastTarget = false;

        interference = GameUI.NewRect("Interference", portraitWindow);
        interference.pivot = new Vector2(0.5f, 0.5f);
        interference.sizeDelta = new Vector2(0f, 14f);
        RawImage band = interference.gameObject.AddComponent<RawImage>();
        band.texture = TitleUI.PatchTexture;
        band.color = TitleUI.Fade(GameUI.Cyan, 0.1f);
        band.material = TitleUI.Additive;
        band.raycastTarget = false;
    }

    void BuildTextPanel()
    {
        Image panel = GameUI.Sliced("Text Panel", box, GameUI.PanelSprite, Color.white);
        GameUI.Anchor(panel.rectTransform, 0f, 0f, 1f, 0f, PortraitSize + Gap, 0f, 0f, 0f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.sizeDelta = new Vector2(-(PortraitSize + Gap), TextPanelHeight);
        Transform body = panel.transform;

        // Name tab and job, sitting on the panel's top edge.
        RectTransform heading = GameUI.NewRect("Heading", body);
        heading.anchorMin = heading.anchorMax = heading.pivot = new Vector2(0f, 1f);
        heading.anchoredPosition = new Vector2(26f, 26f);
        heading.sizeDelta = new Vector2(900f, 52f);
        HorizontalLayoutGroup row = heading.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 16f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        nameTab = GameUI.Sliced("Name Tab", heading, GameUI.TabSprite, GameUI.Amber);
        HorizontalLayoutGroup tabRow = nameTab.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabRow.padding = new RectOffset(20, 20, 6, 8);
        tabRow.childAlignment = TextAnchor.MiddleCenter;
        tabRow.childControlWidth = tabRow.childControlHeight = true;
        tabRow.childForceExpandWidth = tabRow.childForceExpandHeight = false;
        var tabSize = nameTab.gameObject.AddComponent<LayoutElement>();
        tabSize.minHeight = tabSize.preferredHeight = 52f;
        nameLabel = GameUI.Label("Name", nameTab.transform, "", 30f, GameUI.Ink, TextAlignmentOptions.Center, GameUI.Heading);
        nameLabel.characterSpacing = 8f;

        // Above the panel's edge, beside the top half of the tab.
        roleLabel = GameUI.Label("Role", heading, "", 24f, GameUI.Dim, TextAlignmentOptions.TopLeft);
        roleLabel.characterSpacing = 6f;
        roleLabel.fontSharedMaterial = GameUI.Shadow(roleLabel.font);
        var roleSize = roleLabel.gameObject.AddComponent<LayoutElement>();
        roleSize.minHeight = roleSize.preferredHeight = 52f;

        // Top right: a voice meter and the log it's going into.
        RectTransform log = GameUI.NewRect("Log", body);
        log.anchorMin = log.anchorMax = log.pivot = new Vector2(1f, 1f);
        log.anchoredPosition = new Vector2(-28f, -18f);
        log.sizeDelta = new Vector2(330f, 30f);
        TextMeshProUGUI logLabel = GameUI.Label("Label", log, "VOICE LOG", 16f, TitleUI.Fade(GameUI.Cyan, 0.7f), TextAlignmentOptions.Right);
        logLabel.characterSpacing = 6f;
        GameUI.Fill(logLabel.rectTransform);
        recDot = GameUI.NewRect("Rec", log).gameObject.AddComponent<Image>();
        recDot.color = GameUI.Red;
        recDot.raycastTarget = false;
        recDot.rectTransform.anchorMin = recDot.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        recDot.rectTransform.sizeDelta = new Vector2(9f, 9f);
        recDot.rectTransform.anchoredPosition = new Vector2(-150f, 1f);
        for (int i = 0; i < 5; i++)
        {
            RectTransform bar = GameUI.NewRect("Meter", log);
            bar.anchorMin = bar.anchorMax = new Vector2(1f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = new Vector2(4f, 3f);
            bar.anchoredPosition = new Vector2(-222f + i * 8f, 1f);
            Image barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = TitleUI.Fade(GameUI.Cyan, 0.8f);
            barImage.raycastTarget = false;
            meterBars.Add(bar);
        }

        lineLabel = GameUI.Label("Line", body, "", 32f, GameUI.Text, TextAlignmentOptions.TopLeft);
        lineLabel.fontSharedMaterial = GameUI.Shadow(lineLabel.font);
        lineLabel.textWrappingMode = TextWrappingModes.Normal;
        lineLabel.lineSpacing = 12f;
        GameUI.Anchor(lineLabel.rectTransform, 0f, 0f, 1f, 1f, 36f, 54f, 36f, 50f);

        counter = GameUI.Label("Counter", body, "", 16f, TitleUI.Fade(GameUI.Dim, 0.8f), TextAlignmentOptions.BottomLeft);
        counter.characterSpacing = 4f;
        counter.rectTransform.anchorMin = counter.rectTransform.anchorMax = counter.rectTransform.pivot = Vector2.zero;
        counter.rectTransform.anchoredPosition = new Vector2(36f, 16f);
        counter.rectTransform.sizeDelta = new Vector2(200f, 30f);

        // The technician's replies, in place of the line, one row each, with a bar behind the one picked.
        RectTransform choices = GameUI.Anchor(GameUI.NewRect("Choices", body), 0f, 0f, 1f, 1f, 26f, 54f, 36f, 46f);
        for (int i = 0; i < MaxReplies; i++)
        {
            Image bar = GameUI.NewRect("Bar", choices).gameObject.AddComponent<Image>();
            bar.color = TitleUI.Fade(GameUI.Amber, 0.12f);
            bar.raycastTarget = false;
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, ChoiceRowHeight - 4f);
            bar.rectTransform.anchoredPosition = new Vector2(0f, -i * ChoiceRowHeight);
            choiceBars[i] = bar;

            TextMeshProUGUI label = GameUI.Label("Reply", choices, "", 32f, GameUI.Dim, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-20f, ChoiceRowHeight - 4f);
            label.rectTransform.anchoredPosition = new Vector2(10f, -i * ChoiceRowHeight);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            choiceLabels[i] = label;
            bar.gameObject.SetActive(false);
            label.gameObject.SetActive(false);
        }

        // Bottom right: [E] NEXT, with an arrow bobbing beside it once the line has finished.
        next = GameUI.NewRect("Next", body);
        next.anchorMin = next.anchorMax = next.pivot = new Vector2(1f, 0f);
        next.anchoredPosition = new Vector2(-30f, 16f);
        next.sizeDelta = new Vector2(200f, 40f);
        Image cap = GameUI.KeyCap(next, "E", 24f, out _);
        cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = cap.rectTransform.pivot = new Vector2(0f, 0.5f);
        cap.rectTransform.sizeDelta = new Vector2(38f, 40f);
        cap.rectTransform.anchoredPosition = new Vector2(52f, 0f);
        nextLabel = GameUI.Label("Label", next, "NEXT", 24f, GameUI.Amber, TextAlignmentOptions.Left);
        nextLabel.characterSpacing = 6f;
        nextLabel.rectTransform.anchorMin = nextLabel.rectTransform.anchorMax = nextLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
        nextLabel.rectTransform.sizeDelta = new Vector2(90f, 40f);
        nextLabel.rectTransform.anchoredPosition = new Vector2(100f, 2f);
        Image arrow = GameUI.Sliced("Arrow", next, GameUI.ArrowSprite, GameUI.Amber);
        arrow.type = Image.Type.Simple;
        nextArrow = arrow.rectTransform;
        nextArrow.anchorMin = nextArrow.anchorMax = new Vector2(1f, 0.5f);
        nextArrow.pivot = new Vector2(1f, 0.5f);
        nextArrow.sizeDelta = new Vector2(21f, 15f);
        nextArrow.anchoredPosition = new Vector2(0f, -2f);
        next.gameObject.SetActive(false);
    }
}
