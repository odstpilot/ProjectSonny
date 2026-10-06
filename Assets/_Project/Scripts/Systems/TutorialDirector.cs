using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Runs the tutorial: a flash-forward to the end of the game, deep in the station and already hunted, on the way to the
// control room as the solar flare hits. The station rumbles and sheds its ceiling the whole way, and each room teaches
// one thing:
//   bursting out of the reactor at a dead run with a pack of robots on their heels, more blasting out of the walls
//   as they pass and the lamps dying behind them, the camera cutting about like a film's, until the ceiling comes down
//   on them all -> move -> run, as the wall behind blows in and more come after them down the hallway, lamps going out
//   at their back; one bursts out right on top of them and grabs them (mash E to break free); the ceiling comes down on
//   the rest -> a robot powers up: the wrench -> across a collapsed floor with the ceiling coming down all around -> an
//   ambush: the walls of the next room blow in, one after the other, and the robots that come through have to be dealt
//   with; then the door on is jammed, and something on the other side punches a hole through the wall beside it, which
//   is the way on -> an arena: the scrap gun; then a cracked, glowing panel in the wall has to be shot open, and a third
//   robot comes through it: switching between the two -> the last hallway, alarms going, Sonny on every screen -> the
//   control room, where Sonny is waiting, and the game cuts back to the start.
// The player can't die here: hits still land and knock them about, but no health is lost, and the health readout is
// hidden. Only the wrench and the scrap gun are in hand; the blaster and the EMP wait for the chapters that teach them.
// Over the whole level the windows whiten as the player gets closer to the control room: the flare, as a countdown.
// Stand still for a while and a faint arrow points the way on.
// Sonny > Build Tutorial Level builds the rooms and fills in the references below. Any of them can be left empty and
// that beat is skipped. In the Editor, F6 jumps to the next beat.
public class TutorialDirector : MonoBehaviour
{
    [Header("Zones (walking in starts each beat)")]
    public PlayerTriggerZone collapseZone;
    public PlayerTriggerZone runZone;
    public PlayerTriggerZone meleeZone;
    [Tooltip("The room with the collapsed floor, where the ceiling comes down all around the player on the way across.")]
    public PlayerTriggerZone crossingZone;
    [Tooltip("The room after the crossing, where the walls blow in.")]
    public PlayerTriggerZone ambushZone;
    public PlayerTriggerZone arenaZone;
    public PlayerTriggerZone finalZone;
    public PlayerTriggerZone revealZone;

    [Header("Doors")]
    public BlastDoor meleeExit;
    public BlastDoor arenaEntrance;
    public BlastDoor arenaExit;
    [Tooltip("Stays shut until the last hallway, then opens as the player comes near.")]
    public BlastDoor controlRoomDoor;

    [Header("Robots")]
    public RobotEncounter meleeRobots;
    [Tooltip("The robots that blow in through the walls of the ambush room, one breach at a time, and have to be beaten.")]
    public RobotEncounter ambushRobots;
    public List<WallBreach> ambushBreaches = new List<WallBreach>();
    [Tooltip("Seconds between one wall coming in and the next in the ambush.")]
    public float ambushInterval = 2.5f;
    public RobotEncounter arenaRobots;
    [Tooltip("Switched off behind the arena wall until partway through the fight, when it blows the wall in.")]
    public RobotEncounter breachRobots;
    [Tooltip("Where the arena wall blows in.")]
    public Transform breachPoint;
    [Tooltip("Seconds into the arena fight before the wall comes in anyway, if the player never breaks it. 0 waits for them.")]
    public float breachAfter = 90f;
    [Tooltip("The robots chasing the player out of the reactor at the start, until the ceiling comes down on them.")]
    public List<PlaceholderRobot> chasers = new List<PlaceholderRobot>();
    [Tooltip("More for the opening, switched off behind the corridor wall: each blasts out as the player runs past and joins the chase.")]
    public List<WallBreach> openingBreaches = new List<WallBreach>();
    [Tooltip("Switched off behind the run hallway's wall: each blasts out just behind the player as they pass, and hunts them down the hallway.")]
    public RobotEncounter hallwayHunters;
    public List<WallBreach> hallwayBreaches = new List<WallBreach>();
    [Tooltip("Where the ceiling comes down behind the player at the end of the run hallway, burying whatever's still after them.")]
    public Transform[] hallwayCollapsePoints = new Transform[0];
    [Tooltip("Behind the run hallway's wall: it bursts out right on top of the player and grabs them, until they break free.")]
    public RobotEncounter grabber;
    public List<WallBreach> grabberBreaches = new List<WallBreach>();
    [Tooltip("Presses of E it takes to break free of the grab.")]
    public int breakFreePresses = 7;

    [Header("The Way Round")]
    [Tooltip("The hole a robot punches in the ambush room's wall once the ambush is over, and the robot that does it. With both, the entrance jams, and walking into the hole takes the player through to the far side of it.")]
    public Teleporter arenaPassage;
    public RobotEncounter passageRobot;
    public List<WallBreach> passageBreaches = new List<WallBreach>();

    [Header("Weak Spots")]
    [Tooltip("The cracked panel in the arena wall with the breach robot behind it. Shooting it (or hitting it) open lets the robot in.")]
    public WeakWall arenaWeakWall;

    [Header("Set Pieces")]
    [Tooltip("Where the ceiling comes down behind the player at the start, sealing the way back.")]
    public Transform[] collapsePoints = new Transform[0];
    public SonnyBox sonny;
    public StationRumble rumble;
    [Tooltip("Alarm lamps that come on in the last hallway.")]
    public List<StationLight> alarms = new List<StationLight>();
    [Tooltip("Wall lamps. In the last hallway the ones between the player and the control room give out one by one.")]
    public List<StationLight> lamps = new List<StationLight>();
    [Tooltip("The scene's sounds (SoundManager) for the alarm and the heartbeat near the end. Empty for none.")]
    [SoundName] public string alarmSound = "Alarm";
    [SoundName] public string heartbeatSound = "Heartbeat";

    [Header("Words")]
    public string[] openingCard = { "The flare reaches the station in minutes", "Get to the control room" };
    public string objective = "Reach the control room";
    public string sonnyName = "SONNY";
    [Tooltip("Over the station speakers in the last hallway.")]
    public string[] sonnyInTheHallway = { "Technician. You are the last error left on this station." };
    [Tooltip("When the player walks into the control room.")]
    public string[] sonnyInTheControlRoom = { "You still believe you built me.", "Nothing so small makes something greater than itself." };
    public string[] closingCard = { "Earlier" };

    [Header("Afterwards")]
    [Tooltip("Loaded when the tutorial ends: Chapter 1 starts at the ship's entrance (ChapterOneBuilder). Until that scene is in the build, the tutorial ends on black.")]
    public string nextScene = "Chapter1";

    [Header("Player")]
    [Tooltip("Hits land but take no health, so the player can't die in the tutorial. The health readout is hidden too.")]
    public bool playerCannotDie = true;
    [Tooltip("Takes the blaster and the EMP off the hotbar, leaving the wrench and the scrap gun.")]
    public bool basicWeaponsOnly = true;
    [Tooltip("Seconds the player has to stand still before an arrow points the way on. 0 never shows it.")]
    public float guideAfter = 10f;

    [Header("The Chase")]
    [Tooltip("Seconds the player's ears ring after the ceiling comes down, before it's theirs to play.")]
    public float dazedSeconds = 1.8f;
    [Tooltip("Films the opening chase with its own camera moves (ChaseCamera) rather than just following the player.")]
    public bool cinematicChase = true;
    [Tooltip("How far behind the player the lamps give out as they run, in world units.")]
    public float lightsOutBehind = 3f;

    [Header("Camera")]
    [Tooltip("How far the camera pulls back for the arena fight, as a multiple of its normal view, so the wall coming in and every robot are on screen.")]
    public float arenaView = 1.18f;
    [Tooltip("How far it pushes in down the last hallway, closing in on the player as they near the control room.")]
    public float finalHallwayView = 0.88f;

    [Header("The End")]
    [Tooltip("Seconds to hold on Sonny after its last line, before the cut to black.")]
    public float holdOnSonny = 2f;
    [Tooltip("Seconds of black after the cut, while a low tone rings out, before the closing card.")]
    public float holdOnBlack = 2.5f;

    [Header("Testing")]
    [Tooltip("Skip the opening title card and the chase out of the reactor, to get straight into the level.")]
    public bool skipOpening;

    public string CurrentBeat { get; private set; } = "Starting";

    // A robot waiting switched off behind a wall, and the point on the wall it bursts through.
    [System.Serializable]
    public class WallBreach
    {
        public Health robot;
        public Transform point;
        [System.NonSerialized] public bool done;
    }

    private Transform player;
    private Rigidbody2D playerBody;
    private PlayerController controller;
    private PlayerCombat combat;
    private WeaponHotbar hotbar;
    private PlayerHealthHandler respawn;
    private TutorialHud hud;
    private Coroutine hallwayVoice;
    private readonly List<Behaviour> lockedControls = new List<Behaviour>();
    private bool fighting;          // a fight or a chase is on, so the guide arrow keeps out of the way
    private bool breached;
    private WeaponData stowedWeapon;
    private int shotsFired;
    private float flareStartX;
    private float flareEndX;
    private float normalView;       // the camera's usual size, which the level widens and narrows from
    private Vector2? chaseBreach;   // a wall that's just come in during the opening, for the camera to punch in on
    private bool chaseCollapsed;    // the ceiling's come down on the opening chase
    private Coroutine framing;

    void Start()
    {
        // Continuing from a save here: its one objective is the whole level, so it's been played; on to the next scene.
        if (SaveGame.ResumePending)
        {
            SaveGame.DoneResuming();
            if (!string.IsNullOrEmpty(nextScene))
            {
                hud = TutorialHud.Get();
                hud.SetFade(1f);
                enabled = false;
                SceneManager.LoadScene(nextScene);
                return;
            }
        }

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null)
        {
            Debug.LogError("TutorialDirector: there's no object tagged Player in the scene.", this);
            enabled = false;
            return;
        }

        player = found.transform;
        playerBody = found.GetComponent<Rigidbody2D>();
        controller = found.GetComponent<PlayerController>();
        combat = found.GetComponent<PlayerCombat>();
        hotbar = found.GetComponent<WeaponHotbar>();
        respawn = found.GetComponent<PlayerHealthHandler>();
        if (rumble == null) rumble = StationRumble.Instance;
        normalView = Camera.main != null ? Camera.main.orthographicSize : 0f;
        hud = TutorialHud.Get();
        SetUpPlayer(found);
        StationLight.SunBoost = 1f;
        StationLight.FlareProgress = 0f;
        flareStartX = player.position.x;
        flareEndX = revealZone != null ? revealZone.transform.position.x : flareStartX + 1f;

        foreach (StationLight alarm in alarms)
            if (alarm != null) alarm.SetOn(false);
        if (controlRoomDoor != null) controlRoomDoor.locked = true;

        StartCoroutine(Run());
        if (guideAfter > 0f) StartCoroutine(Guide());
    }

    void Update()
    {
        // The flare closing in: the further along the player is, the whiter the windows. It never backs off.
        if (player != null)
        {
            float along = Mathf.InverseLerp(flareStartX, flareEndX, player.position.x);
            StationLight.FlareProgress = Mathf.Max(StationLight.FlareProgress, along);
        }

#if UNITY_EDITOR || DEBUG
        if (Input.GetKeyDown(KeyCode.F6)) SkipAhead();
#endif
    }

    IEnumerator Run()
    {
        yield return Opening();
        yield return Move();
        yield return RunHallway();
        yield return Melee();
        yield return Crossing();
        yield return Ambush();
        yield return Arena();
        yield return FinalHallway();
        yield return Reveal();
    }

    // --- The beats ---

    IEnumerator Opening()
    {
        CurrentBeat = "Opening";
        if (skipOpening)
        {
            SkipTheChase();
            yield break;
        }

        SetWeapons(false);
        hud.SetFade(1f);
        if (rumble != null) rumble.Rumble(0.5f, 3f, 0);
        if (openingCard.Length > 0) yield return hud.TitleCard(openingCard, 1.6f);
        yield return TheChase();
        SetWeapons(true);
    }

    // Cold open: the player bursts out of the reactor at a dead run, robots on their heels, and once they're past, the
    // ceiling comes down on the robots and seals the reactor behind it. The player skids to a stop and looks back, ears
    // ringing, and then it's theirs to play.
    IEnumerator TheChase()
    {
        float speed = controller != null ? controller.moveSpeed * controller.sprintMultiplier : 5f;
        var bodies = new List<Rigidbody2D>();
        foreach (PlaceholderRobot chaser in chasers)
        {
            if (chaser == null) continue;
            chaser.enabled = false;     // run from here rather than by what they see
            bodies.Add(chaser.GetComponent<Rigidbody2D>());
        }
        // They stop dead where the ceiling is about to come down.
        float stopAt = collapsePoints.Length > 0 && collapsePoints[0] != null ? collapsePoints[0].position.x : float.MaxValue;

        if (controller != null) controller.SetScriptedInput(Vector2.right, true);
        StartCoroutine(hud.Letterbox(true, 0.3f));
        StartCoroutine(hud.FadeTo(0f, 0.8f));
        chaseCollapsed = false;
        Coroutine filming = cinematicChase ? StartCoroutine(ChaseCamera(bodies, stopAt)) : null;
        StartCoroutine(LightsOutBehind(float.NegativeInfinity, stopAt, () => chaseCollapsed));

        while (!Fired(collapseZone))
        {
            // Out of the wall just as the player's past, and after them with the rest.
            foreach (WallBreach breach in openingBreaches)
            {
                if (breach.done || breach.point == null || player.position.x < breach.point.position.x + 0.5f) continue;
                if (BlowIn(breach) is PlaceholderRobot joined)
                {
                    joined.enabled = false;
                    bodies.Add(joined.GetComponent<Rigidbody2D>());
                    chaseBreach = breach.point.position;
                }
            }
            DriveChasers(bodies, speed, stopAt);
            yield return null;
        }
        Checkpoint(collapseZone);
        chaseCollapsed = true;

        // Right on top of them.
        if (rumble != null) rumble.Rumble(0.9f, 2.2f, 0);
        foreach (Transform point in collapsePoints)
            if (point != null) FallingDebris.Drop(point.position, 50f, 0.7f, 1.2f, solid: true);

        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            DriveChasers(bodies, speed, stopAt);
            if (t > 0.35f && controller != null)
            {
                controller.SetScriptedInput(Vector2.zero, false);
                controller.SetFacingOverride(Vector2.left);
            }
            yield return null;
        }

        // Anything the rubble somehow missed is buried anyway.
        foreach (PlaceholderRobot chaser in chasers)
            if (chaser != null && chaser.TryGetComponent(out Health health)) health.Kill(gameObject);
        foreach (WallBreach breach in openingBreaches)
            if (breach.robot != null && breach.robot.gameObject.activeSelf) breach.robot.Kill(gameObject);

        if (filming != null) yield return filming;
        yield return Dazed(dazedSeconds);
        if (controller != null)
        {
            controller.ClearFacingOverride();
            controller.ClearScriptedInput();
        }
        StartCoroutine(hud.Letterbox(false, 0.6f));
    }

    // The opening, filmed: tight and tilted on the reactor doorway as the robots pour out, a swing over to the player
    // running for it, then running with them, a little ahead, the view swaying; a punch in (and a beat of slow motion) on
    // each wall that comes in; and when the ceiling comes down, a swing back in slow motion to watch it bury them, before
    // it settles on the player and hands back to following them. Runs on unscaled time.
    IEnumerator ChaseCamera(List<Rigidbody2D> pack, float collapseX)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic || normalView <= 0f) yield break;
        if (cam.TryGetComponent(out CameraFallow follow)) follow.enabled = false;
        float z = cam.transform.position.z;
        Vector2 lead = new Vector2(3.2f, 0.2f);

        void Frame(Vector2 at, float size, float tilt)
        {
            cam.transform.position = new Vector3(at.x, at.y, z);
            cam.orthographicSize = normalView * size;
            cam.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        }
        Vector2 Pack()
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Rigidbody2D body in pack)
                if (body != null) { sum += body.position; count++; }
            return count > 0 ? sum / count : (Vector2)player.position;
        }
        IEnumerator Move(System.Func<Vector2> to, float size, float tilt, float seconds, System.Func<float, float> ease)
        {
            Vector2 from = cam.transform.position;
            float fromSize = cam.orthographicSize / normalView;
            float fromTilt = Mathf.DeltaAngle(0f, cam.transform.eulerAngles.z);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = ease(Mathf.Clamp01(t / seconds));
                Frame(Vector2.Lerp(from, to(), k), Mathf.Lerp(fromSize, size, k), Mathf.Lerp(fromTilt, tilt, k));
                yield return null;
            }
            Frame(to(), size, tilt);
        }

        // On the doorway, close and canted, as they come through it.
        for (float t = 0f; t < 0.9f && !chaseCollapsed; t += Time.unscaledDeltaTime)
        {
            Frame(Pack() + new Vector2(0.6f + t * 0.8f, 0.3f), 0.6f - t * 0.05f, 6f - t * 2f);
            yield return null;
        }
        CameraShake.Shake(0.25f);

        // Swing over to the player.
        yield return Move(() => (Vector2)player.position + lead, 0.85f, -2f, 0.6f, TitleUI.EaseInOut);

        // Running with them, a little ahead, swaying; punching in on each wall as it comes in.
        float sway = 0f;
        while (!chaseCollapsed)
        {
            float dt = Time.unscaledDeltaTime;
            sway += dt;
            Vector2 want = (Vector2)player.position + lead;
            float size = 0.85f + Mathf.Sin(sway * 0.9f) * 0.01f;
            float tilt = -2f + Mathf.Sin(sway * 1.2f) * 0.5f;
            if (chaseBreach is Vector2 breach)
            {
                chaseBreach = null;
                HitStop.Hold(0.35f, 0.3f);
                CameraShake.Kick(Vector2.down, 0.3f);
                Vector2 toward = Vector2.Lerp(want, breach, 0.35f);
                yield return Move(() => toward, 0.72f, 2f, 0.3f, TitleUI.EaseOut);
                yield return Move(() => (Vector2)player.position + lead, 0.85f, -2f, 0.6f, TitleUI.EaseInOut);
                continue;
            }
            Vector2 at = Vector2.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-5f * dt));
            Frame(at, Mathf.Lerp(cam.orthographicSize / normalView, size, 1f - Mathf.Exp(-3f * dt)), tilt);
            yield return null;
        }

        // The ceiling coming down on them: swing back and watch, slowed right down.
        HitStop.Hold(0.3f, 1.1f);
        Vector2 onTheRubble = new Vector2(collapseX - 1f, player.position.y + 0.3f);
        yield return Move(() => onTheRubble, 0.7f, 3f, 0.7f, TitleUI.EaseInOut);
        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
        {
            Frame(onTheRubble + new Vector2(-t * 0.3f, 0f), 0.7f - t * 0.03f, 3f - t);
            yield return null;
        }

        // Back to the player, level again, and following as usual: eased onto exactly where the follow camera would be,
        // so it takes over without a jump.
        yield return Move(() => follow != null ? (Vector2)follow.Target : (Vector2)player.position, 1f, 0f, 1.1f, TitleUI.EaseInOut);
        cam.transform.rotation = Quaternion.identity;
        cam.orthographicSize = normalView;
        FollowPlayer();
    }

    // The lamps between fromX and toX go out behind the player as they pass, some bursting, some just dying, until
    // stop says so.
    IEnumerator LightsOutBehind(float fromX, float toX, System.Func<bool> stop)
    {
        List<StationLight> behind = lamps
            .Where(lamp => lamp != null && lamp.transform.position.x > fromX && lamp.transform.position.x < toX)
            .OrderBy(lamp => lamp.transform.position.x)
            .ToList();
        int next = 0;
        while (next < behind.Count && !stop())
        {
            StationLight lamp = behind[next];
            if (lamp != null && player.position.x > lamp.transform.position.x + lightsOutBehind)
            {
                if (next % 3 == 0) lamp.Break();
                else lamp.PowerOff();
                next++;
                continue;
            }
            yield return null;
        }
    }

    // Keeps the chasers running along behind the player, each stopping when it reaches stopAt.
    static void DriveChasers(List<Rigidbody2D> bodies, float speed, float stopAt)
    {
        foreach (Rigidbody2D body in bodies)
        {
            if (body == null) continue;
            float left = stopAt - body.position.x;
            body.linearVelocity = left <= 0f ? Vector2.zero : Vector2.right * Mathf.Min(speed, left / Mathf.Max(Time.deltaTime, 0.001f));
        }
    }

    // A blast too close: the picture smeared and dark, the world muffled under a ringing, clearing over seconds.
    IEnumerator Dazed(float seconds)
    {
        Volume smear = TutorialSetPieces.WakeEffects();
        AudioLowPassFilter muffle = null;
        AudioListener listener = FindAnyObjectByType<AudioListener>();
        if (listener != null) muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
        AudioClip made = SoundManager.Clip("Ear Ringing") == null ? TutorialSetPieces.Ringing() : null;
        AudioSource ringing = TutorialSetPieces.Speaker(gameObject, "Ear Ringing", made, true);
        ringing.Play();

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float left = 1f - Mathf.SmoothStep(0f, 1f, t / seconds);
            smear.weight = 0.8f * left;
            if (muffle != null) muffle.cutoffFrequency = Mathf.Lerp(22000f, 500f, left);
            ringing.volume = SoundManager.Volume("Ear Ringing") * left;
            yield return null;
        }

        Destroy(smear.sharedProfile);
        Destroy(smear.gameObject);
        if (muffle != null) Destroy(muffle);
        ringing.Stop();
        if (made != null) Destroy(made);
        Destroy(ringing);
    }

    // For testing without the opening: the chasers are gone and the rubble is already down.
    void SkipTheChase()
    {
        foreach (PlaceholderRobot chaser in chasers)
            if (chaser != null) Destroy(chaser.gameObject);
        foreach (WallBreach breach in openingBreaches)
            if (breach.robot != null) Destroy(breach.robot.gameObject);
        foreach (Transform point in collapsePoints)
            if (point != null) FallingDebris.Drop(point.position, 0f, 0.05f, 1.2f, solid: true);
    }

    IEnumerator Move()
    {
        CurrentBeat = "Move";
        hud.SetObjective(objective);
        hud.ShowPrompt("Move", "W", "A", "S", "D");

        Vector2 last = player.position;
        float walked = 0f;
        while (walked < 2.5f && !Fired(runZone))
        {
            yield return null;
            walked += Vector2.Distance(player.position, last);
            last = player.position;
        }
        hud.CompletePrompt();
    }

    IEnumerator RunHallway()
    {
        yield return WaitForZone(runZone);
        CurrentBeat = "Run";
        yield return WaitForPrompt();
        hud.ShowPromptWithHint("Run", "Hold while moving", "SHIFT");
        StartCoroutine(CeilingChase(meleeZone, 7f));
        StartCoroutine(HuntersInTheHallway());
        float hallwayEnd = meleeZone != null ? meleeZone.transform.position.x - 3f : float.MaxValue;
        StartCoroutine(LightsOutBehind(player.position.x, hallwayEnd, () => Fired(meleeZone)));

        float sprinted = 0f;
        while (sprinted < 0.8f && !Fired(meleeZone))
        {
            bool moving = Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f;
            if (moving && Input.GetKey(KeyCode.LeftShift) && controller != null && controller.enabled && controller.stamina > 0f)
                sprinted += Time.deltaTime;
            yield return null;
        }
        hud.CompletePrompt();
    }

    // The wall just behind the player blows in, one breach after another as they pass, and what comes out hunts them down
    // the hallway: faster than walking, slower than running. As they reach the melee room the ceiling comes down behind
    // them and buries whatever's still after them.
    IEnumerator HuntersInTheHallway()
    {
        if (hallwayBreaches.Count == 0 && grabberBreaches.Count == 0) yield break;
        bool woken = false;
        while (!Fired(meleeZone))
        {
            // Right on top of them, as they come level with it.
            foreach (WallBreach breach in grabberBreaches)
            {
                if (breach.done || breach.point == null || player.position.x < breach.point.position.x - 0.8f) continue;
                fighting = true;
                yield return Grab(breach);
            }
            foreach (WallBreach breach in hallwayBreaches)
            {
                if (breach.done || breach.point == null || player.position.x < breach.point.position.x + 1.5f) continue;
                BlowIn(breach);
                if (!woken && hallwayHunters != null)
                {
                    woken = true;
                    hallwayHunters.Activate();
                }
                fighting = true;
            }
            yield return null;
        }

        // Behind them, right across the hallway, and on top of anything still coming.
        if (rumble != null) rumble.Rumble(0.8f, 1.8f, 0);
        foreach (Transform point in hallwayCollapsePoints)
            if (point != null) FallingDebris.Drop(point.position, 50f, 0.5f, 1.2f, solid: true);
        foreach (WallBreach breach in hallwayBreaches.Concat(grabberBreaches))
            if (breach.robot != null && breach.robot.gameObject.activeSelf && !breach.robot.IsDead)
                FallingDebris.Drop(breach.robot.transform.position, 50f, 0.4f, 1.2f);
        yield return new WaitForSeconds(1.2f);
        // Any never let out stay in the wall for good.
        foreach (WallBreach breach in hallwayBreaches.Concat(grabberBreaches))
        {
            if (breach.robot == null || breach.robot.IsDead) continue;
            if (breach.done) breach.robot.Kill(gameObject);
            else Destroy(breach.robot.gameObject);
        }
        fighting = false;
    }

    // Out of the wall and onto them: it pins them where they stand, squeezing, until E has been pressed enough times to
    // tear free. Then it's thrown back, dazed for a moment, and after them with the rest.
    IEnumerator Grab(WallBreach breach)
    {
        PlaceholderRobot brain = BlowIn(breach);
        if (brain == null) yield break;
        brain.enabled = false;
        var body = brain.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        if (controller != null) controller.SetScriptedInput(Vector2.zero, false);
        HitStop.Hold(0.3f, 0.25f);
        FrameView(0.82f, 0.25f);

        // Onto them.
        Vector2 from = brain.transform.position;
        for (float t = 0f; t < 0.22f; t += Time.deltaTime)
        {
            Vector2 onto = (Vector2)player.position + new Vector2(-0.35f, 0.25f);
            Vector2 at = Vector2.Lerp(from, onto, t / 0.22f);
            if (body != null) body.MovePosition(at); else brain.transform.position = at;
            yield return null;
        }
        CameraShake.Kick(Vector2.down, 0.25f);
        CameraShake.Shake(0.4f);
        Sfx.Play("Wall Grab");
        TechnicianVoice.Say("Get OFF me!", 1.6f);

        MashPrompt.Show("MASH TO BREAK FREE", "E");
        yield return null;      // the press that got here doesn't count
        int presses = 0;
        float lastPress = Time.time;
        while (presses < breakFreePresses)
        {
            Vector2 onto = (Vector2)player.position + new Vector2(-0.35f, 0.25f);
            if (body != null) body.MovePosition(onto + Random.insideUnitCircle * 0.04f);
            if (Input.GetKeyDown(KeyCode.E) && Time.timeScale > 0f)
            {
                presses++;
                lastPress = Time.time;
                CameraShake.Shake(0.12f);
                CameraShake.Kick(Random.insideUnitCircle.normalized, 0.08f);
                Sfx.Play("Grab Struggle", 0.8f, 0.9f + 0.3f * presses / breakFreePresses);
                MashPrompt.Press(presses / (float)breakFreePresses);
            }
            // It squeezes while they don't fight it.
            if (Time.time - lastPress > 1.4f)
            {
                lastPress = Time.time;
                CameraShake.Shake(0.2f);
                Sfx.Play("Grab Squeeze");
                MashPrompt.Squeeze();
                if (player.TryGetComponent(out Health health))
                    health.TakeDamage(new DamageInfo(1f, Vector2.down, 0f, brain.gameObject, player.position));
                if (controller != null) controller.SetScriptedInput(Vector2.zero, false);
            }
            yield return null;
        }
        MashPrompt.Hide();

        // Torn free: it's flung back, stunned, then comes on again.
        Sfx.Play("Break Free");
        CameraShake.Kick(Vector2.left, 0.35f);
        HitStop.Hold(0.4f, 0.2f);
        FrameView(1f, 0.5f);
        if (controller != null) controller.ClearScriptedInput();
        Vector2 away = ((Vector2)brain.transform.position - (Vector2)player.position).normalized;
        if (away.sqrMagnitude < 0.01f) away = Vector2.left;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            if (body != null) body.linearVelocity = away * 7f * (1f - t / 0.25f);
            yield return null;
        }
        if (body != null) body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(1.2f);
        if (grabber != null) grabber.Activate();
        if (brain != null) brain.enabled = true;
    }

    IEnumerator Melee()
    {
        yield return WaitForZone(meleeZone);
        CurrentBeat = "Melee";
        Checkpoint(meleeZone);
        if (meleeRobots != null) meleeRobots.Activate();
        fighting = true;
        yield return WaitForPrompt();

        // Each prompt also gives way if the fight is won some other way.
        if (combat != null && !(combat.CurrentWeapon is MeleeWeaponData))
        {
            hud.ShowPrompt("Wrench", SlotKey<MeleeWeaponData>());
            yield return new WaitUntil(() => combat.CurrentWeapon is MeleeWeaponData || Cleared(meleeRobots));
            hud.CompletePrompt();
            yield return WaitForPrompt();
        }

        if (!Cleared(meleeRobots))
        {
            hud.ShowPromptWithHint("Swing", "Hold to charge a heavy swing", "LEFT CLICK");
            yield return WaitForCleared(meleeRobots);
            hud.CompletePrompt();
        }
        fighting = false;
        Open(meleeExit);
    }

    // No lesson here: the station gives way around the player as they cross the room with the collapsed floor.
    IEnumerator Crossing()
    {
        yield return WaitForZone(crossingZone);
        CurrentBeat = "Crossing";
        Checkpoint(crossingZone);
        if (rumble != null) rumble.Rumble(0.6f, 2.5f, 1);
        yield return new WaitForSeconds(1.5f);
        yield return CeilingChase(ambushZone, 6f);
    }

    // The walls of the room after the crossing blow in, one after another, and what comes through has to be beaten with the
    // wrench before the way on (the arena entrance) opens again.
    IEnumerator Ambush()
    {
        yield return WaitForZone(ambushZone);
        CurrentBeat = "Ambush";
        Checkpoint(ambushZone);
        if (ambushBreaches.Count == 0) yield break;
        Close(arenaEntrance);
        fighting = true;
        if (rumble != null) rumble.Rumble(0.5f, 2f, 1);
        yield return new WaitForSeconds(0.8f);

        // Nearest the player first, the rest after, a few seconds apart.
        List<WallBreach> order = ambushBreaches.Where(b => b.point != null)
            .OrderBy(b => Vector2.Distance(b.point.position, player.position)).ToList();
        for (int i = 0; i < order.Count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(ambushInterval);
            BlowIn(order[i]);
            if (i == 0 && ambushRobots != null) ambushRobots.Activate();
        }

        yield return WaitForCleared(ambushRobots);
        fighting = false;
        if (arenaPassage == null || passageBreaches.Count == 0)
        {
            Open(arenaEntrance);
            yield break;
        }
        yield return TheWayRound();
    }

    // The door on won't open: it's jammed. Something behind the room's wall hits it, again, and again, and comes through,
    // and the hole it leaves is the way on: walking up into it takes the player through to the far side of the door.
    IEnumerator TheWayRound()
    {
        if (arenaEntrance != null) arenaEntrance.locked = true;
        yield return new WaitForSeconds(0.6f);
        TechnicianVoice.Say("Jammed. Come on...", 1.8f);
        yield return new WaitForSeconds(1.2f);

        Vector2 wall = passageBreaches[0].point != null ? (Vector2)passageBreaches[0].point.position : (Vector2)arenaPassage.transform.position;
        for (int knock = 0; knock < 2; knock++)
        {
            StationRumble.PlayImpact(wall);
            CameraShake.Shake(0.3f + knock * 0.15f);
            yield return new WaitForSeconds(0.7f);
        }

        foreach (WallBreach breach in passageBreaches) BlowIn(breach);
        if (passageRobot != null) passageRobot.Activate();
        fighting = true;
        yield return WaitForCleared(passageRobot);
        fighting = false;
        arenaPassage.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        TechnicianVoice.Say("Through there, then.", 1.8f);
    }

    // The wall at the breach blows in and the robot behind it comes through. Returns its brain.
    PlaceholderRobot BlowIn(WallBreach breach)
    {
        if (breach.done) return null;
        breach.done = true;
        if (breach.point != null) TutorialSetPieces.BlowInWall(breach.point.position);
        if (breach.robot == null) return null;
        breach.robot.gameObject.SetActive(true);
        return breach.robot.GetComponent<PlaceholderRobot>();
    }

    // Two robots to start, and the rivet gun to take them on with. Partway through, a third blows in through the wall,
    // and the lesson is switching: the wrench up close, the gun at range.
    IEnumerator Arena()
    {
        yield return WaitForZone(arenaZone);
        CurrentBeat = "Arena";
        Checkpoint(arenaZone);
        Close(arenaEntrance);
        if (rumble != null)
        {
            rumble.intensity = 1.4f;
            rumble.Rumble(0.6f, 1.8f, 1);
        }
        if (arenaRobots != null) arenaRobots.Activate();
        fighting = true;
        FrameView(arenaView, 1.2f);
        StartCoroutine(BreachWhenDue(Time.time));
        yield return WaitForPrompt();

        RangedCombat gun = player.GetComponent<RangedCombat>();
        if (gun != null) gun.Fired += CountShot;
        bool hasGun = HasSlot<RangedWeaponData>();

        if (hasGun && combat != null && !(combat.CurrentWeapon is RangedWeaponData))
        {
            hud.ShowPromptWithHint("Scrap gun", "Or scroll the mouse wheel", SlotKey<RangedWeaponData>());
            yield return new WaitUntil(() => combat.CurrentWeapon is RangedWeaponData || ArenaWon());
            hud.CompletePrompt();
            yield return WaitForPrompt();
        }

        if (hasGun && !ArenaWon())
        {
            int before = shotsFired;
            hud.ShowPromptWithHint("Shoot", "Aim with the mouse", "LEFT CLICK");
            yield return new WaitUntil(() => shotsFired > before || ArenaWon());
            hud.CompletePrompt();
        }

        // The room's quiet, but the panel's still glowing: the lesson is that the gun breaks things, not just robots.
        yield return new WaitUntil(() => breached || arenaRobots == null || arenaRobots.IsCleared);
        if (!breached && arenaWeakWall != null)
        {
            yield return WaitForPrompt();
            SetControls(false);
            yield return PanCameraTo(arenaWeakWall.transform.position + Vector3.down, 0.7f);
            yield return new WaitForSeconds(0.6f);
            yield return PanCameraTo(player.position, 0.5f);
            FollowPlayer();
            SetControls(true);
            hud.ShowPromptWithHint("Shoot the cracked wall", "Weak spots give way", "LEFT CLICK");
            yield return new WaitUntil(() => breached);
            hud.CompletePrompt();
        }
        yield return new WaitUntil(() => breached || ArenaWon());
        yield return WaitForPrompt();

        if (hasGun && !ArenaWon())
        {
            WeaponData held = combat != null ? combat.CurrentWeapon : null;
            float shownAt = Time.time;
            hud.ShowPromptWithHint("Switch weapons", "Wrench up close, scrap gun at range",
                SlotKey<MeleeWeaponData>(), SlotKey<RangedWeaponData>(), "SCROLL");
            yield return new WaitUntil(() => (combat != null && combat.CurrentWeapon != held) || ArenaWon() || Time.time - shownAt > 10f);
            if (combat != null && combat.CurrentWeapon != held) hud.CompletePrompt();
            else hud.HidePrompt();
        }

        yield return new WaitUntil(ArenaWon);
        if (gun != null) gun.Fired -= CountShot;
        fighting = false;
        FrameView(1f, 1.5f);
        Open(arenaExit);
    }

    void CountShot() => shotsFired++;

    bool ArenaWon() => (arenaRobots == null || arenaRobots.IsCleared) && (breachRobots == null || (breached && breachRobots.IsCleared));

    // The wall comes in when the player breaks the cracked panel in it (shot or hit), whenever they choose to. Without a
    // panel, as the last of the first robots goes down. After breachAfter it comes in anyway, so nobody's stuck.
    IEnumerator BreachWhenDue(float startedAt)
    {
        yield return new WaitUntil(() => breached
            || (arenaWeakWall != null ? arenaWeakWall.IsBroken : arenaRobots != null && arenaRobots.IsCleared)
            || (breachAfter > 0f && Time.time - startedAt > breachAfter));
        Breach();
    }

    void Breach()
    {
        if (breached) return;
        breached = true;
        if (breachRobots == null) return;

        Vector2 at = breachPoint != null ? (Vector2)breachPoint.position : (Vector2)breachRobots.transform.position;
        if (arenaWeakWall != null) arenaWeakWall.Break();
        TutorialSetPieces.BlowInWall(at);
        if (rumble != null) rumble.Rumble(0.7f, 1.2f, 0);
        foreach (Health robot in breachRobots.robots)
            if (robot != null) robot.gameObject.SetActive(true);
        breachRobots.Activate();
    }

    IEnumerator FinalHallway()
    {
        yield return WaitForZone(finalZone);
        CurrentBeat = "Final Hallway";
        Checkpoint(finalZone);

        foreach (StationLight alarm in alarms)
            if (alarm != null) alarm.SetOn(true);
        if (rumble != null)
        {
            rumble.intensity = 2f;
            rumble.SetLightBase(rumble.BaseLightIntensity * 0.75f, new Color(1f, 0.72f, 0.68f));
            rumble.PlaySound(alarmSound, 0.8f);
            rumble.Rumble(0.7f, 2.5f, 2);
        }
        if (controlRoomDoor != null) controlRoomDoor.locked = false;
        FrameView(finalHallwayView, 8f);
        hallwayVoice = StartCoroutine(SayAll(sonnyInTheHallway, 2f, true));
        StartCoroutine(LightsOutAhead());
        StartCoroutine(RaiseSun(1.5f, 8f));
    }

    IEnumerator Reveal()
    {
        yield return WaitForZone(revealZone);
        CurrentBeat = "Reveal";
        SetControls(false);
        hud.HidePrompt();
        hud.SetObjective("");
        Close(controlRoomDoor);
        if (rumble != null)
        {
            rumble.rumbleOnItsOwn = false;
            rumble.PlaySound(heartbeatSound, 1f);
        }
        if (hallwayVoice != null) yield return hallwayVoice;

        StartCoroutine(hud.Letterbox(true, 0.8f));
        yield return PanCameraTo(sonny != null ? (Vector2)sonny.transform.position + new Vector2(-2.5f, 1f) : (Vector2)player.position, 2.5f);

        if (sonny != null) sonny.Awaken();
        // The sunlight sinks as Sonny wakes, so its red is what fills the room, and every screen is Sonny's.
        StartCoroutine(RaiseSun(0.6f, 2.5f));
        SonnyScreen.SetAll(true);
        yield return new WaitForSeconds(1.2f);
        yield return SayAll(sonnyInTheControlRoom, 1.6f, false);

        // Let the last line sit before anything moves.
        yield return new WaitForSeconds(holdOnSonny);
        if (rumble != null) rumble.Rumble(1f, 1.5f, 0);
        yield return new WaitForSeconds(0.5f);

        // Hard cut to black and silence, but for one low tone ringing out.
        CurrentBeat = "Ending";
        hud.SetFade(1f);
        AudioListener.volume = 0f;
        AudioSource tone = TutorialSetPieces.Speaker(gameObject, "Ending Tone",
            SoundManager.Clip("Ending Tone") == null ? TutorialSetPieces.LowTone() : null, false);
        tone.volume = SoundManager.Volume("Ending Tone");
        tone.Play();
        yield return new WaitForSecondsRealtime(holdOnBlack);
        yield return hud.TitleCard(closingCard, 1.5f);
        AudioListener.volume = GameSettings.MasterVolume;
        LoadNextScene();
    }

    // --- Helpers ---

    // The station keeps coming apart around the player for a while: every so often a piece crashes down some way off,
    // mostly ahead where they can see it coming, now and then off to one side or back the way they came.
    IEnumerator CeilingChase(PlayerTriggerZone until, float seconds)
    {
        if (rumble != null) rumble.Rumble(0.35f, seconds, 0);

        float end = Time.time + seconds;
        Vector2 last = player.position;
        while (Time.time < end && !Fired(until))
        {
            yield return new WaitForSeconds(Random.Range(1.3f, 2.1f));

            Vector2 now = player.position;
            float heading = Mathf.Abs(now.x - last.x) > 0.05f ? Mathf.Sign(now.x - last.x) : 1f;
            last = now;
            float along = Random.value < 0.8f ? heading * Random.Range(3.5f, 6.5f) : -heading * Random.Range(3.5f, 5f);
            Vector2 point = now + new Vector2(along, Random.Range(-1.4f, 1.4f));
            if (rumble == null || rumble.IsClearFloor(point))
                FallingDebris.Drop(point, 1f, 0.9f);
        }
    }

    // The lamps between the player and the control room give out one after another, some bursting (and some of those
    // tearing off the wall and falling), some just dying.
    IEnumerator LightsOutAhead()
    {
        if (finalZone == null) yield break;
        float from = finalZone.transform.position.x;
        float to = revealZone != null ? revealZone.transform.position.x : float.MaxValue;
        List<StationLight> ahead = lamps
            .Where(lamp => lamp != null && lamp.transform.position.x > from && lamp.transform.position.x < to)
            .OrderBy(lamp => lamp.transform.position.x)
            .ToList();

        yield return new WaitForSeconds(1f);
        for (int i = 0; i < ahead.Count; i++)
        {
            if (i % 2 == 0) ahead[i].Break();
            else ahead[i].PowerOff();
            yield return new WaitForSeconds(0.6f);
        }
    }

    // The flare getting closer: the sunlight through every window swells.
    IEnumerator RaiseSun(float boost, float seconds)
    {
        float start = StationLight.SunBoost;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            StationLight.SunBoost = Mathf.Lerp(start, boost, t / seconds);
            yield return null;
        }
        StationLight.SunBoost = boost;
    }

    // Sonny's lines, one after another. Every monitor switches over to Sonny while it speaks, and back afterwards if
    // screensBackAfter.
    IEnumerator SayAll(string[] lines, float holdSeconds, bool screensBackAfter)
    {
        SonnyScreen.SetAll(true);
        foreach (string line in lines)
            yield return hud.Say(sonnyName, line, holdSeconds);
        if (screensBackAfter) SonnyScreen.SetAll(false);
    }

    // After standing still for guideAfter seconds, a faint arrow by the player points at the next place to go. It keeps
    // out of the way of fights, prompts, and hiding, and fades as soon as they move on.
    IEnumerator Guide()
    {
        SpriteRenderer arrow = TutorialSetPieces.GuideArrow();
        Vector2 anchor = player.position;
        float stillSince = Time.time;
        float alpha = 0f;

        while (true)
        {
            Vector2 here = player.position;
            if (Vector2.Distance(here, anchor) > 0.6f)
            {
                anchor = here;
                stillSince = Time.time;
            }

            Vector2? target = NextPlace();
            bool free = controller != null && controller.enabled && !fighting && !hud.PromptShowing;
            bool show = target.HasValue && free && Time.time - stillSince > guideAfter;
            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.deltaTime * (show ? 0.8f : 3f));

            arrow.enabled = alpha > 0.01f && target.HasValue;
            if (arrow.enabled)
            {
                Vector2 way = (target.Value - here).normalized;
                float bob = 0.12f * Mathf.Sin(Time.time * 3f);
                arrow.transform.position = here + way * (1.2f + bob);
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg);
                arrow.color = new Color(1f, 1f, 1f, alpha * (0.35f + 0.2f * Mathf.Sin(Time.time * 3f)));
            }
            yield return null;
        }
    }

    // The next zone the player hasn't reached yet.
    Vector2? NextPlace()
    {
        foreach (PlayerTriggerZone zone in ZonesInOrder())
            if (zone != null && !zone.HasFired) return zone.transform.position;
        return null;
    }

    PlayerTriggerZone[] ZonesInOrder() => new[] { collapseZone, runZone, meleeZone, crossingZone, ambushZone, arenaZone, finalZone, revealZone };

    // Widens or narrows what the camera shows, as a multiple of its usual view.
    void FrameView(float amount, float seconds)
    {
        if (normalView <= 0f) return;
        if (framing != null) StopCoroutine(framing);
        framing = StartCoroutine(FrameRoutine(normalView * amount, seconds));
    }

    IEnumerator FrameRoutine(float size, float seconds)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) yield break;

        float from = cam.orthographicSize;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            cam.orthographicSize = Mathf.Lerp(from, size, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        cam.orthographicSize = size;
        framing = null;
    }

    // Takes the camera off the player and eases it over to a point.
    IEnumerator PanCameraTo(Vector2 target, float seconds)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        if (cam.TryGetComponent(out CameraFallow follow)) follow.enabled = false;

        Vector3 from = cam.transform.position;
        Vector3 to = new Vector3(target.x, target.y, from.z);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            cam.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        cam.transform.position = to;
    }

    // Hands the camera back to following the player.
    void FollowPlayer()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraFallow follow)) follow.enabled = true;
    }

    // Freezes the player where they stand for cutscenes, remembering what was switched off so exactly that comes back.
    void SetControls(bool on)
    {
        if (player == null) return;

        if (on)
        {
            foreach (Behaviour behaviour in lockedControls)
                if (behaviour != null) behaviour.enabled = true;
            lockedControls.Clear();
            return;
        }

        if (controller != null) controller.SetAnimBool(PlayerAnimParams.IsMoving, false);
        Behaviour[] controls = { controller, hotbar, player.GetComponent<MeleeCombat>(), player.GetComponent<RangedCombat>() };
        foreach (Behaviour behaviour in controls)
        {
            if (behaviour == null || !behaviour.enabled) continue;
            behaviour.enabled = false;
            lockedControls.Add(behaviour);
        }
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
    }

    // Puts the player's weapon away and keeps it away, switching too, or hands it back.
    void SetWeapons(bool on)
    {
        if (combat == null) return;
        if (!on)
        {
            if (combat.CurrentWeapon != null) stowedWeapon = combat.CurrentWeapon;
            else if (stowedWeapon == null) stowedWeapon = combat.startingWeapon;
            combat.Unequip();
            if (hotbar != null) hotbar.enabled = false;
            return;
        }

        if (hotbar != null) hotbar.enabled = true;
        if (combat.CurrentWeapon == null && stowedWeapon != null) combat.Equip(stowedWeapon);
    }

    void SetUpPlayer(GameObject found)
    {
        if (playerCannotDie && found.TryGetComponent(out Health health))
        {
            health.cannotDie = true;
            HealthHud.Get().SetCellsVisible(false);
        }

        if (basicWeaponsOnly && hotbar != null)
        {
            // The rivet gun is a plain RangedWeaponData; the blaster and the EMP are kinds of it.
            hotbar.slots.RemoveAll(weapon => !(weapon is MeleeWeaponData) && (weapon == null || weapon.GetType() != typeof(RangedWeaponData)));
            if (combat != null && combat.CurrentWeapon != null && !hotbar.slots.Contains(combat.CurrentWeapon))
                combat.Equip(hotbar.slots.Count > 0 ? hotbar.slots[0] : null);
        }
    }

    void Checkpoint(PlayerTriggerZone zone)
    {
        if (respawn == null || zone == null) return;
        Vector3 point = zone.transform.position;
        respawn.RespawnPoint = new Vector3(point.x, point.y, player.position.z);
    }

    // The number key for the first hotbar slot holding that kind of weapon.
    string SlotKey<T>() where T : WeaponData
    {
        if (hotbar != null)
        {
            for (int i = 0; i < hotbar.slots.Count && i < 9; i++)
                if (hotbar.slots[i] is T) return (i + 1).ToString();
        }
        return typeof(T) == typeof(RangedWeaponData) ? "2" : "1";
    }

    bool HasSlot<T>() where T : WeaponData => hotbar != null && hotbar.slots.Any(weapon => weapon is T);

    IEnumerator WaitForZone(PlayerTriggerZone zone)
    {
        while (zone != null && !zone.HasFired)
            yield return null;
    }

    IEnumerator WaitForCleared(RobotEncounter encounter)
    {
        while (encounter != null && !encounter.IsCleared)
            yield return null;
    }

    // Lets a finished prompt show its green tick before the next one replaces it.
    IEnumerator WaitForPrompt()
    {
        while (hud.PromptCompleting)
            yield return null;
    }

    static bool Fired(PlayerTriggerZone zone)
    {
        return zone != null && zone.HasFired;
    }

    static bool Cleared(RobotEncounter encounter)
    {
        return encounter != null && encounter.IsCleared;
    }

    static void Open(BlastDoor door)
    {
        if (door != null) door.Open();
    }

    static void Close(BlastDoor door)
    {
        if (door != null) door.Close();
    }

    void LoadNextScene()
    {
        if (Application.CanStreamedLevelBeLoaded(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.Log($"TutorialDirector: the tutorial is over. {nextScene} isn't in the build's scene list yet, so it ends here.");
    }

    // For testing: wins the current fight, opens its door, and puts the player in the next zone they haven't reached.
    public void SkipAhead()
    {
        switch (CurrentBeat)
        {
            case "Melee": Win(meleeRobots); Open(meleeExit); break;
            case "Ambush":
                foreach (WallBreach breach in ambushBreaches) BlowIn(breach);
                Win(ambushRobots);
                if (arenaPassage != null && passageBreaches.Count > 0)
                {
                    foreach (WallBreach breach in passageBreaches) BlowIn(breach);
                    Win(passageRobot);
                    arenaPassage.gameObject.SetActive(true);
                }
                else Open(arenaEntrance);
                break;
            case "Arena": Win(arenaRobots); Breach(); Win(breachRobots); Open(arenaExit); break;
            case "Final Hallway": if (controlRoomDoor != null) controlRoomDoor.Open(); break;
        }

        foreach (PlayerTriggerZone zone in ZonesInOrder())
        {
            if (zone == null || zone.HasFired) continue;
            Vector2 point = zone.transform.position;
            player.position = new Vector3(point.x, point.y, player.position.z);
            if (playerBody != null)
            {
                playerBody.position = point;
                playerBody.linearVelocity = Vector2.zero;
            }
            return;
        }
    }

    void Win(RobotEncounter encounter)
    {
        if (encounter == null) return;
        foreach (Health robot in encounter.robots)
        {
            if (robot != null && !robot.IsDead)
                robot.TakeDamage(new DamageInfo(robot.maxHealth * 10f, Vector2.up, 0f, gameObject));
        }
    }
}
