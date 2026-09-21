using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Map = TutorialLevelBuilder.Map;

// Menu: Sonny > Build Floor 1
//
// Rebuilds Scenes/Levels/REAL GAME.unity from the text map in Scenes/Levels/REAL GAME/Floor1Layout.txt: the station's
// first floor as the blueprint lays it out. The common grounds (kitchen and lounge, hallway, bedrooms) with the ship
// entrance at the west end, the storage room, the east hallway down to the stairs, the maintenance deck, and the hallway
// up to the control room. It uses the Tutorial builder's map format and paints the same tiles, lamps, windows, and
// lockers (see TutorialLevelBuilder), so edit the map and run this again. It replaces everything in the scene.
//
// The map is drawn compact, like the blueprint, but the rooms are built far apart: each keeps its place in the layout at
// three times the distance, so no room can be seen from another. The ways between them are Teleporters, which fade the
// screen through black and put the player on the other side. Every room also keeps the camera inside its outside walls
// (CameraRoom).
//
// Rooms are dressed the way the rooms on Map (DEMO) are, with the same tiles put together the same way: a floor laid for
// each room (FloorKits), machinery and pipes along the walls of the working rooms (PipeWallRooms), and furniture (Furnishings):
// console banks with chairs, pods in rows, desks, sofas, tables on hazard-striped floor, railings, and ceiling lights.
//
// On top of the Tutorial builder's walls, floor, windows, lamps, and lockers, the map has:
//   P        where the player starts
//   1 - 9    closed doors: the same digit at each end, on the floor cells against the wall the door is in. Walk up to
//            one and press E to come out just inside the other.
//   ! ? $    hallways that carry on: the same symbol at each end, on floor that runs into a dead end. Walking in takes
//            the player straight through.
//   X        a door that stays locked (the ship entrance)
//   k h b s v m c o   one cell in each room, which names it: kitchen and lounge, common grounds hallway, bedrooms,
//            storage room, east hallway, maintenance deck, hallway to the control room, and control room. Everything the
//            marker's floor reaches is that room.
//   < >      stairs down and up, to the other floors
//   S        where Sonny gets installed
public static class FloorOneBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/REAL GAME.unity";
    const string LayoutPath = "Assets/_Project/Scenes/Levels/REAL GAME/Floor1Layout.txt";
    const string TilesFolder = "Assets/_Project/Art/Environment/Tilesets/ShipTiles";
    const string MagneticFolder = "Assets/_Project/Audio/SFX/Magnetic Sound fx/Wav";
    const string DoorMarkers = "123456789";
    const string PassageMarkers = "!?$";
    const string PipeWallRooms = "msvc";
    const float ArrivalGap = 1f;        // how far past a doorway's inner edge the player comes out
    const int SpreadFactor = 3;         // how much further apart the rooms are built than they're drawn
    const int FaceRows = 3;             // rows of wall face above a room's floor
    static readonly Vector2 ViewSize = new Vector2(18f, 10f);   // what the camera shows, in tiles

    // Bright and ordinary: this is the station before anything goes wrong.
    static readonly Color AmbientColor = new Color(0.86f, 0.92f, 1f);
    const float AmbientIntensity = 0.85f;
    static readonly Color CeilingGlow = new Color(0.9f, 0.95f, 1f);

    static readonly (char marker, string name)[] RoomNames =
    {
        ('h', "Common Grounds Hallway"),
        ('k', "Common Grounds (Kitchen, Lounge, Bathroom)"),
        ('b', "Common Grounds Bedrooms"),
        ('s', "Storage Room"),
        ('v', "East Hallway"),
        ('m', "Maintenance Deck"),
        ('c', "Hallway to Control Room"),
        ('o', "Control Room"),
    };

    static readonly Vector2Int[] Steps = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    // --- Decorations ---

    // What each kind of piece is drawn on. Furniture and what stands in front of it are solid; the rest isn't. Wall
    // pieces hang on the wall faces, and ceiling pieces are drawn over everything, the player included.
    enum Layer { Rug, Detail, Furniture, Front, Wall, Ceiling }

    // A piece of furniture or floor detail: tiles from the ship tileset, top row first, -1 where there's nothing.
    // A piece can reach up onto the wall above the floor (wallRows); its position is where its first floor row starts.
    // A glow adds a small light, for ceiling lights.
    class Stamp
    {
        public readonly string name;
        public readonly Layer layer;
        public readonly int[][] rows;
        public readonly int wallRows;
        public readonly Color glow;

        public Stamp(string name, Layer layer, int[][] rows, int wallRows = 0, Color glow = default)
        {
            this.name = name;
            this.layer = layer;
            this.rows = rows;
            this.wallRows = wallRows;
            this.glow = glow;
        }
    }

    static readonly Stamp Chair = new Stamp("chair", Layer.Front, new[] { new[] { 89 }, new[] { 120 } });
    static readonly Stamp Bench = new Stamp("bench", Layer.Furniture, new[] { new[] { 15, 16, 17 }, new[] { 39, 40, 41 } });
    static readonly Stamp Sofa = new Stamp("sofa", Layer.Furniture, new[] { new[] { 18, 19, 20 }, new[] { 42, 43, 44 } });
    static readonly Stamp Beds = new Stamp("beds", Layer.Furniture, new[] { new[] { 164, 165 }, new[] { 196, 197 } });
    static readonly Stamp Pod = new Stamp("pod", Layer.Furniture, new[] { new[] { 13, 14 }, new[] { 37, 38 }, new[] { 62, 63 } });
    // Chairs go on columns 1 and 3.
    static readonly Stamp ConsoleBank = new Stamp("console bank", Layer.Furniture, new[]
    {
        new[] { 59, 60, 61, 117, 118, 119 },
        new[] { 88, -1, -1, -1, 148, 149 }
    });
    // Chairs go on columns 1, 4, 5, and 8.
    static readonly Stamp LongConsoleBank = new Stamp("long console bank", Layer.Furniture, new[]
    {
        new[] { 59, 60, 61, 179, 180, 61, 60, 179, 180, 181 },
        new[] { 88, -1, -1, -1, -1, -1, -1, -1, -1, 211 }
    });
    // Chairs go on columns 2 and 5.
    static readonly Stamp Counter = new Stamp("counter", Layer.Furniture, new[]
    {
        new[] { 10, 11, 12, 61, 179, 180, 181, -1 },
        new[] { 35, 36, -1, -1, -1, -1, 211, 149 }
    });
    static readonly Stamp WallPipe = new Stamp("wall pipe", Layer.Furniture, new[] { new[] { 400 }, new[] { 428 }, new[] { 457 } }, wallRows: 1);
    static readonly Stamp DarkPad = new Stamp("table pad", Layer.Detail, new[] { new[] { 186, 187 }, new[] { 216, 217 } });
    static readonly Stamp FloorGrid = new Stamp("floor grid", Layer.Rug, new[] { new[] { 4, 4, 4 }, new[] { 4, 4, 4 } });
    static readonly Stamp Carpet = new Stamp("carpet", Layer.Rug, new[] { new[] { 351, 352, 353 }, new[] { 382, 383, 384 }, new[] { 410, 411, 412 } });
    static readonly Stamp Rug = new Stamp("rug", Layer.Rug, new[] { new[] { 440, 441 }, new[] { 469, 470 } });
    static readonly Stamp CeilingLight = new Stamp("ceiling light", Layer.Ceiling, new[] { new[] { 310 } }, glow: CeilingGlow);
    static readonly Stamp CeilingFixtures = new Stamp("ceiling fixtures", Layer.Ceiling, new[] { new[] { 249, 250, 249, 249, 249, 251, 251, 249, 249 } });

    static Stamp Railing(int length)
    {
        var tiles = new int[length];
        for (int i = 0; i < length; i++) tiles[i] = i == 0 ? 64 : i == length - 1 ? 66 : 65;
        return new Stamp("railing", Layer.Furniture, new[] { tiles });
    }

    // Floor edged in hazard stripes.
    static Stamp HazardFloor(int width, int height)
    {
        var rows = new int[height][];
        for (int y = 0; y < height; y++)
        {
            rows[y] = new int[width];
            for (int x = 0; x < width; x++)
                rows[y][x] = NineSlice(x, y, width, height, 157, 158, 159, 189, 190, 191, 219, 220, 221);
        }
        return new Stamp("hazard floor", Layer.Rug, rows);
    }

    // What goes in each room, from the top-left corner of its floor: x cells right and y rows down. A piece that wouldn't
    // fit on open floor, or would stand in front of a doorway, the stairs, or a marker, is left out with a warning.
    static readonly Dictionary<char, (Stamp stamp, int x, int y)[]> Furnishings = new Dictionary<char, (Stamp, int, int)[]>
    {
        {
            // Map (DEMO)'s lounge: a counter along the top wall, desks with chairs, and sofas side by side.
            'k', new[]
            {
                (Counter, 1, 0), (Chair, 3, 0), (Chair, 6, 0),
                (Bench, 2, 5), (Chair, 3, 6), (Bench, 8, 5), (Chair, 9, 6),
                (Sofa, 15, 7), (Sofa, 18, 7), (Carpet, 16, 3),
                (CeilingLight, 8, 3), (CeilingLight, 22, 3), (CeilingLight, 27, 6),
            }
        },
        {
            'h', new[]
            {
                (Bench, 14, 0), (Bench, 20, 3),
                (CeilingLight, 6, 2), (CeilingLight, 13, 2), (CeilingLight, 20, 2), (CeilingLight, 26, 2),
            }
        },
        {
            'b', new[]
            {
                (Beds, 1, 7), (Beds, 4, 7), (Beds, 7, 7), (Beds, 10, 7), (Beds, 17, 7), (Beds, 20, 7), (Beds, 23, 7), (Beds, 26, 7),
                (Carpet, 14, 3), (Rug, 4, 4), (Rug, 23, 4),
                (CeilingLight, 8, 4), (CeilingLight, 22, 4),
            }
        },
        {
            // Map (DEMO)'s dark storerooms: a grid in the middle of a black floor, a railing, and fixtures overhead.
            's', new[]
            {
                (WallPipe, 4, 0), (WallPipe, 12, 0), (FloorGrid, 5, 3), (Railing(7), 3, 6), (CeilingFixtures, 2, 8),
            }
        },
        {
            'v', new[]
            {
                (CeilingLight, 2, 6), (CeilingLight, 2, 12),
            }
        },
        {
            // The north bay like Map (DEMO)'s control rooms: console banks with chairs, and pods in rows of three. The south
            // bay like its workshops: pipes down the wall, and tables with benches on hazard-striped floor.
            'm', new[]
            {
                (ConsoleBank, 12, 0), (Chair, 13, 0), (Chair, 15, 0), (ConsoleBank, 24, 0), (Chair, 25, 0), (Chair, 27, 0),
                (Pod, 15, 4), (Pod, 19, 4), (Pod, 23, 4), (Pod, 15, 8), (Pod, 19, 8), (Pod, 23, 8),
                (WallPipe, 2, 13), (WallPipe, 7, 13),
                (HazardFloor(14, 9), 14, 18), (DarkPad, 17, 21), (DarkPad, 23, 21),
                (Bench, 16, 19), (Bench, 16, 22), (Bench, 22, 19), (Bench, 22, 22),
                (Railing(5), 24, 30),
                (CeilingLight, 5, 20), (CeilingLight, 5, 28), (CeilingLight, 30, 22), (CeilingLight, 20, 15),
            }
        },
        {
            'c', new[]
            {
                (CeilingLight, 2, 5), (CeilingLight, 8, 5),
            }
        },
        {
            // Consoles under the windows, pods, and a row of desks.
            'o', new[]
            {
                (LongConsoleBank, 1, 0), (Chair, 2, 0), (Chair, 5, 0), (Chair, 6, 0), (Chair, 9, 0),
                (LongConsoleBank, 16, 0), (Chair, 17, 0), (Chair, 20, 0), (Chair, 21, 0), (Chair, 24, 0),
                (Pod, 5, 5), (Pod, 9, 5), (Pod, 16, 5), (Pod, 20, 5),
                (Bench, 4, 12), (Chair, 5, 13), (Bench, 10, 12), (Chair, 11, 13), (Bench, 14, 12), (Chair, 15, 13), (Bench, 20, 12), (Chair, 21, 13),
                (CeilingLight, 7, 9), (CeilingLight, 19, 9),
            }
        },
    };

    // How each room's floor is laid, after Map (DEMO): the tile for a cell, counted from the top-left corner of the room's
    // floor, or -1 to leave the plain grating. Rooms not listed keep the grating.
    static readonly Dictionary<char, System.Func<Room, int, int, int>> FloorKits = new Dictionary<char, System.Func<Room, int, int, int>>
    {
        { 'h', Corridor }, { 'v', Corridor }, { 'c', Corridor },
        { 'k', Lounge }, { 's', DarkFloor }, { 'm', Workshop }, { 'o', Bridge },
    };

    // Plates along the walls and panels between, the long way down a hallway.
    static int Corridor(Room room, int x, int y)
    {
        bool lengthwise = room.Width >= room.Height;
        int across = lengthwise ? y : x, size = lengthwise ? room.Height : room.Width;
        return across == 0 || across == size - 1 ? 370 : 339;
    }

    // Light tiles inside a two-tile border.
    static int Lounge(Room room, int x, int y)
    {
        int right = room.Width - 1, bottom = room.Height - 1;
        if (x == 0 && y == 0) return 232;
        if (x == right && y == 0) return 233;
        if (x == 0 && y == bottom) return 264;
        if (x == right && y == bottom) return 265;
        return Mathf.Min(Mathf.Min(x, y), Mathf.Min(right - x, bottom - y)) <= 1 ? 261 : 201;
    }

    static int DarkFloor(Room room, int x, int y) => NineSlice(x, y, room.Width, room.Height, 5, 6, 7, 30, 31, 32, 54, 55, 56);

    // The maintenance deck: panels with grated walkways across the north bay, and in the south bay a grate floor with
    // grates running down under the wall pipes, crates along the wall at the top, and a row of crates along the bottom.
    static int Workshop(Room room, int x, int y)
    {
        const int northBayEnd = 12, westWallEnd = 9;
        if (y <= northBayEnd) return y <= 1 || y == northBayEnd ? 339 : y == 2 || y == northBayEnd - 1 ? 430 : 370;
        if (y == room.Height - 1) return 402;
        if (x == 2 || x == 7) return 458;
        if (y == northBayEnd + 1 && x <= westWallEnd) return 401;
        return 372;
    }

    // Panels along the walls, walkways inside them, and plates in the middle.
    static int Bridge(Room room, int x, int y)
    {
        int fromBottom = room.Height - 1 - y;
        if (y <= 1 || fromBottom <= 1) return 339;
        return y == 2 || fromBottom == 2 ? 430 : 370;
    }

    // The tile for a cell of an area drawn as corners, edges, and a middle: top-left, top, top-right, left, middle,
    // right, bottom-left, bottom, bottom-right.
    static int NineSlice(int x, int y, int width, int height, params int[] tiles)
    {
        int column = x == 0 ? 0 : x == width - 1 ? 2 : 1;
        int row = y == 0 ? 0 : y == height - 1 ? 2 : 1;
        return tiles[row * 3 + column];
    }

    // Wall faces in the working rooms become machinery: a run of cylinder with an end cap at each side. Rows are the top,
    // middle, and bottom of the three-row face. (Map (DEMO) also breaks runs up with a duct, but in a wall it reads as a
    // doorway, so it's left out.)
    static readonly int[] PipeLeftCap = { 68, 95, 126 };
    static readonly int[] PipeRightCap = { 74, 101, 132 };
    static readonly int[] PipeMiddle = { 69, 96, 127 };        // the first of five that repeat

    class Room
    {
        public char marker;
        public string name;
        public List<Vector2Int> cells;
        public int minX, maxX, minRow, maxRow;

        public int Width => maxX - minX + 1;
        public int Height => maxRow - minRow + 1;
    }

    static readonly Dictionary<int, TileBase> decorTiles = new Dictionary<int, TileBase>();

    [MenuItem("Sonny/Build Floor 1")]
    public static void BuildFromMenu()
    {
        bool rebuild = EditorUtility.DisplayDialog("Build Floor 1",
            "Rebuild REAL GAME.unity from Floor1Layout.txt?\n\nThis replaces everything in the scene. Anything added to it by hand will be lost.",
            "Rebuild", "Cancel");
        if (rebuild && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            Build();
    }

    // For batch mode: Unity -batchmode -projectPath <project> -executeMethod FloorOneBuilder.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build() ? 0 : 1);
    }

    public static bool Build()
    {
        // Everything is loaded before the scene is touched, so a missing asset leaves it as it was.
        var layout = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
        if (layout == null) return Fail($"there's no layout at {LayoutPath}.");
        var compact = new Map(layout.text);
        if (compact.Find('P').Count != 1) return Fail("the layout needs exactly one P, where the player starts.");
        if (!TutorialLevelBuilder.LoadTiles()) return false;
        decorTiles.Clear();

        GameObject playerPrefab = TutorialLevelBuilder.LoadPrefab("Characters/Player 1");
        GameObject lockerPrefab = TutorialLevelBuilder.LoadPrefab("Level/Locker");
        GameObject cameraPrefab = TutorialLevelBuilder.LoadPrefab("Player/MainCamera");
        GameObject globalLightPrefab = TutorialLevelBuilder.LoadPrefab("Systems/GlobalLight2D");
        if (!playerPrefab || !lockerPrefab || !cameraPrefab || !globalLightPrefab) return false;

        Map map = Spread(compact);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        Tilemap floor = TutorialLevelBuilder.BuildTilemaps(map);
        Grid grid = floor.GetComponentInParent<Grid>();

        GameObject player = TutorialLevelBuilder.PlacePlayer(map, playerPrefab);
        TutorialLevelBuilder.PlaceCamera(cameraPrefab, player);
        Light2D globalLight = TutorialLevelBuilder.PlaceGlobalLight(globalLightPrefab);
        globalLight.intensity = AmbientIntensity;
        globalLight.color = AmbientColor;
        TutorialLevelBuilder.Record(globalLight);

        Transform level = new GameObject("Level").transform;
        List<Room> rooms = PlaceRooms(map, TutorialLevelBuilder.Group("Rooms", level));
        var roomOf = new Dictionary<Vector2Int, Room>();
        foreach (Room room in rooms)
            foreach (Vector2Int cell in room.cells)
                roomOf[cell] = room;

        AudioClip doorClip = TutorialLevelBuilder.LoadClip(MagneticFolder, "Magnetic industrial layer02_1.wav");
        int doorways = PlaceDoorways(map, roomOf, TutorialLevelBuilder.Group("Doorways", level), doorClip);
        PlaceStairs(map, TutorialLevelBuilder.Group("Stairs", level));

        // Every lamp steady: nothing has gone wrong yet.
        Transform lights = TutorialLevelBuilder.Group("Lights", level);
        foreach (StationLight lamp in TutorialLevelBuilder.PlaceLamps(map, lights, null))
            lamp.mode = StationLight.Mode.Steady;
        TutorialLevelBuilder.PlaceSunlight(map, lights);
        TutorialLevelBuilder.PlaceLockers(map, lockerPrefab, TutorialLevelBuilder.Group("Lockers", level));
        PlaceMarker(map, 'S', "Sonny Install Point", level);

        PipeWalls(map, roomOf, grid.transform.Find(TutorialLevelBuilder.WallsMap).GetComponent<Tilemap>());
        int pieces = Decorate(map, rooms, grid, lights);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) return Fail($"couldn't save {ScenePath}.");

        Debug.Log($"Floor 1 builder: built {rooms.Count} rooms with {doorways} doorways, {pieces} decorations and {lights.childCount} lights, " +
                  $"and saved {ScenePath}.");
        return true;
    }

    // --- Rooms ---

    // Pulls the rooms apart. Each room, with the wall faces over it, keeps its place in the layout but at three times the
    // distance from the others, so the camera never shows one room from inside another.
    static Map Spread(Map compact)
    {
        var chunks = new List<List<Vector2Int>>();
        var names = new List<string>();
        var owner = new Dictionary<Vector2Int, int>();
        foreach ((char marker, string roomName) in RoomNames)
        {
            List<Vector2Int> found = compact.Find(marker);
            if (found.Count == 0 || owner.ContainsKey(found[0])) continue;     // PlaceRooms says why
            List<Vector2Int> cells = Flood(compact, found[0]);
            foreach (Vector2Int cell in cells) owner[cell] = chunks.Count;
            chunks.Add(cells);
            names.Add(roomName);
        }

        int stray = 0;
        for (int row = 0; row < compact.Height; row++)
        {
            for (int x = 0; x < compact.Width; x++)
            {
                char c = compact.At(x, row);
                if (Map.IsFloor(c) && !owner.ContainsKey(new Vector2Int(x, row))) stray++;
                if (!Map.IsFace(c)) continue;

                // Each wall face goes with the floor it stands over.
                for (int below = 1; below <= FaceRows; below++)
                {
                    if (Map.IsFace(compact.At(x, row + below))) continue;
                    if (owner.TryGetValue(new Vector2Int(x, row + below), out int chunk)) chunks[chunk].Add(new Vector2Int(x, row));
                    break;
                }
            }
        }
        if (stray > 0) Debug.LogWarning($"Floor 1 builder: {stray} floor cells aren't in any room (no room marker reaches them), so they're left out.");

        var placed = new Dictionary<Vector2Int, char>();
        var boxes = new List<RectInt>();
        foreach (List<Vector2Int> cells in chunks)
        {
            var min = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
            var size = new Vector2Int(cells.Max(c => c.x) - min.x + 1, cells.Max(c => c.y) - min.y + 1);
            Vector2Int origin = min * SpreadFactor + Vector2Int.one;
            foreach (Vector2Int cell in cells)
                placed[origin + cell - min] = compact.At(cell.x, cell.y);
            boxes.Add(new RectInt(origin - Vector2Int.one, size + 2 * Vector2Int.one));
        }

        // A room narrower or shorter than the camera's view shows past its walls; check nothing else is there.
        for (int i = 0; i < boxes.Count; i++)
        {
            RectInt seen = boxes[i];
            int extraX = Mathf.Max(0, Mathf.CeilToInt((ViewSize.x - seen.width) * 0.5f));
            int extraY = Mathf.Max(0, Mathf.CeilToInt((ViewSize.y - seen.height) * 0.5f));
            seen = new RectInt(seen.x - extraX, seen.y - extraY, seen.width + 2 * extraX, seen.height + 2 * extraY);
            for (int j = 0; j < boxes.Count; j++)
            {
                if (i != j && seen.Overlaps(boxes[j]))
                    Debug.LogWarning($"Floor 1 builder: the {names[j]} can be seen from the {names[i]}. Space the rooms out more.");
            }
        }

        int width = placed.Keys.Max(p => p.x) + 2, height = placed.Keys.Max(p => p.y) + 2;
        var lines = new char[height][];
        for (int row = 0; row < height; row++)
            lines[row] = Enumerable.Repeat(' ', width).ToArray();
        foreach (KeyValuePair<Vector2Int, char> cell in placed)
            lines[cell.Key.y][cell.Key.x] = cell.Value;
        return new Map(string.Join("\n", lines.Select(line => new string(line))));
    }

    // Each room is the floor its marker can reach. It gets a trigger zone over its floor, and a CameraRoom around its
    // outside walls.
    static List<Room> PlaceRooms(Map map, Transform parent)
    {
        var rooms = new List<Room>();
        var taken = new Dictionary<Vector2Int, string>();
        foreach ((char marker, string roomName) in RoomNames)
        {
            List<Vector2Int> found = map.Find(marker);
            if (found.Count == 0)
            {
                Debug.LogWarning($"Floor 1 builder: there's no {marker} in the layout, so the {roomName} is left out.");
                continue;
            }
            if (taken.TryGetValue(found[0], out string other))
            {
                Debug.LogWarning($"Floor 1 builder: the {roomName}'s floor runs into the {other}'s, so it's left out. Rooms need a wall between them.");
                continue;
            }

            List<Vector2Int> cells = Flood(map, found[0]);
            foreach (Vector2Int cell in cells) taken[cell] = roomName;
            var room = new Room
            {
                marker = marker,
                name = roomName,
                cells = cells,
                minX = cells.Min(c => c.x),
                maxX = cells.Max(c => c.x),
                minRow = cells.Min(c => c.y),
                maxRow = cells.Max(c => c.y)
            };
            rooms.Add(room);

            Rect area = map.WorldRect(room.minX, room.maxX, room.minRow, room.maxRow);
            var roomObject = new GameObject(roomName);
            roomObject.transform.SetParent(parent);
            roomObject.transform.position = area.center;
            var zone = roomObject.AddComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = area.size;
            roomObject.AddComponent<PlayerTriggerZone>();
            // Out to the black wall edge all round, over the faces above the floor.
            roomObject.AddComponent<CameraRoom>().bounds = map.WorldRect(room.minX - 1, room.maxX + 1, room.minRow - FaceRows - 1, room.maxRow + 1);
        }
        return rooms;
    }

    static List<Vector2Int> Flood(Map map, Vector2Int start)
    {
        var cells = new List<Vector2Int> { start };
        var seen = new HashSet<Vector2Int> { start };
        for (int i = 0; i < cells.Count; i++)
        {
            foreach (Vector2Int step in Steps)
            {
                Vector2Int next = cells[i] + step;
                if (Map.IsFloor(map.At(next.x, next.y)) && seen.Add(next)) cells.Add(next);
            }
        }
        return cells;
    }

    // --- Doorways ---

    // Pairs of teleporters: closed doors (digits) and hallways that carry on (symbols), each end sending the player to
    // just inside the other. Then the locked doors (X), which go nowhere.
    static int PlaceDoorways(Map map, Dictionary<Vector2Int, Room> roomOf, Transform parent, AudioClip doorClip)
    {
        int count = 0;
        foreach (char marker in DoorMarkers + PassageMarkers)
        {
            List<List<Vector2Int>> ends = Clusters(map, marker);
            if (ends.Count == 0) continue;
            if (ends.Count != 2)
            {
                Debug.LogWarning($"Floor 1 builder: {marker} marks {ends.Count} doorways, but it needs exactly two, one at each end, so they're left out.");
                continue;
            }

            bool door = DoorMarkers.IndexOf(marker) >= 0;
            var made = new Teleporter[2];
            for (int i = 0; i < 2; i++)
            {
                string from = RoomAt(roomOf, ends[i]), to = RoomAt(roomOf, ends[1 - i]);
                made[i] = MakeDoorway(map, ends[i], parent, $"{(door ? "Door" : "Passage")} {marker} ({from} to {to})",
                    door ? Teleporter.Mode.Interact : Teleporter.Mode.WalkThrough);
                if (door) made[i].sound = doorClip;
            }
            made[0].teleportTarget = made[1].transform.Find("Arrival");
            made[1].teleportTarget = made[0].transform.Find("Arrival");
            count += 2;
        }

        foreach (List<Vector2Int> cells in Clusters(map, 'X'))
        {
            Teleporter entrance = MakeDoorway(map, cells, parent, "Ship Entrance", Teleporter.Mode.Interact);
            entrance.locked = true;
            count++;
        }
        return count;
    }

    // A teleporter over the cells, drawn against the wall beside them, with an Arrival point just inside the room for the
    // other end to send the player to.
    static Teleporter MakeDoorway(Map map, List<Vector2Int> cells, Transform parent, string doorwayName, Teleporter.Mode mode)
    {
        Rect area = Bounds(map, cells);
        Vector2 wall = WallSide(map, cells);

        var doorway = new GameObject(doorwayName);
        doorway.transform.SetParent(parent);
        doorway.transform.position = area.center;
        var trigger = doorway.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = area.size;

        var teleporter = doorway.AddComponent<Teleporter>();
        teleporter.mode = mode;
        teleporter.look = mode == Teleporter.Mode.Interact ? Teleporter.Look.Door : Teleporter.Look.Opening;
        teleporter.wallDirection = wall;

        var arrival = new GameObject("Arrival").transform;
        arrival.SetParent(doorway.transform);
        float halfDepth = Mathf.Abs(Vector2.Dot(area.size * 0.5f, wall));
        arrival.position = area.center - wall * (halfDepth + ArrivalGap);
        return teleporter;
    }

    // The side the wall is on, in world directions: away from the room floor that most of the cells open onto.
    static Vector2 WallSide(Map map, List<Vector2Int> cells)
    {
        var inCluster = new HashSet<Vector2Int>(cells);
        Vector2Int inward = Vector2Int.down;
        int most = -1;
        foreach (Vector2Int step in Steps)
        {
            int open = cells.Count(c => !inCluster.Contains(c + step) && Map.IsFloor(map.At(c.x + step.x, c.y + step.y)));
            if (open <= most) continue;
            most = open;
            inward = step;
        }
        // Map rows count down the screen, so a step down the map is up in the world.
        return new Vector2(-inward.x, inward.y);
    }

    // Trigger zones on the stairs, for whatever takes the player to another floor.
    static void PlaceStairs(Map map, Transform parent)
    {
        foreach ((char marker, string stairsName) in new[] { ('<', "Stairs Down"), ('>', "Stairs Up") })
        {
            foreach (List<Vector2Int> cells in Clusters(map, marker))
            {
                Rect area = Bounds(map, cells);
                var stairs = new GameObject(stairsName);
                stairs.transform.SetParent(parent);
                stairs.transform.position = area.center;
                var zone = stairs.AddComponent<BoxCollider2D>();
                zone.isTrigger = true;
                zone.size = area.size;
                stairs.AddComponent<PlayerTriggerZone>();
            }
        }
    }

    static void PlaceMarker(Map map, char marker, string markerName, Transform parent)
    {
        List<Vector2Int> found = map.Find(marker);
        if (found.Count == 0) return;
        var point = new GameObject(markerName);
        point.transform.SetParent(parent);
        point.transform.position = map.Center(found[0].x, found[0].y);
    }

    // --- Decorating ---

    // Repaints the wall faces over the working rooms' floors as machinery (see PipeLeftCap).
    static void PipeWalls(Map map, Dictionary<Vector2Int, Room> roomOf, Tilemap walls)
    {
        // The plain faces (not windows) standing over those rooms' floor.
        var faces = new HashSet<Vector2Int>();
        for (int row = 0; row < map.Height; row++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                char c = map.At(x, row);
                if (!Map.IsFace(c) || c == '+') continue;
                for (int below = 1; below <= FaceRows; below++)
                {
                    if (Map.IsFace(map.At(x, row + below))) continue;
                    if (roomOf.TryGetValue(new Vector2Int(x, row + below), out Room room) && PipeWallRooms.IndexOf(room.marker) >= 0)
                        faces.Add(new Vector2Int(x, row));
                    break;
                }
            }
        }

        foreach (Vector2Int cell in faces)
        {
            // Which row of the face: counting the face cells below it, top, middle, or bottom.
            int below = 0;
            while (below < FaceRows - 1 && faces.Contains(cell + new Vector2Int(0, below + 1))) below++;
            int band = FaceRows - 1 - below;

            int start = cell.x, end = cell.x;
            while (faces.Contains(new Vector2Int(start - 1, cell.y))) start--;
            while (faces.Contains(new Vector2Int(end + 1, cell.y))) end++;

            int tile;
            if (cell.x == start) tile = PipeLeftCap[band];
            else if (cell.x == end) tile = PipeRightCap[band];
            else tile = PipeMiddle[band] + (cell.x - start - 1) % 5;
            walls.SetTile(map.Cell(cell.x, cell.y), Tile(tile));
        }
    }

    // Lays each room's floor and puts its furnishings in, each kind on its own tilemap. Returns how many pieces went in.
    static int Decorate(Map map, List<Room> rooms, Grid grid, Transform lights)
    {
        Tilemap pattern = NewTilemap(grid, "Floor Pattern", "Floor", 1, false);
        var tilemaps = new Dictionary<Layer, Tilemap>
        {
            { Layer.Rug, NewTilemap(grid, "Rugs", "Floor", 2, false) },
            { Layer.Detail, NewTilemap(grid, "Floor Details", "Floor", 3, false) },
            { Layer.Furniture, NewTilemap(grid, "Furniture", "FloorObject", 0, true) },
            { Layer.Front, NewTilemap(grid, "Furniture Front", "FloorObject", 1, true) },
            { Layer.Wall, NewTilemap(grid, "Wall Details", "Collision", 1, false) },
            { Layer.Ceiling, NewTilemap(grid, "Ceiling", "Top", 0, false) },
        };
        var used = tilemaps.Keys.ToDictionary(layer => layer, layer => new HashSet<Vector2Int>());

        int placed = 0;
        foreach (Room room in rooms)
        {
            var inRoom = new HashSet<Vector2Int>(room.cells);
            if (FloorKits.TryGetValue(room.marker, out System.Func<Room, int, int, int> kit))
            {
                foreach (Vector2Int cell in room.cells)
                {
                    int tile = kit(room, cell.x - room.minX, cell.y - room.minRow);
                    if (tile >= 0) pattern.SetTile(map.Cell(cell.x, cell.y), Tile(tile));
                }
            }

            if (!Furnishings.TryGetValue(room.marker, out (Stamp stamp, int x, int y)[] pieces)) continue;
            foreach ((Stamp stamp, int x, int y) in pieces)
            {
                var corner = new Vector2Int(room.minX + x, room.minRow + y);
                string problem = Blocked(map, inRoom, used, stamp, corner);
                if (problem != null)
                {
                    Debug.LogWarning($"Floor 1 builder: the {stamp.name} at {x}, {y} in the {room.name} {problem}, so it's left out.");
                    continue;
                }

                var floorCells = new List<Vector2Int>();
                foreach ((Vector2Int cell, Layer layer, int tile) in Cells(stamp, corner))
                {
                    tilemaps[layer].SetTile(map.Cell(cell.x, cell.y), Tile(tile));
                    used[layer].Add(cell);
                    if (layer != Layer.Wall) floorCells.Add(cell);
                }

                if (stamp.glow != default && floorCells.Count > 0)
                {
                    Rect area = map.WorldRect(floorCells.Min(c => c.x), floorCells.Max(c => c.x), floorCells.Min(c => c.y), floorCells.Max(c => c.y));
                    var glowObject = new GameObject("Ceiling Light");
                    glowObject.transform.SetParent(lights);
                    glowObject.transform.position = area.center;
                    var glow = glowObject.AddComponent<Light2D>();
                    glow.lightType = Light2D.LightType.Point;
                    glow.color = stamp.glow;
                    glow.intensity = 0.6f;
                    glow.pointLightInnerRadius = 0.3f;
                    glow.pointLightOuterRadius = 3f;
                    glow.falloffIntensity = 0.6f;
                }
                placed++;
            }

            var solid = new HashSet<Vector2Int>(used[Layer.Furniture]);
            solid.UnionWith(used[Layer.Front]);
            CheckReachable(map, room, solid);
        }
        return placed;
    }

    // Each tile of a piece placed at corner: where it goes, on which layer, and which tile.
    static IEnumerable<(Vector2Int cell, Layer layer, int tile)> Cells(Stamp stamp, Vector2Int corner)
    {
        for (int row = 0; row < stamp.rows.Length; row++)
        {
            for (int column = 0; column < stamp.rows[row].Length; column++)
            {
                int tile = stamp.rows[row][column];
                if (tile < 0) continue;
                Layer layer = row < stamp.wallRows ? Layer.Wall : stamp.layer;
                yield return (corner + new Vector2Int(column, row - stamp.wallRows), layer, tile);
            }
        }
    }

    // Why a piece can't go here, or null if it can. Wall tiles need a wall face; everything else needs to be in the room,
    // and on open floor unless it's overhead. Nothing can overlap something already on its layer, and solid pieces can't
    // stand right beside a doorway, the stairs, the start, or Sonny's spot.
    static string Blocked(Map map, HashSet<Vector2Int> inRoom, Dictionary<Layer, HashSet<Vector2Int>> used, Stamp stamp, Vector2Int corner)
    {
        foreach ((Vector2Int cell, Layer layer, int tile) in Cells(stamp, corner))
        {
            char c = map.At(cell.x, cell.y);
            if (used[layer].Contains(cell)) return "overlaps something already there";
            if (layer == Layer.Wall)
            {
                if (!Map.IsFace(c)) return "needs a wall above it";
                continue;
            }
            if (!inRoom.Contains(cell)) return "doesn't fit in the room";
            if (layer == Layer.Ceiling) continue;
            if (!IsOpenFloor(c)) return "isn't all on open floor";
            if (layer != Layer.Furniture && layer != Layer.Front) continue;
            foreach (Vector2Int step in Steps)
            {
                Vector2Int next = cell + step;
                if (IsKeptClear(map.At(next.x, next.y))) return "would stand in the way of a doorway, the stairs, or a marker";
            }
        }
        return null;
    }

    // Warns if furniture cuts anything off: every doorway, stairway, and marker in a room has to be reachable from the
    // others without walking through furniture.
    static void CheckReachable(Map map, Room room, HashSet<Vector2Int> solid)
    {
        List<Vector2Int> targets = room.cells.Where(c => IsKeptClear(map.At(c.x, c.y))).ToList();
        if (targets.Count < 2) return;

        var inRoom = new HashSet<Vector2Int>(room.cells);
        var reached = new HashSet<Vector2Int> { targets[0] };
        var open = new Queue<Vector2Int>();
        open.Enqueue(targets[0]);
        while (open.Count > 0)
        {
            Vector2Int cell = open.Dequeue();
            foreach (Vector2Int step in Steps)
            {
                Vector2Int next = cell + step;
                if (inRoom.Contains(next) && !solid.Contains(next) && reached.Add(next)) open.Enqueue(next);
            }
        }

        int cutOff = targets.Count(t => !reached.Contains(t));
        if (cutOff > 0)
            Debug.LogWarning($"Floor 1 builder: furniture in the {room.name} cuts off {cutOff} doorway, stairs, or marker cells. Move some of it.");
    }

    static bool IsOpenFloor(char c) => c == '.' || RoomNames.Any(room => room.marker == c);

    static bool IsKeptClear(char c) =>
        DoorMarkers.IndexOf(c) >= 0 || PassageMarkers.IndexOf(c) >= 0 || c == 'X' || c == '<' || c == '>' || c == 'P' || c == 'S';

    static Tilemap NewTilemap(Grid grid, string tilemapName, string sortingLayer, int order, bool solid)
    {
        var tilemapObject = new GameObject(tilemapName);
        tilemapObject.transform.SetParent(grid.transform, false);
        var tilemap = tilemapObject.AddComponent<Tilemap>();
        var tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingLayerName = sortingLayer;
        tilemapRenderer.sortingOrder = order;
        if (solid) tilemapObject.AddComponent<TilemapCollider2D>();
        return tilemap;
    }

    static TileBase Tile(int number)
    {
        if (!decorTiles.TryGetValue(number, out TileBase tile))
        {
            tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesFolder}/tileset_{number}.asset");
            if (tile == null) Debug.LogWarning($"Floor 1 builder: tile tileset_{number} is missing from {TilesFolder}.");
            decorTiles[number] = tile;
        }
        return tile;
    }

    // --- Helpers ---

    // The groups of touching cells with this marker.
    static List<List<Vector2Int>> Clusters(Map map, char marker)
    {
        var clusters = new List<List<Vector2Int>>();
        var seen = new HashSet<Vector2Int>();
        foreach (Vector2Int start in map.Find(marker))
        {
            if (!seen.Add(start)) continue;
            var cells = new List<Vector2Int> { start };
            for (int i = 0; i < cells.Count; i++)
            {
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cells[i] + step;
                    if (map.At(next.x, next.y) == marker && seen.Add(next)) cells.Add(next);
                }
            }
            clusters.Add(cells);
        }
        return clusters;
    }

    static Rect Bounds(Map map, List<Vector2Int> cells)
    {
        return map.WorldRect(cells.Min(c => c.x), cells.Max(c => c.x), cells.Min(c => c.y), cells.Max(c => c.y));
    }

    static string RoomAt(Dictionary<Vector2Int, Room> roomOf, List<Vector2Int> cells)
    {
        return roomOf.TryGetValue(cells[0], out Room room) ? room.name : "Nowhere";
    }

    static bool Fail(string message)
    {
        Debug.LogError("Floor 1 builder: " + message);
        return false;
    }
}
