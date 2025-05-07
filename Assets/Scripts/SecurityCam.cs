using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
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

    public Material rayMaterial;
    private List<LineRenderer> rayLines = new List<LineRenderer>();

    void Start() // fix case on 'Start'
    {
        Color startColor = new Color(1f, .9f, .5f, .05f);
        Color endColor = new Color(1f, 0.1f, 0.1f, .001f);

        // Initialize line renderers
        for (int i = 0; i < numRays; i++)
        {
            GameObject lineObj = new GameObject("RayLine_" + i);
            lineObj.transform.parent = this.transform;

           

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.sortingLayerName = "Top";
            lr.material = rayMaterial;
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.positionCount = 2;
            lr.startColor = startColor;
            lr.endColor = endColor;
            lr.sortingOrder = 10;

            rayLines.Add(lr);
        }
    }

    void Update()
    {
        CheckForPlayer();

        if (playerInSight)
        {
            GameObject closestWarden = GetClosestObject(target.position);
            if (closestWarden != null)
            {
                Warden warden = closestWarden.GetComponent<Warden>();
                if (!warden.active)
                {
                    warden.active = true;
                }
                warden.NewTarget();
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
            Vector2 endPoint = hit ? hit.point : origin + rayDirection * detectionDistance;

            // Set LineRenderer
            rayLines[i].SetPosition(0, origin);
            rayLines[i].SetPosition(1, endPoint);

            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                lastKnownPosition = target.position;
                playerInSight = true;
            }
        }
    }
}
