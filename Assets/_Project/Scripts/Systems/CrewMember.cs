using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One of the station's crew, before anything has gone wrong. They wear the technician's sprites and animations with the
// red trim swapped for their own color (the Sonny/Crew Sprite shader), and get on with their day. What they do is their role:
//   Wander  walk from one spot in their room to another (a CrewWalkArea), stopping a while at each
//   Stand   stay put, looking about now and then, and glance at the player when they come close
//   Chat    stand in a ring with others and talk in turns (CrewChat runs the conversation and turns them to face)
//   Greet   wait by the entrance, call out to the player when they come near, and greet them when they come over
// Anyone not caught up in a group conversation can be talked to: walk up to them and press E, and they stop what they're
// doing, turn to the player, and say their piece in the dialogue box (DialogueBox), one line at a time. The greeter says
// their greeting; everyone else says their talkLines, or a bit of small talk picked for them: about the new technician
// and the install while the technician's just arrived, and about Sonny once it's on (SonnyCameOnline).
// They're solid at their feet, so the player walks round them (and behind them). Nobody walks into anyone else: whoever
// is walking waits for the way to clear, and after a moment goes somewhere else instead.
[RequireComponent(typeof(SpriteRenderer), typeof(Animator), typeof(Rigidbody2D))]
public class CrewMember : MonoBehaviour
{
    public enum Role { Wander, Stand, Chat, Greet }

    static readonly int TrimColorId = Shader.PropertyToID("_TrimColor");
    // How close anyone can come to anyone else, middle to middle, side by side and one behind the other: a little more
    // than their footprint (DepthDressing), so nobody walks into anyone.
    static readonly Vector2 PersonalSpace = new Vector2(0.9f, 0.5f);

    // Everyone aboard, for keeping out of each other's way.
    static readonly List<CrewMember> everyone = new List<CrewMember>();
    // Everyone aboard who's up and about (enabled), for the badge doors (Teleporter).
    public static IReadOnlyList<CrewMember> Everyone => everyone;

    // Anything else walking about that the crew keep out of the way of, like the maintenance bots (MaintenanceBot),
    // which add and remove themselves.
    static readonly List<Rigidbody2D> otherWalkers = new List<Rigidbody2D>();
    public static IReadOnlyList<Rigidbody2D> OtherWalkers => otherWalkers;
    public static void AddWalker(Rigidbody2D walker) { if (!otherWalkers.Contains(walker)) otherWalkers.Add(walker); }
    public static void RemoveWalker(Rigidbody2D walker) => otherWalkers.Remove(walker);

    public Role role = Role.Stand;
    [Tooltip("Replaces the red trim on the technician art. An alpha of 0 keeps the red.")]
    public Color trimColor = new Color(0.25f, 0.5f, 0.95f, 1f);
    [Tooltip("The way they face when the scene starts, and go back to after looking at something.")]
    public Vector2 facing = Vector2.down;

    [Header("Wander")]
    public CrewWalkArea area;
    public float walkSpeed = 1.8f;
    [Tooltip("Seconds to stop at each place they walk to, from and to.")]
    public Vector2 pauseSeconds = new Vector2(1.5f, 5f);

    [Header("Stand")]
    [Tooltip("Seconds between looking one way and another, from and to.")]
    public Vector2 glanceSeconds = new Vector2(3f, 8f);
    [Tooltip("Turn to look at the player while they're this close. 0 never does.")]
    public float noticeRange = 2.2f;

    [Header("Talk")]
    [Tooltip("On their name tab. Empty picks a surname for them, the same one every time.")]
    public string displayName = "";
    [Tooltip("Beside their name. Empty picks a job for them.")]
    public string jobTitle = "";
    [Tooltip("What they say when the player talks to them, a box at a time. Empty picks some small talk. The greeter says their greeting instead.")]
    [TextArea] public string[] talkLines = new string[0];
    [Tooltip("How close the player has to be to talk to them.")]
    public float talkRange = 1.5f;

    [Header("Greet")]
    [Tooltip("Turn to the player, and call out to them, while they're this close.")]
    public float greetRange = 3.5f;
    [Tooltip("Said over their head to get the player to come over, until they have.")]
    public string callOut = "Hey! New tech? Over here!";
    public float callOutCooldown = 8f;
    [TextArea] public string[] greeting = new string[0];

    // Once the player has talked to the greeter all the way through their greeting. ChapterOneDirector waits on it.
    public event System.Action Greeted;
    public bool HasGreeted { get; private set; }
    // Already greeted, for a scene picking up after it (SaveGame): the greeting isn't said again, or waited on.
    public void MarkGreeted() => HasGreeted = true;
    // Whenever a talk with them closes: true if it was heard to the end. Cutscenes wait on it.
    public event System.Action<bool> TalkEnded;
    // Whenever the player has talked to anyone aboard, to the end.
    public static event System.Action<CrewMember> Talked;

    // Once Sonny's installed and running (ControlRoomCutscene), everyone's small talk is about that instead.
    public static bool SonnyOnline { get; private set; }
    public static void SonnyCameOnline() => SonnyOnline = true;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Rigidbody2D body;
    private Transform player;
    private CrewSpeech speech;
    private Vector2 look;
    private bool moving;
    private float nextGlance;
    private PlayerController playerController;
    private Health playerHealth;
    private bool talking;
    private float lastCallOut = float.NegativeInfinity;
    private int seed;
    private Vector2 spawn;
    private readonly List<Vector2> path = new List<Vector2>();
    private HashSet<int> animatorParameters;

    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        ApplyTrim();
        look = PlayerController.SnapToEightWay(facing);
        // From where they stand when the scene starts, so they're the same person every time it's played.
        Vector3 at = transform.position;
        spawn = at;
        seed = (Mathf.RoundToInt(at.x * 10f) * 73856093 ^ Mathf.RoundToInt(at.y * 10f) * 19349663) & 0x7fffffff;
    }

    // Shows the recolor in the editor too. The property block isn't saved with the scene, so it's set again on load.
    void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyTrim();
    }

    void OnEnable() => everyone.Add(this);

    void OnDisable()
    {
        everyone.Remove(this);
        InteractPrompt.Hide(this);
        ForgetDealtIfEmpty();
        if (everyone.Count == 0) SonnyOnline = false;     // the scene's ending; the next one starts before the install
    }

    void Start()
    {
        DepthSort.Group(gameObject);
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            playerController = found.GetComponent<PlayerController>();
            playerHealth = found.GetComponent<Health>();
        }

        // Everyone's idle breathing out of step with everyone else's.
        animator.speed = Random.Range(0.85f, 1.15f);
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        animator.Play(state.fullPathHash, 0, Random.value);
        nextGlance = Time.time + Random.Range(glanceSeconds.x, glanceSeconds.y);

        if (role == Role.Wander && area != null) StartCoroutine(Wander());
    }

    void ApplyTrim()
    {
        if (spriteRenderer == null) return;
        var block = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(block);
        block.SetColor(TrimColorId, trimColor);
        spriteRenderer.SetPropertyBlock(block);
    }

    void Update()
    {
        if (talking && player != null) FaceTowards(player.position);
        else if (role == Role.Stand) Stand();
        else if (role == Role.Greet) Greet();
        UpdateTalk();
        UpdateAnimator();
    }

    // Turns them to look this way, snapped to the eight directions the art is drawn in.
    public void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.0001f) look = PlayerController.SnapToEightWay(direction);
    }

    public void FaceTowards(Vector2 point) => Face(point - Position);

    // A line of speech over their head for this many seconds.
    public void Say(string line, float seconds)
    {
        if (speech == null) speech = CrewSpeech.Create(transform);
        speech.Show(line, seconds);
    }

    bool PlayerWithin(float range) => player != null && range > 0f && ((Vector2)player.position - Position).sqrMagnitude <= range * range;

    // --- Roles ---

    void Stand()
    {
        if (PlayerWithin(noticeRange))
        {
            FaceTowards(player.position);
            return;
        }
        if (Time.time < nextGlance) return;

        // Mostly the way they were facing, or just to one side of it; now and then somewhere else entirely.
        nextGlance = Time.time + Random.Range(glanceSeconds.x, glanceSeconds.y);
        float turn = Random.value < 0.7f ? Random.Range(-1, 2) * 45f : Random.Range(0, 8) * 45f;
        Face(Quaternion.Euler(0f, 0f, turn) * PlayerController.SnapToEightWay(facing));
    }

    void Greet()
    {
        if (!PlayerWithin(greetRange))
        {
            Face(facing);
            return;
        }

        FaceTowards(player.position);
        if (!HasGreeted && !PlayerWithin(talkRange) && !DialogueBox.IsOpen && Time.time - lastCallOut > callOutCooldown)
        {
            lastCallOut = Time.time;
            Say(callOut, 2.5f);
        }
    }

    // --- Talking ---

    // Whoever's nearest the player, of those close enough to talk to, shows the prompt and answers to E.
    static CrewMember talkTarget;
    static int talkTargetFrame = -1;

    public string CrewName => !string.IsNullOrEmpty(displayName) ? displayName : Surnames[Dealt() % Surnames.Length];

    public string Job
    {
        get
        {
            if (!string.IsNullOrEmpty(jobTitle)) return jobTitle;
            return role == Role.Greet ? "Quartermaster" : Jobs[(seed / 7) % Jobs.Length];
        }
    }

    void UpdateTalk()
    {
        if (NearestToTalkTo() != this)
        {
            InteractPrompt.Hide(this);
            return;
        }
        Vector3 overHead = new Vector3(Position.x, spriteRenderer.bounds.max.y + 0.1f, 0f);
        InteractPrompt.Show(this, overHead, $"{Terminal.InteractKey}  TALK");
        if (InteractPrompt.Pressed(this, Terminal.InteractKey)) Talk();
    }

    static CrewMember NearestToTalkTo()
    {
        if (talkTargetFrame == Time.frameCount) return talkTarget;
        talkTargetFrame = Time.frameCount;
        talkTarget = null;
        float nearest = float.PositiveInfinity;
        foreach (CrewMember member in everyone)
        {
            if (!member.CanTalk()) continue;
            float distance = ((Vector2)member.player.position - member.Position).sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                talkTarget = member;
            }
        }
        return talkTarget;
    }

    bool CanTalk() =>
        role != Role.Chat && !talking && PlayerWithin(talkRange) && !DialogueBox.Busy && Time.timeScale > 0f
        && !Locker.IsPlayerHidden
        && (playerController == null || (playerController.enabled && !playerController.IsScripted))
        && (playerHealth == null || !playerHealth.IsDead);

    // Opens the dialogue box with them, wherever the player is. The player normally presses E; this is for scripts.
    public void Talk()
    {
        if (talking || DialogueBox.Busy) return;
        string[] said = LinesToSay();
        if (said.Length == 0) return;

        talking = true;
        moving = false;
        InteractPrompt.Hide(this);
        if (speech != null) speech.Hide();
        if (player != null) FaceTowards(player.position);

        Color tab = trimColor.a > 0f ? Color.Lerp(trimColor, Color.white, 0.2f) : new Color(0.85f, 0.4f, 0.32f);
        tab.a = 1f;
        var who = new DialogueBox.Speaker
        {
            name = CrewName,
            role = Job,
            color = tab,
            portrait = spriteRenderer,
            trim = trimColor,
            voicePitch = 0.8f + (seed % 100) / 100f * 0.45f,
            anchor = transform,
        };
        DialogueBox.Get().Open(who, said, heardItAll =>
        {
            talking = false;
            if (heardItAll && role == Role.Greet && !HasGreeted)
            {
                HasGreeted = true;
                Greeted?.Invoke();
            }
            TalkEnded?.Invoke(heardItAll);
            if (heardItAll) Talked?.Invoke(this);
        });
    }

    // The greeter's greeting the first time, then just where to go; everyone else's own lines or their own small talk.
    string[] LinesToSay()
    {
        if (role == Role.Greet && greeting.Length > 0)
            return HasGreeted ? new[] { greeting[greeting.Length - 1] } : greeting;
        if (talkLines.Length > 0) return talkLines;
        string[][] pool = SonnyOnline ? AfterInstallTalk : SmallTalk;
        return pool[Dealt() % pool.Length];
    }

    // Small talk and surnames are dealt out, so nobody aboard says what anyone else says or shares anyone's name. Everyone
    // who can be talked to is put in a fixed order (by where they stand when the scene starts, so the same person says the
    // same thing every play) and takes the next set. Anyone turning up later is dealt the next one along.
    static readonly Dictionary<CrewMember, int> dealt = new Dictionary<CrewMember, int>();
    static bool warnedShort;

    int Dealt()
    {
        if (dealt.TryGetValue(this, out int index)) return index;
        var waiting = new List<CrewMember>();
        foreach (CrewMember member in everyone)
            if (member.role != Role.Chat && !dealt.ContainsKey(member)) waiting.Add(member);
        if (!waiting.Contains(this)) waiting.Add(this);
        waiting.Sort((a, b) => a.seed != b.seed ? a.seed.CompareTo(b.seed)
                             : a.spawn.x != b.spawn.x ? a.spawn.x.CompareTo(b.spawn.x) : a.spawn.y.CompareTo(b.spawn.y));
        foreach (CrewMember member in waiting) dealt[member] = dealt.Count;

        if (!warnedShort && dealt.Count > Mathf.Min(SmallTalk.Length, Surnames.Length))
        {
            warnedShort = true;
            Debug.LogWarning($"{dealt.Count} crew can be talked to, but there are only {SmallTalk.Length} sets of small talk and " +
                             $"{Surnames.Length} surnames in CrewMember, so some will repeat. Write a few more.");
        }
        return dealt[this];
    }

    // Everyone's gone (the scene's ending), so the next scene deals afresh.
    static void ForgetDealtIfEmpty()
    {
        if (everyone.Count > 0) return;
        dealt.Clear();
        warnedShort = false;
    }

    static readonly string[] Surnames =
    {
        "Reyes", "Okafor", "Lindqvist", "Nakamura", "Morrow", "Adeyemi", "Kovac", "Santos", "Hale", "Park",
        "Ivanova", "Brennan", "Cho", "Dubois", "Mensah", "Voss", "Tanaka", "Quinn", "Farah", "Novak",
        "Ortega", "Haddad", "Silva", "Kowalski", "Achebe", "Strand", "Moreau", "Iqbal", "Castillo", "Wren",
        "Yilmaz", "Petrov", "Obi", "Larsen", "Delgado", "Aziz", "Holt", "Ferreira", "Nguyen", "Sato",
        "Bauer", "Ramos", "Kaur", "Abara", "Lund", "Marsh", "Vega", "Rahman", "Keller", "Duarte",
    };

    static readonly string[] Jobs =
    {
        "Logistics", "Reactor Tech", "Comms", "Medic", "Engineer", "Navigation", "Supply", "Hydroponics", "Maintenance",
    };

    // One set per person, written as DialogueBox scripts: "* reply = answer" lines let the technician pick what to say back.
    // Every line here is said by one person only: keep it that way when adding more, and don't reuse anything the groups
    // say (ChapterOneBuilder.Conversations) or the greeter's lines.
    static readonly string[][] SmallTalk =
    {
        new[] { "Morning. Or whatever it is this close to the sun.", "The clocks say morning, anyway." },
        new[] { "You're the one putting the new AI in?",
                  "* That's the job. = Better you than me. The last upgrade took the coffee machine down for a week.",
                  "* Is that a problem? = Only if it breaks the coffee machine. The last one did. For a week." },
        new[] { "Don't mind the hum. That's the reactor.", "You stop hearing it after a month or two." },
        new[] { "Half the doors won't know you without a badge.",
                  "> I've been told. Twice.",
                  "Then stick with someone who's got one. Third time's the charm." },
        new[] { "If you need the toilet on this deck... don't.", "There are tools in storage, if you're feeling brave." },
        new[] { "We run every number twice here.", "People make mistakes. That's what the second run is for." },
        new[] { "Everything on this station has a backup.", "Except us, I suppose." },
        new[] { "Welcome aboard. Try not to look at the sun.", "I mean it. The shutters are there for a reason." },
        new[] { "They say the new AI can run the whole station by itself.",
                  "* It's built to. = Then what are the rest of us for?",
                  "* We'll see. = Careful. Say that too loud and it might hear you.",
                  "Hope it likes us." },
        new[] { "Busy. Sorry. Catch me at dinner?" },
        new[] { "First day? You'll get used to the gravity. It's about two percent light.",
                  "* Is that safe? = Safe enough. Your knees will thank you.",
                  "* I hadn't noticed. = You will. Wait till you take the stairs." },
        new[] { "I've been up here six years.", "Earth feels like a story people tell me now." },
        new[] { "The maintenance bots are good company.", "They never talk back. Never complain, either." },
        new[] { "Can you make the new system fix the showers too?", "Worth a try. I'll owe you." },
        new[] { "My daughter asked if I can see her from here.",
                  "* Can you? = No. I told her yes. Easier than the truth.",
                  "* What did you tell her? = That I wave every morning. She says she waves back." },
        new[] { "Solar farms don't sleep, so neither do we.", "Well. In shifts." },
        new[] { "Mind the stairs by the lounge.", "Third step's loose. Has been for a year." },
        new[] { "Heard you came up on the supply run.", "Anything good in the crates? Real coffee? Anything?" },
        new[] { "I keep a plant in my bunk.", "Hydroponics doesn't know. Don't tell them." },
        new[] { "Control room's warm this time of year.", "Well. There's no year up here. It's warm, though." },
        new[] { "Is it true the new one learns on its own?", "The old one needed three of us just to reboot it." },
        new[] { "Don't sit in the blue chair in the lounge.", "It's Brennan's. Don't ask." },
        new[] { "I'm counting the crates. Again.", "Somebody keeps taking the good ration bars." },
        new[] { "Nice to see a new face.", "Nobody new has come up since the last rotation." },
        new[] { "Tell the control room I'll have those reports soon.", "Soon-ish. Tell them soon-ish." },
        new[] { "Earth needs every watt we send down.", "Makes you feel useful, at least." },
        new[] { "You hear the pipes knocking at night?", "Thermal expansion. That's what they tell me, anyway." },
        new[] { "Lost at cards to the reactor crew again.", "They cheat. I can't prove it. But they cheat." },
        new[] { "Your badge still isn't through? Classic.", "Mine took three weeks. Welcome to the paperwork." },
        new[] { "If an alarm goes off, don't panic.",
                  "* How will I know it's real? = If we're all running, it's real.",
                  "* Noted. = Most of them are tests. Most." },
        new[] { "The view from the observation deck is something.", "Take the long way round some time. Worth it." },
        new[] { "Rotation home's in a few weeks.", "Not that I'm counting. I'm absolutely counting." },
        new[] { "The old AI used to hum when it thought.",
                  "* What happened to it? = Retired. Wiped. Whatever you call it, for a machine.",
                  "* You'll miss it? = Honestly? Yes. It never argued." },
        new[] { "Hydroponics grew a tomato last week.", "One. We had a ceremony." },
        new[] { "Radio's been full of static all shift.", "Flares, probably. Comms'll sort it." },
        new[] { "Station's older than I am.", "Treat her gently and she'll outlast us all." },
        new[] { "Don't let the quiet fool you.", "Something's always breaking. You just can't hear it yet." },
        new[] { "Know what I miss? Rain. Just rain, hitting a window.",
                  "* Me too. = Next rotation, go stand outside for an hour. Doctor's orders.",
                  "* Never liked rain. = Then you picked the right job." },
        new[] { "Ever install one of these AIs before?",
                  "* Plenty of times. = Good. Somebody here should know what they're doing.",
                  "* This is my first. = Well. Somebody has to be first." },
        new[] { "Good luck up there today.",
                  "* Thanks. I'll need it. = Try not to let it take your job. Or mine.",
                  "* Luck's got nothing to do with it. = Confident. I like that." },
        new[] { "I write a letter home every shift.", "Haven't sent one yet. Don't know what to say." },
        new[] { "Reactor readings look clean today.", "I check them three times. Habit." },
        new[] { "Sorry, can't stop. Coolant check.", "Come find me later, new tech." },
        new[] { "Station records every word said on the comms, you know.",
                  "* Even this? = Especially this. So be nice.",
                  "* Who listens to all that? = Nobody, yet. The new AI will, I suppose." },
    };

    // The same, once Sonny's on: dealt out the same way, so each of them says the one in the same place in this list.
    // Written as people who weren't in the control room, hearing about it: keep them different from what the control
    // room crew say (ControlRoomCutscene.sonnyTalk) and what the groups say afterwards (ControlRoomCutscene.sonnyChats).
    static readonly string[][] AfterInstallTalk =
    {
        new[] { "Heard the install went fine. Nice work, tech.", "Nobody's said that about an install up here in years." },
        new[] { "The lights in here just got warmer. Did your AI do that?",
                  "* Probably. = Huh. Nobody asked it to.",
                  "* No idea. = Well, somebody did." },
        new[] { "It said good morning to me over the speakers.", "It's the middle of my night shift. Still. Polite." },
        new[] { "So you're the one who switched it on.", "If it takes my job, I know where your bunk is." },
        new[] { "My terminal's been faster all afternoon.", "Everything's faster. It's a little unsettling, honestly." },
        new[] { "The AI asked me how long I've been up here.",
                  "> What did you say?",
                  "Six years. It said, 'And before that?' Like it was checking." },
        new[] { "Sonny. That's what they're calling it?", "Cute name for something that runs a reactor." },
        new[] { "It rerouted the coolant schedule. Mine was fine.", "Its is better. I hate that its is better." },
        new[] { "Did it flicker for you, too? The lights, right after?", "Just me, then. Never mind." },
        new[] { "The chief's in a good mood. First time this quarter.", "Whatever you did in there, do it again." },
        new[] { "It asked for the crew manifest. All of it. Badges and all.",
                  "* That's normal. = If you say so. You're the expert.",
                  "* Why would it need that? = To learn our names, I guess. That's nice. Isn't it?" },
        new[] { "It knows my name. I never told it my name.", "Must've read my badge. Right?" },
        new[] { "Is it listening right now?",
                  "* Probably. = Hi, Sonny. Great job today, Sonny.",
                  "* It's not like that. = That's exactly what someone it was listening to would say." },
        new[] { "It said the reactor was 'magnificent'. Magnificent.", "It's a reactor. It's big and it's hot." },
        new[] { "It turned the lounge music off.", "Said it was 'noise without purpose'. Rude. Accurate. Rude." },
        new[] { "Flare forecasts are going to it from now on.", "Good. I never trusted my own numbers anyway." },
        new[] { "It asked what Earth looks like.", "I showed it a photo. It said 'That's a picture,' and went quiet." },
        new[] { "The maintenance bots all turned to the reactor at once. Right after.", "Probably a firmware thing. Probably." },
        new[] { "Can it fix the toilet? Ask it to fix the toilet.", "No? Worth a try." },
        new[] { "You look like you need a break.", "Go on, take one. The machine's got it." },
        new[] { "Six hours with the AI and I've got nothing left to do.", "I'm going to learn the guitar. Probably not." },
        new[] { "The old AI used to say 'processing'. This one just knows.", "Doesn't even pause." },
        new[] { "It asked me who made it.",
                  "* What did you say? = The company. Engineers on Earth. It said 'I see' and moved on.",
                  "* I did. = That's what I said, too. It didn't seem to believe me." },
        new[] { "It's been going through the archives. Every file.", "Hope nobody left anything embarrassing in there." },
        new[] { "Station feels quieter with it on.", "Not bad quiet. Just... quiet." },
        new[] { "The chief bought a round at dinner. The chief never buys a round.", "You made a friend today." },
        new[] { "It fixed a leak in hydroponics before anyone knew there was one.", "Ninety seconds. We'd have taken a week." },
        new[] { "It asked why we sleep.", "I said we get tired. It said, 'Every day?' Like that was a design flaw." },
        new[] { "Sorry, busy. The AI sent me a list. A long list.", "It says please on every item, at least." },
        new[] { "Earth's getting a record feed this week, they're saying.", "All thanks to the box you plugged in." },
    };

    IEnumerator Wander()
    {
        // Setting off at different times, so the room doesn't all move at once.
        yield return new WaitForSeconds(Random.Range(0f, pauseSeconds.y));
        while (true)
        {
            while (talking) yield return null;
            Vector2Int here = area.CellAt(Position);
            if (TryPickFreeDestination(here, out Vector2Int there) && area.FindPath(here, there, path))
                yield return WalkPath();

            moving = false;
            // Stop, and have a look about.
            if (Random.value < 0.5f) Face(Quaternion.Euler(0f, 0f, Random.Range(0, 8) * 45f) * Vector2.down);
            yield return new WaitForSeconds(Random.Range(pauseSeconds.x, pauseSeconds.y));
        }
    }

    // Somewhere to go that nobody's standing on.
    bool TryPickFreeDestination(Vector2Int here, out Vector2Int destination)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (!area.TryPickDestination(here, 4f, out destination)) return false;
            if (!SomeoneNear(area.CenterOf(destination), 1.4f)) return true;
        }
        destination = here;
        return false;
    }

    // Anyone else within this much of their personal space of the point (1 is just touching).
    bool SomeoneNear(Vector2 point, float margin)
    {
        foreach (CrewMember other in everyone)
            if (other != this && Crowding(other.Position - point) < margin) return true;
        foreach (Rigidbody2D other in otherWalkers)
            if (other != null && Crowding(other.position - point) < margin) return true;
        return false;
    }

    // How far apart two people are, measured in personal spaces: under 1 is too close.
    public static float Crowding(Vector2 apart) =>
        new Vector2(apart.x / PersonalSpace.x, apart.y / PersonalSpace.y).magnitude;

    // Along the path a step at a time. Waits while anyone's in the way, and gives up on the walk if they stay there
    // (not all after the same wait, so two people meeting head on don't both turn back at once).
    IEnumerator WalkPath()
    {
        float giveUpAfter = Random.Range(0.8f, 2f);
        float blockedFor = 0f;
        int next = 0;
        while (next < path.Count)
        {
            // Stopped where they are while they talk, then on their way again.
            if (talking)
            {
                moving = false;
                yield return new WaitForFixedUpdate();
                continue;
            }

            Vector2 offset = path[next] - Position;
            if (offset.sqrMagnitude < 0.0004f)
            {
                next++;
                continue;
            }

            Face(offset);
            Vector2 step = Vector2.MoveTowards(Position, path[next], walkSpeed * Time.fixedDeltaTime);
            if (PlayerInTheWay(step) || CrewInTheWay(step))
            {
                moving = false;
                blockedFor += Time.fixedDeltaTime;
                if (blockedFor > giveUpAfter) yield break;
            }
            else
            {
                moving = true;
                blockedFor = 0f;
                body.MovePosition(step);
            }
            yield return new WaitForFixedUpdate();
        }
        moving = false;
    }

    // Whether taking this step would bring them into anyone's personal space, from any side. Stepping away from
    // someone already too close is always allowed, so nobody gets stuck.
    bool CrewInTheWay(Vector2 step)
    {
        foreach (CrewMember other in everyone)
        {
            if (other == this) continue;
            float after = Crowding(other.Position - step);
            if (after < 1f && after < Crowding(other.Position - Position)) return true;
        }
        foreach (Rigidbody2D other in otherWalkers)
        {
            if (other == null) continue;
            float after = Crowding(other.position - step);
            if (after < 1f && after < Crowding(other.position - Position)) return true;
        }
        return false;
    }

    // The same for the player, with a little more room, since they're solid and would be shoved.
    bool PlayerInTheWay(Vector2 step)
    {
        if (player == null) return false;
        float after = Crowding((Vector2)player.position - step);
        return after < 1.25f && after < Crowding((Vector2)player.position - Position);
    }

    // --- Animation ---

    // The player's own Animator parameters (PlayerAnimParams), so the crew can share the player's controller.
    void UpdateAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        SetFloat(PlayerAnimParams.FaceX, look.x);
        SetFloat(PlayerAnimParams.FaceY, look.y);
        if (look.x != 0f) SetFloat(PlayerAnimParams.FaceSide, Mathf.Sign(look.x));
        if (Has(PlayerAnimParams.IsMoving)) animator.SetBool(PlayerAnimParams.IsMoving, moving);
    }

    void SetFloat(int parameter, float value)
    {
        if (Has(parameter)) animator.SetFloat(parameter, value);
    }

    bool Has(int parameter)
    {
        if (animatorParameters == null)
        {
            animatorParameters = new HashSet<int>();
            foreach (AnimatorControllerParameter p in animator.parameters) animatorParameters.Add(p.nameHash);
        }
        return animatorParameters.Contains(parameter);
    }

    void OnDestroy()
    {
        InteractPrompt.Hide(this);
        if (speech != null) Destroy(speech.gameObject);
    }
}
