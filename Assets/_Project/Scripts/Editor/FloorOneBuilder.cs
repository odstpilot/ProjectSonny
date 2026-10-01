using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Map = TutorialLevelBuilder.Map;

// Builds the whole station into a chapter's scene (ChapterOneBuilder, ChapterTwoBuilder), both floors as the blueprint
// lays them out. Floor 1 is from the text map in Scenes/Levels/Station/Floor1Layout.txt: the common grounds (kitchen and lounge, restroom,
// hallway, bedrooms) with the ship entrance at the west end, the storage room, the east hallway down to the stairs, the
// maintenance deck, and the hallway up to the control room. Floor 2 (the comms ring, the upper maintenance deck, and the
// reactor core) is upstairs, as the blueprint has it: from Scenes/Levels/Station/Floor2Layout.txt, built above floor 1 in
// the world, far enough off that neither shows from the other, with the stairs up on floor 1 (>) leading to the stairs
// down on floor 2 (<) (Stairway). Every chapter's scene is built
// the same way (ChapterOneBuilder, ChapterTwoBuilder), so the whole station is always there. What differs between
// floors (the map, the rooms, their floors and furniture) is a FloorSpec, below. It uses the Tutorial builder's map format and paints the same tiles, lamps, windows, and
// lockers (see TutorialLevelBuilder), so edit the map and run this again. It replaces everything in the scene but the Sound Manager.
//
// The map is drawn compact, like the blueprint, but the rooms are built bigger and far apart. Each room is stretched to
// about RoomScale times its size by repeating rows and columns of plain floor through its middle (never through a piece
// of furniture, a doorway, or a marker, so what's drawn together stays together, and its furniture keeps its place in
// proportion), and each keeps its place in the layout at four times the distance, so no room can be seen from another.
// The ways between them are Teleporters, which fade the screen through black and put the player on the other side.
// Every room also keeps the camera inside its outside walls (CameraRoom).
//
// Built as drawn instead (StationBlueprintBuilder, for testing), every room is its drawn size and in its drawn place,
// right next to the others as on the blueprint, and the camera goes wherever the player does.
//
// Rooms are dressed the way the rooms on Map (DEMO) are, with the same tiles put together the same way: a floor laid for
// each room (FloorKits), machinery and pipes along the walls of the working rooms (PipeWallRooms), and furniture (Furnishings):
// console banks with chairs, pods in rows, desks, sofas, tables on hazard-striped floor, and railings. Furniture stands on
// the floor the way the characters do, sorted by height on screen, with a shadow under it (DepthDressing), and there's
// shade along the foot of every wall. The light comes from lamps on the walls, not the ceiling.
//
// On top of the Tutorial builder's walls, floor, windows, lamps, and lockers, the map has:
//   P        where the player starts
//   1 - 9    closed doors: the same digit at each end, on the floor cells against the wall the door is in. Walk up to
//            one and press E to come out just inside the other.
//   ! ? $    hallways that carry on: the same symbol at each end, on floor that runs into a dead end. Walking in takes
//            the player straight through.
//   X        a door that stays locked (the ship entrance)
//   k w h b s v m c o   one cell in each room, which names it: kitchen and lounge, restroom, common grounds hallway,
//            bedrooms, storage room, east hallway, maintenance deck, hallway to the control room, and control room. Everything the
//            marker's floor reaches is that room.
//   < >      stairs down and up, to the other floor (Stairway). Each floor's stairs are numbered from the left, and lead to
//            the stairs with the same number on the other floor, as the blueprint lines them up.
//   S        where Sonny gets installed
public static class FloorOneBuilder
{
    const string TilesFolder = "Assets/_Project/Art/Environment/Tilesets/ShipTiles";
    const string DoorMarkers = "123456789";
    const string PassageMarkers = "!?$";
    const float ArrivalGap = 1f;        // how far past a doorway's inner edge the player comes out
    const int SpreadFactor = 4;         // how much further apart the rooms are built than they're drawn
    const float RoomScale = 1.5f;       // how much bigger each room is built than it's drawn
    // The two above, for the build going on: both 1 when it's built as drawn.
    static int spreadFactor = SpreadFactor;
    static float roomScale = RoomScale;
    static bool asDrawn;
    const int FaceRows = 3;             // rows of wall face above a room's floor
    const float CameraSize = 7f;        // half the height the camera shows, in tiles (the prefab's is 5)
    static readonly Vector2 ViewSize = new Vector2(CameraSize * 2f * 16f / 9f, CameraSize * 2f);   // what the camera shows, in tiles

    // Bright and ordinary: this is the station before anything goes wrong. The ambient light is kept low enough that the
    // lamps on the walls show: a warm pool on the floor below each, and a glow on the wall around its fitting.
    static readonly Color AmbientColor = new Color(0.86f, 0.92f, 1f);
    const float AmbientIntensity = 0.62f;
    static readonly Color LampColor = new Color(1f, 0.9f, 0.74f);
    const int LampSpacing = 7;          // the most cells along a wall between one lamp and the next
    const float CharacterZ = 1f;        // where characters and furniture stand

    static readonly (char marker, string name)[] FloorOneRooms =
    {
        ('h', "Common Grounds Hallway"),
        ('k', "Common Grounds (Kitchen, Lounge)"),
        ('w', "Restroom"),
        ('b', "Common Grounds Bedrooms"),
        ('s', "Storage Room"),
        ('v', "East Hallway"),
        ('m', "Maintenance Deck"),
        ('c', "Hallway to Control Room"),
        ('o', "Control Room"),
    };

    static readonly Vector2Int[] Steps = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    // --- Decorations ---

    // What each kind of piece is. Furniture, and what stands in front of it (chairs at a console), are objects that sort
    // by height on screen like the characters do, solid only at their foot (PlaceProp). Rugs and details are laid on the
    // floor, and wall pieces hang on the wall faces.
    enum Layer { Rug, Detail, Furniture, Front, Wall }

    // A piece of furniture or floor detail: tiles from the ship tileset, top row first, -1 where there's nothing.
    // A piece can reach up onto the wall above the floor (wallRows); its position is where its first floor row starts.
    // Where furniture is solid is worked out from its tiles and its name (FurnitureFootprint).
    class Stamp
    {
        public readonly string name;
        public readonly Layer layer;
        public readonly int[][] rows;
        public readonly int wallRows;

        public Stamp(string name, Layer layer, int[][] rows, int wallRows = 0)
        {
            this.name = name;
            this.layer = layer;
            this.rows = rows;
            this.wallRows = wallRows;
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
    static readonly Dictionary<char, (Stamp stamp, int x, int y)[]> FloorOneFurnishings = new Dictionary<char, (Stamp, int, int)[]>
    {
        {
            // Map (DEMO)'s lounge: a counter along the top wall, desks with chairs, and sofas side by side.
            'k', new[]
            {
                (Counter, 1, 0), (Chair, 3, 0), (Chair, 6, 0),
                (Bench, 2, 5), (Chair, 3, 6), (Bench, 8, 5), (Chair, 9, 6),
                (Sofa, 15, 7), (Sofa, 18, 7), (Carpet, 16, 3),
            }
        },
        {
            'h', new[]
            {
                (Bench, 14, 0), (Bench, 20, 3),
            }
        },
        {
            'b', new[]
            {
                (Beds, 1, 7), (Beds, 4, 7), (Beds, 7, 7), (Beds, 10, 7), (Beds, 17, 7), (Beds, 20, 7), (Beds, 23, 7), (Beds, 26, 7),
                (Carpet, 14, 3), (Rug, 4, 4), (Rug, 23, 4),
            }
        },
        {
            // Map (DEMO)'s dark storerooms, cut down to a closet: a pipe on the wall and a grid on the black floor. The
            // shelf (Chapter 1) goes against the back wall.
            's', new[]
            {
                (WallPipe, 3, 0), (FloorGrid, 2, 3),
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
            }
        },
    };

    // How each room's floor is laid, after Map (DEMO): the tile for a cell, counted from the top-left corner of the room's
    // floor, or -1 to leave the plain grating. Rooms not listed keep the grating.
    static readonly Dictionary<char, System.Func<Room, int, int, int>> FloorOneKits = new Dictionary<char, System.Func<Room, int, int, int>>
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

    // --- Floor 2 ---

    static readonly (char marker, string name)[] FloorTwoRooms =
    {
        ('g', "Comms Ring"),
        ('d', "Maintenance Deck (Upper)"),
        ('r', "Reactor Core"),
    };

    static readonly Dictionary<char, (Stamp stamp, int x, int y)[]> FloorTwoFurnishings = new Dictionary<char, (Stamp, int, int)[]>
    {
        {
            // Listening posts: consoles under the windows, desks in rows, and pods.
            'g', new[]
            {
                (LongConsoleBank, 2, 0), (Chair, 3, 0), (Chair, 6, 0), (Chair, 7, 0), (Chair, 10, 0),
                (LongConsoleBank, 31, 0), (Chair, 32, 0), (Chair, 35, 0), (Chair, 36, 0), (Chair, 39, 0),
                (Bench, 6, 9), (Chair, 7, 10), (Bench, 12, 9), (Chair, 13, 10), (Bench, 26, 9), (Chair, 27, 10), (Bench, 32, 9), (Chair, 33, 10),
                (Pod, 8, 16), (Pod, 12, 16), (Pod, 30, 16), (Pod, 34, 16),
                (Carpet, 20, 14), (Rug, 21, 24),
            }
        },
        {
            'd', new[]
            {
                (WallPipe, 9, 0), (WallPipe, 14, 0), (WallPipe, 27, 0),
                (HazardFloor(8, 4), 8, 4), (Bench, 10, 5), (FloorGrid, 21, 5), (Railing(6), 2, 9),
            }
        },
        {
            // The reactor cells in a row along the north wall, the core's hazard floor fenced off below them, and the
            // workshop band along the south with pipes down its wall.
            'r', new[]
            {
                (WallPipe, 50, 0), (WallPipe, 55, 0), (WallPipe, 69, 0), (WallPipe, 74, 0),
                (Pod, 51, 4), (Pod, 55, 4), (Pod, 65, 4), (Pod, 69, 4),
                (HazardFloor(16, 10), 53, 11), (Railing(10), 56, 22),
                (WallPipe, 6, 19), (WallPipe, 16, 19), (WallPipe, 24, 19),
                (HazardFloor(12, 6), 8, 24), (Bench, 10, 25), (FloorGrid, 30, 25),
            }
        },
    };

    static readonly Dictionary<char, System.Func<Room, int, int, int>> FloorTwoKits = new Dictionary<char, System.Func<Room, int, int, int>>
    {
        { 'g', Bridge }, { 'd', ReactorFloor }, { 'r', ReactorFloor },
    };

    // Plates round the edge and grating over the rest.
    static int ReactorFloor(Room room, int x, int y)
    {
        int right = room.Width - 1, bottom = room.Height - 1;
        return x <= 1 || y <= 1 || x >= right - 1 || y >= bottom - 1 ? 339 : 372;
    }

    // --- Floors ---

    // What makes one floor different from another: its map, its rooms and what's in them, and which rooms have machinery
    // along their walls.
    class FloorSpec
    {
        public string name, layoutPath, pipeWallRooms;
        public (char marker, string name)[] rooms;
        public Dictionary<char, (Stamp stamp, int x, int y)[]> furnishings;
        public Dictionary<char, System.Func<Room, int, int, int>> floorKits;
    }

    static readonly FloorSpec FloorOne = new FloorSpec
    {
        name = "Floor 1",
        layoutPath = "Assets/_Project/Scenes/Levels/Station/Floor1Layout.txt",
        pipeWallRooms = "msvc",
        rooms = FloorOneRooms,
        furnishings = FloorOneFurnishings,
        floorKits = FloorOneKits,
    };

    static readonly FloorSpec FloorTwo = new FloorSpec
    {
        name = "Floor 2",
        layoutPath = "Assets/_Project/Scenes/Levels/Station/Floor2Layout.txt",
        pipeWallRooms = "dr",
        rooms = FloorTwoRooms,
        furnishings = FloorTwoFurnishings,
        floorKits = FloorTwoKits,
    };

    // The floor being built. Floor 1 between builds and while a chapter fills the station in, for anything asking about
    // its map (ChapterOneBuilder).
    static FloorSpec spec = FloorOne;

    // Rows of empty space between one floor and the next in the world.
    const int FloorGap = 64;

    internal class Room
    {
        public char marker;
        public string name;
        public List<Vector2Int> cells;
        public int minX, maxX, minRow, maxRow;

        public int Width => maxX - minX + 1;
        public int Height => maxRow - minRow + 1;
    }

    static readonly Dictionary<int, TileBase> decorTiles = new Dictionary<int, TileBase>();

    // What a build of a floor made, for a chapter to fill in (see ChapterOneBuilder).
    internal class BuiltFloor
    {
        public string name;
        public Map map;
        public List<Room> rooms;
        public HashSet<Vector2Int> furniture;   // the cells solid furniture stands on
        public HashSet<Vector2Int> seats;       // of those, the ones with a chair on
        public GameObject player;
        public Transform level;
        public Stairway[] stairs;
    }

    // The whole station, built into the scene at scenePath (made if it isn't there yet): floor 1 where the player starts,
    // and floor 2 upstairs, above it in the world, far enough off that neither is ever seen from the other. Each floor's
    // stairs lead to the other's with the same number. Then populate (if there is one) gets both, floor 1 first, to fill
    // in before it's saved. drawn builds every room at its drawn size, in its drawn place (see the top).
    internal static bool Build(string scenePath, System.Action<BuiltFloor[]> populate, bool drawn = false)
    {
        FloorSpec previous = spec;
        asDrawn = drawn;
        spreadFactor = drawn ? 1 : SpreadFactor;
        roomScale = drawn ? 1f : RoomScale;
        try
        {
            return BuildStation(scenePath, populate);
        }
        finally
        {
            spec = previous;
            asDrawn = false;
            spreadFactor = SpreadFactor;
            roomScale = RoomScale;
        }
    }

    static bool BuildStation(string scenePath, System.Action<BuiltFloor[]> populate)
    {
        // Everything is loaded before the scene is touched, so a missing asset leaves it as it was.
        FloorSpec[] floors = { FloorOne, FloorTwo };
        var compacts = new Map[floors.Length];
        for (int i = 0; i < floors.Length; i++)
        {
            var layout = AssetDatabase.LoadAssetAtPath<TextAsset>(floors[i].layoutPath);
            if (layout == null) return Fail($"there's no layout at {floors[i].layoutPath}.");
            compacts[i] = new Map(layout.text);
        }
        if (compacts[0].Find('P').Count != 1) return Fail("floor 1's layout needs exactly one P, where the player starts.");
        if (!TutorialLevelBuilder.LoadTiles()) return false;
        decorTiles.Clear();
        artSpans.Clear();
        tilesetImages.Clear();

        GameObject playerPrefab = TutorialLevelBuilder.LoadPrefab("Characters/Player 1");
        GameObject lockerPrefab = TutorialLevelBuilder.LoadPrefab("Level/Locker");
        GameObject cameraPrefab = TutorialLevelBuilder.LoadPrefab("Player/MainCamera");
        GameObject globalLightPrefab = TutorialLevelBuilder.LoadPrefab("Systems/GlobalLight2D");
        if (!playerPrefab || !lockerPrefab || !cameraPrefab || !globalLightPrefab) return false;

        Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null
            ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        TutorialLevelBuilder.ClearScene(scene);

        Light2D globalLight = TutorialLevelBuilder.PlaceGlobalLight(globalLightPrefab);
        globalLight.intensity = AmbientIntensity;
        globalLight.color = AmbientColor;
        TutorialLevelBuilder.Record(globalLight);

        var built = new BuiltFloor[floors.Length];
        GameObject player = null;
        float up = 0f;
        for (int i = 0; i < floors.Length; i++)
        {
            spec = floors[i];
            Map map = Spread(compacts[i], out Dictionary<char, RoomStretch> stretches);
            // Each floor after the first is upstairs: above the one before it, with a wide gap between.
            if (i > 0) up += built[i - 1].map.Height + FloorGap;
            map.Origin = new Vector2(0f, up);
            if (i == 0)
            {
                player = TutorialLevelBuilder.PlacePlayer(map, playerPrefab);
                GroundPlayer(player);
                Camera cam = TutorialLevelBuilder.PlaceCamera(cameraPrefab, player);
                cam.orthographicSize = CameraSize;
                TutorialLevelBuilder.Record(cam);
            }
            built[i] = BuildFloor(compacts[i], map, stretches, lockerPrefab, i);
            built[i].player = player;
        }
        spec = FloorOne;

        // Each floor's stairs to the ones with the same number on the other.
        for (int i = 0; i < built.Length; i++)
        {
            BuiltFloor other = built[(i + 1) % built.Length];
            foreach (Stairway stairs in built[i].stairs)
            {
                stairs.destination = other.stairs.FirstOrDefault(s => s.index == stairs.index);
                if (stairs.destination == null)
                    Debug.LogWarning($"Station builder: stairs {stairs.index + 1} on {built[i].name} have nothing with the same number on {other.name} to lead to.");
            }
        }

        SoundDefaults.Fill(scene, SoundDefaults.Player, SoundDefaults.Floor);
        populate?.Invoke(built);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, scenePath)) return Fail($"couldn't save {scenePath}.");
        if (!asDrawn) AddToBuild(scenePath);     // a test scene stays out of the game
        Debug.Log($"Station builder: built {string.Join(" and ", built.Select(f => $"{f.name} ({f.rooms.Count} rooms)"))}, and saved {scenePath}.");
        return true;
    }

    // One floor, where its map's Origin puts it: its tilemaps (their Grid moved there), rooms, doorways, stairs, lamps,
    // lockers, plan for the map screen, and furniture, all under a root of its own.
    static BuiltFloor BuildFloor(Map compact, Map map, Dictionary<char, RoomStretch> stretches, GameObject lockerPrefab, int index)
    {
        Tilemap floor = TutorialLevelBuilder.BuildTilemaps(map);
        Grid grid = floor.GetComponentInParent<Grid>();
        grid.transform.position = map.Origin;
        if (index > 0) grid.name = $"Grid ({spec.name})";

        Transform level = new GameObject(index == 0 ? "Level" : $"Level ({spec.name})").transform;
        List<Room> rooms = PlaceRooms(map, TutorialLevelBuilder.Group("Rooms", level));
        var roomOf = new Dictionary<Vector2Int, Room>();
        foreach (Room room in rooms)
            foreach (Vector2Int cell in room.cells)
                roomOf[cell] = room;

        PlaceDoorways(map, roomOf, TutorialLevelBuilder.Group("Doorways", level));
        Stairway[] stairs = PlaceStairs(map, TutorialLevelBuilder.Group("Stairs", level));

        // Every lamp steady: nothing has gone wrong yet (a chapter that's later on turns some off).
        Transform lights = TutorialLevelBuilder.Group("Lights", level);
        foreach (StationLight lamp in TutorialLevelBuilder.PlaceLamps(WithWallLamps(map), lights, ""))
            LightWall(lamp);
        TutorialLevelBuilder.PlaceSunlight(map, lights);
        TutorialLevelBuilder.PlaceLockers(map, lockerPrefab, TutorialLevelBuilder.Group("Lockers", level));
        PlaceMarker(map, 'S', "Sonny Install Point", level);
        PlaceStationMap(compact, map, rooms, stretches, level);

        PipeWalls(map, roomOf, grid.transform.Find(TutorialLevelBuilder.WallsMap).GetComponent<Tilemap>());
        Decorate(map, rooms, stretches, grid, TutorialLevelBuilder.Group("Furniture", level), out HashSet<Vector2Int> furniture,
            out HashSet<Vector2Int> seats);
        ShadeWalls(map, grid);

        return new BuiltFloor { name = spec.name, map = map, rooms = rooms, furniture = furniture, seats = seats, level = level, stairs = stairs };
    }

    // --- Rooms ---

    // Pulls the rooms apart. Each room, with the wall faces over it, keeps its place in the layout but at three times the
    // distance from the others, so the camera never shows one room from inside another.
    // How a room was stretched, counted from the top-left cell of its floor: for each column and row as it's drawn, where
    // it is now, and for each one now, which one it was drawn as.
    internal class RoomStretch
    {
        public int[] newX, newY, drawnX, drawnY;
        public RectInt drawnFloor;      // the floor as it's drawn in the layout, from its top-left cell


        public int X(int drawn) => At(newX, drawn);
        public int Y(int drawn) => At(newY, drawn);
        public int DrawnX(int now) => At(drawnX, now);
        public int DrawnY(int now) => At(drawnY, now);

        // The room as it's drawn, for the floor kits, which lay their patterns out by the drawn size.
        public Room Drawn => new Room { minX = 0, maxX = newX.Length - 1, minRow = 0, maxRow = newY.Length - 1 };

        static int At(int[] map, int i) => map.Length == 0 ? i : i < 0 ? i : i < map.Length ? map[i] : map[map.Length - 1] + i - (map.Length - 1);
    }

    static Map Spread(Map compact, out Dictionary<char, RoomStretch> stretches)
    {
        var chunks = new List<List<Vector2Int>>();
        var names = new List<string>();
        var markers = new List<char>();
        var owner = new Dictionary<Vector2Int, int>();
        foreach ((char marker, string roomName) in spec.rooms)
        {
            List<Vector2Int> found = compact.Find(marker);
            if (found.Count == 0 || owner.ContainsKey(found[0])) continue;     // PlaceRooms says why
            List<Vector2Int> cells = Flood(compact, found[0]);
            foreach (Vector2Int cell in cells) owner[cell] = chunks.Count;
            chunks.Add(cells);
            names.Add(roomName);
            markers.Add(marker);
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
        if (stray > 0) Debug.LogWarning($"{spec.name} builder: {stray} floor cells aren't in any room (no room marker reaches them), so they're left out.");

        var placed = new Dictionary<Vector2Int, char>();
        var boxes = new List<RectInt>();
        stretches = new Dictionary<char, RoomStretch>();
        for (int i = 0; i < chunks.Count; i++)
        {
            List<Vector2Int> cells = chunks[i];
            var min = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
            var max = new Vector2Int(cells.Max(c => c.x), cells.Max(c => c.y));
            Stretch(compact, cells, markers[i], min, max, out int[] copiesX, out int[] copiesY, out RoomStretch stretch);
            stretches[markers[i]] = stretch;

            // Where each drawn column and row of the chunk starts now.
            int[] startX = Starts(copiesX), startY = Starts(copiesY);
            Vector2Int origin = min * spreadFactor + Vector2Int.one;
            foreach (Vector2Int cell in cells)
            {
                int x = cell.x - min.x, y = cell.y - min.y;
                for (int dx = 0; dx <= copiesX[x]; dx++)
                    for (int dy = 0; dy <= copiesY[y]; dy++)
                        placed[origin + new Vector2Int(startX[x] + dx, startY[y] + dy)] = compact.At(cell.x, cell.y);
            }
            var size = new Vector2Int(startX[startX.Length - 1] + copiesX[copiesX.Length - 1] + 1, startY[startY.Length - 1] + copiesY[copiesY.Length - 1] + 1);
            boxes.Add(new RectInt(origin - Vector2Int.one, size + 2 * Vector2Int.one));
        }

        // A room narrower or shorter than the camera's view shows past its walls; check nothing else is there. (As
        // drawn, they're meant to.)
        for (int i = 0; i < boxes.Count && !asDrawn; i++)
        {
            RectInt seen = boxes[i];
            int extraX = Mathf.Max(0, Mathf.CeilToInt((ViewSize.x - seen.width) * 0.5f));
            int extraY = Mathf.Max(0, Mathf.CeilToInt((ViewSize.y - seen.height) * 0.5f));
            seen = new RectInt(seen.x - extraX, seen.y - extraY, seen.width + 2 * extraX, seen.height + 2 * extraY);
            for (int j = 0; j < boxes.Count; j++)
            {
                if (i != j && seen.Overlaps(boxes[j]))
                    Debug.LogWarning($"{spec.name} builder: the {names[j]} can be seen from the {names[i]}. Space the rooms out more.");
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

    // How many extra copies of each column and row of a chunk (from its top-left corner) make its room about RoomScale
    // times as big. Only rows and columns of nothing but plain floor (and plain wall above it) are repeated, away from
    // the room's edges, and never one a piece of furniture stands on, so pieces drawn together stay together.
    static void Stretch(Map compact, List<Vector2Int> cells, char marker, Vector2Int min, Vector2Int max,
        out int[] copiesX, out int[] copiesY, out RoomStretch stretch)
    {
        var inChunk = new HashSet<Vector2Int>(cells);
        List<Vector2Int> floor = cells.Where(c => Map.IsFloor(compact.At(c.x, c.y))).ToList();
        // Measured from the room's own floor, not stairs let into its wall, so furniture keeps its place.
        List<Vector2Int> ownFloor = floor.Where(c => !IsStairs(compact.At(c.x, c.y))).ToList();
        var floorMin = new Vector2Int(ownFloor.Min(c => c.x), ownFloor.Min(c => c.y));
        var floorMax = new Vector2Int(floor.Max(c => c.x), floor.Max(c => c.y));

        var furnishedX = new HashSet<int>();
        var furnishedY = new HashSet<int>();
        if (spec.furnishings.TryGetValue(marker, out (Stamp stamp, int x, int y)[] pieces))
        {
            foreach ((Stamp stamp, int x, int y) in pieces)
            {
                foreach ((Vector2Int cell, Layer _, int _) in Cells(stamp, floorMin + new Vector2Int(x, y)))
                {
                    furnishedX.Add(cell.x);
                    furnishedY.Add(cell.y);
                }
            }
        }

        char At(int x, int y) => inChunk.Contains(new Vector2Int(x, y)) ? compact.At(x, y) : ' ';
        bool PlainRow(int y) => y >= floorMin.y + 2 && y <= floorMax.y - 2 && !furnishedY.Contains(y) &&
            Enumerable.Range(min.x, max.x - min.x + 1).All(x => At(x, y) == '.' || At(x, y) == ' ') &&
            Enumerable.Range(min.x, max.x - min.x + 1).Any(x => At(x, y) == '.');
        bool PlainColumn(int x) => x >= floorMin.x + 2 && x <= floorMax.x - 2 && !furnishedX.Contains(x) &&
            Enumerable.Range(min.y, max.y - min.y + 1).All(y => At(x, y) == '.' || At(x, y) == '=' || At(x, y) == ' ') &&
            Enumerable.Range(min.y, max.y - min.y + 1).Any(y => At(x, y) == '.');

        copiesX = Copies(Enumerable.Range(min.x, max.x - min.x + 1).Where(PlainColumn).Select(x => x - min.x).ToList(),
            Mathf.RoundToInt((floorMax.x - floorMin.x + 1) * (roomScale - 1f)), max.x - min.x + 1);
        copiesY = Copies(Enumerable.Range(min.y, max.y - min.y + 1).Where(PlainRow).Select(y => y - min.y).ToList(),
            Mathf.RoundToInt((floorMax.y - floorMin.y + 1) * (roomScale - 1f)), max.y - min.y + 1);

        stretch = new RoomStretch
        {
            newX = Offsets(copiesX, floorMin.x - min.x, floorMax.x - min.x, out int[] drawnX),
            newY = Offsets(copiesY, floorMin.y - min.y, floorMax.y - min.y, out int[] drawnY),
            drawnX = drawnX,
            drawnY = drawnY,
            drawnFloor = new RectInt(floorMin, floorMax - floorMin + Vector2Int.one),
        };
    }

    // Extra copies spread evenly over the lines that can take them, doubling up if there are more than lines.
    static int[] Copies(List<int> lines, int extra, int count)
    {
        var copies = new int[count];
        if (lines.Count == 0) return copies;
        for (int k = 0; k < extra; k++)
            copies[lines[(int)((k + 0.5f) * lines.Count / extra) % lines.Count]]++;
        return copies;
    }

    static int[] Starts(int[] copies)
    {
        var starts = new int[copies.Length];
        for (int i = 1; i < copies.Length; i++) starts[i] = starts[i - 1] + copies[i - 1] + 1;
        return starts;
    }

    // For each drawn line of the floor from first to last, where it starts now, counted from the first; and the other
    // way, for each line now, which drawn line it's a copy of.
    static int[] Offsets(int[] copies, int first, int last, out int[] drawn)
    {
        int[] starts = Starts(copies);
        var now = new int[last - first + 1];
        var back = new List<int>();
        for (int i = first; i <= last; i++)
        {
            now[i - first] = starts[i] - starts[first];
            for (int c = 0; c <= copies[i]; c++) back.Add(i - first);
        }
        drawn = back.ToArray();
        return now;
    }

    // Each room is the floor its marker can reach. It gets a trigger zone over its floor, and a CameraRoom around its
    // outside walls.
    static List<Room> PlaceRooms(Map map, Transform parent)
    {
        var rooms = new List<Room>();
        var taken = new Dictionary<Vector2Int, string>();
        foreach ((char marker, string roomName) in spec.rooms)
        {
            List<Vector2Int> found = map.Find(marker);
            if (found.Count == 0)
            {
                Debug.LogWarning($"{spec.name} builder: there's no {marker} in the layout, so the {roomName} is left out.");
                continue;
            }
            if (taken.TryGetValue(found[0], out string other))
            {
                Debug.LogWarning($"{spec.name} builder: the {roomName}'s floor runs into the {other}'s, so it's left out. Rooms need a wall between them.");
                continue;
            }

            List<Vector2Int> cells = Flood(map, found[0]);
            foreach (Vector2Int cell in cells) taken[cell] = roomName;
            // Its top-left corner is its own floor's, not stairs let into its wall: furniture and floors are laid from it.
            List<Vector2Int> ownFloor = cells.Where(c => !IsStairs(map.At(c.x, c.y))).ToList();
            var room = new Room
            {
                marker = marker,
                name = roomName,
                cells = cells,
                minX = ownFloor.Min(c => c.x),
                maxX = cells.Max(c => c.x),
                minRow = ownFloor.Min(c => c.y),
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
            // Out to the black wall edge all round, over the faces above the floor. As drawn, the camera's free to show
            // the rooms around.
            if (!asDrawn) roomObject.AddComponent<CameraRoom>().bounds = map.WorldRect(room.minX - 1, room.maxX + 1, room.minRow - FaceRows - 1, room.maxRow + 1);
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

    // --- The map ---

    // The plan for the map in the technician's suit (StationMap, MapScreen): each room's floor as it's drawn in the
    // layout, the wall between the two ends of each door filled in as the way through, and marks on the doorways, the
    // ship entrance, the stairs, and where Sonny goes in. Each room also keeps how it was stretched, so the player's place
    // in the built room can be found on the drawn one.
    static void PlaceStationMap(Map compact, Map built, List<Room> rooms, Dictionary<char, RoomStretch> stretches, Transform parent)
    {
        var plan = new GameObject("Station Map").AddComponent<StationMap>();
        plan.transform.SetParent(parent);
        plan.floorName = spec.name;
        plan.width = compact.Width;
        plan.height = compact.Height;
        plan.cells = new byte[compact.Width * compact.Height];
        plan.marks = new byte[compact.Width * compact.Height];
        int Index(Vector2Int cell) => cell.y * compact.Width + cell.x;

        foreach (Room room in rooms)
        {
            if (!stretches.TryGetValue(room.marker, out RoomStretch stretch)) continue;
            var drawn = Flood(compact, compact.Find(room.marker)[0]);
            byte value = (byte)(plan.rooms.Count + 1);
            foreach (Vector2Int cell in drawn) plan.cells[Index(cell)] = value;
            plan.rooms.Add(new StationMap.Room
            {
                name = room.name,
                marker = room.marker,
                drawn = stretch.drawnFloor,
                builtX = room.minX,
                builtRow = room.minRow,
                builtHeight = built.Height,
                builtWidth = room.Width,
                builtRows = room.Height,
                drawnColumns = stretch.drawnX,
                drawnRows = stretch.drawnY,
                origin = built.Origin,
            });
        }

        foreach (char marker in DoorMarkers + PassageMarkers)
        {
            List<List<Vector2Int>> ends = Clusters(compact, marker);
            foreach (Vector2Int cell in ends.SelectMany(end => end)) plan.marks[Index(cell)] = (byte)StationMap.Mark.Door;
            if (ends.Count != 2) continue;
            // Two ends facing each other across a wall: the wall between is the way through. Hallways that carry on
            // somewhere else entirely are left as the marks at each end.
            RectInt a = Bounds(ends[0]), b = Bounds(ends[1]);
            bool acrossX = a.yMin < b.yMax && b.yMin < a.yMax;
            bool acrossY = a.xMin < b.xMax && b.xMin < a.xMax;
            if (!acrossX && !acrossY) continue;
            int xMin = acrossY ? Mathf.Max(a.xMin, b.xMin) : Mathf.Min(a.xMin, b.xMin);
            int xMax = acrossY ? Mathf.Min(a.xMax, b.xMax) : Mathf.Max(a.xMax, b.xMax);
            int yMin = acrossX ? Mathf.Max(a.yMin, b.yMin) : Mathf.Min(a.yMin, b.yMin);
            int yMax = acrossX ? Mathf.Min(a.yMax, b.yMax) : Mathf.Max(a.yMax, b.yMax);
            for (int y = yMin; y < yMax; y++)
                for (int x = xMin; x < xMax; x++)
                    if (plan.cells[Index(new Vector2Int(x, y))] == StationMap.Empty) plan.cells[Index(new Vector2Int(x, y))] = StationMap.DoorGap;
        }

        foreach (Vector2Int cell in compact.Find('X')) plan.marks[Index(cell)] = (byte)StationMap.Mark.Locked;
        foreach (Vector2Int cell in compact.Find('<').Concat(compact.Find('>'))) plan.marks[Index(cell)] = (byte)StationMap.Mark.Stairs;
        foreach (Vector2Int cell in compact.Find('S')) plan.marks[Index(cell)] = (byte)StationMap.Mark.Sonny;
        TutorialLevelBuilder.Record(plan);
    }

    static RectInt Bounds(List<Vector2Int> cells)
    {
        int xMin = cells.Min(c => c.x), yMin = cells.Min(c => c.y);
        return new RectInt(xMin, yMin, cells.Max(c => c.x) - xMin + 1, cells.Max(c => c.y) - yMin + 1);
    }

    // --- Doorways ---

    // Pairs of teleporters: closed doors (digits) and hallways that carry on (symbols), each end sending the player to
    // just inside the other. Then the locked doors (X), which go nowhere.
    static int PlaceDoorways(Map map, Dictionary<Vector2Int, Room> roomOf, Transform parent)
    {
        int count = 0;
        foreach (char marker in DoorMarkers + PassageMarkers)
        {
            List<List<Vector2Int>> ends = Clusters(map, marker);
            if (ends.Count == 0) continue;
            if (ends.Count != 2)
            {
                Debug.LogWarning($"{spec.name} builder: {marker} marks {ends.Count} doorways, but it needs exactly two, one at each end, so they're left out.");
                continue;
            }

            bool door = DoorMarkers.IndexOf(marker) >= 0;
            var made = new Teleporter[2];
            for (int i = 0; i < 2; i++)
            {
                string from = RoomAt(roomOf, ends[i]), to = RoomAt(roomOf, ends[1 - i]);
                made[i] = MakeDoorway(map, ends[i], parent, $"{(door ? "Door" : "Passage")} {marker} ({from} to {to})",
                    door ? Teleporter.Mode.Interact : Teleporter.Mode.WalkThrough);
                if (door) made[i].sound = "Door";
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

    // The stairs to the other floor (Stairway), numbered from the left the way the blueprint lines them up, each with the
    // spot on the floor beside it where the player comes out.
    static Stairway[] PlaceStairs(Map map, Transform parent)
    {
        var made = new List<Stairway>();
        var all = new List<(List<Vector2Int> cells, string name)>();
        foreach ((char marker, string stairsName) in new[] { ('<', "Stairs Down"), ('>', "Stairs Up") })
            foreach (List<Vector2Int> cells in Clusters(map, marker))
                all.Add((cells, stairsName));
        all.Sort((a, b) => a.cells.Average(c => c.x).CompareTo(b.cells.Average(c => c.x)));

        for (int i = 0; i < all.Count; i++)
        {
            (List<Vector2Int> cells, string stairsName) = all[i];
            Rect area = Bounds(map, cells);
            var stairs = new GameObject($"{stairsName} {i + 1}");
            stairs.transform.SetParent(parent);
            stairs.transform.position = area.center;
            var zone = stairs.AddComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = area.size;
            stairs.AddComponent<PlayerTriggerZone>();

            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(stairs.transform);
            arrival.position = StairsArrival(map, cells, area.center);
            var stairway = stairs.AddComponent<Stairway>();
            stairway.index = i;
            stairway.arrival = arrival;
            made.Add(stairway);
        }
        return made.ToArray();
    }

    // A couple of cells out onto the floor from the side of the stairs the floor's on.
    static Vector2 StairsArrival(Map map, List<Vector2Int> cells, Vector2 center)
    {
        var stairs = new HashSet<Vector2Int>(cells);
        Vector2 sum = Vector2.zero;
        int count = 0;
        foreach (Vector2Int cell in cells)
        {
            foreach (Vector2Int step in Steps)
            {
                Vector2Int next = cell + step;
                if (stairs.Contains(next) || !IsOpenFloor(map.At(next.x, next.y))) continue;
                sum += map.Center(next.x, next.y);
                count++;
            }
        }
        if (count == 0) return center;
        Vector2 edge = sum / count;
        return edge + (edge - center).normalized * 1.5f;
    }

    // So the stairs can load it.
    static void AddToBuild(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(scene => scene.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
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
                    if (roomOf.TryGetValue(new Vector2Int(x, row + below), out Room room) && spec.pipeWallRooms.IndexOf(room.marker) >= 0)
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

    // Lays each room's floor and puts its furnishings in: rugs, details, and wall pieces on tilemaps of their own, and
    // furniture as objects under props. Returns how many pieces went in, and the cells the furniture stands on.
    static int Decorate(Map map, List<Room> rooms, Dictionary<char, RoomStretch> stretches, Grid grid, Transform props,
        out HashSet<Vector2Int> furniture, out HashSet<Vector2Int> seats)
    {
        Tilemap pattern = NewTilemap(grid, "Floor Pattern", "Floor", 1, false);
        var tilemaps = new Dictionary<Layer, Tilemap>
        {
            { Layer.Rug, NewTilemap(grid, "Rugs", "Floor", 2, false) },
            { Layer.Detail, NewTilemap(grid, "Floor Details", "Floor", 3, false) },
            { Layer.Wall, NewTilemap(grid, "Wall Details", "Collision", 1, false) },
        };
        var used = System.Enum.GetValues(typeof(Layer)).Cast<Layer>().ToDictionary(layer => layer, layer => new HashSet<Vector2Int>());
        var nooks = new HashSet<Vector2Int>();

        int placed = 0;
        foreach (Room room in rooms)
        {
            var inRoom = new HashSet<Vector2Int>(room.cells);
            RoomStretch stretch = stretches.TryGetValue(room.marker, out RoomStretch found) ? found : new RoomStretch
            {
                newX = new int[0], newY = new int[0], drawnX = new int[0], drawnY = new int[0]
            };
            if (spec.floorKits.TryGetValue(room.marker, out System.Func<Room, int, int, int> kit))
            {
                // Laid out as drawn: each repeated line of floor gets the pattern of the line it repeats.
                Room drawn = stretch.newX.Length > 0 ? stretch.Drawn : room;
                foreach (Vector2Int cell in room.cells)
                {
                    int tile = kit(drawn, stretch.DrawnX(cell.x - room.minX), stretch.DrawnY(cell.y - room.minRow));
                    if (tile >= 0) pattern.SetTile(map.Cell(cell.x, cell.y), Tile(tile));
                }
            }

            if (!spec.furnishings.TryGetValue(room.marker, out (Stamp stamp, int x, int y)[] pieces)) continue;
            foreach ((Stamp stamp, int x, int y) in pieces)
            {
                var corner = new Vector2Int(room.minX + stretch.X(x), room.minRow + stretch.Y(y));
                string problem = Blocked(map, inRoom, used, stamp, corner);
                if (problem != null)
                {
                    Debug.LogWarning($"{spec.name} builder: the {stamp.name} at {x}, {y} in the {room.name} {problem}, so it's left out.");
                    continue;
                }

                var standing = new List<(Vector2Int cell, int tile)>();
                foreach ((Vector2Int cell, Layer layer, int tile) in Cells(stamp, corner))
                {
                    used[layer].Add(cell);
                    if (layer == Layer.Furniture || layer == Layer.Front) standing.Add((cell, tile));
                    else tilemaps[layer].SetTile(map.Cell(cell.x, cell.y), Tile(tile));
                }
                if (standing.Count > 0) PlaceProp(map, stamp, standing, props);
                if (stamp.layer == Layer.Furniture) nooks.UnionWith(Nooks(stamp, corner));
                placed++;
            }

            var solid = new HashSet<Vector2Int>(used[Layer.Furniture]);
            solid.UnionWith(used[Layer.Front]);
            CheckReachable(map, room, solid);
        }

        furniture = new HashSet<Vector2Int>(used[Layer.Furniture]);
        furniture.UnionWith(used[Layer.Front]);
        furniture.UnionWith(nooks);
        seats = new HashSet<Vector2Int>(used[Layer.Front]);
        return placed;
    }

    // The empty cells inside a piece on the floor: the knee space under a console or counter, where the chairs go. Nobody
    // stands or walks about in there, though a chair can go in.
    static IEnumerable<Vector2Int> Nooks(Stamp stamp, Vector2Int corner)
    {
        for (int row = stamp.wallRows; row < stamp.rows.Length; row++)
            for (int column = 0; column < stamp.rows[row].Length; column++)
                if (stamp.rows[row][column] < 0) yield return corner + new Vector2Int(column, row - stamp.wallRows);
    }

    // A piece of furniture as one object that sorts by where it stands, the way characters do (DepthSort): anyone lower
    // on screen than its front edge is drawn in front of it, anyone higher up behind it. Only the part of it standing on
    // the floor is solid (FurnitureFootprint), so the player can walk right up to it and behind a tall piece, and it has
    // a shadow under it. A piece standing in front of another (a chair at a console) sorts a hair in front of it.
    static void PlaceProp(Map map, Stamp stamp, List<(Vector2Int cell, int tile)> cells, Transform parent)
    {
        int minX = cells.Min(c => c.cell.x), baseRow = cells.Max(c => c.cell.y);
        float lift = DepthSort.FeetToCenter - (stamp.layer == Layer.Front ? 0.05f : 0f);
        float front = map.Origin.y + map.Height - 1 - baseRow;

        // The object sits at its sorting point; everything in it is placed down from there.
        var prop = new GameObject(char.ToUpperInvariant(stamp.name[0]) + stamp.name.Substring(1));
        prop.transform.SetParent(parent);
        prop.transform.position = new Vector3(map.Origin.x + minX, front + lift, CharacterZ);
        DepthSort.Group(prop);

        foreach ((Vector2Int cell, int tile) in cells)
        {
            Sprite sprite = (Tile(tile) as Tile)?.sprite;
            if (sprite == null) continue;
            var piece = new GameObject("Tile").AddComponent<SpriteRenderer>();
            piece.transform.SetParent(prop.transform, false);
            piece.transform.localPosition = new Vector3(cell.x - minX + 0.5f, baseRow - cell.y + 0.5f - lift, 0f);
            piece.sprite = sprite;
            piece.sortingLayerName = DepthSort.Layer;
        }

        FurnitureFootprint.AddColliders(prop, stamp.name, stamp.rows, stamp.wallRows, lift);

        // The shadow is where the art meets the floor, a column at a time: under a console, along the front of the desk
        // and under its two ends, not across the knee space in front of it where the chairs go. Each one is only as wide
        // as the art, so it doesn't run out past an end piece that's mostly empty tile.
        Sprite shadowSprite = DepthDressing.FurnitureShadow;
        if (shadowSprite == null) return;
        foreach ((int start, int end, int lowest) in FurnitureFootprint.Runs(stamp.rows, stamp.wallRows))
        {
            int row = stamp.rows.Length - 1 - lowest;
            float left = start, right = end + 1;
            if (ArtSpan(stamp.rows[row][start]) is Vector2 first) left = start + first.x;
            if (ArtSpan(stamp.rows[row][end]) is Vector2 last) right = end + last.y;
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(prop.transform, false);
            shadow.transform.localPosition = new Vector3((left + right) * 0.5f, lowest + 0.04f - lift, 0f);
            shadow.sprite = shadowSprite;
            shadow.drawMode = SpriteDrawMode.Sliced;
            shadow.size = new Vector2(right - left + 0.1f, 0.4f);
            shadow.sortingLayerName = DepthSort.Layer;
            shadow.sortingOrder = -1;
        }
    }

    // How far across a tile its art reaches, from its left edge, as a fraction of the tile: (left, right), or null if it
    // can't tell. The end pieces of a console slant down to the floor, so it's the whole tile, not only its bottom edge.
    // Read from the tileset image itself, which isn't imported readable.
    static readonly Dictionary<int, Vector2?> artSpans = new Dictionary<int, Vector2?>();
    static readonly Dictionary<string, Texture2D> tilesetImages = new Dictionary<string, Texture2D>();
    static Vector2? ArtSpan(int number)
    {
        if (artSpans.TryGetValue(number, out Vector2? known)) return known;
        Vector2? span = null;
        Sprite sprite = (Tile(number) as Tile)?.sprite;
        // The sprite's own image, not sprite.texture: that's the atlas the tiles are packed into.
        string path = sprite != null ? AssetDatabase.GetAssetPath(sprite) : null;
        if (!string.IsNullOrEmpty(path))
        {
            if (!tilesetImages.TryGetValue(path, out Texture2D image))
            {
                image = new Texture2D(2, 2);
                if (!image.LoadImage(System.IO.File.ReadAllBytes(path))) image = null;
                tilesetImages[path] = image;
            }
            Rect rect = sprite.rect;
            if (image != null && rect.xMax <= image.width && rect.yMax <= image.height)
            {
                int minX = int.MaxValue, maxX = -1;
                for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
                    for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                        if (image.GetPixel(x, y).a > 0.5f) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
                if (maxX >= 0) span = new Vector2((minX - rect.xMin) / rect.width, (maxX + 1 - rect.xMin) / rect.width);
            }
        }
        artSpans[number] = span;
        return span;
    }

    // The map with more lamps ('^') along its walls, so no stretch of wall runs more than LampSpacing cells without one.
    // They go on the middle row of a wall face over a room's floor, never on a window, and never right at the end of a
    // wall. The layout's own lamps stay where they are.
    static Map WithWallLamps(Map map)
    {
        var rows = new char[map.Height][];
        for (int row = 0; row < map.Height; row++)
        {
            rows[row] = new char[map.Width];
            for (int x = 0; x < map.Width; x++) rows[row][x] = map.At(x, row);
        }

        bool IsLamp(char c) => c == '^' || c == '_';
        bool MiddleOfFace(int x, int row) =>
            Map.IsFace(map.At(x, row)) && map.At(x, row) != '+' && Map.IsFace(map.At(x, row - 1)) &&
            Map.IsFace(map.At(x, row + 1)) && Map.IsFloor(map.At(x, row + 2));

        for (int row = 0; row < map.Height; row++)
        {
            int x = 0;
            while (x < map.Width)
            {
                if (!MiddleOfFace(x, row)) { x++; continue; }
                int start = x;
                while (x < map.Width && MiddleOfFace(x, row)) x++;
                int end = x - 1;

                // Along the run, a lamp wherever the last one is too far back and the next one isn't close ahead.
                int last = int.MinValue / 2;
                for (int cell = start; cell <= end; cell++)
                {
                    if (IsLamp(rows[row][cell])) { last = cell; continue; }
                    if (cell - start < 2 || end - cell < 2 || cell - last < LampSpacing) continue;
                    bool lampAhead = false;
                    for (int ahead = 1; ahead <= LampSpacing / 2 && cell + ahead <= end; ahead++)
                        lampAhead |= IsLamp(rows[row][cell + ahead]);
                    if (lampAhead) continue;
                    rows[row][cell] = '^';
                    last = cell;
                }
            }
        }
        return new Map(string.Join("\n", rows.Select(line => new string(line)))) { Origin = map.Origin };
    }

    // A lamp on the wall as the station's lighting: warm white, a pool of it on the floor below, and a glow spilling
    // over the wall around the fitting, so the light reads as coming from the lamp.
    static void LightWall(StationLight lamp)
    {
        lamp.mode = StationLight.Mode.Steady;
        var pool = lamp.GetComponent<Light2D>();
        pool.color = LampColor;
        pool.intensity = 1.1f;

        var wash = new GameObject("Wall Glow").AddComponent<Light2D>();
        wash.transform.SetParent(lamp.transform);
        wash.transform.position = lamp.FixturePoint;
        wash.lightType = Light2D.LightType.Point;
        wash.color = LampColor;
        wash.intensity = 0.8f;
        wash.pointLightInnerRadius = 0.2f;
        wash.pointLightOuterRadius = 2.4f;
        wash.falloffIntensity = 0.7f;
    }

    // Shade along the floor at the foot of every wall face, as if the wall stood up out of it.
    static void ShadeWalls(Map map, Grid grid)
    {
        TileBase shade = DepthDressing.WallShadeTile;
        if (shade == null) return;
        Tilemap tilemap = NewTilemap(grid, "Wall Shade", "Floor", 4, false);
        for (int row = 0; row < map.Height; row++)
            for (int x = 0; x < map.Width; x++)
                if (Map.IsFloor(map.At(x, row)) && Map.IsFace(map.At(x, row - 1)))
                    tilemap.SetTile(map.Cell(x, row), shade);
    }

    // The player stands on the floor like everyone else: solid only at the feet, a shadow under them, and (unless
    // keepLantern, for the dark of the tutorial) no lantern, since the station's own lights are on.
    internal static void GroundPlayer(GameObject player, bool keepLantern = false)
    {
        if (!keepLantern)
            foreach (Light2D lantern in player.GetComponentsInChildren<Light2D>(true))
            {
                lantern.enabled = false;
                TutorialLevelBuilder.Record(lantern);
            }

        player.transform.position += new Vector3(0f, DepthDressing.FeetLift, 0f);
        TutorialLevelBuilder.Record(player.transform);

        var sprite = player.GetComponent<SpriteRenderer>();
        if (sprite == null || sprite.sprite == null) return;
        if (player.TryGetComponent(out BoxCollider2D body))
        {
            DepthDressing.SetFootprint(body, sprite.sprite);
            TutorialLevelBuilder.Record(body);
        }
        DepthDressing.AddShadow(player, sprite.sprite);
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
            Debug.LogWarning($"{spec.name} builder: furniture in the {room.name} cuts off {cutOff} doorway, stairs, or marker cells. Move some of it.");
    }

    static bool IsStairs(char c) => c == '<' || c == '>';

    internal static bool IsOpenFloor(char c) => c == '.' || spec.rooms.Any(room => room.marker == c);

    internal static bool IsKeptClear(char c) =>
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
            if (tile == null) Debug.LogWarning($"{spec.name} builder: tile tileset_{number} is missing from {TilesFolder}.");
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
        Debug.LogError($"{spec.name} builder: " + message);
        return false;
    }
}
