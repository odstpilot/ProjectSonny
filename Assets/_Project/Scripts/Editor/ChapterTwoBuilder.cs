using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Menu: Sonny > Build Chapter 2
//
// Builds Scenes/Levels/Chapter2.unity: the whole station again, both floors, as FloorOneBuilder lays it out, a few hours
// after Chapter 1 ends. Nobody's left alive. The lights are down to emergency power (a dim red, every other lamp out),
// the crew lie dead all through the rooms, converted bots roam among them (PatrolReveal shows them to the player on
// stepping out of the restroom), Sonny's box glows red in the control room, and everything's where Chapter 1
// left it (all placed by ChapterOneBuilder, from the same crowds, so it lines up): the broken toilet, the knocked-about
// shelf, and the storage room door jammed with rubble behind it, with the vents the only way out. Every other way on
// is shut too: the ship entrance is sealed and the escape pods gone (Pip says so, PatrolReveal), the hallway to the
// control room has caved in (BlockedWay), so the only way on is the maintenance deck's stairs, up to the upper maintenance deck. The technician starts on the storage room floor, in front of the shelf, and
// ChapterTwoDirector wakes them up. Floor 2 (the comms ring, the upper maintenance deck, the reactor) is there, empty
// for now.
// It replaces everything in the scene but the Sound Manager.
public static class ChapterTwoBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Chapter2.unity";

    // Emergency power: the ambient light at this much of what it is in Chapter 1, in this color.
    const float EmergencyLight = 0.4f;
    // The rooms past the lounge, with the power out altogether (DarkRooms): the common grounds hallway, the bedrooms, the
    // maintenance deck, the hallway to the control room and the control room, and all of floor 2.
    const string DarkRoomMarkers = "hbmcodgr";
    static readonly Color EmergencyColor = new Color(1f, 0.5f, 0.42f);
    static readonly Color SonnyLens = new Color(1f, 0.16f, 0.1f);

    [MenuItem("Sonny/Build Chapter 2")]
    public static void BuildFromMenu()
    {
        bool rebuild = EditorUtility.DisplayDialog("Build Chapter 2",
            "Rebuild Chapter2.unity from Floor1Layout.txt and Floor2Layout.txt?\n\nThis replaces everything in the scene except the Sound Manager. Anything else added to it by hand will be lost.",
            "Rebuild", "Cancel");
        if (rebuild && UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            Build();
    }

    // For batch mode: Unity -batchmode -projectPath <project> -executeMethod ChapterTwoBuilder.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build() ? 0 : 1);
    }

    public static bool Build()
    {
        bool filled = false;
        bool built = FloorOneBuilder.Build(ScenePath, floors => filled = Populate(floors));
        if (!built || !filled) return false;
        Debug.Log($"Chapter 2 builder: saved {ScenePath}.");
        return true;
    }

    static bool Populate(FloorOneBuilder.BuiltFloor[] floors)
    {
        if (!ChapterOneBuilder.PopulateChapterTwo(floors)) return false;
        FloorOneBuilder.BuiltFloor floor = floors[0];
        GameObject player = floor.player;
        ChapterOneBuilder.Unarm(player);
        EmergencyPower(floors);

        // Sonny, running things now, lens red.
        SonnyBox sonny = TutorialLevelBuilder.PlaceSonny(floor.map, floor.level);
        if (sonny != null)
        {
            sonny.startPowered = true;
            sonny.lensColor = SonnyLens;
            if (sonny.glow != null) sonny.glow.color = SonnyLens;
            TutorialLevelBuilder.Record(sonny);
        }

        // On the storage room floor, where the crate knocked them out.
        if (ChapterOneBuilder.WakeSpot is Vector2 wake)
        {
            player.transform.position = new Vector3(wake.x, wake.y, player.transform.position.z);
            TutorialLevelBuilder.Record(player.transform);
        }
        else Debug.LogWarning("Chapter 2 builder: there's no shelf in the storage room, so the technician wakes up at the ship entrance.");

        // Past the lounge, the power's out (DarkRooms), and the suit light comes on.
        var darkness = new GameObject("Dark Rooms").AddComponent<DarkRooms>();
        darkness.transform.SetParent(floor.level);
        darkness.rooms = DarkRoomMarkers;
        TutorialLevelBuilder.Record(darkness);

        var director = new GameObject("Chapter 2 Director").AddComponent<ChapterTwoDirector>();
        director.transform.SetParent(floor.level);
        director.player = player.GetComponent<PlayerController>();
        TutorialLevelBuilder.Record(director);

        return true;
    }

    // The ambient light down and red, and every other lamp out, on every floor.
    static void EmergencyPower(FloorOneBuilder.BuiltFloor[] floors)
    {
        foreach (Light2D light in Object.FindObjectsByType<Light2D>())
        {
            if (light.lightType != Light2D.LightType.Global) continue;
            light.intensity *= EmergencyLight;
            light.color = EmergencyColor;
            TutorialLevelBuilder.Record(light);
        }
        foreach (FloorOneBuilder.BuiltFloor floor in floors)
        {
            // The rooms with the power out, by where their walls are (a lamp hangs on the wall above the floor).
            var dark = floor.rooms.Where(r => DarkRoomMarkers.IndexOf(r.marker) >= 0)
                .Select(r => floor.map.WorldRect(r.minX, r.maxX, r.minRow - 4, r.maxRow)).ToList();
            int lamp = 0, darkLamp = 0;
            foreach (StationLight station in floor.level.GetComponentsInChildren<StationLight>())
            {
                if (station.mode == StationLight.Mode.Sunlight) continue;
                if (dark.Any(r => r.Contains(station.transform.position)))
                {
                    // Out, but for the odd one still flickering, weakly, and the odd one smashed and sparking.
                    int which = darkLamp++ % 8;
                    if (which == 0)
                    {
                        station.mode = StationLight.Mode.Flicker;
                        station.flickerRate = 1.4f;
                        foreach (Light2D bulbLight in station.GetComponentsInChildren<Light2D>())
                        {
                            bulbLight.intensity *= 0.45f;
                            TutorialLevelBuilder.Record(bulbLight);
                        }
                    }
                    else if (which == 4) station.startBroken = true;
                    else station.startOn = false;
                }
                else if (lamp++ % 2 == 1) station.startOn = false;
                TutorialLevelBuilder.Record(station);
            }
        }
    }
}
