using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// puts sonny in the middle of the control room
public static class SonnyPlacement
{
    const char ControlRoom = 'o';
    const float ArtMiddle = 5f;
    static readonly Rect Covers = Rect.MinMaxRect(-11.2f, -2.8f, 11.2f, 11f);
    static readonly Rect Walkway = Rect.MinMaxRect(-20f, -2.8f, -10.4f, 8f);
    static readonly Vector2 ChiefSpot = new Vector2(2.6f, -1.1f);
    const float WallHeight = 3.5f;
    const float Spacing = 1.2f;

    public static void PlaceInOpenScene()
    {
        var sonny = Object.FindAnyObjectByType<SonnyBox>(FindObjectsInactive.Include);
        if (sonny == null)
        {
            Debug.LogWarning("Place Sonny: this scene has no Sonny.");
            return;
        }
        StationMap map = FindMap(sonny);
        StationMap.Room room = map != null ? map.RoomByMarker(ControlRoom) : null;
        if (room == null)
        {
            if (Object.FindAnyObjectByType<TutorialDirector>(FindObjectsInactive.Include) != null) PlaceInTutorial(sonny);
            else Debug.LogWarning("Place Sonny: no control room on this scene's map.");
            return;
        }

        Rect world = room.World;
        Vector2 from = sonny.transform.position;
        Vector2 to = new Vector2(Mathf.Floor(world.center.x) + 0.5f, Mathf.Round(world.center.y - ArtMiddle));
        Vector2 delta = to - from;
        var moved = new List<string>();
        if (delta.sqrMagnitude > 0.01f)
        {
            Move(sonny.transform, delta);
            moved.Add("Sonny");
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Sonny Install Point") { Move(t, delta); moved.Add(t.name); }
            MoveMark(map, to);
        }

        int cleared = ClearRoom(world);
        if (cleared > 0) moved.Add($"{cleared} things cleared out of the room");

        var cutscene = Object.FindAnyObjectByType<ControlRoomCutscene>(FindObjectsInactive.Include);
        CrewMember chief = cutscene != null ? cutscene.chief : null;
        if (chief != null)
        {
            SetPosition(chief.transform, to + ChiefSpot);
            moved.Add("the chief");
        }

        var everyone = new List<CrewMember>(Object.FindObjectsByType<CrewMember>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        var settled = new List<Vector2>();
        var inTheWay = new List<CrewMember>();
        foreach (CrewMember crew in everyone)
        {
            if (crew == chief || !Covers.Contains((Vector2)crew.transform.position - to)) settled.Add(crew.transform.position);
            else inTheWay.Add(crew);
        }
        int relocated = 0;
        foreach (CrewMember crew in inTheWay)
        {
            if (!ClearSpot(world, to, crew.transform.position, settled, out Vector2 spot))
            {
                Debug.LogWarning($"Place Sonny: nowhere clear in the control room for {crew.name}; left where it was.", crew);
                continue;
            }
            SetPosition(crew.transform, spot);
            settled.Add(spot);
            relocated++;
        }
        if (relocated > 0) moved.Add($"{relocated} crew out of its way");

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(moved.Count > 0 ? $"Place Sonny: moved {string.Join(", ", moved)}." : "Place Sonny: everything's already in place.");
    }

    static void PlaceInTutorial(SonnyBox sonny)
    {
        Debug.Log("Place Sonny: the tutorial's Sonny is where its layout's S puts it (TutorialLayout.txt); rebuild the level to move it.");
    }

    public static void PlaceInAllScenes()
    {
        foreach (string scene in new[] { "Chapter1", "Chapter2", "Chapter3" })
        {
            EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Levels/{scene}.unity");
            PlaceInOpenScene();
            EditorSceneManager.SaveOpenScenes();
        }
    }

    static bool ClearSpot(Rect room, Vector2 sonny, Vector2 near, List<Vector2> taken, out Vector2 spot)
    {
        spot = near;
        float best = float.MaxValue;
        var filter = new ContactFilter2D { useTriggers = false };
        var hits = new List<Collider2D>();
        for (float x = room.xMin + 1.5f; x <= room.xMax - 1.5f; x += 0.5f)
        {
            for (float y = room.yMin + 1.5f; y <= room.yMax - 2.5f; y += 0.5f)
            {
                var p = new Vector2(x, y);
                if (Covers.Contains(p - sonny) || Walkway.Contains(p - sonny)) continue;
                if ((p - near).sqrMagnitude >= best) continue;
                bool crowded = false;
                foreach (Vector2 other in taken)
                    if ((other - p).sqrMagnitude < Spacing * Spacing) { crowded = true; break; }
                if (crowded) continue;
                hits.Clear();
                bool blocked = false;
                if (Physics2D.OverlapCircle(p, 0.45f, filter, hits) > 0)
                    foreach (Collider2D hit in hits)
                        if (hit.GetComponentInParent<CrewMember>() == null) { blocked = true; break; }
                if (blocked) continue;
                best = (p - near).sqrMagnitude;
                spot = p;
            }
        }
        return best < float.MaxValue;
    }

    static int ClearRoom(Rect floor)
    {
        Rect withWalls = Rect.MinMaxRect(floor.xMin, floor.yMin, floor.xMax, floor.yMax + WallHeight);
        var doomed = new HashSet<GameObject>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "Furniture" && t.name != "Blinking Lights" && t.name != "Things to Look At") continue;
            foreach (Transform child in t)
                if (withWalls.Contains(child.position) && !Keep(child)) doomed.Add(child.gameObject);
        }
        foreach (WallArt art in Object.FindObjectsByType<WallArt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (withWalls.Contains(art.transform.position) && !Keep(art.transform)) doomed.Add(art.gameObject);
        foreach (StationProp prop in Object.FindObjectsByType<StationProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (withWalls.Contains(prop.transform.position) && !Keep(prop.transform)) doomed.Add(prop.gameObject);
        foreach (GameObject go in doomed)
            if (go != null) Undo.DestroyObjectImmediate(go);

        int tiles = 0;
        foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map.name != "Rugs" && map.name != "Floor Details") continue;
            Undo.RecordObject(map, "Place Sonny");
            Vector3Int min = map.WorldToCell(new Vector3(floor.xMin, floor.yMin)), max = map.WorldToCell(new Vector3(floor.xMax, floor.yMax));
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (map.GetTile(cell) == null) continue;
                    map.SetTile(cell, null);
                    tiles++;
                }
        }
        return doomed.Count + tiles;
    }

    static bool Keep(Transform t) =>
        t.GetComponentInChildren<SonnyBox>(true) != null || t.GetComponentInChildren<CrewMember>(true) != null ||
        t.GetComponentInChildren<Teleporter>(true) != null || t.GetComponentInChildren<StationCamera>(true) != null ||
        t.GetComponentInChildren<Locker>(true) != null || t.GetComponentInChildren<StationLight>(true) != null;

    static StationMap FindMap(SonnyBox sonny)
    {
        foreach (StationMap map in Object.FindObjectsByType<StationMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (map.Locate(sonny.transform.position, out _, out StationMap.Room room) && room != null) return map;
        return null;
    }

    static void Move(Transform t, Vector2 by)
    {
        Undo.RecordObject(t, "Place Sonny");
        t.position += (Vector3)by;
    }

    static void SetPosition(Transform t, Vector2 at)
    {
        Undo.RecordObject(t, "Place Sonny");
        t.position = new Vector3(at.x, at.y, t.position.z);
    }

    static void MoveMark(StationMap map, Vector2 to)
    {
        if (!map.Locate(to + Vector2.up * 2f, out Vector2 onPlan, out _)) return;
        Undo.RecordObject(map, "Place Sonny");
        for (int i = 0; i < map.marks.Length; i++)
            if (map.marks[i] == (byte)StationMap.Mark.Sonny) map.marks[i] = (byte)StationMap.Mark.None;
        int x = Mathf.FloorToInt(onPlan.x), row = Mathf.FloorToInt(onPlan.y);
        if (x >= 0 && row >= 0 && x < map.width && row < map.height) map.marks[row * map.width + x] = (byte)StationMap.Mark.Sonny;
        EditorUtility.SetDirty(map);
    }
}
