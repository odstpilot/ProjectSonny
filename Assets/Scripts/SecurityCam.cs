using UnityEngine;
using System.Collections.Generic;

public class SecurityCam : MonoBehaviour
{
    [Tooltip("List of GameObjects to search through")]
    public List<GameObject> wardens;
    public Transform target; // The player
    private Vector3 lastKnownPosition;
    private bool playerInSight = false;

    public float detectionDistance = 5f;
    public int numRays = 5;
    public float coneAngle = 45f;

    void start()
    {

    }
    void Update()
    {
        CheckForPlayer();

        if (playerInSight)
        {
            
            GameObject closestWarden = GetClosestObject(target.position);
            if (closestWarden != null)
            {
                
                if (closestWarden.GetComponent<Warden>().active = false)
                {
                    closestWarden.GetComponent<Warden>().active = true;
                }
                closestWarden.GetComponent<Warden>().NewTarget();
            }
        }
    }

    public GameObject GetClosestObject(Vector2 position)
    {
        GameObject closestObject = null;
        float shortestDistance = Mathf.Infinity;

        foreach (GameObject obj in wardens)
        {
            if (obj == null) continue;

            Vector2 objPos = obj.transform.position;
            float distance = Vector2.Distance(position, objPos);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestObject = obj;
            }
        }

        return closestObject;
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
}
