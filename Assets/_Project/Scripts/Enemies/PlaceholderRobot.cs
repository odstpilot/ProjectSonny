using System.Collections.Generic;
using UnityEngine;

// A stand-in enemy for testing combat and stealth. It patrols between points. When it spots the player (in front of
// it, in range, nothing solid in between) a "!" pops over its head and, after a moment, it gives chase: winding up
// (shaking and turning orange), lunging, and hurting the player if they're still in front of it. Hitting it stuns it.
// If it loses sight of the player it carries on to where it last saw them and a little past, looks around under a
// "?", and goes back to its patrol. Hiding in a Locker mid-chase counts as losing sight, so it walks right past the
// locker, unless it was close enough to see the player climb in: then it pulls them out.
// No pathfinding: it walks in straight lines and gives up on a point when a wall stops it.
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class PlaceholderRobot : MonoBehaviour
{
    const float WindupShake = 0.06f;
    const float SquashRecoverTime = 0.15f;
    const float ArriveDistance = 0.2f;
    const float StuckCheckInterval = 0.5f;  // how often it checks whether a wall has stopped it
    const float CatchStandOff = 0.8f;       // how far in front of a locker's exit it stands to pull the player out

    [Header("Patrol")]
    [Tooltip("Points to walk between, relative to where the robot starts: in order, then back to the first. Leave empty to stand guard.")]
    public Vector2[] patrolRoute = { new Vector2(-3f, 0f), new Vector2(3f, 0f) };
    public float patrolSpeed = 1.2f;
    [Tooltip("Pause at each patrol point.")]
    public float patrolPause = 1f;

    [Header("Senses")]
    [Tooltip("How far it can see.")]
    public float sightRange = 6f;
    [Tooltip("How wide its view is, in degrees, centered on the way it's facing.")]
    public float fieldOfView = 100f;
    [Tooltip("Notices the player this close whichever way it's facing, as long as nothing's in between.")]
    public float closeRange = 1.2f;
    [Tooltip("Seconds in plain sight before it's sure and gives chase. The ring around the player fills over this long.")]
    public float detectionTime = 1.2f;
    [Tooltip("How much quicker it makes the player out at point-blank range than at the very edge of its sight.")]
    public float closeDetectionMultiplier = 2.5f;
    [Tooltip("Seconds for its suspicion to drain away once it can't see them.")]
    public float detectionFade = 2f;
    [Tooltip("Seconds it looks around under the ? before going back to its patrol.")]
    public float searchTime = 2.5f;
    [Tooltip("After losing the player, it carries on this far past where it last saw them before searching.")]
    public float searchOvershoot = 1.5f;

    [Header("Lockers")]
    [Tooltip("If it's this close to a locker's door when the player climbs in mid-chase, it saw them and drags them out.")]
    public float catchRange = 2f;
    [Tooltip("Damage when it drags the player out of a locker.")]
    public float catchDamage = 2f;

    [Header("Movement")]
    [Tooltip("Speed while chasing.")]
    public float moveSpeed = 2.2f;
    [Tooltip("Starts winding up an attack at this distance from the player.")]
    public float attackRange = 1.1f;

    [Header("Attack")]
    public float damage = 1f;
    public float knockback = 0.6f;
    [Tooltip("Warning time before the lunge: the player's chance to dodge or interrupt it.")]
    public float windupTime = 0.45f;
    public float lungeDistance = 0.5f;
    public float lungeTime = 0.1f;
    [Tooltip("The lunge hurts inside a circle this far in front of the robot...")]
    public float hitReach = 0.7f;
    [Tooltip("...with this radius.")]
    public float hitRadius = 0.5f;
    [Tooltip("Pause after a lunge before it moves again.")]
    public float recoverTime = 0.6f;
    [Tooltip("How long getting hit freezes it.")]
    public float hitStunTime = 0.3f;
    public Color windupColor = new Color(1f, 0.6f, 0.2f);

    enum State { Patrol, Chase, Investigate, Search, Catch, Windup, Lunge, Recover, Stunned }

    private Health health;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private Vector3 bodyBasePosition;
    private Color bodyBaseColor;
    private Transform player;
    private Collider2D playerCollider;
    private Health playerHealth;
    private State state = State.Patrol;
    private float stateTimer;
    private Vector2 attackDirection = Vector2.right;
    private Vector2 facing = Vector2.right;
    private bool hitLanded;
    private float squash;   // 1 right after a hit, easing back to 0

    private Vector2 home;   // where it started; the patrol route is relative to this
    private int patrolIndex;
    private Vector2 lastSeenPosition;
    private Vector2 investigatePoint;
    private float nextLookAround;
    private Locker lockerToOpen;
    private Vector2 stuckCheckPosition;
    private float stuckCheckTimer;
    private readonly List<RaycastHit2D> sightHits = new List<RaycastHit2D>();

    // 0 to 1: how sure it is of what it's looking at. At 1 it gives chase. Drawn as the ring around the player.
    private float detection;

    void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponentInChildren<SpriteRenderer>();
        if (body != null)
        {
            bodyBasePosition = body.transform.localPosition;
            bodyBaseColor = body.color;
        }
        home = transform.position;
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        StealthMeter.Clear(this);
    }

    void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            playerCollider = found.GetComponent<Collider2D>();
            playerHealth = found.GetComponent<Health>();
        }

        stuckCheckPosition = rb.position;
    }

    void Update()
    {
        UpdateSquash();

        if (health.IsDead)
        {
            StealthMeter.Clear(this);
            return;
        }
        stateTimer -= Time.deltaTime;
        UpdateDetection();

        switch (state)
        {
            case State.Patrol:
                if (SpotPlayer()) break;
                if (patrolRoute.Length == 0 || stateTimer > 0f)
                {
                    rb.linearVelocity = Vector2.zero; // standing guard, or pausing at a point
                }
                else if (WalkTo(home + patrolRoute[patrolIndex % patrolRoute.Length], patrolSpeed))
                {
                    patrolIndex = (patrolIndex + 1) % patrolRoute.Length;
                    Enter(State.Patrol, patrolPause);
                }
                break;

            case State.Chase:
                if (Locker.IsPlayerHidden)
                {
                    PlayerHid();
                }
                else if (!CanSeePlayer(true))
                {
                    LoseTrack();
                }
                else
                {
                    Remember();
                    if (DistanceToPlayer() <= attackRange)
                    {
                        // Lock the lunge direction now, so the player can sidestep during the windup.
                        attackDirection = DirectionToPlayer();
                        Look(attackDirection);
                        Enter(State.Windup, windupTime);
                    }
                    else
                    {
                        WalkTo(player.position, moveSpeed);
                    }
                }
                break;

            case State.Investigate:
                if (SpotPlayer()) break;
                if (WalkTo(investigatePoint, moveSpeed)) StartSearch();
                break;

            case State.Search:
                if (SpotPlayer()) break;
                rb.linearVelocity = Vector2.zero;
                // Looks one way, then the other, then back.
                if (stateTimer <= nextLookAround)
                {
                    Look(new Vector2(facing.x >= 0f ? -1f : 1f, 0f));
                    nextLookAround -= searchTime / 3f;
                }
                if (stateTimer <= 0f) Enter(State.Patrol, 0f);
                break;

            case State.Catch:
                // They got out on their own before it reached the door.
                if (lockerToOpen == null || Locker.Occupied != lockerToOpen)
                {
                    lockerToOpen = null;
                    Enter(State.Chase, 0f);
                }
                else if (WalkTo(lockerToOpen.ExitPoint + lockerToOpen.DoorDirection * CatchStandOff, moveSpeed))
                {
                    CatchPlayer();
                }
                break;

            case State.Windup:
                rb.linearVelocity = Vector2.zero;
                ShowWindup(1f - Mathf.Clamp01(stateTimer / windupTime));
                if (stateTimer <= 0f)
                {
                    hitLanded = false;
                    Enter(State.Lunge, lungeTime);
                }
                break;

            case State.Lunge:
                rb.linearVelocity = attackDirection * (lungeDistance / lungeTime);
                if (!hitLanded) TryHitPlayer();
                if (stateTimer <= 0f) Enter(State.Recover, recoverTime);
                break;

            case State.Recover:
            case State.Stunned:
                rb.linearVelocity = Vector2.zero;
                if (stateTimer <= 0f) Enter(State.Chase, 0f);
                break;
        }
    }

    void Enter(State next, float duration)
    {
        if (state == State.Windup) ResetBody();
        state = next;
        stateTimer = duration;
        stuckCheckTimer = 0f;
        stuckCheckPosition = rb.position;
        if (next != State.Lunge) rb.linearVelocity = Vector2.zero;
    }

    // --- Senses ---

    // tracking: already after the player, so it keeps them in view whichever way it's facing.
    bool CanSeePlayer(bool tracking = false)
    {
        if (player == null || Locker.IsPlayerHidden) return false;
        if (playerHealth != null && playerHealth.IsDead) return false;

        Vector2 toPlayer = (Vector2)player.position - rb.position;
        float distance = toPlayer.magnitude;
        if (distance > sightRange) return false;
        if (!tracking && distance > closeRange && Vector2.Angle(facing, toPlayer) > fieldOfView * 0.5f) return false;
        return HasLineOfSight(player.position);
    }

    // Nothing solid between it and the point, apart from itself, the player, and other robots.
    bool HasLineOfSight(Vector2 target)
    {
        var solidOnly = new ContactFilter2D { useTriggers = false };
        Physics2D.Linecast(rb.position, target, solidOnly, sightHits);
        foreach (RaycastHit2D hit in sightHits)
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player)) continue;
            if (hit.collider.GetComponentInParent<PlaceholderRobot>() != null) continue;
            return false;
        }
        return true;
    }

    // Watches the player and fills the ring around them: quicker up close, draining away once they're out of sight.
    // While it's already hunting them it stays certain, so the ring stays full until it loses them.
    void UpdateDetection()
    {
        bool hunting = state == State.Chase || state == State.Catch || state == State.Windup
            || state == State.Lunge || state == State.Recover;

        if (CanSeePlayer(hunting))
        {
            float nearness = 1f - Mathf.Clamp01(Mathf.InverseLerp(closeRange, sightRange, DistanceToPlayer()));
            float rate = Mathf.Lerp(1f, closeDetectionMultiplier, nearness) / Mathf.Max(0.05f, detectionTime);
            detection = Mathf.Clamp01(detection + rate * Time.deltaTime);
        }
        else
        {
            detection = Mathf.MoveTowards(detection, 0f, Time.deltaTime / Mathf.Max(0.05f, detectionFade));
        }

        if (detection > 0f || hunting)
            StealthMeter.Report(this, rb.position, hunting ? 1f : detection, hunting);
        else
            StealthMeter.Clear(this);
    }

    // Checked while patrolling, investigating, or searching: once the ring fills, it gives chase. True if it did.
    bool SpotPlayer()
    {
        if (player == null || detection < 1f) return false;

        Remember();
        Look(DirectionToPlayer());
        Enter(State.Chase, 0f);
        return true;
    }

    void Remember()
    {
        lastSeenPosition = player.position;
    }

    // --- Losing the player ---

    // The player ducked into a locker mid-chase. Near enough to have seen them climb in, it goes to pull them out.
    // Otherwise it's as if they vanished: it keeps going past where they were, right by the locker, and searches.
    void PlayerHid()
    {
        Locker locker = Locker.Occupied;
        if (locker != null && Vector2.Distance(rb.position, locker.ExitPoint) <= catchRange)
        {
            lockerToOpen = locker;
            Enter(State.Catch, 0f);
        }
        else
        {
            LoseTrack();
        }
    }

    // Heads for where it last saw the player, carrying on a little past in the direction it was going.
    void LoseTrack()
    {
        Vector2 toLastSeen = lastSeenPosition - rb.position;
        Vector2 onward = toLastSeen.sqrMagnitude > 0.0001f ? toLastSeen.normalized : facing;
        investigatePoint = lastSeenPosition + onward * searchOvershoot;
        Enter(State.Investigate, 0f);
    }

    void StartSearch()
    {
        nextLookAround = searchTime * 2f / 3f;
        Enter(State.Search, searchTime);
    }

    // Yanks the locker open and hits the player as they come out.
    void CatchPlayer()
    {
        Locker locker = lockerToOpen;
        lockerToOpen = null;
        locker.PullPlayerOut();

        attackDirection = DirectionToPlayer();
        Look(attackDirection);
        IDamageable target = playerCollider != null ? Damageable.FromCollider(playerCollider) : player.GetComponent<IDamageable>();
        target?.TakeDamage(new DamageInfo(catchDamage, attackDirection, knockback, gameObject, player.position));
        Enter(State.Recover, recoverTime);
    }

    // --- Moving ---

    // Walks straight toward a point. True once it's there, or once a wall has stopped it (it can't path around one).
    bool WalkTo(Vector2 target, float speed)
    {
        Vector2 offset = target - rb.position;
        if (offset.magnitude <= ArriveDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        Vector2 direction = offset.normalized;
        rb.linearVelocity = direction * speed;
        Look(direction);

        stuckCheckTimer += Time.deltaTime;
        if (stuckCheckTimer < StuckCheckInterval) return false;

        bool stuck = Vector2.Distance(rb.position, stuckCheckPosition) < speed * StuckCheckInterval * 0.25f;
        stuckCheckTimer = 0f;
        stuckCheckPosition = rb.position;
        return stuck;
    }

    // Faces a direction: its view cone points this way, and the sprite flips to match.
    void Look(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        facing = direction.normalized;
        if (body != null && Mathf.Abs(facing.x) > 0.01f)
            body.flipX = facing.x < 0f;
    }

    // --- Attacking and getting hit ---

    void TryHitPlayer()
    {
        Vector2 center = rb.position + attackDirection * hitReach;
        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, hitRadius))
        {
            // Only the player; robots don't hurt each other.
            if (player == null || !col.transform.IsChildOf(player)) continue;

            IDamageable target = Damageable.FromCollider(col);
            if (target == null) continue;

            target.TakeDamage(new DamageInfo(damage, attackDirection, knockback, gameObject, col.ClosestPoint(rb.position)));
            hitLanded = true;
            return;
        }
    }

    void OnDamaged(DamageInfo info)
    {
        // Getting hit cancels whatever it was doing, and gives away where the player is.
        detection = 1f;
        if (player != null && !Locker.IsPlayerHidden) Remember();

        Enter(State.Stunned, hitStunTime);
        ResetBody();
        squash = 1f;
    }

    void OnDied()
    {
        rb.linearVelocity = Vector2.zero;
        ResetBody();
    }

    void ShowWindup(float progress)
    {
        if (body == null) return;
        body.transform.localPosition = bodyBasePosition + (Vector3)(Random.insideUnitCircle * WindupShake * progress);
        body.color = Color.Lerp(bodyBaseColor, windupColor, progress);
    }

    void ResetBody()
    {
        if (body == null) return;
        body.transform.localPosition = bodyBasePosition;
        body.color = bodyBaseColor;
    }

    // Squash wide and short when hit, then spring back.
    void UpdateSquash()
    {
        if (body == null || squash <= 0f) return;
        squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime / SquashRecoverTime);
        body.transform.localScale = new Vector3(1f + 0.3f * squash, 1f - 0.25f * squash, 1f);
    }

    float DistanceToPlayer()
    {
        return Vector2.Distance(rb.position, player.position);
    }

    Vector2 DirectionToPlayer()
    {
        Vector2 toPlayer = (Vector2)player.position - rb.position;
        return toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : attackDirection;
    }

    // Select the robot to see its patrol route (cyan), view cone and close range (white), how near it must be to a
    // locker to catch the player climbing in (magenta), attack range (yellow), and where the lunge hurts (red).
    void OnDrawGizmosSelected()
    {
        Vector2 origin = Application.isPlaying ? home : (Vector2)transform.position;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < patrolRoute.Length; i++)
        {
            Vector2 from = origin + patrolRoute[i];
            Vector2 to = origin + patrolRoute[(i + 1) % patrolRoute.Length];
            Gizmos.DrawWireSphere(from, 0.15f);
            Gizmos.DrawLine(from, to);
        }

        Vector3 center = transform.position;
        Vector3 look = Application.isPlaying ? facing : Vector2.right;
        Gizmos.color = Color.white;
        Gizmos.DrawLine(center, center + Quaternion.Euler(0f, 0f, fieldOfView * 0.5f) * look * sightRange);
        Gizmos.DrawLine(center, center + Quaternion.Euler(0f, 0f, -fieldOfView * 0.5f) * look * sightRange);
        Gizmos.DrawWireSphere(center, closeRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, catchRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center + (Vector3)(attackDirection * hitReach), hitRadius);
    }
}
