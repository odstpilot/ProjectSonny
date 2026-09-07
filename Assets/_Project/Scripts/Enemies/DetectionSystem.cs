using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DetectionSystem : MonoBehaviour
{
    public Transform target; // The player
    public List<Transform> patrolPoints; // Assign in inspector
    private NavMeshAgent agent;
    private Vector3 StartingPos;

    private int patrolIndex = 0;
    private bool patrolForward = true;
    private bool isChasingPlayer = false;
    private Vector3 lastKnownPosition;
    private bool playerInSight = false;
    private int savedPatrolIndex;

    public float detectionDistance = 5f;
    public int numRays = 5;
    public float coneAngle = 45f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateUpAxis = false;
        agent.updateRotation = false;

        if (patrolPoints.Count > 0)
            agent.SetDestination(patrolPoints[patrolIndex].position);
    }

    void Update()
    {
        CheckForPlayer();

        if (playerInSight)
        {
            if (!isChasingPlayer)
            {
                savedPatrolIndex = patrolIndex;
                isChasingPlayer = true;
            }
            agent.SetDestination(lastKnownPosition);
        }
        else
        {
            if (isChasingPlayer)
            {
                if (agent.remainingDistance <= 0.2f)
                {
                    isChasingPlayer = false;
                    patrolIndex = savedPatrolIndex;
                    agent.SetDestination(patrolPoints[patrolIndex].position);
                }
            }
            else
            {
                Patrol();
            }
        }
    }

    void CheckForPlayer()
    {
        playerInSight = false;

        Vector2 origin = transform.position;
        Vector2 dirToPlayer = (target.position - transform.position).normalized;
        float angleOffset = -coneAngle / 2f;
        float angleIncrement = coneAngle / (numRays - 1);

        for (int i = 0; i < numRays; i++)
        {
            float angle = angleOffset + angleIncrement * i;
            Vector2 rayDirection = Quaternion.Euler(0, 0, angle) * dirToPlayer;

            RaycastHit2D hit = Physics2D.Raycast(origin, rayDirection, detectionDistance);
            Debug.DrawRay(origin, rayDirection * detectionDistance, Color.red, 0.1f);

            if (hit.collider != null && hit.collider.gameObject.name == "Player")
            {
                lastKnownPosition = target.position;
                playerInSight = true;
                break;
            }
        }
    }

    void Patrol()
    {
        if (patrolPoints.Count == 0) return;

        if (agent.remainingDistance <= 0.2f)
        {
            if (patrolForward)
            {
                patrolIndex++;
                if (patrolIndex >= patrolPoints.Count)
                {
                    patrolIndex = patrolPoints.Count - 2;
                    patrolForward = false;
                }
            }
            else
            {
                patrolIndex--;
                if (patrolIndex < 0)
                {
                    patrolIndex = 1;
                    patrolForward = true;
                }
            }

            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
    }
   
}
