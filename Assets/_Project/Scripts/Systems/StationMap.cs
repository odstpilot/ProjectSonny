using System.Collections.Generic;
using UnityEngine;

// The floor's plan, for the map in the technician's suit (MapScreen). Baked by FloorOneBuilder from the layout as it's
// drawn: the rooms at their drawn size and place, the doorways between them, and a few marks (the ship entrance, the
// stairs, where Sonny goes in). The rooms are built bigger and far apart (see FloorOneBuilder), so where the player is
// on the map is worked out room by room: which room they're standing in, then which drawn row and column that part of
// the room was stretched from.
// One per floor scene: StationMap.Current.
public class StationMap : MonoBehaviour
{
    // What a cell of the drawn plan is.
    public const byte Empty = 0;
    public const byte DoorGap = 255;     // the wall between the two ends of a door, drawn as the way through
    // Anything from 1 to 254 is the floor of rooms[value - 1].

    public enum Mark : byte { None, Door, Locked, Stairs, Sonny }

    [System.Serializable]
    public class Room
    {
        public string name;
        public char marker;
        public RectInt drawn;           // its floor in the plan, in cells, row 0 at the top
        // Where it's built: its floor's first column and top row in the built level, and the level's height in rows.
        public int builtX, builtRow, builtHeight;
        public int builtWidth, builtRows;
        // For each built column and row of its floor, which drawn one it's a copy of, counted from drawn's corner.
        public int[] drawnColumns, drawnRows;
        // Where its floor's built map starts in the world: zero, unless the floor was built beside another in the scene.
        public Vector2 origin;

        public Vector2 DrawnCenter => new Vector2(drawn.x + drawn.width * 0.5f, drawn.y + drawn.height * 0.5f);

        // The built floor, in world units.
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

    // The floor the player is on. A scene can hold more than one (every chapter has the whole station); the last one
    // the player was on stays current while they're somewhere that isn't on any, like the stairs.
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

    void OnEnable() => all.Add(this);

    void OnDisable()
    {
        all.Remove(this);
        if (current == this) current = null;
    }

    // So there's something listening for the map key, once the suit has a map.
    void Start() => MapScreen.Get();

    public byte CellAt(int x, int row) => x >= 0 && row >= 0 && x < width && row < height ? cells[row * width + x] : Empty;

    public Mark MarkAt(int x, int row) => x >= 0 && row >= 0 && x < width && row < height ? (Mark)marks[row * width + x] : Mark.None;

    public Room RoomByMarker(char marker) => rooms.Find(room => room.marker == marker);

    // Where a point in the level is on the plan, in cells from its top-left corner (x right, y down), and which room
    // it's in. Just off a room's floor (up against its top wall, or in a doorway) counts as the nearest edge of it.
    // False when it's nowhere near any of them.
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
            // Built column and row inside the room, and how far across each it is.
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
