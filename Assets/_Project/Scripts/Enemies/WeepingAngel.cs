using UnityEngine;
using UnityEngine.AI;

// A converted maintenance robot that hunts the player without pause. Light doesn't touch it and hiding doesn't stop it:
// only an EMP shuts it down (Stun), and a hit from a weapon staggers it for a moment. It can't be destroyed - the EMP
// and running are the only answers.
//
// It gives off no light of its own, so in a dark corridor its footsteps are the only warning the player gets. They
// quicken as it works up to speed, and stop dead the instant it's shut down.
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

        if (body != null) baseColor = body.color;
        if (loop != null)
        {
            baseLoopVolume = loop.volume;
            baseLoopPitch = loop.pitch;
        }

        CreateFootstepSource();

        if (!agent.isOnNavMesh)
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
    }

    // --- Moving ---

    void Hunt()
    {
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

    // Stops it where it stands, instantly.
    void Hold()
    {
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
    }

    void SendPlayerBack(Collider2D playerCollider)
    {
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

        float speedFraction = baseSpeed > 0f ? Mathf.Clamp01(agent.velocity.magnitude / baseSpeed) : 0f;
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

        float speedFraction = baseSpeed > 0f ? Mathf.Clamp01(agent.velocity.magnitude / baseSpeed) : 0f;
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
