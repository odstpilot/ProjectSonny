using UnityEngine;

public class CameraFallow : MonoBehaviour
{
    public Transform player;    // Reference to the player's Transform
    public Vector3 offset;      // Offset position of the camera relative to the player
    public float smoothSpeed = 0.125f;  // Speed of the smooth follow

    void LateUpdate()
    {
        // Target position of the camera
        Vector3 desiredPosition = player.position + offset;

        // Smoothly move the camera to the desired position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        transform.position = smoothedPosition;

        // Optional: Keep the camera looking at the player
        // transform.LookAt(player);
    }
}


