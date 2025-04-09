using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AI;
using UnityEngine.AI;

public class DetectionSystem : MonoBehaviour
{
    public Transform target;
    private NavMeshAgent agent;
    private int distance;

    private Vector3 lastKnownPosition;
    private bool playerInSight;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateUpAxis = false;
        agent.updateRotation = false;
        distance = 5;
        playerInSight = false;
    }

    void Update()
    {
        int numRays = 5;

        float coneAngle = 45f;
        float angleOffset = -coneAngle / 2f;
        float angleIncrement = coneAngle / (numRays - 1);

        Vector2 origin = transform.position;
        Vector2 dir = (target.position - transform.position).normalized;

        playerInSight = false;

        for (int i = 0; i < numRays; i++)
        {
            float angle = angleOffset + angleIncrement * i;
            Vector2 rotatedDir = Quaternion.Euler(0, 0, angle) * dir;

            RaycastHit2D hit = Physics2D.Raycast(origin, rotatedDir, distance);
            Debug.DrawRay(origin, rotatedDir * distance, Color.red, 0.1f);

            if (hit.collider != null && hit.collider.gameObject.name == "Player")
            {
                lastKnownPosition = target.position;
                playerInSight = true;
                break; // stop once player is seen in one of the rays
            }
        }

        if (playerInSight)
        {
            agent.SetDestination(lastKnownPosition);
        }
        else if (agent.remainingDistance <= 0.1f)
        {
            // Idle or patrol here if needed
            // Debug.Log("Reached last known position. Player lost.");
        }
    }
}
