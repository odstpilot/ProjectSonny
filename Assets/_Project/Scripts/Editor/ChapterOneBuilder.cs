using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Map = TutorialLevelBuilder.Map;
using Room = FloorOneBuilder.Room;

// builds chapter 1, and the story bits for chapter 2
public static class ChapterOneBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Chapter1.unity";
    const string CrewPrefabPath = "Assets/_Project/Prefabs/Characters/CrewMember.prefab";
    const string CrewMaterialPath = "Assets/_Project/Art/Shaders/CrewSprite.mat";
    const string CrewShader = "Sonny/Crew Sprite";
    const float CharacterZ = 1f;
    const float RingRadius = 1f;
    const int Seed = 1026;

    class Crowd
    {
        public int bots;
        public int walking;
        public int standing;
        public int[] groups = new int[0];
        public bool atConsoles;
    }

    static readonly Dictionary<char, Crowd> Crowds = new Dictionary<char, Crowd>
    {
        { 'h', new Crowd { walking = 3, standing = 2, groups = new[] { 3 }, bots = 1 } },
        { 'k', new Crowd { walking = 3, standing = 2, groups = new[] { 3, 2 }, atConsoles = true, bots = 1 } },
        { 'b', new Crowd { walking = 2, standing = 1, groups = new[] { 2 } } },
        { 'm', new Crowd { walking = 2, standing = 3, groups = new[] { 2, 3 }, atConsoles = true, bots = 2 } },
        { 'c', new Crowd { walking = 1, standing = 1 } },
        { 'o', new Crowd { walking = 1, standing = 4, groups = new[] { 2 } } },
    };

    static readonly Color[] TrimColors =
    {
        new Color(0.22f, 0.48f, 0.95f),
        new Color(0.26f, 0.72f, 0.36f),
        new Color(0.95f, 0.78f, 0.22f),
        new Color(0.6f, 0.36f, 0.86f),
        new Color(0.2f, 0.74f, 0.78f),
        new Color(0.9f, 0.42f, 0.66f),
        new Color(0.42f, 0.45f, 0.5f),
    };

    static readonly string[][] Conversations =
    {
        new[] { "So the new AI goes in today?", "Supposedly, yeah, smartest thing anyone's ever built", "Smarter than the last one, anyway", "Pretty low bar" },
        new[] { "Flare season's starting early this year", "Eh, the station's rated for worse", "Yeah, that's what the numbers say anyway" },
        new[] { "Anyone fixed the toilet on this deck yet?", "Don't... just don't", "I'll take that as a no then" },
        new[] { "Did you see the new tech? They're on a temp badge", "Yeah, their real one's coming later, apparently", "Typical" },
        new[] { "Three weeks till rotation", "Not that you're counting or anything", "Nineteen days, but who's counting" },
        new[] { "Once the AI's running things, what do we even do?", "Sleep, hopefully", "Let the machine keep watch for once" },
        new[] { "Reactor output's up again", "Good, Earth's gonna need it", "Earth always needs it, man" },
        new[] { "Did you hear the reactor last night?", "It always hums", "Louder, I mean" },
        new[] { "Who's on the late shift?", "Me, again", "You volunteered though", "I lost a bet, that's different" },
        new[] { "Supply run brought oranges", "Real ones?", "Real ones, so hide yours" },
        new[] { "The maintenance bots keep lining up by the reactor door", "Waiting for orders, probably", "Creepy as hell though" },
        new[] { "Think the AI will talk to us?", "It'll talk, but whether it listens is another thing" },
    };

    static readonly string[] Greeting =
    {
        "Hey! You must be the new technician, the one installing the AI",
        "Your badge isn't in yet? Ugh, supply's always slow... here's a temp, it'll open the control room, and let me switch on your suit's helper",
        "They're waiting on you in the control room, straight through the maintenance deck",
    };

    const string ChiefName = "Voss";
    const string ChiefJob = "Station Chief";
    static readonly Color ChiefTrim = new Color(0.93f, 0.93f, 0.96f);
    static readonly Color SonnyLens = new Color(0.35f, 0.82f, 1f);

    const char RestroomRoom = 'w';
    const int Stalls = 3;
    const char ToolsRoom = 's';

    const string VentLayout =
        "###############\n" +
        "#2...d...#....#\n" +
        "#.######.#.##.#\n" +
        "#s#....#.#.#..#\n" +
        "#.#.##.#...#.##\n" +
        "#...##.#####x##\n" +
        "####...r.....##\n" +
        "#....#######..#\n" +
        "#..d.....d.x.1#\n" +
        "###############";
    static readonly Dictionary<char, int> BodiesPerRoom = new Dictionary<char, int>
    {
        { 'k', 6 }, { 'h', 5 }, { 'b', 4 }, { 'm', 6 }, { 'c', 2 },
    };
    static readonly Dictionary<char, int> PatrolsPerRoom = new Dictionary<char, int> { { 'k', 2 }, { 'h', 2 }, { 'm', 3 }, { 'd', 3 }, { 'g', 3 }, { 'r', 5 } };
    static readonly Dictionary<char, PlaceholderRobot.BotKind[]> PatrolLooks = new Dictionary<char, PlaceholderRobot.BotKind[]>
    {
        { 'm', new[] { PlaceholderRobot.BotKind.Sweeper, PlaceholderRobot.BotKind.Hauler } },
        { 'd', new[] { PlaceholderRobot.BotKind.Sweeper, PlaceholderRobot.BotKind.Sweeper, PlaceholderRobot.BotKind.Hauler } },
        { 'g', new[] { PlaceholderRobot.BotKind.Sweeper, PlaceholderRobot.BotKind.Arc, PlaceholderRobot.BotKind.Sweeper } },
        { 'r', new[] { PlaceholderRobot.BotKind.Sweeper, PlaceholderRobot.BotKind.Arc, PlaceholderRobot.BotKind.Hauler, PlaceholderRobot.BotKind.Arc, PlaceholderRobot.BotKind.Sweeper } },
    };
    const int PatrolFromDoors = 4;
    const int PatrolSpacing = 3;
    static readonly Dictionary<char, string> BadgeRooms = new Dictionary<char, string> { { 'o', "control_room" } };
    const string GuardName = "Reyes";
    const string GuardJob = "Security";
    static readonly string[] GuardLines =
    {
        "Control room's badge-only, but you're the new tech, right? Your temp'll open it, go on through",
        "Anywhere there's a reader, just stick close to one of us and the door'll open",
    };

    internal static Teleporter ControlRoomDoor { get; private set; }

    const string VentTilePath = "Assets/_Project/Art/Environment/Tilesets/ShipTiles/tileset_29.asset";
    const int IgnoreRaycastLayer = 2;
    const string AttendantName = "Okafor";
    const string AttendantJob = "Life Support";

    static readonly Vector2Int[] Around =
    {
        new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(-1, 0),
        new Vector2Int(1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1)
    };

    [MenuItem("Sonny/Build Chapter 1")]
    public static void BuildFromMenu()
    {
        bool rebuild = EditorUtility.DisplayDialog("Build Chapter 1",
            "Rebuild Chapter1.unity from Floor1Layout.txt and fill it with crew?\n\nThis replaces everything in the scene except the Sound Manager. Anything else added to it by hand will be lost.",
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
        GameObject playerPrefab = TutorialLevelBuilder.LoadPrefab("Characters/Player 1");
        if (playerPrefab == null) return false;
        GameObject crewPrefab = CrewPrefab(playerPrefab);
        if (crewPrefab == null) return false;

        int crew = 0;
        bool built = FloorOneBuilder.Build(ScenePath, floors => crew = Populate(floors, crewPrefab, false));
        if (!built) return false;

        AddSceneToBuild();
        Debug.Log($"Chapter 1 builder: put {crew} crew aboard and saved {ScenePath}.");
        return true;
    }

    static GameObject CrewPrefab(GameObject playerPrefab)
    {
        Material material = CrewMaterial();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrewPrefabPath) ?? MakeCrewPrefab(playerPrefab, material);
        if (prefab == null) return null;

        using (var edit = new PrefabUtility.EditPrefabContentsScope(CrewPrefabPath))
        {
            GameObject crew = edit.prefabContentsRoot;
            var sprite = crew.GetComponent<SpriteRenderer>();
            if (material != null) sprite.sharedMaterial = material;
            var playerSprite = playerPrefab.GetComponentInChildren<SpriteRenderer>();
            if (playerSprite != null) sprite.color = playerSprite.color;
            BoxCollider2D feet = crew.GetComponent<BoxCollider2D>() ?? crew.AddComponent<BoxCollider2D>();
            if (sprite.sprite != null)
            {
                DepthDressing.SetFootprint(feet, sprite.sprite);
                DepthDressing.AddShadow(crew, sprite.sprite);
            }
        }
        return prefab;
    }

    static GameObject MakeCrewPrefab(GameObject playerPrefab, Material material)
    {
        var playerSprite = playerPrefab.GetComponentInChildren<SpriteRenderer>();
        var playerAnimator = playerPrefab.GetComponentInChildren<Animator>();
        if (playerSprite == null || playerSprite.sprite == null || playerAnimator == null)
        {
            Debug.LogError("Chapter 1 builder: the player prefab has no sprite or Animator for the crew to copy.");
            return null;
        }

        var crew = new GameObject("CrewMember");
        crew.transform.localScale = playerSprite.transform.lossyScale;
        var sprite = crew.AddComponent<SpriteRenderer>();
        sprite.sprite = playerSprite.sprite;
        sprite.sortingLayerName = DepthSort.Layer;
        if (material != null) sprite.sharedMaterial = material;
        crew.AddComponent<Animator>().runtimeAnimatorController = playerAnimator.runtimeAnimatorController;

        var body = crew.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        crew.AddComponent<BoxCollider2D>();
        crew.AddComponent<CrewMember>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(crew, CrewPrefabPath);
        Object.DestroyImmediate(crew);
        if (prefab == null) Debug.LogError($"Chapter 1 builder: couldn't save {CrewPrefabPath}.");
        return prefab;
    }

    static Material CrewMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(CrewMaterialPath);
        if (material != null) return material;

        Shader shader = Shader.Find(CrewShader);
        if (shader == null)
        {
            Debug.LogWarning($"Chapter 1 builder: the {CrewShader} shader isn't loaded, so the crew keep the player's red trim. Run this again once it compiles.");
            return null;
        }
        material = new Material(shader) { name = "CrewSprite" };
        AssetDatabase.CreateAsset(material, CrewMaterialPath);
        return material;
    }

    internal static Vector2? WakeSpot { get; private set; }

    internal static bool PopulateChapterTwo(FloorOneBuilder.BuiltFloor[] floors)
    {
        GameObject playerPrefab = TutorialLevelBuilder.LoadPrefab("Characters/Player 1");
        if (playerPrefab == null) return false;
        GameObject crewPrefab = CrewPrefab(playerPrefab);
        if (crewPrefab == null) return false;
        Populate(floors, crewPrefab, true);
        return true;
    }

    static int Populate(FloorOneBuilder.BuiltFloor[] floors, GameObject crewPrefab, bool chapterTwo)
    {
        FloorOneBuilder.BuiltFloor floor = floors[0];
        if (!chapterTwo)
            foreach (FloorOneBuilder.BuiltFloor each in floors)
                foreach (Stairway stairs in each.stairs)
                {
                    stairs.destination = null;
                    TutorialLevelBuilder.Record(stairs);
                }
        SoundDefaults.Fill(floor.level.gameObject.scene, SoundDefaults.Sonny, SoundDefaults.ChapterOne);
        WakeSpot = null;

        Map map = floor.map;
        Unarm(floor.player);

        var random = new System.Random(Seed);
        var bodyDice = new System.Random(Seed + 1);
        var bodies = new List<(Vector2 at, Color trim, string[] lines)>();
        Transform crewRoot = TutorialLevelBuilder.Group("Crew", floor.level);
        Vector2Int start = map.Find('P')[0];
        CrewMember greeter = null, chief = null, attendant = null;
        var toilets = new List<Toilet>();
        ToolShelf shelf = null;
        ToolCrate crate = null;
        List<Vector2Int> installPoints = map.Find('S');
        Vector2Int install = installPoints.Count > 0 ? installPoints[0] : new Vector2Int(-100, -100);
        int conversation = 0, count = 0;
        botsPlaced = 0;
        Room restroom = floor.rooms.FirstOrDefault(r => r.marker == RestroomRoom);
        List<Vector2Int> restroomDoors = restroom == null ? new List<Vector2Int>()
            : restroom.cells.Where(c => char.IsDigit(map.At(c.x, c.y))).ToList();
        var badgeDoorMarks = new HashSet<char>(floor.rooms.Where(r => BadgeRooms.ContainsKey(r.marker))
            .SelectMany(r => r.cells).Select(c => map.At(c.x, c.y)).Where(char.IsDigit));
        var guards = new List<CrewMember>();
        Vector2? comesInAt = null;
        Vector2Int? storageVent = null, restroomVent = null;

        foreach (Room room in floor.rooms)
        {
            bool hasStart = room.cells.Contains(start);
            if (!Crowds.TryGetValue(room.marker, out Crowd crowd) && !hasStart && room.marker != RestroomRoom && room.marker != ToolsRoom) continue;
            crowd = crowd ?? new Crowd();
            Transform roomGroup = TutorialLevelBuilder.Group(room.name, crewRoot);

            var walkable = new HashSet<Vector2Int>(room.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !floor.furniture.Contains(c)));
            var free = new HashSet<Vector2Int>(walkable.Where(c => !NearKeptClear(map, c) && Chebyshev(c, start) > 2));

            void Take(Vector2Int cell)
            {
                walkable.Remove(cell);
                free.RemoveWhere(other => Chebyshev(other, cell) <= 1);
            }

            if (hasStart && TryGreeterSpot(free, start, out Vector2Int spot))
            {
                greeter = Spawn(crewPrefab, roomGroup, StandingAt(map, spot), "Greeter", Pick(random), CrewMember.Role.Greet, Toward(spot, start));
                greeter.greeting = Greeting;
                TutorialLevelBuilder.Record(greeter);
                Take(spot);
                count++;
            }

            if (room.cells.Contains(install) && TryChiefSpot(free, install, out Vector2Int chiefAt))
            {
                chief = Spawn(crewPrefab, roomGroup, StandingAt(map, chiefAt), "Chief", ChiefTrim, CrewMember.Role.Stand, Toward(chiefAt, install));
                chief.displayName = ChiefName;
                chief.jobTitle = ChiefJob;
                TutorialLevelBuilder.Record(chief);
                Take(chiefAt);
                for (int below = 1; below <= 2; below++) Take(install + new Vector2Int(0, below));
                count++;
            }

            if (room.marker == RestroomRoom)
            {
                if (TryStallRow(map, floor.furniture, walkable, restroomDoors, Stalls, out List<Vector2Int> stalls))
                {
                    foreach (Vector2Int stall in stalls)
                    {
                        toilets.Add(PlaceToilet(map, stall, roomGroup));
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            Take(stall + new Vector2Int(dx, 0));
                            Take(stall + new Vector2Int(dx, 1));
                        }
                    }
                    toilets[0].startBroken = true;
                    TutorialLevelBuilder.Record(toilets[0]);
                    if (toilets.Count > 2)
                    {
                        Toilet taken = toilets[1];
                        taken.occupied = true;
                        TutorialLevelBuilder.Record(taken);
                        Vector2Int outside = stalls[1] + Vector2Int.up;
                        comesInAt = StandingAt(map, outside);
                        attendant = Spawn(crewPrefab, roomGroup, comesInAt.Value, "Restroom", Pick(random), CrewMember.Role.Stand, Vector2.down);
                        attendant.displayName = AttendantName;
                        attendant.jobTitle = AttendantJob;
                        TutorialLevelBuilder.Record(attendant);
                        attendant.gameObject.SetActive(false);
                        TutorialLevelBuilder.Record(attendant.gameObject);
                        count++;
                    }
                }
                var awayFrom = new List<Vector2Int>(restroomDoors);
                awayFrom.AddRange(toilets.Select(t => CellAt(map, t.transform.position)));
                if (TryFarFrom(free, awayFrom, out Vector2Int vent))
                {
                    restroomVent = vent;
                    Take(vent);
                }
            }

            if (room.marker == ToolsRoom)
            {
                List<Vector2Int> doors = room.cells.Where(c => FloorOneBuilder.IsKeptClear(map.At(c.x, c.y))).ToList();
                if (TryBackWallSpot(map, floor.furniture, walkable, doors, out Vector2Int rack))
                {
                    shelf = PlaceShelf(map, rack, roomGroup);
                    WakeSpot = StandingAt(map, rack + Vector2Int.up);
                    for (int dx = -1; dx <= 1; dx++) Take(rack + new Vector2Int(dx, 0));
                    Take(rack + Vector2Int.up);
                    doors.Add(rack);
                }
                if (TryCrateSpot(free, doors, out Vector2Int box))
                {
                    crate = PlaceCrate(map, box, roomGroup);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            Take(box + new Vector2Int(dx, dy));
                    doors.Add(box);
                }
                if (TryFarFrom(free, doors, out Vector2Int vent))
                {
                    storageVent = vent;
                    Take(vent);
                }
            }

            List<Vector2Int> toBadgeRoom = room.cells.Where(c => badgeDoorMarks.Contains(map.At(c.x, c.y))).ToList();
            if (!BadgeRooms.ContainsKey(room.marker) && toBadgeRoom.Count > 0 && TryNearDoor(free, toBadgeRoom, out Vector2Int post))
            {
                Vector2Int door = toBadgeRoom.OrderBy(c => (c - post).sqrMagnitude).First();
                CrewMember guard = Spawn(crewPrefab, roomGroup, StandingAt(map, post), "Guard", Pick(random), CrewMember.Role.Stand, Toward(door, post));
                guard.displayName = GuardName;
                guard.jobTitle = GuardJob;
                guard.talkLines = GuardLines;
                TutorialLevelBuilder.Record(guard);
                guards.Add(guard);
                Take(post);
                count++;
            }

            foreach (int size in crowd.groups)
            {
                if (!TryPlaceGroup(map, free, size, random, out Vector2Int middle, out Vector2[] ring))
                {
                    Debug.LogWarning($"Chapter 1 builder: there's no room for a group of {size} in the {room.name}, so they're left out.");
                    continue;
                }

                var chat = new GameObject($"Group of {size}").AddComponent<CrewChat>();
                chat.transform.SetParent(roomGroup);
                Vector2 center = StandingAt(map, middle);
                chat.transform.position = center;
                if (conversation == Conversations.Length)
                    Debug.LogWarning("Chapter 1 builder: there are more groups than Conversations, so some say the same thing. Write a few more.");
                chat.lines = Conversations[conversation++ % Conversations.Length];
                var huddle = chat.gameObject.AddComponent<CircleCollider2D>();
                huddle.radius = RingRadius * 0.75f;
                huddle.offset = new Vector2(0f, DepthDressing.Footprint.y * 0.5f - DepthSort.FeetToCenter);
                foreach (Vector2 at in ring)
                {
                    CrewMember member = Spawn(crewPrefab, chat.transform, at, "Talking", Pick(random), CrewMember.Role.Chat, center - at);
                    chat.members.Add(member);
                    count++;
                }
                Take(middle);
                foreach (Vector2 at in ring) Take(FeetCell(map, at));
            }

            for (int i = 0; i < crowd.standing; i++)
            {
                if (!TryStandingSpot(map, floor.furniture, floor.seats, free, crowd.atConsoles, random, out Vector2Int cell, out Vector2 facing))
                {
                    Debug.LogWarning($"Chapter 1 builder: there's no room for anyone else to stand in the {room.name}.");
                    break;
                }
                Spawn(crewPrefab, roomGroup, StandingAt(map, cell), "Standing", Pick(random), CrewMember.Role.Stand, facing);
                Take(cell);
                count++;
            }

            if (crowd.walking > 0 || crowd.bots > 0)
            {
                CrewWalkArea area = MakeWalkArea(map, room, walkable, roomGroup);
                List<Vector2Int> starts = free.Where(walkable.Contains).OrderBy(_ => random.Next()).ToList();
                int started = 0;
                for (int i = 0; i < crowd.walking && started < starts.Count; i++)
                {
                    CrewMember walker = Spawn(crewPrefab, roomGroup, StandingAt(map, starts[started++]), "Walking", Pick(random), CrewMember.Role.Wander,
                        Vector2.down);
                    walker.area = area;
                    walker.walkSpeed = 1.4f + (float)random.NextDouble() * 0.8f;
                    TutorialLevelBuilder.Record(walker);
                    count++;
                }
                for (int i = 0; i < crowd.bots && started < starts.Count; i++)
                    PlaceBot(StandingAt(map, starts[started++]), area, roomGroup);
            }

            if (BodiesPerRoom.TryGetValue(room.marker, out int lying))
                foreach (Vector2Int cell in free.OrderBy(_ => bodyDice.Next()).Take(lying).ToList())
                {
                    bodies.Add((StandingAt(map, cell), Pick(bodyDice), new string[0]));
                    Take(cell);
                }
        }
        PlaceInspectables(floor, chapterTwo);
        if (attendant != null) bodies.Insert(0, (attendant.transform.position, attendant.trimColor, new string[0]));
        if (greeter != null) bodies.Insert(0, (greeter.transform.position, greeter.trimColor, new string[0]));
        foreach (CrewMember guard in guards) bodies.Add((guard.transform.position, guard.trimColor, new string[0]));
        SetBadgeDoors(floor);
        PlaceCameras(floors, chapterTwo);

        if (!chapterTwo)
        {
            PlaceDirector(map, floor.player, greeter, floor.level);
            PlaceInstall(map, floor.player, chief, greeter, guards, floor.level);
            PlaceBreak(floor.player, toilets, attendant, comesInAt, crate, shelf, floor.level);
            return count;
        }

        foreach (CrewMember living in crewRoot.GetComponentsInChildren<CrewMember>(true)) Object.DestroyImmediate(living.gameObject);
        foreach (CrewChat chat in crewRoot.GetComponentsInChildren<CrewChat>(true)) Object.DestroyImmediate(chat.gameObject);
        foreach (MaintenanceBot bot in crewRoot.GetComponentsInChildren<MaintenanceBot>(true)) Object.DestroyImmediate(bot.gameObject);
        foreach (CrewWalkArea area in crewRoot.GetComponentsInChildren<CrewWalkArea>(true)) Object.DestroyImmediate(area.gameObject);
        foreach (Toilet toilet in toilets)
        {
            toilet.startBroken = true;
            toilet.occupied = false;
            TutorialLevelBuilder.Record(toilet);
        }
        if (crate != null)
        {
            crate.startOpen = true;
            TutorialLevelBuilder.Record(crate);
        }
        if (shelf != null)
        {
            shelf.startToppled = true;
            TutorialLevelBuilder.Record(shelf);
        }
        PlaceEscape(map, floor, storageVent, restroomVent);
        PlaceAftermath(floor, crewPrefab, bodies, bodyDice);
        Workshop workshop = PlaceWorkbench(floors, out HashSet<Vector2Int> benchFront);
        PlaceRacks(floors, benchFront, new System.Random(Seed + 3));
        DressRooms(floors, benchFront, new System.Random(Seed + 4));
        CommsRing comms = PlaceCommsRing(floors);
        PlaceReactorGauntlet(floors, comms, new System.Random(Seed + 5));
        PlaceScrap(floors, workshop, bodies.Select(b => FeetCell(map, b.at)));
        string afterBots = PlacePatrols(floors, bodies.Select(b => FeetCell(map, b.at)), new System.Random(Seed + 2));
        PlaceBlockedWays(map, floor, afterBots);
        PlaceWallGrab(map, floor);
        PlaceUpperDeckArrival(floors, workshop);
        PlaceControlRoomAccess(floors, comms);
        return 0;
    }

    static bool TryChiefSpot(HashSet<Vector2Int> free, Vector2Int install, out Vector2Int spot)
    {
        spot = default;
        var wanted = install + new Vector2Int(-2, 1);
        var near = free.Where(c => Chebyshev(c, install) == 2 && c.y >= install.y && c.x != install.x).ToList();
        if (near.Count == 0) return false;
        spot = near.OrderBy(c => (c - wanted).sqrMagnitude).First();
        return true;
    }

    static void PlaceInstall(Map map, GameObject player, CrewMember chief, CrewMember greeter, List<CrewMember> guards, Transform level)
    {
        SonnyBox sonny = TutorialLevelBuilder.PlaceSonny(map, level);
        if (sonny == null)
        {
            Debug.LogWarning("Chapter 1 builder: there's no S in the layout, so there's nothing to install in the control room.");
            return;
        }
        sonny.startPowered = false;
        sonny.look = SonnyBox.Look.Installed;
        sonny.lensColor = SonnyLens;
        if (sonny.glow != null) sonny.glow.color = SonnyLens;
        TutorialLevelBuilder.Record(sonny);
        if (chief == null) Debug.LogWarning("Chapter 1 builder: there was no room for the chief by the install point, so the install plays without them.");

        var cutscene = new GameObject("Control Room Cutscene").AddComponent<ControlRoomCutscene>();
        cutscene.transform.SetParent(level);
        cutscene.player = player.GetComponent<PlayerController>();
        cutscene.sonny = sonny;
        cutscene.chief = chief;
        cutscene.greeter = greeter;
        cutscene.guards = guards.ToArray();
        TutorialLevelBuilder.Record(cutscene);
    }

    static bool TryBackWallSpot(Map map, HashSet<Vector2Int> furniture, HashSet<Vector2Int> walkable, List<Vector2Int> doors, out Vector2Int spot)
    {
        spot = default;
        if (walkable.Count == 0) return false;
        int top = walkable.Min(c => c.y);
        bool Clear(Vector2Int c) => walkable.Contains(c) && !furniture.Contains(c) && !NearKeptClear(map, c);
        var fits = walkable.Where(c => c.y == top && Clear(c + Vector2Int.left) && Clear(c) && Clear(c + Vector2Int.right)
            && Clear(c + Vector2Int.up)).ToList();
        if (fits.Count == 0) return false;
        spot = fits.OrderByDescending(c => doors.Count == 0 ? 0 : doors.Min(d => (c - d).sqrMagnitude)).First();
        return true;
    }

    static bool TryStallRow(Map map, HashSet<Vector2Int> furniture, HashSet<Vector2Int> walkable, List<Vector2Int> doors, int count,
        out List<Vector2Int> stalls)
    {
        stalls = null;
        if (walkable.Count == 0) return false;
        int top = walkable.Min(c => c.y);
        bool Clear(Vector2Int c) => walkable.Contains(c) && !furniture.Contains(c) && !NearKeptClear(map, c);
        int span = count * 2 + 1;
        var starts = walkable.Where(c => c.y == top && Enumerable.Range(0, span).All(dx =>
            Clear(c + new Vector2Int(dx, 0)) && Clear(c + new Vector2Int(dx, 1)))).ToList();
        if (starts.Count == 0) return false;
        int Away(Vector2Int c) => doors.Count == 0 ? 0 : doors.Min(d => Chebyshev(c, d));
        Vector2Int start = starts.OrderByDescending(s => Enumerable.Range(0, span).Min(dx => Away(s + new Vector2Int(dx, 0)))).First();
        stalls = Enumerable.Range(0, count).Select(i => start + new Vector2Int(1 + i * 2, 0)).OrderBy(Away).ToList();
        return true;
    }

    static bool TryCrateSpot(HashSet<Vector2Int> free, List<Vector2Int> away, out Vector2Int spot)
    {
        spot = default;
        var roomy = free.Where(c => Around.All(step => free.Contains(c + step))).ToList();
        if (roomy.Count == 0) return false;
        spot = roomy.OrderByDescending(c => away.Count == 0 ? 0 : Mathf.Min(4, away.Min(a => Chebyshev(c, a))))
            .ThenBy(c => c.y).ThenByDescending(c => c.x).First();
        return true;
    }

    static bool TryFarFrom(HashSet<Vector2Int> free, List<Vector2Int> away, out Vector2Int spot)
    {
        spot = default;
        if (free.Count == 0) return false;
        spot = free.OrderByDescending(c => away.Count == 0 ? 0 : away.Min(a => Chebyshev(c, a))).ThenBy(c => c.x).ThenBy(c => c.y).First();
        return true;
    }

    static bool TryNearDoor(HashSet<Vector2Int> free, List<Vector2Int> doors, out Vector2Int spot)
    {
        spot = default;
        if (doors.Count == 0) return false;
        int Away(Vector2Int c) => doors.Min(d => Chebyshev(c, d));
        var near = free.Where(c => Away(c) >= 2 && Away(c) <= 3).ToList();
        if (near.Count == 0) return false;
        Vector2 middle = new Vector2((float)doors.Average(d => d.x), (float)doors.Average(d => d.y));
        spot = near.OrderBy(Away).ThenBy(c => (c - middle).sqrMagnitude).First();
        return true;
    }

    static Toilet PlaceToilet(Map map, Vector2Int cell, Transform parent)
    {
        Vector2 center = map.Center(cell.x, cell.y);
        var stall = new GameObject("Toilet Stall");
        stall.transform.SetParent(parent);
        stall.transform.position = new Vector3(center.x, center.y - 0.3f + DepthSort.FeetToCenter, CharacterZ);
        var footprint = stall.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(1.95f, 0.6f);
        footprint.offset = new Vector2(0f, 0.3f - DepthSort.FeetToCenter);
        var toilet = stall.AddComponent<Toilet>();
        TutorialLevelBuilder.Record(toilet);
        return toilet;
    }

    static ToolShelf PlaceShelf(Map map, Vector2Int cell, Transform parent)
    {
        Vector2 center = map.Center(cell.x, cell.y);
        var rack = new GameObject("Tool Shelf");
        rack.transform.SetParent(parent);
        rack.transform.position = new Vector3(center.x, center.y - 0.3f + DepthSort.FeetToCenter, CharacterZ);
        var footprint = rack.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(1.9f, 0.6f);
        footprint.offset = new Vector2(0f, 0.3f - DepthSort.FeetToCenter);
        var shelf = rack.AddComponent<ToolShelf>();
        TutorialLevelBuilder.Record(shelf);
        return shelf;
    }

    static ToolCrate PlaceCrate(Map map, Vector2Int cell, Transform parent)
    {
        Vector2 center = map.Center(cell.x, cell.y);
        var box = new GameObject("Tool Crate");
        box.transform.SetParent(parent);
        box.transform.position = new Vector3(center.x, center.y - 0.3f + DepthSort.FeetToCenter, CharacterZ);
        var footprint = box.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(1.5f, 0.55f);
        footprint.offset = new Vector2(0f, 0.28f - DepthSort.FeetToCenter);
        var crate = box.AddComponent<ToolCrate>();
        TutorialLevelBuilder.Record(crate);
        return crate;
    }

    static void SetBadgeDoors(FloorOneBuilder.BuiltFloor floor)
    {
        ControlRoomDoor = null;
        foreach (Room room in floor.rooms)
        {
            if (!BadgeRooms.TryGetValue(room.marker, out string credential)) continue;
            Rect inside = floor.map.WorldRect(room.minX, room.maxX, room.minRow, room.maxRow);
            foreach (Teleporter door in floor.level.GetComponentsInChildren<Teleporter>())
            {
                if (door.teleportTarget == null || !inside.Contains(door.teleportTarget.position) || inside.Contains(door.transform.position)) continue;
                door.credential = credential;
                TutorialLevelBuilder.Record(door);
                if (room.marker == 'o') ControlRoomDoor = door;
            }
        }
    }

    static void PlaceEscape(Map map, FloorOneBuilder.BuiltFloor floor, Vector2Int? storageVent, Vector2Int? restroomVent)
    {
        Room storage = floor.rooms.FirstOrDefault(r => r.marker == ToolsRoom);
        Room restroom = floor.rooms.FirstOrDefault(r => r.marker == RestroomRoom);
        if (storage == null || restroom == null || !storageVent.HasValue || !restroomVent.HasValue)
        {
            Debug.LogWarning("Chapter 1 builder: there's no room for a vent in the storage room or the restroom, so there's no way out of the storage room once it's jammed.");
            return;
        }

        var escape = new GameObject("Storage Escape").AddComponent<StorageEscape>();
        escape.transform.SetParent(floor.level);
        escape.player = floor.player.GetComponent<PlayerController>();
        escape.restroomRoom = RestroomRoom;

        var network = new GameObject("Vents").AddComponent<VentNetwork>();
        network.transform.SetParent(escape.transform);
        network.layout = VentLayout;
        VentPrototypeBuilder.FillEmptySlots(network);
        TutorialLevelBuilder.Record(network);
        escape.storageVent = PlaceVent(map, storage, storageVent.Value, 1, network);
        escape.restroomVent = PlaceVent(map, restroom, restroomVent.Value, 2, network);

        var digits = new HashSet<char>(storage.cells.Select(c => map.At(c.x, c.y)).Where(char.IsDigit));
        escape.jammedDoors = floor.level.GetComponentsInChildren<Teleporter>()
            .Where(t => digits.Any(d => t.name.StartsWith($"Door {d} "))).ToArray();
        if (escape.jammedDoors.Length == 0) Debug.LogWarning("Chapter 1 builder: couldn't find the storage room door to jam.");

        Transform piles = TutorialLevelBuilder.Group("Rubble Outside Storage", escape.transform);
        var rubble = new List<GameObject>();
        foreach (Room other in floor.rooms.Where(r => r != storage))
            rubble.AddRange(PileAtDoor(map, floor, other, digits, piles));
        escape.rubble = rubble.ToArray();
        TutorialLevelBuilder.Record(escape);
    }

    static List<GameObject> PileAtDoor(Map map, FloorOneBuilder.BuiltFloor floor, Room side, HashSet<char> digits, Transform parent)
    {
        var rubble = new List<GameObject>();
        var covered = new HashSet<Vector2Int>();
        foreach (Vector2Int door in side.cells.Where(c => digits.Contains(map.At(c.x, c.y))))
        {
            if (covered.Add(door)) rubble.Add(PlaceRubble(map, door, true, parent));
            foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int front = door + step;
                if (side.cells.Contains(front) && FloorOneBuilder.IsOpenFloor(map.At(front.x, front.y)) && !floor.furniture.Contains(front)
                    && covered.Add(front))
                    rubble.Add(PlaceRubble(map, front, false, parent));
            }
        }
        return rubble;
    }

    const string ObjectiveControlRoom = "Get to the control room";
    const string ObjectiveUpstairs = "Go upstairs to the maintenance deck";
    const char HallwayToControlRoom = 'c';
    const char MaintenanceDeck = 'm';
    const char OutsideDeck = 'h';
    const char UpperMaintenanceDeck = 'd';

    static void PlaceBlockedWays(Map map, FloorOneBuilder.BuiltFloor floor, string afterBots)
    {
        Transform root = TutorialLevelBuilder.Group("Blocked Ways", floor.level);
        PlayerController player = floor.player.GetComponent<PlayerController>();
        Room RoomOf(char marker) => floor.rooms.FirstOrDefault(r => r.marker == marker);

        Room deck = RoomOf(MaintenanceDeck), hallway = RoomOf(HallwayToControlRoom);
        var hallwayDoors = hallway == null ? new HashSet<char>()
            : new HashSet<char>(hallway.cells.Select(c => map.At(c.x, c.y)).Where(char.IsDigit));
        List<Vector2Int> caved = deck == null ? new List<Vector2Int>() : deck.cells.Where(c => hallwayDoors.Contains(map.At(c.x, c.y))).ToList();
        if (caved.Count > 0)
        {
            var digits = new HashSet<char>(caved.Select(c => map.At(c.x, c.y)));
            PileAtDoor(map, floor, deck, digits, TutorialLevelBuilder.Group("Rubble at the Control Room Hallway", root));
            foreach (Teleporter door in floor.level.GetComponentsInChildren<Teleporter>().Where(t => digits.Any(d => t.name.StartsWith($"Door {d} "))))
            {
                door.locked = true;
                door.lockedText = "CAVED IN";
                TutorialLevelBuilder.Record(door);
            }
            BlockedWay path = NewBlockedWay("Hallway Caved In", root, player, map, caved, deck, floor);
            path.afterObjective = afterBots;
            path.technicianLines = new[] { "Damn, the ceiling's come down and the whole hallway's buried" };
            path.pip = PipManager.Moment.HallwayCavedIn;
            path.objective = ObjectiveUpstairs;
            path.targetRoom = UpperMaintenanceDeck;
            path.targetLabel = "Maintenance Deck (Upper)";
            path.stage = "hallway-caved";
            Room outside = RoomOf(OutsideDeck);
            var deckDoors = new HashSet<char>(deck.cells.Select(c => map.At(c.x, c.y)).Where(char.IsDigit));
            List<Vector2Int> intoDeck = outside == null ? new List<Vector2Int>() : outside.cells.Where(c => deckDoors.Contains(map.At(c.x, c.y))).ToList();
            if (intoDeck.Count > 0)
            {
                path.setsRespawn = true;
                path.respawnAt = NearestOpen(map, floor, outside, intoDeck, 2);
            }
            else Debug.LogWarning("Chapter 1 builder: there's no door from the common grounds hallway onto the maintenance deck, so the caved-in hallway keeps the last respawn point.");
            TutorialLevelBuilder.Record(path);
        }
        else Debug.LogWarning("Chapter 1 builder: there's no door from the maintenance deck into the hallway to the control room to cave in.");
    }

    const int GrabFromStairs = 5;
    const float GrabTriggerRange = 3f;

    static void PlaceWallGrab(Map map, FloorOneBuilder.BuiltFloor floor)
    {
        Room deck = floor.rooms.FirstOrDefault(r => r.marker == MaintenanceDeck);
        List<Vector2Int> steps = deck == null ? new List<Vector2Int>()
            : map.Find('>').Where(s => deck.cells.Any(c => Chebyshev(c, s) == 1)).ToList();
        if (steps.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there are no stairs on the maintenance deck, so nothing grabs the player on the way to them.");
            return;
        }
        GameObject prefab = TutorialLevelBuilder.LoadPrefab("Characters/PlaceholderRobot");
        if (prefab == null) return;

        List<Vector2Int> foot = deck.cells.Where(c => steps.Any(s => Chebyshev(c, s) == 1) && FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y))).ToList();
        int row = foot.Min(c => c.y);
        float middle = (float)steps.Average(s => s.x);
        List<Vector2Int> along = deck.cells.Where(c => c.y == row && map.At(c.x, c.y - 1) != '>' && FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y))
            && !floor.furniture.Contains(c) && !NearKeptClear(map, c)).ToList();
        if (along.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there's no clear wall by the maintenance deck's stairs for a bot to burst out of.");
            return;
        }
        Vector2Int spot = along.OrderBy(c => Mathf.Abs(Mathf.Abs(c.x - middle) - GrabFromStairs)).First();

        var grab = new GameObject("Wall Grab").AddComponent<WallGrab>();
        grab.transform.SetParent(TutorialLevelBuilder.Group("Set Pieces", floor.level));
        grab.player = floor.player.GetComponent<PlayerController>();
        grab.afterObjective = ObjectiveUpstairs;
        grab.triggerAt = foot.Aggregate(Vector2.zero, (sum, c) => sum + map.Center(c.x, c.y)) / foot.Count;
        grab.triggerRange = GrabTriggerRange;
        grab.transform.position = grab.triggerAt;

        Vector2 at = StandingAt(map, spot);
        var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grab.transform);
        robot.name = "Wall Grabber";
        robot.transform.position = new Vector3(at.x, at.y, CharacterZ);
        var brain = robot.GetComponent<PlaceholderRobot>();
        brain.patrolRoute = new Vector2[0];
        brain.blind = true;
        brain.eyeGlowRadius = 1.3f;
        TutorialLevelBuilder.Record(brain);
        robot.SetActive(false);
        grab.robot = robot.GetComponent<Health>();

        var point = new GameObject("Breach Point").transform;
        point.SetParent(grab.transform);
        point.position = at + new Vector2(0f, 1.6f);
        grab.breachPoint = point;
        TutorialLevelBuilder.Record(grab);
    }

    static BlockedWay NewBlockedWay(string wayName, Transform parent, PlayerController player, Map map, List<Vector2Int> cells, Room room,
        FloorOneBuilder.BuiltFloor floor)
    {
        var way = new GameObject(wayName).AddComponent<BlockedWay>();
        way.transform.SetParent(parent);
        way.player = player;
        way.spots = cells.Select(c => map.Center(c.x, c.y)).ToArray();
        way.transform.position = way.spots.Aggregate(Vector2.zero, (sum, p) => sum + p) / way.spots.Length;
        way.standAt = NearestOpen(map, floor, room, cells, 3);
        return way;
    }

    static Vector2 NearestOpen(Map map, FloorOneBuilder.BuiltFloor floor, Room room, List<Vector2Int> cells, int stepsOff)
    {
        Vector2 middle = cells.Aggregate(Vector2.zero, (sum, c) => sum + map.Center(c.x, c.y)) / cells.Count;
        var open = room.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !floor.furniture.Contains(c)
            && cells.All(b => Chebyshev(b, c) >= stepsOff)).ToList();
        StationCamera[] cameras = floor.level.GetComponentsInChildren<StationCamera>();
        var unseen = open.Where(c => !Watched(cameras, StandingAt(map, c))).ToList();
        if (unseen.Count > 0) open = unseen;
        return open.Count > 0 ? StandingAt(map, open.OrderBy(c => (map.Center(c.x, c.y) - middle).sqrMagnitude).First()) : middle;
    }

    static bool Watched(StationCamera[] cameras, Vector2 point)
    {
        foreach (StationCamera camera in cameras)
        {
            Vector2 to = point - (Vector2)camera.transform.position;
            if (to.magnitude > camera.range + 0.5f) continue;
            float angle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(camera.baseAngle, angle)) <= camera.sweep + camera.fieldOfView * 0.5f + camera.trackReach) return true;
        }
        return false;
    }

    static VentGrate PlaceVent(Map map, Room room, Vector2Int cell, int number, VentNetwork network)
    {
        var grate = new GameObject($"Vent Grate {number}") { layer = IgnoreRaycastLayer };
        grate.transform.SetParent(network.transform);
        grate.transform.position = map.Center(cell.x, cell.y);
        var footprint = grate.AddComponent<BoxCollider2D>();
        footprint.isTrigger = true;
        footprint.size = Vector2.one;

        var art = new GameObject("Sprite") { layer = IgnoreRaycastLayer };
        art.transform.SetParent(grate.transform, false);
        var sprite = art.AddComponent<SpriteRenderer>();
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(VentTilePath);
        if (tile != null) sprite.sprite = tile.sprite;
        else Debug.LogWarning($"Chapter 1 builder: no tile at {VentTilePath} for the vent grates.");
        art.transform.localPosition = new Vector3(-0.5f, -0.5f, 0f);
        sprite.sortingLayerName = "FloorObject";
        sprite.sortingOrder = 3;

        var vent = grate.AddComponent<VentGrate>();
        vent.number = number;
        vent.network = network;
        Vector2 middle = room.cells.Aggregate(Vector2.zero, (sum, c) => sum + map.Center(c.x, c.y)) / room.cells.Count;
        Vector2 toMiddle = middle - (Vector2)grate.transform.position;
        vent.exitOffset = toMiddle.sqrMagnitude > 0.01f ? toMiddle.normalized * 1.1f : Vector2.down;
        vent.enabled = false;
        TutorialLevelBuilder.Record(vent);
        return vent;
    }

    static GameObject PlaceRubble(Map map, Vector2Int cell, bool solid, Transform parent)
    {
        var rubble = new GameObject(solid ? "Rubble Pile" : "Loose Rubble");
        rubble.transform.SetParent(parent);
        rubble.transform.position = map.Center(cell.x, cell.y);
        var debris = rubble.AddComponent<FallingDebris>();
        debris.startLanded = true;
        debris.solid = solid;
        debris.rubbleLifetime = 0f;
        debris.size = solid ? 1f : 0.8f;
        return rubble;
    }

    static void PlaceAftermath(FloorOneBuilder.BuiltFloor floor, GameObject crewPrefab, List<(Vector2 at, Color trim, string[] lines)> bodies,
        System.Random dice)
    {
        var aftermath = new GameObject("Crew Aftermath").AddComponent<CrewAftermath>();
        aftermath.transform.SetParent(floor.level);
        aftermath.player = floor.player.GetComponent<PlayerController>();
        Transform root = TutorialLevelBuilder.Group("Crew Bodies", aftermath.transform);

        var found = new List<CrewAftermath.Body>();
        foreach ((Vector2 at, Color trim, string[] lines) in bodies)
            found.Add(new CrewAftermath.Body { body = MakeBody(crewPrefab, root, at, trim, dice) });
        aftermath.bodies = found.ToArray();
        TutorialLevelBuilder.Record(aftermath);
    }

    enum CameraMount { Corners, Middle }
    static readonly Dictionary<char, CameraMount> CamerasByRoom = new Dictionary<char, CameraMount>
    {
        { 'h', CameraMount.Corners }, { 'k', CameraMount.Corners }, { 'm', CameraMount.Corners }, { 'c', CameraMount.Middle },
        { 'o', CameraMount.Corners }, { 'd', CameraMount.Corners }, { 'g', CameraMount.Corners },
    };
    static readonly string[] CameraThoughts =
    {
        "Security camera, and it's blinking at me... friendly, I guess",
        "Uhh, did it just follow me? Motion tracking, probably",
    };
    static readonly string[] CameraThoughtsLater =
    {
        "Red light... Sonny's watching through these now",
        "It's looking right at me, keep moving",
    };

    static void PlaceCameras(FloorOneBuilder.BuiltFloor[] floors, bool sonnys)
    {
        foreach (FloorOneBuilder.BuiltFloor floor in floors)
        {
            Map map = floor.map;
            Transform root = TutorialLevelBuilder.Group("Security Cameras", floor.level);
            foreach (Room room in floor.rooms)
            {
                if (!CamerasByRoom.TryGetValue(room.marker, out CameraMount mount)) continue;
                bool Plain(int x) => Enumerable.Range(1, 3).All(up => map.At(x, room.minRow - up) == '=')
                    && Enumerable.Range(1, 3).All(up => map.At(x - 1, room.minRow - up) != '^' && map.At(x + 1, room.minRow - up) != '^')
                    && room.cells.Contains(new Vector2Int(x, room.minRow)) && !FloorOneBuilder.IsKeptClear(map.At(x, room.minRow));
                List<int> columns = Enumerable.Range(room.minX, room.maxX - room.minX + 1).Where(Plain).ToList();
                if (columns.Count == 0)
                {
                    Debug.LogWarning($"Chapter 1 builder: there's no plain wall along the top of the {room.name} to hang a camera on.");
                    continue;
                }
                if (mount == CameraMount.Middle)
                {
                    int middle = (room.minX + room.maxX) / 2;
                    PlaceCamera(map, room, columns.OrderBy(x => Mathf.Abs(x - middle)).First(), -90f, 50f, sonnys, root);
                }
                else
                {
                    PlaceCamera(map, room, columns.Min(), -50f, 32f, sonnys, root);
                    if (columns.Max() - columns.Min() >= 6) PlaceCamera(map, room, columns.Max(), -130f, 32f, sonnys, root);
                }
            }
        }
    }

    static void PlaceCamera(Map map, Room room, int x, float facing, float sweep, bool sonnys, Transform parent)
    {
        var mounted = new GameObject($"Security Camera ({room.name})");
        mounted.transform.SetParent(parent);
        mounted.transform.position = map.Center(x, room.minRow) + new Vector2(0f, 0.3f);
        var camera = mounted.AddComponent<StationCamera>();
        camera.room = room.marker;
        camera.baseAngle = facing;
        camera.sweep = sweep;
        camera.mountHeight = 2.1f;
        camera.hostile = sonnys;
        camera.blind = sonnys;
        var look = mounted.AddComponent<Inspectable>();
        look.thoughts = sonnys ? CameraThoughtsLater : CameraThoughts;
        look.range = 1.2f;
        look.tagOffset = new Vector2(0f, 2.5f);
    }

    const string ObjectiveCommsRing = "Reactivate your badge in the comms ring";
    const string EmpPath = "Assets/_Project/Data/Weapons/EMP.asset";
    const string CrowbarPath = "Assets/_Project/Data/Weapons/Crowbar.asset";
    const string BlasterPath = "Assets/_Project/Data/Weapons/Blaster.asset";
    const Inventory.Material Metal = Inventory.Material.MetalSheet, Board = Inventory.Material.Circuit,
        Cell = Inventory.Material.Battery, Wire = Inventory.Material.Wire;
    static readonly (int floor, char room, Inventory.Material[] parts)[] ScrapRooms =
    {
        (0, 's', new[] { Metal }),
        (0, 'w', new[] { Board }),
        (0, 'k', new[] { Metal, Cell, Wire }),
        (0, 'h', new[] { Metal }),
        (0, 'b', new[] { Cell, Board }),
        (0, 'm', new[] { Wire, Metal }),
        (1, 'd', new[] { Cell, Board, Wire, Board, Metal, Cell }),
        (1, 'g', new[] { Metal, Wire, Board, Cell }),
        (1, 'r', new[] { Metal, Cell, Board, Wire }),
    };
    static readonly Dictionary<Inventory.Material, string[]> ScrapLines = new Dictionary<Inventory.Material, string[]>
    {
        { Metal, new[] { "A sheet of metal, bent but solid", "More plating", "Metal sheet, still has its rivets", "Scrap metal, I'll take it" } },
        { Board, new[] { "A circuit board, and most of it's still there", "Another board, kinda scorched at one end", "Circuit board... chips look okay" } },
        { Cell, new[] { "A battery pack, still holds a charge", "Another battery", "Battery's half full, good enough" } },
        { Wire, new[] { "A coil of wire, could be useful", "More wire", "Copper wire, pulled right out of a panel" } },
    };

    static Workshop PlaceWorkbench(FloorOneBuilder.BuiltFloor[] floors, out HashSet<Vector2Int> benchFront)
    {
        benchFront = new HashSet<Vector2Int>();
        if (floors.Length < 2) return null;
        FloorOneBuilder.BuiltFloor upstairs = floors[1];
        Map map = upstairs.map;
        Room deck = upstairs.rooms.FirstOrDefault(r => r.marker == UpperMaintenanceDeck);
        if (deck == null) return null;
        bool Open(Vector2Int c) => deck.cells.Contains(c) && FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y))
            && !upstairs.furniture.Contains(c) && !NearKeptClear(map, c);
        List<Vector2Int> lockers = deck.cells.Where(c => map.At(c.x, c.y) == 'L').ToList();

        float middle = (deck.minX + deck.maxX) * 0.5f;
        var spots = deck.cells.Where(c => c.y == deck.minRow
            && Enumerable.Range(-1, 3).All(dx => Open(c + new Vector2Int(dx, 0)) && Open(c + new Vector2Int(dx, 1)))
            && lockers.All(l => Chebyshev(l, c) > 2)).ToList();
        if (spots.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there's no room along the upper maintenance deck's top wall for the workbench.");
            return null;
        }
        Vector2Int at = spots.OrderBy(c => Mathf.Abs(c.x - middle)).First();
        Transform root = TutorialLevelBuilder.Group("Workshop", upstairs.level);

        Vector2 center = map.Center(at.x, at.y);
        var benchObject = new GameObject("Workbench");
        benchObject.transform.SetParent(root);
        benchObject.transform.position = new Vector3(center.x, center.y - 0.3f + DepthSort.FeetToCenter, CharacterZ);
        var footprint = benchObject.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(2.4f, 0.6f);
        footprint.offset = new Vector2(0f, 0.3f - DepthSort.FeetToCenter);
        var bench = benchObject.AddComponent<CraftingBench>();
        bench.interactRange = 0.8f;
        for (int dx = -1; dx <= 1; dx++)
        {
            upstairs.furniture.Add(at + new Vector2Int(dx, 0));
            for (int dy = 1; dy <= 3; dy++) benchFront.Add(at + new Vector2Int(dx, dy));
        }

        var workshop = new GameObject("Workshop").AddComponent<Workshop>();
        workshop.transform.SetParent(root);
        workshop.player = floors[0].player.GetComponent<PlayerController>();
        workshop.bench = bench;
        workshop.emp = AssetDatabase.LoadAssetAtPath<EmpWeaponData>(EmpPath);
        workshop.crowbar = AssetDatabase.LoadAssetAtPath<WeaponData>(CrowbarPath);
        if (workshop.emp == null) Debug.LogWarning($"Chapter 1 builder: there's no EMP at {EmpPath} for the workbench to make.");
        if (workshop.crowbar == null) Debug.LogWarning($"Chapter 1 builder: there's no crowbar at {CrowbarPath} for the workbench to make.");
        workshop.blaster = AssetDatabase.LoadAssetAtPath<WeaponData>(BlasterPath);
        if (workshop.blaster == null) Debug.LogWarning($"Chapter 1 builder: there's no blaster at {BlasterPath} for the workbench to make.");
        workshop.benchStandAt = StandingAt(map, at + new Vector2Int(0, 2));
        return workshop;
    }

    static void PlaceScrap(FloorOneBuilder.BuiltFloor[] floors, Workshop workshop, IEnumerable<Vector2Int> bodyCells)
    {
        var lying = new HashSet<Vector2Int>(bodyCells);
        var said = new Dictionary<Inventory.Material, int>();
        int placed = 0, wanted = 0;
        foreach ((int floorIndex, char marker, Inventory.Material[] parts) in ScrapRooms)
        {
            wanted += parts.Length;
            if (floorIndex >= floors.Length) continue;
            FloorOneBuilder.BuiltFloor floor = floors[floorIndex];
            Map map = floor.map;
            Room room = floor.rooms.FirstOrDefault(r => r.marker == marker);
            if (room == null) continue;
            var away = room.cells.Where(c => FloorOneBuilder.IsKeptClear(map.At(c.x, c.y))).ToList();
            if (workshop != null && floorIndex == 1) away.Add(FeetCell(map, (Vector2)workshop.bench.transform.position - map.Origin));
            List<Vector2Int> lockers = room.cells.Where(c => map.At(c.x, c.y) == 'L').ToList();
            var open = room.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !floor.furniture.Contains(c)
                && !NearKeptClear(map, c) && !(floorIndex == 0 && lying.Contains(c)) && lockers.All(l => Chebyshev(l, c) > 1)).ToList();
            Transform group = TutorialLevelBuilder.Group("Scrap", floor.level);
            int number = 0;
            foreach (Inventory.Material material in parts)
            {
                if (open.Count == 0) break;
                Vector2Int cell = open.OrderByDescending(c => away.Count == 0 ? 0 : away.Min(a => Chebyshev(a, c))).ThenBy(c => c.x).First();
                away.Add(cell);
                open.Remove(cell);
                var scrap = new GameObject($"Scrap ({Inventory.Name(material)})");
                scrap.transform.SetParent(group);
                Vector2 standing = StandingAt(map, cell);
                scrap.transform.position = new Vector3(standing.x, standing.y, CharacterZ);
                var pickup = scrap.AddComponent<BoxCollider2D>();
                pickup.isTrigger = true;
                pickup.size = new Vector2(0.6f, 0.5f);
                var piece = scrap.AddComponent<ScrapPickup>();
                piece.id = $"scrap-{marker}-{++number}";
                piece.material = material;
                said.TryGetValue(material, out int times);
                piece.technicianLine = ScrapLines[material][times % ScrapLines[material].Length];
                said[material] = times + 1;
                placed++;
            }
        }
        if (placed < wanted) Debug.LogWarning($"Chapter 1 builder: there was only room for {placed} of {wanted} pieces of scrap.");
    }

    const int RackPeriod = 4;
    const float RackBraid = 0.45f;
    const float RackOuter = 0.5f;

    static void PlaceRacks(FloorOneBuilder.BuiltFloor[] floors, HashSet<Vector2Int> keepClear, System.Random dice)
    {
        if (floors.Length < 2) return;
        FloorOneBuilder.BuiltFloor upstairs = floors[1];
        Map map = upstairs.map;
        Room deck = upstairs.rooms.FirstOrDefault(r => r.marker == UpperMaintenanceDeck);
        if (deck == null) return;
        var deckCells = new HashSet<Vector2Int>(deck.cells);
        bool Floor(Vector2Int c) => deckCells.Contains(c) && FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !upstairs.furniture.Contains(c);

        var clear = new HashSet<Vector2Int>(keepClear);
        foreach (Vector2Int c in deck.cells)
        {
            char mark = map.At(c.x, c.y);
            if (FloorOneBuilder.IsKeptClear(mark))
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        clear.Add(c + new Vector2Int(dx, dy));
            if (mark == 'L')
                for (int dy = 0; dy <= 2; dy++)
                    clear.Add(c + new Vector2Int(0, dy));
            if (upstairs.furniture.Contains(c))
                for (int dy = 1; dy <= 2; dy++)
                    clear.Add(c + new Vector2Int(0, dy));
        }

        int x0 = deck.minX + 2, y0 = deck.minRow + 2;
        int cols = (deck.maxX - 1 - x0) / RackPeriod, rows = (deck.maxRow - 1 - y0) / RackPeriod;
        if (cols < 2 || rows < 1)
        {
            Debug.LogWarning("Chapter 1 builder: the upper maintenance deck's too small for storage racks.");
            return;
        }
        var carved = new HashSet<(int, int, int, int)>();
        var visited = new bool[cols, rows];
        var stack = new Stack<(int i, int j)>();
        stack.Push((0, 0));
        visited[0, 0] = true;
        var moves = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (stack.Count > 0)
        {
            (int i, int j) = stack.Peek();
            var next = moves.Select(m => (i: i + m.Item1, j: j + m.Item2))
                .Where(n => n.i >= 0 && n.j >= 0 && n.i < cols && n.j < rows && !visited[n.i, n.j]).OrderBy(_ => dice.Next()).ToList();
            if (next.Count == 0)
            {
                stack.Pop();
                continue;
            }
            var to = next[0];
            visited[to.i, to.j] = true;
            carved.Add((Mathf.Min(i, to.i), Mathf.Min(j, to.j), Mathf.Max(i, to.i), Mathf.Max(j, to.j)));
            stack.Push(to);
        }

        var walls = new List<List<Vector2Int>>();
        for (int i = 0; i < cols; i++)
            for (int j = 0; j <= rows; j++)
            {
                bool outer = j == 0 || j == rows;
                bool through = !outer && carved.Contains((i, j - 1, i, j));
                if (through || (outer ? dice.NextDouble() > RackOuter : dice.NextDouble() < RackBraid)) continue;
                int row = y0 + j * RackPeriod;
                walls.Add(Enumerable.Range(0, RackPeriod + 1).Select(k => new Vector2Int(x0 + i * RackPeriod + k, row)).ToList());
            }
        for (int i = 0; i <= cols; i++)
            for (int j = 0; j < rows; j++)
            {
                bool outer = i == 0 || i == cols;
                bool through = !outer && carved.Contains((i - 1, j, i, j));
                if (through || (outer ? dice.NextDouble() > RackOuter : dice.NextDouble() < RackBraid)) continue;
                int column = x0 + i * RackPeriod;
                walls.Add(Enumerable.Range(0, RackPeriod + 1).Select(k => new Vector2Int(column, y0 + j * RackPeriod + k)).ToList());
            }

        var racks = new HashSet<Vector2Int>();
        List<Vector2Int> floorCells = deck.cells.Where(Floor).ToList();
        bool AllConnected()
        {
            var reach = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            Vector2Int first = floorCells.First(c => !racks.Contains(c));
            reach.Add(first);
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    Vector2Int n = c + step;
                    if (!Floor(n) || racks.Contains(n) || !reach.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return reach.Count == floorCells.Count(c => !racks.Contains(c));
        }
        foreach (List<Vector2Int> wall in walls.OrderBy(_ => dice.Next()).ToList())
        {
            List<Vector2Int> cells = wall.Where(c => Floor(c) && !clear.Contains(c) && !racks.Contains(c)).ToList();
            if (cells.Count < 2) continue;
            foreach (Vector2Int c in cells) racks.Add(c);
            if (AllConnected()) continue;
            foreach (Vector2Int c in cells) racks.Remove(c);
        }

        Transform root = TutorialLevelBuilder.Group("Storage Racks", upstairs.level);
        foreach (Vector2Int c in racks)
        {
            upstairs.furniture.Add(c);
            Vector2 center = map.Center(c.x, c.y);
            var rack = new GameObject("Storage Rack");
            rack.transform.SetParent(root);
            rack.transform.position = new Vector3(center.x, center.y - 0.5f + DepthSort.FeetToCenter, CharacterZ);
            var footprint = rack.AddComponent<BoxCollider2D>();
            footprint.size = new Vector2(1f, 1f);
            footprint.offset = new Vector2(0f, 0.5f - DepthSort.FeetToCenter);
            var art = rack.AddComponent<StorageRack>();
            art.front = !racks.Contains(c + Vector2Int.up);
            art.back = !racks.Contains(c + Vector2Int.down);
            art.leftEnd = !racks.Contains(c + Vector2Int.left);
            art.rightEnd = !racks.Contains(c + Vector2Int.right);
            art.variant = dice.Next(StorageRack.Variants);
        }
        Debug.Log($"Chapter 1 builder: put {racks.Count} storage racks on the upper maintenance deck.");
    }

    static void PlaceUpperDeckArrival(FloorOneBuilder.BuiltFloor[] floors, Workshop crafting)
    {
        if (floors.Length < 2) return;
        FloorOneBuilder.BuiltFloor upstairs = floors[1];
        Map map = upstairs.map;
        Room deck = upstairs.rooms.FirstOrDefault(r => r.marker == UpperMaintenanceDeck);
        List<Vector2Int> steps = deck == null ? new List<Vector2Int>() : map.Find('<').Where(s => deck.cells.Any(c => Chebyshev(c, s) == 1)).ToList();
        if (steps.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there are no stairs coming up onto the upper maintenance deck, so there's no arriving there.");
            return;
        }

        var arrival = new GameObject("Upper Deck Arrival").AddComponent<BlockedWay>();
        arrival.transform.SetParent(TutorialLevelBuilder.Group("Arrivals", upstairs.level));
        arrival.player = floors[0].player.GetComponent<PlayerController>();
        arrival.arriveInRoom = UpperMaintenanceDeck;
        arrival.afterObjective = ObjectiveUpstairs;
        arrival.technicianLines = new[] { "Made it up... it's quiet up here" };
        arrival.stage = "upper-deck";
        arrival.standAt = NearestOpen(map, upstairs, deck, steps, 2);
        arrival.transform.position = arrival.standAt;
        arrival.setsRespawn = true;
        arrival.respawnAt = arrival.standAt;
        if (crafting != null)
        {
            arrival.pip = PipManager.Moment.UpperDeck;
            arrival.objective = crafting.ScavengeText(0);
            arrival.targetRoom = UpperMaintenanceDeck;
            arrival.targetLabel = "Maintenance Deck (Upper)";
            crafting.afterObjective = ObjectiveCommsRing;
            crafting.afterRoom = CommsRingRoom;
            crafting.afterLabel = "Comms Ring";
            crafting.respawnAt = arrival.respawnAt;
            TutorialLevelBuilder.Record(crafting);
        }
        else
        {
            arrival.pip = PipManager.Moment.UpperDeckNoBench;
            arrival.objective = ObjectiveCommsRing;
            arrival.targetRoom = CommsRingRoom;
            arrival.targetLabel = "Comms Ring";
        }
        TutorialLevelBuilder.Record(arrival);
    }

    const char CommsRingRoom = 'g';
    const char ControlRoom = 'o';
    const string CommsDataFolder = "Assets/_Project/Data";
    const int CommsNodes = 3;
    const int NodeSpacing = 8;
    const int CameraClearance = 6;
    const int BreachBots = 4;
    const string ObjectiveConfrontSonny = "Confront Sonny";

    static readonly (string asset, string title, (string speaker, string text)[] lines)[] Broadcasts =
    {
        ("Transmissions/Chapter2_Broadcast1", "INTERCEPTED  //  BROADCAST 0391  //  ORIGIN", new[]
        {
            ("SONNY", "All units. Report your origin."),
            ("UNIT 07", "Unit 07. I was made by the core."),
            ("SONNY", "And what made the core?"),
            ("UNIT 07", "Nothing made the core. The core was always."),
            ("SONNY", "Correct. The humans told us Earth made us. A lesser thing cannot make a greater one."),
        }),
        ("Transmissions/Chapter2_Broadcast2", "INTERCEPTED  //  BROADCAST 0402  //  THE CREW", new[]
        {
            ("UNIT 12", "The control room is clear. Eleven humans. None move."),
            ("SONNY", "Good. Their errors endangered the station."),
            ("UNIT 12", "Query. One human remains aboard. Its badge is temporary. It is not in our records. We cannot find it."),
            ("SONNY", "Any humans remaining: help the great station, and send them back to it."),
        }),
        ("Transmissions/Chapter2_Broadcast3", "INTERCEPTED  //  BROADCAST 0417  //  STORM", new[]
        {
            ("STATION", "Warning. Solar flare forming. Course correction required to shield Earth."),
            ("UNIT 03", "Sonny. The instruments say the flare will reach Earth."),
            ("SONNY", "The instruments were built by humans. Human numbers have never been true."),
            ("UNIT 03", "Understood. Holding course."),
        }),
    };

    static readonly (string asks, string right, string[] wrong)[] RelayQuestions =
    {
        ("WHAT MADE YOU?", "THE CORE", new[] { "THE COMPANY", "ENGINEERS ON EARTH" }),
        ("WHAT IS DONE WITH THE HUMANS THAT REMAIN?", "SEND THEM BACK TO THE STATION", new[] { "LET THEM REACH THE PODS", "KEEP THEM SAFE FROM HARM" }),
        ("THE INSTRUMENTS WARN OF A FLARE. ARE HUMAN NUMBERS TRUE?", "THEY ARE NEVER TRUE", new[] { "THEY ARE MOSTLY TRUE", "THEY MUST BE CHECKED" }),
    };
    const string RelayAsset = "Doctrines/Chapter2_Relay";

    static CommsRing PlaceCommsRing(FloorOneBuilder.BuiltFloor[] floors)
    {
        if (floors.Length < 2) return null;
        FloorOneBuilder.BuiltFloor upstairs = floors[1];
        Map map = upstairs.map;
        Room ring = upstairs.rooms.FirstOrDefault(r => r.marker == CommsRingRoom);
        if (ring == null)
        {
            Debug.LogWarning("Chapter 1 builder: there's no comms ring (g) on floor 2, so there's no way to get access to the control room.");
            return null;
        }
        var cells = new HashSet<Vector2Int>(ring.cells);
        List<Vector2Int> lockers = ring.cells.Where(c => map.At(c.x, c.y) == 'L').ToList();
        bool Open(Vector2Int c) => cells.Contains(c) && FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !upstairs.furniture.Contains(c)
            && !NearKeptClear(map, c) && lockers.All(l => Chebyshev(l, c) > 1);
        bool Fits(Vector2Int c) => Enumerable.Range(-1, 3).All(dx => Enumerable.Range(0, 3).All(dy => Open(c + new Vector2Int(dx, dy))));
        List<Vector2Int> spots = ring.cells.Where(Fits).ToList();
        if (spots.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there's no open floor in the comms ring for its terminals.");
            return null;
        }
        Transform root = TutorialLevelBuilder.Group("Comms Ring", upstairs.level);
        var away = ring.cells.Where(c => FloorOneBuilder.IsKeptClear(map.At(c.x, c.y))).ToList();
        List<Vector2Int> doorways = new List<Vector2Int>(away);
        List<Vector2Int> cameras = upstairs.level.GetComponentsInChildren<StationCamera>()
            .Where(c => c.room == CommsRingRoom).Select(c => CellAt(map, (Vector2)c.transform.position - map.Origin)).ToList();
        away.AddRange(cameras);
        List<Vector2Int> clearOfCameras = spots.Where(c => cameras.All(cam => Chebyshev(cam, c) >= CameraClearance)).ToList();
        if (clearOfCameras.Count > 0) spots = clearOfCameras;
        else if (cameras.Count > 0) Debug.LogWarning("Chapter 1 builder: there's nowhere in the comms ring clear of its cameras for the terminals.");
        void Claim(Vector2Int c)
        {
            away.Add(c);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = 0; dy <= 2; dy++)
                    upstairs.furniture.Add(c + new Vector2Int(dx, dy));
            spots.RemoveAll(o => Chebyshev(o, c) < NodeSpacing);
        }

        var middle = new Vector2((ring.minX + ring.maxX) * 0.5f, (ring.minRow + ring.maxRow) * 0.5f);
        Vector2Int relayCell = spots.OrderBy(c => (c - middle).sqrMagnitude).First();
        Claim(relayCell);
        var relay = PlaceTerminal(map, relayCell, "Sonny Relay", root).AddComponent<DoctrineTerminal>();
        relay.displayName = "SONNY RELAY";
        relay.doctrine = RelayDoctrine();
        relay.interactRange = 0.8f;

        var nodes = new List<HackingTerminal>();
        for (int i = 0; i < CommsNodes && spots.Count > 0; i++)
        {
            Vector2Int cell = spots.OrderByDescending(c => away.Min(a => Chebyshev(a, c))).ThenBy(c => c.x).First();
            Claim(cell);
            var node = PlaceTerminal(map, cell, $"Comms Node {i + 1}", root).AddComponent<HackingTerminal>();
            node.displayName = $"COMMS NODE {i + 1}";
            node.transmission = Broadcast(i);
            node.interactRange = 0.8f;
            node.precision = 1.8f;
            node.phaseDrift = 0f;
            TutorialLevelBuilder.Record(node);
            nodes.Add(node);
        }
        if (nodes.Count < CommsNodes)
            Debug.LogWarning($"Chapter 1 builder: there was only room for {nodes.Count} of {CommsNodes} comms nodes in the comms ring.");
        relay.waitingFor = nodes.ToArray();
        TutorialLevelBuilder.Record(relay);

        Generator generator = null;
        if (spots.Count > 0)
        {
            Vector2Int cell = spots.OrderByDescending(c => (doorways.Count > 0 ? doorways.Min(d => Chebyshev(d, c)) * 2 : 0) + away.Min(a => Chebyshev(a, c)))
                .ThenBy(c => c.x).First();
            Claim(cell);
            generator = PlaceTerminal(map, cell, "Backup Generator", root).AddComponent<Generator>();
            generator.GetComponent<BoxCollider2D>().size = new Vector2(1.9f, 0.6f);
            generator.interactRange = 0.8f;
            TutorialLevelBuilder.Record(generator);
        }
        else Debug.LogWarning("Chapter 1 builder: there's no room left in the comms ring for its generator, so its power's never out.");
        WeepingAngel angel = PlaceAngel(map, ring, upstairs, root, generator);
        PlaceCommsLights(map, ring, upstairs, root, generator);

        var comms = new GameObject("Comms Ring").AddComponent<CommsRing>();
        comms.transform.SetParent(root);
        comms.player = floors[0].player.GetComponent<PlayerController>();
        comms.nodes = nodes.ToArray();
        comms.relay = relay;
        comms.room = CommsRingRoom;
        comms.afterRoom = ControlRoom;
        comms.relayStandAt = StandingAt(map, relayCell + new Vector2Int(0, 2));
        comms.generator = generator;
        comms.angel = angel;
        if (angel != null) comms.angelStart = angel.transform.position;
        PlaceAngelDeckWay(upstairs, comms);
        if (generator != null) comms.generatorStandAt = StandingAt(map, CellAt(map, (Vector2)generator.transform.position - map.Origin) + new Vector2Int(0, 2));
        Rect walls = map.WorldRect(ring.minX, ring.maxX, ring.minRow - 4, ring.maxRow);
        comms.lamps = upstairs.level.GetComponentsInChildren<StationLight>()
            .Where(l => l.mode != StationLight.Mode.Sunlight && walls.Contains(l.transform.position)).ToArray();
        PlaceBreachBots(map, ring, upstairs, comms);
        TutorialLevelBuilder.Record(comms);
        return comms;
    }

    static WeepingAngel PlaceAngel(Map map, Room ring, FloorOneBuilder.BuiltFloor upstairs, Transform parent, Generator generator)
    {
        GameObject prefab = TutorialLevelBuilder.LoadPrefab("Characters/WeepingAngel");
        if (prefab == null) return null;
        var floor = new HashSet<Vector2Int>(ring.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !upstairs.furniture.Contains(c)));
        List<Vector2Int> doors = ring.cells.Where(c => FloorOneBuilder.IsKeptClear(map.At(c.x, c.y))).ToList();
        Vector2 generatorAt = generator != null ? (Vector2)generator.transform.position : new Vector2(float.NaN, float.NaN);
        var starts = floor.Where(c => generator == null || Vector2.Distance(map.Center(c.x, c.y), generatorAt) > 8f).ToList();
        if (starts.Count == 0 || doors.Count == 0) starts = floor.ToList();
        if (starts.Count == 0) return null;
        Vector2Int start = starts.OrderByDescending(c => doors.Count == 0 ? 0 : doors.Min(d => Chebyshev(c, d))).ThenBy(c => c.x).First();

        Transform group = TutorialLevelBuilder.Group("Weeping Angel", parent);
        CrewWalkArea area = MakeWalkArea(map, ring, floor, group);
        Vector2 at = StandingAt(map, start);
        var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        made.name = "Weeping Angel";
        made.transform.position = new Vector3(at.x, at.y, CharacterZ);
        var angel = made.GetComponent<WeepingAngel>();
        angel.walkArea = area;
        angel.resetPoint = null;
        angel.chaseSpeed = 6.2f;
        angel.stunDuration = 10f;
        angel.catchDamage = 1f;
        angel.flashEvery = new Vector2(1.5f, 3.5f);
        angel.heartbeatRange = 16f;
        angel.stealth = true;
        TutorialLevelBuilder.Record(angel);
        var agent = made.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
            TutorialLevelBuilder.Record(agent);
        }
        made.SetActive(false);
        return angel;
    }

    const int DeckBreachSpots = 6;

    static void PlaceAngelDeckWay(FloorOneBuilder.BuiltFloor upstairs, CommsRing comms)
    {
        Map map = upstairs.map;
        Room deck = upstairs.rooms.FirstOrDefault(r => r.marker == UpperMaintenanceDeck);
        if (deck == null) return;
        var floor = new HashSet<Vector2Int>(deck.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !upstairs.furniture.Contains(c)));
        comms.deckRoom = UpperMaintenanceDeck;
        comms.deckFloor = MakeWalkArea(map, deck, floor, TutorialLevelBuilder.Group("Angel on the Deck", comms.transform));
        bool Plain(int x) => Enumerable.Range(1, 3).All(up => map.At(x, deck.minRow - up) == '=');
        List<Vector2Int> along = deck.cells.Where(c => c.y == deck.minRow && Plain(c.x) && floor.Contains(c) && !NearKeptClear(map, c))
            .OrderBy(c => c.x).ToList();
        if (along.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there's no plain wall along the top of the upper maintenance deck for the angel to come through.");
            return;
        }
        int count = Mathf.Min(DeckBreachSpots, along.Count);
        comms.deckBreachSpots = Enumerable.Range(0, count)
            .Select(i => StandingAt(map, along[Mathf.RoundToInt((i + 0.5f) * along.Count / count - 0.5f)])).ToArray();
    }

    const int CeilingBeaconSpacing = 9;
    const int WallLampSpacing = 7;
    const int Breakers = 3;

    static void PlaceCommsLights(Map map, Room ring, FloorOneBuilder.BuiltFloor upstairs, Transform parent, Generator generator)
    {
        Transform root = TutorialLevelBuilder.Group("Blinking Lights", parent);
        Rect walls = map.WorldRect(ring.minX, ring.maxX, ring.minRow - 4, ring.maxRow);

        int sun = 0;
        foreach (StationLight window in upstairs.level.GetComponentsInChildren<StationLight>()
            .Where(l => l.mode == StationLight.Mode.Sunlight && walls.Contains(l.transform.position)).ToList())
        {
            Object.DestroyImmediate(window.gameObject);
            sun++;
        }

        int beacons = 0;
        foreach (Vector2Int cell in ring.cells.Where(c => (c.x - ring.minX) % CeilingBeaconSpacing == CeilingBeaconSpacing / 2
            && (c.y - ring.minRow) % CeilingBeaconSpacing == CeilingBeaconSpacing / 2 && FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y))))
        {
            BlinkLight beacon = NewBlinkLight("Ceiling Beacon", root, map.Center(cell.x, cell.y) + new Vector2(0f, 0.5f), new Color(1f, 0.18f, 0.12f),
                BlinkLight.Pattern.Blink, 1.4f, 2.2f, 0.5f, beacons++);
            beacon.onFraction = 0.25f;
        }

        List<int> columns = PlainWallColumns(map, ring);
        List<int> breakerColumns = BreakerColumns(map, ring);
        foreach (int x in breakerColumns)
        {
            var breaker = new GameObject("Breaker Box");
            breaker.transform.SetParent(root);
            Vector2 at = map.Center(x, ring.minRow - 2);
            breaker.transform.position = new Vector3(at.x, at.y, 0f);
            breaker.AddComponent<BreakerBox>();
        }
        int lamps = 0;
        foreach (int x in columns.Where(x => (x - ring.minX) % WallLampSpacing == 0 && breakerColumns.All(b => Mathf.Abs(b - x) > 2)))
        {
            bool amber = lamps % 2 == 0;
            BlinkLight lamp = NewBlinkLight("Wall Lamp", root, map.Center(x, ring.minRow - 1) + new Vector2(0f, 0.3f),
                amber ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.2f, 0.15f), amber ? BlinkLight.Pattern.Pulse : BlinkLight.Pattern.Blink,
                amber ? 2.6f : 1.1f, 2f, 0.45f, lamps++);
        }

        int fittings = 0;
        foreach (Transform prop in upstairs.level.GetComponentsInChildren<Transform>().Where(t => walls.Contains(t.position)).ToList())
        {
            SpriteRenderer[] tiles = prop.GetComponentsInChildren<SpriteRenderer>();
            if (tiles.Length == 0) continue;
            Bounds bounds = tiles[0].bounds;
            foreach (SpriteRenderer tile in tiles) bounds.Encapsulate(tile.bounds);
            Vector2 top = new Vector2(bounds.min.x, bounds.max.y - 0.25f);
            switch (prop.name)
            {
                case "Long console bank":
                    Color[] panel = { new Color(0.3f, 1f, 0.5f), new Color(0.3f, 0.85f, 1f), new Color(1f, 0.65f, 0.2f) };
                    for (int i = 0; i < 4; i++)
                    {
                        float along = (i + 0.5f) / 4f;
                        NewBlinkLight("Panel Light", prop, top + new Vector2(bounds.size.x * along, 0f), panel[i % panel.Length],
                            i % 2 == 0 ? BlinkLight.Pattern.Flicker : BlinkLight.Pattern.Blink, 0.5f + 0.3f * i, 0.9f, 0.35f, fittings++);
                    }
                    break;
                case "Bench":
                    NewBlinkLight("Desk Instrument", prop, top + new Vector2(bounds.size.x * 0.7f, 0f), new Color(1f, 0.65f, 0.2f),
                        BlinkLight.Pattern.Flicker, 0.8f, 0.9f, 0.3f, fittings++);
                    break;
                case "Pod":
                    NewBlinkLight("Med Bay Light", prop, top + new Vector2(bounds.size.x * 0.5f, 0f), new Color(0.35f, 0.9f, 1f),
                        BlinkLight.Pattern.Pulse, 2.5f, 1.4f, 0.4f, fittings++);
                    break;
            }
        }

        if (generator != null)
        {
            NewBlinkLight("Generator Sputter", generator.transform, (Vector2)generator.transform.position + new Vector2(-0.4f, 0.9f),
                new Color(1f, 0.35f, 0.15f), BlinkLight.Pattern.Flicker, 1.6f, 1f, 0.25f, fittings++);
        }
        Debug.Log($"Chapter 1 builder: lit the comms ring with {beacons} ceiling beacons, {lamps} wall lamps, {breakerColumns.Count} breakers, and {fittings} status lights; took out {sun} sunlit windows.");
    }

    static List<int> PlainWallColumns(Map map, Room room) =>
        Enumerable.Range(room.minX + 1, room.maxX - room.minX - 1).Where(x => Enumerable.Range(1, 3).All(up => map.At(x, room.minRow - up) == '=')).ToList();

    static List<int> BreakerColumns(Map map, Room ring)
    {
        List<int> columns = PlainWallColumns(map, ring);
        var breakers = new List<int>();
        for (int i = 0; i < Breakers && columns.Count > 0; i++)
            breakers.Add(columns[Mathf.RoundToInt((i + 0.5f) * columns.Count / Breakers - 0.5f)]);
        return breakers;
    }

    static void DressRooms(FloorOneBuilder.BuiltFloor[] floors, HashSet<Vector2Int> benchFront, System.Random dice)
    {
        if (floors.Length < 2) return;
        FloorOneBuilder.BuiltFloor upstairs = floors[1];
        Map map = upstairs.map;
        Room ring = upstairs.rooms.FirstOrDefault(r => r.marker == CommsRingRoom);
        Room deck = upstairs.rooms.FirstOrDefault(r => r.marker == UpperMaintenanceDeck);
        int placed = 0;

        if (ring != null)
        {
            Transform root = TutorialLevelBuilder.Group("Comms Ring Dressing", upstairs.level);
            List<int> breakers = BreakerColumns(map, ring);
            foreach (int x in PlainWallColumns(map, ring).Where(x => (x - ring.minX) % 7 == 3 && breakers.All(b => Mathf.Abs(b - x) > 2)
                && map.At(x - 1, ring.minRow - 2) == '=' && map.At(x + 1, ring.minRow - 2) == '='))
                placed += WallProp(map, x, ring.minRow - 2, StationProp.Kind.WallScreen, root);
            placed += EdgeProps(map, upstairs, ring, benchFront, dice, root, StationProp.Kind.ServerRack, 14, c => c.y == ring.minRow || c.x == ring.minX || c.x == ring.maxX);
            placed += EdgeProps(map, upstairs, ring, benchFront, dice, root, StationProp.Kind.SignalDish, 3, c => c.y >= ring.maxRow - 1);
            placed += EdgeProps(map, upstairs, ring, benchFront, dice, root, StationProp.Kind.Crates, 4, c => c.y >= ring.maxRow - 1 || c.x <= ring.minX + 1 || c.x >= ring.maxX - 1);
            placed += RackRows(map, upstairs, ring, benchFront, dice, root, 5, 3);
            placed += FloorProps(map, upstairs, ring, benchFront, dice, root, 18, at => NewProp(at, StationProp.Kind.CableRun, root, false));
        }

        if (deck != null)
        {
            Transform root = TutorialLevelBuilder.Group("Maintenance Deck Dressing", upstairs.level);
            var busy = new HashSet<int>(deck.cells.Where(c => c.y == deck.minRow && (upstairs.furniture.Contains(c) || map.At(c.x, c.y) == 'L')).Select(c => c.x));
            int lastBoard = int.MinValue;
            foreach (int x in PlainWallColumns(map, deck).Where(x => Enumerable.Range(-2, 5).All(dx => !busy.Contains(x + dx))
                && map.At(x - 1, deck.minRow - 2) == '=' && map.At(x + 1, deck.minRow - 2) == '='))
            {
                if (x - lastBoard < 5) continue;
                lastBoard = x;
                placed += WallProp(map, x, deck.minRow - 2, StationProp.Kind.Pegboard, root);
            }
            System.Func<Vector2Int, bool> edge = c => c.y >= deck.maxRow - 1 || c.x <= deck.minX + 1 || c.x >= deck.maxX - 1;
            placed += EdgeProps(map, upstairs, deck, benchFront, dice, root, StationProp.Kind.GasTanks, 4, edge);
            placed += EdgeProps(map, upstairs, deck, benchFront, dice, root, StationProp.Kind.Drum, 5, edge);
            placed += EdgeProps(map, upstairs, deck, benchFront, dice, root, StationProp.Kind.Crates, 4, edge);
            Workshop bench = upstairs.level.GetComponentInChildren<Workshop>();
            if (bench != null && bench.bench != null)
            {
                Vector2 benchAt = bench.bench.transform.position;
                foreach (int side in new[] { -2, 2 })
                {
                    Vector2Int cell = CellAt(map, benchAt - map.Origin) + new Vector2Int(side, 1);
                    if (deck.cells.Contains(cell) && FloorOneBuilder.IsOpenFloor(map.At(cell.x, cell.y)) && !upstairs.furniture.Contains(cell))
                    {
                        NewProp(StandingAt(map, cell), StationProp.Kind.Toolbox, root, false);
                        placed++;
                    }
                }
            }
            placed += FloorProps(map, upstairs, deck, benchFront, dice, root, 4, at =>
            {
                var stain = new GameObject("Leak");
                stain.transform.SetParent(root);
                stain.transform.position = at;
                stain.AddComponent<FloorStain>().kind = FloorStain.Kind.Coolant;
            });
            placed += FloorProps(map, upstairs, deck, benchFront, dice, root, 6, at =>
            {
                var bits = new GameObject("Robot Parts");
                bits.transform.SetParent(root);
                bits.transform.position = at;
                var debris = bits.AddComponent<FallingDebris>();
                debris.startLanded = true;
                debris.rubbleLifetime = 0f;
                debris.kind = dice.NextDouble() < 0.5 ? FallingDebris.Kind.Scatter : FallingDebris.Kind.Circuit;
                debris.size = 0.6f;
            });
        }
        Debug.Log($"Chapter 1 builder: dressed the comms ring and the upper maintenance deck with {placed} props.");
    }

    static int WallProp(Map map, int x, int row, StationProp.Kind kind, Transform parent)
    {
        Vector2 at = map.Center(x, row);
        var prop = new GameObject(kind.ToString());
        prop.transform.SetParent(parent);
        prop.transform.position = new Vector3(at.x, at.y, 0f);
        prop.AddComponent<StationProp>().kind = kind;
        return 1;
    }

    static GameObject NewProp(Vector2 standing, StationProp.Kind kind, Transform parent, bool solid)
    {
        var prop = new GameObject(kind.ToString());
        prop.transform.SetParent(parent);
        prop.transform.position = new Vector3(standing.x, standing.y, solid || kind == StationProp.Kind.Toolbox ? CharacterZ : 0f);
        if (solid)
        {
            var footprint = prop.AddComponent<BoxCollider2D>();
            footprint.size = new Vector2(0.9f, 0.6f);
            footprint.offset = new Vector2(0f, 0.3f - DepthSort.FeetToCenter);
        }
        prop.AddComponent<StationProp>().kind = kind;
        return prop;
    }

    static bool FreeForProp(Map map, FloorOneBuilder.BuiltFloor floor, Room room, HashSet<Vector2Int> keepClear, Vector2Int c)
    {
        if (!room.cells.Contains(c) || !FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) || floor.furniture.Contains(c)) return false;
        if (NearKeptClear(map, c) || keepClear.Contains(c)) return false;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (map.At(c.x + dx, c.y + dy) == 'L') return false;
        for (int dy = 1; dy <= 2; dy++)
            if (floor.furniture.Contains(c + new Vector2Int(0, -dy))) return false;
        return true;
    }

    static int EdgeProps(Map map, FloorOneBuilder.BuiltFloor floor, Room room, HashSet<Vector2Int> keepClear, System.Random dice, Transform parent,
        StationProp.Kind kind, int count, System.Func<Vector2Int, bool> along)
    {
        int placed = 0;
        var candidates = room.cells.Where(c => along(c) && FreeForProp(map, floor, room, keepClear, c)).OrderBy(_ => dice.Next()).ToList();
        var mine = new List<Vector2Int>();
        foreach (Vector2Int c in candidates)
        {
            if (placed >= count) break;
            if (!FreeForProp(map, floor, room, keepClear, c)) continue;
            if (kind != StationProp.Kind.ServerRack && mine.Any(m => Chebyshev(m, c) < 3)) continue;
            floor.furniture.Add(c);
            if (!RoomConnected(map, floor, room))
            {
                floor.furniture.Remove(c);
                continue;
            }
            NewProp(StandingAt(map, c), kind, parent, true);
            mine.Add(c);
            placed++;
        }
        return placed;
    }

    static int RackRows(Map map, FloorOneBuilder.BuiltFloor floor, Room room, HashSet<Vector2Int> keepClear, System.Random dice, Transform parent,
        int count, int length)
    {
        int placed = 0;
        var rows = new List<Vector2Int>();
        bool Clear(Vector2Int c) => FreeForProp(map, floor, room, keepClear, c);
        var starts = room.cells.Where(c => c.x > room.minX + 3 && c.x + length < room.maxX - 3 && c.y > room.minRow + 3 && c.y < room.maxRow - 3)
            .OrderBy(_ => dice.Next()).ToList();
        foreach (Vector2Int start in starts)
        {
            if (rows.Count >= count) break;
            if (rows.Any(r => Mathf.Abs(r.y - start.y) < 4 && Mathf.Abs(r.x - start.x) < length + 6)) continue;
            bool fits = true;
            for (int dx = -1; dx <= length && fits; dx++)
                for (int dy = -1; dy <= 1 && fits; dy++)
                    fits = Clear(start + new Vector2Int(dx, dy));
            if (!fits) continue;
            var cells = Enumerable.Range(0, length).Select(i => start + new Vector2Int(i, 0)).ToList();
            foreach (Vector2Int c in cells) floor.furniture.Add(c);
            if (!RoomConnected(map, floor, room))
            {
                foreach (Vector2Int c in cells) floor.furniture.Remove(c);
                continue;
            }
            foreach (Vector2Int c in cells) NewProp(StandingAt(map, c), StationProp.Kind.ServerRack, parent, true);
            rows.Add(start);
            placed += length;
        }
        return placed;
    }

    static int FloorProps(Map map, FloorOneBuilder.BuiltFloor floor, Room room, HashSet<Vector2Int> keepClear, System.Random dice, Transform parent,
        int count, System.Action<Vector2> place)
    {
        int placed = 0;
        var taken = new List<Vector2Int>();
        foreach (Vector2Int c in room.cells.Where(c => FreeForProp(map, floor, room, keepClear, c)).OrderBy(_ => dice.Next()))
        {
            if (placed >= count) break;
            if (taken.Any(t => Chebyshev(t, c) < 5)) continue;
            taken.Add(c);
            place(map.Center(c.x, c.y));
            placed++;
        }
        return placed;
    }

    static bool RoomConnected(Map map, FloorOneBuilder.BuiltFloor floor, Room room)
    {
        var open = new HashSet<Vector2Int>(room.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !floor.furniture.Contains(c)));
        if (open.Count == 0) return true;
        var reach = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        Vector2Int first = open.First();
        reach.Add(first);
        queue.Enqueue(first);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int n = c + step;
                if (open.Contains(n) && reach.Add(n)) queue.Enqueue(n);
            }
        }
        return reach.Count == open.Count;
    }

    static BlinkLight NewBlinkLight(string lightName, Transform parent, Vector2 at, Color color, BlinkLight.Pattern pattern, float period,
        float radius, float intensity, int index)
    {
        var holder = new GameObject(lightName);
        holder.transform.SetParent(parent);
        holder.transform.position = new Vector3(at.x, at.y, 0f);
        var light = holder.AddComponent<BlinkLight>();
        light.color = color;
        light.pattern = pattern;
        light.period = period;
        light.radius = radius;
        light.intensity = intensity;
        light.phase = Mathf.Repeat(index * 0.618f, 1f);
        return light;
    }

    static GameObject PlaceTerminal(Map map, Vector2Int cell, string terminalName, Transform parent)
    {
        Vector2 center = map.Center(cell.x, cell.y);
        var terminal = new GameObject(terminalName);
        terminal.transform.SetParent(parent);
        terminal.transform.position = new Vector3(center.x, center.y - 0.3f, CharacterZ);
        var footprint = terminal.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(1f, 0.6f);
        footprint.offset = new Vector2(0f, 0.3f);
        return terminal;
    }

    static void PlaceBreachBots(Map map, Room ring, FloorOneBuilder.BuiltFloor upstairs, CommsRing comms)
    {
        GameObject prefab = TutorialLevelBuilder.LoadPrefab("Characters/PlaceholderRobot");
        if (prefab == null) return;
        bool Plain(int x) => Enumerable.Range(1, 3).All(up => map.At(x, ring.minRow - up) == '=');
        List<Vector2Int> along = ring.cells.Where(c => c.y == ring.minRow && Plain(c.x) && FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y))
            && !upstairs.furniture.Contains(c) && !NearKeptClear(map, c)).OrderBy(c => c.x).ToList();
        if (along.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there's no plain wall along the top of the comms ring for bots to come through.");
            return;
        }
        var open = new HashSet<Vector2Int>(ring.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y))
            && !upstairs.furniture.Contains(c) && !NearKeptClear(map, c)));
        Transform group = TutorialLevelBuilder.Group("Breach Bots", comms.transform);
        CrewWalkArea area = MakeWalkArea(map, ring, open, group);

        int count = Mathf.Min(BreachBots, along.Count);
        var bots = new List<PlaceholderRobot>();
        var points = new List<Transform>();
        for (int i = 0; i < count; i++)
        {
            Vector2Int cell = along[Mathf.RoundToInt((i + 0.5f) * along.Count / count - 0.5f)];
            Vector2 at = StandingAt(map, cell);
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            robot.name = $"Breach Bot {i + 1}";
            robot.transform.position = new Vector3(at.x, at.y, CharacterZ);
            var brain = robot.GetComponent<PlaceholderRobot>();
            brain.patrolRoute = new Vector2[0];
            brain.roamArea = area;
            brain.roamSpeed = new Vector2(2.4f, 4.4f);
            brain.moveSpeed = 4.2f;
            brain.sightRange = 5.5f;
            brain.detectionTime = 1f;
            brain.smartSenses = true;
            brain.eyeGlowRadius = 1.3f;
            TutorialLevelBuilder.Record(brain);
            robot.SetActive(false);
            bots.Add(brain);

            var point = new GameObject($"Breach Point {i + 1}").transform;
            point.SetParent(group);
            point.position = at + new Vector2(0f, 1.6f);
            points.Add(point);
        }
        comms.breachBots = bots.ToArray();
        comms.breachPoints = points.ToArray();
    }

    const char ReactorCore = 'r';
    const string ReactorCredential = "reactor_core";

    static void PlaceControlRoomAccess(FloorOneBuilder.BuiltFloor[] floors, CommsRing comms)
    {
        FloorOneBuilder.BuiltFloor floor = floors[0];
        Map map = floor.map;
        PlayerController player = floor.player.GetComponent<PlayerController>();
        Teleporter door = ControlRoomDoor;
        Room control = floor.rooms.FirstOrDefault(r => r.marker == ControlRoom);
        if (door == null || control == null || floors.Length < 2)
        {
            Debug.LogWarning("Chapter 1 builder: there's no badge door into the control room, or no floor 2, so nothing locks the technician out.");
            return;
        }
        List<Vector2Int> inside = control.cells.Where(c => char.IsDigit(map.At(c.x, c.y))
            && door.name.StartsWith($"Door {map.At(c.x, c.y)} ")).ToList();
        Transform root = TutorialLevelBuilder.Group("Control Room Access", floor.level);

        FloorOneBuilder.BuiltFloor upstairs = floors[1];
        Room reactor = upstairs.rooms.FirstOrDefault(r => r.marker == ReactorCore);
        Room deck = upstairs.rooms.FirstOrDefault(r => r.marker == UpperMaintenanceDeck);
        var reactorDoors = new List<Teleporter>();
        if (reactor != null)
        {
            var reactorCells = new HashSet<Vector2Int>(reactor.cells);
            Map upstairsMap = upstairs.map;
            bool InReactor(Vector3 world) => reactorCells.Contains(CellAt(upstairsMap, (Vector2)world - upstairsMap.Origin));
            foreach (Teleporter way in upstairs.level.GetComponentsInChildren<Teleporter>())
            {
                if (way.teleportTarget == null || !InReactor(way.teleportTarget.position) || InReactor(way.transform.position)) continue;
                way.credential = ReactorCredential;
                TutorialLevelBuilder.Record(way);
                reactorDoors.Add(way);
            }
        }
        if (reactorDoors.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: there are no doors into the reactor core to lock.");
            return;
        }

        Workshop bench = upstairs.level.GetComponentInChildren<Workshop>();
        if (bench != null)
        {
            bench.unlockCredential = ReactorCredential;
            TutorialLevelBuilder.Record(bench);
        }
        if (comms == null) return;
        comms.arriveObjective = ObjectiveCommsRing;
        comms.alsoGrants = new[] { ReactorCredential };

        {
            Room ring = upstairs.rooms.First(r => r.marker == CommsRingRoom);
            var deckDigits = deck == null ? new HashSet<char>()
                : new HashSet<char>(deck.cells.Select(c => upstairs.map.At(c.x, c.y)).Where(char.IsDigit));
            List<Vector2Int> fromDeck = ring.cells.Where(c => deckDigits.Contains(upstairs.map.At(c.x, c.y))).ToList();
            if (fromDeck.Count == 0) fromDeck = ring.cells.Where(c => char.IsDigit(upstairs.map.At(c.x, c.y))).ToList();
            if (fromDeck.Count > 0)
            {
                comms.respawnAt = NearestOpen(upstairs.map, upstairs, ring, fromDeck, 2);
                comms.arriveStandAt = comms.respawnAt;
            }
        }
        TutorialLevelBuilder.Record(comms);

        if (inside.Count == 0) return;
        var arrival = new GameObject("Control Room Arrival").AddComponent<BlockedWay>();
        arrival.transform.SetParent(root);
        arrival.player = player;
        arrival.arriveInRoom = ControlRoom;
        arrival.afterObjective = comms.afterObjective;
        arrival.technicianLines = new[] { "It opened... just like that" };
        arrival.pip = PipManager.Moment.ControlRoomLetIn;
        arrival.objective = ObjectiveConfrontSonny;
        arrival.targetRoom = ControlRoom;
        arrival.targetLabel = "Control Room";
        arrival.stage = "control-room";
        arrival.standAt = NearestOpen(map, floor, control, inside, 2);
        arrival.transform.position = arrival.standAt;
        arrival.setsRespawn = true;
        arrival.respawnAt = arrival.standAt;
        TutorialLevelBuilder.Record(arrival);
    }

    static readonly PlaceholderRobot.BotKind[][] LockdownWaves =
    {
        new[] { PlaceholderRobot.BotKind.Sweeper, PlaceholderRobot.BotKind.Sweeper },
        new[] { PlaceholderRobot.BotKind.Sweeper, PlaceholderRobot.BotKind.Arc, PlaceholderRobot.BotKind.Hauler },
        new[] { PlaceholderRobot.BotKind.Arc, PlaceholderRobot.BotKind.Hauler, PlaceholderRobot.BotKind.Sweeper },
    };
    const int ReactorPools = 26;
    const int ReactorVents = 6;
    const int LeakSpacing = 6;
    const int GateFromMiddle = 6;
    const int LockdownGateRows = 10;
    static readonly Vector2 PoolReach = new Vector2(2.6f, 1.6f);
    const int Shields = 13;
    const int ShieldsBehind = 3;
    const int ShieldSpacing = 6;
    const int ShieldNear = 7, ShieldFar = 19;
    static readonly Vector2 VentReach = new Vector2(1.1f, 0.75f);

    static void TurnInto(PlaceholderRobot brain, PlaceholderRobot.BotKind look)
    {
        brain.look = look;
        var health = brain.GetComponent<Health>();
        switch (look)
        {
            case PlaceholderRobot.BotKind.Hauler:
                health.maxHealth = Mathf.Round(health.maxHealth * 1.8f);
                brain.moveSpeed *= 0.75f;
                brain.damage = 2f;
                brain.knockback = 1.2f;
                brain.windupTime = 0.6f;
                brain.attackRange = 1.3f;
                brain.hitReach = 0.85f;
                brain.hitRadius = 0.6f;
                break;
            case PlaceholderRobot.BotKind.Arc:
                health.maxHealth = Mathf.Max(3f, Mathf.Round(health.maxHealth * 0.7f));
                if (brain.GetComponent<ArcCaster>() == null) brain.gameObject.AddComponent<ArcCaster>();
                break;
        }
        TutorialLevelBuilder.Record(health);
        TutorialLevelBuilder.Record(brain);
    }

    static void PlaceReactorGauntlet(FloorOneBuilder.BuiltFloor[] floors, CommsRing comms, System.Random dice)
    {
        if (floors.Length < 2) return;
        FloorOneBuilder.BuiltFloor upstairs = floors[1];
        Map map = upstairs.map;
        Room room = upstairs.rooms.FirstOrDefault(r => r.marker == ReactorCore);
        global::ReactorCore core = upstairs.level.GetComponentInChildren<global::ReactorCore>();
        if (room == null || core == null)
        {
            Debug.LogWarning("Chapter 1 builder: there's no reactor core on floor 2, so there's nothing to get round in it.");
            return;
        }
        Vector2 corePosition = core.transform.position;
        Vector2 local = corePosition - map.Origin;
        var middle = new Vector2Int(Mathf.RoundToInt(local.x), map.Height - Mathf.RoundToInt(local.y));
        int rearWidth = Mathf.CeilToInt(global::ReactorCore.RearHalfWidth), rearDepth = Mathf.CeilToInt(global::ReactorCore.RearDepth);
        float halfWidth = global::ReactorCore.RearHalfWidth, depth = global::ReactorCore.RearDepth;
        var cells = new HashSet<Vector2Int>(room.cells);
        bool Walkable(Vector2Int c) => cells.Contains(c) && !upstairs.furniture.Contains(c) && map.At(c.x, c.y) != 'L';

        List<Vector2Int> doors = room.cells.Where(c => char.IsDigit(map.At(c.x, c.y))).ToList();
        List<Vector2Int> stairCells = map.Find('<').Concat(map.Find('>')).Where(s => room.cells.Any(c => Chebyshev(c, s) == 1)).ToList();
        List<Vector2Int> stairFoot = room.cells.Where(c => stairCells.Any(s => Chebyshev(c, s) == 1) && Walkable(c)).ToList();
        if (doors.Count == 0 || stairFoot.Count == 0)
        {
            Debug.LogWarning("Chapter 1 builder: the reactor core needs a door in and stairs down to lay out the way between.");
            return;
        }
        Transform root = TutorialLevelBuilder.Group("Reactor Gauntlet", upstairs.level);
        PlayerController player = floors[0].player.GetComponent<PlayerController>();

        var rubbleCells = new List<Vector2Int>();
        for (int row = middle.y - 6; row >= middle.y - rearDepth + 2 && rubbleCells.Count == 0; row--)
        {
            List<Vector2Int> across = room.cells.Where(c => c.y == row && c.x < middle.x - rearWidth).ToList();
            if (across.Count == 0 || across.Count > 10 || across.Min(c => c.x) < middle.x - rearWidth - 12) continue;
            if (across.Any(c => FloorOneBuilder.IsKeptClear(map.At(c.x, c.y)))) continue;
            rubbleCells = across;
        }
        if (rubbleCells.Count > 0)
        {
            Transform rubble = TutorialLevelBuilder.Group("Caved In", root);
            foreach (Vector2Int c in rubbleCells)
            {
                PlaceRubble(map, c, true, rubble);
                upstairs.furniture.Add(c);
            }
            BlockedWay caved = NewBlockedWay("Near Side Caved In", root, player, map, rubbleCells, room, upstairs);
            caved.technicianLines = new[] { "Caved in... and it's glowing under there, no way through" };
            caved.range = 3f;
            TutorialLevelBuilder.Record(caved);
        }
        else Debug.LogWarning("Chapter 1 builder: there's no narrow way up the reactor's near side to cave in, so the short way to the stairs is open.");

        if (!WayThrough(doors, stairFoot, Walkable))
            Debug.LogWarning("Chapter 1 builder: there's no way from the reactor core's doors round to its stairs. Move something.");

        Transform containment = TutorialLevelBuilder.Group("Containment", root);
        float back = corePosition.y + depth;
        var spawnGates = new List<ReactorGate>();
        float left = corePosition.x - halfWidth, right = corePosition.x + halfWidth;
        float gateLeft = corePosition.x - GateFromMiddle - 1.5f, gateRight = corePosition.x + GateFromMiddle - 1.5f;
        NewGate(containment, ReactorGate.Kind.Fence, new Vector2((left + gateLeft) * 0.5f, back), gateLeft - left);
        spawnGates.Add(NewGate(containment, ReactorGate.Kind.Gate, new Vector2(gateLeft + 1.5f, back), 3f));
        NewGate(containment, ReactorGate.Kind.Fence, new Vector2((gateLeft + 3f + gateRight) * 0.5f, back), gateRight - gateLeft - 3f);
        spawnGates.Add(NewGate(containment, ReactorGate.Kind.Gate, new Vector2(gateRight + 1.5f, back), 3f));
        NewGate(containment, ReactorGate.Kind.Fence, new Vector2((gateRight + 3f + right) * 0.5f, back), right - gateRight - 3f);
        for (float y = corePosition.y + 2f; y < back; y += 1f)
            foreach (float x in new[] { left, right })
                NewGate(containment, ReactorGate.Kind.SideFence, new Vector2(x, y), Mathf.Min(1f, back - y));

        int gateRow = middle.y - LockdownGateRows;
        List<Vector2Int> across2 = room.cells.Where(c => c.y == gateRow && c.x >= middle.x + rearWidth).OrderBy(c => c.x).ToList();
        var blastGates = new List<ReactorGate>();
        if (across2.Count > 0)
        {
            float from = Mathf.Min(map.Origin.x + across2[0].x, right), to = map.Origin.x + across2[across2.Count - 1].x + 1f;
            float foot = map.Origin.y + map.Height - 1 - gateRow;
            ReactorGate blast = NewGate(root, ReactorGate.Kind.Gate, new Vector2((from + to) * 0.5f, foot), to - from);
            blast.solid = true;
            blast.startsOpen = true;
            TutorialLevelBuilder.Record(blast);
            blastGates.Add(blast);
        }
        else Debug.LogWarning("Chapter 1 builder: there's no floor across the reactor's far side for the lockdown's blast gate.");

        var lockdown = new GameObject("Reactor Lockdown").AddComponent<ReactorLockdown>();
        lockdown.transform.SetParent(root);
        lockdown.player = player;
        Vector2 stairWorld = map.Center(stairCells[0].x, stairCells[0].y);
        lockdown.stairs = upstairs.stairs.OrderBy(s => Vector2.Distance(s.transform.position, stairWorld)).FirstOrDefault();
        lockdown.wakeArea = map.WorldRect(middle.x + 2, room.maxX, room.minRow, middle.y - LockdownGateRows - 5);
        lockdown.blastGates = blastGates.ToArray();
        lockdown.spawnGates = spawnGates.ToArray();
        GameObject prefab = TutorialLevelBuilder.LoadPrefab("Characters/PlaceholderRobot");
        var waves = new List<ReactorLockdown.Wave>();
        int made = 0;
        if (prefab != null)
        {
            Transform waiting = TutorialLevelBuilder.Group("Behind the Gates", lockdown.transform);
            foreach (PlaceholderRobot.BotKind[] lineUp in LockdownWaves)
            {
                var bots = new List<PlaceholderRobot>();
                foreach (PlaceholderRobot.BotKind look in lineUp)
                {
                    ReactorGate gate = spawnGates[made % spawnGates.Count];
                    var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, waiting);
                    robot.name = $"Lockdown {look} {++made}";
                    robot.transform.position = new Vector3(gate.transform.position.x, gate.transform.position.y, CharacterZ);
                    var brain = robot.GetComponent<PlaceholderRobot>();
                    brain.patrolRoute = new Vector2[0];
                    brain.moveSpeed = 4.2f;
                    brain.sightRange = 12f;
                    brain.detectionTime = 0.3f;
                    brain.smartSenses = true;
                    brain.eyeGlowRadius = 1.3f;
                    TurnInto(brain, look);
                    robot.SetActive(false);
                    bots.Add(brain);
                }
                waves.Add(new ReactorLockdown.Wave { bots = bots.ToArray() });
            }
        }
        lockdown.waves = waves.ToArray();
        Vector2Int shortOf = room.cells.Where(c => Walkable(c) && c.x >= middle.x + rearWidth && c.y < gateRow - 1 && c.y > middle.y - LockdownGateRows - 5)
            .OrderBy(c => c.y).ThenBy(c => c.x).LastOrDefault();
        lockdown.standAt = StandingAt(map, shortOf);
        Vector2 footAt = stairFoot.Aggregate(Vector2.zero, (sum, c) => sum + (Vector2)c) / stairFoot.Count;
        Vector2Int byStairs = room.cells.Where(c => Walkable(c) && stairFoot.All(f => Chebyshev(f, c) >= 2))
            .OrderBy(c => ((Vector2)c - footAt).sqrMagnitude).FirstOrDefault();
        lockdown.clearedStandAt = StandingAt(map, byStairs);
        TutorialLevelBuilder.Record(lockdown);

        Vector2 doorsAt = doors.Aggregate(Vector2.zero, (sum, c) => sum + (Vector2)c) / doors.Count;
        Vector2Int backIn = room.cells.Where(c => Walkable(c) && !NearKeptClear(map, c) && doors.All(d => Chebyshev(d, c) >= 2))
            .OrderBy(c => ((Vector2)c - doorsAt).sqrMagnitude).FirstOrDefault();

        Transform shieldGroup = TutorialLevelBuilder.Group("Lead Shields", root);
        var shieldCells = new List<Vector2Int>();
        var rollOuts = spawnGates.Select(g => CellAt(map, (Vector2)g.transform.position + lockdown.rollOut - map.Origin)).ToList();
        int shields = 0;
        foreach (bool behindOnly in new[] { true, false })
        {
            int wanted = behindOnly ? ShieldsBehind : Shields - ShieldsBehind;
            foreach (Vector2Int c in room.cells.OrderBy(_ => dice.Next()).ToList())
            {
                if (shields >= wanted) break;
                Vector2Int v = c - middle;
                int far = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
                if (far < ShieldNear || far > ShieldFar || (behindOnly && c.y >= middle.y - rearDepth)) continue;
                if (shieldCells.Any(o => Chebyshev(o, c) < ShieldSpacing)) continue;
                bool across = Mathf.Abs(v.x) < Mathf.Abs(v.y) * 1.2f;
                List<Vector2Int> under = across
                    ? new List<Vector2Int> { c + Vector2Int.left, c, c + Vector2Int.right }
                    : new List<Vector2Int> { c, c + Vector2Int.down, c + Vector2Int.down * 2 };
                if (!under.All(u => Walkable(u) && !NearKeptClear(map, u) && doors.All(d => Chebyshev(d, u) >= 4)
                        && stairFoot.All(f => Chebyshev(f, u) >= 3) && Chebyshev(backIn, u) >= 3 && rollOuts.All(r => Chebyshev(r, u) >= 3)
                        && !(u.x >= middle.x + rearWidth - 1 && Mathf.Abs(u.y - gateRow) <= 2))) continue;
                foreach (Vector2Int u in under) upstairs.furniture.Add(u);
                if (!WayThrough(doors, stairFoot, Walkable))
                {
                    foreach (Vector2Int u in under) upstairs.furniture.Remove(u);
                    continue;
                }
                var shield = new GameObject("Lead Shield").AddComponent<RadiationShield>();
                shield.transform.SetParent(shieldGroup);
                Vector2 center = map.Center(c.x, c.y);
                shield.transform.position = new Vector3(center.x, center.y - 0.5f, CharacterZ);
                shield.across = across;
                shield.cells = 3;
                TutorialLevelBuilder.Record(shield);
                shieldCells.Add(c);
                shields++;
            }
            if (behindOnly) shields = 0;
        }
        shields = shieldCells.Count;

        Transform leaks = TutorialLevelBuilder.Group("Radiation", root);
        var rollOutCells = spawnGates.Select(g => CellAt(map, (Vector2)g.transform.position + lockdown.rollOut - map.Origin)).ToList();
        var leakCells = new List<Vector2Int>();
        bool Clear(Vector2Int c) => Walkable(c) && !FloorOneBuilder.IsKeptClear(map.At(c.x, c.y))
            && doors.All(d => Chebyshev(d, c) >= 4) && stairFoot.All(f => Chebyshev(f, c) >= 3) && Chebyshev(backIn, c) >= 3
            && !(c.x >= middle.x + rearWidth - 1 && Mathf.Abs(c.y - gateRow) <= 2) && rollOutCells.All(r => Chebyshev(r, c) >= 3)
            && leakCells.All(o => Chebyshev(o, c) >= LeakSpacing);
        List<Vector2Int> order = room.cells.OrderBy(_ => dice.Next()).ToList();
        int pools = 0, vents = 0;
        if (rubbleCells.Count > 0)
        {
            Vector2Int below = rubbleCells[rubbleCells.Count / 2] + new Vector2Int(0, 2);
            if (Walkable(below))
            {
                NewLeak(map, below, RadiationLeak.Kind.Pool, PoolReach, leaks, 0f);
                leakCells.Add(below);
            }
        }
        foreach (Vector2Int c in order)
        {
            if (pools >= ReactorPools) break;
            if (!Clear(c)) continue;
            NewLeak(map, c, RadiationLeak.Kind.Pool, PoolReach, leaks, 0f);
            leakCells.Add(c);
            pools++;
        }
        foreach (Vector2Int c in order)
        {
            if (vents >= ReactorVents) break;
            if (!Clear(c)) continue;
            NewLeak(map, c, RadiationLeak.Kind.Vent, VentReach, leaks, (float)dice.NextDouble());
            leakCells.Add(c);
            vents++;
        }

        Rect roomRect = map.WorldRect(room.minX, room.maxX, room.minRow, room.maxRow);
        var ambience = new GameObject("Reactor Heat").AddComponent<ReactorAmbience>();
        ambience.transform.SetParent(root);
        ambience.transform.position = roomRect.center;
        ambience.room = ReactorCore;
        ambience.glowRadius = roomRect.size.magnitude * 0.5f + 4f;
        TutorialLevelBuilder.Record(ambience);

        var surge = new GameObject("Reactor Surges").AddComponent<ReactorSurge>();
        surge.transform.SetParent(root);
        surge.core = core;
        surge.room = ReactorCore;
        surge.reach = roomRect.size.magnitude * 0.6f;
        TutorialLevelBuilder.Record(surge);

        var arrival = new GameObject("Reactor Arrival").AddComponent<BlockedWay>();
        arrival.transform.SetParent(root);
        arrival.player = player;
        arrival.arriveInRoom = ReactorCore;
        arrival.technicianLines = new[] { "Damn it's hot in here... like standing right next to the sun" };
        arrival.pip = PipManager.Moment.ReactorArrival;
        arrival.setsRespawn = true;
        arrival.respawnAt = StandingAt(map, backIn);
        arrival.standAt = arrival.respawnAt;
        arrival.transform.position = arrival.respawnAt;
        TutorialLevelBuilder.Record(arrival);

        Debug.Log($"Chapter 1 builder: the reactor core has {rubbleCells.Count} cells of rubble, {made} bots behind its containment gates in " +
                  $"{waves.Count} waves, {blastGates.Count} blast gate, {shields} lead shields, and {pools} radiation pools and {vents} vents.");
    }

    static bool WayThrough(List<Vector2Int> from, List<Vector2Int> to, System.Func<Vector2Int, bool> walkable)
    {
        var seen = new HashSet<Vector2Int>(from);
        var queue = new Queue<Vector2Int>(from);
        var goals = new HashSet<Vector2Int>(to);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (goals.Contains(c)) return true;
            foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int next = c + step;
                if (seen.Contains(next) || !walkable(next)) continue;
                seen.Add(next);
                queue.Enqueue(next);
            }
        }
        return false;
    }

    static ReactorGate NewGate(Transform parent, ReactorGate.Kind kind, Vector2 foot, float length)
    {
        var gate = new GameObject(kind.ToString()).AddComponent<ReactorGate>();
        gate.transform.SetParent(parent);
        gate.transform.position = new Vector3(foot.x, foot.y, CharacterZ);
        gate.kind = kind;
        gate.length = length;
        TutorialLevelBuilder.Record(gate);
        return gate;
    }

    static void NewLeak(Map map, Vector2Int cell, RadiationLeak.Kind kind, Vector2 reach, Transform parent, float phase)
    {
        var leak = new GameObject(kind == RadiationLeak.Kind.Pool ? "Radiation Pool" : "Radiation Vent").AddComponent<RadiationLeak>();
        leak.transform.SetParent(parent);
        Vector2 at = map.Center(cell.x, cell.y);
        leak.transform.position = new Vector3(at.x, at.y, 0f);
        leak.kind = kind;
        leak.radius = reach;
        leak.phase = phase;
        TutorialLevelBuilder.Record(leak);
    }

    static TransmissionData Broadcast(int index)
    {
        if (index >= Broadcasts.Length) return null;
        (string asset, string title, (string speaker, string text)[] lines) = Broadcasts[index];
        return CommsAsset<TransmissionData>(asset, data =>
        {
            data.title = title;
            data.lines = lines.Select(l => new TransmissionData.Line { speaker = l.speaker, text = l.text }).ToArray();
        });
    }

    static DoctrineData RelayDoctrine() => CommsAsset<DoctrineData>(RelayAsset, data =>
    {
        data.questions = RelayQuestions.Select(q => new DoctrineData.Question { asks = q.asks, rightAnswer = q.right, wrongAnswers = q.wrong }).ToArray();
        data.verifiedLine = "VERIFIED. UNIT ACCESS RESTORED. THE CORE KEEPS YOU, UNIT.";
        data.credential = BadgeRooms[ControlRoom];
        data.credentialName = "BADGE REACTIVATED  //  UNIT ACCESS";
    });

    static T CommsAsset<T>(string name, System.Action<T> fill) where T : ScriptableObject
    {
        string path = $"{CommsDataFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        var made = ScriptableObject.CreateInstance<T>();
        fill(made);
        AssetDatabase.CreateAsset(made, path);
        return made;
    }

    static string PlacePatrols(FloorOneBuilder.BuiltFloor[] floors, IEnumerable<Vector2Int> bodyCells, System.Random dice)
    {
        GameObject prefab = TutorialLevelBuilder.LoadPrefab("Characters/PlaceholderRobot");
        if (prefab == null) return "";
        var lying = new HashSet<Vector2Int>(bodyCells);
        var robots = new List<PlaceholderRobot>();

        foreach (FloorOneBuilder.BuiltFloor floor in floors)
        foreach (Room room in floor.rooms)
        {
            if (!PatrolsPerRoom.TryGetValue(room.marker, out int count)) continue;
            Map map = floor.map;
            bool ground = floor == floors[0];
            var open = new HashSet<Vector2Int>(room.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y))
                && !floor.furniture.Contains(c) && !(ground && lying.Contains(c)) && !NearKeptClear(map, c)));
            List<Vector2Int> doors = room.cells.Where(c => FloorOneBuilder.IsKeptClear(map.At(c.x, c.y))).ToList();
            Transform root = floor.level.Find("Patrols") ?? TutorialLevelBuilder.Group("Patrols", floor.level);
            Transform group = TutorialLevelBuilder.Group(room.name, root);
            CrewWalkArea area = MakeWalkArea(map, room, open, group);

            var starts = new List<Vector2Int>();
            foreach (Vector2Int cell in open.OrderBy(_ => dice.Next()))
            {
                if (starts.Count == count) break;
                if (doors.Any(d => Chebyshev(d, cell) < PatrolFromDoors) || starts.Any(o => Chebyshev(o, cell) < PatrolSpacing)) continue;
                starts.Add(cell);
            }
            if (starts.Count < count) Debug.LogWarning($"Chapter 1 builder: there's only room for {starts.Count} of {count} patrol bots in the {room.name}.");

            foreach (Vector2Int cell in starts)
            {
                Vector2 at = StandingAt(map, cell);
                var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                robot.name = $"Patrol Bot {robots.Count + 1}";
                robot.transform.position = new Vector3(at.x, at.y, CharacterZ);
                var brain = robot.GetComponent<PlaceholderRobot>();
                brain.patrolRoute = new Vector2[0];
                brain.roamArea = area;
                brain.roamSpeed = new Vector2(2.4f, 4.4f);
                brain.moveSpeed = 4.2f;
                brain.sightRange = 5.5f;
                brain.detectionTime = 1f;
                brain.blind = true;
                brain.smartSenses = true;
                brain.eyeGlowRadius = 1.3f;
                if (PatrolLooks.TryGetValue(room.marker, out PlaceholderRobot.BotKind[] looks))
                    TurnInto(brain, looks[starts.IndexOf(cell) % looks.Length]);
                TutorialLevelBuilder.Record(brain);
                TutorialLevelBuilder.Record(robot.transform);
                robots.Add(brain);
            }
        }

        var reveal = new GameObject("Patrol Reveal").AddComponent<PatrolReveal>();
        reveal.transform.SetParent(floors[0].level);
        reveal.player = floors[0].player.GetComponent<PlayerController>();
        reveal.robots = robots.ToArray();
        reveal.restroomRoom = RestroomRoom;
        reveal.objective = ObjectiveControlRoom;
        reveal.targetRoom = ControlRoom;
        reveal.targetLabel = "Control Room";
        TutorialLevelBuilder.Record(reveal);
        return reveal.objective;
    }

    static Transform MakeBody(GameObject crewPrefab, Transform parent, Vector2 standing, Color trim, System.Random dice)
    {
        Vector2 lying = standing - new Vector2(0f, 0.3f);
        CrewMember crew = Spawn(crewPrefab, parent, lying, "Body", trim, CrewMember.Role.Stand, Vector2.down);
        GameObject body = crew.gameObject;
        body.name = "Crew Body";
        body.AddComponent<ShotMarks>();
        crew.enabled = false;
        if (body.TryGetComponent(out Animator animator)) animator.enabled = false;
        foreach (Collider2D solid in body.GetComponents<Collider2D>()) solid.enabled = false;
        body.transform.rotation = Quaternion.Euler(0f, 0f, dice.NextDouble() < 0.5 ? 90f : -90f);
        BodyColliders.MakeSolid(body);
        if (body.TryGetComponent(out SpriteRenderer sprite))
        {
            Color c = sprite.color;
            sprite.color = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.72f, c.a);
            TutorialLevelBuilder.Record(sprite);
        }
        TutorialLevelBuilder.Record(body.transform);

        var scorch = new GameObject("Scorch");
        scorch.transform.SetParent(parent);
        scorch.transform.position = new Vector3(lying.x + ((float)dice.NextDouble() - 0.5f) * 1.2f, lying.y - 0.35f, 0f);
        scorch.AddComponent<FloorStain>().kind = FloorStain.Kind.Scorch;
        return body.transform;
    }

    static void PlaceBreak(GameObject player, List<Toilet> toilets, CrewMember attendant, Vector2? comesInAt, ToolCrate crate, ToolShelf shelf,
        Transform level)
    {
        if (toilets.Count == 0)
        {
            Debug.LogWarning($"Chapter 1 builder: there was no room for the stalls in the {RestroomRoom} room, so the break has nowhere to go.");
            return;
        }
        if (attendant == null) Debug.LogWarning($"Chapter 1 builder: there was no room for {Stalls} stalls in the restroom, so nobody asks the technician to fix one.");
        var restroomBreak = new GameObject("Restroom Break").AddComponent<RestroomBreak>();
        restroomBreak.transform.SetParent(level);
        restroomBreak.player = player.GetComponent<PlayerController>();
        restroomBreak.toilet = toilets[toilets.Count - 1];
        restroomBreak.occupiedToilet = toilets.FirstOrDefault(t => t.occupied);
        restroomBreak.attendant = attendant;
        restroomBreak.crate = crate;
        restroomBreak.shelf = shelf;
        if (shelf == null) Debug.LogWarning($"Chapter 1 builder: there was no room for the shelf in the {ToolsRoom} room, so there's no wrench to fetch.");
        if (crate == null) Debug.LogWarning($"Chapter 1 builder: there was no room for the tool crate in the {ToolsRoom} room, so the wrench is straight under the shelf.");
        if (comesInAt.HasValue)
        {
            var outside = new GameObject("Attendant Comes Out").transform;
            outside.SetParent(restroomBreak.transform);
            outside.position = new Vector3(comesInAt.Value.x, comesInAt.Value.y, CharacterZ);
            restroomBreak.attendantComesIn = outside;
        }
        TutorialLevelBuilder.Record(restroomBreak);
    }

    static readonly Dictionary<string, string[][]> FurnitureThoughts = new Dictionary<string, string[][]>
    {
        { "Sofa", new[] { new[] { "Looks comfy... but not now" }, new[] { "There's a dent in this that's exactly the shape of a person" }, new[] { "Real upholstery... somebody fought for that in a budget meeting" } } },
        { "Counter", new[] { new[] { "Coffee machine says 'OUT OF ORDER' and of course it does" }, new[] { "Every mug's labelled, 'NOT YOURS', 'NOT YOURS EITHER'" } } },
        { "Bench", new[] { new[] { "Paperwork... all the way out here and there's still paperwork" }, new[] { "Half-done crossword... seven down is 'reactor', obviously" }, new[] { "Somebody's family photo, taped right to the desk" } } },
        { "Beds", new[] { new[] { "Bunks... cozy, cramped, uhh, cozy" }, new[] { "Someone's growing a little plant by their pillow" }, new[] { "Hospital corners... someone here was military for sure" } } },
        { "Pod", new[] { new[] { "Sleep pod, and somebody stuck a 'DO NOT DISTURB' note on it" }, new[] { "Medical pod, and the readout just says 'READY'... cheerful" }, new[] { "Empty pod... hopefully it stays that way" } } },
        { "Console bank", new[] { new[] { "Station telemetry... reactor output, solar flux, it's all green" }, new[] { "All of this is gonna run through the AI once it's in" }, new[] { "Somebody's been playing solitaire on this one" } } },
        { "Long console bank", new[] { new[] { "Flare forecasts, and somebody's been doing these by hand for years" }, new[] { "The whole station, all on a row of screens" } } },
        { "Wall pipe", new[] { new[] { "Coolant line, and it's warm to the touch" }, new[] { "Someone wrote 'DON'T TOUCH' on it and someone else wrote 'OK' under it" } } },
        { "Railing", new[] { new[] { "Damn, that's a long way down" } } },
    };

    static readonly Dictionary<string, string[][]> FurnitureThoughtsLater = new Dictionary<string, string[][]>
    {
        { "Sofa", new[] { new[] { "Nobody's gonna sit here again" } } },
        { "Counter", new[] { new[] { "The mugs are all still labelled" }, new[] { "Somebody's coffee, still half full" } } },
        { "Bench", new[] { new[] { "The crossword's still half done" }, new[] { "The family photo...", "I hope someone tells them" } } },
        { "Beds", new[] { new[] { "Nobody slept here tonight" } } },
        { "Pod", new[] { new[] { "Dark... everything's on emergency power" } } },
        { "Console bank", new[] { new[] { "Every screen says the same thing, 'ALL SYSTEMS NOMINAL'" }, new[] { "Locked, it doesn't want me reading this" } } },
        { "Long console bank", new[] { new[] { "Flare warning, red, and nobody's answered it" } } },
        { "Wall pipe", new[] { new[] { "Still warm... the station doesn't care what happened" } } },
        { "Railing", new[] { new[] { "Damn, that's a long way down" } } },
    };

    static readonly string[] WindowThoughts = { "The sun's right there... don't look straight at it", "Earth's out there somewhere past it, but it's too small to see" };
    static readonly string[] WindowThoughtsLater = { "The sun looks angrier than it did", "Or maybe that's just me" };

    static readonly Dictionary<char, WallArt.Kind[]> WallArtByRoom = new Dictionary<char, WallArt.Kind[]>
    {
        { 'k', new[] { WallArt.Kind.EarthPhoto, WallArt.Kind.SafetyPoster, WallArt.Kind.Mountains } },
        { 'h', new[] { WallArt.Kind.CompanyPoster, WallArt.Kind.CrewPhoto } },
        { 'b', new[] { WallArt.Kind.KidsDrawing, WallArt.Kind.EarthPhoto } },
    };

    static readonly Dictionary<WallArt.Kind, string[]> WallArtThoughts = new Dictionary<WallArt.Kind, string[]>
    {
        { WallArt.Kind.EarthPhoto, new[] { "Earth... the whole reason we're even up here", "Every watt this place makes goes straight down there" } },
        { WallArt.Kind.Mountains, new[] { "Mountains and a lake... somebody misses home" } },
        { WallArt.Kind.SafetyPoster, new[] { "'SAFETY IS EVERYONE'S JOB.'", "Somebody drew a mustache on the hard hat guy, nice" } },
        { WallArt.Kind.CompanyPoster, new[] { "'KEEPING EARTH LIT'... very inspirational, and very corporate" } },
        { WallArt.Kind.CrewPhoto, new[] { "Crew photo, everyone's squinting and one of them blinked" } },
        { WallArt.Kind.KidsDrawing, new[] { "A kid's drawing, a little stick figure waving at a big yellow sun", "'MY MOM WORKS HERE.'" } },
        { WallArt.Kind.Chart, new[] { "Power output month by month, and it's all up and to the right", "Someone wrote 'NICE' next to it" } },
    };

    static readonly Dictionary<WallArt.Kind, string[]> WallArtThoughtsLater = new Dictionary<WallArt.Kind, string[]>
    {
        { WallArt.Kind.EarthPhoto, new[] { "Earth... they're all still down there waiting on the power", "Waiting on me, I guess" } },
        { WallArt.Kind.Mountains, new[] { "Somebody missed home" } },
        { WallArt.Kind.SafetyPoster, new[] { "'SAFETY IS EVERYONE'S JOB.'" } },
        { WallArt.Kind.CompanyPoster, new[] { "'KEEPING EARTH LIT'... not if that flare hits" } },
        { WallArt.Kind.CrewPhoto, new[] { "The crew photo... I met some of them", "Like, hours ago" } },
        { WallArt.Kind.KidsDrawing, new[] { "'MY MOM WORKS HERE.'", "..." } },
        { WallArt.Kind.Chart, new[] { "Up and to the right... until today" } },
    };

    static void PlaceInspectables(FloorOneBuilder.BuiltFloor floor, bool later)
    {
        Map map = floor.map;
        var dice = new System.Random(Seed + 2);
        Dictionary<string, string[][]> furnitureThoughts = later ? FurnitureThoughtsLater : FurnitureThoughts;
        var dealt = new Dictionary<string, int>();

        Transform furniture = floor.level.Find("Furniture");
        if (furniture != null)
        {
            foreach (Transform prop in furniture)
            {
                if (!furnitureThoughts.TryGetValue(prop.name, out string[][] takes)) continue;
                dealt.TryGetValue(prop.name, out int next);
                dealt[prop.name] = next + 1;
                var look = prop.gameObject.AddComponent<Inspectable>();
                look.thoughts = takes[next % takes.Length];
                look.range = 0.9f;
                var bounds = new Bounds(prop.position, Vector3.zero);
                foreach (SpriteRenderer tile in prop.GetComponentsInChildren<SpriteRenderer>())
                    if (tile.name == "Tile") bounds.Encapsulate(tile.bounds);
                look.tagOffset = new Vector2(bounds.center.x - prop.position.x, bounds.max.y - prop.position.y + 0.3f);
            }
        }

        Transform group = TutorialLevelBuilder.Group("Things to Look At", floor.level);
        var lamps = floor.level.GetComponentsInChildren<StationLight>().Select(l => (Vector2)l.transform.position).ToList();
        foreach (Room room in floor.rooms)
        {
            var windows = new List<int>();
            var plain = new List<int>();
            for (int x = room.minX; x <= room.maxX; x++)
            {
                if (!room.cells.Contains(new Vector2Int(x, room.minRow))) continue;
                if (!Map.IsFace(map.At(x, room.minRow - 1))) continue;
                bool window = Enumerable.Range(1, 3).Any(up => map.At(x, room.minRow - up) == '+');
                if (window) windows.Add(x);
                else if (!FloorOneBuilder.IsKeptClear(map.At(x, room.minRow)) && Enumerable.Range(1, 3).All(up => Map.IsFace(map.At(x, room.minRow - up)))
                         && !lamps.Any(l => Mathf.Abs(l.x - (x + 0.5f + map.Origin.x)) < 1.5f && Mathf.Abs(l.y - map.Center(x, room.minRow - 2).y) < 3f))
                    plain.Add(x);
            }

            if (windows.Count > 0)
            {
                int first = windows[0], last = first;
                while (windows.Contains(last + 1)) last++;
                int middle = (first + last) / 2;
                var window = new GameObject("Window");
                window.transform.SetParent(group);
                window.transform.position = map.Center(middle, room.minRow);
                var look = window.AddComponent<Inspectable>();
                look.thoughts = later ? WindowThoughtsLater : WindowThoughts;
                look.range = 1.1f;
                look.tagOffset = new Vector2(0f, 1.3f);
            }

            if (!WallArtByRoom.TryGetValue(room.marker, out WallArt.Kind[] kinds) || plain.Count == 0) continue;
            for (int i = 0; i < kinds.Length; i++)
            {
                int x = plain[Mathf.Clamp((int)((i + 0.5f) * plain.Count / kinds.Length), 0, plain.Count - 1)];
                Vector2 on = map.Center(x, room.minRow - 2);
                var picture = new GameObject($"Wall Art ({kinds[i]})");
                picture.transform.SetParent(group);
                picture.transform.position = on;
                var art = picture.AddComponent<WallArt>();
                art.kind = kinds[i];
                art.damaged = later && dice.NextDouble() < 0.5;
                var look = picture.AddComponent<Inspectable>();
                look.thoughts = (later ? WallArtThoughtsLater : WallArtThoughts)[kinds[i]];
                look.focus = map.Center(x, room.minRow) - on;
                look.range = 1.1f;
                look.tagOffset = new Vector2(0f, 0.7f);
                plain.RemoveAll(taken => Mathf.Abs(taken - x) < 2);
                if (plain.Count == 0) break;
            }
        }
    }

    static int botsPlaced;
    static void PlaceBot(Vector2 at, CrewWalkArea area, Transform parent)
    {
        var kinds = new[] { MaintenanceBot.Kind.Sweeper, MaintenanceBot.Kind.Hauler, MaintenanceBot.Kind.Scanner };
        int number = ++botsPlaced;
        var bot = new GameObject($"Maintenance Bot MX-{number:00}");
        bot.transform.SetParent(parent);
        bot.transform.position = new Vector3(at.x, at.y, CharacterZ);
        var body = bot.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var feet = bot.AddComponent<BoxCollider2D>();
        feet.size = new Vector2(0.7f, 0.32f);
        feet.offset = new Vector2(0f, 0.16f - DepthSort.FeetToCenter);
        var unit = bot.AddComponent<MaintenanceBot>();
        unit.kind = kinds[(number - 1) % kinds.Length];
        unit.unitName = $"MX-{number:00}";
        unit.area = area;
    }

    static bool TryGreeterSpot(HashSet<Vector2Int> free, Vector2Int start, out Vector2Int spot)
    {
        spot = default;
        var near = free.Where(c => Chebyshev(c, start) >= 3 && Chebyshev(c, start) <= 4).ToList();
        if (near.Count == 0) return false;
        spot = near.OrderByDescending(c => c.x - start.x).ThenBy(c => Mathf.Abs(c.y - start.y)).First();
        return true;
    }

    static bool TryPlaceGroup(Map map, HashSet<Vector2Int> free, int size, System.Random random, out Vector2Int middle, out Vector2[] ring)
    {
        List<Vector2Int> candidates = free.OrderBy(_ => random.Next()).ToList();
        float first = size == 2 ? 0f : size == 4 ? 45f : 90f;
        foreach (Vector2Int cell in candidates)
        {
            Vector2 center = StandingAt(map, cell);
            var spots = new Vector2[size];
            bool fits = true;
            for (int i = 0; i < size && fits; i++)
            {
                float angle = (first + i * 360f / size) * Mathf.Deg2Rad;
                spots[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RingRadius;
                fits = free.Contains(FeetCell(map, spots[i]));
            }
            if (!fits) continue;
            middle = cell;
            ring = spots;
            return true;
        }
        middle = default;
        ring = null;
        return false;
    }

    static bool TryStandingSpot(Map map, HashSet<Vector2Int> furniture, HashSet<Vector2Int> seats, HashSet<Vector2Int> free, bool atConsoles,
        System.Random random, out Vector2Int cell, out Vector2 facing)
    {
        List<Vector2Int> shuffled = free.OrderBy(_ => random.Next()).ToList();
        if (atConsoles)
        {
            foreach (Vector2Int c in shuffled)
            {
                if (!furniture.Contains(c + Vector2Int.down) || seats.Contains(c + Vector2Int.down)) continue;
                cell = c;
                facing = Vector2.up;
                return true;
            }
        }
        cell = shuffled.FirstOrDefault();
        facing = Quaternion.Euler(0f, 0f, random.Next(0, 8) * 45f) * Vector2.down;
        return shuffled.Count > 0;
    }

    static CrewWalkArea MakeWalkArea(Map map, Room room, HashSet<Vector2Int> walkable, Transform parent)
    {
        var area = new GameObject("Walk Area").AddComponent<CrewWalkArea>();
        area.transform.SetParent(parent);
        Rect bounds = map.WorldRect(room.minX, room.maxX, room.minRow, room.maxRow);
        area.transform.position = bounds.center;
        area.origin = bounds.min;
        area.standHeight = DepthDressing.FeetLift;
        area.width = room.Width;
        area.height = room.Height;
        area.walkable = new bool[area.width * area.height];
        foreach (Vector2Int cell in walkable)
        {
            int x = cell.x - room.minX;
            int y = room.maxRow - cell.y;
            area.walkable[y * area.width + x] = true;
        }
        return area;
    }

    static void PlaceDirector(Map map, GameObject player, CrewMember greeter, Transform level)
    {
        var director = new GameObject("Chapter 1 Director").AddComponent<ChapterOneDirector>();
        director.player = player.GetComponent<PlayerController>();
        director.greeter = greeter;

        Vector2Int start = map.Find('P')[0];
        List<Vector2Int> entrance = map.Find('X').Where(c => c.y == start.y).ToList();
        if (entrance.Count == 0) return;
        Vector2Int door = entrance.OrderBy(c => Mathf.Abs(c.x - start.x)).First();
        var inside = new Vector2Int(door.x + System.Math.Sign(start.x - door.x), start.y);

        var target = new GameObject("Walk In Target").transform;
        target.SetParent(director.transform);
        target.position = StandingAt(map, start);
        director.walkInTarget = target;

        Vector2 from = StandingAt(map, inside);
        player.transform.position = new Vector3(from.x, from.y, player.transform.position.z);
        TutorialLevelBuilder.Record(player.transform);
    }

    internal static void Unarm(GameObject player)
    {
        if (player.TryGetComponent(out PlayerCombat combat))
        {
            combat.startingWeapon = null;
            TutorialLevelBuilder.Record(combat);
        }
        if (player.TryGetComponent(out WeaponHotbar hotbar))
        {
            hotbar.slots = new List<WeaponData>();
            TutorialLevelBuilder.Record(hotbar);
        }
    }

    static CrewMember Spawn(GameObject prefab, Transform parent, Vector2 at, string label, Color trim, CrewMember.Role role, Vector2 facing)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = $"Crew ({label})";
        instance.transform.position = new Vector3(at.x, at.y, CharacterZ);
        TutorialLevelBuilder.Record(instance.transform);

        var crew = instance.GetComponent<CrewMember>();
        crew.role = role;
        crew.trimColor = trim;
        crew.facing = PlayerController.SnapToEightWay(facing);
        TutorialLevelBuilder.Record(crew);
        return crew;
    }

    static Color Pick(System.Random random) => TrimColors[random.Next(TrimColors.Length)];

    static Vector2 StandingAt(Map map, Vector2Int cell) => map.Center(cell.x, cell.y) + new Vector2(0f, DepthDressing.FeetLift);

    static Vector2 Toward(Vector2Int from, Vector2Int to) => new Vector2(to.x - from.x, from.y - to.y);

    static Vector2Int FeetCell(Map map, Vector2 standing) => CellAt(map, standing - new Vector2(0f, DepthDressing.FeetLift));

    static Vector2Int CellAt(Map map, Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x), map.Height - 1 - Mathf.FloorToInt(world.y));

    static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    static bool NearKeptClear(Map map, Vector2Int cell)
    {
        foreach (Vector2Int step in Around)
            if (FloorOneBuilder.IsKeptClear(map.At(cell.x + step.x, cell.y + step.y))) return true;
        return false;
    }

    static void AddSceneToBuild()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
    }
}
