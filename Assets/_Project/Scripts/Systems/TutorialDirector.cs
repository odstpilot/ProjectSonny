using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Runs the tutorial: a flash-forward to the end of the game, deep in the station and already hunted, on the way to the
// control room as the solar flare hits. The station rumbles and sheds its ceiling the whole way, and each room teaches
// one thing:
//   bursting out of the reactor at a dead run with robots on your heels, until the ceiling comes down on them -> move
//   -> run -> a robot powers up: the wrench -> across a collapsed floor with the ceiling coming down all around -> a
//   patrol that can't be fought (the camera shows it, then the lockers): hide while it passes, then crouch to sneak by
//   once it stands guard -> an arena: the scrap gun, then a third robot blows in through the wall as the player comes
//   near it: switching between the two -> the last hallway, alarms going, Sonny on every screen -> the control room, where Sonny is waiting, and the
//   game cuts back to the start.
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
    public PlayerTriggerZone hideZone;
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
    public RobotEncounter patrolRobots;
    public RobotEncounter arenaRobots;
    [Tooltip("Switched off behind the arena wall until partway through the fight, when it blows the wall in.")]
    public RobotEncounter breachRobots;
    [Tooltip("Where the arena wall blows in.")]
    public Transform breachPoint;
    [Tooltip("How close the player has to come to the wall for it to blow in on them.")]
    public float breachDistance = 5f;
    [Tooltip("Seconds into the arena fight before the wall comes in anyway, if the player never goes near it. 0 waits for them.")]
    public float breachAfter = 25f;
    [Tooltip("The robots chasing the player out of the reactor at the start, until the ceiling comes down on them.")]
    public List<PlaceholderRobot> chasers = new List<PlaceholderRobot>();
    [Tooltip("How far the patrol sees once it has walked the room and stands guard over it. Crouching cuts it right down.")]
    public float guardSight = 16f;
    [Tooltip("Seconds the patrol holds after powering up before it sets off, unless the player hides sooner: time to get to a locker.")]
    public float patrolHeadStart = 3f;

    [Header("Set Pieces")]
    [Tooltip("Where the ceiling comes down behind the player at the start, sealing the way back.")]
    public Transform[] collapsePoints = new Transform[0];
    public SonnyBox sonny;
    public StationRumble rumble;
    [Tooltip("Alarm lamps that come on in the last hallway.")]
    public List<StationLight> alarms = new List<StationLight>();
    [Tooltip("Wall lamps. In the last hallway the ones between the player and the control room give out one by one.")]
    public List<StationLight> lamps = new List<StationLight>();
    public AudioClip alarmClip;
    public AudioClip heartbeatClip;

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
    [Tooltip("Loaded when the tutorial ends: Chapter 1 starts at the ship's entrance. Until that scene is in the build, the tutorial ends on black.")]
    public string nextScene = "ShipEntrance";

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
    [Range(0f, 1f)] public float ringingVolume = 0.12f;

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

    private Transform player;
    private Rigidbody2D playerBody;
    private PlayerController controller;
    private PlayerCombat combat;
    private WeaponHotbar hotbar;
    private PlayerHealthHandler respawn;
    private TutorialHud hud;
    private Coroutine hallwayVoice;
    private readonly List<Behaviour> lockedControls = new List<Behaviour>();
    private bool fighting;          // a fight or the patrol is on, so the guide arrow keeps out of the way
    private bool breached;
    private bool spotted;
    private WeaponData stowedWeapon;
    private readonly Dictionary<PlaceholderRobot, float> patrolSight = new Dictionary<PlaceholderRobot, float>();
    private int shotsFired;
    private float flareStartX;
    private float flareEndX;
    private float normalView;       // the camera's usual size, which the level widens and narrows from
    private Coroutine framing;

    void Start()
    {
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
        yield return Hide();
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

        while (!Fired(collapseZone))
        {
            DriveChasers(bodies, speed, stopAt);
            yield return null;
        }
        Checkpoint(collapseZone);

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

        yield return Dazed(dazedSeconds);
        if (controller != null)
        {
            controller.ClearFacingOverride();
            controller.ClearScriptedInput();
        }
        StartCoroutine(hud.Letterbox(false, 0.6f));
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
        AudioSource ringing = TutorialSetPieces.Speaker(gameObject, TutorialSetPieces.Ringing(), true);
        ringing.Play();

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float left = 1f - Mathf.SmoothStep(0f, 1f, t / seconds);
            smear.weight = 0.8f * left;
            if (muffle != null) muffle.cutoffFrequency = Mathf.Lerp(22000f, 500f, left);
            ringing.volume = ringingVolume * left;
            yield return null;
        }

        Destroy(smear.sharedProfile);
        Destroy(smear.gameObject);
        if (muffle != null) Destroy(muffle);
        ringing.Stop();
        Destroy(ringing.clip);
        Destroy(ringing);
    }

    // For testing without the opening: the chasers are gone and the rubble is already down.
    void SkipTheChase()
    {
        foreach (PlaceholderRobot chaser in chasers)
            if (chaser != null) Destroy(chaser.gameObject);
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
        yield return CeilingChase(hideZone, 6f);
    }

    // The patrol can't be fought, only avoided. Hide in a locker while it walks the room; once it has taken up its post
    // watching the room, crouch to get past it, since crouched the player can only be seen from much closer. Seen at any
    // point, and the section starts over from the doorway.
    IEnumerator Hide()
    {
        yield return WaitForZone(hideZone);
        CurrentBeat = "Hide";
        Checkpoint(hideZone);
        fighting = true;
        SetWeapons(false);
        foreach (PlaceholderRobot guard in Guards())
        {
            patrolSight[guard] = guard.sightRange;
            if (guard.TryGetComponent(out Health health)) health.cannotDie = true;
        }
        yield return WaitForPrompt();
        yield return ShowThePatrol();

        while (true)
        {
            yield return SneakPast();
            if (!spotted) break;
            yield return StartOver();
        }
        fighting = false;
        SetWeapons(true);
    }

    // One attempt at getting past the patrol. Leaves spotted set if they were seen.
    IEnumerator SneakPast()
    {
        spotted = false;
        hud.ShowPromptWithHint("Hide in a locker", "You can't fight these. Stay out of sight", "E");
        float setsOff = Time.time + patrolHeadStart;
        SetPatrolWalking(false);
        while (!Locker.IsPlayerHidden && !Fired(arenaZone))
        {
            if (Time.time >= setsOff) SetPatrolWalking(true);
            spotted = Spotted();
            if (spotted) yield break;
            yield return null;
        }
        SetPatrolWalking(true);
        if (hud.PromptShowing) hud.CompletePrompt();

        // Wait them out: they walk on past the lockers to the far end, then turn and watch the room.
        while (!PatrolAtPosts() && !Fired(arenaZone))
        {
            spotted = Spotted();
            if (spotted) yield break;
            yield return null;
        }
        foreach (PlaceholderRobot guard in Guards()) guard.sightRange = guardSight;

        yield return WaitForPrompt();
        if (!Fired(arenaZone)) hud.ShowPromptWithHint("Sneak past them", "Hold to crouch: they can't see you as far", "C");
        float sneaked = 0f;
        while (!Fired(arenaZone))
        {
            spotted = Spotted();
            if (spotted) yield break;
            bool creeping = controller != null && controller.IsCrouching && playerBody != null && playerBody.linearVelocity.sqrMagnitude > 0.1f;
            if (creeping) sneaked += Time.deltaTime;
            if (sneaked > 1.2f && hud.PromptShowing) hud.CompletePrompt();
            yield return null;
        }
        if (hud.PromptShowing) hud.HidePrompt();
    }

    // Seen: a moment of it, then black, and back at the doorway with the patrol where it began.
    IEnumerator StartOver()
    {
        hud.HidePrompt();
        SetControls(false);
        if (rumble != null) rumble.PlaySound(alarmClip, 0.5f);
        yield return new WaitForSeconds(0.6f);
        yield return hud.FadeTo(1f, 0.35f);

        if (Locker.Occupied != null) Locker.Occupied.PullPlayerOut();
        Vector2 doorway = hideZone.transform.position;
        player.position = new Vector3(doorway.x, doorway.y, player.position.z);
        if (playerBody != null)
        {
            playerBody.position = doorway;
            playerBody.linearVelocity = Vector2.zero;
        }
        foreach (PlaceholderRobot guard in Guards())
        {
            guard.ResetToStart();
            if (patrolSight.TryGetValue(guard, out float sight)) guard.sightRange = sight;
        }
        FollowPlayer();
        if (Camera.main != null && Camera.main.TryGetComponent(out CameraFallow follow)) follow.SnapToPlayer();

        yield return new WaitForSeconds(0.3f);
        yield return hud.FadeTo(0f, 0.35f);
        SetControls(true);
    }

    IEnumerable<PlaceholderRobot> Guards()
    {
        if (patrolRobots == null) yield break;
        foreach (Health robot in patrolRobots.robots)
            if (robot != null && robot.TryGetComponent(out PlaceholderRobot guard)) yield return guard;
    }

    bool Spotted() => Guards().Any(guard => guard.IsHunting);

    // Holds the patrol where it stands, or lets it carry on.
    void SetPatrolWalking(bool walking)
    {
        foreach (PlaceholderRobot guard in Guards())
        {
            if (guard.enabled == walking) continue;
            guard.enabled = walking;
            if (!walking && guard.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;
        }
    }

    bool PatrolAtPosts() => Guards().All(guard => Vector2.Distance(guard.transform.position, guard.PatrolEnd) < 0.4f);

    // Before the prompt, the camera leaves the player to show what's coming (the patrol powering up), then where to go
    // (the nearest locker), then comes back.
    IEnumerator ShowThePatrol()
    {
        Health lead = patrolRobots != null ? patrolRobots.robots.FirstOrDefault(robot => robot != null && !robot.IsDead) : null;
        if (lead == null)
        {
            if (patrolRobots != null) patrolRobots.Activate();
            yield break;
        }

        SetControls(false);
        yield return PanCameraTo(lead.transform.position, 0.9f);
        patrolRobots.Activate();
        if (rumble != null) rumble.PlaySound(alarmClip, 0.4f);
        yield return new WaitForSeconds(1.1f);

        Locker nearest = FindObjectsByType<Locker>(FindObjectsSortMode.None)
            .OrderBy(locker => Vector2.Distance(locker.transform.position, player.position))
            .FirstOrDefault();
        if (nearest != null)
        {
            yield return PanCameraTo(nearest.transform.position, 0.7f);
            yield return new WaitForSeconds(0.5f);
        }

        yield return PanCameraTo(player.position, 0.6f);
        FollowPlayer();
        SetControls(true);
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

    // The wall comes in when the player wanders close to it, right in their face. If they clear the room from a distance
    // instead, it comes in as the last of the first robots goes down, so they're never left waiting.
    IEnumerator BreachWhenDue(float startedAt)
    {
        Vector2 wall = breachPoint != null ? (Vector2)breachPoint.position
            : breachRobots != null ? (Vector2)breachRobots.transform.position : (Vector2)player.position;
        yield return new WaitUntil(() => breached
            || Vector2.Distance(player.position, wall) <= breachDistance
            || (arenaRobots != null && arenaRobots.IsCleared)     // nothing left in the room to keep them busy
            || (breachAfter > 0f && Time.time - startedAt > breachAfter));
        Breach();
    }

    void Breach()
    {
        if (breached) return;
        breached = true;
        if (breachRobots == null) return;

        Vector2 at = breachPoint != null ? (Vector2)breachPoint.position : (Vector2)breachRobots.transform.position;
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
            rumble.PlaySound(alarmClip, 0.8f);
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
            rumble.PlaySound(heartbeatClip, 1f);
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
        AudioSource tone = TutorialSetPieces.Speaker(gameObject, TutorialSetPieces.LowTone(), false);
        tone.volume = 0.9f;
        tone.Play();
        yield return new WaitForSecondsRealtime(holdOnBlack);
        yield return hud.TitleCard(closingCard, 1.5f);
        AudioListener.volume = 1f;
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
            bool free = controller != null && controller.enabled && !fighting && !Locker.IsPlayerHidden && !hud.PromptShowing;
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

    PlayerTriggerZone[] ZonesInOrder() => new[] { collapseZone, runZone, meleeZone, crossingZone, hideZone, arenaZone, finalZone, revealZone };

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
