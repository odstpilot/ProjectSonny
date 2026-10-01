using System.Collections.Generic;
using UnityEngine;

// Keeps the little boxes over the level from covering each other: speech (CrewSpeech: the crew, their group chats, the
// technician's thoughts, the bots) and the E and Q tags (PromptBadge). Each frame, after they've all worked out where
// they want to be, they're laid out in order: the tags first, where they are, since they point at what the player can
// use; then the speech, oldest first, each nudged straight up until it's clear of everything already placed. So two
// people talking side by side stack their lines instead of printing over each other, and a line already up keeps its
// place: whatever's said after makes room round it. The dialogue box, Pip, and the
// HUD aren't part of it; they have their own places on screen.
// Made the first time something joins it. Everything's measured in world units.
[DefaultExecutionOrder(1000)]
public class OverheadLayout : MonoBehaviour
{
    public interface IItem
    {
        bool Alive { get; }         // false once it's gone for good, and it's dropped
        bool Visible { get; }       // taking up room right now
        int Priority { get; }       // lower is placed first, and never moves for anything placed after
        float Since { get; }        // when it came up: of the same priority, the earlier is placed first
        Rect Wanted { get; }        // where it would be with nothing else about, in the world
        void Place(Vector2 offset); // where it's to be instead: this far from Wanted
    }

    const float Gap = 0.06f;        // between one box and the next, in world units
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
        // Tags first, then whatever came up first, so nothing already on screen jumps for something new.
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
