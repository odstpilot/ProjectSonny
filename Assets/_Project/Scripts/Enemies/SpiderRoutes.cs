using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ways the camera spider can travel
public static class SpiderRoutes
{
    public class Door
    {
        public Teleporter door, back;
        public char from, to;
        public CrewWalkArea fromFloor, toFloor;
        public Vector2 at;
        public Vector2 exit;
    }

    static readonly Dictionary<char, CrewWalkArea> floors = new Dictionary<char, CrewWalkArea>();
    static readonly List<Door> doors = new List<Door>();
    static Scene builtFor;

    public static char RoomAt(Vector2 point)
    {
        foreach (StationMap map in Object.FindObjectsByType<StationMap>())
            if (map.Locate(point, out _, out StationMap.Room room)) return room.marker;
        return '\0';
    }

    public static CrewWalkArea FloorOf(char room)
    {
        Build();
        return floors.TryGetValue(room, out CrewWalkArea floor) ? floor : null;
    }

    public static List<Door> In(char room)
    {
        Build();
        return doors.FindAll(door => door.from == room);
    }

    public static List<Door> From(char room, string allowed)
    {
        Build();
        var out_ = new List<Door>();
        foreach (Door door in doors)
            if (door.from == room && allowed.IndexOf(door.to) >= 0) out_.Add(door);
        return out_;
    }

    public static Door NextTowards(char from, char goal, string allowed)
    {
        Build();
        if (from == goal) return null;
        var firstDoor = new Dictionary<char, Door> { [from] = null };
        var frontier = new Queue<char>();
        frontier.Enqueue(from);
        while (frontier.Count > 0)
        {
            char room = frontier.Dequeue();
            foreach (Door door in doors)
            {
                if (door.from != room || firstDoor.ContainsKey(door.to) || allowed.IndexOf(door.to) < 0) continue;
                firstDoor[door.to] = firstDoor[room] ?? door;
                if (door.to == goal) return firstDoor[door.to];
                frontier.Enqueue(door.to);
            }
        }
        return null;
    }

    static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (builtFor == scene && doors.Count + floors.Count > 0) return;
        builtFor = scene;
        floors.Clear();
        doors.Clear();

        var holder = new GameObject("Spider Floors").transform;
        foreach (StationMap map in StationMap.All)
            foreach (StationMap.Room room in map.rooms)
                if (!floors.ContainsKey(room.marker))
                {
                    CrewWalkArea floor = MakeFloor(room, holder);
                    if (floor != null) floors[room.marker] = floor;
                }

        foreach (Teleporter door in Object.FindObjectsByType<Teleporter>())
        {
            if (door.locked || door.teleportTarget == null) continue;
            char from = RoomAt(door.transform.position), to = RoomAt(door.teleportTarget.position);
            if (from == '\0' || to == '\0' || from == to) continue;
            if (!floors.TryGetValue(from, out CrewWalkArea fromFloor) || !floors.TryGetValue(to, out CrewWalkArea toFloor)) continue;
            if (!Nearest(fromFloor, door.transform.position, out Vector2 at) || !Nearest(toFloor, door.teleportTarget.position, out Vector2 exit)) continue;
            doors.Add(new Door { door = door, from = from, to = to, fromFloor = fromFloor, toFloor = toFloor, at = at, exit = exit });
        }
        foreach (Door way in doors)
        {
            float best = 3f;
            foreach (Door other in doors)
            {
                if (other.from != way.to || other.to != way.from) continue;
                float gap = Vector2.Distance(other.door.transform.position, way.door.teleportTarget.position);
                if (gap < best)
                {
                    best = gap;
                    way.back = other.door;
                }
            }
        }
    }

    static CrewWalkArea MakeFloor(StationMap.Room room, Transform holder)
    {
        Rect built = room.World;
        int width = Mathf.RoundToInt(built.width), height = Mathf.RoundToInt(built.height);
        if (width <= 0 || height <= 0) return null;
        var open = new bool[width * height];
        var solidOnly = new ContactFilter2D { useTriggers = false };
        var hits = new List<Collider2D>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 feet = built.min + new Vector2(x + 0.5f, y + 0.5f);
                Physics2D.OverlapBox(feet, FeetSize, 0f, solidOnly, hits);
                open[y * width + x] = hits.TrueForAll(StepsOver);
            }

        bool[] kept = Biggest(open, width, height);
        if (kept == null) return null;
        var area = new GameObject("Spider Floor " + room.marker).AddComponent<CrewWalkArea>();
        area.transform.SetParent(holder);
        area.transform.position = built.center;
        area.origin = built.min;
        area.width = width;
        area.height = height;
        area.walkable = kept;
        return area;
    }

    static readonly Vector2 FeetSize = new Vector2(0.6f, 0.45f);

    static bool StepsOver(Collider2D hit) =>
        hit.GetComponentInParent<CrewMember>() != null || hit.GetComponentInParent<PlaceholderRobot>() != null
        || hit.GetComponentInParent<PlayerController>() != null || hit.GetComponentInParent<CameraSpider>() != null
        || hit.GetComponentInParent<MaintenanceBot>() != null;

    static bool[] Biggest(bool[] open, int width, int height)
    {
        var region = new int[open.Length];
        int best = 0, bestSize = 0, next = 0;
        var queue = new Queue<int>();
        for (int start = 0; start < open.Length; start++)
        {
            if (!open[start] || region[start] != 0) continue;
            int id = ++next, size = 0;
            region[start] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                size++;
                int x = i % width, y = i / width;
                foreach (int n in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                {
                    if (n < 0 || !open[n] || region[n] != 0) continue;
                    region[n] = id;
                    queue.Enqueue(n);
                }
            }
            if (size > bestSize)
            {
                bestSize = size;
                best = id;
            }
        }
        if (bestSize == 0) return null;
        var kept = new bool[open.Length];
        for (int i = 0; i < open.Length; i++) kept[i] = region[i] == best;
        return kept;
    }

    static bool Nearest(CrewWalkArea floor, Vector2 point, out Vector2 found)
    {
        Vector2Int cell = floor.CellAt(point);
        for (int r = 0; r <= 5; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                    var near = new Vector2Int(cell.x + x, cell.y + y);
                    if (!floor.IsWalkable(near)) continue;
                    found = floor.CenterOf(near);
                    return true;
                }
        found = point;
        return false;
    }
}
