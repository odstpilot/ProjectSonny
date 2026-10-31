using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// sonny talking through the screens
public static class SonnyBroadcast
{
    const float TvWidth = 1.45f, TvHeight = 1.1f;
    const float Apart = 3.5f;
    const float WallLift = 1.5f;
    const float NearPlayer = 14f;

    public static bool IsSonny(string speaker) =>
        !string.IsNullOrEmpty(speaker) && speaker.Trim().Equals("Sonny", System.StringComparison.OrdinalIgnoreCase);

    public static bool Speaking { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Speaking = false;

    public static IEnumerator Say(string line, float holdSeconds = 2f, bool slip = false)
    {
        string[] words = (line ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) yield break;
        bool hostile = slip || FindChapterOne() == null;
        List<StationTV> tvs = TVsNearPlayer();
        var screens = new List<SonnyScreen>();
        foreach (SonnyScreen monitor in SonnyScreen.All)
            if (monitor != null && monitor.IsOn) screens.Add(monitor);

        Speaking = true;
        foreach (StationTV tv in tvs) tv.BeginBroadcast();
        yield return Wait(0.35f);

        foreach (string raw in words)
        {
            string shown = raw.ToUpperInvariant();
            foreach (StationTV tv in tvs) tv.ShowWord(shown, hostile);
            foreach (SonnyScreen monitor in screens) monitor.ShowWord(shown);
            StationTV loudest = Nearest(tvs);
            if (loudest != null) loudest.PlayTone(hostile ? Random.Range(0.85f, 0.95f) : 1f);
            else Sfx.Play("Sonny Word", 0.6f, hostile ? 0.9f : 1f);
            bool stop = shown.EndsWith(".") || shown.EndsWith("?") || shown.EndsWith("!");
            yield return Wait(0.4f + 0.04f * shown.Length + (stop ? 0.35f : 0f));
            foreach (StationTV tv in tvs) tv.BetweenWords();
            foreach (SonnyScreen monitor in screens) monitor.ShowWord("");
            yield return Wait(0.1f);
        }

        foreach (StationTV tv in tvs) tv.EndBroadcast(holdSeconds + 1.5f);
        yield return Wait(holdSeconds);
        Speaking = false;
    }

    static Object FindChapterOne() => Object.FindAnyObjectByType<ChapterOneDirector>();

    static List<StationTV> TVsNearPlayer()
    {
        var near = new List<StationTV>();
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return near;
        Vector2 at = player.transform.position;
        StationMap map = StationMap.Current;
        if (map != null && map.Locate(at, out _, out StationMap.Room here))
            foreach (StationTV tv in StationTV.All)
                if (tv.room == here.marker) near.Add(tv);
        if (near.Count > 0) return near;
        foreach (StationTV tv in StationTV.All)
            if (Vector2.Distance(at, tv.transform.position) <= NearPlayer) near.Add(tv);
        return near;
    }

    static StationTV Nearest(List<StationTV> tvs)
    {
        GameObject player = GameObject.FindWithTag("Player");
        StationTV best = null;
        float closest = float.MaxValue;
        foreach (StationTV tv in tvs)
        {
            float distance = player != null ? Vector2.Distance(player.transform.position, tv.transform.position) : 0f;
            if (distance < closest)
            {
                closest = distance;
                best = tv;
            }
        }
        return best;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    public static void PlaceTVs(string rooms, bool rogue)
    {
        var taken = new List<Bounds>();
        foreach (SpriteRenderer drawn in Object.FindObjectsByType<SpriteRenderer>())
            if (drawn.enabled && drawn.bounds.size.x < 6f && drawn.bounds.size.y < 6f) taken.Add(drawn.bounds);

        foreach (StationMap map in Object.FindObjectsByType<StationMap>())
            foreach (StationMap.Room room in map.rooms)
            {
                if (rooms.IndexOf(room.marker) < 0) continue;
                Rect floor = room.World;
                if (floor.width < 6f) continue;
                float y = floor.yMax + WallLift;
                float[] aims = floor.width >= 24f ? new[] { 0.3f, 0.7f } : new[] { 0.35f };
                var hung = new List<float>();
                foreach (float aim in aims)
                {
                    float? spot = ClearSpot(floor, y, floor.xMin + floor.width * aim, taken, hung);
                    if (spot == null) continue;
                    hung.Add(spot.Value);
                    var set = new GameObject($"TV ({room.name})").AddComponent<StationTV>();
                    set.transform.position = new Vector3(spot.Value, y, 0f);
                    set.room = room.marker;
                    set.rogue = rogue;
                    taken.Add(new Bounds(set.transform.position, new Vector3(TvWidth, TvHeight, 1f)));
                }
            }
    }

    static float? ClearSpot(Rect floor, float y, float wanted, List<Bounds> taken, List<float> hung)
    {
        float? best = null;
        float closest = float.MaxValue;
        for (float x = floor.xMin + 1.2f; x <= floor.xMax - 1.2f; x += 0.5f)
        {
            var box = new Bounds(new Vector3(x, y, 0f), new Vector3(TvWidth + 0.3f, TvHeight + 0.2f, 100f));
            bool clear = true;
            foreach (Bounds other in taken)
            {
                var flat = new Bounds(new Vector3(other.center.x, other.center.y, 0f), new Vector3(other.size.x, other.size.y, 100f));
                if (box.Intersects(flat))
                {
                    clear = false;
                    break;
                }
            }
            foreach (float other in hung)
                if (Mathf.Abs(other - x) < Apart) clear = false;
            foreach (StationTV set in StationTV.All)
                if (Vector2.Distance(set.transform.position, new Vector2(x, y)) < Apart) clear = false;
            if (!clear || Mathf.Abs(x - wanted) >= closest) continue;
            closest = Mathf.Abs(x - wanted);
            best = x;
        }
        return best;
    }
}
