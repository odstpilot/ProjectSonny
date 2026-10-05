
using UnityEngine;
using System.Collections.Generic;

public class SecurityCam : MonoBehaviour
{
    [Header("References")]
    public List<GameObject> wardens;
    public Transform target;

    [Header("Detection")]
    public float detectionDistance = 5f;
    public int numRays = 5;
    public float coneAngle = 45f;

    // Include Player and obstacles, but exclude
    // the camera's own collider.
    public LayerMask visionMask = ~0;

    [Header("Visuals")]
    public Material rayMaterial;

    private List<LineRenderer> rayLines =
        new List<LineRenderer>();

    [Header("Warden Alerts")]

    // Minimum time between updated alerts.
    public float alertInterval = 0.5f;

    // Minimum player movement before sending
    // another location.
    public float alertMoveDistance = 1f;

    private bool playerInSight;

    private bool hasReported;

    private Vector3 lastReportedPosition;

    private float lastAlertTime;

    private Warden lastReportedWarden;


    // =====================================================
    // START
    // =====================================================

    void Start()
    {
        Color startColor =
            new Color(1f, .9f, .5f, .05f);

        Color endColor =
            new Color(1f, 0.1f, 0.1f, .001f);

        int rayCount = Mathf.Max(1, numRays);

        for (int i = 0; i < rayCount; i++)
        {
            GameObject lineObj =
                new GameObject("RayLine_" + i);

            lineObj.transform.parent = transform;

            LineRenderer lr =
                lineObj.AddComponent<LineRenderer>();

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


    // =====================================================
    // UPDATE
    // =====================================================

    void Update()
    {
        if (target == null)
            return;

        CheckForPlayer();

        if (!playerInSight)
        {
            // Allow a new alert when the player
            // re-enters the camera's view.
            hasReported = false;
            return;
        }

        Warden warden = GetClosestWarden(
            target.position
        );

        if (warden == null)
            return;

        // Don't interrupt the Warden's own chase
        // or attack.
        if (warden.CurrentState == Warden.WardenState.Chasing ||
            warden.CurrentState == Warden.WardenState.Attacking)
        {
      
            return;
        }

        // Don't repeatedly report the same location.
        if (hasReported && lastReportedWarden == warden)
        {
            if (Time.time - lastAlertTime < alertInterval)
                return;

            if (Vector2.Distance(
                target.position,
                lastReportedPosition
            ) < alertMoveDistance)
            {
                return;
            }
        }

        // Activate an inactive Warden.
        
            warden.ActivitedByCam();
        

        // Tell it where the camera saw the player.
        warden.NewTarget(target.position);
        Debug.Log("hhdudiduduididuh");
        lastReportedPosition = target.position;
        lastReportedWarden = warden;

        lastAlertTime = Time.time;
        hasReported = true;
    }


    // =====================================================
    // CLOSEST WARDEN
    // =====================================================

    public Warden GetClosestWarden(Vector2 position)
    {
        Warden closest = null;

        float shortestDistance = Mathf.Infinity;

        foreach (GameObject obj in wardens)
        {
            if (obj == null)
                continue;

            Warden warden = obj.GetComponent<Warden>();
       

            if (warden == null || !warden.enabled)
                continue;

            float distance = Vector2.Distance(
                position,
                obj.transform.position
            );

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closest = warden;
            }
        }

        return closest;
    }


    // =====================================================
    // PLAYER DETECTION
    // =====================================================

    void CheckForPlayer()
    {
        playerInSight = false;
    
        Vector2 origin = transform.position;

        // The camera looks in its local RIGHT direction.
        // Rotate the camera object to rotate its vision.
        Vector2 forward = transform.right;

        float halfAngle = coneAngle * 0.5f;

        // ---------------------------------------------
        // DRAW THE CAMERA'S VISION RAYS
        // ---------------------------------------------

        for (int i = 0; i < rayLines.Count; i++)
        {
            float angle = rayLines.Count == 1
                ? 0f
                : -halfAngle +
                  coneAngle * i / (rayLines.Count - 1);

            Vector2 direction =
                Quaternion.Euler(0, 0, angle) * forward;

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                direction,
                detectionDistance,
                visionMask
            );

            Vector2 endPoint = hit.collider != null
                ? hit.point
                : origin + direction * detectionDistance;

            rayLines[i].SetPosition(0, origin);
            rayLines[i].SetPosition(1, endPoint);
        }

        // ---------------------------------------------
        // DETECT THE PLAYER
        // ---------------------------------------------

        Vector2 toPlayer =
            (Vector2)target.position - origin;

        float distance = toPlayer.magnitude;

        // Outside detection distance.
        if (distance > detectionDistance)
            return;

        if (distance <= 0.001f)
        {
            playerInSight = true;
         
            return;
        }

        // Outside the camera's viewing angle.
        float angleToPlayer = Vector2.Angle(
            forward,
            toPlayer
        );

        if (angleToPlayer > halfAngle)
            return;

        // Check whether a wall blocks the player.
        RaycastHit2D playerHit = Physics2D.Raycast(
            origin,
            toPlayer.normalized,
            distance + 0.05f,
            visionMask
        );

        if (playerHit.collider == null)
        {
            Debug.Log("not dected");
            return;
        }
        

        // Support Player colliders on child objects.
        if (playerHit.collider.transform == target ||
            playerHit.collider.transform.IsChildOf(target))
        {
            
            playerInSight = true;
        }
    }
}
