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

    public LayerMask visionMask = ~0;

    [Header("Visuals")]
    public Material rayMaterial;

    private List<LineRenderer> rayLines =
        new List<LineRenderer>();

    [Header("Warden Alerts")]

    public float alertInterval = 0.5f;

    public float alertMoveDistance = 1f;

    private bool playerInSight;

    private bool hasReported;

    private Vector3 lastReportedPosition;

    private float lastAlertTime;

    private Warden lastReportedWarden;

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

    void Update()
    {
        if (target == null)
            return;

        CheckForPlayer();

        if (!playerInSight)
        {
            hasReported = false;
            return;
        }

        Warden warden = GetClosestWarden(
            target.position
        );

        if (warden == null)
            return;

        if (warden.CurrentState == Warden.WardenState.Chasing ||
            warden.CurrentState == Warden.WardenState.Attacking)
        {
            return;
        }

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

            warden.ActivitedByCam();

        warden.NewTarget(target.position);
        Debug.Log("hhdudiduduididuh");
        lastReportedPosition = target.position;
        lastReportedWarden = warden;

        lastAlertTime = Time.time;
        hasReported = true;
    }

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

    void CheckForPlayer()
    {
        playerInSight = false;

        Vector2 origin = transform.position;

        Vector2 forward = transform.right;

        float halfAngle = coneAngle * 0.5f;

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

        Vector2 toPlayer =
            (Vector2)target.position - origin;

        float distance = toPlayer.magnitude;

        if (distance > detectionDistance)
            return;

        if (distance <= 0.001f)
        {
            playerInSight = true;

            return;
        }

        float angleToPlayer = Vector2.Angle(
            forward,
            toPlayer
        );

        if (angleToPlayer > halfAngle)
            return;

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

        if (playerHit.collider.transform == target ||
            playerHit.collider.transform.IsChildOf(target))
        {
            playerInSight = true;
        }
    }
}
