using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Menu: Sonny > Build Chapter 2
//
// Builds Scenes/Levels/Chapter2.unity: the whole station again, both floors, as FloorOneBuilder lays it out, a few hours
// after Chapter 1 ends. Nobody's left alive. The lights are down to emergency power (a dim red, every other lamp out),
// the crew lie where they stood in Chapter 1, Sonny's box glows red in the control room, and everything's where Chapter 1
// left it (all placed by ChapterOneBuilder, from the same crowds, so it lines up): the broken toilet, the knocked-about
// shelf, and the storage room door jammed with rubble behind it, with the vents the only way out. The control room's
// badge door won't open for anyone now (BadgeLockout): the way in is through the comms ring, on floor 2.
// The technician starts on the storage room floor, in front of the shelf, and ChapterTwoDirector wakes them up. The
// stairs work: floor 2 (the comms ring, the lower maintenance deck, the reactor) is there, empty for now.
// It replaces everything in the scene but the Sound Manager.
public static class ChapterTwoBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Chapter2.unity";

    // Emergency power: the ambient light at this much of what it is in Chapter 1, in this color.
    const float EmergencyLight = 0.4f;
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

        // The control room's badge door, with nobody left to badge them through.
        if (ChapterOneBuilder.ControlRoomDoor != null)
        {
            var lockout = new GameObject("Control Room Lockout").AddComponent<BadgeLockout>();
            lockout.transform.SetParent(floor.level);
            lockout.player = player.GetComponent<PlayerController>();
            lockout.door = ChapterOneBuilder.ControlRoomDoor;
            TutorialLevelBuilder.Record(lockout);
        }
        else Debug.LogWarning("Chapter 2 builder: there's no badge door into the control room.");

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
            int lamp = 0;
            foreach (StationLight station in floor.level.GetComponentsInChildren<StationLight>())
            {
                if (station.mode == StationLight.Mode.Sunlight || lamp++ % 2 == 0) continue;
                station.startOn = false;
                TutorialLevelBuilder.Record(station);
            }
        }
    }
}
