using UnityEngine;
using UnityEngine.AI;

// A converted maintenance robot that hunts the player without pause. Light doesn't touch it and hiding doesn't stop it:
// only an EMP shuts it down (Stun), and a hit from a weapon staggers it for a moment. It can't be destroyed - the EMP
// and running are the only answers.
//
// It gives off no light of its own, so in a dark corridor its footsteps are the only warning the player gets. They
// quicken as it works up to speed, and stop dead the instant it's shut down.
//
// It chases over a NavMesh, or, given a walkArea (a room's floor as a grid, CrewWalkArea), over that instead, with its
// Nav Mesh Agent switched off: for the station's rooms, which have no NavMesh. It keeps to that floor; with the player
// off it, it waits. There it can be snuck past (stealth), the way the robots are: it has to notice the player before it
// comes for them, seeing and hearing much further than they do, and crouching barely dulls it.
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngel : MonoBehaviour, IDamageable
{
    const float HitFlashTime = 0.08f;
    const float StunSparkInterval = 0.14f;
    const float ArriveDistance = 0.4f;

    [Header("Targets")]
    [Tooltip("Leave empty to find the object tagged Player.")]
    public Transform player;
    [Tooltip("Where the player is sent when it catches them. Leave empty to use their last checkpoint.")]
    public Transform resetPoint;

    [Header("Shutdown")]
    [Tooltip("Seconds an EMP shuts it down for. The only thing that stops it for long.")]
    public float stunDuration = 2f;
    [Tooltip("Seconds a hit from a weapon staggers it. It can't be destroyed.")]
    public float staggerDuration = 0.5f;

    [Header("Chase")]
    [Tooltip("Top speed, in world units a second. This replaces the Nav Mesh Agent's own speed.")]
    public float chaseSpeed = 9f;
    [Tooltip("Speed the moment it starts moving, as a fraction of top speed.")]
    [Range(0.05f, 1f)] public float startSpeedFraction = 0.75f;
    [Tooltip("Seconds of moving before it reaches top speed. Shutting it down puts it back to its starting speed.")]
    public float speedRampTime = 2.5f;
    [Tooltip("How hard it speeds up and stops. High keeps it from sliding round corners or drifting past the player.")]
    public float acceleration = 60f;
    [Tooltip("Flip the sprite to face the way it's moving.")]
    public bool flipSpriteToMovement = true;
    [Tooltip("Chase over this floor instead of a NavMesh. Leave empty to use the NavMesh.")]
    public CrewWalkArea walkArea;

    [Header("Unpredictable (on a walkArea)")]
    [Tooltip("Seconds between lurches off its line: a swerve to one side of the player, a dead stop, or a dash somewhere else entirely.")]
    public Vector2 lurchEvery = new Vector2(1.2f, 3f);
    [Tooltip("How far to one side of the player a swerve aims, in cells.")]
    public float swerveCells = 3f;
    [Tooltip("Chances of each lurch being a dead stop, or a dash off somewhere at random. The rest are swerves.")]
    [Range(0f, 1f)] public float freezeChance = 0.25f;
    [Range(0f, 1f)] public float prowlChance = 0.2f;
    [Tooltip("How long a stop lasts, and a dash off somewhere.")]
    public Vector2 freezeTime = new Vector2(0.4f, 1.1f);
    public Vector2 prowlTime = new Vector2(1.5f, 3f);
    [Tooltip("It only dashes off somewhere when the player's at least this far away; closer, it commits.")]
    public float commitDistance = 4f;

    [Header("Stealth (on a walkArea)")]
    [Tooltip("It has to notice the player (the ring round them) before it chases, and can lose them. Off, it always knows where they are.")]
    public bool stealth;
    [Tooltip("How far it sees, how wide (degrees), and how close it notices them whichever way it's facing.")]
    public float sightRange = 12f;
    public float fieldOfView = 260f;
    public float closeRange = 2.5f;
    [Tooltip("How far it hears footsteps all round, walking and running.")]
    public float hearingRange = 7f;
    public float sprintHearingRange = 12f;
    [Tooltip("Seconds of looking right at them before it's sure, and for the ring to drain once they're out of sight.")]
    public float detectionTime = 0.9f;
    public float detectionFade = 2.5f;
    [Tooltip("Crouching: how far it still sees them, and how fast the ring still fills, as fractions of normal.")]
    [Range(0.1f, 1f)] public float crouchSightMultiplier = 0.85f;
    [Range(0.05f, 1f)] public float crouchDetectionMultiplier = 0.75f;
    [Tooltip("Seconds out of sight before it gives up the chase (once it's got to where it last saw them).")]
    public float loseTrackAfter = 3f;
    [Tooltip("Its prowling speed, as a fraction of its top speed.")]
    [Range(0.1f, 1f)] public float prowlSpeedFraction = 0.45f;

    [Header("Heartbeat")]
    [Tooltip("The player's heartbeat, heard from this far off and louder and faster the nearer it gets. 0 for none.")]
    public float heartbeatRange;
    [SoundName] public string heartbeatSound = "Low Health Heartbeat";
    [Range(0f, 1f)] public float heartbeatVolume = 1f;

    [Header("Flashing its lights")]
    [Tooltip("Seconds between flashes of its lights, so it shows itself now and then. 0 for never.")]
    public Vector2 flashEvery = Vector2.zero;
    public float flashRadius = 3.5f;
    public float flashIntensity = 1.3f;

    [Header("Catch")]
    [Tooltip("Damage when it reaches the player. 0 just sends them back.")]
    public float catchDamage = 0f;
    [Tooltip("Seconds before it can catch the player again.")]
    public float catchCooldown = 1.5f;
    [Range(0f, 1f)] public float catchShake = 0.8f;
    public float catchHitStop = 0.12f;

    [Header("Look")]
    [Tooltip("Its sparks, and the flash when something hits it. It casts no light of its own.")]
    public Color huntColor = new Color(1f, 0.2f, 0.15f);
    [Tooltip("The tint and sparks while it's shut down.")]
    public Color stunnedColor = new Color(0.35f, 0.9f, 1f);

    [Header("Sound")]
    [Tooltip("Its footsteps, the only warning the player gets. Played in 3D, so they can hear which side it's on.")]
    public AudioClip[] footstepClips = new AudioClip[0];
    [Range(0f, 1f)] public float footstepVolume = 0.85f;
    [Tooltip("Seconds between steps at top speed. It steps slower when it's moving slower.")]
    public float footstepInterval = 0.38f;
    [Tooltip("How far its footsteps carry, in world units.")]
    public float footstepRange = 20f;
    [Tooltip("Drive the looping sound on this robot from how fast it's moving: louder and higher as it speeds up.")]
    public bool driveLoopingSound = true;
    [Tooltip("Plays when it shuts down. Optional.")]
    public AudioClip stunClip;
    [Tooltip("Plays when it catches the player. Optional.")]
    public AudioClip catchClip;

    enum State { Hunting, Stunned }

    private NavMeshAgent agent;
    private SpriteRenderer body;
    private Collider2D bodyCollider;
    private AudioSource loop;
    private AudioSource steps;
    private float stepTimer;

    private State state = State.Hunting;
    private Color baseColor = Color.white;
    private float baseSpeed = 9f;
    private float baseLoopVolume = 1f;
    private float baseLoopPitch = 1f;
    private float shutdownUntil;
    private float movingTime;       // seconds it's been moving; winds its speed up
    private float nextCatchTime;
    private float flashUntil;
    private float sparkTimer;
    private Vector3 lastSeen;
    private bool warnedOffNavMesh;
    private readonly System.Collections.Generic.List<Vector2> gridPath = new System.Collections.Generic.List<Vector2>();
    private Vector2 gridVelocity;
    private float nextRepath;
    private float nextLurch;
    private float lurchUntil;
    private enum Lurch { None, Swerve, Freeze, Prowl }
    private Lurch lurch;
    private Vector2 lurchTarget;
    private float nextFlash;
    private bool chasing;
    private AudioSource heartbeat;
    private float detection;
    private float lastPerceived = -100f;
    private Vector2 facing = Vector2.down;
    private Vector2 lastPlayerPosition;
    private Vector2Int prowlGoal = new Vector2Int(-1, -1);
    private float prowlGiveUp;
    private float prowlPauseUntil;
    private float nextGlance;
    private Health playerHealth;
    private PlayerController playerController;
    private readonly System.Collections.Generic.List<RaycastHit2D> viewHits = new System.Collections.Generic.List<RaycastHit2D>();

    // How fast it's going, whichever way it gets about.
    Vector2 Velocity => walkArea != null ? gridVelocity : (Vector2)agent.velocity;

    // Bigger than its body, so sparks land around its edges.
    float BodyRadius => bodyCollider != null
        ? Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) + 0.05f
        : 0.55f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<SpriteRenderer>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        loop = GetComponentInChildren<AudioSource>();
    }

    void Start()
    {
        // On its own floor, the agent's not needed (and would complain about there being no NavMesh).
        if (walkArea != null) agent.enabled = false;

        // A 2D navmesh agent walks the XY plane and never turns.
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        // Its own speed and acceleration, so it starts, turns, and stops sharply instead of gliding about.
        baseSpeed = chaseSpeed;
        agent.speed = chaseSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = 1080f;
        agent.autoBraking = false;
        agent.stoppingDistance = 0f;

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        lastSeen = player != null ? player.position : transform.position;
        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();
            playerController = player.GetComponent<PlayerController>();
            lastPlayerPosition = player.position;
        }

        if (body != null) baseColor = body.color;
        if (loop != null)
        {
            baseLoopVolume = loop.volume;
            baseLoopPitch = loop.pitch;
        }

        CreateFootstepSource();

        if (walkArea == null && !agent.isOnNavMesh)
            Debug.LogWarning($"{name}: not on a NavMesh, so it can't move. Bake the navmesh or move it onto one.", this);
    }

    void Update()
    {
        State next = Time.time < shutdownUntil ? State.Stunned : State.Hunting;
        if (next != state) EnterState(next);

        if (state == State.Stunned)
        {
            Hold();
            ShowShutdownSparks();
        }
        else
        {
            Hunt();
        }

        UpdateLook();
        UpdateSound();
        UpdateFootsteps();
        UpdateFlash();
        UpdateHeartbeat();
    }

    // Now and then, its lights flare red for a moment: there, then gone.
    void UpdateFlash()
    {
        if (flashEvery.y <= 0f || state != State.Hunting) return;
        if (nextFlash <= 0f) nextFlash = Time.time + Random.Range(flashEvery.x, flashEvery.y);
        if (Time.time < nextFlash) return;
        nextFlash = Time.time + Random.Range(flashEvery.x, flashEvery.y);
        LightFlash.Spawn(transform.position, huntColor, flashRadius, flashIntensity, 0.35f);
        HitEffects.Sparks(transform.position, Vector2.up, 5, 2.5f, 360f, huntColor, Color.white);
    }

    // --- Moving ---

    void Hunt()
    {
        if (walkArea != null)
        {
            HuntOnFloor();
            return;
        }
        if (player == null || !agent.isOnNavMesh)
        {
            WarnOffNavMesh();
            return;
        }

        // It works up to top speed the longer it goes without being shut down.
        movingTime += Time.deltaTime;
        float windUp = speedRampTime > 0f ? Mathf.Clamp01(movingTime / speedRampTime) : 1f;
        agent.speed = Mathf.Lerp(baseSpeed * startSpeedFraction, baseSpeed, windUp);

        // Someone in a locker has vanished as far as it's concerned: it goes to where they were and waits there.
        bool hidden = Locker.IsPlayerHidden;
        if (!hidden) lastSeen = player.position;

        if (hidden && Vector3.Distance(transform.position, lastSeen) <= ArriveDistance)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(lastSeen);

        if (flipSpriteToMovement && body != null && Mathf.Abs(agent.velocity.x) > 0.05f)
            body.flipX = agent.velocity.x < 0f;
    }

    // Over its walkArea: the shortest way to the player across the floor, found again every so often as they move. With
    // stealth on, it has to notice them first (Sense), and until it does it prowls the room, or goes to look at what it
    // half noticed.
    void HuntOnFloor()
    {
        if (player == null) return;
        Vector2 at = transform.position;
        if (stealth)
        {
            Sense(at);
            if (!chasing)
            {
                movingTime = 0f;
                Prowl(at);
                return;
            }
        }

        movingTime += Time.deltaTime;
        float windUp = speedRampTime > 0f ? Mathf.Clamp01(movingTime / speedRampTime) : 1f;
        float speed = Mathf.Lerp(baseSpeed * startSpeedFraction, baseSpeed, windUp);

        bool hidden = Locker.IsPlayerHidden;
        if (!hidden && !stealth) lastSeen = player.position;
        // Where they are on its floor, or near enough; nowhere near it, they've left the room, and it waits.
        if (!NearestOnFloor(walkArea.CellAt(lastSeen), out Vector2Int goal) || (hidden && Vector2.Distance(at, lastSeen) <= ArriveDistance))
        {
            Brake();
            return;
        }

        // Every so often it lurches off its line, so there's no telling where it'll come from.
        UpdateLurch(at);
        if (lurch == Lurch.Freeze)
        {
            Brake();
            return;
        }
        if (lurch != Lurch.None && NearestOnFloor(walkArea.CellAt(lurchTarget), out Vector2Int aside)) goal = aside;
        // Down to its last cell, it goes straight for them (or wherever it's lurched to).
        MoveToward(at, goal, speed, lurch != Lurch.None ? walkArea.CenterOf(goal) : (Vector2)lastSeen);
    }

    // Along the shortest way over the floor to the goal cell, then straight at finalPoint.
    void MoveToward(Vector2 at, Vector2Int goal, float speed, Vector2 finalPoint)
    {
        if (Time.time >= nextRepath)
        {
            nextRepath = Time.time + 0.2f;
            if (!NearestOnFloor(walkArea.CellAt(at), out Vector2Int from) || !walkArea.FindPath(from, goal, gridPath)) gridPath.Clear();
        }
        while (gridPath.Count > 0 && Vector2.Distance(at, gridPath[0]) < 0.2f) gridPath.RemoveAt(0);
        Vector2 target = gridPath.Count > 0 ? gridPath[0] : finalPoint;
        Vector2 to = target - at;
        Vector2 wanted = to.magnitude > 0.05f ? to.normalized * speed : Vector2.zero;
        gridVelocity = Vector2.MoveTowards(gridVelocity, wanted, acceleration * Time.deltaTime);
        transform.position += (Vector3)(gridVelocity * Time.deltaTime);
        if (gridVelocity.sqrMagnitude > 0.04f) facing = gridVelocity.normalized;

        if (flipSpriteToMovement && body != null && Mathf.Abs(gridVelocity.x) > 0.05f)
            body.flipX = gridVelocity.x < 0f;
    }

    // --- Stealth (on a walkArea) ---

    // Notices the player as the robots do, from what it sees and hears of them, but never shows it (no ring round them,
    // StealthMeter: the heartbeat's the only warning). Sure of them, it gives chase; chasing, it keeps them in view
    // anywhere in sight, and gives up once it's lost them for long enough and got to where it last saw them.
    void Sense(Vector2 at)
    {
        Vector2 now = player.position;
        bool moving = Time.deltaTime > 0f && (now - lastPlayerPosition).sqrMagnitude / (Time.deltaTime * Time.deltaTime) > 0.25f;
        lastPlayerPosition = now;

        float seen = Perceive(at, chasing, moving);
        if (seen > 0f)
        {
            float nearness = 1f - Mathf.Clamp01(Mathf.InverseLerp(closeRange, sightRange, Vector2.Distance(at, now)));
            float rate = Mathf.Lerp(1f, 2.5f, nearness) / Mathf.Max(0.05f, detectionTime) * seen;
            if (!chasing && PlayerCrouching) rate *= crouchDetectionMultiplier;
            detection = Mathf.Clamp01(detection + rate * Time.deltaTime);
            lastSeen = now;
            lastPerceived = Time.time;
        }
        else if (!chasing)
        {
            detection = Mathf.MoveTowards(detection, 0f, Time.deltaTime / Mathf.Max(0.05f, detectionFade));
        }

        if (!chasing && detection >= 1f)
        {
            chasing = true;
            nextRepath = 0f;
        }
        else if (chasing && Time.time - lastPerceived > loseTrackAfter && Vector2.Distance(at, lastSeen) < 1f)
        {
            // Lost them: it goes back to prowling, still on edge.
            chasing = false;
            detection = 0.5f;
            gridPath.Clear();
        }

    }

    // How well it makes the player out right now: 1 clearly, less for only hearing them, 0 not at all.
    float Perceive(Vector2 at, bool tracking, bool moving)
    {
        if (Locker.IsPlayerHidden || (playerHealth != null && playerHealth.IsDead)) return 0f;
        Vector2 to = (Vector2)player.position - at;
        float distance = to.magnitude;
        float range = sightRange * (!tracking && PlayerCrouching ? crouchSightMultiplier : 1f);
        float seen = 0f;
        if (distance <= range && (tracking || distance <= closeRange || Vector2.Angle(facing, to) <= fieldOfView * 0.5f)) seen = 1f;
        else if (!tracking && moving)
        {
            // Footsteps: further off when they run, and quieter, not silent, crouched.
            float hearing = PlayerSprinting ? sprintHearingRange : hearingRange;
            if (distance <= hearing) seen = PlayerCrouching ? 0.5f * crouchDetectionMultiplier : 0.6f;
        }
        return seen > 0f && ClearView(at, player.position) ? seen : 0f;
    }

    // Nothing solid between it and the point, apart from itself, the player, and the robots.
    bool ClearView(Vector2 from, Vector2 target)
    {
        var solidOnly = new ContactFilter2D { useTriggers = false };
        Physics2D.Linecast(from, target, solidOnly, viewHits);
        foreach (RaycastHit2D hit in viewHits)
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player)) continue;
            if (hit.collider.GetComponentInParent<PlaceholderRobot>() != null) continue;
            return false;
        }
        return true;
    }

    // Not chasing: half sure of something, it goes to look; otherwise it prowls the room at a stalk, somewhere new each
    // time, stopping now and then to look about.
    void Prowl(Vector2 at)
    {
        float speed = baseSpeed * prowlSpeedFraction;
        if (detection >= 0.4f && NearestOnFloor(walkArea.CellAt(lastSeen), out Vector2Int glimpsed))
        {
            MoveToward(at, glimpsed, speed * 1.3f, walkArea.CenterOf(glimpsed));
            return;
        }
        if (Time.time < prowlPauseUntil)
        {
            Brake();
            if (Time.time >= nextGlance)
            {
                facing = Random.insideUnitCircle.normalized;
                nextGlance = Time.time + Random.Range(0.3f, 0.8f);
            }
            return;
        }
        bool there = Vector2.Distance(at, walkArea.CenterOf(prowlGoal)) < 0.4f;
        if (!walkArea.IsWalkable(prowlGoal) || there || Time.time >= prowlGiveUp)
        {
            if (there) prowlPauseUntil = Time.time + Random.Range(0.4f, 1.6f);
            if (!walkArea.TryPickDestination(walkArea.CellAt(at), 5f, out prowlGoal)) return;
            prowlGiveUp = Time.time + 8f;
            nextRepath = 0f;
            return;
        }
        MoveToward(at, prowlGoal, speed, walkArea.CenterOf(prowlGoal));
    }

    // Something set it off (an alarm): it knows where to go, and goes there flat out.
    public void Alarm(Vector2 at)
    {
        lastSeen = at;
        lastPerceived = Time.time;
        detection = 1f;
        chasing = true;
        nextRepath = 0f;
    }

    bool PlayerCrouching => playerController != null && playerController.IsCrouching;
    bool PlayerSprinting => playerController != null && playerController.IsSprinting;

    // Starts a lurch when it's due, and ends one when it's done (or when a swerve gets where it was going).
    void UpdateLurch(Vector2 at)
    {
        if (lurch != Lurch.None)
        {
            bool arrived = lurch == Lurch.Swerve && Vector2.Distance(at, lurchTarget) < 0.6f;
            if (Time.time < lurchUntil && !arrived) return;
            lurch = Lurch.None;
            nextRepath = 0f;
            nextLurch = Time.time + Random.Range(lurchEvery.x, lurchEvery.y);
            return;
        }
        if (nextLurch <= 0f) nextLurch = Time.time + Random.Range(lurchEvery.x, lurchEvery.y);
        if (Time.time < nextLurch) return;

        float roll = Random.value;
        float distance = Vector2.Distance(at, lastSeen);
        nextRepath = 0f;
        if (roll < freezeChance)
        {
            lurch = Lurch.Freeze;
            lurchUntil = Time.time + Random.Range(freezeTime.x, freezeTime.y);
        }
        else if (roll < freezeChance + prowlChance && distance > commitDistance
            && walkArea.TryPickDestination(walkArea.CellAt(at), 4f, out Vector2Int somewhere))
        {
            lurch = Lurch.Prowl;
            lurchTarget = walkArea.CenterOf(somewhere);
            lurchUntil = Time.time + Random.Range(prowlTime.x, prowlTime.y);
        }
        else
        {
            // Off to one side of them, to come at them from an angle.
            Vector2 toward = (Vector2)lastSeen - at;
            Vector2 side = toward.sqrMagnitude > 0.01f ? new Vector2(-toward.y, toward.x).normalized : Random.insideUnitCircle.normalized;
            if (Random.value < 0.5f) side = -side;
            lurch = Lurch.Swerve;
            lurchTarget = Vector2.Lerp(at, (Vector2)lastSeen, 0.6f) + side * swerveCells;
            lurchUntil = Time.time + 1.2f;
        }
    }

    // The walkable cell nearest this one, within a few cells.
    bool NearestOnFloor(Vector2Int cell, out Vector2Int found)
    {
        found = cell;
        if (walkArea.IsWalkable(cell)) return true;
        for (int reach = 1; reach <= 3; reach++)
            for (int dx = -reach; dx <= reach; dx++)
                for (int dy = -reach; dy <= reach; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != reach) continue;
                    Vector2Int next = cell + new Vector2Int(dx, dy);
                    if (!walkArea.IsWalkable(next)) continue;
                    found = next;
                    return true;
                }
        return false;
    }

    void Brake()
    {
        gridVelocity = Vector2.MoveTowards(gridVelocity, Vector2.zero, acceleration * Time.deltaTime);
        transform.position += (Vector3)(gridVelocity * Time.deltaTime);
    }

    // Stops it where it stands, instantly.
    void Hold()
    {
        gridVelocity = Vector2.zero;
        if (!agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    void EnterState(State next)
    {
        State previous = state;
        state = next;

        if (next == State.Stunned)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            gridPath.Clear();
            lurch = Lurch.None;
            // Coming round, it's lost them: back to prowling, on edge.
            if (stealth)
            {
                chasing = false;
                detection = 0.5f;
            }
            Hold();
            movingTime = 0f;   // back to its starting speed once it comes round

            HitEffects.Ring(transform.position, stunnedColor, 1.4f);
            HitEffects.Sparks(transform.position, Vector2.up, 14, 5f, 360f, stunnedColor, Color.white);
            LightFlash.Spawn(transform.position, stunnedColor, 2.5f, 1.4f, 0.3f);
            PlayOneShot(stunClip);
        }
        else if (previous == State.Stunned)
        {
            // Servos catching as it comes back up.
            HitEffects.Sparks(transform.position, Vector2.up, 8, 3f, 360f, huntColor, Color.white);
        }
    }

    // --- Being hit ---

    // An EMP shutdown. Shutting it down again while it's already down extends it.
    public void Stun()
    {
        Stagger(stunDuration);
    }

    // Holds it still for seconds, never cutting short a hold that already runs longer.
    public void Stagger(float seconds)
    {
        if (seconds <= 0f) return;
        shutdownUntil = Mathf.Max(shutdownUntil, Time.time + seconds);
    }

    // Weapons can't destroy it: a hit knocks it back a step and staggers it.
    public void TakeDamage(DamageInfo info)
    {
        flashUntil = Time.time + HitFlashTime;

        Vector2 point = info.point ?? (Vector2)transform.position;
        Vector2 away = info.direction.sqrMagnitude > 0.0001f ? info.direction.normalized : Vector2.up;
        HitEffects.Sparks(point, away, 10, 5f, 120f, huntColor, Color.white);
        HitEffects.Ring(point, Color.white, 0.8f);

        if (info.knockback > 0f && agent.isOnNavMesh)
            agent.Move(away * info.knockback);
        else if (info.knockback > 0f && walkArea != null)
        {
            Vector2 pushed = (Vector2)transform.position + away * info.knockback;
            if (walkArea.IsWalkable(walkArea.CellAt(pushed))) transform.position = new Vector3(pushed.x, pushed.y, transform.position.z);
        }

        Stagger(staggerDuration);
    }

    // --- Catching the player ---

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCatch(other);
    }

    // Also while they stay inside it, so standing still in its arms still counts.
    void OnTriggerStay2D(Collider2D other)
    {
        TryCatch(other);
    }

    void TryCatch(Collider2D other)
    {
        if (state == State.Stunned || Time.time < nextCatchTime) return;
        if (other == null || !other.CompareTag("Player")) return;

        nextCatchTime = Time.time + catchCooldown;
        movingTime = 0f;   // back to its starting speed afterwards

        Vector2 toPlayer = (Vector2)other.transform.position - (Vector2)transform.position;
        Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.up;

        CameraShake.Shake(catchShake);
        CameraShake.Kick(direction, 0.25f);
        HitStop.Freeze(catchHitStop);
        HitEffects.Ring(transform.position, huntColor, 2f);
        HitEffects.Sparks(transform.position, direction, 18, 6f, 200f, huntColor, Color.white);
        LightFlash.Spawn(transform.position, huntColor, 3f, 1.6f, 0.35f);
        PlayOneShot(catchClip);

        if (catchDamage > 0f)
        {
            IDamageable target = Damageable.FromCollider(other);
            if (target != null)
                target.TakeDamage(new DamageInfo(catchDamage, direction, 0f, gameObject, other.transform.position));
        }

        SendPlayerBack(other);
        // Thrown back, they're out of its sight again.
        if (stealth)
        {
            chasing = false;
            detection = 0.3f;
        }
    }

    void OnDisable()
    {
        if (heartbeat != null) heartbeat.Stop();
    }

    // The player's heart, pounding harder and faster the nearer it is: heard from heartbeatRange away, filling in as it
    // closes. 2D, so it's the same in both ears; it's their own heart.
    void UpdateHeartbeat()
    {
        if (heartbeatRange <= 0f || player == null) return;
        if (heartbeat == null)
        {
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.spatialBlend = 0f;
            SoundManager.Setup(heartbeat, heartbeatSound);
            heartbeat.loop = true;
            heartbeat.playOnAwake = false;
            heartbeat.volume = 0f;
        }
        if (heartbeat.clip == null) return;
        float near = 1f - Mathf.Clamp01(Vector2.Distance(transform.position, player.position) / heartbeatRange);
        float wanted = near * near * SoundManager.Volume(heartbeatSound) * heartbeatVolume;
        heartbeat.volume = Mathf.MoveTowards(heartbeat.volume, wanted, Time.deltaTime * 1.5f);
        heartbeat.pitch = Mathf.Lerp(0.85f, 1.7f, near);
        if (heartbeat.volume > 0.001f && !heartbeat.isPlaying) heartbeat.Play();
        else if (heartbeat.volume <= 0.001f && heartbeat.isPlaying) heartbeat.Stop();
    }

    // Somewhere else to hunt: a new floor (another room's), standing here, after the player.
    public void Relocate(CrewWalkArea area, Vector2 at)
    {
        if (area != null) walkArea = area;
        transform.position = new Vector3(at.x, at.y, transform.position.z);
        gridPath.Clear();
        gridVelocity = Vector2.zero;
        prowlGoal = new Vector2Int(-1, -1);
        lurch = Lurch.None;
        nextRepath = 0f;
    }

    void SendPlayerBack(Collider2D playerCollider)
    {
        // If that was the hit that killed them, they go down where they are; dying puts them back at the checkpoint.
        var dying = playerCollider.GetComponentInParent<PlayerHealthHandler>();
        if (dying != null && dying.IsDying) return;
        Vector3 destination;
        if (resetPoint != null)
        {
            destination = resetPoint.position;
        }
        else
        {
            // No reset point set: put them back at their last checkpoint instead.
            var handler = playerCollider.GetComponentInParent<PlayerHealthHandler>();
            if (handler == null) return;
            destination = handler.RespawnPoint;
        }

        Rigidbody2D rb = playerCollider.attachedRigidbody;
        Transform moved = rb != null ? rb.transform : playerCollider.transform;
        moved.position = destination;
        if (rb != null)
        {
            rb.position = destination;
            rb.linearVelocity = Vector2.zero;
        }
    }

    // --- Look and sound ---

    void UpdateLook()
    {
        if (body == null) return;

        Color tint = baseColor;
        if (Time.time < flashUntil) tint = Color.white;
        else if (state == State.Stunned) tint = Color.Lerp(baseColor, stunnedColor, 0.5f);
        body.color = tint;
    }

    // Electricity crawling over it while it's shut down.
    void ShowShutdownSparks()
    {
        sparkTimer -= Time.deltaTime;
        if (sparkTimer > 0f) return;

        sparkTimer = StunSparkInterval * Random.Range(0.6f, 1.6f);
        Vector2 at = (Vector2)transform.position + Random.insideUnitCircle * BodyRadius;
        HitEffects.Sparks(at, Random.insideUnitCircle.normalized, 3, 3f, 140f, stunnedColor, Color.white);
    }

    void UpdateSound()
    {
        if (!driveLoopingSound || loop == null) return;

        float speedFraction = baseSpeed > 0f ? Mathf.Clamp01(Velocity.magnitude / baseSpeed) : 0f;
        float volume = state == State.Hunting ? Mathf.Lerp(0.4f, 1f, speedFraction) : 0.05f;
        float pitch = state == State.Hunting ? Mathf.Lerp(0.85f, 1.15f, speedFraction) : 0.7f;

        loop.volume = Mathf.MoveTowards(loop.volume, baseLoopVolume * volume, Time.deltaTime * baseLoopVolume * 2f);
        loop.pitch = Mathf.MoveTowards(loop.pitch, baseLoopPitch * pitch, Time.deltaTime * 2f);
    }

    // Its own 3D source for footsteps, so they carry across the room and the looping hum's volume can't drag them down.
    void CreateFootstepSource()
    {
        if (footstepClips.Length == 0) return;

        steps = gameObject.AddComponent<AudioSource>();
        steps.playOnAwake = false;
        steps.loop = false;
        steps.spatialBlend = 1f;
        steps.dopplerLevel = 0f;
        steps.rolloffMode = AudioRolloffMode.Linear;
        steps.minDistance = 1.5f;
        steps.maxDistance = footstepRange;
    }

    // Steps land in time with how fast it's moving, and stop dead the moment an EMP puts it down.
    void UpdateFootsteps()
    {
        if (steps == null || state != State.Hunting) return;

        float speedFraction = baseSpeed > 0f ? Mathf.Clamp01(Velocity.magnitude / baseSpeed) : 0f;
        if (speedFraction < 0.05f) return;

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;

        stepTimer = footstepInterval / Mathf.Max(0.35f, speedFraction);
        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip == null) return;

        steps.pitch = Random.Range(0.92f, 1.08f);
        steps.PlayOneShot(clip, footstepVolume);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    void WarnOffNavMesh()
    {
        if (warnedOffNavMesh || agent.isOnNavMesh) return;
        warnedOffNavMesh = true;
        Debug.LogWarning($"{name}: off the NavMesh, so it has stopped chasing.", this);
    }

    // Select it to see how close the player has to be for it to catch them (cyan), and where it sends them (magenta).
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? BodyRadius : 0.55f);

        if (resetPoint == null) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, resetPoint.position);
        Gizmos.DrawWireSphere(resetPoint.position, 0.3f);
    }
}
