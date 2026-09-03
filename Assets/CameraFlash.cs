using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal;

public class CameraFlash : MonoBehaviour
{
    public Transform player;
    public float yOffset = 1.5f;
    public float flashDuration = 0.2f;
    public float fadeDuration = 0.5f;
    public float cooldownDuration = 1f;

    public GameObject flashLight;
    public LayerMask angelLayer;
    public float stunRadius = 3f;

    public float rotationOffset = -90f;          // Aiming rotation offset
    public float coneRotationOffset = 0f;        // NEW: cone angle offset for detection and gizmos

    public AudioSource audioSource;
    public AudioClip flashClip;

    private bool canFlash = true;
    private Light2D light2D;

    void Start()
    {
        light2D = flashLight.GetComponent<Light2D>();
        light2D.intensity = 0f;
        flashLight.SetActive(false);
    }

    private void Update()
    {
        Vector3 playerPosition = player.position + new Vector3(0, yOffset, 0);
        transform.position = playerPosition;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mouseWorld - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);

        if (Input.GetMouseButtonDown(0) && canFlash)
        {
            StartCoroutine(Flash());
        }
    }

    private IEnumerator Flash()
    {
        canFlash = false;
        flashLight.SetActive(true);
        light2D.intensity = 1f;

        if (audioSource && flashClip)
        {
            audioSource.PlayOneShot(flashClip);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, stunRadius, angelLayer);

        // Apply coneRotationOffset here for cone direction
        Vector2 flashDirection = Quaternion.Euler(0, 0, coneRotationOffset) * (Vector2)transform.right;
        float halfConeAngle = light2D.pointLightOuterAngle / 2f;

        foreach (var hit in hits)
        {
            Vector2 toTarget = (Vector2)(hit.transform.position - transform.position);
            float angleToTarget = Vector2.Angle(flashDirection, toTarget);

            if (angleToTarget <= halfConeAngle)
            {
                hit.GetComponent<WeepingAngel>()?.Stun();
                Debug.Log($"Stunned angel: {hit.name}");
            }
        }

        yield return new WaitForSeconds(flashDuration);

        float startIntensity = light2D.intensity;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            light2D.intensity = Mathf.Lerp(startIntensity, 0f, t / fadeDuration);
            yield return null;
        }

        light2D.intensity = 0f;
        flashLight.SetActive(false);

        yield return new WaitForSeconds(cooldownDuration);
        canFlash = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null || flashLight == null)
            return;

        Vector3 origin = transform.position;

        // Apply the coneRotationOffset here
        Vector3 direction = Quaternion.Euler(0, 0, coneRotationOffset) * transform.right;

        float radius = stunRadius;
        float halfAngle = light2D ? light2D.pointLightOuterAngle / 2f : 30f;

        Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Transparent yellow

        Vector3 leftBoundary = Quaternion.Euler(0, 0, -halfAngle) * direction * radius;
        Vector3 rightBoundary = Quaternion.Euler(0, 0, halfAngle) * direction * radius;

        Gizmos.DrawLine(origin, origin + leftBoundary);
        Gizmos.DrawLine(origin, origin + rightBoundary);

        int segments = 20;
        Vector3 previousPoint = origin + leftBoundary;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (i * (2f * halfAngle) / segments);
            Vector3 nextPoint = origin + Quaternion.Euler(0, 0, currentAngle) * direction * radius;
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
}
