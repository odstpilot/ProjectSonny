using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Menu: Sonny > Build Tutorial Level
//
// Rebuilds Scenes/Levels/Tutorial.unity from the text map in Scenes/Levels/Tutorial/TutorialLayout.txt, one character
// per tile, and puts the Tutorial first in the build. Edit the map, run this again, and the whole
// level is rebuilt to match: walls, floor, doors, robots, lamps, trigger zones, and the TutorialDirector that runs it.
// It replaces everything in the scene, so change the map, prefabs, or scripts rather than the scene itself, at least
// until the layout is settled.
//
// The map:
//   (space)  nothing. Walls are drawn around the edge of everything else.
//   .        floor
//   =        wall face: the 3 rows of wall seen above a floor
//   +        window in a wall face, 3 wide
//   :        collapsed floor: a hole with a rail around it that stops walking but not shots
//   P        where the player starts
//   M G H A  robots: the melee lesson, the gallery across the hole, the patrol, the arena
//   L        locker, against the wall above it
//   S        Sonny's processing box
//   1 - 5    blast doors: melee room exit, gallery exit, arena entrance (starts open), arena exit, control room
//   c r m g h a f e   zones the director waits for the player to walk into: the collapse, run, melee, gallery, hide,
//            arena, final hallway, and the reveal. Mark a line of cells across the way in.
//   ^        ceiling lamp, on a wall face
//   *        alarm lamp, on a wall face: off until the last hallway
//   %        loose rubble          &  a solid pile of rubble
public static class TutorialLevelBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Tutorial.unity";
    const string LayoutPath = "Assets/_Project/Scenes/Levels/Tutorial/TutorialLayout.txt";
    const string TilesFolder = "Assets/_Project/Art/Environment/Tilesets/ShipTiles";
    const string PrefabsFolder = "Assets/_Project/Prefabs";
    const string SfxFolder = "Assets/_Project/Audio/SFX";
    const string MagneticFolder = "Assets/_Project/Audio/SFX/Magnetic Sound fx/Wav";
    const int IgnoreRaycastLayer = 2;
    const float CharacterZ = 1f;    // where the character prefabs sit

    // Tile numbers in the ship tileset (ShipTiles/tileset_N).
    static class Tiles
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
    class Map
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
        public static bool IsFace(char c) => c == '=' || c == '+' || c == '^' || c == '*';
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
        GameObject canvasPrefab = LoadPrefab("UI/CanvasCont");
        GameObject globalLightPrefab = LoadPrefab("Systems/GlobalLight2D");
        if (!playerPrefab || !robotPrefab || !lockerPrefab || !cameraPrefab || !canvasPrefab || !globalLightPrefab) return false;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        Tilemap floor = BuildTilemaps(map);

        GameObject player = PlacePlayer(map, playerPrefab);
        Camera cam = PlaceCamera(cameraPrefab, player);
        PlaceCanvas(canvasPrefab, player, cam);
        Light2D globalLight = PlaceGlobalLight(globalLightPrefab);
        StationRumble rumble = PlaceRumble(floor, globalLight);

        Transform level = new GameObject("Level").transform;
        Transform zones = Group("Zones", level);
        Transform doors = Group("Doors", level);
        Transform robots = Group("Robots", level);
        Transform props = Group("Props", level);

        var director = new GameObject("Tutorial Director").AddComponent<TutorialDirector>();
        director.rumble = rumble;
        director.alarmClip = LoadClip(SfxFolder, "316847__lalks__alarm-04-short.wav");
        director.heartbeatClip = LoadClip(SfxFolder, "heartbeat.wav");

        director.collapseZone = PlaceZone(map, 'c', "Collapse Zone", zones);
        director.runZone = PlaceZone(map, 'r', "Run Zone", zones);
        director.meleeZone = PlaceZone(map, 'm', "Melee Zone", zones);
        director.galleryZone = PlaceZone(map, 'g', "Gallery Zone", zones);
        director.hideZone = PlaceZone(map, 'h', "Hide Zone", zones);
        director.arenaZone = PlaceZone(map, 'a', "Arena Zone", zones);
        director.finalZone = PlaceZone(map, 'f', "Final Hallway Zone", zones);
        director.revealZone = PlaceZone(map, 'e', "Reveal Zone", zones);

        AudioClip doorClip = LoadClip(MagneticFolder, "Magnetic industrial layer02_1.wav");
        director.meleeExit = PlaceDoor(map, '1', "Melee Room Exit", doors, doorClip);
        director.galleryExit = PlaceDoor(map, '2', "Gallery Exit", doors, doorClip);
        director.arenaEntrance = PlaceDoor(map, '3', "Arena Entrance", doors, doorClip);
        director.arenaExit = PlaceDoor(map, '4', "Arena Exit", doors, doorClip);
        director.controlRoomDoor = PlaceDoor(map, '5', "Control Room Door", doors, doorClip);
        if (director.arenaEntrance != null) director.arenaEntrance.startsOpen = true;
        if (director.controlRoomDoor != null) director.controlRoomDoor.openWhenPlayerWithin = 3.5f;

        AudioClip wakeClip = LoadClip(SfxFolder, "746988__gammagool__robot-awakening-power-on (1).wav");
        director.meleeRobots = PlaceRobots(map, 'M', "Melee Robot", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            StandGuard(robot, 9f);
        });
        director.galleryRobots = PlaceRobots(map, 'G', "Gallery Robots", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            StandGuard(robot, 12f);
            health.maxHealth = 6f;   // three rivets each
        });
        director.patrolRobots = PlaceRobots(map, 'H', "Patrol", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            robot.patrolRoute = new[] { new Vector2(-14f, 0f), Vector2.zero };
            robot.patrolSpeed = 1.3f;
            robot.patrolPause = 1.5f;
        });
        director.arenaRobots = PlaceRobots(map, 'A', "Arena Robots", robots, robotPrefab, wakeClip, (robot, health) =>
        {
            StandGuard(robot, 14f);
        });

        director.collapsePoints = PlaceCollapsePoints(map, props);
        director.alarms = PlaceLamps(map, Group("Lamps", level));
        director.sonny = PlaceSonny(map, props);
        PlaceLockers(map, lockerPrefab, Group("Lockers", level));
        PlaceRubble(map, Group("Rubble", level));
        PlaceRailings(map, props);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) return Fail($"couldn't save {ScenePath}.");
        AddSceneToBuild();

        Debug.Log($"Tutorial builder: built a {map.Width} by {map.Height} level with {robots.GetComponentsInChildren<PlaceholderRobot>().Length} robots, " +
                  $"{doors.childCount} doors and {zones.childCount} zones, and saved {ScenePath}.");
        return true;
    }

    // --- Tiles ---

    static Tilemap BuildTilemaps(Map map)
    {
        var grid = new GameObject("Grid").AddComponent<Grid>();
        Tilemap floor = CreateTilemap(grid, "Floor", "Floor", 0, false);
        Tilemap hole = CreateTilemap(grid, "Collapsed Floor", "Floor", 1, false);
        Tilemap walls = CreateTilemap(grid, "Walls", "Collision", 0, true);

        // One cell past every edge, so the walls close around the whole map.
        for (int row = -1; row <= map.Height; row++)
        {
            for (int x = -1; x <= map.Width; x++)
            {
                char c = map.At(x, row);
                Vector3Int cell = map.Cell(x, row);
                if (Map.IsFloor(c))
                    floor.SetTile(cell, tiles[FloorTile(map, x, row)]);
                else if (Map.IsFace(c))
                    walls.SetTile(cell, tiles[FaceTile(map, x, row)]);
                else if (Map.IsHole(c))
                    hole.SetTile(cell, tiles[EdgeTile(map, x, row, Map.IsFloor) ?? Tiles.Solid]);
                else if (EdgeTile(map, x, row, ch => !Map.IsVoid(ch)) is int edge)
                    walls.SetTile(cell, tiles[edge]);
            }
        }
        return floor;
    }

    static Tilemap CreateTilemap(Grid grid, string tilemapName, string sortingLayer, int order, bool solid)
    {
        var go = new GameObject(tilemapName);
        go.transform.SetParent(grid.transform, false);
        var tilemap = go.AddComponent<Tilemap>();
        var tilemapRenderer = go.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingLayerName = sortingLayer;
        tilemapRenderer.sortingOrder = order;
        if (solid) go.AddComponent<TilemapCollider2D>();
        return tilemap;
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

    static GameObject PlacePlayer(Map map, GameObject prefab)
    {
        Vector2Int start = map.Find('P')[0];
        Vector2 spawn = map.Center(start.x, start.y);
        GameObject player = Spawn(prefab, null, new Vector3(spawn.x, spawn.y, CharacterZ));

        var controller = player.GetComponent<PlayerController>();
        controller.StartingPos = player.transform.position;
        controller.footstepClips = LoadClips(SfxFolder, "footstep1.wav", "footstep2.wav", "footstep3.wav", "footstep4.wav");
        Record(controller);

        var footsteps = player.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.spatialBlend = 0f;
        footsteps.volume = 0.3f;
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("audioSource").objectReferenceValue = footsteps;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        // The Health system, set up like the player in the combat test scene.
        var health = player.AddComponent<Health>();
        health.maxHealth = 5f;
        health.hitFlashColor = new Color(1f, 0.25f, 0.25f);
        health.hitFlashDuration = 0.12f;
        health.invincibilityDuration = 1f;
        health.knockbackDuration = 0.15f;
        health.destroyOnDeath = false;
        health.disableCollidersOnDeath = false;
        player.AddComponent<PlayerHealthHandler>();

        // A smaller glow than the prefab's, so the dark between the station's lamps reads as dark.
        var lantern = player.GetComponentInChildren<Light2D>();
        if (lantern != null)
        {
            lantern.pointLightOuterRadius = 6f;
            lantern.pointLightInnerRadius = 0.5f;
            lantern.intensity = 0.8f;
            Record(lantern);
        }
        return player;
    }

    static Camera PlaceCamera(GameObject prefab, GameObject player)
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
        return cam;
    }

    // PlayerController looks for CanvasCont by name when it starts, so it has to be here.
    static void PlaceCanvas(GameObject prefab, GameObject player, Camera cam)
    {
        GameObject canvas = Spawn(prefab, null, Vector3.zero);
        foreach (Canvas each in canvas.GetComponentsInChildren<Canvas>(true))
        {
            if (each.renderMode == RenderMode.ScreenSpaceOverlay) continue;
            each.worldCamera = cam;
            Record(each);
        }

        var controller = player.GetComponent<PlayerController>();
        controller.CurrentCanvas = canvas.GetComponent<CanvasCont>();
        Record(controller);
    }

    static Light2D PlaceGlobalLight(GameObject prefab)
    {
        var globalLight = Spawn(prefab, null, Vector3.zero).GetComponent<Light2D>();
        globalLight.intensity = 0.45f;
        globalLight.color = new Color(0.78f, 0.84f, 1f);
        Record(globalLight);
        return globalLight;
    }

    static StationRumble PlaceRumble(Tilemap floor, Light2D globalLight)
    {
        var rumble = new GameObject("Station Rumble").AddComponent<StationRumble>();
        rumble.floor = floor;
        rumble.globalLight = globalLight;
        rumble.rumbleClips = LoadClips(MagneticFolder, "Magnetic bass ground.wav", "Magnetic bass once 01.wav", "Magnetic bass once 02.wav", "Magnetic bass full.wav");
        rumble.impactClips = LoadClips(MagneticFolder, "Magnetic hit 01.wav", "Magnetic hit 02.wav", "Magnetic hit 03.wav", "Magnetic hit 04.wav");
        rumble.ambientLoop = LoadClip(SfxFolder, "700008__newlocknew__scimisc_low-steady-hum-2_em.wav");
        return rumble;
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
        return encounter;
    }

    // Stands still, and once awake sees all the way around.
    static void StandGuard(PlaceholderRobot robot, float sightRange)
    {
        robot.patrolRoute = new Vector2[0];
        robot.fieldOfView = 360f;
        robot.sightRange = sightRange;
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

    // Returns the alarms, which the director switches on later.
    static List<StationLight> PlaceLamps(Map map, Transform parent)
    {
        int lampCount = 0;
        foreach (Vector2Int at in map.Find('^'))
        {
            // On the wall, shining down into the middle of the room below it.
            Light2D lamp = NewLight("Ceiling Lamp", parent, map.Center(at.x, at.y) + new Vector2(0f, -3.5f), new Color(0.75f, 0.85f, 1f), 1f, 6f);
            var station = lamp.gameObject.AddComponent<StationLight>();
            station.mode = lampCount++ % 4 == 2 ? StationLight.Mode.Flicker : StationLight.Mode.Steady;
        }

        var alarms = new List<StationLight>();
        foreach (Vector2Int at in map.Find('*'))
        {
            Vector2 wall = map.Center(at.x, at.y);
            Light2D lamp = NewLight("Alarm Lamp", parent, wall + new Vector2(0f, -2f), new Color(1f, 0.12f, 0.08f), 1.4f, 5f);

            var fixture = new GameObject("Fixture").AddComponent<SpriteRenderer>();
            fixture.transform.SetParent(lamp.transform);
            fixture.transform.position = wall;
            fixture.sprite = (tiles[Tiles.AlarmFixture] as Tile)?.sprite;
            fixture.sortingLayerName = "Collision";
            fixture.sortingOrder = 2;

            var alarm = lamp.gameObject.AddComponent<StationLight>();
            alarm.mode = StationLight.Mode.Alarm;
            alarm.startOn = false;
            alarm.bulb = fixture;
            alarms.Add(alarm);
        }
        return alarms;
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
        sonny.humLoop = LoadClip(MagneticFolder + "/Looping", "Magnetic wave bass loop.wav");
        sonny.awakenClip = LoadClip(MagneticFolder, "Magnetic bass tone 01.wav");
        return sonny;
    }

    static void PlaceLockers(Map map, GameObject prefab, Transform parent)
    {
        // Lifted a little so the locker stands against the wall above its cell.
        foreach (Vector2Int at in map.Find('L'))
        {
            Vector2 center = map.Center(at.x, at.y);
            Spawn(prefab, parent, new Vector3(center.x, center.y + 0.3f, CharacterZ));
        }
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
                debris.size = solid ? 1f : 0.75f;
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

    static bool LoadTiles()
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

    static GameObject LoadPrefab(string pathInPrefabs)
    {
        string path = $"{PrefabsFolder}/{pathInPrefabs}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Fail($"the prefab {path} is missing.");
        return prefab;
    }

    static AudioClip LoadClip(string folder, string file)
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

    static Transform Group(string groupName, Transform parent)
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
    static void Record(Object changed)
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
