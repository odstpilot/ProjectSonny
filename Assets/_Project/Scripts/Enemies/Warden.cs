using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Warden : MonoBehaviour
{
    public enum WardenState
    {
        Inactive,
        Chasing,
        Investigating,
        Searching,
        MemoryPatrol,
        Attacking,
        Returning,
        Idle
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    public Transform target;
    public Transform spriteTransform;

    [SerializeField]
    private LightManager lightManager;

    // Optional: assign a BoxCollider2D or PolygonCollider2D
    // covering the playable area.
    [SerializeField]
    private Collider2D playableArea;


    // =========================================================
    // STARTING POSITION
    // =========================================================

    [Header("Starting Position")]

    public Vector3 StartingPos;

    [SerializeField]
    private bool captureStartingPositionOnAwake = true;

    [SerializeField]
    public bool startActive = false;

    [SerializeField]
    private bool teleportOnReset = false;


    // =========================================================
    // DETECTION
    // =========================================================

    [Header("Detection")]

    public float detectionDistance = 5f;

    [Range(0f, 360f)]
    public float coneAngle = 90f;

    // Include the Player and walls/obstacles.
    // The Warden's own colliders are ignored in code.
    public LayerMask visionMask = ~0;

    // How frequently the Warden updates its chase path.
    public float chaseRepathInterval = 0.2f;

    // Minimum player movement before recalculating a chase path.
    public float chaseRepathDistance = 0.25f;


    // =========================================================
    // RANDOM SEARCH
    // =========================================================

    [Header("Random Search")]

    public int maxRandomSearches = 4;

    public float randomSearchRadius = 5f;

    public int maxSearchPointAttempts = 12;

    // Maximum acceptable path length relative to search radius.
    // Helps avoid destinations requiring enormous detours.
    public float maxSearchPathMultiplier = 3f;

    // Maximum duration of an individual search destination.
    public float searchTimeout = 10f;

    // Maximum duration of an investigation.
    public float investigationTimeout = 15f;


    // =========================================================
    // MEMORY
    // =========================================================

    [Header("Memory")]

    public float memorySaveInterval = 2f;

    public float minMemoryDistance = 2f;

    public int maxMemoryPoints = 15;

    public float memoryPatrolTimeout = 20f;


    // =========================================================
    // NAVIGATION
    // =========================================================

    [Header("Navigation")]

    // How far a point can be projected onto the NavMesh.
    public float navMeshSnapDistance = 0.75f;

    // Helps avoid selecting NavMesh points on another Z layer.
    public float maxNavMeshZDifference = 1f;

    public float arrivalDistance = 0.2f;

    public float returnTimeout = 20f;


    // =========================================================
    // ATTACK
    // =========================================================

    [Header("Attack")]

    public float attackDuration = 1f;


    // =========================================================
    // SPRITE
    // =========================================================

    [Header("Sprite")]

    // Set false if the original sprite faces left at positive X scale.
    public bool spriteFacesRightAtPositiveScale = false;


    // =========================================================
    // RUNTIME STATE
    // =========================================================


    public WardenState CurrentState { get; private set; }
        = WardenState.Inactive;

    public bool active { get; private set; }

    public bool playerInSight { get; private set; }

    private NavMeshAgent agent;

    private Vector3 lastKnownPosition;

    private Vector3 currentDestination;

    private Vector3 lastChaseDestination;

    private readonly List<Vector3> memoryPoints
        = new List<Vector3>();

    private int randomSearchesRemaining;

    private int memoryIndex;

    private bool facingRight = true;

    private bool hasDestination;

    private bool alertTriggered;

    private float destinationAssignedTime;

    private float nextChaseRepathTime;

    private float nextMemorySaveTime;

    private float attackEndsAt;

    [Header("Debug")]
    public bool debugWarden = true;

    private void WardenLog(string message)
    {
        if (debugWarden)
        {
            Debug.Log(
                $"[WARDEN {gameObject.name}] " +
                $"State={CurrentState} | Active={active} | {message}",
                this
            );
        }
    }

    // =========================================================
    // CACHED OBJECTS
    // =========================================================

    private NavMeshPath calculatedPath;

    private ContactFilter2D visionFilter;

    // Reuse this array instead of allocating RaycastAll results
    // every frame.
    private readonly RaycastHit2D[] visionHits
        = new RaycastHit2D[64];


    // =========================================================
    // INITIALIZATION
    // =========================================================
    private void Start()
    {
        agent.updateUpAxis = false;
        agent.updateRotation = false;
        agent.baseOffset = 0f;

        if (!EnsureOnNavMesh())
        {
            Debug.LogError(
                $"Warden {name} started without a valid NavMesh."
            );
        }

        if (startActive)
        {
            ActivateRobot();
        }
    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.updateUpAxis = false;
        agent.updateRotation = false;

        calculatedPath = new NavMeshPath();

        if (captureStartingPositionOnAwake)
        {
            StartingPos = transform.position;
        }

        if (lightManager == null)
        {
            GameObject lightObject =
                GameObject.Find("Global Light 2D");

            if (lightObject != null)
            {
                lightManager =
                    lightObject.GetComponent<LightManager>();
            }
        }

        visionFilter = new ContactFilter2D();

        visionFilter.useLayerMask = true;
        visionFilter.layerMask = visionMask;

        visionFilter.useTriggers = true;

        active = false;

        CurrentState = WardenState.Inactive;

        if (target == null)
        {
            Debug.LogError(
                "Warden: Player target is not assigned.",
                this
            );

            enabled = false;
            return;
        }

        if (spriteTransform == null)
        {
            Debug.LogWarning(
                "Warden: Sprite Transform is not assigned.",
                this
            );
        }

        Flip(facingRight);
    }



    public void ActivitedByCam()
    {
        Debug.Log(
            $"[WARDEN {name}] Activated by security camera."
        );

        if (!EnsureOnNavMesh())
        {
            Debug.LogError(
                $"[WARDEN {name}] Camera activated Warden, " +
                $"but Warden could not attach to NavMesh."
            );

            return;
        }

        active = true;
    }


    // =========================================================
    // MAIN UPDATE
    // =========================================================

    private void Update()
    {
        // Reset can leave the agent returning to its spawn
        // while the AI itself is inactive.
        if (!active)
        {
            if (CurrentState == WardenState.Returning)
            {
                UpdateReturning();
            }

            UpdateFacingDirection();

            return;
        }

        if (!agent.isOnNavMesh)
        {
            EnterIdle();
            return;
        }

        // Attacking takes priority over normal detection
        // and navigation.
        if (CurrentState == WardenState.Attacking)
        {
            UpdateAttacking();
            UpdateFacingDirection();

            return;
        }

        playerInSight = CheckForPlayer();

        // -----------------------------------------------------
        // PLAYER VISIBLE
        // -----------------------------------------------------

        if (playerInSight)
        {
            if (CurrentState != WardenState.Chasing)
            {
                EnterChase();
            }

            UpdateChase();
        }

        // -----------------------------------------------------
        // PLAYER NOT VISIBLE
        // -----------------------------------------------------

        else
        {
            // The player has just escaped detection.
            if (CurrentState == WardenState.Chasing)
            {
                BeginInvestigation();
            }

            switch (CurrentState)
            {
                case WardenState.Investigating:

                    UpdateInvestigation();
                    break;

                case WardenState.Searching:

                    UpdateSearching();
                    break;

                case WardenState.MemoryPatrol:

                    UpdateMemoryPatrol();
                    break;

                case WardenState.Idle:

                    // Remain idle until the player is detected
                    // or an external event supplies a target.
                    break;
            }
        }
     

        UpdateFacingDirection();
    }


    // =========================================================
    // PLAYER DETECTION
    // =========================================================

    private bool CheckForPlayer()
    {
        Debug.Log("check 1");
        if (target == null)
        {
            return false;
        }

        Vector2 origin = transform.position;

        Vector2 toPlayer =
            (Vector2)target.position - origin;

        float distance = toPlayer.magnitude;

        // Player is outside detection range.
        if (distance > detectionDistance)
        {
            return false;
        }

        // Handle the player being at the same position.
        if (distance <= 0.001f)
        {
            lastKnownPosition = target.position;
            return true;
        }

        Vector2 directionToPlayer =
            toPlayer / distance;

        // Vision is based on the Warden's actual
        // facing direction, not the player's position.
        Vector2 facingDirection =
            facingRight
                ? Vector2.right
                : Vector2.left;

        float angle = Vector2.Angle(
            facingDirection,
            directionToPlayer
        );

        if (angle > coneAngle * 0.5f)
        {
            return false;
        }

        // Cast toward the player to check line of sight.
        //
        // Multiple hits allow us to ignore the Warden's
        // own colliders without ignoring actual obstacles.

        int hitCount = Physics2D.Raycast(
            origin,
            directionToPlayer,
            visionFilter,
            visionHits,
            distance
        );

        Collider2D closestCollider = null;

        float closestDistance =
            float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider =
                visionHits[i].collider;

            if (hitCollider == null)
            {
                continue;
            }

            // Ignore this Warden's own colliders,
            // including colliders on child objects.
            if (hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (visionHits[i].distance < closestDistance)
            {
                closestDistance =
                    visionHits[i].distance;

                closestCollider = hitCollider;
            }
        }

        if (closestCollider == null)
        {
            return false;
        }
        Debug.Log("check 2");
        // Support a collider on the Player object
        // or on one of its children.
        bool hitPlayer =
            closestCollider.transform == target ||
            closestCollider.transform.IsChildOf(target);

        if (!hitPlayer)
        {
            return false;
        }

        lastKnownPosition = target.position;

        return true;
    }


    // =========================================================
    // CHASING
    // =========================================================

    private void EnterChase()
    {
        CurrentState = WardenState.Chasing;

        randomSearchesRemaining =
            Mathf.Max(0, maxRandomSearches);

        lastKnownPosition = target.position;

        // Force a destination update when chasing starts.
        nextChaseRepathTime = 0f;

        TriggerAlert();
    }


    private void UpdateChase()
    {
        lastKnownPosition = target.position;

        TrySaveMemoryPoint();

        if (Time.time < nextChaseRepathTime)
        {
            return;
        }

        nextChaseRepathTime =
            Time.time + Mathf.Max(0.02f, chaseRepathInterval);

        // Do not recalculate an unchanged destination.
        if (hasDestination &&
            agent.hasPath &&
            Vector2.Distance(
                lastChaseDestination,
                target.position
            ) < chaseRepathDistance)
        {
            return;
        }

        if (TrySetDestination(
            target.position,
            navMeshSnapDistance,
            out Vector3 destination))
        {
            lastChaseDestination = destination;
        }
    }


    // =========================================================
    // INVESTIGATING
    // =========================================================

    private void BeginInvestigation()
    {
        CurrentState = WardenState.Investigating;

        WardenLog(
            $"BEGIN INVESTIGATION. " +
            $"Trying to navigate to lastKnownPosition {lastKnownPosition}. " +
            $"Agent isOnNavMesh = {agent.isOnNavMesh}"
        );

        if (!agent.isOnNavMesh)
        {
            WardenLog(
                "ERROR: NavMeshAgent is NOT on a NavMesh. " +
                "Cannot navigate."
            );

            return;
        }

        if (!TrySetDestination(
            lastKnownPosition,
            navMeshSnapDistance,
            out Vector3 destination))
        {
            WardenLog(
                $"FAILED to set investigation destination. " +
                $"Requested = {lastKnownPosition}"
            );

            BeginSearch();
            return;
        }

        WardenLog(
            $"Investigation destination successfully assigned: {destination}"
        );
    }


    private float nextDebugMovementLog;

    private void UpdateInvestigation()
    {
        if (Time.time >= nextDebugMovementLog)
        {
            nextDebugMovementLog = Time.time + 1f;

            WardenLog(
                $"INVESTIGATION MOVEMENT | " +
                $"Position={transform.position} | " +
                $"Destination={currentDestination} | " +
                $"Velocity={agent.velocity} | " +
                $"SpeedSetting={agent.speed} | " +
                $"isStopped={agent.isStopped} | " +
                $"hasPath={agent.hasPath} | " +
                $"pathPending={agent.pathPending} | " +
                $"pathStatus={agent.pathStatus} | " +
                $"remainingDistance={agent.remainingDistance}"
            );
        }

        if (HasReachedDestination())
        {
            WardenLog(
                "Reached investigation destination. Beginning random search."
            );

            BeginSearch();
            return;
        }

        if (HasMovementFailed(investigationTimeout))
        {
            WardenLog(
                "Investigation movement considered FAILED/TIMED OUT. " +
                "Beginning random search."
            );

            BeginSearch();
        }
    }


    // =========================================================
    // RANDOM SEARCH
    // =========================================================

    private void BeginSearch()
    {
        CurrentState = WardenState.Searching;

        BeginNextRandomSearch();
    }


    private void UpdateSearching()
    {
        if (HasReachedDestination())
        {
            BeginNextRandomSearch();
            return;
        }

        if (HasMovementFailed(searchTimeout))
        {
            // Skip the failed or timed-out destination.
            BeginNextRandomSearch();
        }
    }


    private void BeginNextRandomSearch()
    {
        if (randomSearchesRemaining <= 0)
        {
            EnterMemoryPatrol();
            return;
        }

        if (!TrySearchRandomlyNearLastKnown())
        {
            // No valid search points could be generated.
            // Do not get stuck attempting an invalid location.
            EnterMemoryPatrol();
        }
    }


    private bool TrySearchRandomlyNearLastKnown()
    {
        if (!agent.isOnNavMesh)
        {
            return false;
        }

        float radius =
            Mathf.Max(0f, randomSearchRadius);

        float maxPathLength =
            Mathf.Max(
                1f,
                radius * maxSearchPathMultiplier
            );

        for (
            int i = 0;
            i < maxSearchPointAttempts;
            i++
        )
        {
            Vector2 randomOffset =
                Random.insideUnitCircle * radius;

            Vector3 candidate =
                lastKnownPosition +
                new Vector3(
                    randomOffset.x,
                    randomOffset.y,
                    0f
                );

            if (!TrySetDestination(
                candidate,
                navMeshSnapDistance,
                out Vector3 validPoint,
                maxPathLength))
            {
                continue;
            }

            // Reject sampled points outside the
            // intended search area.
            if (Vector2.Distance(
                validPoint,
                lastKnownPosition
            ) > radius + navMeshSnapDistance)
            {
                continue;
            }

            // Avoid selecting the current location.
            if (Vector2.Distance(
                validPoint,
                transform.position
            ) <= GetArrivalDistance())
            {
                continue;
            }

            // A valid search has been started.
            randomSearchesRemaining--;

            return true;
        }

        return false;
    }


    // =========================================================
    // MEMORY SYSTEM
    // =========================================================

    private void TrySaveMemoryPoint()
    {
        if (Time.time < nextMemorySaveTime)
        {
            return;
        }

        nextMemorySaveTime =
            Time.time + Mathf.Max(0.1f, memorySaveInterval);

        if (maxMemoryPoints <= 0)
        {
            return;
        }

        if (!NavMesh.SamplePosition(
            lastKnownPosition,
            out NavMeshHit hit,
            navMeshSnapDistance,
            agent.areaMask))
        {
            return;
        }

        Vector3 newPoint = hit.position;

        if (Mathf.Abs(
            newPoint.z - transform.position.z
        ) > maxNavMeshZDifference)
        {
            return;
        }

       

        // Prevent duplicate or nearly identical memories.
        foreach (Vector3 point in memoryPoints)
        {
            if (Vector2.Distance(
                point,
                newPoint
            ) < minMemoryDistance)
            {
                return;
            }
        }

        memoryPoints.Add(newPoint);

        // Discard the oldest memory at capacity.
        while (memoryPoints.Count > maxMemoryPoints)
        {
            memoryPoints.RemoveAt(0);
        }
    }


    // =========================================================
    // MEMORY PATROL
    // =========================================================

    private void EnterMemoryPatrol()
    {
        CurrentState = WardenState.MemoryPatrol;

        // Allow a new alert to trigger when the player
        // is detected during a future patrol.
        alertTriggered = false;

        // Start with the most recently stored memory.
        memoryIndex = memoryPoints.Count;

        if (!BeginNextMemoryPoint())
        {
            EnterIdle();
        }
    }


    private void UpdateMemoryPatrol()
    {
        if (HasReachedDestination())
        {
            if (!BeginNextMemoryPoint())
            {
                EnterIdle();
            }

            return;
        }

        if (HasMovementFailed(memoryPatrolTimeout))
        {
            if (!BeginNextMemoryPoint())
            {
                EnterIdle();
            }
        }
    }


    private bool BeginNextMemoryPoint()
    {
        if (memoryPoints.Count == 0)
        {
            return false;
        }

        // Try each memory at most once when choosing
        // the next destination.
        for (
            int attempt = 0;
            attempt < memoryPoints.Count;
            attempt++
        )
        {
            memoryIndex--;

            if (memoryIndex < 0)
            {
                memoryIndex =
                    memoryPoints.Count - 1;
            }

            Vector3 point =
                memoryPoints[memoryIndex];

            // Don't repeatedly assign a location
            // where the Warden is already standing.
            if (Vector2.Distance(
                transform.position,
                point
            ) <= GetArrivalDistance())
            {
                continue;
            }

            if (TrySetDestination(
                point,
                navMeshSnapDistance,
                out _))
            {
                return true;
            }
        }

        // No usable or distinct destinations remain.
        return false;
    }


    // =========================================================
    // NAVIGATION HELPERS
    // =========================================================

    private bool TrySetDestination(
    Vector3 requestedPosition,
    float snapDistance,
    out Vector3 validPosition,
    float maxPathLength = -1f)
    {
        validPosition = requestedPosition;

        if (!EnsureOnNavMesh())
        {
            Debug.LogWarning(
                $"[WARDEN {name}] Destination failed: agent not on NavMesh."
            );

            return false;
        }

        // Find the closest valid NavMesh point.
        Vector3 navPosition = requestedPosition;

        // In our 2D XY game, Z is not part of navigation.
        navPosition.z = transform.position.z;

        if (!NavMesh.SamplePosition(
            navPosition,
            out NavMeshHit hit,
            snapDistance,
            agent.areaMask))
        {
            Debug.LogWarning(
                $"[WARDEN {name}] No NavMesh near requested XY position " +
                $"{requestedPosition}. Sampled using {navPosition}"
            );

            return false;
        }

        validPosition = hit.position;

        NavMeshPath path = new NavMeshPath();

        if (!agent.CalculatePath(validPosition, path))
        {
            Debug.LogWarning(
                $"[WARDEN {name}] Could not calculate path to " +
                $"{validPosition}"
            );

            return false;
        }

        if (path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning(
                $"[WARDEN {name}] Path to {validPosition} is " +
                $"{path.status}"
            );

            return false;
        }

        // Optional random-search detour restriction.
        if (maxPathLength >= 0f)
        {
            float pathLength = 0f;

            for (int i = 1; i < path.corners.Length; i++)
            {
                pathLength += Vector2.Distance(
                    path.corners[i - 1],
                    path.corners[i]
                );
            }

            if (pathLength > maxPathLength)
            {
                return false;
            }
        }

        agent.isStopped = false;

        if (!agent.SetDestination(validPosition))
        {
            return false;
        }

        currentDestination = validPosition;
        destinationAssignedTime = Time.time;
        hasDestination = true;

        Debug.Log(
            $"[WARDEN {name}] Navigating to {validPosition}"
        );

        return true;
    }



    private float GetArrivalDistance()
    {
        return Mathf.Max(
            agent.stoppingDistance,
            arrivalDistance
        );
    }


    private bool HasReachedDestination()
    {
        if (!agent.isOnNavMesh || !hasDestination)
        {
            return false;
        }

        if (agent.pathPending)
        {
            return false;
        }

        float distance = Vector2.Distance(
            transform.position,
            currentDestination
        );

        // Handles destinations at the current position,
        // where the agent may not create a path.
        if (distance <= GetArrivalDistance())
        {
            return true;
        }

        if (!agent.hasPath)
        {
            return false;
        }

        if (agent.pathStatus !=
            NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        return
            agent.remainingDistance <= GetArrivalDistance()
            &&
            agent.velocity.sqrMagnitude <= 0.04f;
    }


    private bool HasMovementFailed(float timeout)
    {
        if (!agent.isOnNavMesh)
        {
            return true;
        }

        if (!hasDestination)
        {
            return true;
        }

        float elapsed =
            Time.time - destinationAssignedTime;

        // Every assigned destination has a time limit.
        if (elapsed > timeout)
        {
            return true;
        }

        // Allow Unity time to calculate the path.
        if (agent.pathPending || elapsed < 0.2f)
        {
            return false;
        }

        if (agent.pathStatus !=
            NavMeshPathStatus.PathComplete)
        {
            return true;
        }

        if (!agent.hasPath &&
            !HasReachedDestination())
        {
            return true;
        }

        return false;
    }


    // =========================================================
    // ACTIVATION
    // =========================================================

    public void ActivateRobot()
    {
        if (target == null)
            return;

        if (!EnsureOnNavMesh())
        {
            Debug.LogError(
                $"[WARDEN {name}] Cannot activate: not on NavMesh."
            );

            return;
        }

        active = true;

        lastKnownPosition = target.position;

        randomSearchesRemaining =
            Mathf.Max(0, maxRandomSearches);

        BeginInvestigation();
    }

    // =========================================================
    // EXTERNAL TARGET NOTIFICATION
    // =========================================================

    public void NewTarget()
    {
        WardenLog("NewTarget() called with NO position argument.");

        if (target == null)
        {
            WardenLog("ERROR: target is NULL.");
            return;
        }

        NewTarget(target.position);
    }

    private bool EnsureOnNavMesh(float searchDistance = 2f)
    {
        if (agent.isOnNavMesh)
            return true;

        Debug.LogWarning(
            $"[WARDEN {name}] Agent is not currently on NavMesh. " +
            $"Trying to find NavMesh near {transform.position}"
        );

        if (NavMesh.SamplePosition(
            transform.position,
            out NavMeshHit hit,
            searchDistance,
            agent.areaMask))
        {
            Debug.Log(
                $"[WARDEN {name}] Found NavMesh at {hit.position}. " +
                $"Snapping Warden onto it."
            );

            if (agent.Warp(hit.position))
            {
                Debug.Log(
                    $"[WARDEN {name}] Successfully attached to NavMesh."
                );

                return true;
            }
        }

        Debug.LogError(
            $"[WARDEN {name}] Could not find NavMesh within " +
            $"{searchDistance} units of {transform.position}."
        );

        return false;
    }

    public void NewTarget(Vector3 reportedPosition)
    {
        Debug.Log(
            $"[WARDEN {name}] New target reported at {reportedPosition}"
        );

        if (CurrentState == WardenState.Attacking)
            return;

        if (!EnsureOnNavMesh())
            return;

        active = true;

        // Only X/Y matter for our 2D navigation.
        reportedPosition.z = transform.position.z;

        lastKnownPosition = reportedPosition;

        randomSearchesRemaining =
            Mathf.Max(0, maxRandomSearches);

        TriggerAlert();

        BeginInvestigation();
    }


    // =========================================================
    // ATTACKING
    // =========================================================

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        if (!active)
        {
            return;
        }

        if (CurrentState == WardenState.Attacking)
        {
            return;
        }

        bool collidedWithPlayer =
            collision.transform == target ||
            collision.transform.IsChildOf(target);

        if (!collidedWithPlayer)
        {
            return;
        }

        BeginAttack();
    }


    private void BeginAttack()
    {
        CurrentState = WardenState.Attacking;

        lastKnownPosition = target.position;

        randomSearchesRemaining =
            Mathf.Max(0, maxRandomSearches);

        attackEndsAt =
            Time.time + Mathf.Max(0f, attackDuration);

        playerInSight = false;

        hasDestination = false;

        agent.ResetPath();

        agent.isStopped = true;

        TriggerAlert();

        // Add your player damage / game-over /
        // attack animation logic here.
    }


    private void UpdateAttacking()
    {
        if (Time.time < attackEndsAt)
        {
            return;
        }

        agent.isStopped = false;

        // Investigate where the player was at contact.
        // On the following frame, normal detection can
        // immediately switch back to chasing.
        BeginInvestigation();
    }


    // =========================================================
    // IDLE
    // =========================================================

    private void EnterIdle()
    {
        CurrentState = WardenState.Idle;

        hasDestination = false;

        playerInSight = false;

        alertTriggered = false;

        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = false;
        }
    }


    // =========================================================
    // RESET
    // =========================================================

    public void ResetWarden()
    {
        active = false;

        playerInSight = false;

        alertTriggered = false;

        hasDestination = false;

        randomSearchesRemaining = 0;

        memoryIndex = 0;

        nextChaseRepathTime = 0f;

        nextMemorySaveTime = 0f;

        attackEndsAt = 0f;

        lastKnownPosition = Vector3.zero;

        lastChaseDestination = Vector3.zero;

        currentDestination = Vector3.zero;

        memoryPoints.Clear();

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }

        if (teleportOnReset)
        {
            TeleportToStart();
            return;
        }

        // Return normally if a complete path exists.
        if (TrySetDestination(
            StartingPos,
            navMeshSnapDistance,
            out _))
        {
            CurrentState = WardenState.Returning;
        }

        // Otherwise use a teleport as a fallback.
        else
        {
            TeleportToStart();
        }
    }


    private void UpdateReturning()
    {
        if (HasReachedDestination())
        {
            FinishReturning();
            return;
        }

        if (HasMovementFailed(returnTimeout))
        {
            TeleportToStart();
        }
    }


    private void FinishReturning()
    {
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        hasDestination = false;

        CurrentState = WardenState.Inactive;
    }


    private void TeleportToStart()
    {
        if (NavMesh.SamplePosition(
            StartingPos,
            out NavMeshHit hit,
            navMeshSnapDistance,
            agent.areaMask))
        {
            if (!agent.Warp(hit.position))
            {
                Debug.LogWarning(
                    "Warden: Could not warp to starting position.",
                    this
                );
            }
        }

        else
        {
            Debug.LogWarning(
                "Warden: Starting position is not on the NavMesh.",
                this
            );
        }

        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = false;
        }

        hasDestination = false;

        CurrentState = WardenState.Inactive;
    }


    // =========================================================
    // LIGHT / ALERT
    // =========================================================

    private void TriggerAlert()
    {
        if (alertTriggered)
        {
            return;
        }

        alertTriggered = true;

        if (lightManager != null)
        {
            lightManager.TriggerLightOff();
        }
    }


    // =========================================================
    // FACING DIRECTION
    // =========================================================

    private void UpdateFacingDirection()
    {
        if (spriteTransform == null)
        {
            return;
        }

        if (!agent.isOnNavMesh)
        {
            return;
        }

        // Use actual velocity rather than steering target.
        // This avoids flipping toward stale path corners.
        float horizontalVelocity =
            agent.velocity.x;

        if (Mathf.Abs(horizontalVelocity) < 0.05f)
        {
            return;
        }

        bool shouldFaceRight =
            horizontalVelocity > 0f;

        if (shouldFaceRight != facingRight)
        {
            Flip(shouldFaceRight);
        }
    }


    private void Flip(bool faceRight)
    {
        facingRight = faceRight;

        if (spriteTransform == null)
        {
            return;
        }

        Vector3 scale =
            spriteTransform.localScale;

        float sign;

        if (spriteFacesRightAtPositiveScale)
        {
            sign = faceRight ? 1f : -1f;
        }

        else
        {
            sign = faceRight ? -1f : 1f;
        }

        scale.x =
            Mathf.Abs(scale.x) * sign;

        spriteTransform.localScale = scale;
    }


    // =========================================================
    // INSPECTOR VALIDATION
    // =========================================================

    private void OnValidate()
    {
        detectionDistance =
            Mathf.Max(0f, detectionDistance);

        chaseRepathInterval =
            Mathf.Max(0.02f, chaseRepathInterval);

        chaseRepathDistance =
            Mathf.Max(0f, chaseRepathDistance);

        maxRandomSearches =
            Mathf.Max(0, maxRandomSearches);

        randomSearchRadius =
            Mathf.Max(0f, randomSearchRadius);

        maxSearchPointAttempts =
            Mathf.Max(1, maxSearchPointAttempts);

        maxSearchPathMultiplier =
            Mathf.Max(1f, maxSearchPathMultiplier);

        searchTimeout =
            Mathf.Max(0.2f, searchTimeout);

        investigationTimeout =
            Mathf.Max(0.2f, investigationTimeout);

        memorySaveInterval =
            Mathf.Max(0.1f, memorySaveInterval);

        minMemoryDistance =
            Mathf.Max(0f, minMemoryDistance);

        maxMemoryPoints =
            Mathf.Max(0, maxMemoryPoints);

        memoryPatrolTimeout =
            Mathf.Max(0.2f, memoryPatrolTimeout);

        navMeshSnapDistance =
            Mathf.Max(0.01f, navMeshSnapDistance);

        maxNavMeshZDifference =
            Mathf.Max(0f, maxNavMeshZDifference);

        arrivalDistance =
            Mathf.Max(0.01f, arrivalDistance);

        returnTimeout =
            Mathf.Max(0.2f, returnTimeout);

        attackDuration =
            Mathf.Max(0f, attackDuration);
    }
}