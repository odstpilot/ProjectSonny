using System.Collections.Generic;
using UnityEngine;

// A room's outside walls, for the camera. While the player is inside, CameraFallow keeps its view within bounds, so it
// never looks past the walls into the dark beyond. If the view is wider or taller than the room, it's centered instead.
// Setup: put bounds around the room's outer walls, in world units. Floors built by FloorOneBuilder get one per room.
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

    // The room around this point, or null. The smallest one wins if rooms overlap.
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

    // As close to wanted as a view of this size can be while staying inside the walls.
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
