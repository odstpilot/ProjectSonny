using UnityEngine;

public class CameraFallow : MonoBehaviour
{
    public Transform player;
    public Vector3 offset;
    public float smoothSpeed = 0.125f;

    private Camera cam;

    void LateUpdate()
    {
        Vector3 desiredPosition = Target;

        float closed = 1f - Mathf.Pow(1f - Mathf.Clamp01(smoothSpeed), Time.deltaTime * 60f);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, closed);

        transform.position = smoothedPosition;
    }

    public Vector3 Target => Limit(player.position + offset);

    public void SnapToPlayer()
    {
        if (player != null) transform.position = Target;
    }

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
