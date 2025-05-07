using UnityEngine;
using UnityEngine.Rendering.Universal; // For Light2D

public class MouseFollowLight2D : MonoBehaviour
{
    private Camera mainCamera;
    private Light2D light2D;

    void Start()
    {
        mainCamera = Camera.main;
        light2D = GetComponent<Light2D>();
    }

    void Update()
    {
        // Follow mouse position
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;
        transform.position = mousePosition;

        // Light turns off while holding Shift, on when released
        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        light2D.enabled = !isShiftHeld;
    }
}