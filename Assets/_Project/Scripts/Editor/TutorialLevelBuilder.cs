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

// builds the tutorial from its layout file
public static class TutorialLevelBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Tutorial.unity";
    const string LayoutPath = "Assets/_Project/Scenes/Levels/Tutorial/TutorialLayout.txt";
    const string PostProcessingPath = "Assets/_Project/Scenes/Levels/Tutorial/TutorialPostProcessing.asset";
    const string TilesFolder = "Assets/_Project/Art/Environment/Tilesets/ShipTiles";
    const string PrefabsFolder = "Assets/_Project/Prefabs";
    const int IgnoreRaycastLayer = 2;
    const float CharacterZ = 1f;
    const float LampDrop = 3.5f;

    static readonly Color AmbientColor = new Color(1f, 0.68f, 0.45f);
    static readonly Color LampColor = new Color(1f, 0.72f, 0.36f);
    static readonly Color SunColor = new Color(1f, 0.5f, 0.18f);
    static readonly Color AlarmColor = new Color(1f, 0.2f, 0.06f);
    static readonly Color ReactorColor = new Color(0.45f, 0.85f, 1f);
    static readonly Color LanternColor = new Color(1f, 0.84f, 0.62f);
    static readonly Color CeilingLightColor = new Color(1f, 0.8f, 0.55f);

    internal const string FloorMap = "floor";
    internal const string OnFloorMap = "Objects on Floor";
    internal const string WallsMap = "Collision and Walls";
    internal const string SolidMap = "InvisibleColumn";
    internal const string OverheadMap = "Very Top";
    internal const string DecorationMap = "Decoration";
    const int SolidDepth = 2;

    internal static class Tiles
    {
        public const int Floor = 377, FloorN = 346, FloorS = 405, FloorW = 376, FloorE = 378;
        public const int FloorNW = 345, FloorNE = 347, FloorSW = 404, FloorSE = 406;
        public const int EdgeN = 257, EdgeS = 319, EdgeW = 288, EdgeE = 290;
        public const int EdgeNW = 256, EdgeNE = 258, EdgeSW = 318, EdgeSE = 320;
        public const int NotchNE = 291, NotchNW = 292, NotchSE = 259, NotchSW = 260, Solid = 289;
        public static readonly int[] FacePlain = { 75, 104, 135 };
        public static readonly int[] FaceVent = { 76, 105, 136 };
        public static readonly int[] FaceSign = { 77, 106, 137 };
        public static readonly int[] WindowLeft = { 166, 198, 226 };
        public static readonly int[] WindowMiddle = { 167, 199, 227 };
        public static readonly int[] WindowRight = { 168, 200, 228 };
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

        public Vector2 Origin;

        public char At(int x, int row) => row >= 0 && row < Height && x >= 0 && x < rows[row].Length ? rows[row][x] : ' ';
        public Vector3Int Cell(int x, int row) => new Vector3Int(x, Height - 1 - row, 0);
        public Vector2 Center(int x, int row) => Origin + new Vector2(x + 0.5f, Height - 1 - row + 0.5f);

        public List<Vector2Int> Find(char marker)
        {
            var found = new List<Vector2Int>();
            for (int row = 0; row < Height; row++)
                for (int x = 0; x < rows[row].Length; x++)
                    if (rows[row][x] == marker) found.Add(new Vector2Int(x, row));
            return found;
        }

        public bool TryGetArea(char marker, out Rect area)
        {
            List<Vector2Int> cells = Find(marker);
            area = default;
            if (cells.Count == 0) return false;
            area = WorldRect(cells.Min(c => c.x), cells.Max(c => c.x), cells.Min(c => c.y), cells.Max(c => c.y));
            return true;
        }

        public Rect WorldRect(int minX, int maxX, int minRow, int maxRow) =>
            Rect.MinMaxRect(Origin.x + minX, Origin.y + Height - 1 - maxRow, Origin.x + maxX + 1, Origin.y + Height - minRow);

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
            "Rebuild Tutorial.unity from TutorialLayout.txt?\n\nThis replaces everything in the scene except the Sound Manager. Anything else added to it by hand will be lost.",
            "Rebuild", "Cancel");
        if (rebuild && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            Build();
    }

    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build() ? 0 : 1);
    }

    public static bool Build()
    {
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
        ClearScene(scene);

        Tilemap floor = BuildTilemaps(map);
        Grid grid = floor.GetComponentInParent<Grid>();

        GameObject player = PlacePlayer(map, playerPrefab);
        FloorOneBuilder.GroundPlayer(player, keepLantern: true);
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

        director.collapseZone = PlaceZone(map, 'c', "Collapse Zone", zones);
        director.runZone = PlaceZone(map, 'r', "Run Zone", zones);
        director.fightZone = PlaceZone(map, 'h', "Fight Zone", zones);
        director.finalZone = PlaceZone(map, 'f', "Final Hallway Zone", zones);
        director.revealZone = PlaceZone(map, 'e', "Reveal Zone", zones);

        director.fightExit = PlaceDoor(map, '2', "Fight Room Exit", doors);
        director.controlRoomDoor = PlaceDoor(map, '4', "Control Room Door", doors);
        PlaceReactorDoor(map, doors, lights);
        if (director.controlRoomDoor != null) director.controlRoomDoor.openWhenPlayerWithin = 3.5f;

        director.chasers = PlaceChasers(map, robotPrefab, Group("Chasers", robots));
        director.openingBreaches = PlaceBreachChasers(map, robotPrefab, Group("Opening Breaches", robots));
        float twoHits = TwoHitHealth();
        director.firstWave = PlaceRobots(map, 'V', "First Wave", robots, robotPrefab, (robot, health) =>
        {
            StandGuard(robot, 16f);
            health.maxHealth = twoHits;
            robot.gameObject.SetActive(false);
        });
        director.firstBreaches = BreachesFor(director.firstWave);
        director.secondWave = PlaceRobots(map, 'N', "Second Wave", robots, robotPrefab, (robot, health) =>
        {
            StandGuard(robot, 16f);
            health.maxHealth = twoHits;
            robot.gameObject.SetActive(false);
        });
        director.secondBreaches = BreachesFor(director.secondWave);

        director.collapsePoints = PlaceCollapsePoints(map, props);
        director.lamps = PlaceLamps(map, lights, "Lamp Break");
        director.alarms = PlaceAlarms(map, lights);
        PlaceSunlight(map, lights);
        director.sonny = PlaceSonny(map, props);
        if (director.sonny != null) director.sonny.look = SonnyBox.Look.Shadow;
        PlaceLockers(map, lockerPrefab, Group("Lockers", level));
        PlaceFurniture(map, Group("Furniture", level));
        PlaceRubble(map, Group("Rubble", level));
        PlaceStains(map, Group("Stains", level));
        DressLevel(map, grid, lights);
        PlaceRailings(map, props);

        SoundDefaults.Fill(scene, SoundDefaults.Player, SoundDefaults.Tutorial, SoundDefaults.Sonny, SoundDefaults.Combat);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) return Fail($"couldn't save {ScenePath}.");
        AddSceneToBuild();

        Debug.Log($"Tutorial builder: built a {map.Width} by {map.Height} level with {robots.GetComponentsInChildren<PlaceholderRobot>().Length} robots, " +
                  $"{doors.childCount} doors, {zones.childCount} zones and {lights.childCount} lights, and saved {ScenePath}.");
        return true;
    }

    internal static void ClearScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<SoundManager>() == null) Object.DestroyImmediate(root);
    }

    internal static Tilemap BuildTilemaps(Map map)
    {
        var grid = new GameObject("Grid").AddComponent<Grid>();
        Tilemap floor = CreateTilemap(grid, FloorMap, "Floor", Walkable.Yes);
        Tilemap onFloor = CreateTilemap(grid, OnFloorMap, "FloorObject", Walkable.Yes);
        Tilemap walls = CreateTilemap(grid, WallsMap, "Collision", Walkable.No);
        Tilemap solid = CreateTilemap(grid, SolidMap, "Collision", Walkable.No);
        CreateTilemap(grid, OverheadMap, "Top", Walkable.Yes);
        CreateTilemap(grid, DecorationMap, "Decor", Walkable.Unmarked);

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
            modifier.area = walkable == Walkable.Yes ? 0 : 1;
        }
        return tilemap;
    }

    static void MakeSolid(Tilemap tilemap)
    {
        GameObject go = tilemap.gameObject;
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var composite = go.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        go.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        composite.GenerateGeometry();
    }

    static bool NearSomething(Map map, int x, int row, int reach)
    {
        for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
                if (!Map.IsVoid(map.At(x + dx, row + dy))) return true;
        return false;
    }

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
        int below = 0;
        while (below < 2 && Map.IsFace(map.At(x, row + 1 + below))) below++;
        int band = 2 - below;

        if (map.At(x, row) == '+')
        {
            bool left = map.At(x - 1, row) == '+';
            bool right = map.At(x + 1, row) == '+';
            return (left && right ? Tiles.WindowMiddle : right ? Tiles.WindowLeft : Tiles.WindowRight)[band];
        }

        int pick = (x * 7349 + 13) % 11;
        return (pick == 3 ? Tiles.FaceVent : pick == 7 ? Tiles.FaceSign : Tiles.FacePlain)[band];
    }

    internal static GameObject PlacePlayer(Map map, GameObject prefab)
    {
        Vector2Int start = map.Find('P')[0];
        Vector2 spawn = map.Center(start.x, start.y);
        GameObject player = Spawn(prefab, null, new Vector3(spawn.x, spawn.y, CharacterZ));

        var controller = player.GetComponent<PlayerController>();
        Record(controller);

        var footsteps = player.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.spatialBlend = 0f;
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("audioSource").objectReferenceValue = footsteps;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        var lantern = player.GetComponentInChildren<Light2D>();
        if (lantern != null)
        {
            lantern.pointLightOuterRadius = 5.5f;
            lantern.pointLightInnerRadius = 0.4f;
            lantern.intensity = 0.55f;
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
        rumble.secondsBetween = new Vector2(14f, 22f);
        rumble.debrisPerRumble = new Vector2Int(1, 1);
        rumble.debrisMinDistance = 4f;
        rumble.debrisMaxDistance = 8f;
        rumble.aimAtPlayerChance = 0f;
        rumble.aheadChance = 0.75f;
        rumble.floor = floor;
        rumble.globalLight = globalLight;
        return rumble;
    }

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

    static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T effect)) return effect;
        effect = profile.Add<T>();
        effect.name = typeof(T).Name;
        effect.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(effect, profile);
        return effect;
    }

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

    static BlastDoor PlaceDoor(Map map, char marker, string doorName, Transform parent)
    {
        if (!map.TryGetArea(marker, out Rect area)) return null;
        var doorObject = new GameObject(doorName);
        doorObject.transform.SetParent(parent);
        doorObject.transform.position = area.center;
        doorObject.AddComponent<BoxCollider2D>().size = area.size;
        var door = doorObject.AddComponent<BlastDoor>();
        return door;
    }

    static RobotEncounter PlaceRobots(Map map, char marker, string groupName, Transform parent, GameObject prefab,
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
        encounter.robots = group.GetComponentsInChildren<Health>(true).ToList();
        return encounter;
    }

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

    static List<TutorialDirector.WallBreach> PlaceBreachChasers(Map map, GameObject prefab, Transform parent)
    {
        var breaches = new List<TutorialDirector.WallBreach>();
        foreach (Vector2Int at in map.Find('Y'))
        {
            Vector2 center = map.Center(at.x, at.y);
            GameObject robot = Spawn(prefab, parent, new Vector3(center.x, center.y + 0.15f, CharacterZ));
            var brain = robot.GetComponent<PlaceholderRobot>();
            brain.enabled = false;
            brain.patrolRoute = new Vector2[0];
            Record(brain);
            robot.SetActive(false);
            Record(robot);
            breaches.Add(new TutorialDirector.WallBreach { robot = robot.GetComponent<Health>(), point = BreachPoint(robot.transform, center) });
        }
        return breaches;
    }

    static float TwoHitHealth()
    {
        var wrench = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/Data/Weapons/Wrench.asset");
        if (wrench != null && wrench.damage > 0f) return wrench.damage * 2f;
        Debug.LogWarning("Tutorial builder: there's no wrench at Data/Weapons/Wrench, so the fight room's robots keep the prefab's health.");
        return LoadPrefab("Characters/PlaceholderRobot").GetComponent<Health>().maxHealth;
    }

    static List<TutorialDirector.WallBreach> BreachesFor(RobotEncounter group)
    {
        var breaches = new List<TutorialDirector.WallBreach>();
        if (group == null) return breaches;
        foreach (Health robot in group.robots)
        {
            if (robot == null) continue;
            Record(robot.gameObject);
            Vector2 center = (Vector2)robot.transform.position - new Vector2(0f, 0.15f);
            breaches.Add(new TutorialDirector.WallBreach { robot = robot, point = BreachPoint(robot.transform, center) });
        }
        return breaches;
    }

    static Transform BreachPoint(Transform robot, Vector2 cellCenter)
    {
        var point = new GameObject("Breach Point").transform;
        point.SetParent(robot.parent);
        point.position = cellCenter + new Vector2(0f, 1.6f);
        return point;
    }

    static void PlaceReactorDoor(Map map, Transform doors, Transform lights)
    {
        BlastDoor door = PlaceDoor(map, 'R', "Reactor Door", doors);
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

    static void StandGuard(PlaceholderRobot robot, float sightRange)
    {
        robot.patrolRoute = new Vector2[0];
        robot.fieldOfView = 360f;
        robot.sightRange = sightRange;
    }

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

    internal static List<StationLight> PlaceLamps(Map map, Transform parent, string breakSound)
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
                lamp.breakSound = breakSound;
                if (lamps.Count % 3 == 1) lamp.swing = 0.45f;
                lamp.dropOnBreak = 0.6f;
                lamps.Add(lamp);
            }
        }
        return lamps;
    }

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

    internal static void PlaceSunlight(Map map, Transform parent)
    {
        for (int row = 0; row < map.Height; row++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.At(x, row) != '+' || map.At(x - 1, row) == '+' || !Map.IsFloor(map.At(x, row + 1))) continue;

                int width = 0;
                while (map.At(x + width, row) == '+') width++;
                int depth = 0;
                while (depth < 16 && Map.IsFloor(map.At(x + width / 2, row + 1 + depth))) depth++;
                float slant = depth * 0.45f;

                var sun = new GameObject("Sunlight");
                sun.transform.SetParent(parent);
                sun.transform.position = new Vector3(map.Origin.x + x, map.Origin.y + map.Height - 1 - row, 0f);

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

    internal static SonnyBox PlaceSonny(Map map, Transform parent)
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
        return sonny;
    }

    internal static void PlaceLockers(Map map, GameObject prefab, Transform parent)
    {
        foreach (Vector2Int at in map.Find('L'))
        {
            Vector2 center = map.Center(at.x, at.y);
            Spawn(prefab, parent, new Vector3(center.x, center.y + 0.3f, CharacterZ));
        }
    }

    class Furnishing
    {
        public readonly string name;
        public readonly int[][] rows;
        public readonly int wallRows;

        public Furnishing(string name, int[][] rows, int wallRows = 0)
        {
            this.name = name;
            this.rows = rows;
            this.wallRows = wallRows;
        }

        public int Width => rows.Max(row => row.Length);
    }

    static readonly Dictionary<char, Furnishing> Furnishings = new Dictionary<char, Furnishing>
    {
        { 'K', new Furnishing("Monitor Bank", new[] { new[] { 179, 180 } }) },
        { 'k', new Furnishing("Control Desk", new[] { new[] { 60, 61 } }) },
        { 'Q', new Furnishing("Console Bank", new[] { new[] { 59, 60, 61, 117, 118, 119 }, new[] { 88, -1, -1, -1, 148, 149 } }) },
        { 'C', new Furnishing("Chair", new[] { new[] { 89 }, new[] { 120 } }) },
        { 'T', new Furnishing("Bench", new[] { new[] { 15, 16, 17 }, new[] { 39, 40, 41 } }) },
        { 'B', new Furnishing("Sofa", new[] { new[] { 18, 19, 20 }, new[] { 42, 43, 44 } }) },
        { 'O', new Furnishing("Pod", new[] { new[] { 13, 14 }, new[] { 37, 38 }, new[] { 62, 63 } }) },
        { 'I', new Furnishing("Wall Pipe", new[] { new[] { 400 }, new[] { 428 }, new[] { 457 } }, wallRows: 1) },
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

    internal static void PlaceFurniture(Map map, Transform parent)
    {
        foreach (KeyValuePair<char, Furnishing> entry in Furnishings)
        {
            Furnishing piece = entry.Value;
            int height = piece.rows.Length;
            foreach (Vector2Int at in map.Find(entry.Key))
            {
                float lift = DepthSort.FeetToCenter;
                var furniture = new GameObject(piece.name);
                furniture.transform.SetParent(parent);
                furniture.transform.position = new Vector3(map.Origin.x + at.x, map.Origin.y + map.Height - 1 - at.y + 0.1f + lift, CharacterZ);
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

                FurnitureFootprint.AddColliders(furniture, piece.name, piece.rows, piece.wallRows, lift + 0.1f);

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

    static System.Func<Stretch, int, int, int> FloorKit(Stretch stretch)
    {
        if (stretch.Has('a')) return (s, x, y) => NineSlice(x, y, s.Width, s.Height, 157, 158, 159, 189, 190, 191, 219, 220, 221);
        if (stretch.Has('S')) return Walkways;
        if (stretch.Has('m')) return (s, x, y) => NineSlice(x, y, s.Width, s.Height, 5, 6, 7, 30, 31, 32, 54, 55, 56);
        if (stretch.IsHallway) return (s, x, y) => y == 0 || y == s.Height - 1 ? 370 : 339;
        return null;
    }

    static int Walkways(Stretch stretch, int x, int y)
    {
        int fromBottom = stretch.Height - 1 - y;
        if (y <= 1 || fromBottom <= 1) return 339;
        return y == 2 || fromBottom == 2 ? 430 : 370;
    }

    static int NineSlice(int x, int y, int width, int height, params int[] kit)
    {
        int column = x == 0 ? 0 : x == width - 1 ? 2 : 1;
        int row = y == 0 ? 0 : y == height - 1 ? 2 : 1;
        return kit[row * 3 + column];
    }

    static readonly int[] MachineLeftCap = { 68, 95, 126 };
    static readonly int[] MachineRightCap = { 74, 101, 132 };
    static readonly int[] MachineMiddle = { 69, 96, 127 };
    static readonly int[] CeilingFixtures = { 249, 250, 249, 249, 249, 251, 251, 249, 249 };
    const int CeilingLight = 310;

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

            if (!stretch.IsHallway && (stretch.Has('m') || stretch.Has('h') || stretch.Has('a')))
                MachineWall(map, walls, stretch);

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

            if (stretch.Has('h') && !stretch.IsHallway && stretch.Width >= CeilingFixtures.Length + 4)
            {
                int start = stretch.minX + (stretch.Width - CeilingFixtures.Length) / 2;
                for (int i = 0; i < CeilingFixtures.Length; i++)
                    overhead.SetTile(map.Cell(start + i, middleRow), DecorTile(CeilingFixtures[i]));
            }
        }
    }

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

    static bool IsPlainFace(Map map, int x, int floorRow)
    {
        for (int band = 1; band <= 3; band++)
        {
            char c = map.At(x, floorRow - band);
            if (!Map.IsFace(c) || c == '+') return false;
        }
        return true;
    }

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

    static void AddSceneToBuild()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

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
