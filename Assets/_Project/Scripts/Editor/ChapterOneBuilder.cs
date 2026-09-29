using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Map = TutorialLevelBuilder.Map;
using Room = FloorOneBuilder.Room;

// Menu: Sonny > Build Chapter 1
//
// Builds Scenes/Levels/Chapter1.unity: the whole station exactly as FloorOneBuilder lays it out (both floors, the same
// rooms, doorways, furniture, and bright, ordinary light), with the station as it was before anything went wrong: full
// of crew. The chapter all happens on floor 1, so its stairs lead nowhere. The technician comes aboard through the ship entrance with nothing in hand (no weapons, empty hotbar), a
// crew member by the door says hello and points them to the control room (ChapterOneDirector), and everywhere else
// people are walking about, standing around, and talking in little groups. In the control room, Sonny's processing box
// waits at the install point (S), dark, with the station chief beside it, for the install (ControlRoomCutscene). The
// restroom is its own little room off the kitchen and lounge (w in the layout), with a row of stalls along its back wall:
// the one nearest the door out of order, the next one occupied (a crew member's in it, who comes out and asks the
// technician to fix it once it breaks on them), and a free one at the end (RestroomBreak). They send the technician to
// the storage room, with a tool crate and a shelf, where Chapter 1 ends. Edit the layout, the crowds
// below, or the crew prefab, and run this again. It replaces everything in the scene but the Sound Manager.
//
// Chapter 2 (ChapterTwoBuilder) is filled in here too, from the same crowds placed the same way, so everything in it is
// where Chapter 1 left it: the crew lie where they stood (CrewAftermath), the toilets are broken, the crate's open, the
// shelf's knocked about, and the storage room door's jammed, with the vents out (StorageEscape).
//
// Everyone in the crew is the technician's own art for now, with the red trim swapped for a color of their own
// (CrewMember, the Sonny/Crew Sprite shader), so they're told apart from the player and each other until they get art
// of their own. The prefab (Prefabs/Characters/CrewMember) is made the first time this runs; after that only how it stands
// on the floor (its footprint and shadow) is set again, so give it new art or a new controller there and every crew
// member picks it up on the next build.
//
// Who's where is laid out per room (Crowds), and placed the same way every build: groups first, in rings of two to
// four; then the people standing on their own, at a console if the room has them; then the people walking about, who
// get the room's floor as a grid to find their way over (CrewWalkArea), minus the furniture, the doorways, and everyone
// standing still. Nobody stands in front of a doorway, the stairs, or right where the player comes in. Maintenance bots
// (MaintenanceBot) go about their rounds over the same floor as the people walking about.
//
// Things to look at (Inspectable, Q): the furniture, the windows, and pictures and posters hung on the walls of the
// rooms people live in (WallArt), each with what the technician thinks of it, in Chapter 1 and, darker, in Chapter 2.
public static class ChapterOneBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Chapter1.unity";
    const string CrewPrefabPath = "Assets/_Project/Prefabs/Characters/CrewMember.prefab";
    const string CrewMaterialPath = "Assets/_Project/Art/Shaders/CrewSprite.mat";
    const string CrewShader = "Sonny/Crew Sprite";
    const float CharacterZ = 1f;
    const float RingRadius = 1f;        // from the middle of a group to each of them
    const int Seed = 1026;              // the same crowd every build; change it to shuffle everyone

    // How many are in a room: walking about, standing on their own, and talking in groups of so many, and how many
    // maintenance bots are on their rounds there. atConsoles puts the ones standing on their own at the desks and
    // consoles, facing them, as if working.
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
        { 'h', new Crowd { walking = 5, standing = 2, groups = new[] { 2, 3 }, bots = 1 } },                      // common grounds hallway, where the player comes in
        { 'k', new Crowd { walking = 4, standing = 3, groups = new[] { 3, 2, 4 }, atConsoles = true, bots = 2 } }, // kitchen and lounge
        { 'b', new Crowd { walking = 2, standing = 2, groups = new[] { 2 } } },                                   // bedrooms
        { 'v', new Crowd { walking = 2, bots = 1 } },                                                          // east hallway
        { 'm', new Crowd { walking = 3, standing = 4, groups = new[] { 2, 3 }, atConsoles = true, bots = 3 } },   // maintenance deck
        { 'c', new Crowd { walking = 1, standing = 1 } },                                                      // hallway to the control room
        { 'o', new Crowd { walking = 2, standing = 6, groups = new[] { 2 }, atConsoles = true, bots = 1 } },      // control room
        // The storage room stays empty: it's where the player ends up alone.
    };

    // Everyone's trim, anything but the player's red.
    static readonly Color[] TrimColors =
    {
        new Color(0.22f, 0.48f, 0.95f),     // blue
        new Color(0.26f, 0.72f, 0.36f),     // green
        new Color(0.95f, 0.78f, 0.22f),     // yellow
        new Color(0.6f, 0.36f, 0.86f),      // purple
        new Color(0.2f, 0.74f, 0.78f),      // teal
        new Color(0.9f, 0.42f, 0.66f),      // pink
        new Color(0.42f, 0.45f, 0.5f),      // grey
    };

    // What the groups talk about, one each, handed out in turn. Each line is said by the next one round the ring. Keep
    // at least as many as there are groups in Crowds, so no two groups have the same conversation.
    static readonly string[][] Conversations =
    {
        new[] { "So the new AI goes in today?", "Supposedly. Smartest thing anyone's ever built.", "Smarter than the last one, anyway.", "Low bar." },
        new[] { "Flare season's starting early.", "Station's rated for worse.", "That's what the numbers say." },
        new[] { "Anyone fixed the toilet on this deck yet?", "Don't. Just don't.", "I'll take that as a no." },
        new[] { "Did you see the new tech? No badge.", "Theirs is coming later, apparently.", "Typical." },
        new[] { "Three weeks till rotation.", "Not that you're counting.", "Nineteen days. Not counting." },
        new[] { "Once the AI's running things, what do we even do?", "Sleep, hopefully.", "Let the machine keep watch for once." },
        new[] { "Reactor output's up again.", "Good. Earth's going to need it.", "Earth always needs it." },
        new[] { "Did you hear the reactor last night?", "It always hums.", "Louder, I mean." },
        new[] { "Who's on the late shift?", "Me. Again.", "You volunteered.", "I lost a bet. It's different." },
        new[] { "Supply run brought oranges.", "Real ones?", "Real ones. Hide yours." },
        new[] { "The maintenance bots keep lining up by the reactor door.", "Waiting for orders, probably.", "Creepy, though." },
        new[] { "Think the AI will talk to us?", "It'll talk. Whether it listens is another thing." },
    };

    // The crew member waiting by the entrance, as a DialogueBox script: the technician picks their answers (the * lines).
    // They switch on Pip, the helper in the technician's suit, near the end; Pip boots once the talk's over
    // (ChapterOneDirector). The last line is what they say again if the player comes back, so keep it plain.
    static readonly string[] Greeting =
    {
        "Hey! You must be the new technician.",
        "* That's me. Here to install the AI. = So you're the one everybody's been waiting on. No pressure.",
        "* Depends who's asking. = The quartermaster. I sign your supply forms, so be nice.",
        "* (Just nod.) = Quiet type. You'll fit right in up here.",
        "No badge? Yours is coming later. Supply's always slow.",
        "* Will the doors let me through without one? = Most will, if someone with a badge is close by. Stick with the crew.",
        "* I'll manage. = That's the spirit.",
        "Oh, before you go. Every suit up here has a little helper built in. Yours won't be switched on yet.",
        "Hold your arm out. There. Give it a second to wake up. It'll show you around better than I can.",
        "They're waiting on you in the control room. East hallway, then across the maintenance deck.",
    };

    // The station chief, waiting by the install point in the control room (ControlRoomCutscene).
    const string ChiefName = "Voss";
    const string ChiefJob = "Station Chief";
    static readonly Color ChiefTrim = new Color(0.93f, 0.93f, 0.96f);
    // Sonny's lens before anything's wrong: a friendly blue.
    static readonly Color SonnyLens = new Color(0.35f, 0.82f, 1f);

    // The restroom (its own room, off the kitchen and lounge), with this many stalls in a row, and the crew member in one.
    const char RestroomRoom = 'w';
    const int Stalls = 3;
    const char ToolsRoom = 's';     // the storage room, where the wrench is

    // The air ducts from the storage room (grate 1) to the restroom (grate 2), the way out once the door's jammed
    // (StorageEscape). Crawled in first person (VentNetwork, which has the key to the map): . duct, d a dented panel that
    // bangs, s a tight squeeze, r the hatch a drone comes out of if they make too much noise. Two ways round the middle.
    const string VentLayout =
        "###############\n" +
        "#2...d...#....#\n" +
        "#.######.#.##.#\n" +
        "#s#....#.#.#..#\n" +
        "#.#.##.#...#.##\n" +
        "#...##.#####.##\n" +
        "####...r.....##\n" +
        "#....#######..#\n" +
        "#..d.....d...1#\n" +
        "###############";
    // Chapter 2's common grounds: how many of the crew lie where they fell in each room, besides the ones the technician
    // met (Okafor by the restroom door, the quartermaster by the entrance), who lie where they stood (CrewAftermath).
    static readonly Dictionary<char, int> BodiesPerRoom = new Dictionary<char, int> { { 'k', 1 }, { 'h', 1 }, { 'b', 2 } };
    static readonly string[] OkaforFound =
    {
        "~Tech... that's Okafor. From the restroom.",
        "~No vitals. Okafor's gone.",
    };
    // The badge doors: the doorways into these rooms only open for someone with this credential, or with a crew member
    // who has a badge close by (Teleporter.credential). A guard stands by each one on the outside, to badge people
    // through, so the technician gets in in Chapter 1; in Chapter 2 there's nobody left to (BadgeLockout).
    static readonly Dictionary<char, string> BadgeRooms = new Dictionary<char, string> { { 'o', "control_room" } };
    const string GuardName = "Reyes";
    const string GuardJob = "Security";
    static readonly string[] GuardLines =
    {
        "Control room's badge-only. You're the new tech? Go on, I'll badge you through.",
        "Anywhere there's a reader, just stick close to one of us and the door'll open.",
    };
    static readonly string[] GuardFound =
    {
        "~That's Reyes. Security. The one who badged you in.",
        "~Right in front of the door they were guarding.",
    };

    // The badge door into the control room, once it's built, for Chapter 2 (ChapterTwoBuilder).
    internal static Teleporter ControlRoomDoor { get; private set; }

    static readonly string[] QuartermasterFound =
    {
        "~That's the quartermaster. The one who switched me on.",
        "~...They said your badge was coming later.",
    };

    const string VentTilePath = "Assets/_Project/Art/Environment/Tilesets/ShipTiles/tileset_29.asset";     // a dark floor grate
    const int IgnoreRaycastLayer = 2;   // so a grate never blocks a robot's line of sight
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

    // For batch mode: Unity -batchmode -projectPath <project> -executeMethod ChapterOneBuilder.BuildFromCommandLine
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

    // --- The crew prefab ---

    // The player's sprite and Animator on a kinematic body with a CrewMember. Made if it isn't there. Either way, its
    // material, footprint, and shadow are set again each build, since the builders own how characters stand on the
    // floor (DepthDressing); its art, controller, and CrewMember settings are left as they are.
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
            // Tinted as the player is, so the same light falls the same way on everyone.
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

    // --- Chapter 2 ---

    // Where the technician lies in Chapter 2: where they were knocked out, in front of the shelf.
    internal static Vector2? WakeSpot { get; private set; }

    // Fills in Chapter 2's floor 1 (see the top). Returns false if the crew prefab can't be made.
    internal static bool PopulateChapterTwo(FloorOneBuilder.BuiltFloor[] floors)
    {
        GameObject playerPrefab = TutorialLevelBuilder.LoadPrefab("Characters/Player 1");
        if (playerPrefab == null) return false;
        GameObject crewPrefab = CrewPrefab(playerPrefab);
        if (crewPrefab == null) return false;
        Populate(floors, crewPrefab, true);
        return true;
    }

    // --- Filling the floor ---

    // Returns how many crew went in: alive, in Chapter 1, and none in Chapter 2, where only the bodies are left.
    static int Populate(FloorOneBuilder.BuiltFloor[] floors, GameObject crewPrefab, bool chapterTwo)
    {
        FloorOneBuilder.BuiltFloor floor = floors[0];
        // Chapter 1 all happens on this floor: the stairs are there, but lead nowhere yet.
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
        var bodyDice = new System.Random(Seed + 1);     // its own, so where the bodies go doesn't move the living crew
        var bodies = new List<(Vector2 at, Color trim, string[] lines)>();
        Transform crewRoot = TutorialLevelBuilder.Group("Crew", floor.level);
        Vector2Int start = map.Find('P')[0];
        CrewMember greeter = null, chief = null, attendant = null;
        var toilets = new List<Toilet>();     // nearest the door first: out of order, occupied, free
        ToolShelf shelf = null;
        ToolCrate crate = null;
        List<Vector2Int> installPoints = map.Find('S');
        Vector2Int install = installPoints.Count > 0 ? installPoints[0] : new Vector2Int(-100, -100);
        int conversation = 0, count = 0;
        botsPlaced = 0;
        // The restroom's doorways.
        Room restroom = floor.rooms.FirstOrDefault(r => r.marker == RestroomRoom);
        List<Vector2Int> restroomDoors = restroom == null ? new List<Vector2Int>()
            : restroom.cells.Where(c => char.IsDigit(map.At(c.x, c.y))).ToList();
        // The digits on the doorways into the badge rooms, so the guards can be put on the other side of them.
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

            // Walkable: open floor with no furniture on it (doorways, stairs, and the start aren't open floor).
            // Free to stand on: walkable, and not beside anything that has to be kept clear.
            var walkable = new HashSet<Vector2Int>(room.cells.Where(c => FloorOneBuilder.IsOpenFloor(map.At(c.x, c.y)) && !floor.furniture.Contains(c)));
            var free = new HashSet<Vector2Int>(walkable.Where(c => !NearKeptClear(map, c) && Chebyshev(c, start) > 2));

            // Taking a spot to stand in: nobody walks through it, and nobody else stands right beside it.
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

            // The chief stands just off to the side of the install point, and nobody else stands or walks where the
            // technician will work, just below it.
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

            // The restroom: the stalls in a row along the back wall, as far from the door as they go. The one nearest the
            // door has been out of order all week; the next has someone in it, who comes out to the spot in front of it
            // once it breaks; the last is free.
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
                            Take(stall + new Vector2Int(dx, 1));    // map rows count down, so this is in front of it
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
                        attendant.gameObject.SetActive(false);      // in the stall, until it breaks on them
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

            // The shelf with the wrench under it, at the back of the storage room, and the tool crate out on the floor.
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

            // A guard outside each badge door, a couple of steps from it, looking out the way people come.
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
                // The floor inside the ring is taken: nobody walks into the middle of a conversation, or squeezes
                // between two of the people in it. At the height of their feet, like their own footprints.
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
                if (!TryStandingSpot(map, floor.furniture, free, crowd.atConsoles, random, out Vector2Int cell, out Vector2 facing))
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
                // Each starts somewhere of their own, not on top of anyone.
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

            // Where the bodies will be, a few hours later, on free floor.
            if (BodiesPerRoom.TryGetValue(room.marker, out int lying))
                foreach (Vector2Int cell in free.OrderBy(_ => bodyDice.Next()).Take(lying).ToList())
                {
                    bodies.Add((StandingAt(map, cell), Pick(bodyDice), new string[0]));
                    Take(cell);
                }
        }
        PlaceInspectables(floor, chapterTwo);
        if (attendant != null) bodies.Insert(0, (attendant.transform.position, attendant.trimColor, OkaforFound));
        if (greeter != null) bodies.Insert(0, (greeter.transform.position, greeter.trimColor, QuartermasterFound));
        foreach (CrewMember guard in guards) bodies.Add((guard.transform.position, guard.trimColor, GuardFound));
        SetBadgeDoors(floor);

        if (!chapterTwo)
        {
            PlaceDirector(map, floor.player, greeter, floor.level);
            PlaceInstall(map, floor.player, chief, greeter, guards, floor.level);
            PlaceBreak(floor.player, toilets, attendant, comesInAt, crate, shelf, floor.level);
            return count;
        }

        // A few hours later: the crew placed above were only there to say where everyone was. The toilets, the crate, and the
        // shelf stay, as Chapter 1 left them.
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
        return 0;
    }

    // Beside the install point and a step down, off to the left if there's room: never right in front of the box.
    static bool TryChiefSpot(HashSet<Vector2Int> free, Vector2Int install, out Vector2Int spot)
    {
        spot = default;
        var wanted = install + new Vector2Int(-2, 1);
        var near = free.Where(c => Chebyshev(c, install) == 2 && c.y >= install.y && c.x != install.x).ToList();
        if (near.Count == 0) return false;
        spot = near.OrderBy(c => (c - wanted).sqrMagnitude).First();
        return true;
    }

    // Sonny's box at the install point, dark until it's installed, and the cutscene that installs it. The greeter and the
    // guards have their own lines once it's on.
    static void PlaceInstall(Map map, GameObject player, CrewMember chief, CrewMember greeter, List<CrewMember> guards, Transform level)
    {
        SonnyBox sonny = TutorialLevelBuilder.PlaceSonny(map, level);
        if (sonny == null)
        {
            Debug.LogWarning("Chapter 1 builder: there's no S in the layout, so there's nothing to install in the control room.");
            return;
        }
        sonny.startPowered = false;
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

    // For the shelf: against the top wall, as far from the doors as there's open floor three cells wide
    // with nothing in front of it.
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

    // A row of stalls against the top wall, as far from the doors as there's open floor for them all, side by side, with
    // nothing in front: each stall is two cells wide, standing on every other cell of the row. Nearest the doors first.
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

    // Out on the floor, clear all round, a few steps from the doors and the shelf.
    static bool TryCrateSpot(HashSet<Vector2Int> free, List<Vector2Int> away, out Vector2Int spot)
    {
        spot = default;
        var roomy = free.Where(c => Around.All(step => free.Contains(c + step))).ToList();
        if (roomy.Count == 0) return false;
        spot = roomy.OrderByDescending(c => away.Count == 0 ? 0 : Mathf.Min(4, away.Min(a => Chebyshev(c, a))))
            .ThenBy(c => c.y).ThenByDescending(c => c.x).First();
        return true;
    }

    // Free floor as far as it gets from all of these, for a vent in the floor.
    static bool TryFarFrom(HashSet<Vector2Int> free, List<Vector2Int> away, out Vector2Int spot)
    {
        spot = default;
        if (free.Count == 0) return false;
        spot = free.OrderByDescending(c => away.Count == 0 ? 0 : away.Min(a => Chebyshev(c, a))).ThenBy(c => c.x).ThenBy(c => c.y).First();
        return true;
    }

    // A couple of steps from a doorway, as close as there's free floor.
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

    // The stall stands on the cell with its back to the wall, sorted like the furniture from FeetToCenter above its base.
    static Toilet PlaceToilet(Map map, Vector2Int cell, Transform parent)
    {
        Vector2 center = map.Center(cell.x, cell.y);
        var stall = new GameObject("Toilet Stall");
        stall.transform.SetParent(parent);
        stall.transform.position = new Vector3(center.x, center.y - 0.3f + DepthSort.FeetToCenter, CharacterZ);
        var footprint = stall.AddComponent<BoxCollider2D>();
        footprint.size = new Vector2(1.95f, 0.6f);     // side to side, so the stalls in a row meet
        footprint.offset = new Vector2(0f, 0.3f - DepthSort.FeetToCenter);
        var toilet = stall.AddComponent<Toilet>();
        TutorialLevelBuilder.Record(toilet);
        return toilet;
    }

    // The storage room's rack, placed like the stall: sorted from FeetToCenter above its base.
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

    // The tool crate, placed like the shelf.
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

    // --- Badge doors ---

    // Every doorway into a badge room, from outside, needs its credential. Leaving is never stopped.
    static void SetBadgeDoors(FloorOneBuilder.BuiltFloor floor)
    {
        ControlRoomDoor = null;
        foreach (Room room in floor.rooms)
        {
            if (!BadgeRooms.TryGetValue(room.marker, out string credential)) continue;
            Rect inside = floor.map.WorldRect(room.minX, room.maxX, room.minRow, room.maxRow);
            foreach (Teleporter door in floor.level.GetComponentsInChildren<Teleporter>())
            {
                // A door that comes out in the room, from outside it.
                if (door.teleportTarget == null || !inside.Contains(door.teleportTarget.position) || inside.Contains(door.transform.position)) continue;
                door.credential = credential;
                TutorialLevelBuilder.Record(door);
                if (room.marker == 'o') ControlRoomDoor = door;
            }
        }
    }

    // --- The way out of the storage room ---

    // The ducts and their two grates, the storage room door (both ends, jammed), the rubble on the lounge side of it, and
    // what ties them together (StorageEscape).
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

        // The door: every doorway out of the storage room, at both ends.
        var digits = new HashSet<char>(storage.cells.Select(c => map.At(c.x, c.y)).Where(char.IsDigit));
        escape.jammedDoors = floor.level.GetComponentsInChildren<Teleporter>()
            .Where(t => digits.Any(d => t.name.StartsWith($"Door {d} "))).ToArray();
        if (escape.jammedDoors.Length == 0) Debug.LogWarning("Chapter 1 builder: couldn't find the storage room door to jam.");

        // The rubble: a solid pile on each cell of the doorway on the other side, and loose wreckage just in front of it.
        Transform piles = TutorialLevelBuilder.Group("Rubble Outside Storage", escape.transform);
        var rubble = new List<GameObject>();
        var covered = new HashSet<Vector2Int>();
        foreach (Room other in floor.rooms.Where(r => r != storage))
        {
            foreach (Vector2Int door in other.cells.Where(c => digits.Contains(map.At(c.x, c.y))))
            {
                if (covered.Add(door)) rubble.Add(PlaceRubble(map, door, true, piles));
                foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    Vector2Int front = door + step;
                    if (other.cells.Contains(front) && FloorOneBuilder.IsOpenFloor(map.At(front.x, front.y)) && !floor.furniture.Contains(front)
                        && covered.Add(front))
                        rubble.Add(PlaceRubble(map, front, false, piles));
                }
            }
        }
        escape.rubble = rubble.ToArray();
        TutorialLevelBuilder.Record(escape);
    }

    // A grate in the floor, off until the wake-up. Climbing out, the player steps off it toward the middle of the room.
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
        // The tile's pivot is its corner; the grate's position is its middle.
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

    // --- A few hours later ---

    // The crew where they fell, and what has Pip find them (CrewAftermath).
    static void PlaceAftermath(FloorOneBuilder.BuiltFloor floor, GameObject crewPrefab, List<(Vector2 at, Color trim, string[] lines)> bodies,
        System.Random dice)
    {
        var aftermath = new GameObject("Crew Aftermath").AddComponent<CrewAftermath>();
        aftermath.transform.SetParent(floor.level);
        aftermath.player = floor.player.GetComponent<PlayerController>();
        Transform root = TutorialLevelBuilder.Group("Crew Bodies", aftermath.transform);

        var found = new List<CrewAftermath.Body>();
        foreach ((Vector2 at, Color trim, string[] lines) in bodies)
            found.Add(new CrewAftermath.Body { body = MakeBody(crewPrefab, root, at, trim, dice), pipLines = lines });
        aftermath.bodies = found.ToArray();
        TutorialLevelBuilder.Record(aftermath);
    }

    // One of the crew lying on their side, still, a little darker, with a scorch mark on the floor beside them. Nothing
    // on them moves or can be talked to.
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

    // The break after the install, the restroom it sends the technician to (the stalls, nearest the door first), and the
    // crate and shelf in the storage room they look for a wrench in.
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

    // --- Things to look at ---

    // What the technician thinks of each kind of furniture (by its name, FloorOneBuilder's stamps), a few takes on each,
    // dealt out so two sofas side by side don't say the same thing. Each take is the thoughts for one look after another.
    static readonly Dictionary<string, string[][]> FurnitureThoughts = new Dictionary<string, string[][]>
    {
        { "Sofa", new[] { new[] { "Looks comfy. Not now, though." }, new[] { "There's a dent in this exactly the shape of a person." }, new[] { "Real upholstery. Somebody fought for that in a budget meeting." } } },
        { "Counter", new[] { new[] { "Coffee machine. 'OUT OF ORDER.' Of course." }, new[] { "Every mug's labelled. 'NOT YOURS.' 'NOT YOURS EITHER.'" } } },
        { "Bench", new[] { new[] { "Paperwork. All the way out here, and still paperwork." }, new[] { "A half-done crossword. Seven down is 'reactor'." }, new[] { "Somebody's family photo, taped to the desk." } } },
        { "Beds", new[] { new[] { "Bunks. Cozy. Cramped. Cozy." }, new[] { "Someone's growing a little plant by their pillow." }, new[] { "Hospital corners. Someone here was military." } } },
        { "Pod", new[] { new[] { "Sleep pod. Somebody stuck a 'DO NOT DISTURB' note on it." }, new[] { "Medical pod. The readout says 'READY'. Cheerful." }, new[] { "Empty pod. Hopefully it stays that way." } } },
        { "Console bank", new[] { new[] { "Station telemetry. Reactor output, solar flux... all green." }, new[] { "All of this'll run through the AI once it's in." }, new[] { "Somebody's been playing solitaire on this one." } } },
        { "Long console bank", new[] { new[] { "Flare forecasts. Somebody's done these by hand for years." }, new[] { "The whole station, on a row of screens." } } },
        { "Wall pipe", new[] { new[] { "Coolant line. Warm to the touch." }, new[] { "Someone's written 'DON'T TOUCH' on it. Someone else wrote 'OK'." } } },
        { "Railing", new[] { new[] { "Long way down." } } },
    };

    static readonly Dictionary<string, string[][]> FurnitureThoughtsLater = new Dictionary<string, string[][]>
    {
        { "Sofa", new[] { new[] { "Nobody's going to sit here again." } } },
        { "Counter", new[] { new[] { "The mugs are all still labelled." }, new[] { "Somebody's coffee. Still half full." } } },
        { "Bench", new[] { new[] { "The crossword's still half done." }, new[] { "The family photo.", "I hope someone tells them." } } },
        { "Beds", new[] { new[] { "Nobody slept here tonight." } } },
        { "Pod", new[] { new[] { "Dark. Everything's on emergency power." } } },
        { "Console bank", new[] { new[] { "Every screen says the same thing. 'ALL SYSTEMS NOMINAL.'" }, new[] { "Locked. It doesn't want me reading this." } } },
        { "Long console bank", new[] { new[] { "Flare warning. Red. Nobody's answered it." } } },
        { "Wall pipe", new[] { new[] { "Still warm. The station doesn't care what happened." } } },
        { "Railing", new[] { new[] { "Long way down." } } },
    };

    static readonly string[] WindowThoughts = { "The sun. Right there. Don't look straight at it.", "Earth's out there somewhere past it. Too small to see." };
    static readonly string[] WindowThoughtsLater = { "The sun looks angrier than it did.", "Or maybe that's just me." };

    // The pictures on the walls, by room, and what's thought of each then and later.
    static readonly Dictionary<char, WallArt.Kind[]> WallArtByRoom = new Dictionary<char, WallArt.Kind[]>
    {
        { 'k', new[] { WallArt.Kind.EarthPhoto, WallArt.Kind.SafetyPoster, WallArt.Kind.Mountains } },
        { 'h', new[] { WallArt.Kind.CompanyPoster, WallArt.Kind.CrewPhoto } },
        { 'b', new[] { WallArt.Kind.KidsDrawing, WallArt.Kind.EarthPhoto } },
        { 'o', new[] { WallArt.Kind.Chart } },
    };

    static readonly Dictionary<WallArt.Kind, string[]> WallArtThoughts = new Dictionary<WallArt.Kind, string[]>
    {
        { WallArt.Kind.EarthPhoto, new[] { "Earth. The whole reason we're up here.", "Every watt this station makes goes down there." } },
        { WallArt.Kind.Mountains, new[] { "Mountains and a lake. Somebody misses home." } },
        { WallArt.Kind.SafetyPoster, new[] { "'SAFETY IS EVERYONE'S JOB.'", "Somebody's drawn a mustache on the hard hat guy." } },
        { WallArt.Kind.CompanyPoster, new[] { "'KEEPING EARTH LIT.' Very inspirational. Very corporate." } },
        { WallArt.Kind.CrewPhoto, new[] { "Crew photo. Everyone's squinting. One of them blinked." } },
        { WallArt.Kind.KidsDrawing, new[] { "A kid's drawing. A stick figure waving at a big yellow sun.", "'MY MOM WORKS HERE.'" } },
        { WallArt.Kind.Chart, new[] { "Power output, month by month. Up and to the right.", "Someone's written 'NICE' next to it." } },
    };

    static readonly Dictionary<WallArt.Kind, string[]> WallArtThoughtsLater = new Dictionary<WallArt.Kind, string[]>
    {
        { WallArt.Kind.EarthPhoto, new[] { "Earth. They're still down there, waiting on the power.", "Waiting on me, I guess." } },
        { WallArt.Kind.Mountains, new[] { "Somebody missed home." } },
        { WallArt.Kind.SafetyPoster, new[] { "'SAFETY IS EVERYONE'S JOB.'" } },
        { WallArt.Kind.CompanyPoster, new[] { "'KEEPING EARTH LIT.' Not if that flare hits." } },
        { WallArt.Kind.CrewPhoto, new[] { "The crew photo. I met some of them.", "Hours ago." } },
        { WallArt.Kind.KidsDrawing, new[] { "'MY MOM WORKS HERE.'", "..." } },
        { WallArt.Kind.Chart, new[] { "Up and to the right. Until today." } },
    };

    // Everything on floor 1 there is to look at: the furniture, a window in each room that has them, and the pictures.
    static void PlaceInspectables(FloorOneBuilder.BuiltFloor floor, bool later)
    {
        Map map = floor.map;
        var dice = new System.Random(Seed + 2);
        Dictionary<string, string[][]> furnitureThoughts = later ? FurnitureThoughtsLater : FurnitureThoughts;
        var dealt = new Dictionary<string, int>();

        // The furniture, on floor 1.
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
            // Wall face cells over the room's top row of floor, x by x: whether there's a window in it, or a lamp, or
            // it's over a doorway.
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

            // A window: the middle of the first run of them.
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

            // The pictures, spread along the wall, each on the middle row of the face, clear of the lamps.
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

    // A maintenance bot on its rounds over the room's floor: the kinds and unit numbers dealt out in turn.
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

    // In the room, a few steps in from where the player starts, and as far from the entrance as that allows.
    static bool TryGreeterSpot(HashSet<Vector2Int> free, Vector2Int start, out Vector2Int spot)
    {
        spot = default;
        var near = free.Where(c => Chebyshev(c, start) >= 3 && Chebyshev(c, start) <= 4).ToList();
        if (near.Count == 0) return false;
        spot = near.OrderByDescending(c => c.x - start.x).ThenBy(c => Mathf.Abs(c.y - start.y)).First();
        return true;
    }

    // A middle cell with every one of the ring on free floor around it, spaced evenly and all facing in.
    static bool TryPlaceGroup(Map map, HashSet<Vector2Int> free, int size, System.Random random, out Vector2Int middle, out Vector2[] ring)
    {
        List<Vector2Int> candidates = free.OrderBy(_ => random.Next()).ToList();
        // Two face each other across the middle; three or more stand round it, the first at the top facing down.
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

    // At a console when there is one free (the cell just below a desk, facing up at it), anywhere free otherwise.
    static bool TryStandingSpot(Map map, HashSet<Vector2Int> furniture, HashSet<Vector2Int> free, bool atConsoles, System.Random random,
        out Vector2Int cell, out Vector2 facing)
    {
        List<Vector2Int> shuffled = free.OrderBy(_ => random.Next()).ToList();
        if (atConsoles)
        {
            foreach (Vector2Int c in shuffled)
            {
                if (!furniture.Contains(c + Vector2Int.down)) continue;     // map rows count down, so this is the cell above
                cell = c;
                facing = Vector2.up;
                return true;
            }
        }
        cell = shuffled.FirstOrDefault();
        facing = Quaternion.Euler(0f, 0f, random.Next(0, 8) * 45f) * Vector2.down;
        return shuffled.Count > 0;
    }

    // The room's floor as a grid for the people walking about in it.
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
            int y = room.maxRow - cell.y;       // rows count down the map, and up the grid
            area.walkable[y * area.width + x] = true;
        }
        return area;
    }

    // Arrival: the player starts in the doorway of the ship entrance and walks in to where the layout puts them.
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

    // Chapter 1 starts before any of the fighting: nothing in hand, and nothing on the hotbar. Chapter 2 too, for now.
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

    // --- Helpers ---

    static Color Pick(System.Random random) => TrimColors[random.Next(TrimColors.Length)];

    // Where someone stands to have their feet in this cell.
    static Vector2 StandingAt(Map map, Vector2Int cell) => map.Center(cell.x, cell.y) + new Vector2(0f, DepthDressing.FeetLift);

    // The way from one cell to look at another, in world directions.
    static Vector2 Toward(Vector2Int from, Vector2Int to) => new Vector2(to.x - from.x, from.y - to.y);

    // The map cell someone standing here has their feet in.
    static Vector2Int FeetCell(Map map, Vector2 standing) => CellAt(map, standing - new Vector2(0f, DepthDressing.FeetLift));

    // The map cell a world point is in.
    static Vector2Int CellAt(Map map, Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x), map.Height - 1 - Mathf.FloorToInt(world.y));

    static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    static bool NearKeptClear(Map map, Vector2Int cell)
    {
        foreach (Vector2Int step in Around)
            if (FloorOneBuilder.IsKeptClear(map.At(cell.x + step.x, cell.y + step.y))) return true;
        return false;
    }

    // At the end of the build, so the scenes already there keep their places.
    static void AddSceneToBuild()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        // Written to ProjectSettings now, rather than whenever the editor next saves the project.
        AssetDatabase.SaveAssets();
    }
}
