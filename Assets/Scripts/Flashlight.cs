using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
[RequireComponent(typeof(Collider2D))]
public class Flashlight : MonoBehaviour
{
    private Camera mainCamera;
    private Light2D light2D;
    private Collider2D lightCollider;

    public Transform player;
    public float rotationOffset = -90f; // Fixes light facing mismatch
    public float rotationSpeed = 360f;  // Degrees per second

    void Start()
    {
        mainCamera = Camera.main;
        light2D = GetComponent<Light2D>();
        lightCollider = GetComponent<Collider2D>();

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        // Stick to player position
        transform.position = player.position;

        // Calculate target rotation
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mouseWorld - player.position).normalized;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

        // Smooth rotation
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        // Toggle with Shift
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        light2D.enabled = !shiftHeld;
        lightCollider.enabled = !shiftHeld;
    }
}
