using UnityEngine;
using UnityEngine.Rendering;

// draws lower things in front
public static class DepthSort
{
    public const string Layer = "Player";
    public const float FeetToCenter = 0.62f;

    public static SortingGroup Group(GameObject target)
    {
        if (!target.TryGetComponent(out SortingGroup group)) group = target.AddComponent<SortingGroup>();
        group.sortingLayerName = Layer;
        group.sortingOrder = 0;
        return group;
    }
}
