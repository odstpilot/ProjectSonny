using UnityEngine;
using UnityEngine.Rendering;

// Things standing on the floor are drawn in front of or behind each other by how low they are on screen: the 2D
// renderer sorts along the Y axis (Settings/Renderer2D), so something further down the screen is nearer the camera.
// Each thing is placed by the point it sorts from. For a character that's the middle of their sprite, which sits about
// FeetToCenter above their feet; a prop that isn't centered on its sprite (furniture, say) puts its sorting point that
// far above its base, so the two compare like for like.
public static class DepthSort
{
    public const string Layer = "Player";
    // From a character's feet to the middle of their sprite, in world units. Near enough for the player and the robots.
    public const float FeetToCenter = 0.62f;

    // Keeps every sprite under the object together as one, so a held weapon or a hit flash can't show through a desk the
    // character is standing behind. The group sorts from the object's own position.
    public static SortingGroup Group(GameObject target)
    {
        if (!target.TryGetComponent(out SortingGroup group)) group = target.AddComponent<SortingGroup>();
        group.sortingLayerName = Layer;
        group.sortingOrder = 0;
        return group;
    }
}
