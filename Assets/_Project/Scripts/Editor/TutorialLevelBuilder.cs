using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using NavMeshPlus.Components;

// Menu: Sonny > Build Tutorial Level
//
// Rebuilds Scenes/Levels/Tutorial.unity from the text map in Scenes/Levels/Tutorial/TutorialLayout.txt, one character
// per tile, and puts the Tutorial first in the build. Edit the map, run this again, and the whole level is rebuilt to
// match: walls, floor, doors, robots, lighting, trigger zones, and the TutorialDirector that runs it. It replaces
// everything in the scene, so change the map, prefabs, or scripts rather than the scene itself, at least until the layout
// is settled.
// FloorOneBuilder builds REAL GAME.unity from a map in the same format, with the tiles and pieces painted here.
//
// The map:
//   (space)  nothing. Walls are drawn around the edge of everything else.
//   .        floor
//   =        wall face: the 3 rows of wall seen above a floor
//   +        window in a wall face, 3 wide. Orange sunlight falls through it onto the floor.
//   :        collapsed floor: a hole with a rail around it that stops walking but not shots
//   P        where the player starts
//   M H A    robots: the melee lesson, the patrol, the arena
//   W        a robot that breaks through the wall above it partway through the arena fight
//   L        locker, against the wall above it
//   R        the reactor door, standing open with the reactor's light pouring out: where the player comes from
//   Z        robots chasing the player out of the reactor at the start, until the ceiling comes down on them
//            (the H patrol walks the length of its room to the H zone's doorway and stands guard there, watching the
//            room: it can't be fought, only hidden from and crept past)
//   S        Sonny's processing box
//   1 - 4    blast doors: melee room exit, arena entrance (starts open), arena exit, control room
//   c r m g h a f e   zones the director waits for the player to walk into: the collapse, run, melee, the crossing of
//            the collapsed floor, hide, arena, final hallway, and the reveal. Mark a line of cells across the way in.
//   ^        lamp, on a wall face      _  a lamp that's already broken, spitting sparks
//   *        alarm lamp, on a wall face: off until the last hallway
//   %        loose wreckage            &  a solid pile of wreckage
//            (each piece is drawn from its position: hull panels, insulation foil, pipe, grates, wiring, electronics, odds and ends)
//   ~        a stain on the floor: a scorch mark, or coolant leaked from a burst line
//   furniture from the ship tileset, the marker on its bottom-left cell, running right from there. The 2D renderer sorts
//   by height on screen, so it can stand anywhere, against a wall or out in a room, with the player walking round it.
//   K        a bank of monitors (2 wide), which Sonny can take over      k  a control desk (2 wide)
//   Q        a console bank (6 wide)       C  a chair       T  a bench (3 wide)       B  a sofa (3 wide)
//   O        a pod (2 wide, 3 tall)        I  a pipe running down the wall above it to the floor
//
// The grid is laid out the way Map (DEMO)'s is (Prefabs/Level/Grids & Maps/DEMO LAYOUT): floor, Objects on Floor,
// Collision and Walls, InvisibleColumn (the solid black past the walls), Very Top (overhead, drawn over the player), and
// Decoration, each on the same sorting layer and marked for the NavMesh the same way. Each solid tilemap's colliders are
// merged into one shape, so nothing snags on the seams between tiles.
// Each stretch of the level is dressed from what's in it, the way Map (DEMO)'s rooms are: hallways get plated floors
// and ceiling lights, the melee room a dark floor, the arena a hazard-striped one, the control room walkways, and the
// rooms with fights or a patrol machinery along their top wall.
public static class TutorialLevelBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Tutorial.unity";
    const string LayoutPath = "Assets/_Project/Scenes/Levels/Tutorial/TutorialLayout.txt";
    const string PostProcessingPath = "Assets/_Project/Scenes/Levels/Tutorial/TutorialPostProcessing.asset";
    const string TilesFolder = "Assets/_Project/Art/Environment/Tilesets/ShipTiles";
    const string PrefabsFolder = "Assets/_Project/Prefabs";
    const string SfxFolder = "Assets/_Project/Audio/SFX";
    const string MagneticFolder = "Assets/_Project/Audio/SFX/Magnetic Sound fx/Wav";
    const int IgnoreRaycastLayer = 2;
    const float CharacterZ = 1f;    // where the character prefabs sit
    const float LampDrop = 3.5f;    // a lamp on a wall face lights the floor this far below it

    // Warm, dim, and failing: amber ambient light, sodium lamps, and orange sunlight from the side facing the flare.
    static readonly Color AmbientColor = new Color(1f, 0.68f, 0.45f);
    static readonly Color LampColor = new Color(1f, 0.72f, 0.36f);
    static readonly Color SunColor = new Color(1f, 0.5f, 0.18f);
    static readonly Color AlarmColor = new Color(1f, 0.2f, 0.06f);
    static readonly Color ReactorColor = new Color(0.45f, 0.85f, 1f);
    static readonly Color LanternColor = new Color(1f, 0.84f, 0.62f);
    static readonly Color CeilingLightColor = new Color(1f, 0.8f, 0.55f);

    // The grid's tilemaps, named as Map (DEMO)'s are.
    internal const string FloorMap = "floor";
    internal const string OnFloorMap = "Objects on Floor";
    internal const string WallsMap = "Collision and Walls";
    internal const string SolidMap = "InvisibleColumn";
    internal const string OverheadMap = "Very Top";
    internal const string DecorationMap = "Decoration";
    const int SolidDepth = 2;       // cells of solid black past the walls

    // Tile numbers in the ship tileset (ShipTiles/tileset_N).
    internal static class Tiles
    {
        // Floor: grating, framed in metal plates wherever it meets a wall.
        public const int Floor = 377, FloorN = 346, FloorS = 405, FloorW = 376, FloorE = 378;
        public const int FloorNW = 345, FloorNE = 347, FloorSW = 404, FloorSE = 406;
        // Walls: black, with a grey edge on each side that faces the room, and a corner notch where only a diagonal does.
        public const int EdgeN = 257, EdgeS = 319, EdgeW = 288, EdgeE = 290;
        public const int EdgeNW = 256, EdgeNE = 258, EdgeSW = 318, EdgeSE = 320;
        public const int NotchNE = 291, NotchNW = 292, NotchSE = 259, NotchSW = 260, Solid = 289;
        // Wall faces as [top, middle, bottom]: plain, with a vent, with a sign, and a window's left, middle, and right.
        public static readonly int[] FacePlain = { 75, 104, 135 };
        public static readonly int[] FaceVent = { 76, 105, 136 };
        public static readonly int[] FaceSign = { 77, 106, 137 };
        public static readonly int[] WindowLeft = { 166, 198, 226 };
        public static readonly int[] WindowMiddle = { 167, 199, 227 };
        public static readonly int[] WindowRight = { 168, 200, 228 };
        // A strip of red lights, used as the alarm lamp's fixture.
        public const int AlarmFixture = 128;

        public static IEnumerable<int> All()
        {
            int[] single =
            {
                Floor, FloorN, FloorS, FloorW, FloorE, FloorNW, FloorNE, FloorSW, FloorSE,
                EdgeN, EdgeS, EdgeW, EdgeE, EdgeNW, EdgeNE, EdgeSW, EdgeSE, NotchNE, NotchNW, NotchSE, NotchSW, Solid,
                AlarmFixture
            };
            return single.Concat(FacePlain).Concat(FaceVent).Concat(FaceSign).Concat(WindowLeft).Concat(WindowMiddle).Concat(WindowRight);
        }
    }

    // The text map. Cells are (column, row) with row 0 at the top; in the world, row r is at y = Height - 1 - r.
    internal class Map
    {
        readonly string[] rows;
        public readonly int Width;
        public readonly int Height;

        public Map(string text)
        {
            List<string> lines = text.Replace("\r", "").Split('\n').ToList();
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
                lines.RemoveAt(lines.Count - 1);
            rows = lines.ToArray();
            Height = rows.Length;
            Width = Height == 0 ? 0 : rows.Max(row => row.Length);
        }

        public char At(int x, int row) => row >= 0 && row < Height && x >= 0 && x < rows[row].Length ? rows[row][x] : ' ';
        public Vector3Int Cell(int x, int row) => new Vector3Int(x, Height - 1 - row, 0);
        public Vector2 Center(int x, int row) => new Vector2(x + 0.5f, Height - 1 - row + 0.5f);

        public List<Vector2Int> Find(char marker)
        {
            var found = new List<Vector2Int>();
            for (int row = 0; row < Height; row++)
                for (int x = 0; x < rows[row].Length; x++)
                    if (rows[row][x] == marker) found.Add(new Vector2Int(x, row));
            return found;
        }

        // The world rectangle around every cell with this marker.
        public bool TryGetArea(char marker, out Rect area)
        {
            List<Vector2Int> cells = Find(marker);
            area = default;
            if (cells.Count == 0) return false;
            area = WorldRect(cells.Min(c => c.x), cells.Max(c => c.x), cells.Min(c => c.y), cells.Max(c => c.y));
            return true;
        }

        public Rect WorldRect(int minX, int maxX, int minRow, int maxRow) => Rect.MinMaxRect(minX, Height - 1 - maxRow, maxX + 1, Height - minRow);

        public static bool IsVoid(char c) => c == ' ';
        public static bool IsFace(char c) => c == '=' || c == '+' || c == '^' || c == '_' || c == '*';
        public static bool IsHole(char c) => c == ':';
        public static bool IsFloor(char c) => !IsVoid(c) && !IsFace(c) && !IsHole(c);
    }

    static readonly Dictionary<int, TileBase> tiles = new Dictionary<int, TileBase>();

    [MenuItem("Sonny/Build Tutorial Level")]
    public static void BuildFromMenu()
    {
        bool rebuild = EditorUtility.DisplayDialog("Build Tutorial Level",
            "Rebuild Tutorial.unity from TutorialLayout.txt?\n\nThis replaces everything in the scene. Anything added to it by hand will be lost.",
            "Rebuild", "Cancel");
        if (rebuild && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            Build();
    }

    // For batch mode: Unity -batchmode -projectPath <project> -executeMethod TutorialLevelBuilder.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build() ? 0 : 1);
    }

    public static bool Build()
    {
        // Everything is loaded before the scene is touched, so a missing asset leaves it as it was.
        var layout = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
        if (layout == null) return Fail($"there's no layout at {LayoutPath}.");
        var map = new Map(layout.text);
        if (map.Find('P').Count != 1) return Fail("the layout needs exactly one P, where the player starts.");
        if (!LoadTiles()) return false;

        GameObject playerPrefab = LoadPrefab("Characters/Player 1");
        GameObject robotPrefab = LoadPrefab("Characters/PlaceholderRobot");
        GameObject lockerPrefab = LoadPrefab("Level/Locker");
        GameObject cameraPrefab = LoadPrefab("Player/MainCamera");
        GameObject globalLightPrefab = LoadPrefab("Systems/GlobalLight2D");
        if (!playerPrefab || !robotPrefab || !lockerPrefab || !cameraPrefab || !globalLightPrefab) return false;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        Tilemap floor = BuildTilemaps(map);
        Grid grid = floor.GetComponentInParent<Grid>();

        GameObject player = PlacePlayer(map, playerPrefab);
        PlaceCamera(cameraPrefab, player);
        Light2D globalLight = PlaceGlobalLight(globalLightPrefab);
        StationRumble rumble = PlaceRumble(floor, globalLight);
        PlacePostProcessing();

        Transform level = new GameObject("Level").transform;
        Transform zones = Group("Zones", level);
        Transform doors = Group("Doors", level);
        Transform robots = Group("Robots", level);
        Transform props = Group("Props", level);
        Transform lights = Group("Lights", level);

        var director = new GameObject("Tutorial Director").AddComponent<TutorialDirector>();
        director.rumble = rumble;
        director.alarmClip = LoadClip(SfxFolder, "316847__lalks__alarm-04-short.wav");
        director.heartbeatClip = LoadClip(SfxFolder, "heartbeat.wav");

        director.collapseZone = PlaceZone(map, 'c', "Collapse Zone", zones);
        director.runZone = PlaceZone(map, 'r', "Run Zone", zones);
        director.meleeZone = PlaceZone(map, 'm', "Melee Zone", zones);
        director.crossingZone = PlaceZone(map, 'g', "Crossing Zone", zones);
        director.hideZone = PlaceZone(map, 'h', "Hide Zone", zones);
        director.arenaZone = PlaceZone(map, 'a', "Arena Zone", zones);
        director.finalZone = PlaceZone(map, 'f', "Final Hallway Zone", zones);
        director.revealZone = PlaceZone(map, 'e', "Reveal Zone", zones);

        AudioClip doorClip = LoadClip(MagneticFolder, "Magnetic industrial layer02_1.wav");
        director.meleeExit = PlaceDoor(map, '1', "Melee Room Exit", doors, doorClip);
        director.arenaEntrance = PlaceDoor(map, '2', "Arena Entrance", doors, doorClip);
        director.arenaExit = PlaceDoor(map, '3', "Arena Exit", doors, doorClip);
        director.controlRoomDoor = PlaceDoor(map, '4', "Control Room Door", doors, doorClip);
        PlaceReactorDoor(map, doors, lights, doorClip);
        if (director.arenaEntrance != null) director.arenaEntrance.startsOpen = true;
        if (director.controlRoomDoor != null) director.controlRoomDoor.openWhenPlayerWithin = 3.5f;

        AudioClip wakeClip = LoadClip(SfxFolder, "746988__gammagool__robot-awakening-power-on (1).wav");
        director.meleeRobots = PlaceRobots(map, 'M', "Melee Robot", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            StandGuard(robot, 9f);
        });
        // The patrol walks the room to just inside its doorway and turns to watch it. It sees only a short way while it
        // walks, so there's time to hide; the director widens that once it stands guard. Crouching cuts it to a third.
        float postX = map.TryGetArea('h', out Rect hideDoorway) ? hideDoorway.xMax + 0.5f : 0f;
        director.patrolRobots = PlaceRobots(map, 'H', "Patrol", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            robot.patrolRoute = new[] { new Vector2(postX - robot.transform.position.x, 0f) };
            robot.patrolSpeed = 1.3f;
            robot.patrolPause = 0.5f;
            robot.sightRange = 7f;
            robot.fieldOfView = 100f;
            robot.crouchSightMultiplier = 0.35f;
            robot.idleFacing = Vector2.right;
            health.cannotDie = true;
        });
        director.chasers = PlaceChasers(map, robotPrefab, Group("Chasers", robots));
        director.arenaRobots = PlaceRobots(map, 'A', "Arena Robots", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            StandGuard(robot, 14f);
        });
        // Waits switched off, out of sight behind the wall, until the director blows the wall in.
        director.breachRobots = PlaceRobots(map, 'W', "Breach Robot", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            StandGuard(robot, 16f);
            robot.gameObject.SetActive(false);
        });
        director.breachPoint = PlaceBreachPoint(map, props);

        director.collapsePoints = PlaceCollapsePoints(map, props);
        director.lamps = PlaceLamps(map, lights, LoadClip(SfxFolder, "636578__swag1773__cutting-power.wav"));
        director.alarms = PlaceAlarms(map, lights);
        PlaceSunlight(map, lights);
        director.sonny = PlaceSonny(map, props);
        PlaceLockers(map, lockerPrefab, Group("Lockers", level));
        PlaceFurniture(map, Group("Furniture", level));
        PlaceRubble(map, Group("Rubble", level));
        PlaceStains(map, Group("Stains", level));
        DressLevel(map, grid, lights);
        PlaceRailings(map, props);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) return Fail($"couldn't save {ScenePath}.");
        AddSceneToBuild();

        Debug.Log($"Tutorial builder: built a {map.Width} by {map.Height} level with {robots.GetComponentsInChildren<PlaceholderRobot>().Length} robots, " +
                  $"{doors.childCount} doors, {zones.childCount} zones and {lights.childCount} lights, and saved {ScenePath}.");
        return true;
    }

    // --- Tiles ---

    // The tilemaps, named and layered like Map (DEMO)'s grid, with the walls, floor, and the hole in it drawn in. Returns
    // the floor.
    internal static Tilemap BuildTilemaps(Map map)
    {
        var grid = new GameObject("Grid").AddComponent<Grid>();
        Tilemap floor = CreateTilemap(grid, FloorMap, "Floor", Walkable.Yes);
        Tilemap onFloor = CreateTilemap(grid, OnFloorMap, "FloorObject", Walkable.Yes);
        Tilemap walls = CreateTilemap(grid, WallsMap, "Collision", Walkable.No);
        Tilemap solid = CreateTilemap(grid, SolidMap, "Collision", Walkable.No);
        CreateTilemap(grid, OverheadMap, "Top", Walkable.Yes);
        CreateTilemap(grid, DecorationMap, "Decor", Walkable.Unmarked);

        // The walls close one cell past every edge, and solid black runs on a little way past them.
        for (int row = -1 - SolidDepth; row <= map.Height + SolidDepth; row++)
        {
            for (int x = -1 - SolidDepth; x <= map.Width + SolidDepth; x++)
            {
                char c = map.At(x, row);
                Vector3Int cell = map.Cell(x, row);
                if (Map.IsFloor(c))
                    floor.SetTile(cell, tiles[FloorTile(map, x, row)]);
                else if (Map.IsFace(c))
                    walls.SetTile(cell, tiles[FaceTile(map, x, row)]);
                else if (Map.IsHole(c))
                    onFloor.SetTile(cell, tiles[EdgeTile(map, x, row, Map.IsFloor) ?? Tiles.Solid]);
                else if (EdgeTile(map, x, row, ch => !Map.IsVoid(ch)) is int edge)
                    walls.SetTile(cell, tiles[edge]);
                else if (NearSomething(map, x, row, SolidDepth + 1))
                    solid.SetTile(cell, tiles[Tiles.Solid]);
            }
        }

        MakeSolid(walls);
        MakeSolid(solid);
        return floor;
    }

    enum Walkable { Yes, No, Unmarked }

    // Sorting order 0 on every one, as on Map (DEMO); the sorting layers keep them in order. Marked for the NavMesh as
    // walkable or not, the same way too.
    static Tilemap CreateTilemap(Grid grid, string tilemapName, string sortingLayer, Walkable walkable)
    {
        var go = new GameObject(tilemapName);
        go.transform.SetParent(grid.transform, false);
        var tilemap = go.AddComponent<Tilemap>();
        var tilemapRenderer = go.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingLayerName = sortingLayer;
        tilemapRenderer.sortingOrder = 0;
        if (walkable != Walkable.Unmarked)
        {
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = walkable == Walkable.Yes ? 0 : 1;   // Walkable, Not Walkable
        }
        return tilemap;
    }

    // Every tile's collider merged into one shape, so something sliding along a wall doesn't catch on the joins.
    static void MakeSolid(Tilemap tilemap)
    {
        GameObject go = tilemap.gameObject;
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var composite = go.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        go.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        composite.GenerateGeometry();
    }

    // Anything but empty space within reach cells, counting diagonals.
    static bool NearSomething(Map map, int x, int row, int reach)
    {
        for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
                if (!Map.IsVoid(map.At(x + dx, row + dy))) return true;
        return false;
    }

    // Grating, with a plate along each side that meets a wall or a hole.
    static int FloorTile(Map map, int x, int row)
    {
        bool n = !Map.IsFloor(map.At(x, row - 1));
        bool s = !Map.IsFloor(map.At(x, row + 1));
        bool w = !Map.IsFloor(map.At(x - 1, row));
        bool e = !Map.IsFloor(map.At(x + 1, row));
        if (n && w) return Tiles.FloorNW;
        if (n && e) return Tiles.FloorNE;
        if (s && w) return Tiles.FloorSW;
        if (s && e) return Tiles.FloorSE;
        if (n) return Tiles.FloorN;
        if (s) return Tiles.FloorS;
        if (w) return Tiles.FloorW;
        if (e) return Tiles.FloorE;
        return Tiles.Floor;
    }

    // Black, edged on the sides that face an open cell. Null when nothing open is next to it, not even diagonally.
    static int? EdgeTile(Map map, int x, int row, System.Func<char, bool> isOpen)
    {
        bool n = isOpen(map.At(x, row - 1));
        bool s = isOpen(map.At(x, row + 1));
        bool w = isOpen(map.At(x - 1, row));
        bool e = isOpen(map.At(x + 1, row));
        if (n && w) return Tiles.EdgeNW;
        if (n && e) return Tiles.EdgeNE;
        if (s && w) return Tiles.EdgeSW;
        if (s && e) return Tiles.EdgeSE;
        if (n) return Tiles.EdgeN;
        if (s) return Tiles.EdgeS;
        if (w) return Tiles.EdgeW;
        if (e) return Tiles.EdgeE;
        if (isOpen(map.At(x + 1, row - 1))) return Tiles.NotchNE;
        if (isOpen(map.At(x - 1, row - 1))) return Tiles.NotchNW;
        if (isOpen(map.At(x + 1, row + 1))) return Tiles.NotchSE;
        if (isOpen(map.At(x - 1, row + 1))) return Tiles.NotchSW;
        return null;
    }

    static int FaceTile(Map map, int x, int row)
    {
        // Which row of the face this is, counting the wall cells below it: bottom, middle, or top.
        int below = 0;
        while (below < 2 && Map.IsFace(map.At(x, row + 1 + below))) below++;
        int band = 2 - below;

        if (map.At(x, row) == '+')
        {
            bool left = map.At(x - 1, row) == '+';
            bool right = map.At(x + 1, row) == '+';
            return (left && right ? Tiles.WindowMiddle : right ? Tiles.WindowLeft : Tiles.WindowRight)[band];
        }

        // Mostly plain, with the odd vent or sign, the same every build.
        int pick = (x * 7349 + 13) % 11;
        return (pick == 3 ? Tiles.FaceVent : pick == 7 ? Tiles.FaceSign : Tiles.FacePlain)[band];
    }

    // --- The player and what's always around them ---

    internal static GameObject PlacePlayer(Map map, GameObject prefab)
    {
        Vector2Int start = map.Find('P')[0];
        Vector2 spawn = map.Center(start.x, start.y);
        GameObject player = Spawn(prefab, null, new Vector3(spawn.x, spawn.y, CharacterZ));

        var controller = player.GetComponent<PlayerController>();
        controller.footstepClips = LoadClips(SfxFolder, "footstep1.wav", "footstep2.wav", "footstep3.wav", "footstep4.wav");
        Record(controller);

        var footsteps = player.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.spatialBlend = 0f;
        footsteps.volume = 0.3f;
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("audioSource").objectReferenceValue = footsteps;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        // A small, dim, warm glow, so the lamps and the sunlight do the lighting and the dark between them stays dark.
        var lantern = player.GetComponentInChildren<Light2D>();
        if (lantern != null)
        {
            lantern.pointLightOuterRadius = 5.5f;
            lantern.pointLightInnerRadius = 0.4f;
            lantern.intensity = 0.75f;
            lantern.color = LanternColor;
            Record(lantern);
        }
        return player;
    }

    internal static Camera PlaceCamera(GameObject prefab, GameObject player)
    {
        Vector3 at = player.transform.position;
        GameObject cameraObject = Spawn(prefab, null, new Vector3(at.x, at.y, at.z - 5f));
        cameraObject.tag = "MainCamera";
        Record(cameraObject);

        var follow = cameraObject.GetComponent<CameraFallow>();
        follow.player = player.transform;
        Record(follow);

        var cam = cameraObject.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        Record(cam);

        // Post-processing is off on the prefab.
        var cameraData = cam.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask = ~0;
        Record(cameraData);
        return cam;
    }

    internal static Light2D PlaceGlobalLight(GameObject prefab)
    {
        var globalLight = Spawn(prefab, null, Vector3.zero).GetComponent<Light2D>();
        globalLight.intensity = 0.38f;
        globalLight.color = AmbientColor;
        LightEveryLayer(globalLight);
        Record(globalLight);
        return globalLight;
    }

    // A light that reaches every sorting layer. Left to the prefab's list, anything on a layer it misses (characters,
    // furniture, decoration) stays pitch black until some other light comes near it.
    internal static void LightEveryLayer(Light2D light)
    {
        var serialized = new SerializedObject(light);
        SerializedProperty layers = serialized.FindProperty("m_ApplyToSortingLayers");
        SortingLayer[] all = SortingLayer.layers;
        layers.arraySize = all.Length;
        for (int i = 0; i < all.Length; i++) layers.GetArrayElementAtIndex(i).intValue = all[i].id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static StationRumble PlaceRumble(Tilemap floor, Light2D globalLight)
    {
        var rumble = new GameObject("Station Rumble").AddComponent<StationRumble>();
        // Now and then one piece, some way off and mostly ahead, rather than showers of it around the player.
        rumble.secondsBetween = new Vector2(14f, 22f);
        rumble.debrisPerRumble = new Vector2Int(1, 1);
        rumble.debrisMinDistance = 4f;
        rumble.debrisMaxDistance = 8f;
        rumble.aimAtPlayerChance = 0f;
        rumble.aheadChance = 0.75f;
        rumble.floor = floor;
        rumble.globalLight = globalLight;
        rumble.rumbleClips = LoadClips(MagneticFolder, "Magnetic bass ground.wav", "Magnetic bass once 01.wav", "Magnetic bass once 02.wav", "Magnetic bass full.wav");
        rumble.impactClips = LoadClips(MagneticFolder, "Magnetic hit 01.wav", "Magnetic hit 02.wav", "Magnetic hit 03.wav", "Magnetic hit 04.wav");
        rumble.ambientLoop = LoadClip(SfxFolder, "700008__newlocknew__scimisc_low-steady-hum-2_em.wav");
        return rumble;
    }

    // A glow on anything bright, warm grading with a bit of contrast, dark corners, and a little film grain.
    // The profile is saved next to the layout and updated in place, so it keeps the same asset between builds.
    static void PlacePostProcessing()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessingPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessingPath);
        }

        Bloom bloom = Effect<Bloom>(profile);
        bloom.threshold.Override(0.75f);
        bloom.intensity.Override(1.3f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.86f, 0.7f));

        ColorAdjustments grade = Effect<ColorAdjustments>(profile);
        grade.postExposure.Override(0.2f);
        grade.contrast.Override(18f);
        grade.saturation.Override(-6f);
        grade.colorFilter.Override(new Color(1f, 0.94f, 0.86f));

        Vignette vignette = Effect<Vignette>(profile);
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(new Color(0.07f, 0.03f, 0.01f));

        FilmGrain grain = Effect<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.2f);
        grain.response.Override(0.8f);

        Effect<ChromaticAberration>(profile).intensity.Override(0.06f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volume = new GameObject("Post Processing").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // The profile's effect of this kind, added into the asset if it isn't there yet.
    static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T effect)) return effect;
        effect = profile.Add<T>();
        effect.name = typeof(T).Name;
        effect.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(effect, profile);
        return effect;
    }

    // --- Level pieces ---

    static PlayerTriggerZone PlaceZone(Map map, char marker, string zoneName, Transform parent)
    {
        if (!map.TryGetArea(marker, out Rect area)) return null;
        var zone = new GameObject(zoneName);
        zone.transform.SetParent(parent);
        zone.transform.position = area.center;
        var trigger = zone.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = area.size;
        return zone.AddComponent<PlayerTriggerZone>();
    }

    static BlastDoor PlaceDoor(Map map, char marker, string doorName, Transform parent, AudioClip clip)
    {
        if (!map.TryGetArea(marker, out Rect area)) return null;
        var doorObject = new GameObject(doorName);
        doorObject.transform.SetParent(parent);
        doorObject.transform.position = area.center;
        doorObject.AddComponent<BoxCollider2D>().size = area.size;
        var door = doorObject.AddComponent<BlastDoor>();
        door.moveClip = clip;
        return door;
    }

    static RobotEncounter PlaceRobots(Map map, char marker, string groupName, Transform parent, GameObject prefab, AudioClip wakeClip,
        System.Action<PlaceholderRobot, Health> setUp)
    {
        List<Vector2Int> cells = map.Find(marker);
        if (cells.Count == 0) return null;

        Transform group = Group(groupName, parent);
        foreach (Vector2Int at in cells)
        {
            Vector2 center = map.Center(at.x, at.y);
            GameObject robot = Spawn(prefab, group, new Vector3(center.x, center.y + 0.15f, CharacterZ));
            var brain = robot.GetComponent<PlaceholderRobot>();
            var health = robot.GetComponent<Health>();
            setUp(brain, health);
            Record(brain);
            Record(health);
        }

        var encounter = group.gameObject.AddComponent<RobotEncounter>();
        encounter.wakeClip = wakeClip;
        // Listed, rather than found when the scene starts, so robots switched off until later are counted too.
        encounter.robots = group.GetComponentsInChildren<Health>(true).ToList();
        return encounter;
    }

    // Robots for the opening, run by the director rather than by their own senses, so they start switched off.
    static List<PlaceholderRobot> PlaceChasers(Map map, GameObject prefab, Transform parent)
    {
        var chasers = new List<PlaceholderRobot>();
        foreach (Vector2Int at in map.Find('Z'))
        {
            Vector2 center = map.Center(at.x, at.y);
            var brain = Spawn(prefab, parent, new Vector3(center.x, center.y + 0.15f, CharacterZ)).GetComponent<PlaceholderRobot>();
            brain.enabled = false;
            brain.patrolRoute = new Vector2[0];
            Record(brain);
            chasers.Add(brain);
        }
        return chasers;
    }

    // The way into the reactor, jammed open, its light pouring out: a slow cold pulse, and a haze filling the doorway.
    static void PlaceReactorDoor(Map map, Transform doors, Transform lights, AudioClip clip)
    {
        BlastDoor door = PlaceDoor(map, 'R', "Reactor Door", doors, clip);
        if (door == null) return;
        door.startsOpen = true;
        door.locked = true;

        map.TryGetArea('R', out Rect doorway);
        Light2D glow = NewLight("Reactor Glow", lights, doorway.center + new Vector2(0.4f, 0f), ReactorColor, 2.2f, 7f);
        glow.volumetricEnabled = true;
        glow.volumeIntensity = 0.2f;
        var pulse = glow.gameObject.AddComponent<StationLight>();
        pulse.mode = StationLight.Mode.Alarm;
        pulse.pulseRate = 0.3f;
        pulse.breakable = false;
        pulse.rumbleStutter = 0.2f;

        var haze = new GameObject("Reactor Haze").AddComponent<SpriteRenderer>();
        haze.transform.SetParent(glow.transform);
        haze.transform.position = doorway.center;
        haze.transform.localScale = new Vector3(2.5f, doorway.height * 1.3f, 1f);
        haze.sprite = CombatSprites.Glow;
        haze.color = new Color(ReactorColor.r, ReactorColor.g, ReactorColor.b, 0.5f);
        haze.sortingLayerName = "Collision";
        haze.sortingOrder = 7;
        if (CombatSprites.EffectMaterial != null) haze.sharedMaterial = CombatSprites.EffectMaterial;
    }

    // Stands still, and once awake sees all the way around.
    static void StandGuard(PlaceholderRobot robot, float sightRange)
    {
        robot.patrolRoute = new Vector2[0];
        robot.fieldOfView = 360f;
        robot.sightRange = sightRange;
    }

    // The wall face above the breach robot, where it bursts through.
    static Transform PlaceBreachPoint(Map map, Transform parent)
    {
        List<Vector2Int> cells = map.Find('W');
        if (cells.Count == 0) return null;
        Transform point = Group("Breach Point", parent);
        point.position = map.Center(cells[0].x, cells[0].y) + new Vector2(0f, 1.6f);
        return point;
    }

    // Where the ceiling comes down behind the player: a column of points a little way back from the collapse zone.
    static Transform[] PlaceCollapsePoints(Map map, Transform parent)
    {
        if (!map.TryGetArea('c', out Rect zone)) return new Transform[0];

        Transform group = Group("Collapse Points", parent);
        var points = new List<Transform>();
        for (float y = zone.yMin + 0.5f; y < zone.yMax; y += 1f)
        {
            Transform point = Group("Collapse Point", group);
            point.position = new Vector3(zone.xMin - 2.5f, y, 0f);
            points.Add(point);
        }
        return points.ToArray();
    }

    // Sodium lamps on the walls, each lighting a warm pool of floor below it. Some flicker; the '_' ones are already smashed.
    internal static List<StationLight> PlaceLamps(Map map, Transform parent, AudioClip breakClip)
    {
        var lamps = new List<StationLight>();
        foreach (char marker in new[] { '^', '_' })
        {
            foreach (Vector2Int at in map.Find(marker))
            {
                Vector2 wall = map.Center(at.x, at.y);
                Light2D light = NewLight(marker == '_' ? "Broken Lamp" : "Lamp", parent, wall + new Vector2(0f, -LampDrop), LampColor, 1.2f, 6.5f);
                light.volumetricEnabled = true;
                light.volumeIntensity = 0.05f;

                var lamp = light.gameObject.AddComponent<StationLight>();
                lamp.mode = lamps.Count % 4 == 2 ? StationLight.Mode.Flicker : StationLight.Mode.Steady;
                lamp.startBroken = marker == '_';
                lamp.drawFixture = true;
                lamp.fixtureOffset = new Vector2(0f, LampDrop);
                lamp.breakClip = breakClip;
                // Every third one hangs loose enough to swing when the station shakes, and any can fall when smashed.
                if (lamps.Count % 3 == 1) lamp.swing = 0.45f;
                lamp.dropOnBreak = 0.6f;
                lamps.Add(lamp);
            }
        }
        return lamps;
    }

    // Red alarm lamps, off until the director switches them on.
    static List<StationLight> PlaceAlarms(Map map, Transform parent)
    {
        var alarms = new List<StationLight>();
        foreach (Vector2Int at in map.Find('*'))
        {
            Vector2 wall = map.Center(at.x, at.y);
            Light2D light = NewLight("Alarm Lamp", parent, wall + new Vector2(0f, -2f), AlarmColor, 1.4f, 5f);
            light.volumetricEnabled = true;
            light.volumeIntensity = 0.08f;

            var fixture = new GameObject("Fixture").AddComponent<SpriteRenderer>();
            fixture.transform.SetParent(light.transform);
            fixture.transform.position = wall;
            fixture.sprite = (tiles[Tiles.AlarmFixture] as Tile)?.sprite;
            fixture.sortingLayerName = "Collision";
            fixture.sortingOrder = 2;

            var alarm = light.gameObject.AddComponent<StationLight>();
            alarm.mode = StationLight.Mode.Alarm;
            alarm.startOn = false;
            alarm.breakable = false;
            alarm.bulb = fixture;
            alarms.Add(alarm);
        }
        return alarms;
    }

    // Sunlight through every window: a slanting shaft of orange light across the floor below, and glare in the glass.
    internal static void PlaceSunlight(Map map, Transform parent)
    {
        for (int row = 0; row < map.Height; row++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                // Start from the bottom-left cell of each window.
                if (map.At(x, row) != '+' || map.At(x - 1, row) == '+' || !Map.IsFloor(map.At(x, row + 1))) continue;

                int width = 0;
                while (map.At(x + width, row) == '+') width++;
                int depth = 0;
                while (depth < 16 && Map.IsFloor(map.At(x + width / 2, row + 1 + depth))) depth++;
                float slant = depth * 0.45f;

                // Anchored at the bottom-left corner of the window.
                var sun = new GameObject("Sunlight");
                sun.transform.SetParent(parent);
                sun.transform.position = new Vector3(x, map.Height - 1 - row, 0f);

                var light = sun.AddComponent<Light2D>();
                light.SetShapePath(new[]
                {
                    new Vector3(0.1f, 1.8f), new Vector3(width - 0.1f, 1.8f),
                    new Vector3(width + slant + 0.8f, -depth), new Vector3(slant - 0.3f, -depth)
                });
                light.lightType = Light2D.LightType.Freeform;
                light.shapeLightFalloffSize = 1.2f;
                light.color = SunColor;
                light.intensity = 0.4f;
                light.falloffIntensity = 1f;
                light.volumetricEnabled = true;
                light.volumeIntensity = 0.12f;

                var station = sun.AddComponent<StationLight>();
                station.mode = StationLight.Mode.Sunlight;
                station.breakable = false;
                station.rumbleStutter = 0.25f;
                station.drawFixture = true;
                station.fixtureOffset = new Vector2(width * 0.5f, 1.5f);
                station.windowSize = new Vector2(width, 3f);
            }
        }
    }

    static SonnyBox PlaceSonny(Map map, Transform parent)
    {
        List<Vector2Int> cells = map.Find('S');
        if (cells.Count == 0) return null;

        Vector2 center = map.Center(cells[0].x, cells[0].y);
        var box = new GameObject("Sonny");
        box.transform.SetParent(parent);
        box.transform.position = new Vector3(center.x, center.y - 0.5f, CharacterZ);
        var footprint = box.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(1.4f, 0.8f);
        footprint.offset = new Vector2(0f, 0.4f);

        var sonny = box.AddComponent<SonnyBox>();
        sonny.glow = NewLight("Glow", box.transform, center + new Vector2(0f, 0.9f), new Color(1f, 0.15f, 0.1f), 1.5f, 8f);
        sonny.glow.volumetricEnabled = true;
        sonny.glow.volumeIntensity = 0.15f;
        sonny.humLoop = LoadClip(MagneticFolder + "/Looping", "Magnetic wave bass loop.wav");
        sonny.awakenClip = LoadClip(MagneticFolder, "Magnetic bass tone 01.wav");
        return sonny;
    }

    internal static void PlaceLockers(Map map, GameObject prefab, Transform parent)
    {
        // Lifted a little so the locker stands against the wall above its cell.
        foreach (Vector2Int at in map.Find('L'))
        {
            Vector2 center = map.Center(at.x, at.y);
            Spawn(prefab, parent, new Vector3(center.x, center.y + 0.3f, CharacterZ));
        }
    }

    // Furniture from the ship tileset, the same pieces Map (DEMO) and FloorOneBuilder use: tile numbers row by row from the
    // top, -1 where there's nothing. The top wallRows rows hang on the wall above the floor the piece stands on.
    class Furnishing
    {
        public readonly string name;
        public readonly int[][] rows;
        public readonly int wallRows;
        public readonly float footDepth;    // how deep its solid foot is, front to back

        public Furnishing(string name, int[][] rows, float footDepth, int wallRows = 0)
        {
            this.name = name;
            this.rows = rows;
            this.footDepth = footDepth;
            this.wallRows = wallRows;
        }

        public int Width => rows.Max(row => row.Length);
    }

    static readonly Dictionary<char, Furnishing> Furnishings = new Dictionary<char, Furnishing>
    {
        { 'K', new Furnishing("Monitor Bank", new[] { new[] { 179, 180 } }, 0.7f) },
        { 'k', new Furnishing("Control Desk", new[] { new[] { 60, 61 } }, 0.7f) },
        { 'Q', new Furnishing("Console Bank", new[] { new[] { 59, 60, 61, 117, 118, 119 }, new[] { 88, -1, -1, -1, 148, 149 } }, 0.9f) },
        { 'C', new Furnishing("Chair", new[] { new[] { 89 }, new[] { 120 } }, 0.45f) },
        { 'T', new Furnishing("Bench", new[] { new[] { 15, 16, 17 }, new[] { 39, 40, 41 } }, 0.9f) },
        { 'B', new Furnishing("Sofa", new[] { new[] { 18, 19, 20 }, new[] { 42, 43, 44 } }, 0.9f) },
        { 'O', new Furnishing("Pod", new[] { new[] { 13, 14 }, new[] { 37, 38 }, new[] { 62, 63 } }, 1.2f) },
        { 'I', new Furnishing("Wall Pipe", new[] { new[] { 400 }, new[] { 428 }, new[] { 457 } }, 0.4f, wallRows: 1) },
    };

    static readonly Dictionary<int, Sprite> decorSprites = new Dictionary<int, Sprite>();

    static Sprite DecorSprite(int number)
    {
        if (!decorSprites.TryGetValue(number, out Sprite sprite))
        {
            sprite = (DecorTile(number) as Tile)?.sprite;
            decorSprites[number] = sprite;
        }
        return sprite;
    }

    static readonly Dictionary<int, TileBase> decorTiles = new Dictionary<int, TileBase>();

    static TileBase DecorTile(int number)
    {
        if (!decorTiles.TryGetValue(number, out TileBase tile))
        {
            tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesFolder}/tileset_{number}.asset");
            if (tile == null) Debug.LogWarning($"Tutorial builder: tile tileset_{number} is missing from {TilesFolder}.");
            decorTiles[number] = tile;
        }
        return tile;
    }

    // Each piece stands with its bottom-left on its marker and runs right from it. Only its foot is solid, so the player
    // can walk right up to it, and behind it. It sorts as one piece from DepthSort.FeetToCenter above its base, the same
    // height above the floor that characters sort from, so whoever is lower on screen is drawn in front.
    internal static void PlaceFurniture(Map map, Transform parent)
    {
        foreach (KeyValuePair<char, Furnishing> entry in Furnishings)
        {
            Furnishing piece = entry.Value;
            int height = piece.rows.Length;
            foreach (Vector2Int at in map.Find(entry.Key))
            {
                // The object sits at its sorting point; everything in it is placed down from there to the base.
                float lift = DepthSort.FeetToCenter;
                var furniture = new GameObject(piece.name);
                furniture.transform.SetParent(parent);
                furniture.transform.position = new Vector3(at.x, map.Height - 1 - at.y + 0.1f + lift, CharacterZ);
                DepthSort.Group(furniture);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < piece.rows[y].Length; x++)
                    {
                        Sprite sprite = piece.rows[y][x] >= 0 ? DecorSprite(piece.rows[y][x]) : null;
                        if (sprite == null) continue;
                        var tile = new GameObject("Tile").AddComponent<SpriteRenderer>();
                        tile.transform.SetParent(furniture.transform, false);
                        tile.transform.localPosition = new Vector3(x + 0.5f, height - 1 - y + 0.5f - lift, 0f);
                        tile.sprite = sprite;
                        tile.sortingLayerName = DepthSort.Layer;
                        tile.sortingOrder = 0;
                    }
                }

                var foot = furniture.AddComponent<BoxCollider2D>();
                foot.size = new Vector2(piece.Width - 0.2f, piece.footDepth);
                foot.offset = new Vector2(piece.Width * 0.5f, piece.footDepth * 0.5f - lift);

                // Sonny can take over the monitors: the screens run along the top half of the bank.
                if (entry.Key == 'K')
                {
                    var screen = furniture.AddComponent<SonnyScreen>();
                    screen.screenCenter = new Vector2(piece.Width * 0.5f, 1f - 0.33f - lift);
                    screen.screenSize = new Vector2(piece.Width - 0.2f, 0.42f);
                }
            }
        }
    }

    static void PlaceStains(Map map, Transform parent)
    {
        foreach (Vector2Int at in map.Find('~'))
        {
            var stain = new GameObject("Floor Stain");
            stain.transform.SetParent(parent);
            stain.transform.position = map.Center(at.x, at.y);
            stain.AddComponent<FloorStain>();
        }
    }

    // --- Dressing, after Map (DEMO) ---

    // A stretch of the level where the floor keeps the same top and bottom: a hallway, or a room.
    class Stretch
    {
        public int minX, maxX, top, bottom;
        public string contents = "";
        public int Width => maxX - minX + 1;
        public int Height => bottom - top + 1;
        public bool IsHallway => Height <= 4;
        public bool Has(char marker) => contents.IndexOf(marker) >= 0;
    }

    static List<Stretch> Stretches(Map map)
    {
        var stretches = new List<Stretch>();
        Stretch current = null;
        for (int x = 0; x < map.Width; x++)
        {
            int top = -1, bottom = -1;
            for (int row = 0; row < map.Height; row++)
            {
                char c = map.At(x, row);
                if (!Map.IsFloor(c) && !Map.IsHole(c)) continue;
                if (top < 0) top = row;
                bottom = row;
            }
            if (top < 0)
            {
                current = null;
                continue;
            }
            if (current == null || current.top != top || current.bottom != bottom)
            {
                current = new Stretch { minX = x, top = top, bottom = bottom };
                stretches.Add(current);
            }
            current.maxX = x;
            for (int row = top; row <= bottom; row++) current.contents += map.At(x, row);
        }
        return stretches;
    }

    // The floor laid for each stretch, the tile for a cell counted from its top-left corner, or -1 to keep the grating.
    static System.Func<Stretch, int, int, int> FloorKit(Stretch stretch)
    {
        if (stretch.Has('a')) return (s, x, y) => NineSlice(x, y, s.Width, s.Height, 157, 158, 159, 189, 190, 191, 219, 220, 221);
        if (stretch.Has('S')) return Walkways;
        if (stretch.Has('m')) return (s, x, y) => NineSlice(x, y, s.Width, s.Height, 5, 6, 7, 30, 31, 32, 54, 55, 56);
        if (stretch.IsHallway) return (s, x, y) => y == 0 || y == s.Height - 1 ? 370 : 339;
        return null;
    }

    // Panels along the walls, walkways inside them, and plates in the middle.
    static int Walkways(Stretch stretch, int x, int y)
    {
        int fromBottom = stretch.Height - 1 - y;
        if (y <= 1 || fromBottom <= 1) return 339;
        return y == 2 || fromBottom == 2 ? 430 : 370;
    }

    // The tile for a cell of an area drawn as corners, edges, and a middle: top-left, top, top-right, left, middle,
    // right, bottom-left, bottom, bottom-right.
    static int NineSlice(int x, int y, int width, int height, params int[] kit)
    {
        int column = x == 0 ? 0 : x == width - 1 ? 2 : 1;
        int row = y == 0 ? 0 : y == height - 1 ? 2 : 1;
        return kit[row * 3 + column];
    }

    // Machinery along a wall: a run of cylinder with an end cap at each side, as rows top, middle, and bottom.
    static readonly int[] MachineLeftCap = { 68, 95, 126 };
    static readonly int[] MachineRightCap = { 74, 101, 132 };
    static readonly int[] MachineMiddle = { 69, 96, 127 };     // the first of five that repeat
    static readonly int[] CeilingFixtures = { 249, 250, 249, 249, 249, 251, 251, 249, 249 };
    const int CeilingLight = 310;

    // Floors, machinery on the walls, and fittings overhead, stretch by stretch.
    static void DressLevel(Map map, Grid grid, Transform lights)
    {
        Tilemap floor = grid.transform.Find(FloorMap).GetComponent<Tilemap>();
        Tilemap walls = grid.transform.Find(WallsMap).GetComponent<Tilemap>();
        Tilemap overhead = grid.transform.Find(OverheadMap).GetComponent<Tilemap>();
        int lightsPlaced = 0;

        foreach (Stretch stretch in Stretches(map))
        {
            System.Func<Stretch, int, int, int> kit = FloorKit(stretch);
            if (kit != null)
            {
                for (int x = stretch.minX; x <= stretch.maxX; x++)
                    for (int row = stretch.top; row <= stretch.bottom; row++)
                    {
                        if (!Map.IsFloor(map.At(x, row))) continue;
                        int tile = kit(stretch, x - stretch.minX, row - stretch.top);
                        if (tile >= 0) floor.SetTile(map.Cell(x, row), DecorTile(tile));
                    }
            }

            // The rooms something happens in get machinery along their top wall.
            if (!stretch.IsHallway && (stretch.Has('m') || stretch.Has('h') || stretch.Has('a')))
                MachineWall(map, walls, stretch);

            // Lights overhead down the length of the hallways, and a single one hanging in the middle of the dark melee room.
            int middleRow = stretch.top + (stretch.Height - 1) / 2;
            if (stretch.IsHallway && stretch.Width >= 8)
            {
                for (int x = stretch.minX + 4; x <= stretch.maxX - 3; x += 9)
                    PlaceCeilingLight(map, overhead, lights, x, middleRow, lightsPlaced++);
            }
            else if (stretch.Has('m'))
            {
                PlaceCeilingLight(map, overhead, lights, stretch.minX + stretch.Width / 2, middleRow, lightsPlaced++);
            }

            // Fittings strung across the ceiling of the room the patrol comes through.
            if (stretch.Has('h') && !stretch.IsHallway && stretch.Width >= CeilingFixtures.Length + 4)
            {
                int start = stretch.minX + (stretch.Width - CeilingFixtures.Length) / 2;
                for (int i = 0; i < CeilingFixtures.Length; i++)
                    overhead.SetTile(map.Cell(start + i, middleRow), DecorTile(CeilingFixtures[i]));
            }
        }
    }

    // Turns the plain faces of a stretch's top wall into machinery. Windows break the runs.
    static void MachineWall(Map map, Tilemap walls, Stretch stretch)
    {
        for (int band = 0; band < 3; band++)
        {
            int row = stretch.top - 3 + band;
            int x = stretch.minX;
            while (x <= stretch.maxX)
            {
                if (!IsPlainFace(map, x, stretch.top)) { x++; continue; }
                int start = x;
                while (x + 1 <= stretch.maxX && IsPlainFace(map, x + 1, stretch.top)) x++;
                int end = x;
                for (int cell = start; cell <= end && end - start >= 2; cell++)
                {
                    int tile = cell == start ? MachineLeftCap[band] : cell == end ? MachineRightCap[band] : MachineMiddle[band] + (cell - start - 1) % 5;
                    walls.SetTile(map.Cell(cell, row), DecorTile(tile));
                }
                x++;
            }
        }
    }

    // All three rows of the wall face above a floor cell, and none of them window.
    static bool IsPlainFace(Map map, int x, int floorRow)
    {
        for (int band = 1; band <= 3; band++)
        {
            char c = map.At(x, floorRow - band);
            if (!Map.IsFace(c) || c == '+') return false;
        }
        return true;
    }

    // A light fitting overhead with a warm pool of light under it. Every third one flickers; they all stutter when the
    // station shakes.
    static void PlaceCeilingLight(Map map, Tilemap overhead, Transform lights, int x, int row, int index)
    {
        overhead.SetTile(map.Cell(x, row), DecorTile(CeilingLight));
        Light2D light = NewLight("Ceiling Light", lights, map.Center(x, row), CeilingLightColor, 0.55f, 3.2f);
        var fitting = light.gameObject.AddComponent<StationLight>();
        fitting.mode = index % 3 == 2 ? StationLight.Mode.Flicker : StationLight.Mode.Steady;
        fitting.breakable = false;
        fitting.rumbleStutter = 0.5f;
    }

    static void PlaceRubble(Map map, Transform parent)
    {
        foreach (char marker in new[] { '%', '&' })
        {
            bool solid = marker == '&';
            foreach (Vector2Int at in map.Find(marker))
            {
                var rubble = new GameObject(solid ? "Rubble Pile" : "Loose Rubble");
                rubble.transform.SetParent(parent);
                rubble.transform.position = map.Center(at.x, at.y);
                var debris = rubble.AddComponent<FallingDebris>();
                debris.startLanded = true;
                debris.solid = solid;
                debris.rubbleLifetime = 0f;
                debris.size = solid ? 1f : 0.8f;
            }
        }
    }

    // One railing around each hole in the floor, on a layer that shots pass through.
    static void PlaceRailings(Map map, Transform parent)
    {
        var seen = new HashSet<Vector2Int>();
        foreach (Vector2Int start in map.Find(':'))
        {
            if (!seen.Add(start)) continue;

            int minX = start.x, maxX = start.x, minRow = start.y, maxRow = start.y;
            var open = new Stack<Vector2Int>();
            open.Push(start);
            while (open.Count > 0)
            {
                Vector2Int cell = open.Pop();
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minRow = Mathf.Min(minRow, cell.y);
                maxRow = Mathf.Max(maxRow, cell.y);
                foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    Vector2Int next = cell + step;
                    if (Map.IsHole(map.At(next.x, next.y)) && seen.Add(next)) open.Push(next);
                }
            }

            Rect area = map.WorldRect(minX, maxX, minRow, maxRow);
            var railing = new GameObject("Railing") { layer = IgnoreRaycastLayer };
            railing.transform.SetParent(parent);
            railing.transform.position = area.center;
            railing.AddComponent<BoxCollider2D>().size = area.size;
            railing.AddComponent<Railing>();
        }
    }

    // --- Build settings ---

    // First in the build, so a build of the game starts with the tutorial.
    static void AddSceneToBuild()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // --- Helpers ---

    internal static bool LoadTiles()
    {
        tiles.Clear();
        foreach (int number in Tiles.All().Distinct())
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesFolder}/tileset_{number}.asset");
            if (tile == null) return Fail($"tile tileset_{number} is missing from {TilesFolder}.");
            tiles[number] = tile;
        }
        return true;
    }

    internal static GameObject LoadPrefab(string pathInPrefabs)
    {
        string path = $"{PrefabsFolder}/{pathInPrefabs}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Fail($"the prefab {path} is missing.");
        return prefab;
    }

    internal static AudioClip LoadClip(string folder, string file)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{file}");
        if (clip == null) Debug.LogWarning($"Tutorial builder: couldn't find the sound {folder}/{file}, so it's left out.");
        return clip;
    }

    static AudioClip[] LoadClips(string folder, params string[] files)
    {
        return files.Select(file => LoadClip(folder, file)).Where(clip => clip != null).ToArray();
    }

    static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = position;
        return instance;
    }

    internal static Transform Group(string groupName, Transform parent)
    {
        var group = new GameObject(groupName).transform;
        group.SetParent(parent);
        return group;
    }

    static Light2D NewLight(string lightName, Transform parent, Vector2 position, Color color, float intensity, float radius)
    {
        var lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(parent);
        lightObject.transform.position = position;
        var light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightInnerRadius = radius * 0.15f;
        light.pointLightOuterRadius = radius;
        light.falloffIntensity = 0.6f;
        return light;
    }

    // Changes to a prefab instance only stay as overrides once they're recorded.
    internal static void Record(Object changed)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(changed))
            PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
    }

    static bool Fail(string message)
    {
        Debug.LogError("Tutorial builder: " + message);
        return false;
    }
}
