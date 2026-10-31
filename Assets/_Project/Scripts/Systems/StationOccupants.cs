using System.Collections.Generic;
using UnityEngine;

// fewer bots, and the spider, after chapter 1
public class StationOccupants : MonoBehaviour
{
    [Tooltip("How many of the roaming bots stay in a room: its marker then the count, space apart, like 'm1 d2'. Rooms " +
             "not listed keep all of theirs.")]
    public string keepBots = "h0 k0 m0 d0 g1 r1";
    [Tooltip("Where each camera spider starts, by the markers of the rooms: one spider for each.")]
    public string spiderRooms = "km";
    [Tooltip("The rooms they search between them, going through the doors (CameraSpider.roams), and the ones they only " +
             "pass through (passesThrough). Anywhere else, like the restroom and the storage room, is left safe.")]
    public string spidersRoam = "kbmc";
    public string spidersPassThrough = "h";
    [Tooltip("Rooms that each get a spider of their own, that never leaves them.")]
    public string lonerRooms = "d";
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
            char marker = SpiderRoutes.RoomAt(bot.transform.position);
            if (marker == '\0') continue;
            if (!keep.TryGetValue(marker, out int most)) continue;
            counted.TryGetValue(marker, out int already);
            counted[marker] = already + 1;
            if (already >= most) Destroy(bot.gameObject);
        }

        var reveal = FindAnyObjectByType<PatrolReveal>();
        bool any = false;
        foreach (char home in spiderRooms + lonerRooms)
        {
            bool loner = lonerRooms.IndexOf(home) >= 0;
            CrewWalkArea floor = SpiderRoutes.FloorOf(home);
            if (floor == null || !floor.TryPickDestination(Vector2Int.zero, 0f, out Vector2Int cell)) continue;
            var spider = new GameObject("Camera Spider").AddComponent<CameraSpider>();
            spider.transform.position = floor.CenterOf(cell);
            spider.walkArea = floor;
            spider.room = home;
            spider.roams = loner ? home.ToString() : spidersRoam;
            spider.passesThrough = loner ? "" : spidersPassThrough;
            spider.blind = reveal == null || !reveal.Revealed;
            any = true;
        }
        if (any && reveal != null && technicianSeesSpider.Length > 0) reveal.technicianBots = technicianSeesSpider;
    }

    static Dictionary<char, int> Counts(string text)
    {
        var counts = new Dictionary<char, int>();
        foreach (string entry in text.Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            if (entry.Length >= 2 && int.TryParse(entry.Substring(1), out int count)) counts[entry[0]] = count;
        return counts;
    }
}
