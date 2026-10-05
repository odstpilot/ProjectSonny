using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A security camera up on a room's wall, the station's own, there since before anything went wrong. It sweeps the room
// from side to side, easing into each end and resting there, sometimes stopping short partway as if something caught
// its eye, with the patch of floor it can see drawn as a cone of light, clipped where walls and furniture block it.
//
// In Chapter 1 it's harmless (hostile off): a green light blinking on it, a faint pale cone. Once Sonny's on
// (CrewMember.SonnyOnline) it takes to following the technician a little when they cross its view.
// In Chapter 2 it's Sonny's eyes (hostile on). Catching sight of the player, it stops and turns to them while its ring
// fills (StealthMeter), amber going red; crouching makes it slower to be sure, but only staying out of the cone, or in
// a locker, beats it. Sure, it sounds the alarm: the robots in the room (PlaceholderRobot.Alarm), and any Wardens near
// enough, come to where it saw them, and it keeps following and calling them for as long as the player's in view.
// Losing them, it holds on the spot a moment, then goes back to its sweep. An EMP (Shutdown) puts it out for a while.
// The art is made in code until it has its own. Its position is the eye: the foot of the wall below it, where its view
// of the floor starts; the housing hangs mountHeight above, on the wall. Built by ChapterOneBuilder, for both chapters.
public class StationCamera : MonoBehaviour
{
    const float PixelsPerUnit = 16f;
    const int ConeRays = 28;
    const float ActiveDistance = 24f;      // it only draws and looks when the player's this close

    [Tooltip("Sonny's (Chapter 2): it watches for the player and calls the robots. Off, it only sweeps.")]
    public bool hostile;
    [Tooltip("Can't see the player at all, for a level holding it off for a moment.")]
    public bool blind;
    [Tooltip("The room it's in, by its marker in the layout: only robots in the same room answer it.")]
    public char room;

    [Header("Sweep")]
    [Tooltip("The way it faces in the middle of its sweep, in degrees: -90 straight down, -45 down and right, -135 down and left.")]
    public float baseAngle = -90f;
    [Tooltip("How far it turns either side of that.")]
    public float sweep = 40f;
    [Tooltip("Degrees a second, at full speed in the middle of a sweep.")]
    public float sweepSpeed = 24f;
    [Tooltip("Seconds it rests at each end, from and to.")]
    public Vector2 endPause = new Vector2(0.8f, 1.8f);
    [Tooltip("Chance, each way across, that it stops partway for a moment first.")]
    [Range(0f, 1f)] public float stopShortChance = 0.3f;

    [Header("Sight")]
    public float range = 7f;
    [Tooltip("How wide its view is, in degrees.")]
    public float fieldOfView = 38f;
    [Tooltip("Seconds in view before it's sure, standing up. Crouching takes longer.")]
    public float detectionTime = 0.9f;
    [Range(0.1f, 1f)] public float crouchRate = 0.55f;
    [Tooltip("Degrees a second it turns to follow the player.")]
    public float trackSpeed = 140f;
    [Tooltip("How far past its sweep it can turn to follow them.")]
    public float trackReach = 25f;
    [Tooltip("Seconds it keeps looking where it lost them before sweeping again.")]
    public float lostHold = 1.6f;

    [Header("Alarm")]
    [Tooltip("Robots in the room, and Wardens, this close come when it's sure.")]
    public float alertRange = 18f;
    [Tooltip("Seconds between calls while it keeps the player in view.")]
    public float realertInterval = 1.5f;

    [Header("Look")]
    public float mountHeight = 1.7f;
    public Color calmColor = new Color(0.55f, 0.95f, 0.85f);
    public Color watchColor = new Color(1f, 0.72f, 0.25f);
    public Color alarmColor = new Color(1f, 0.16f, 0.12f);
    [Range(0f, 1f)] public float coneAlpha = 0.16f;
    [Range(0f, 2f)] public float lightIntensity = 0.55f;

    [Header("Sound")]
    [SoundName] public string servoSound = "Camera Servo";
    [SoundName] public string alertSound = "Camera Alert";
    [Tooltip("Its sounds fade out by this far from the player.")]
    public float hearingRange = 16f;

    enum State { Sweep, Watch, Alarm, Hold, Off }

    static readonly List<StationCamera> all = new List<StationCamera>();
    static Sprite[] housingFrames;
    static Sprite bracketSprite, lensSprite;
    static AudioClip servoFallback, alertFallback;

    private State state = State.Sweep;
    private float aim;                  // where it's facing, in degrees
    private float legTarget;            // where this pass of the sweep is heading
    private float pauseUntil;
    private bool stoppedShort;
    private float detection;
    private float nextAlert;
    private float stateUntil;
    private Vector2 lastSeen;
    private Transform player;
    private PlayerController playerController;
    private Health playerHealth;

    private SpriteRenderer housing, lens;
    private Transform housingPivot;
    private Mesh coneMesh;
    private MeshRenderer coneRenderer;
    private Light2D beam, lensGlow;
    private AudioSource voice;
    private readonly Vector3[] coneVertices = new Vector3[ConeRays + 1];
    private readonly Color[] coneColors = new Color[ConeRays + 1];
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private Color shownColor;

    public static IReadOnlyList<StationCamera> All => all;
    public bool IsAlarmed => state == State.Alarm;

    void Awake()
    {
        aim = baseAngle;
        legTarget = baseAngle + sweep;
        shownColor = hostile ? watchColor : calmColor;
        BuildArt();
    }

    void OnEnable() => all.Add(this);

    void OnDisable()
    {
        all.Remove(this);
        StealthMeter.Clear(this);
    }

    void Start()
    {
        GameObject found = GameObject.FindWithTag("Player");
        if (found == null) return;
        player = found.transform;
        playerController = found.GetComponent<PlayerController>();
        playerHealth = found.GetComponent<Health>();
    }

    void Update()
    {
        bool near = player != null && Vector2.Distance(player.position, transform.position) < ActiveDistance;
        if (state == State.Off)
        {
            if (Time.time >= stateUntil) state = State.Sweep;
            // Shorted out: spitting the odd spark.
            if (near && Random.value < Time.deltaTime * 3f)
                HitEffects.Sparks((Vector2)housingPivot.position + new Vector2(0f, -0.4f), Vector2.down, 3, 2f, 120f, Color.white, new Color(0.4f, 0.85f, 1f));
            Show(near, 0f);
            return;
        }

        float seen = near ? Sees() : 0f;
        if (hostile) Think(seen);
        else Wander(seen);

        Color want = hostile
            ? state == State.Alarm ? alarmColor : Color.Lerp(watchColor, alarmColor, detection)
            : calmColor;
        shownColor = Color.Lerp(shownColor, want, Time.deltaTime * 10f);
        Show(near, 1f);
    }

    // --- Chapter 2 ---

    void Think(float seen)
    {
        switch (state)
        {
            case State.Sweep:
                if (seen > 0f) Begin(State.Watch);
                else Sweep();
                break;

            case State.Watch:
                if (seen > 0f)
                {
                    TurnToward(Angle(lastSeen), trackSpeed * 0.5f);
                    detection = Mathf.Clamp01(detection + seen * Time.deltaTime / Mathf.Max(0.05f, detectionTime));
                    if (detection >= 1f) Begin(State.Alarm);
                }
                else
                {
                    detection = Mathf.MoveTowards(detection, 0f, Time.deltaTime / 1.5f);
                    if (detection <= 0f) Begin(State.Sweep);
                }
                break;

            case State.Alarm:
                if (seen > 0f)
                {
                    TurnToward(Angle(lastSeen), trackSpeed);
                    if (Time.time >= nextAlert) RaiseAlarm();
                }
                else Begin(State.Hold);
                break;

            case State.Hold:
                if (seen > 0f) Begin(State.Alarm);
                else if (Time.time >= stateUntil)
                {
                    detection = 0f;
                    Begin(State.Sweep);
                }
                break;
        }

        if (detection > 0f || state == State.Alarm || state == State.Hold)
            StealthMeter.Report(this, transform.position, state == State.Alarm || state == State.Hold ? 1f : detection, state == State.Alarm);
        else
            StealthMeter.Clear(this);
    }

    void Begin(State next)
    {
        if (next == State.Watch) Play(servoSound, ServoFallback, 0.8f, 1.3f);
        if (next == State.Alarm)
        {
            Play(alertSound, AlertFallback, 1f, 1f);
            nextAlert = 0f;
        }
        if (next == State.Hold) stateUntil = Time.time + lostHold;
        if (next == State.Sweep)
        {
            pauseUntil = Time.time + 0.4f;
            legTarget = aim >= baseAngle ? baseAngle - sweep : baseAngle + sweep;
        }
        state = next;
    }

    // The robots in the room come to where it saw them, and any Wardens near enough.
    void RaiseAlarm()
    {
        nextAlert = Time.time + realertInterval;
        foreach (PlaceholderRobot robot in FindObjectsByType<PlaceholderRobot>())
        {
            if (Vector2.Distance(robot.transform.position, transform.position) > alertRange) continue;
            if (room != '\0' && !InRoom(robot.transform.position)) continue;
            robot.Alarm(lastSeen);
        }
        foreach (Warden warden in FindObjectsByType<Warden>())
        {
            if (!warden.enabled || Vector2.Distance(warden.transform.position, transform.position) > alertRange) continue;
            warden.ActivitedByCam();
            warden.NewTarget(lastSeen);
        }
    }

    // --- Chapter 1 ---

    // Sweeping, and once Sonny's on, following the technician a while when they cross its view.
    void Wander(float seen)
    {
        if (seen > 0f && CrewMember.SonnyOnline)
        {
            if (state != State.Watch) Play(servoSound, ServoFallback, 0.5f, 1.1f);
            state = State.Watch;
            stateUntil = Time.time + lostHold;
            TurnToward(Angle(lastSeen), trackSpeed * 0.35f);
        }
        else if (state == State.Watch)
        {
            if (Time.time >= stateUntil) Begin(State.Sweep);
        }
        else Sweep();
    }

    // --- Sweeping ---

    // Easing into each end, resting there, and now and then stopping short on the way.
    void Sweep()
    {
        if (Time.time < pauseUntil) return;
        float left = Mathf.DeltaAngle(aim, legTarget);
        if (Mathf.Abs(left) < 0.5f)
        {
            aim = legTarget;
            bool atEnd = Mathf.Abs(Mathf.DeltaAngle(legTarget, baseAngle)) >= sweep - 1f;
            if (atEnd)
            {
                pauseUntil = Time.time + Random.Range(endPause.x, endPause.y);
                float otherEnd = legTarget > baseAngle ? baseAngle - sweep : baseAngle + sweep;
                stoppedShort = Random.value < stopShortChance;
                legTarget = stoppedShort ? Mathf.Lerp(aim, otherEnd, Random.Range(0.3f, 0.7f)) : otherEnd;
            }
            else
            {
                // Stopped short: a look, then on to the end it was going to.
                pauseUntil = Time.time + Random.Range(0.35f, 0.8f);
                legTarget = Mathf.DeltaAngle(baseAngle, aim) < 0f ? baseAngle - sweep : baseAngle + sweep;
                if (Random.value < 0.5f) aim += Random.Range(-4f, 4f);
            }
            return;
        }
        // Slower near where it's stopping, so it eases in rather than bumping.
        float ease = Mathf.Clamp01(Mathf.Abs(left) / 12f) * 0.8f + 0.2f;
        aim = Mathf.MoveTowardsAngle(aim, legTarget, sweepSpeed * ease * Time.deltaTime);
    }

    void TurnToward(float angle, float speed)
    {
        float lowest = baseAngle - sweep - trackReach, highest = baseAngle + sweep + trackReach;
        float clamped = baseAngle + Mathf.Clamp(Mathf.DeltaAngle(baseAngle, angle), lowest - baseAngle, highest - baseAngle);
        aim = Mathf.MoveTowardsAngle(aim, clamped, speed * Time.deltaTime);
    }

    // --- Seeing ---

    // How well it can make the player out: 1 standing in its view, less crouching, 0 not at all.
    float Sees()
    {
        if (blind || player == null || Locker.IsPlayerHidden || (playerHealth != null && playerHealth.IsDead)) return 0f;
        Vector2 eye = transform.position;
        Vector2 toPlayer = (Vector2)player.position - eye;
        if (toPlayer.magnitude > range || Vector2.Angle(Direction(aim), toPlayer) > fieldOfView * 0.5f) return 0f;
        if (Blocked(eye, player.position)) return 0f;
        lastSeen = player.position;
        return playerController != null && playerController.IsCrouching ? crouchRate : 1f;
    }

    // Something solid between it and the point: a wall, furniture, a locker. Not the player, and not the robots.
    bool Blocked(Vector2 from, Vector2 to)
    {
        var solidOnly = new ContactFilter2D { useTriggers = false };
        Physics2D.Linecast(from, to, solidOnly, hits);
        foreach (RaycastHit2D hit in hits)
            if (Blocks(hit.collider)) return true;
        return false;
    }

    bool Blocks(Collider2D collider)
    {
        if (player != null && collider.transform.IsChildOf(player)) return false;
        if (collider.GetComponentInParent<PlaceholderRobot>() != null || collider.GetComponentInParent<MaintenanceBot>() != null) return false;
        if (collider.GetComponentInParent<CrewMember>() != null) return false;
        return true;
    }

    // How far it can see along a direction before something's in the way.
    float Reach(Vector2 direction)
    {
        var solidOnly = new ContactFilter2D { useTriggers = false };
        Physics2D.Raycast(transform.position, direction, solidOnly, hits, range);
        float nearest = range;
        foreach (RaycastHit2D hit in hits)
            if (Blocks(hit.collider) && hit.distance < nearest) nearest = hit.distance;
        return nearest;
    }

    // --- Being put out ---

    // An EMP, or anything else that shorts it out: dark for this long, then back to its sweep, having forgotten.
    public void Shutdown(float seconds)
    {
        state = State.Off;
        stateUntil = Time.time + seconds;
        detection = 0f;
        StealthMeter.Clear(this);
    }

    // Back to sweeping, as if it had never seen the player. For the player coming back after dying.
    public void CalmDown()
    {
        if (state == State.Off) return;
        detection = 0f;
        StealthMeter.Clear(this);
        Begin(State.Sweep);
    }

    bool InRoom(Vector2 point)
    {
        StationMap map = StationMap.Current;
        return map == null || (map.Locate(point, out _, out StationMap.Room found) && found.marker == room);
    }

    float Angle(Vector2 point)
    {
        Vector2 to = point - (Vector2)transform.position;
        return Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
    }

    static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    // --- Showing it ---

    // The housing turned the way it's looking, the light on it, and the cone on the floor. power: 1 on, 0 out.
    void Show(bool near, float power)
    {
        // The housing: a frame for down, down-and-across, and across, mirrored for the left.
        float fromDown = Mathf.DeltaAngle(-90f, aim);
        int frame = Mathf.Abs(fromDown) < 22f ? 0 : Mathf.Abs(fromDown) < 62f ? 1 : 2;
        bool left = fromDown < 0f;
        housing.sprite = housingFrames[frame];
        housing.flipX = left;
        Vector2 lensAt = LensPixel[frame];
        lens.transform.localPosition = new Vector3((left ? -1f : 1f) * (lensAt.x - HousingHalfWidth) / PixelsPerUnit, -lensAt.y / PixelsPerUnit, 0f);

        // The light on it: blinking slow while calm, steady watching, flashing in alarm, out when it's out.
        float blink = state == State.Alarm ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 1f : 0.25f)
            : hostile && state == State.Watch ? 1f
            : Mathf.Repeat(Time.time, 1.4f) < 0.25f ? 1f : 0.35f;
        lens.color = power > 0f ? new Color(shownColor.r, shownColor.g, shownColor.b, blink) : new Color(0.2f, 0.2f, 0.2f, 1f);
        lensGlow.enabled = power > 0f;
        lensGlow.color = shownColor;
        lensGlow.intensity = 0.9f * blink;

        coneRenderer.enabled = near && power > 0f;
        beam.enabled = near && power > 0f;
        if (!near || power <= 0f) return;

        // The cone: fanned out across its view, each ray stopping at whatever's in the way.
        float alpha = hostile ? coneAlpha : coneAlpha * 0.45f;
        if (state == State.Alarm) alpha *= 1.6f;
        coneVertices[0] = Vector3.zero;
        coneColors[0] = new Color(shownColor.r, shownColor.g, shownColor.b, alpha);
        for (int i = 0; i < ConeRays; i++)
        {
            float angle = aim - fieldOfView * 0.5f + fieldOfView * i / (ConeRays - 1);
            Vector2 direction = Direction(angle);
            float reach = Reach(direction);
            coneVertices[i + 1] = direction * reach;
            coneColors[i + 1] = new Color(shownColor.r, shownColor.g, shownColor.b, alpha * 0.2f * (1f - reach / range * 0.5f));
        }
        coneMesh.vertices = coneVertices;
        coneMesh.colors = coneColors;
        coneMesh.RecalculateBounds();

        beam.transform.localRotation = Quaternion.Euler(0f, 0f, aim - 90f);
        beam.color = shownColor;
        beam.intensity = (hostile ? lightIntensity : lightIntensity * 0.4f) * (state == State.Alarm ? 1.4f : 1f);
    }

    void BuildArt()
    {
        // Made once, and again after a scene change has unloaded them (they're not assets, so nothing keeps them).
        if (housingFrames == null || housingFrames[0] == null || bracketSprite == null || lensSprite == null) MakeSprites();

        housingPivot = new GameObject("Housing").transform;
        housingPivot.SetParent(transform, false);
        housingPivot.localPosition = new Vector3(0f, mountHeight, 0f);

        var bracket = new GameObject("Bracket").AddComponent<SpriteRenderer>();
        bracket.transform.SetParent(housingPivot, false);
        bracket.sprite = bracketSprite;
        Wall(bracket, 3);

        housing = new GameObject("Body").AddComponent<SpriteRenderer>();
        housing.transform.SetParent(housingPivot, false);
        housing.sprite = housingFrames[0];
        Wall(housing, 4);

        lens = new GameObject("Lens").AddComponent<SpriteRenderer>();
        lens.transform.SetParent(housing.transform, false);
        lens.sprite = lensSprite;
        Wall(lens, 5);
        if (CombatSprites.EffectMaterial != null) lens.sharedMaterial = CombatSprites.EffectMaterial;

        // A little glow round the lens, so it reads in the dark.
        lensGlow = new GameObject("Lens Glow").AddComponent<Light2D>();
        lensGlow.transform.SetParent(lens.transform, false);
        lensGlow.lightType = Light2D.LightType.Point;
        lensGlow.pointLightInnerRadius = 0f;
        lensGlow.pointLightOuterRadius = 0.7f;
        lensGlow.falloffIntensity = 0.7f;

        // The cone, on the floor, glowing (unlit, so it shows under emergency lighting).
        var cone = new GameObject("View Cone");
        cone.transform.SetParent(transform, false);
        coneMesh = new Mesh { name = "Camera View Cone" };
        coneMesh.MarkDynamic();
        coneMesh.vertices = coneVertices;
        var triangles = new int[(ConeRays - 1) * 3];
        for (int i = 0; i < ConeRays - 1; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }
        coneMesh.triangles = triangles;
        cone.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        coneRenderer = cone.AddComponent<MeshRenderer>();
        coneRenderer.sharedMaterial = CombatSprites.EffectMaterial;
        coneRenderer.sortingLayerName = "FloorObject";
        coneRenderer.sortingOrder = 6;

        // A spot of real light along it, so it lights the floor and whoever's standing in it.
        beam = new GameObject("Beam").AddComponent<Light2D>();
        beam.transform.SetParent(transform, false);
        beam.lightType = Light2D.LightType.Point;
        beam.pointLightInnerAngle = fieldOfView * 0.6f;
        beam.pointLightOuterAngle = fieldOfView;
        beam.pointLightInnerRadius = 0f;
        beam.pointLightOuterRadius = range;
        beam.falloffIntensity = 0.6f;
        beam.shadowsEnabled = false;

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
    }

    static void Wall(SpriteRenderer sprite, int order)
    {
        sprite.sortingLayerName = "Collision";
        sprite.sortingOrder = order;
    }

    void Play(string sound, AudioClip fallback, float volume, float pitch)
    {
        if (player == null || voice == null) return;
        float nearness = 1f - Mathf.Clamp01(Vector2.Distance(player.position, transform.position) / hearingRange);
        if (nearness <= 0f) return;
        SoundManager.PlayOneShot(voice, sound, volume * nearness, pitch, fallback);
    }

    // --- Art, made in code ---

    // The middle of the lens on each frame, in pixels from the top-left of the housing (which hangs from its top middle).
    const float HousingHalfWidth = 7f;
    static readonly Vector2[] LensPixel = { new Vector2(7f, 9f), new Vector2(12f, 8f), new Vector2(13f, 5f) };

    static void MakeSprites()
    {
        var palette = new Dictionary<char, Color32>
        {
            { '#', new Color32(22, 24, 29, 255) },
            { 'H', new Color32(196, 202, 210, 255) },
            { 'h', new Color32(146, 152, 162, 255) },
            { 's', new Color32(104, 110, 120, 255) },
            { 'b', new Color32(42, 46, 54, 255) },
            { 'L', new Color32(12, 14, 18, 255) },
            { 'r', new Color32(70, 76, 86, 255) },      // a rivet
        };
        var top = new Vector2(0.5f, 1f);
        housingFrames = new[]
        {
            // Looking straight down.
            PixelArt.FromText(new[]
            {
                "...########...",
                "..#HHHHHHHH#..",
                ".#HhhhhhhhhH#.",
                ".#hhhhhrhhhh#.",
                ".#hhhhhhhhhh#.",
                ".#ssssssssss#.",
                "..#ssssssss#..",
                "...##bbbb##...",
                "....#bLLb#....",
                "....#bLLb#....",
                ".....####.....",
            }, palette, PixelsPerUnit, top),
            // Down and across.
            PixelArt.FromText(new[]
            {
                "..#######.....",
                ".#HHHHHHH#....",
                "#HhhhhhhhH#...",
                "#hhhhrhhhhh#..",
                "#hhhhhhhhhhh#.",
                ".#sssssssssh#.",
                "..#sssssssbb##",
                "...#ssssssbLL#",
                "....#sssssbLL#",
                ".....#####bbb#",
                "..........####",
            }, palette, PixelsPerUnit, top),
            // Across.
            PixelArt.FromText(new[]
            {
                "..............",
                "..#########...",
                ".#HHHHHHHHH#..",
                "#Hhhhhrhhhhh##",
                "#hhhhhhhhhhbLL",
                "#hhhhhhhhhhbLL",
                "#ssssssssssb##",
                ".#sssssssss#..",
                "..#########...",
                "..............",
                "..............",
            }, palette, PixelsPerUnit, top),
        };
        bracketSprite = PixelArt.FromText(new[]
        {
            ".########.",
            "#HHHHHHHH#",
            "#ssssssss#",
            ".###ss###.",
            "...#ss#...",
            "...####...",
        }, palette, PixelsPerUnit, new Vector2(0.5f, 0f));
        lensSprite = PixelArt.FromText(new[] { "WW", "WW" },
            new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } }, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }

    // A quick rising whirr, as it turns to look.
    static AudioClip ServoFallback => servoFallback != null ? servoFallback : (servoFallback = MakeClip("Camera Servo", 0.28f, t =>
    {
        float pitch = 180f + 260f * t / 0.28f;
        float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * pitch * t)) * 0.25f + Mathf.Sin(2f * Mathf.PI * pitch * 2f * t) * 0.15f;
        return buzz * Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((0.28f - t) / 0.06f);
    }));

    // Two sharp tones, twice.
    static AudioClip AlertFallback => alertFallback != null ? alertFallback : (alertFallback = MakeClip("Camera Alert", 0.6f, t =>
    {
        float pitch = (int)(t / 0.15f) % 2 == 0 ? 1320f : 990f;
        float envelope = Mathf.Clamp01(Mathf.Repeat(t, 0.15f) / 0.01f) * Mathf.Clamp01((0.15f - Mathf.Repeat(t, 0.15f)) / 0.03f);
        return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * pitch * t)) * 0.22f * envelope;
    }));

    static AudioClip MakeClip(string clipName, float seconds, System.Func<float, float> sample)
    {
        const int rate = 44100;
        var data = new float[Mathf.CeilToInt(seconds * rate)];
        for (int i = 0; i < data.Length; i++) data[i] = sample(i / (float)rate);
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDrawGizmosSelected()
    {
        float angle = Application.isPlaying ? aim : baseAngle;
        Vector3 eye = transform.position;
        Gizmos.color = new Color(1f, 0.7f, 0.2f);
        foreach (float side in new[] { -fieldOfView * 0.5f, fieldOfView * 0.5f })
            Gizmos.DrawLine(eye, eye + (Vector3)(Direction(angle + side) * range));
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawLine(eye, eye + (Vector3)(Direction(baseAngle - sweep) * range * 0.5f));
        Gizmos.DrawLine(eye, eye + (Vector3)(Direction(baseAngle + sweep) * range * 0.5f));
    }
}
