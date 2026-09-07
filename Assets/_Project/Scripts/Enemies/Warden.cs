using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Warden : MonoBehaviour
{
    public Transform target; // The player
    private NavMeshAgent agent;
    public Vector3 StartingPos;
    public Transform spriteTransform; // Drag the child sprite GameObject here in the Inspector
    private bool facingRight = true;

    private bool isChasingPlayer = false;
    private Vector3 lastKnownPosition;
    private bool playerInSight = false;
    public bool active;

    public float detectionDistance = 5f;
    public int numRays = 5;
    public float coneAngle = 45f;

    private List<Vector3> memoryPoints = new List<Vector3>();
    private int randomSearchesRemaining = 0;
    private bool isSearchingRandomly = false;
    private Vector3 currentRandomPoint;
    private int memoryIndex = 0;

    public int maxRandomSearches = 4;
    public float randomSearchRadius = 5f;
    public float memoryPointSaveChance = 0.3f; // 30% chance to save a memory point

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateUpAxis = false;
        agent.updateRotation = false;
        active = false;
    }

    void ActivateRobot()
    {
        active = true;
        lastKnownPosition = GameObject.FindGameObjectWithTag("Player").transform.position;
        agent.SetDestination(lastKnownPosition);
    }

    void Update()
    {
        if (active)
        {
            CheckForPlayer();

            if (playerInSight)
            {
                if(!isChasingPlayer)
                {

                    GameObject.Find("Global Light 2D")?.GetComponent<LightManager>().TriggerLightOff();
                }
                isChasingPlayer = true;
                isSearchingRandomly = false;
                randomSearchesRemaining = maxRandomSearches;
                agent.SetDestination(lastKnownPosition);

                // Occasionally save a memory point
                if (Random.value < memoryPointSaveChance)
                {
                    memoryPoints.Add(lastKnownPosition);
                }
            }
            else
            {
                if (isChasingPlayer)
                {
                    if (agent.remainingDistance <= 0.2f)
                    {
                        if (randomSearchesRemaining > 0)
                        {
                            SearchRandomlyNearLastKnown();
                        }
                        else
                        {
                            StartMemoryPatrol();
                        }
                    }
                }
                else
                {
                    PatrolMemoryPoints();
                }
            }
        }
        UpdateFacingDirection();
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

            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                lastKnownPosition = target.position;
                playerInSight = true;
                break;
            }
        }
    }

    void SearchRandomlyNearLastKnown()
    {
        randomSearchesRemaining--;
        Vector2 randomOffset = Random.insideUnitCircle * randomSearchRadius;
        currentRandomPoint = lastKnownPosition + new Vector3(randomOffset.x, randomOffset.y, 0f);
        agent.SetDestination(currentRandomPoint);
        isSearchingRandomly = true;
    }

    void StartMemoryPatrol()
    {
        Debug.Log("jjj");
        isChasingPlayer = false;
        
        if (memoryPoints.Count > 0)
        {
            memoryIndex = 0;
            agent.SetDestination(memoryPoints[memoryIndex]);
        }
    }

    void PatrolMemoryPoints()
    {
        if (memoryPoints.Count == 0) return;

        if (agent.remainingDistance <= 0.2f)
        {
            memoryIndex++;
            if (memoryIndex >= memoryPoints.Count)
            {
                memoryIndex = 0; // Loop or stop if you want
            }
            agent.SetDestination(memoryPoints[memoryIndex]);
        }
    }
    public void NewTarget()
    {
        if(!isChasingPlayer)
        {
            GameObject.Find("Global Light 2D")?.GetComponent<LightManager>().TriggerLightOff();
        }
        isChasingPlayer = true;
        agent.SetDestination(target.position);
    }
    public void Reset()
    {

        agent.SetDestination(StartingPos);
        active = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            //TriggerLightOff()
            StartMemoryPatrol();
            Debug.Log("AGHHHHHHHHHHHHHHHHHHH");

        }
    }
    void UpdateFacingDirection()
    {
        // Check agent direction if moving
        if (agent.hasPath && agent.velocity.magnitude > 0.1f)
        {
            Vector3 direction = agent.steeringTarget - transform.position;

            if (direction.x > 0 && !facingRight)
            {
                Flip(true);
                Debug.Log("jj");
            }
            else if (direction.x < 0 && facingRight)
            {
                Flip(false);
            }
        }
    }

    void Flip(bool faceRight)
    {
        facingRight = faceRight;
        Vector3 scale = spriteTransform.localScale;
        scale.x = Mathf.Abs(scale.x) * (faceRight ? -1 : 1); // Flip if facing right
        spriteTransform.localScale = scale;
    }
}


