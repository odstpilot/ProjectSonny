using UnityEngine;
using UnityEngine.AI;

// bot that only moves when not seen
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
    [Tooltip("Its face slams up to fill the screen when it catches the player (AngelJumpscare).")]
    public bool jumpscare = true;
    [Tooltip("The face for the jumpscare. Empty uses its own sprite.")]
    public Sprite jumpscareFace;
    [Tooltip("The scene's sound for the jumpscare, on top of catchClip. Empty for none.")]
    [SoundName] public string jumpscareSound = "Wall Grab";
    [Tooltip("Sent back, they're dragged there in the dark and wake on the floor as it rolls away (DraggedBack). Off, " +
             "they're just put there.")]
    public bool dragBack = true;

    [Header("Look")]
    [Tooltip("Its sparks, and the flash when something hits it. It casts no light of its own.")]
    public Color huntColor = new Color(1f, 0.2f, 0.15f);
    [Tooltip("The tint and sparks while it's shut down.")]
    public Color stunnedColor = new Color(0.35f, 0.9f, 1f);

    [Header("Sound")]
    [Tooltip("The scene's sound for its footsteps, the only warning the player gets. Without one, the player's own " +
             "footsteps, slowed right down, under a clank made in code.")]
    [SoundName] public string footstepSound = "Angel Footsteps";
    [Tooltip("Clips to use instead of the scene's sound. Optional.")]
    public AudioClip[] footstepClips = new AudioClip[0];
    [Range(0f, 1f)] public float footstepVolume = 0.85f;
    [Tooltip("Seconds between steps at top speed. It steps slower when it's moving slower.")]
    public float footstepInterval = 0.38f;
    [Tooltip("How far its footsteps carry, in world units. Louder the closer it is, and panned to the side it's on.")]
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
    private float movingTime;
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

    Vector2 Velocity => walkArea != null ? gridVelocity : (Vector2)agent.velocity;

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
        if (walkArea != null) agent.enabled = false;

        agent.updateRotation = false;
        agent.updateUpAxis = false;

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

    void UpdateFlash()
    {
        if (flashEvery.y <= 0f || state != State.Hunting) return;
        if (nextFlash <= 0f) nextFlash = Time.time + Random.Range(flashEvery.x, flashEvery.y);
        if (Time.time < nextFlash) return;
        nextFlash = Time.time + Random.Range(flashEvery.x, flashEvery.y);
        LightFlash.Spawn(transform.position, huntColor, flashRadius, flashIntensity, 0.35f);
        HitEffects.Sparks(transform.position, Vector2.up, 5, 2.5f, 360f, huntColor, Color.white);
    }

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

        movingTime += Time.deltaTime;
        float windUp = speedRampTime > 0f ? Mathf.Clamp01(movingTime / speedRampTime) : 1f;
        agent.speed = Mathf.Lerp(baseSpeed * startSpeedFraction, baseSpeed, windUp);

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
        if (!NearestOnFloor(walkArea.CellAt(lastSeen), out Vector2Int goal) || (hidden && Vector2.Distance(at, lastSeen) <= ArriveDistance))
        {
            Brake();
            return;
        }

        UpdateLurch(at);
        if (lurch == Lurch.Freeze)
        {
            Brake();
            return;
        }
        if (lurch != Lurch.None && NearestOnFloor(walkArea.CellAt(lurchTarget), out Vector2Int aside)) goal = aside;
        MoveToward(at, goal, speed, lurch != Lurch.None ? walkArea.CenterOf(goal) : (Vector2)lastSeen);
    }

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
            chasing = false;
            detection = 0.5f;
            gridPath.Clear();
        }
    }

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
            float hearing = PlayerSprinting ? sprintHearingRange : hearingRange;
            if (distance <= hearing) seen = PlayerCrouching ? 0.5f * crouchDetectionMultiplier : 0.6f;
        }
        return seen > 0f && ClearView(at, player.position) ? seen : 0f;
    }

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
            Vector2 toward = (Vector2)lastSeen - at;
            Vector2 side = toward.sqrMagnitude > 0.01f ? new Vector2(-toward.y, toward.x).normalized : Random.insideUnitCircle.normalized;
            if (Random.value < 0.5f) side = -side;
            lurch = Lurch.Swerve;
            lurchTarget = Vector2.Lerp(at, (Vector2)lastSeen, 0.6f) + side * swerveCells;
            lurchUntil = Time.time + 1.2f;
        }
    }

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
            if (stealth)
            {
                chasing = false;
                detection = 0.5f;
            }
            Hold();
            movingTime = 0f;

            HitEffects.Ring(transform.position, stunnedColor, 1.4f);
            HitEffects.Sparks(transform.position, Vector2.up, 14, 5f, 360f, stunnedColor, Color.white);
            LightFlash.Spawn(transform.position, stunnedColor, 2.5f, 1.4f, 0.3f);
            PlayOneShot(stunClip);
        }
        else if (previous == State.Stunned)
        {
            HitEffects.Sparks(transform.position, Vector2.up, 8, 3f, 360f, huntColor, Color.white);
        }
    }

    public void Stun()
    {
        Stagger(stunDuration);
    }

    public void Stagger(float seconds)
    {
        if (seconds <= 0f) return;
        shutdownUntil = Mathf.Max(shutdownUntil, Time.time + seconds);
    }

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

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCatch(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryCatch(other);
    }

    void TryCatch(Collider2D other)
    {
        if (state == State.Stunned || Time.time < nextCatchTime || DraggedBack.Running) return;
        if (other == null || !other.CompareTag("Player")) return;

        nextCatchTime = Time.time + catchCooldown;
        movingTime = 0f;

        Vector2 toPlayer = (Vector2)other.transform.position - (Vector2)transform.position;
        Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.up;

        CameraShake.Shake(catchShake);
        CameraShake.Kick(direction, 0.25f);
        HitStop.Freeze(catchHitStop);
        HitEffects.Ring(transform.position, huntColor, 2f);
        HitEffects.Sparks(transform.position, direction, 18, 6f, 200f, huntColor, Color.white);
        LightFlash.Spawn(transform.position, huntColor, 3f, 1.6f, 0.35f);
        PlayOneShot(catchClip);
        if (jumpscare)
        {
            AngelJumpscare.Play(jumpscareFace != null ? jumpscareFace : body != null ? body.sprite : null);
            Sfx.Play(jumpscareSound, 1f, 0.8f);
            Sfx.Play("Robot Alert", 1f, 0.55f);
        }

        if (catchDamage > 0f)
        {
            IDamageable target = Damageable.FromCollider(other);
            if (target != null)
                target.TakeDamage(new DamageInfo(catchDamage, direction, 0f, gameObject, other.transform.position));
        }

        SendPlayerBack(other);
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
        var dying = playerCollider.GetComponentInParent<PlayerHealthHandler>();
        if (dying != null && dying.IsDying) return;
        Vector3 destination;
        if (resetPoint != null)
        {
            destination = resetPoint.position;
        }
        else
        {
            if (dying == null) return;
            destination = dying.RespawnPoint;
        }

        if (dragBack && dying != null)
        {
            Stagger(AngelJumpscare.ToBlack + DraggedBack.Seconds);
            DraggedBack.AfterJumpscare(dying, destination, body != null ? new[] { body.sprite } : null);
            return;
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

    void UpdateLook()
    {
        if (body == null) return;

        Color tint = baseColor;
        if (Time.time < flashUntil) tint = Color.white;
        else if (state == State.Stunned) tint = Color.Lerp(baseColor, stunnedColor, 0.5f);
        body.color = tint;
    }

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

    void CreateFootstepSource()
    {
        steps = gameObject.AddComponent<AudioSource>();
        steps.playOnAwake = false;
        steps.loop = false;
        steps.spatialBlend = 0f;
    }

    void UpdateFootsteps()
    {
        if (steps == null || state != State.Hunting || player == null) return;

        float speedFraction = baseSpeed > 0f ? Mathf.Clamp01(Velocity.magnitude / baseSpeed) : 0f;
        if (speedFraction < 0.05f) return;

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;
        stepTimer = footstepInterval / Mathf.Max(0.35f, speedFraction);

        Vector2 offset = transform.position - player.position;
        float near = 1f - Mathf.Clamp01(offset.magnitude / footstepRange);
        if (near <= 0f) return;
        float loudness = footstepVolume * near * near * (0.6f + 0.4f * speedFraction);
        steps.panStereo = Mathf.Clamp(offset.x / 8f, -0.8f, 0.8f);

        AudioClip own = footstepClips.Length > 0 ? footstepClips[Random.Range(0, footstepClips.Length)] : null;
        if (own != null)
        {
            steps.pitch = Random.Range(0.92f, 1.08f);
            steps.PlayOneShot(own, loudness * GameSettings.EffectsVolume);
        }
        else if (SoundManager.Get(footstepSound)?.HasClip == true)
            SoundManager.PlayOneShot(steps, footstepSound, loudness);
        else
        {
            SoundManager.PlayOneShot(steps, "Footsteps", loudness * 1.4f, Random.Range(0.5f, 0.6f));
            steps.pitch = Random.Range(0.9f, 1.1f);
            steps.PlayOneShot(Clank, loudness * 0.7f * GameSettings.EffectsVolume);
        }
    }

    static AudioClip clank;
    static AudioClip Clank
    {
        get
        {
            if (clank != null) return clank;
            const int rate = 44100;
            const float seconds = 0.32f;
            var data = new float[Mathf.CeilToInt(seconds * rate)];
            var noise = new System.Random(7);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float thump = Mathf.Sin(2f * Mathf.PI * (70f - 40f * t) * t) * Mathf.Exp(-t * 22f);
                float ring = (Mathf.Sin(2f * Mathf.PI * 410f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1130f * t)) * Mathf.Exp(-t * 30f) * 0.35f;
                float grit = ((float)noise.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.4f;
                data[i] = (thump * 0.8f + ring + grit) * Mathf.Clamp01(t / 0.002f);
            }
            clank = AudioClip.Create("Angel Clank", data.Length, 1, rate, false);
            clank.SetData(data, 0);
            return clank;
        }
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
