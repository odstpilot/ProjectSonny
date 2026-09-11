using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the tutorial: a flash-forward to the end of the game, deep in the station and already hunted, on the way to the
// control room as the solar flare hits. The station rumbles and sheds its ceiling the whole way, and each room teaches
// one thing:
//   wake up and move -> the ceiling comes down behind you: run -> a robot powers up: the wrench -> robots across a
//   collapsed floor: the rivet gun -> a patrol comes through: hide in a locker -> an arena: everything at once ->
//   the last hallway, alarms going -> the control room, where Sonny is waiting, and the game cuts back to the start.
// Sonny > Build Tutorial Level builds the rooms and fills in the references below. Any of them can be left empty and
// that beat is skipped. In the Editor, F6 jumps to the next beat.
public class TutorialDirector : MonoBehaviour
{
    [Header("Zones (walking in starts each beat)")]
    public PlayerTriggerZone collapseZone;
    public PlayerTriggerZone runZone;
    public PlayerTriggerZone meleeZone;
    public PlayerTriggerZone galleryZone;
    public PlayerTriggerZone hideZone;
    public PlayerTriggerZone arenaZone;
    public PlayerTriggerZone finalZone;
    public PlayerTriggerZone revealZone;

    [Header("Doors")]
    public BlastDoor meleeExit;
    public BlastDoor galleryExit;
    public BlastDoor arenaEntrance;
    public BlastDoor arenaExit;
    [Tooltip("Stays shut until the last hallway, then opens as the player comes near.")]
    public BlastDoor controlRoomDoor;

    [Header("Robots")]
    public RobotEncounter meleeRobots;
    public RobotEncounter galleryRobots;
    public RobotEncounter patrolRobots;
    public RobotEncounter arenaRobots;

    [Header("Set Pieces")]
    [Tooltip("Where the ceiling comes down behind the player at the start, sealing the way back.")]
    public Transform[] collapsePoints = new Transform[0];
    public SonnyBox sonny;
    public StationRumble rumble;
    [Tooltip("Alarm lamps that come on in the last hallway.")]
    public List<StationLight> alarms = new List<StationLight>();
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

    [Header("Testing")]
    [Tooltip("Skip the opening title card, to get straight into the level.")]
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
        hud = TutorialHud.Get();

        foreach (StationLight alarm in alarms)
            if (alarm != null) alarm.SetOn(false);
        if (controlRoomDoor != null) controlRoomDoor.locked = true;

        StartCoroutine(Run());
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6)) SkipAhead();
    }
#endif

    IEnumerator Run()
    {
        yield return Opening();
        yield return Move();
        yield return Collapse();
        yield return Melee();
        yield return Gallery();
        yield return Hide();
        yield return Arena();
        yield return FinalHallway();
        yield return Reveal();
    }

    // --- The beats ---

    IEnumerator Opening()
    {
        CurrentBeat = "Opening";
        if (skipOpening || openingCard.Length == 0) yield break;

        SetControls(false);
        hud.SetFade(1f);
        if (rumble != null) rumble.Rumble(0.5f, 3f, 0);
        yield return hud.TitleCard(openingCard, 1.6f);
        yield return hud.FadeTo(0f, 1.5f);
        SetControls(true);
    }

    IEnumerator Move()
    {
        CurrentBeat = "Move";
        hud.SetObjective(objective);
        hud.ShowPrompt("Move", "W", "A", "S", "D");

        Vector2 last = player.position;
        float walked = 0f;
        while (walked < 2.5f && !Fired(collapseZone))
        {
            yield return null;
            walked += Vector2.Distance(player.position, last);
            last = player.position;
        }
        hud.CompletePrompt();
    }

    IEnumerator Collapse()
    {
        yield return WaitForZone(collapseZone);
        CurrentBeat = "Collapse";
        Checkpoint(collapseZone);

        // A big one: the ceiling comes down behind the player and there's no going back.
        if (rumble != null) rumble.Rumble(0.85f, 2.2f, 0);
        foreach (Transform point in collapsePoints)
        {
            if (point == null) continue;
            FallingDebris.Drop(point.position, 1f, 0.7f, 1.2f, solid: true);
            yield return new WaitForSeconds(0.12f);
        }

        yield return WaitForZone(runZone);
        CurrentBeat = "Run";
        yield return WaitForPrompt();
        hud.ShowPrompt("Hold to run", "SHIFT");
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
            hud.ShowPrompt("Swing   Hold to charge a heavy swing", "LEFT CLICK");
            yield return WaitForCleared(meleeRobots);
            hud.CompletePrompt();
        }
        Open(meleeExit);
    }

    IEnumerator Gallery()
    {
        yield return WaitForZone(galleryZone);
        CurrentBeat = "Gallery";
        Checkpoint(galleryZone);
        if (galleryRobots != null) galleryRobots.Activate();
        yield return WaitForPrompt();

        if (combat != null && !(combat.CurrentWeapon is RangedWeaponData))
        {
            hud.ShowPrompt("Rivet gun", SlotKey<RangedWeaponData>());
            yield return new WaitUntil(() => combat.CurrentWeapon is RangedWeaponData || Cleared(galleryRobots));
            hud.CompletePrompt();
            yield return WaitForPrompt();
        }

        if (!Cleared(galleryRobots))
        {
            hud.ShowPrompt("Shoot toward the cursor", "LEFT CLICK");
            yield return WaitForCleared(galleryRobots);
            hud.CompletePrompt();
        }
        Open(galleryExit);
    }

    IEnumerator Hide()
    {
        yield return WaitForZone(hideZone);
        CurrentBeat = "Hide";
        Checkpoint(hideZone);
        if (patrolRobots != null) patrolRobots.Activate();
        if (rumble != null) rumble.PlaySound(alarmClip, 0.4f);
        yield return WaitForPrompt();

        hud.ShowPrompt("A patrol is coming   Hide in a locker", "E");
        yield return new WaitUntil(() => Locker.IsPlayerHidden || Fired(arenaZone) || (patrolRobots != null && patrolRobots.IsCleared));
        if (Locker.IsPlayerHidden) hud.CompletePrompt();
        else hud.HidePrompt();
    }

    IEnumerator Arena()
    {
        yield return WaitForZone(arenaZone);
        CurrentBeat = "Arena";
        Checkpoint(arenaZone);
        Close(arenaEntrance);
        if (rumble != null)
        {
            rumble.intensity = 1.4f;
            rumble.Rumble(0.6f, 1.8f, 2);
        }
        if (arenaRobots != null) arenaRobots.Activate();
        yield return WaitForPrompt();

        hud.ShowPrompt("Switch weapons", SlotKey<MeleeWeaponData>(), SlotKey<RangedWeaponData>(), "SCROLL");
        WeaponData startingWeapon = combat != null ? combat.CurrentWeapon : null;
        float shownAt = Time.time;
        while (arenaRobots != null && !arenaRobots.IsCleared)
        {
            if (hud.PromptShowing && combat != null && combat.CurrentWeapon != startingWeapon) hud.CompletePrompt();
            else if (hud.PromptShowing && Time.time - shownAt > 8f) hud.HidePrompt();
            yield return null;
        }
        if (hud.PromptShowing) hud.HidePrompt();
        Open(arenaExit);
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
            rumble.Rumble(0.7f, 2.5f, 3);
        }
        if (controlRoomDoor != null) controlRoomDoor.locked = false;
        hallwayVoice = StartCoroutine(SayAll(sonnyInTheHallway, 2f));
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
        yield return new WaitForSeconds(1.2f);
        yield return SayAll(sonnyInTheControlRoom, 1.6f);

        if (rumble != null) rumble.Rumble(1f, 1.5f, 0);
        yield return new WaitForSeconds(0.5f);

        // Hard cut to black and silence.
        CurrentBeat = "Ending";
        hud.SetFade(1f);
        AudioListener.volume = 0f;
        yield return new WaitForSecondsRealtime(1f);
        yield return hud.TitleCard(closingCard, 1.5f);
        AudioListener.volume = 1f;
        LoadNextScene();
    }

    // --- Helpers ---

    // Debris keeps crashing down just behind the player for a while, so running feels like the right idea.
    IEnumerator CeilingChase(PlayerTriggerZone until, float seconds)
    {
        if (rumble != null) rumble.Rumble(0.35f, seconds, 0);

        float end = Time.time + seconds;
        Vector2 last = player.position;
        while (Time.time < end && !Fired(until))
        {
            yield return new WaitForSeconds(0.45f);

            Vector2 now = player.position;
            float heading = Mathf.Abs(now.x - last.x) > 0.05f ? Mathf.Sign(now.x - last.x) : 1f;
            last = now;
            Vector2 point = now + new Vector2(-heading * Random.Range(1.6f, 2.6f), Random.Range(-1.2f, 0.4f));
            if (rumble == null || rumble.IsClearFloor(point))
                FallingDebris.Drop(point, 1f, 0.6f);
        }
    }

    IEnumerator SayAll(string[] lines, float holdSeconds)
    {
        foreach (string line in lines)
            yield return hud.Say(sonnyName, line, holdSeconds);
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
            case "Gallery": Win(galleryRobots); Open(galleryExit); break;
            case "Arena": Win(arenaRobots); Open(arenaExit); break;
            case "Final Hallway": if (controlRoomDoor != null) controlRoomDoor.Open(); break;
        }

        PlayerTriggerZone[] order = { collapseZone, runZone, meleeZone, galleryZone, hideZone, arenaZone, finalZone, revealZone };
        foreach (PlayerTriggerZone zone in order)
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
