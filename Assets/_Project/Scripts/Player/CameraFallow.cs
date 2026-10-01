using UnityEngine;

public class CameraFallow : MonoBehaviour
{
    public Transform player;    // Reference to the player's Transform
    public Vector3 offset;      // Offset position of the camera relative to the player
    public float smoothSpeed = 0.125f;  // Speed of the smooth follow: how much of the way it closes in a 60th of a second

    private Camera cam;

    void LateUpdate()
    {
        // Target position of the camera, kept inside the walls of the room the player is in
        Vector3 desiredPosition = Target;

        // Smoothly move the camera to the desired position, the same at any frame rate
        float closed = 1f - Mathf.Pow(1f - Mathf.Clamp01(smoothSpeed), Time.deltaTime * 60f);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, closed);

        transform.position = smoothedPosition;

        // Optional: Keep the camera looking at the player
        // transform.LookAt(player);
    }

    // Where the camera is heading: on the player, kept inside the walls of the room they're in.
    public Vector3 Target => Limit(player.position + offset);

    // Jumps straight to the player, for when they've been moved somewhere else in one go (Teleporter).
    public void SnapToPlayer()
    {
        if (player != null) transform.position = Target;
    }

    // If the player is in a room with a CameraRoom, keeps the view from going past its outside walls.
    Vector3 Limit(Vector3 position)
    {
        CameraRoom room = CameraRoom.At(player.position);
        if (room == null) return position;

        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return position;

        float height = cam.orthographicSize * 2f;
        Vector2 limited = room.Clamp(position, new Vector2(height * cam.aspect, height));
        return new Vector3(limited.x, limited.y, position.z);
    }
}
