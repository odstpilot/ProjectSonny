using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// runs the tutorial
public class TutorialDirector : MonoBehaviour
{
    [Header("Zones (walking in starts each beat)")]
    public PlayerTriggerZone collapseZone;
    public PlayerTriggerZone runZone;
    [Tooltip("The room with the fight, where the walls blow in.")]
    public PlayerTriggerZone fightZone;
    public PlayerTriggerZone finalZone;
    public PlayerTriggerZone revealZone;

    [Header("Doors")]
    [Tooltip("The way on out of the fight room: shut until both waves are down.")]
    public BlastDoor fightExit;
    [Tooltip("Stays shut until the last hallway, then opens as the player comes near.")]
    public BlastDoor controlRoomDoor;

    [Header("Robots")]
    [Tooltip("The robots chasing the player out of the reactor at the start, until the ceiling comes down on them.")]
    public List<PlaceholderRobot> chasers = new List<PlaceholderRobot>();
    [Tooltip("More for the opening, switched off behind the corridor wall: each blasts out as the player runs past and joins the chase.")]
    public List<WallBreach> openingBreaches = new List<WallBreach>();
    [Tooltip("The first two through the fight room's walls, for the wrench.")]
    public RobotEncounter firstWave;
    public List<WallBreach> firstBreaches = new List<WallBreach>();
    [Tooltip("The two after them, for the blaster.")]
    public RobotEncounter secondWave;
    public List<WallBreach> secondBreaches = new List<WallBreach>();
    [Tooltip("Seconds between one wall coming in and the next.")]
    public float breachInterval = 1.2f;
    [Tooltip("Seconds between the first wave going down and the second coming in.")]
    public float secondWaveAfter = 1.2f;

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
    public string[] closingCard = { "Earlier" };

    [Header("Afterwards")]
    [Tooltip("Loaded when the tutorial ends: Chapter 1 starts at the ship's entrance (ChapterOneBuilder). Until that scene is in the build, the tutorial ends on black.")]
    public string nextScene = "Chapter1";

    [Header("Player")]
    [Tooltip("Hits land but take no health, so the player can't die in the tutorial. The health readout is hidden too.")]
    public bool playerCannotDie = true;
    [Tooltip("Only the wrench on the hotbar to start; the blaster's handed over for the second wave, and the EMP's taken off.")]
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
    [Tooltip("How far the camera pulls back for the fight, as a multiple of its normal view, so the walls coming in and every robot are on screen.")]
    public float fightView = 1.1f;
    [Tooltip("How far it pushes in down the last hallway, closing in on the player as they near the control room.")]
    public float finalHallwayView = 0.95f;
    [Tooltip("How far it pulls back on Sonny at the end, so all of it is in shot, and where it looks from, from Sonny's feet.")]
    public float revealView = 1.9f;
    public Vector2 revealFrom = new Vector2(0f, 4f);

    [Header("The End")]
    [Tooltip("Seconds to hold on Sonny once it's woken, before the cut to black.")]
    public float holdOnSonny = 3f;
    [Tooltip("Seconds of black after the cut, while a low tone rings out, before the closing card.")]
    public float holdOnBlack = 2.5f;

    [Header("Testing")]
    [Tooltip("Skip the opening title card and the chase out of the reactor, to get straight into the level.")]
    public bool skipOpening;

    public string CurrentBeat { get; private set; } = "Starting";

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
    private readonly List<Behaviour> lockedControls = new List<Behaviour>();
    private bool fighting;
    private WeaponData stowedWeapon;
    private WeaponData blaster;
    private int shotsFired;
    private float flareStartX;
    private float flareEndX;
    private float normalView;
    private Vector2? chaseBreach;
    private bool chaseCollapsed;
    private Coroutine framing;

    void Start()
    {
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
        yield return Fight();
        yield return FinalHallway();
        yield return Reveal();
    }

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

    IEnumerator TheChase()
    {
        float speed = controller != null ? controller.moveSpeed * controller.sprintMultiplier : 5f;
        var bodies = new List<Rigidbody2D>();
        foreach (PlaceholderRobot chaser in chasers)
        {
            if (chaser == null) continue;
            chaser.enabled = false;
            bodies.Add(chaser.GetComponent<Rigidbody2D>());
        }
        float stopAt = collapsePoints.Length > 0 && collapsePoints[0] != null ? collapsePoints[0].position.x : float.MaxValue;

        if (controller != null) controller.SetScriptedInput(Vector2.right, true);
        StartCoroutine(hud.Letterbox(true, 0.3f));
        StartCoroutine(hud.FadeTo(0f, 0.8f));
        chaseCollapsed = false;
        Coroutine filming = cinematicChase ? StartCoroutine(ChaseCamera(bodies, stopAt)) : null;
        StartCoroutine(LightsOutBehind(float.NegativeInfinity, stopAt, () => chaseCollapsed));

        while (!Fired(collapseZone))
        {
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

        for (float t = 0f; t < 0.9f && !chaseCollapsed; t += Time.unscaledDeltaTime)
        {
            Frame(Pack() + new Vector2(0.6f + t * 0.5f, 0.3f), 0.82f - t * 0.02f, 1.5f - t * 0.5f);
            yield return null;
        }
        CameraShake.Shake(0.12f);

        yield return Move(() => (Vector2)player.position + lead, 0.94f, -0.5f, 0.9f, TitleUI.EaseInOut);

        float sway = 0f;
        while (!chaseCollapsed)
        {
            float dt = Time.unscaledDeltaTime;
            sway += dt;
            Vector2 want = (Vector2)player.position + lead;
            float size = 0.94f + Mathf.Sin(sway * 0.9f) * 0.005f;
            float tilt = -0.5f + Mathf.Sin(sway * 1.2f) * 0.15f;
            if (chaseBreach is Vector2 breach)
            {
                chaseBreach = null;
                HitStop.Hold(0.6f, 0.2f);
                CameraShake.Kick(Vector2.down, 0.15f);
                Vector2 toward = Vector2.Lerp(want, breach, 0.2f);
                yield return Move(() => toward, 0.88f, 0.5f, 0.45f, TitleUI.EaseOut);
                yield return Move(() => (Vector2)player.position + lead, 0.94f, -0.5f, 0.8f, TitleUI.EaseInOut);
                continue;
            }
            Vector2 at = Vector2.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-5f * dt));
            Frame(at, Mathf.Lerp(cam.orthographicSize / normalView, size, 1f - Mathf.Exp(-3f * dt)), tilt);
            yield return null;
        }

        HitStop.Hold(0.5f, 0.8f);
        Vector2 onTheRubble = new Vector2(collapseX - 1f, player.position.y + 0.3f);
        yield return Move(() => onTheRubble, 0.88f, 0.8f, 0.9f, TitleUI.EaseInOut);
        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
        {
            Frame(onTheRubble + new Vector2(-t * 0.15f, 0f), 0.88f - t * 0.01f, 0.8f - t * 0.5f);
            yield return null;
        }

        yield return Move(() => follow != null ? (Vector2)follow.Target : (Vector2)player.position, 1f, 0f, 1.1f, TitleUI.EaseInOut);
        cam.transform.rotation = Quaternion.identity;
        cam.orthographicSize = normalView;
        FollowPlayer();
    }

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

    static void DriveChasers(List<Rigidbody2D> bodies, float speed, float stopAt)
    {
        foreach (Rigidbody2D body in bodies)
        {
            if (body == null) continue;
            float left = stopAt - body.position.x;
            body.linearVelocity = left <= 0f ? Vector2.zero : Vector2.right * Mathf.Min(speed, left / Mathf.Max(Time.deltaTime, 0.001f));
        }
    }

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
        StartCoroutine(CeilingChase(fightZone, 7f));
        float hallwayEnd = fightZone != null ? fightZone.transform.position.x - 3f : float.MaxValue;
        StartCoroutine(LightsOutBehind(player.position.x, hallwayEnd, () => Fired(fightZone)));

        float sprinted = 0f;
        while (sprinted < 0.8f && !Fired(fightZone))
        {
            bool moving = Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f;
            if (moving && Input.GetKey(KeyCode.LeftShift) && controller != null && controller.enabled && controller.stamina > 0f)
                sprinted += Time.deltaTime;
            yield return null;
        }
        hud.CompletePrompt();
    }

    IEnumerator Fight()
    {
        yield return WaitForZone(fightZone);
        CurrentBeat = "Fight";
        Checkpoint(fightZone);
        Close(fightExit);
        fighting = true;
        FrameView(fightView, 1.2f);
        if (rumble != null) rumble.Rumble(0.5f, 2f, 1);
        yield return new WaitForSeconds(0.8f);

        yield return BlowInAll(firstBreaches, firstWave);
        yield return WaitForPrompt();
        if (combat != null && !(combat.CurrentWeapon is MeleeWeaponData) && !Cleared(firstWave))
        {
            hud.ShowPrompt("Wrench", SlotKey<MeleeWeaponData>());
            yield return new WaitUntil(() => combat.CurrentWeapon is MeleeWeaponData || Cleared(firstWave));
            hud.CompletePrompt();
            yield return WaitForPrompt();
        }
        if (!Cleared(firstWave))
        {
            hud.ShowPromptWithHint("Swing", "Two hits takes one down", "LEFT CLICK");
            yield return WaitForCleared(firstWave);
            hud.CompletePrompt();
        }

        yield return new WaitForSeconds(secondWaveAfter);
        if (rumble != null) rumble.Rumble(0.5f, 1.5f, 1);
        GiveBlaster();
        StartCoroutine(BlowInAll(secondBreaches, secondWave));
        yield return WaitForPrompt();

        RangedCombat gun = player.GetComponent<RangedCombat>();
        if (gun != null) gun.Fired += CountShot;
        if (blaster != null && combat != null && combat.CurrentWeapon != blaster && !Cleared(secondWave))
        {
            hud.ShowPromptWithHint("Switch to the blaster", "Scroll the mouse wheel", "SCROLL");
            yield return new WaitUntil(() => combat.CurrentWeapon == blaster || Cleared(secondWave));
            hud.CompletePrompt();
            yield return WaitForPrompt();
        }
        if (blaster != null && !Cleared(secondWave))
        {
            int before = shotsFired;
            hud.ShowPromptWithHint("Shoot", "Hold to charge, let go to fire", "LEFT CLICK");
            yield return new WaitUntil(() => shotsFired > before || Cleared(secondWave));
            hud.CompletePrompt();
        }
        yield return WaitForCleared(secondWave);
        if (gun != null) gun.Fired -= CountShot;
        fighting = false;
        FrameView(1f, 1.5f);
        Open(fightExit);
    }

    void CountShot() => shotsFired++;

    IEnumerator BlowInAll(List<WallBreach> breaches, RobotEncounter encounter)
    {
        List<WallBreach> order = breaches.Where(b => b.point != null)
            .OrderBy(b => Vector2.Distance(b.point.position, player.position)).ToList();
        for (int i = 0; i < order.Count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(breachInterval);
            BlowIn(order[i]);
            if (i == 0 && encounter != null) encounter.Activate();
        }
    }

    PlaceholderRobot BlowIn(WallBreach breach)
    {
        if (breach.done) return null;
        breach.done = true;
        if (breach.point != null) TutorialSetPieces.BlowInWall(breach.point.position);
        if (breach.robot == null) return null;
        breach.robot.gameObject.SetActive(true);
        return breach.robot.GetComponent<PlaceholderRobot>();
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

        StartCoroutine(hud.Letterbox(true, 0.8f));
        StartCoroutine(hud.Vignette(0.85f, 2.5f));
        FrameView(revealView, 2.5f);
        yield return PanCameraTo(sonny != null ? (Vector2)sonny.transform.position + revealFrom : (Vector2)player.position, 2.5f);

        if (sonny != null) sonny.Awaken();
        StartCoroutine(RaiseSun(0.6f, 2.5f));
        SonnyScreen.SetAll(true);
        yield return new WaitForSeconds(holdOnSonny);
        if (rumble != null) rumble.Rumble(1f, 1.5f, 0);
        yield return new WaitForSeconds(0.5f);

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

    Vector2? NextPlace()
    {
        foreach (PlayerTriggerZone zone in ZonesInOrder())
            if (zone != null && !zone.HasFired) return zone.transform.position;
        return null;
    }

    PlayerTriggerZone[] ZonesInOrder() => new[] { collapseZone, runZone, fightZone, finalZone, revealZone };

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

    void FollowPlayer()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraFallow follow)) follow.enabled = true;
    }

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
            blaster = hotbar.slots.FirstOrDefault(weapon => weapon is BlasterWeaponData);
            hotbar.slots.RemoveAll(weapon => !(weapon is MeleeWeaponData));
            if (combat != null && combat.CurrentWeapon != null && !hotbar.slots.Contains(combat.CurrentWeapon))
                combat.Equip(hotbar.slots.Count > 0 ? hotbar.slots[0] : null);
        }
        else if (hotbar != null) blaster = hotbar.slots.FirstOrDefault(weapon => weapon is BlasterWeaponData);
    }

    void GiveBlaster()
    {
        if (blaster != null && hotbar != null && !hotbar.slots.Contains(blaster)) hotbar.slots.Add(blaster);
    }

    void Checkpoint(PlayerTriggerZone zone)
    {
        if (respawn == null || zone == null) return;
        Vector3 point = zone.transform.position;
        respawn.RespawnPoint = new Vector3(point.x, point.y, player.position.z);
    }

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

    public void SkipAhead()
    {
        switch (CurrentBeat)
        {
            case "Fight":
                foreach (WallBreach breach in firstBreaches.Concat(secondBreaches)) BlowIn(breach);
                GiveBlaster();
                Win(firstWave);
                Win(secondWave);
                Open(fightExit);
                break;
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
