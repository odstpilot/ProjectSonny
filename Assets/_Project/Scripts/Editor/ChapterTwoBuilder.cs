using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// builds chapter 2
public static class ChapterTwoBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Chapter2.unity";
    internal const string ChapterThreeScene = "Chapter3";
    const string ReactorCredential = "reactor_core";
    const char ReactorRoom = 'r';

    const float EmergencyLight = 0.5f;
    const string DarkRoomMarkers = "hbmcodg";
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

    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build() ? 0 : 1);
    }

    public static bool Build()
    {
        if (!BuildStation(ScenePath, false)) return false;
        Debug.Log($"Chapter 2 builder: saved {ScenePath}.");
        return true;
    }

    internal static bool BuildStation(string scenePath, bool chapterThree)
    {
        bool filled = false;
        bool built = FloorOneBuilder.Build(scenePath, floors => filled = Populate(floors, chapterThree));
        return built && filled;
    }

    static bool Populate(FloorOneBuilder.BuiltFloor[] floors, bool chapterThree)
    {
        if (!ChapterOneBuilder.PopulateChapterTwo(floors)) return false;
        FloorOneBuilder.BuiltFloor floor = floors[0];
        GameObject player = floor.player;
        ChapterOneBuilder.Unarm(player);
        EmergencyPower(floors);

        SonnyBox sonny = TutorialLevelBuilder.PlaceSonny(floor.map, floor.level);
        if (sonny != null)
        {
            sonny.startPowered = true;
            sonny.look = SonnyBox.Look.Rogue;
            sonny.lensColor = SonnyLens;
            if (sonny.glow != null) sonny.glow.color = SonnyLens;
            TutorialLevelBuilder.Record(sonny);
        }

        if (ChapterOneBuilder.WakeSpot is Vector2 wake)
        {
            player.transform.position = new Vector3(wake.x, wake.y, player.transform.position.z);
            TutorialLevelBuilder.Record(player.transform);
        }
        else Debug.LogWarning("Chapter 2 builder: there's no shelf in the storage room, so the technician wakes up at the ship entrance.");

        var darkness = new GameObject("Dark Rooms").AddComponent<DarkRooms>();
        darkness.transform.SetParent(floor.level);
        darkness.rooms = DarkRoomMarkers;
        TutorialLevelBuilder.Record(darkness);

        if (chapterThree)
        {
            BlockedWay inside = floors.SelectMany(f => f.level.GetComponentsInChildren<BlockedWay>(true)).FirstOrDefault(w => w.arriveInRoom == ReactorRoom);
            if (inside != null)
            {
                player.transform.position = new Vector3(inside.standAt.x, inside.standAt.y, player.transform.position.z);
                TutorialLevelBuilder.Record(player.transform);
            }
            else Debug.LogWarning("Chapter 3 builder: there's no way into the reactor core, so the technician starts in the storage room.");

            var three = new GameObject("Chapter 3 Director").AddComponent<ChapterThreeDirector>();
            three.transform.SetParent(floor.level);
            three.player = player.GetComponent<PlayerController>();
            TutorialLevelBuilder.Record(three);
            return true;
        }

        EndAtTheReactor(floors);
        var director = new GameObject("Chapter 2 Director").AddComponent<ChapterTwoDirector>();
        director.transform.SetParent(floor.level);
        director.player = player.GetComponent<PlayerController>();
        TutorialLevelBuilder.Record(director);

        return true;
    }

    static void EndAtTheReactor(FloorOneBuilder.BuiltFloor[] floors)
    {
        int doors = 0;
        foreach (FloorOneBuilder.BuiltFloor floor in floors)
        {
            foreach (Teleporter door in floor.level.GetComponentsInChildren<Teleporter>(true))
            {
                if (door.credential != ReactorCredential) continue;
                door.nextScene = ChapterThreeScene;
                TutorialLevelBuilder.Record(door);
                doors++;
            }

            Transform gauntlet = floor.level.Find("Reactor Gauntlet");
            if (gauntlet != null) Object.DestroyImmediate(gauntlet.gameObject);
            foreach (BlockedWay way in floor.level.GetComponentsInChildren<BlockedWay>(true))
                if (way.stage == "control-room") Object.DestroyImmediate(way.gameObject);

            var reactor = floor.rooms.FirstOrDefault(r => r.marker == ReactorRoom);
            if (reactor == null) continue;
            var cells = new HashSet<Vector2Int>(reactor.cells);
            TutorialLevelBuilder.Map map = floor.map;
            bool Inside(Vector2 world)
            {
                Vector2 local = world - map.Origin;
                return cells.Contains(new Vector2Int(Mathf.FloorToInt(local.x), map.Height - 1 - Mathf.FloorToInt(local.y)));
            }
            foreach (PlaceholderRobot bot in floor.level.GetComponentsInChildren<PlaceholderRobot>(true))
                if (Inside(bot.transform.position)) Object.DestroyImmediate(bot.gameObject);
        }
        if (doors == 0) Debug.LogWarning("Chapter 2 builder: there are no doors into the reactor core, so nothing takes the player on to Chapter 3.");
    }

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
            var dark = floor.rooms.Where(r => DarkRoomMarkers.IndexOf(r.marker) >= 0)
                .Select(r => floor.map.WorldRect(r.minX, r.maxX, r.minRow - 4, r.maxRow)).ToList();
            int lamp = 0, darkLamp = 0;
            foreach (StationLight station in floor.level.GetComponentsInChildren<StationLight>())
            {
                if (station.mode == StationLight.Mode.Sunlight) continue;
                if (dark.Any(r => r.Contains(station.transform.position)))
                {
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
