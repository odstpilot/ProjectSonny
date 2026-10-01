using System.Collections.Generic;
using UnityEngine;

// A stand-in enemy for testing combat and stealth. It patrols between points. When it spots the player (in front of
// it, in range, nothing solid in between) a "!" pops over its head and, after a moment, it gives chase: winding up
// (shaking and turning orange), lunging, and hurting the player if they're still in front of it. Hitting it stuns it.
// If it loses sight of the player it carries on to where it last saw them and a little past, looks around under a
// "?", and goes back to its patrol. Hiding in a Locker mid-chase counts as losing sight, so it walks right past the
// locker, unless it was close enough to see the player climb in: then it pulls them out.
// No pathfinding: it walks in straight lines and gives up on a point when a wall stops it. Given a Roam Area instead of a
// route, it roams the room's floor grid (CrewWalkArea) instead: somewhere new each time, at a different speed each
// time, changing its mind partway there, and snapping its eye about whenever it stops, so it can't be timed. Roaming
// robots keep out of each other's way: none heads for where another is or is going, and they steer round each other.
// With Smart Senses it's harder to sneak past: it catches movement out of the corner of its eye and hears footsteps
// close behind it (crouching beats both), goes to look at anything it half saw, calls the others in the room over once
// it's sure, and when it loses the player it heads the way they were running rather than to where they were.
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
    [Tooltip("Which way it looks while standing still on its patrol, like a guard watching a doorway. Zero keeps looking whichever way it last walked.")]
    public Vector2 idleFacing;

    [Header("Roaming")]
    [Tooltip("Roams this room's floor instead of walking the patrol route. Empty walks the route.")]
    public CrewWalkArea roamArea;
    [Tooltip("Speed for each leg of its roaming, picked at random from and to.")]
    public Vector2 roamSpeed = new Vector2(2.4f, 4.4f);
    [Tooltip("Seconds it stops at the end of each leg, from and to. It looks about while it's stopped.")]
    public Vector2 roamPause = new Vector2(0f, 0.8f);
    [Tooltip("Seconds between changes of mind partway along a leg, from and to: it drops where it was going and heads somewhere else.")]
    public Vector2 roamSwerve = new Vector2(1.2f, 3.5f);
    [Tooltip("Seconds between snaps of its eye to a new direction while it's stopped, from and to.")]
    public Vector2 roamGlance = new Vector2(0.2f, 0.6f);
    [Tooltip("Keeps this far from the other roaming robots: it steers round them, and turns off somewhere else rather than run into one.")]
    public float personalSpace = 1.4f;

    [Header("Senses")]
    [Tooltip("Can't see or hear the player at all, for a level holding it off until the player's had a look at it.")]
    public bool blind;
    [Tooltip("A glow from its eye, this far, so it can be seen in the dark. 0 for none.")]
    public float eyeGlowRadius;
    public Color eyeGlowColor = new Color(1f, 0.2f, 0.15f);
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
    [Tooltip("A crouching player can only be seen from this fraction of Sight Range. Close Range still notices them.")]
    [Range(0.1f, 1f)] public float crouchSightMultiplier = 0.6f;
    [Tooltip("How fast the ring fills while the player crouches, as a fraction of normal.")]
    [Range(0.05f, 1f)] public float crouchDetectionMultiplier = 0.45f;
    [Tooltip("A sprinting player can be seen this many times further than Sight Range.")]
    [Range(1f, 3f)] public float sprintSightMultiplier = 1.3f;
    [Tooltip("How fast the ring fills while the player sprints, as a multiple of normal.")]
    [Range(1f, 5f)] public float sprintDetectionMultiplier = 1.8f;
    [Tooltip("It hears a sprinting player this close whichever way it's facing, as long as nothing solid is in between.")]
    public float sprintHearingRange = 3.5f;
    [Tooltip("Seconds it looks around under the ? before going back to its patrol.")]
    public float searchTime = 2.5f;
    [Tooltip("After losing the player, it carries on this far past where it last saw them before searching.")]
    public float searchOvershoot = 1.5f;

    [Header("Smart Senses")]
    [Tooltip("Turns on everything below: seeing out of the corner of its eye, hearing footsteps, going to look at a glimpse, calling the others over, and guessing where the player ran.")]
    public bool smartSenses;
    [Tooltip("The corner of its eye: this wide, in degrees, out to this fraction of Sight Range, filling the ring this much slower.")]
    public float peripheralFieldOfView = 200f;
    [Range(0.1f, 1f)] public float peripheralRange = 0.55f;
    [Range(0.05f, 1f)] public float peripheralRate = 0.35f;
    [Tooltip("It hears a walking (not crouching) player this close whichever way it's facing, filling the ring this much slower.")]
    public float walkHearingRange = 2.2f;
    [Range(0.05f, 1f)] public float hearingRate = 0.5f;
    [Tooltip("Half sure, it stops what it's doing and goes to look where it glimpsed the player, at its roaming speed.")]
    [Range(0.1f, 0.95f)] public float suspicionThreshold = 0.4f;
    [Tooltip("Once it's sure, other robots in the same room this close come to where it saw the player.")]
    public float alertRange = 9f;
    [Tooltip("Losing the player, it heads this far along the way they were going, rather than to where they were.")]
    public float predictDistance = 3f;

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

    enum State { Patrol, Chase, Investigate, Search, Catch, Windup, Lunge, Recover, Stunned, Shorted }
    static readonly Color ShortedColor = new Color(0.4f, 0.85f, 1f);

    private Health health;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private Vector3 bodyBasePosition;
    private Color bodyBaseColor;
    private Transform player;
    private Collider2D playerCollider;
    private Health playerHealth;
    private PlayerController playerController;
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
    private readonly List<Vector2> roamPath = new List<Vector2>();
    private static readonly List<PlaceholderRobot> all = new List<PlaceholderRobot>();
    private Vector2 roamGoal;           // where it's roaming to, so the others don't pick the same spot
    private float investigateSpeed;
    private Vector2 glimpse;            // where it last saw or heard the player, however briefly
    private float glimpseTime = -1f;
    private Vector2 playerHeading;      // which way they were going when it last saw them
    private Vector2 playerVelocity;
    private Vector2 previousPlayerPosition;
    private float roamSpeedNow;
    private float nextDodge;
    private UnityEngine.Rendering.Universal.Light2D eyeGlow;
    private float nextSwerve;
    private float nextGlance;

    // 0 to 1: how sure it is of what it's looking at. At 1 it gives chase. Drawn as the ring around the player.
    private float detection;

    // After the player: chasing, going for them in a locker, or attacking.
    public bool IsHunting => state == State.Chase || state == State.Catch || state == State.Windup
        || state == State.Lunge || state == State.Recover;
    // The last point of its patrol, where it ends up standing if the route is a single point.
    public Vector2 PatrolEnd => home + (patrolRoute.Length > 0 ? patrolRoute[patrolRoute.Length - 1] : Vector2.zero);

    void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponentInChildren<SpriteRenderer>();
        DepthSort.Group(gameObject);
        if (body != null)
        {
            bodyBasePosition = body.transform.localPosition;
            bodyBaseColor = body.color;
        }
        home = transform.position;
        roamGoal = home;
        investigateSpeed = moveSpeed;
        if (eyeGlowRadius > 0f)
        {
            eyeGlow = new GameObject("Eye Glow").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            eyeGlow.transform.SetParent(transform, false);
            eyeGlow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            eyeGlow.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            eyeGlow.pointLightInnerRadius = 0f;
            eyeGlow.pointLightOuterRadius = eyeGlowRadius;
            eyeGlow.falloffIntensity = 0.75f;
        }
    }

    // The eye glow: red, brighter when it's after the player, blue while it's shorted out, out when it's done for.
    void UpdateEyeGlow()
    {
        if (eyeGlow == null) return;
        eyeGlow.enabled = !health.IsDead;
        eyeGlow.color = state == State.Shorted ? ShortedColor : eyeGlowColor;
        eyeGlow.intensity = state == State.Shorted ? Random.Range(0.1f, 0.6f) : IsHunting ? 1f : 0.55f;
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        all.Add(this);
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        all.Remove(this);
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
            playerController = found.GetComponent<PlayerController>();
        }

        stuckCheckPosition = rb.position;
    }

    void Update()
    {
        UpdateSquash();
        UpdateEyeGlow();

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
                if (SpotPlayer() || Suspect()) break;
                if (roamArea != null)
                {
                    Roam();
                }
                else if (patrolRoute.Length == 0 || stateTimer > 0f)
                {
                    rb.linearVelocity = Vector2.zero; // standing guard, or pausing at a point
                    if (idleFacing != Vector2.zero) Look(idleFacing);
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
                if (WalkTo(investigatePoint, investigateSpeed)) StartSearch();
                break;

            case State.Search:
                if (SpotPlayer() || Suspect()) break;
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

            case State.Shorted:
                // Dead still, blue, spitting sparks; then it comes to, not knowing where the player went.
                rb.linearVelocity = Vector2.zero;
                if (body != null) body.color = Color.Lerp(bodyBaseColor, ShortedColor, 0.6f + 0.4f * Mathf.PingPong(Time.time * 6f, 1f));
                if (Random.value < Time.deltaTime * 5f)
                    HitEffects.Sparks(rb.position + Random.insideUnitCircle * 0.3f, Vector2.up, 3, 2.5f, 360f, Color.white, ShortedColor);
                if (stateTimer <= 0f)
                {
                    ResetBody();
                    StartSearch();
                }
                break;
        }
    }

    // An EMP (EmpPulse) went through it: shorted out for this long, blind and still, having forgotten the player.
    public void Emp(float seconds)
    {
        if (health.IsDead) return;
        lockerToOpen = null;
        detection = 0f;
        glimpseTime = -1f;
        ResetBody();
        Enter(State.Shorted, seconds);
        StealthMeter.Clear(this);
    }

    public bool IsShorted => state == State.Shorted;

    // Back where it started, calm, at the beginning of its patrol, as if it had never seen the player. For a level
    // putting a stealth section back to how it began.
    public void ResetToStart()
    {
        lockerToOpen = null;
        detection = 0f;
        patrolIndex = 0;
        roamPath.Clear();
        ResetBody();
        rb.position = home;
        transform.position = new Vector3(home.x, home.y, transform.position.z);
        Enter(State.Patrol, 0f);
        StealthMeter.Clear(this);
    }

    // A camera (StationCamera) saw the player there: unless it's already after them, it hurries over to look.
    public void Alarm(Vector2 at)
    {
        if (health.IsDead || blind || IsHunting || state == State.Stunned || state == State.Shorted) return;
        investigatePoint = at;
        investigateSpeed = moveSpeed;
        Look(at - rb.position);
        Enter(State.Investigate, 0f);
    }

    // Calm again where it is, back on its rounds, as if it had never seen the player. For the player coming back after
    // dying, somewhere else.
    public void CalmDown()
    {
        if (health.IsDead) return;
        lockerToOpen = null;
        detection = 0f;
        glimpseTime = -1f;
        ResetBody();
        Enter(State.Patrol, 0f);
        StealthMeter.Clear(this);
    }

    void Enter(State next, float duration)
    {
        if (state == State.Windup) ResetBody();
        // Off its rounds, it'll pick somewhere new once it's back on them.
        if (next != State.Patrol) roamPath.Clear();
        state = next;
        stateTimer = duration;
        stuckCheckTimer = 0f;
        stuckCheckPosition = rb.position;
        if (next != State.Lunge) rb.linearVelocity = Vector2.zero;
    }

    // --- Senses ---

    // tracking: already after the player, so it keeps them in view whichever way it's facing.
    bool CanSeePlayer(bool tracking = false) => Perceive(tracking) > 0f;

    // How well it can make the player out: 1 in plain view, less out of the corner of its eye or only heard (Smart
    // Senses), 0 not at all.
    float Perceive(bool tracking)
    {
        if (blind || state == State.Shorted || player == null || Locker.IsPlayerHidden) return 0f;
        if (playerHealth != null && playerHealth.IsDead) return 0f;

        Vector2 toPlayer = (Vector2)player.position - rb.position;
        float distance = toPlayer.magnitude;
        // Crouching keeps the player out of sight at a distance. Sprinting carries further, and it hears the footsteps
        // coming up behind it. Once it's hunting them it keeps them in view either way.
        // In the dark (DarkRooms) it sees less far.
        float sight = sightRange * DarkRooms.SightScale;
        float range = sight;
        float allAround = closeRange;
        if (!tracking && PlayerCrouching)
        {
            range = Mathf.Max(closeRange, sight * crouchSightMultiplier);
        }
        else if (!tracking && PlayerSprinting)
        {
            range = sight * sprintSightMultiplier;
            allAround = Mathf.Max(closeRange, sprintHearingRange);
        }

        float angle = Vector2.Angle(facing, toPlayer);
        float seen = 0f;
        if (distance <= range && (tracking || distance <= allAround || angle <= fieldOfView * 0.5f))
        {
            seen = 1f;
        }
        else if (smartSenses && !tracking && !PlayerCrouching)
        {
            // Out of the corner of its eye, or the sound of their footsteps behind it. Crouching gets past both.
            if (distance <= sight * peripheralRange && angle <= peripheralFieldOfView * 0.5f) seen = peripheralRate;
            else if (distance <= walkHearingRange && PlayerMoving) seen = hearingRate;
        }
        return seen > 0f && HasLineOfSight(player.position) ? seen : 0f;
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
        bool hunting = IsHunting;
        TrackPlayer();

        float seen = Perceive(hunting);
        if (seen > 0f)
        {
            float nearness = 1f - Mathf.Clamp01(Mathf.InverseLerp(closeRange, sightRange, DistanceToPlayer()));
            float rate = Mathf.Lerp(1f, closeDetectionMultiplier, nearness) / Mathf.Max(0.05f, detectionTime);
            if (!hunting && PlayerCrouching) rate *= crouchDetectionMultiplier;
            else if (!hunting && PlayerSprinting) rate *= sprintDetectionMultiplier;
            if (!hunting) rate *= seen;
            detection = Mathf.Clamp01(detection + rate * Time.deltaTime);
            glimpse = player.position;
            glimpseTime = Time.time;
            if (playerVelocity.sqrMagnitude > 0.25f) playerHeading = playerVelocity.normalized;
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
        Alert();
        return true;
    }

    // Smart Senses: half sure of something it's making out right now, it drops what it's doing and goes to look,
    // turning to face it first. True if it did.
    bool Suspect()
    {
        if (!smartSenses || player == null || detection < suspicionThreshold || Time.time - glimpseTime > 0.2f) return false;
        investigatePoint = glimpse;
        investigateSpeed = roamArea != null ? roamSpeed.x : patrolSpeed;
        Look(glimpse - rb.position);
        Enter(State.Investigate, 0f);
        return true;
    }

    // Smart Senses: sure of the player, it calls the other robots roaming the same room over to where it saw them.
    void Alert()
    {
        if (!smartSenses) return;
        foreach (PlaceholderRobot other in all)
        {
            if (other == this || !other.smartSenses || other.blind || other.IsHunting || other.health.IsDead) continue;
            if (other.roamArea != roamArea || Vector2.Distance(other.rb.position, rb.position) > alertRange) continue;
            other.investigatePoint = lastSeenPosition;
            other.investigateSpeed = other.moveSpeed;
            other.Look(lastSeenPosition - other.rb.position);
            other.Enter(State.Investigate, 0f);
        }
    }

    // How fast the player's moving, and which way, from where they were last frame.
    void TrackPlayer()
    {
        if (player == null || Time.deltaTime <= 0f) return;
        Vector2 now = player.position;
        playerVelocity = (now - previousPlayerPosition) / Time.deltaTime;
        previousPlayerPosition = now;
    }

    bool PlayerMoving => playerVelocity.sqrMagnitude > 0.25f;
    bool PlayerCrouching => playerController != null && playerController.IsCrouching;
    bool PlayerSprinting => playerController != null && playerController.IsSprinting;

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

    // Heads for where it last saw the player, carrying on a little past in the direction it was going. With Smart Senses
    // it goes the way the player was heading instead, further, to cut them off.
    void LoseTrack()
    {
        Vector2 toLastSeen = lastSeenPosition - rb.position;
        Vector2 onward = toLastSeen.sqrMagnitude > 0.0001f ? toLastSeen.normalized : facing;
        investigatePoint = smartSenses && playerHeading != Vector2.zero
            ? lastSeenPosition + playerHeading * predictDistance
            : lastSeenPosition + onward * searchOvershoot;
        investigateSpeed = moveSpeed;
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

    // --- Roaming ---

    // Along the leg it's on, or stopped between legs looking about. Partway along, now and then, it changes its mind.
    void Roam()
    {
        if (stateTimer > 0f)
        {
            rb.linearVelocity = Vector2.zero;
            if (Time.time >= nextGlance)
            {
                Look(Random.insideUnitCircle.normalized);
                nextGlance = Time.time + Random.Range(roamGlance.x, roamGlance.y);
            }
            return;
        }
        // About to run into another one: turn off somewhere else instead.
        bool blocked = roamPath.Count > 0 && Time.time >= nextDodge && RobotAhead((roamPath[0] - rb.position).normalized);
        if (blocked) nextDodge = Time.time + 0.4f;
        if (roamPath.Count == 0 || Time.time >= nextSwerve || blocked)
        {
            if (!PlanRoam())
            {
                Enter(State.Patrol, roamPause.y);
                return;
            }
        }
        // WalkTo also gives up on a point when something's in the way: the next one's somewhere else.
        if (WalkTo(roamPath[0], roamSpeedNow))
        {
            roamPath.RemoveAt(0);
            if (roamPath.Count == 0) Enter(State.Patrol, Random.Range(roamPause.x, roamPause.y));
        }
    }

    // Somewhere else in the room, clear of where the others are and are headed, the way there over the floor grid, and
    // how fast to go.
    bool PlanRoam()
    {
        nextSwerve = Time.time + Random.Range(roamSwerve.x, roamSwerve.y);
        Vector2Int from = roamArea.CellAt(rb.position);
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (!roamArea.TryPickDestination(from, 3f, out Vector2Int to)) break;
            Vector2 goal = roamArea.CenterOf(to);
            if (attempt < 5 && Crowded(goal)) continue;
            if (!roamArea.FindPath(from, to, roamPath) || roamPath.Count == 0) continue;
            // Heading straight into another one: try somewhere else.
            if (attempt < 5 && RobotAhead((roamPath[0] - rb.position).normalized)) continue;
            roamGoal = goal;
            roamSpeedNow = Random.Range(roamSpeed.x, roamSpeed.y);
            return true;
        }
        roamPath.Clear();
        return false;
    }

    // Another roaming robot is at, or on its way to, somewhere this close to the point.
    bool Crowded(Vector2 point)
    {
        foreach (PlaceholderRobot other in all)
        {
            if (other == this || other.roamArea == null) continue;
            if (Vector2.Distance(other.rb.position, point) < personalSpace * 1.5f || Vector2.Distance(other.roamGoal, point) < personalSpace * 1.5f)
                return true;
        }
        return false;
    }

    // Another robot just in front, the way it's going.
    bool RobotAhead(Vector2 direction)
    {
        foreach (PlaceholderRobot other in all)
        {
            if (other == this) continue;
            Vector2 toOther = other.rb.position - rb.position;
            if (toOther.magnitude < personalSpace && Vector2.Angle(direction, toOther) < 50f) return true;
        }
        return false;
    }

    // The way it wants to go, bent away from any robot inside its personal space, so they slip past each other.
    Vector2 Steer(Vector2 direction)
    {
        Vector2 away = Vector2.zero;
        foreach (PlaceholderRobot other in all)
        {
            if (other == this) continue;
            Vector2 fromOther = rb.position - other.rb.position;
            float distance = fromOther.magnitude;
            if (distance >= personalSpace || distance < 0.0001f) continue;
            away += fromOther / distance * (1f - distance / personalSpace);
        }
        if (away == Vector2.zero) return direction;
        Vector2 steered = direction + away * 1.5f;
        return steered.sqrMagnitude > 0.0001f ? steered.normalized : direction;
    }

    // --- Moving ---

    // Walks straight toward a point. True once it's there, or once a wall has stopped it (it can't path around one).
    // Roaming, it steers round the other robots on the way.
    bool WalkTo(Vector2 target, float speed)
    {
        Vector2 offset = target - rb.position;
        if (offset.magnitude <= ArriveDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        Vector2 direction = offset.normalized;
        rb.linearVelocity = (roamArea != null ? Steer(direction) : direction) * speed;
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

    // Select the robot to see its patrol route (cyan), view cone and close range (white), how far it hears a sprinting
    // player (orange), how near it must be to a locker to catch the player climbing in (magenta), attack range
    // (yellow), and where the lunge hurts (red).
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
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(center, sprintHearingRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, catchRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center + (Vector3)(attackDirection * hitReach), hitRadius);
    }
}
