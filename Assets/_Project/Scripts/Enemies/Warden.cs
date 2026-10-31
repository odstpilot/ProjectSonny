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

    [Header("References")]
    public Transform target;
    public Transform spriteTransform;

    [SerializeField]
    private LightManager lightManager;

    [SerializeField]
    private Collider2D playableArea;

    [Header("Starting Position")]

    public Vector3 StartingPos;

    [SerializeField]
    private bool captureStartingPositionOnAwake = true;

    [SerializeField]
    public bool startActive = false;

    [SerializeField]
    private bool teleportOnReset = false;

    [Header("Detection")]

    public float detectionDistance = 5f;

    [Range(0f, 360f)]
    public float coneAngle = 90f;

    public LayerMask visionMask = ~0;

    public float chaseRepathInterval = 0.2f;

    public float chaseRepathDistance = 0.25f;

    [Header("Random Search")]

    public int maxRandomSearches = 4;

    public float randomSearchRadius = 5f;

    public int maxSearchPointAttempts = 12;

    public float maxSearchPathMultiplier = 3f;

    public float searchTimeout = 10f;

    public float investigationTimeout = 15f;

    [Header("Memory")]

    public float memorySaveInterval = 2f;

    public float minMemoryDistance = 2f;

    public int maxMemoryPoints = 15;

    public float memoryPatrolTimeout = 20f;

    [Header("Navigation")]

    public float navMeshSnapDistance = 0.75f;

    public float maxNavMeshZDifference = 1f;

    public float arrivalDistance = 0.2f;

    public float returnTimeout = 20f;

    [Header("Attack")]

    public float attackDuration = 1f;

    [Header("Sprite")]

    public bool spriteFacesRightAtPositiveScale = false;

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

    private NavMeshPath calculatedPath;

    private ContactFilter2D visionFilter;

    private readonly RaycastHit2D[] visionHits
        = new RaycastHit2D[64];

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

    private void Update()
    {
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

        if (CurrentState == WardenState.Attacking)
        {
            UpdateAttacking();
            UpdateFacingDirection();

            return;
        }

        playerInSight = CheckForPlayer();

        if (playerInSight)
        {
            if (CurrentState != WardenState.Chasing)
            {
                EnterChase();
            }

            UpdateChase();
        }

        else
        {
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

                    break;
            }
        }

        UpdateFacingDirection();
    }

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

        if (distance > detectionDistance)
        {
            return false;
        }

        if (distance <= 0.001f)
        {
            lastKnownPosition = target.position;
            return true;
        }

        Vector2 directionToPlayer =
            toPlayer / distance;

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

    private void EnterChase()
    {
        CurrentState = WardenState.Chasing;

        randomSearchesRemaining =
            Mathf.Max(0, maxRandomSearches);

        lastKnownPosition = target.position;

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

            if (Vector2.Distance(
                validPoint,
                lastKnownPosition
            ) > radius + navMeshSnapDistance)
            {
                continue;
            }

            if (Vector2.Distance(
                validPoint,
                transform.position
            ) <= GetArrivalDistance())
            {
                continue;
            }

            randomSearchesRemaining--;

            return true;
        }

        return false;
    }

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

        while (memoryPoints.Count > maxMemoryPoints)
        {
            memoryPoints.RemoveAt(0);
        }
    }

    private void EnterMemoryPatrol()
    {
        CurrentState = WardenState.MemoryPatrol;

        alertTriggered = false;

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

        return false;
    }

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

        Vector3 navPosition = requestedPosition;

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

        if (elapsed > timeout)
        {
            return true;
        }

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

        reportedPosition.z = transform.position.z;

        lastKnownPosition = reportedPosition;

        randomSearchesRemaining =
            Mathf.Max(0, maxRandomSearches);

        TriggerAlert();

        BeginInvestigation();
    }

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
    }

    private void UpdateAttacking()
    {
        if (Time.time < attackEndsAt)
        {
            return;
        }

        agent.isStopped = false;

        BeginInvestigation();
    }

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

        if (TrySetDestination(
            StartingPos,
            navMeshSnapDistance,
            out _))
        {
            CurrentState = WardenState.Returning;
        }

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
