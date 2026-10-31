using System.Collections.Generic;
using UnityEngine;

// keeps the camera inside a room
public class CameraRoom : MonoBehaviour
{
    static readonly List<CameraRoom> all = new List<CameraRoom>();

    [Tooltip("The room's outer walls, in world units.")]
    public Rect bounds;

    void OnEnable()
    {
        all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    public static CameraRoom At(Vector2 point)
    {
        CameraRoom found = null;
        foreach (CameraRoom room in all)
        {
            if (!room.bounds.Contains(point)) continue;
            if (found == null || room.bounds.width * room.bounds.height < found.bounds.width * found.bounds.height)
                found = room;
        }
        return found;
    }

    public Vector2 Clamp(Vector2 wanted, Vector2 viewSize)
    {
        return new Vector2(
            ClampAxis(wanted.x, viewSize.x, bounds.xMin, bounds.xMax),
            ClampAxis(wanted.y, viewSize.y, bounds.yMin, bounds.yMax));
    }

    static float ClampAxis(float center, float size, float min, float max)
    {
        if (size >= max - min) return (min + max) * 0.5f;
        return Mathf.Clamp(center, min + size * 0.5f, max - size * 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
