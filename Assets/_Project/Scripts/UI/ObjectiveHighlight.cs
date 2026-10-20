using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A way to go, pointed out in the level: a faint arrow bobbing just in front of a doorway, pointing into it, and a soft
// pulse of light on the floor there, a nudge rather than a sign. Shown over any number of doorways at once (Show),
// until Hide.
// Between those, it follows the room pinned on the map (MapScreen.SetTarget calls Follow): the next door on the way
// there from whichever room the player's in, or the stairs if the room's on the other floor, until they're in it.
// Made in code, so there's nothing to set up; one shared by whatever needs it.
public class ObjectiveHighlight : MonoBehaviour
{
    const float PixelsPerUnit = 16f;
    const float StandOff = 1.3f;        // how far in front of the doorway the arrow hangs
    const float BobDistance = 0.18f;
    const float BobSeconds = 0.9f;
    static readonly Color ArrowColor = new Color(1f, 0.78f, 0.25f, 0.4f);
    const float GlowStrength = 0.3f;
    const float RecheckSeconds = 0.4f;

    static ObjectiveHighlight instance;
    static Sprite arrowSprite;
    static char following;              // the room pinned on the map, '\0' for none
    static bool shownByHand;            // Show's in charge until Hide

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        following = '\0';
        shownByHand = false;
    }

    private float recheckIn;
    private string pointingAt = "";     // what the arrows are over now, so they're only made again when it changes

    private readonly List<(Transform arrow, Light2D glow, Vector2 at, Vector2 toward)> markers =
        new List<(Transform, Light2D, Vector2, Vector2)>();

    static ObjectiveHighlight Get()
    {
        if (instance == null) instance = new GameObject("Objective Highlight").AddComponent<ObjectiveHighlight>();
        return instance;
    }

    // Points out these doorways, in place of whatever was pointed out before.
    public static void Show(IEnumerable<Teleporter> doors)
    {
        ObjectiveHighlight highlight = Get();
        shownByHand = true;
        highlight.Clear();
        foreach (Teleporter door in doors) highlight.AddDoor(door);
    }

    // Back to following the map's pin.
    public static void Hide()
    {
        shownByHand = false;
        if (instance != null)
        {
            instance.Clear();
            instance.recheckIn = 0f;
        }
    }

    // The room to point the way to, by its marker in the layout. '\0' for none.
    public static void Follow(char marker)
    {
        following = marker;
        if (marker == '\0' && instance == null) return;
        Get().recheckIn = 0f;
    }

    void AddDoor(Teleporter door)
    {
        if (door == null) return;
        var trigger = door.GetComponent<Collider2D>();
        Vector2 middle = trigger != null ? (Vector2)trigger.bounds.center : (Vector2)door.transform.position;
        Vector2 toward = door.wallDirection.sqrMagnitude > 0f ? door.wallDirection.normalized : Vector2.up;
        Add(middle - toward * StandOff, toward);
    }

    // Following the pin: the next door to take from the room the player's in, on the shortest way through the doors to
    // the pinned room (or, with that room on the other floor, to the stairs off this one, and then the stairs
    // themselves). Nothing once they're in it, inside the ducts, or with nothing pinned.
    void PointTheWay()
    {
        var doors = new List<Teleporter>();
        var stairs = new List<Stairway>();
        StationMap map = StationMap.Current;
        GameObject found = GameObject.FindWithTag("Player");
        char RoomOf(Vector2 at) => map.Locate(at, out _, out StationMap.Room room) ? room.marker : '\0';
        char here = map != null && found != null ? RoomOf(found.transform.position) : '\0';
        if (following != '\0' && here != '\0' && here != following && !VentNetwork.IsPlayerInside)
        {
            // The rooms on this floor and the doors between them.
            var ways = new List<(char from, char to, Teleporter door)>();
            foreach (Teleporter door in FindObjectsByType<Teleporter>())
            {
                if (door.locked || door.teleportTarget == null) continue;
                char from = RoomOf(door.transform.position), to = RoomOf(door.teleportTarget.position);
                if (from != '\0' && to != '\0' && from != to) ways.Add((from, to, door));
            }
            // Where it ends: the pinned room, or the rooms with stairs off this floor if the room's on the other one.
            var goals = new HashSet<char>();
            var stairsIn = new Dictionary<char, List<Stairway>>();
            if (map.RoomByMarker(following) != null) goals.Add(following);
            else
            {
                foreach (Stairway stair in FindObjectsByType<Stairway>())
                {
                    if (stair.locked || stair.destination == null) continue;
                    char room = RoomOf(stair.transform.position);
                    if (room == '\0') continue;
                    goals.Add(room);
                    if (!stairsIn.TryGetValue(room, out List<Stairway> list)) stairsIn[room] = list = new List<Stairway>();
                    list.Add(stair);
                }
            }

            if (goals.Contains(here)) { if (stairsIn.TryGetValue(here, out List<Stairway> inHere)) stairs.AddRange(inHere); }
            else
            {
                // Breadth first from where they are, remembering which room each was first reached from.
                var cameFrom = new Dictionary<char, char> { [here] = here };
                var queue = new Queue<char>();
                queue.Enqueue(here);
                char reached = '\0';
                while (queue.Count > 0 && reached == '\0')
                {
                    char room = queue.Dequeue();
                    foreach ((char from, char to, Teleporter _) in ways)
                    {
                        if (from != room || cameFrom.ContainsKey(to)) continue;
                        cameFrom[to] = room;
                        if (goals.Contains(to)) { reached = to; break; }
                        queue.Enqueue(to);
                    }
                }
                if (reached != '\0')
                {
                    // Back along the way to the first room past this one; the doors from here into it.
                    char next = reached;
                    while (cameFrom[next] != here) next = cameFrom[next];
                    foreach ((char from, char to, Teleporter door) in ways)
                        if (from == here && to == next) doors.Add(door);
                }
            }
        }

        var key = new System.Text.StringBuilder();
        foreach (Teleporter door in doors) key.Append(door.GetHashCode()).Append(',');
        foreach (Stairway stair in stairs) key.Append(stair.GetHashCode()).Append(';');
        if (key.ToString() == pointingAt) return;
        Clear();
        pointingAt = key.ToString();
        foreach (Teleporter door in doors) AddDoor(door);
        // Over the stairs, pointing down at them.
        foreach (Stairway stair in stairs)
        {
            var trigger = stair.GetComponent<Collider2D>();
            Vector2 middle = trigger != null ? (Vector2)trigger.bounds.center : (Vector2)stair.transform.position;
            Add(middle - Vector2.down * StandOff, Vector2.down);
        }
    }

    void Add(Vector2 at, Vector2 toward)
    {
        var arrow = new GameObject("Arrow").AddComponent<SpriteRenderer>();
        arrow.transform.SetParent(transform, false);
        arrow.transform.position = at;
        // The art points up.
        arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, toward);
        arrow.sprite = ArrowSprite;
        arrow.color = ArrowColor;
        if (CombatSprites.EffectMaterial != null) arrow.sharedMaterial = CombatSprites.EffectMaterial;
        arrow.sortingLayerName = "Top";

        var glow = new GameObject("Glow").AddComponent<Light2D>();
        glow.transform.SetParent(arrow.transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.pointLightInnerRadius = 0.2f;
        glow.pointLightOuterRadius = 2.2f;
        glow.falloffIntensity = 0.6f;
        glow.color = new Color(ArrowColor.r, ArrowColor.g, ArrowColor.b);
        glow.shadowsEnabled = false;
        markers.Add((arrow.transform, glow, at, toward));
    }

    void Clear()
    {
        foreach (var marker in markers)
            if (marker.arrow != null) Destroy(marker.arrow.gameObject);
        markers.Clear();
        pointingAt = "";
    }

    void Update()
    {
        if (!shownByHand)
        {
            recheckIn -= Time.unscaledDeltaTime;
            if (recheckIn <= 0f)
            {
                recheckIn = RecheckSeconds;
                PointTheWay();
            }
        }

        float wave = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BobSeconds);
        foreach (var marker in markers)
        {
            if (marker.arrow == null) continue;
            marker.arrow.position = marker.at + marker.toward * (wave * BobDistance);
            marker.glow.intensity = GlowStrength * (0.8f + 0.25f * wave);
        }
    }

    // A chunky chevron with a short stem, white so the renderer's color decides it.
    static Sprite ArrowSprite => arrowSprite != null ? arrowSprite : (arrowSprite = PixelArt.FromText(new[]
    {
        "......##......",
        ".....####.....",
        "....######....",
        "...########...",
        "..##########..",
        ".####.##.####.",
        "####..##..####",
        "###...##...###",
        "......##......",
        "......##......",
        "......##......",
        "......##......",
    }, new Dictionary<char, Color32> { { '#', new Color32(255, 255, 255, 255) } }, PixelsPerUnit, new Vector2(0.5f, 0.5f)));
}
