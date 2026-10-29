using System.Collections.Generic;
using UnityEngine;

// Chapter 2: who's still moving about the station. Far fewer of the turned bots than the level was built with, and none
// at all in the common grounds, where something else is now: two camera spiders (CameraSpider), picking their way among
// the bodies and from room to room through the doors. A quiet room is worse than a busy one.
// It sorts this out as the chapter starts (ChapterTwoDirector adds it), before anything's had a chance to move. The
// bots that come through the comms ring's walls (CommsRing.breachBots) and the reactor's lockdown waves
// (ReactorLockdown) aren't touched: they're part of those.
public class StationOccupants : MonoBehaviour
{
    [Tooltip("How many of the roaming bots stay in a room: its marker then the count, space apart, like 'm1 d2'. Rooms " +
             "not listed keep all of theirs.")]
    public string keepBots = "h0 k0 m1 d2 g1 r2";
    [Tooltip("Where each camera spider starts, by the markers of the rooms: one spider for each.")]
    public string spiderRooms = "km";
    [Tooltip("The rooms they search between them, going through the doors (CameraSpider.roams), and the ones they only " +
             "pass through (passesThrough). Anywhere else, like the restroom and the storage room, is left safe.")]
    public string spidersRoam = "kbmc";
    public string spidersPassThrough = "h";
    [Tooltip("The technician, seeing it, in place of what they'd say about the bots (PatrolReveal).")]
    [TextArea] public string[] technicianSeesSpider = { "...What is that thing... it's all eyes" };

    void Awake() => Populate();

    void Populate()
    {
        StationMap map = StationMap.Current;
        if (map == null) return;

        var spared = new HashSet<PlaceholderRobot>();
        foreach (ReactorLockdown lockdown in FindObjectsByType<ReactorLockdown>())
            foreach (ReactorLockdown.Wave wave in lockdown.waves)
                foreach (PlaceholderRobot bot in wave.bots)
                    if (bot != null) spared.Add(bot);
        foreach (CommsRing ring in FindObjectsByType<CommsRing>())
            foreach (PlaceholderRobot bot in ring.breachBots)
                if (bot != null) spared.Add(bot);

        Dictionary<char, int> keep = Counts(keepBots);
        var counted = new Dictionary<char, int>();
        foreach (PlaceholderRobot bot in FindObjectsByType<PlaceholderRobot>())
        {
            if (bot.roamArea == null || spared.Contains(bot)) continue;
            if (!map.Locate(bot.transform.position, out _, out StationMap.Room room)) continue;
            if (!keep.TryGetValue(room.marker, out int most)) continue;
            counted.TryGetValue(room.marker, out int already);
            counted[room.marker] = already + 1;
            if (already >= most) Destroy(bot.gameObject);
        }

        var reveal = FindAnyObjectByType<PatrolReveal>();
        bool any = false;
        foreach (char marker in spiderRooms)
        {
            // Its own floor (SpiderRoutes), not the crew's: it walks over the bodies.
            CrewWalkArea floor = SpiderRoutes.FloorOf(marker);
            if (floor == null || !floor.TryPickDestination(Vector2Int.zero, 0f, out Vector2Int cell)) continue;
            var spider = new GameObject("Camera Spider").AddComponent<CameraSpider>();
            spider.transform.position = floor.CenterOf(cell);
            spider.walkArea = floor;
            spider.room = marker;
            spider.roams = spidersRoam;
            spider.passesThrough = spidersPassThrough;
            // Blind till the technician's seen what happened to the crew, like the bots (PatrolReveal).
            spider.blind = reveal == null || !reveal.Revealed;
            any = true;
        }
        if (any && reveal != null && technicianSeesSpider.Length > 0) reveal.technicianBots = technicianSeesSpider;
    }

    // "m1 d2" as m: 1, d: 2.
    static Dictionary<char, int> Counts(string text)
    {
        var counts = new Dictionary<char, int>();
        foreach (string entry in text.Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            if (entry.Length >= 2 && int.TryParse(entry.Substring(1), out int count)) counts[entry[0]] = count;
        return counts;
    }
}
