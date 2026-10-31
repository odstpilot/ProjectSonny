using System.Collections.Generic;
using UnityEngine;

// floor plan for the suit map
public class StationMap : MonoBehaviour
{
    public const byte Empty = 0;
    public const byte DoorGap = 255;

    public enum Mark : byte { None, Door, Locked, Stairs, Sonny }

    [System.Serializable]
    public class Room
    {
        public string name;
        public char marker;
        public RectInt drawn;
        public int builtX, builtRow, builtHeight;
        public int builtWidth, builtRows;
        public int[] drawnColumns, drawnRows;
        public Vector2 origin;

        public Vector2 DrawnCenter => new Vector2(drawn.x + drawn.width * 0.5f, drawn.y + drawn.height * 0.5f);

        public Rect World => Rect.MinMaxRect(origin.x + builtX, origin.y + builtHeight - builtRow - builtRows,
            origin.x + builtX + builtWidth, origin.y + builtHeight - builtRow);
    }

    static StationMap current;
    static readonly List<StationMap> all = new List<StationMap>();
    static Transform player;

    public string floorName = "Floor 1";
    public int width, height;
    public byte[] cells = new byte[0];
    public byte[] marks = new byte[0];
    public List<Room> rooms = new List<Room>();

    public static StationMap Current
    {
        get
        {
            if (all.Count == 0)
            {
                StationMap found = FindAnyObjectByType<StationMap>();
                return found;
            }
            if (all.Count == 1) return current = all[0];
            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null) player = found.transform;
            }
            if (player != null)
                foreach (StationMap map in all)
                    if (map.Locate(player.position, out _, out _)) return current = map;
            return current != null ? current : current = all[0];
        }
    }

    public static IReadOnlyList<StationMap> All => all;

    public static bool Find(Vector2 world, out StationMap floor, out Room room)
    {
        foreach (StationMap map in all)
            if (map.Locate(world, out _, out room))
            {
                floor = map;
                return true;
            }
        floor = null;
        room = null;
        return false;
    }

    public int Number
    {
        get
        {
            string digits = "";
            foreach (char c in floorName)
                if (char.IsDigit(c)) digits += c;
            return int.TryParse(digits, out int n) ? n : 0;
        }
    }

    void OnEnable() => all.Add(this);

    void OnDisable()
    {
        all.Remove(this);
        if (current == this) current = null;
    }

    void Start() => MapScreen.Get();

    public byte CellAt(int x, int row) => x >= 0 && row >= 0 && x < width && row < height ? cells[row * width + x] : Empty;

    public Mark MarkAt(int x, int row) => x >= 0 && row >= 0 && x < width && row < height ? (Mark)marks[row * width + x] : Mark.None;

    public Room RoomByMarker(char marker) => rooms.Find(room => room.marker == marker);

    public bool Locate(Vector2 world, out Vector2 onPlan, out Room room)
    {
        const float Nearby = 2.5f;
        foreach (float reach in new[] { 0f, Nearby })
            if (Locate(world, reach, out onPlan, out room)) return true;
        onPlan = default;
        room = null;
        return false;
    }

    bool Locate(Vector2 world, float reach, out Vector2 onPlan, out Room room)
    {
        foreach (Room r in rooms)
        {
            Rect built = r.World;
            Rect near = new Rect(built.x - reach, built.y - reach, built.width + reach * 2f, built.height + reach * 2f);
            if (!near.Contains(world)) continue;
            world = new Vector2(Mathf.Clamp(world.x, built.xMin, built.xMax - 0.01f), Mathf.Clamp(world.y, built.yMin + 0.01f, built.yMax));
            float across = world.x - built.xMin;
            float down = built.yMax - world.y;
            int column = Mathf.Clamp(Mathf.FloorToInt(across), 0, r.builtWidth - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt(down), 0, r.builtRows - 1);
            float x = Drawn(r.drawnColumns, column) + (across - column);
            float y = Drawn(r.drawnRows, row) + (down - row);
            onPlan = new Vector2(r.drawn.x + x, r.drawn.y + y);
            room = r;
            return true;
        }
        onPlan = default;
        room = null;
        return false;
    }

    static int Drawn(int[] lines, int built) => lines == null || lines.Length == 0 ? built : lines[Mathf.Clamp(built, 0, lines.Length - 1)];
}
