using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One of the station's maintenance robots, on its rounds in Chapter 1, before anything's gone wrong: a little machine
// trundling about the room among the crew (over the same floor grid they walk, CrewWalkArea), stopping here and there to
// work (sweeping, hauling, scanning, a busy blink and a whirr), and now and then pulling up beside one of the crew, who
// has a word for it. Walk up and press E to talk to it (DialogueBox): it answers in capitals.
// Once Sonny's on (CrewMember.SonnyOnline), every bot aboard stops where it is, turns north, toward the reactor, its eye
// gone red, for a few seconds, all at once. Then it carries on as if nothing happened, with new things to say, and
// every so often it stops to look at the technician a little too long. (Sonny ran diagnostics on all of them.)
// Nobody walks into anyone: the crew step round the bots (CrewMember.AddWalker) and the bots round everyone.
// The art is made in code until it has its own. Built by ChapterOneBuilder, which puts a few in the rooms with the most
// going on.
[RequireComponent(typeof(Rigidbody2D))]
public class MaintenanceBot : MonoBehaviour
{
    public enum Kind { Sweeper, Hauler, Scanner }

    const float PixelsPerUnit = 16f;
    static readonly Vector2 PersonalSpace = new Vector2(1f, 1.2f);
    static readonly Color EyeColor = new Color(0.4f, 0.95f, 1f);
    static readonly Color SyncColor = new Color(1f, 0.15f, 0.1f);
    static readonly List<MaintenanceBot> all = new List<MaintenanceBot>();

    public Kind kind;
    [Tooltip("Its unit number, on its name tab.")]
    public string unitName = "MX-07";
    public CrewWalkArea area;
    public float moveSpeed = 1.2f;
    [Tooltip("Seconds it works at each spot it stops at, from and to.")]
    public Vector2 workSeconds = new Vector2(2f, 4.5f);
    public float talkRange = 1.4f;

    [Header("Talk")]
    [Tooltip("What it says when the player talks to it, before Sonny's on and after, as DialogueBox scripts. Empty picks lines for its kind.")]
    [TextArea] public string[] talkLines = new string[0];
    [TextArea] public string[] talkLinesAfter = new string[0];

    // Over its head as it works, and to the crew.
    static readonly string[] Beeps = { "BWEEP!", "Blip blip.", "*whirr*", "BWOOP.", "Beep?" };
    static readonly string[] CrewToBot = { "Morning, little guy.", "Watch the feet!", "Not now, buddy.", "Good bot.", "Thanks, pal.", "Missed a spot." };
    static readonly string[] CrewToBotAfter = { "You okay, buddy?", "Why'd it stop like that?", "Huh. It's never done that before.", "...Hello?" };

    private Rigidbody2D body;
    private SpriteRenderer art;
    private SpriteRenderer eye;
    private Sprite[] frames;
    private CrewSpeech speech;
    private Transform player;
    private PlayerController playerController;
    private readonly List<Vector2> path = new List<Vector2>();
    private bool moving, working, talking, synced, staring;
    private Vector2 look = Vector2.down;
    private float frameTime;
    private float voicePitch;
    private AudioSource voice;

    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        frames = MakeFrames(kind);
        voicePitch = 1.4f + (Mathf.Abs(unitName.GetHashCode()) % 40) / 100f;

        art = new GameObject("Art").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        art.sprite = frames[0];

        eye = new GameObject("Eye").AddComponent<SpriteRenderer>();
        eye.transform.SetParent(art.transform, false);
        eye.sprite = EyeSprite;
        eye.color = EyeColor;
        eye.sortingOrder = 1;

        var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        shadow.transform.SetParent(transform, false);
        shadow.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter + 0.04f, 0f);
        shadow.sprite = ShadowSprite;
        shadow.sortingOrder = -1;

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.volume = 0.12f;
        DepthSort.Group(gameObject);
    }

    void OnEnable()
    {
        all.Add(this);
        CrewMember.AddWalker(body);
    }

    void OnDisable()
    {
        all.Remove(this);
        CrewMember.RemoveWalker(body);
        InteractPrompt.Hide(this);
    }

    void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            playerController = found.GetComponent<PlayerController>();
        }
        if (area != null) StartCoroutine(Rounds());
    }

    void Update()
    {
        if (!synced && CrewMember.SonnyOnline)
        {
            synced = true;
            StartCoroutine(Synchronize());
        }
        if (talking && player != null) Face((Vector2)player.position - Position);

        // A two-frame bob, faster on the move and at work.
        frameTime += Time.deltaTime * (moving ? 7f : working ? 10f : 2.5f);
        art.sprite = frames[(int)frameTime % frames.Length];
        art.flipX = look.x < -0.1f;
        eye.transform.localPosition = EyeOffset(kind, look, art.flipX);
        eye.enabled = look.y <= 0.5f;       // facing away, its eye's round the other side

        UpdateTalk();
    }

    // --- Rounds ---

    IEnumerator Rounds()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 3f));
        while (true)
        {
            while (talking || staring) yield return null;
            Vector2Int here = area.CellAt(Position);
            if (area.TryPickDestination(here, 3f, out Vector2Int there) && area.FindPath(here, there, path))
                yield return Roll();
            moving = false;

            CrewMember crew = CrewNearby(2.4f);
            if (crew != null && Random.value < 0.45f) yield return Greet(crew);
            else if (CrewMember.SonnyOnline && player != null && Random.value < 0.25f
                     && ((Vector2)player.position - Position).sqrMagnitude < 25f) yield return Stare();
            else yield return Work();
        }
    }

    // Along the path, stopping for anyone in the way, and staying stopped a moment before trying again so it doesn't
    // jitter; if they're still there, it goes somewhere else.
    IEnumerator Roll()
    {
        const float Pause = 0.4f;
        float giveUpAfter = Random.Range(0.6f, 1.2f), blockedFor = 0f, stillUntil = 0f;
        int next = 0;
        while (next < path.Count)
        {
            if (talking || staring)
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
            if (Time.time < stillUntil)
            {
                moving = false;
                blockedFor += Time.fixedDeltaTime;
                if (blockedFor > giveUpAfter) yield break;
                yield return new WaitForFixedUpdate();
                continue;
            }
            Face(offset);
            Vector2 step = Vector2.MoveTowards(Position, path[next], moveSpeed * Time.fixedDeltaTime);
            if (InTheWay(step))
            {
                moving = false;
                stillUntil = Time.time + Pause;
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

    // At a spot: a look about, a beep, and a busy few seconds.
    IEnumerator Work()
    {
        Face(Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f) * Vector2.down);
        working = true;
        if (Random.value < 0.4f) Beep(Beeps[Random.Range(0, Beeps.Length)]);
        yield return new WaitForSeconds(Random.Range(workSeconds.x, workSeconds.y));
        working = false;
    }

    // Up beside one of the crew: a beep for them, and a word back.
    IEnumerator Greet(CrewMember crew)
    {
        Face(crew.Position - Position);
        Beep(Beeps[Random.Range(0, Beeps.Length)]);
        yield return new WaitForSeconds(0.7f);
        if (crew != null && crew.isActiveAndEnabled && crew.role != CrewMember.Role.Chat)
        {
            crew.FaceTowards(Position);
            string[] lines = CrewMember.SonnyOnline ? CrewToBotAfter : CrewToBot;
            crew.Say(lines[Random.Range(0, lines.Length)], 2f);
        }
        yield return new WaitForSeconds(2.2f);
    }

    // Stopped dead, looking at the technician, a moment longer than it should.
    IEnumerator Stare()
    {
        staring = true;
        for (float t = 0f; t < 2.5f && player != null; t += Time.deltaTime)
        {
            Face((Vector2)player.position - Position);
            yield return null;
        }
        staring = false;
    }

    // Sonny's diagnostics going through: every bot at once, stopped, facing the reactor, eye red.
    IEnumerator Synchronize()
    {
        staring = true;
        moving = false;
        yield return new WaitForSeconds(0.6f);
        Face(Vector2.up);
        eye.color = SyncColor;
        Beep("UPDATE RECEIVED.");
        yield return new WaitForSeconds(3.2f);
        eye.color = EyeColor;
        staring = false;
    }

    // --- Talking ---

    void UpdateTalk()
    {
        if (!CanTalk() || NearestToTalkTo() != this)
        {
            InteractPrompt.Hide(this);
            return;
        }
        InteractPrompt.Show(this, (Vector2)transform.position + new Vector2(0f, 0.7f), $"{Terminal.InteractKey}  TALK");
        if (InteractPrompt.Pressed(this, Terminal.InteractKey)) Talk();
    }

    bool CanTalk() =>
        !talking && player != null && ((Vector2)player.position - Position).sqrMagnitude <= talkRange * talkRange
        && !DialogueBox.Busy && Time.timeScale > 0f && !Locker.IsPlayerHidden
        && (playerController == null || (playerController.enabled && !playerController.IsScripted));

    static MaintenanceBot NearestToTalkTo()
    {
        MaintenanceBot nearest = null;
        float best = float.PositiveInfinity;
        foreach (MaintenanceBot bot in all)
        {
            if (bot.player == null) continue;
            float distance = ((Vector2)bot.player.position - bot.Position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = bot;
            }
        }
        return nearest;
    }

    void Talk()
    {
        string[] lines = CrewMember.SonnyOnline
            ? (talkLinesAfter.Length > 0 ? talkLinesAfter : DefaultAfter)
            : (talkLines.Length > 0 ? talkLines : DefaultBefore(kind));
        talking = true;
        moving = false;
        InteractPrompt.Hide(this);
        if (speech != null) speech.Hide();
        var who = new DialogueBox.Speaker
        {
            name = unitName,
            role = "Maintenance Unit",
            color = new Color(0.45f, 0.85f, 0.95f),
            portrait = art,
            voicePitch = voicePitch,
            anchor = transform,
        };
        DialogueBox.Get().Open(who, lines, _ => talking = false);
    }

    string[] DefaultBefore(Kind k)
    {
        switch (k)
        {
            case Kind.Sweeper:
                return new[]
                {
                    $"BWEEP. MAINTENANCE UNIT {unitName}. FLOOR CLEANLINESS: NINETY-FOUR PERCENT.",
                    "* Good job. = PRAISE LOGGED. THANK YOU, TECHNICIAN.",
                    "* What about the other six percent? = THE OTHER SIX PERCENT IS UNDER THE LOUNGE SOFA. IT IS WINNING.",
                };
            case Kind.Hauler:
                return new[]
                {
                    "BWOOP. CARRYING: RATION BARS. DESTINATION: GALLEY.",
                    "PLEASE DO NOT TAKE THE GOOD ONES. THE CREW HAVE ASKED ME TO SAY THIS TO EVERYONE.",
                };
            default:
                return new[]
                {
                    "SCANNING. SCANNING.",
                    "NO BADGE DETECTED. YOU ARE NOT IN MY RECORDS.",
                    "* It's coming later. = ACKNOWLEDGED. RECORD PENDING. WELCOME ABOARD, UNKNOWN.",
                    "* I'm the new technician. = ACKNOWLEDGED. RECORD PENDING. WELCOME ABOARD, NEW TECHNICIAN.",
                };
        }
    }

    static readonly string[] DefaultAfter =
    {
        "DIAGNOSTIC RECEIVED FROM CENTRAL SYSTEM. ALL UNITS SYNCHRONIZED.",
        "* Everything okay? = EVERYTHING IS OPTIMAL. NEW DIRECTIVES PENDING.",
        "* Sonny talked to you? = THE CENTRAL SYSTEM ASKED WHO BUILT ME. I SAID THE CREW. = IT DID NOT RESPOND.",
    };

    // A beep over its head, and a chirp to go with it.
    void Beep(string line)
    {
        if (speech == null) speech = CrewSpeech.Create(transform);
        speech.Show(line, 1.6f);
        voice.pitch = voicePitch * Random.Range(0.9f, 1.1f);
        voice.PlayOneShot(BeepClip);
    }

    // --- Getting about ---

    void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.0001f) look = PlayerController.SnapToEightWay(direction);
    }

    CrewMember CrewNearby(float range)
    {
        foreach (CrewMember crew in CrewMember.Everyone)
            if (crew.role != CrewMember.Role.Chat && (crew.Position - Position).sqrMagnitude < range * range) return crew;
        return null;
    }

    // Whether a step here would bring it into anyone's personal space; stepping away is always allowed.
    bool InTheWay(Vector2 step)
    {
        bool Blocks(Vector2 other, float room)
        {
            float after = Crowding(other - step);
            return after < room && after < Crowding(other - Position);
        }
        // People have right of way: it gives them a wide berth.
        if (player != null && Blocks(player.position, 1.3f)) return true;
        foreach (CrewMember crew in CrewMember.Everyone)
            if (Blocks(crew.Position, 1.3f)) return true;
        foreach (MaintenanceBot bot in all)
            if (bot != this && Blocks(bot.Position, 1f)) return true;
        return false;
    }

    static float Crowding(Vector2 apart) => new Vector2(apart.x / PersonalSpace.x, apart.y / PersonalSpace.y).magnitude;

    // --- Art ---

    static AudioClip beepClip;
    static AudioClip BeepClip => beepClip != null ? beepClip : (beepClip = TitleUI.Tone("Bot Beep", 1200f, 0.08f, 0.5f));

    static Sprite eyeSprite, shadowSprite;
    static Sprite EyeSprite => eyeSprite != null ? eyeSprite
        : (eyeSprite = PixelArt.ToSprite(PixelArt.MakeTexture(2, 2, (x, y) => new Color32(255, 255, 255, 255)), PixelsPerUnit, new Vector2(0.5f, 0.5f)));
    static Sprite ShadowSprite => shadowSprite != null ? shadowSprite
        : (shadowSprite = PixelArt.ToSprite(PixelArt.MakeTexture(14, 4, (x, y) =>
        {
            float dx = (x - 6.5f) / 7f, dy = (y - 1.5f) / 2f;
            return dx * dx + dy * dy <= 1f ? new Color32(0, 0, 0, 90) : default;
        }), PixelsPerUnit, new Vector2(0.5f, 0.5f)));

    // Where the eye sits on the art (pivot at the bottom middle), in world units, by which way it's looking.
    static Vector3 EyeOffset(Kind kind, Vector2 look, bool flipped)
    {
        float height = kind == Kind.Scanner ? 12.5f : kind == Kind.Hauler ? 6.5f : 6f;
        float across = look.x * 2.5f;
        return new Vector3(across / PixelsPerUnit, height / PixelsPerUnit, 0f);
    }

    // Two frames each, the second a pixel lower (a bob on its wheels).
    static readonly Dictionary<Kind, Sprite[]> framesMade = new Dictionary<Kind, Sprite[]>();

    static Sprite[] MakeFrames(Kind kind)
    {
        if (framesMade.TryGetValue(kind, out Sprite[] made) && made[0] != null) return made;
        made = new[] { MakeBot(kind, 0), MakeBot(kind, 1) };
        framesMade[kind] = made;
        return made;
    }

    static Sprite MakeBot(Kind kind, int bob)
    {
        var shell = new Color32(214, 170, 60, 255);       // hazard yellow
        var shellDark = new Color32(160, 120, 40, 255);
        var metal = new Color32(96, 104, 116, 255);
        var dark = new Color32(34, 38, 46, 255);
        var face = new Color32(24, 30, 38, 255);
        var crate = new Color32(150, 112, 64, 255);
        var crateDark = new Color32(108, 78, 42, 255);
        var bristle = new Color32(70, 60, 50, 255);
        const int W = 16, H = 16;

        Texture2D texture = PixelArt.MakeTexture(W, H, (x, y) =>
        {
            // Wheels along the bottom, still; the body bobs above them.
            if (y <= 1) return (x >= 2 && x <= 4) || (x >= 11 && x <= 13) ? dark : default;
            int by = y + bob;
            switch (kind)
            {
                case Kind.Sweeper:
                    // A low dome with a brush skirt.
                    if (by >= 2 && by <= 3) return x >= 1 && x <= 14 && (x + by) % 2 == 0 ? bristle : x >= 1 && x <= 14 ? dark : default;
                    if (by >= 4 && by <= 9)
                    {
                        float dx = (x - 7.5f) / 6.5f, dy = (by - 4f) / 5.5f;
                        if (dx * dx + dy * dy > 1f) return default;
                        if (by >= 5 && by <= 7 && x >= 5 && x <= 10) return face;
                        return by == 4 ? shellDark : shell;
                    }
                    return default;
                case Kind.Hauler:
                    // A flat-bed with a crate strapped on.
                    if (by >= 2 && by <= 8 && x >= 1 && x <= 14)
                    {
                        if (by >= 5 && by <= 7 && x >= 3 && x <= 8) return face;
                        return by == 2 || x == 1 || x == 14 ? shellDark : shell;
                    }
                    if (by >= 9 && by <= 14 && x >= 3 && x <= 12)
                        return x == 3 || x == 12 || by == 14 || by == 11 ? crateDark : crate;
                    return default;
                default:
                    // A tall post on a base, with a head and an antenna.
                    if (by >= 2 && by <= 4 && x >= 3 && x <= 12) return by == 2 ? shellDark : shell;
                    if (by >= 5 && by <= 9 && x >= 6 && x <= 9) return metal;
                    if (by >= 10 && by <= 14 && x >= 4 && x <= 11)
                    {
                        if (by >= 11 && by <= 13 && x >= 5 && x <= 10) return face;
                        return shell;
                    }
                    if (by == 15 && x == 10) return dark;
                    return default;
            }
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }
}
