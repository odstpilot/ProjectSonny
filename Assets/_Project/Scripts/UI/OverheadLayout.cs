using System.Collections.Generic;
using UnityEngine;

// keeps overhead boxes from overlapping
[DefaultExecutionOrder(1000)]
public class OverheadLayout : MonoBehaviour
{
    public interface IItem
    {
        bool Alive { get; }
        bool Visible { get; }
        int Priority { get; }
        float Since { get; }
        Rect Wanted { get; }
        void Place(Vector2 offset);
    }

    const float Gap = 0.06f;
    const int MaxPushes = 32;

    static readonly List<IItem> items = new List<IItem>();
    static OverheadLayout instance;

    private readonly List<IItem> order = new List<IItem>();
    private readonly List<Rect> placed = new List<Rect>();

    public static void Add(IItem item)
    {
        if (instance == null) instance = new GameObject("Overhead Layout").AddComponent<OverheadLayout>();
        if (!items.Contains(item)) items.Add(item);
    }

    public static void Remove(IItem item) => items.Remove(item);

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void LateUpdate()
    {
        items.RemoveAll(item => item == null || !item.Alive);
        order.Clear();
        foreach (IItem item in items)
            if (item.Visible) order.Add(item);
        order.Sort((a, b) =>
        {
            if (a.Priority != b.Priority) return a.Priority.CompareTo(b.Priority);
            if (a.Since != b.Since) return a.Since.CompareTo(b.Since);
            return a.Wanted.yMin.CompareTo(b.Wanted.yMin);
        });

        placed.Clear();
        foreach (IItem item in order)
        {
            Rect wanted = item.Wanted;
            float up = 0f;
            for (int push = 0; push < MaxPushes; push++)
            {
                Rect here = new Rect(wanted.x, wanted.y + up, wanted.width, wanted.height);
                bool clear = true;
                foreach (Rect other in placed)
                {
                    if (!Overlaps(here, other)) continue;
                    up = other.yMax + Gap - wanted.yMin;
                    clear = false;
                    break;
                }
                if (clear) break;
            }
            placed.Add(new Rect(wanted.x, wanted.y + up, wanted.width, wanted.height));
            item.Place(new Vector2(0f, up));
        }
    }

    static bool Overlaps(Rect a, Rect b) =>
        a.xMin < b.xMax + Gap && b.xMin < a.xMax + Gap && a.yMin < b.yMax + Gap && b.yMin < a.yMax + Gap;
}
