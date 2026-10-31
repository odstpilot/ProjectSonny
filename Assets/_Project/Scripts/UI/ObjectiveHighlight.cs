using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// arrows showing the way
public class ObjectiveHighlight : MonoBehaviour
{
    const float PixelsPerUnit = 16f;
    const float StandOff = 1.3f;
    const float BobDistance = 0.18f;
    const float BobSeconds = 0.9f;
    static readonly Color ArrowColor = new Color(1f, 0.78f, 0.25f, 0.4f);
    const float GlowStrength = 0.3f;
    const float RecheckSeconds = 0.4f;

    static ObjectiveHighlight instance;
    static Sprite arrowSprite;
    static char following;
    static bool shownByHand;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        following = '\0';
        shownByHand = false;
    }

    private float recheckIn;
    private string pointingAt = "";

    private readonly List<(Transform arrow, Light2D glow, Vector2 at, Vector2 toward)> markers =
        new List<(Transform, Light2D, Vector2, Vector2)>();

    static ObjectiveHighlight Get()
    {
        if (instance == null) instance = new GameObject("Objective Highlight").AddComponent<ObjectiveHighlight>();
        return instance;
    }

    public static void Show(IEnumerable<Teleporter> doors)
    {
        ObjectiveHighlight highlight = Get();
        shownByHand = true;
        highlight.Clear();
        foreach (Teleporter door in doors) highlight.AddDoor(door);
    }

    public static void Hide()
    {
        shownByHand = false;
        if (instance != null)
        {
            instance.Clear();
            instance.recheckIn = 0f;
        }
    }

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

    public struct Place : System.IEquatable<Place>
    {
        public StationMap floor;
        public char room;
        public Place(StationMap floor, char room) { this.floor = floor; this.room = room; }
        public bool Equals(Place other) => floor == other.floor && room == other.room;
        public override bool Equals(object other) => other is Place place && Equals(place);
        public override int GetHashCode() => (floor != null ? floor.GetHashCode() : 0) * 31 + room;
    }

    public struct Way
    {
        public Place from, to;
        public Teleporter door;
        public Stairway stair;
    }

    static bool PlaceOf(Vector2 at, out Place place)
    {
        bool found = StationMap.Find(at, out StationMap floor, out StationMap.Room room);
        place = found ? new Place(floor, room.marker) : default;
        return found;
    }

    public static List<Way> Route()
    {
        var none = new List<Way>();
        GameObject found = GameObject.FindWithTag("Player");
        if (following == '\0' || found == null || VentNetwork.IsPlayerInside) return none;
        if (!PlaceOf(found.transform.position, out Place here) || here.room == following) return none;

        bool inScene = false;
        foreach (StationMap floor in StationMap.All)
            if (floor.RoomByMarker(following) != null) inScene = true;
        bool Goal(Place place) => inScene ? place.floor != null && place.room == following : place.floor == null;

        var open = new List<Way>();
        var all = new List<Way>();
        foreach (Teleporter door in FindObjectsByType<Teleporter>())
        {
            if (door.locked || door.teleportTarget == null) continue;
            if (!PlaceOf(door.transform.position, out Place from) || !PlaceOf(door.teleportTarget.position, out Place to) || from.Equals(to)) continue;
            var way = new Way { from = from, to = to, door = door };
            all.Add(way);
            if (door.HasAccess) open.Add(way);
        }
        foreach (Stairway stair in FindObjectsByType<Stairway>())
        {
            if (!PlaceOf(stair.transform.position, out Place from)) continue;
            Place to = default;
            if (stair.destination != null)
            {
                Transform end = stair.destination.arrival != null ? stair.destination.arrival : stair.destination.transform;
                if (!PlaceOf(end.position, out to)) continue;
            }
            else if (string.IsNullOrEmpty(stair.targetScene)) continue;
            var way = new Way { from = from, to = to, stair = stair };
            all.Add(way);
            if (!stair.locked) open.Add(way);
        }

        List<Way> route = Search(here, open, Goal);
        return route.Count > 0 ? route : Search(here, all, Goal);
    }

    static List<Way> Search(Place here, List<Way> ways, System.Func<Place, bool> goal)
    {
        var cameBy = new Dictionary<Place, Way>();
        var seen = new HashSet<Place> { here };
        var queue = new Queue<Place>();
        queue.Enqueue(here);
        while (queue.Count > 0)
        {
            Place room = queue.Dequeue();
            foreach (Way way in ways)
            {
                if (!way.from.Equals(room) || !seen.Add(way.to)) continue;
                cameBy[way.to] = way;
                if (goal(way.to))
                {
                    var route = new List<Way>();
                    for (Place at = way.to; !at.Equals(here); at = cameBy[at].from) route.Insert(0, cameBy[at]);
                    return route;
                }
                if (way.to.floor != null) queue.Enqueue(way.to);
            }
        }
        return new List<Way>();
    }

    public static Stairway StairsOnTheWay(StationMap floor, out bool up)
    {
        up = false;
        foreach (Way way in Route())
        {
            if (way.stair == null || way.from.floor != floor) continue;
            up = way.to.floor != null ? way.to.floor.Number > floor.Number : way.stair.name.Contains("Up");
            return way.stair;
        }
        return null;
    }

    void PointTheWay()
    {
        var doors = new List<Teleporter>();
        var stairs = new List<Stairway>();
        List<Way> route = Route();
        if (route.Count > 0)
        {
            if (route[0].door != null) doors.Add(route[0].door);
            else stairs.Add(route[0].stair);
        }

        var key = new System.Text.StringBuilder();
        foreach (Teleporter door in doors) key.Append(door.GetHashCode()).Append(',');
        foreach (Stairway stair in stairs) key.Append(stair.GetHashCode()).Append(';');
        if (key.ToString() == pointingAt) return;
        Clear();
        pointingAt = key.ToString();
        foreach (Teleporter door in doors) AddDoor(door);
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
