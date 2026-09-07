using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
public class Flashlight : MonoBehaviour
{
    private Camera mainCamera;
    private Light2D light2D;
    private Collider2D lightCollider;
    private Animator animator;

    public Transform player;
    public float rotationOffset = -90f;
    public float rotationSpeed = 360f;

    private float defaultIntensity;
    private Color defaultColor;

    [Header("Overheat Settings")]
    public float maxOverheat = 5f;           
    public float overheatCooldownRate = 2f;  
    public float overheatDrainRate = 1f;     

    private float overheatValue;
    private bool isFlashlightOn = false;

    [Header("Pulsing Settings")]
    public float pulseThreshold = 0.25f; // % of overheat before pulsing
    public float pulseSpeed = 4f;
    public float minIntensity = 0.8f;
    public float maxIntensity = 1.2f;

    private bool isPulsing = false;
    private float pulseTimer = 0f;

    void Start()
    {
        mainCamera = Camera.main;
        light2D = GetComponent<Light2D>();
        lightCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        overheatValue = maxOverheat;

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        UpdateFlashlightState(false); // Start with flashlight OFF
        defaultIntensity = light2D.intensity;
        defaultColor = light2D.color;

    }

    void Update()
    {
        if (player == null) return;

        // Position & Rotation
        transform.position = player.position;

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mouseWorld - player.position).normalized;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isFlashlightOn)
            {
                UpdateFlashlightState(false);
            }
            else if (overheatValue > 0f) // Only allow toggle ON if we have charge
            {
                UpdateFlashlightState(true);
            }
        }

        // Handle overheat logic
        if (isFlashlightOn)
        {
            overheatValue -= overheatDrainRate * Time.deltaTime;
            if (overheatValue <= 0f)
            {
                overheatValue = 0f;
                UpdateFlashlightState(false); // Auto turn off
            }
        }
        else
        {
            if (overheatValue < maxOverheat)
            {
                overheatValue += overheatCooldownRate * Time.deltaTime;
                overheatValue = Mathf.Min(overheatValue, maxOverheat);
            }
        }

        float percent = overheatValue / maxOverheat;

        if (isFlashlightOn && percent <= pulseThreshold)
        {
            isPulsing = true;
            pulseTimer += Time.deltaTime * pulseSpeed;

            // Intensity pulsing
            float pulse = Mathf.Lerp(minIntensity, maxIntensity, (Mathf.Sin(pulseTimer) + 1f) / 2f);
            light2D.intensity = pulse;

            // Color pulsing between original and red
            Color pulseColor = Color.Lerp(defaultColor, Color.red, (Mathf.Sin(pulseTimer) + 1f) / 2f);
            light2D.color = pulseColor;
        }
        else if (isPulsing)
        {
            // Stop pulsing and reset
            isPulsing = false;
            pulseTimer = 0f;
            light2D.intensity = defaultIntensity;
            light2D.color = defaultColor;
        }
    }

    private void UpdateFlashlightState(bool turnOn)
    {
        isFlashlightOn = turnOn;
        light2D.enabled = turnOn;
        lightCollider.enabled = turnOn;

        if (turnOn)
        {
            // Reset light settings to default
            light2D.intensity = defaultIntensity;
            light2D.color = defaultColor;
        }

        if (animator != null)
        {
            animator.ResetTrigger(turnOn ? "TurnOff" : "TurnOn");
            animator.SetTrigger(turnOn ? "TurnOn" : "TurnOff");
        }
    }

    public float GetOverheatPercent()
    {
        return overheatValue / maxOverheat;
    }
}
