using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Chapter 2's way out of the storage room, where the technician wakes up (ChapterTwoDirector).
// While they were out, something big came down in the lounge, right against the storage room door: the door's jammed
// (both ends locked), and rubble lies piled on the lounge side, where they'll find it later. The door won't budge.
// The first time they try it, the technician says so and Pip spots the floor vent: the air ducts (VentNetwork) run from here to the
// restroom. The vent grates at both ends only work from the wake-up on. Getting caught in the ducts puts them back in
// the storage room, where they woke. Climbing out in the restroom, the objective moves on to finding the crew, which is
// over the moment they step out of it (PatrolReveal). Until they do, the restroom's way out is pointed out
// (ObjectiveHighlight), so there's no wondering which way to go.
// Built by ChapterOneBuilder for ChapterTwoBuilder, which places the rubble, the grates, and the ducts.
public class StorageEscape : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("Both ends of the storage room door. They lock once it's jammed.")]
    public Teleporter[] jammedDoors = new Teleporter[0];
    [Tooltip("The rubble on the lounge side of the door.")]
    public GameObject[] rubble = new GameObject[0];
    [Tooltip("The vent in the storage room, and the one in the restroom it comes out at. Both off until the wake-up.")]
    public VentGrate storageVent;
    public VentGrate restroomVent;
    [Tooltip("The restroom, by its marker in the layout, so climbing out there is known.")]
    public char restroomRoom = 'w';

    [Header("The jammed door")]
    public string jammedText = "WON'T BUDGE";
    [Tooltip("How close to the storage room door counts as trying it.")]
    public float tryDoorRange = 1.4f;
    [Tooltip("The technician, when they try the door.")]
    [TextArea] public string[] technicianDoorJammed = { "It won't budge. Something's jammed against the other side." };
    [Tooltip("Pip, after that. ~ opens a line with static; ^ says it happily; [W] shows a key.")]
    [TextArea] public string[] pipDoorJammed = { "~Jammed. Take the vent on the floor." };
    [Tooltip("Pip, on how to get about in the ducts, said once the vent's been pointed out.")]
    [TextArea] public string[] pipVentHowTo = { "[W] crawls, [A] and [D] turn. Hold [C] to go slow and quiet." };
    public string objectiveVent = "Crawl through the vent";

    [Header("Out the other side")]
    [Tooltip("The technician, climbing out.")]
    [TextArea] public string[] technicianOut = { "The restroom. Of course." };
    [TextArea] public string[] pipOut = new string[0];
    public string objectiveOut = "Find the crew";

    // Once they've climbed out in the restroom (CrewAftermath).
    public static event System.Action Escaped;

    void OnEnable()
    {
        ChapterTwoDirector.WokeUp += BeginEscape;
    }

    void OnDisable()
    {
        ChapterTwoDirector.WokeUp -= BeginEscape;
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        Jam();
        SetVents(false);
    }

    // Whatever hit the station while they were out came down right outside the door.
    void Jam()
    {
        foreach (Teleporter door in jammedDoors)
        {
            if (door == null) continue;
            door.locked = true;
            door.lockedText = jammedText;
        }
        foreach (GameObject pile in rubble)
            if (pile != null) pile.SetActive(true);
    }

    void BeginEscape()
    {
        SetVents(true);
        // Caught in the ducts, they come back here, where they woke.
        if (player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = player.transform.position;
        StartCoroutine(Escape());
    }

    IEnumerator Escape()
    {
        TutorialHud hud = TutorialHud.Get();
        Teleporter door = jammedDoors.Length > 0 ? jammedDoors[0] : null;
        foreach (Teleporter end in jammedDoors)
            if (end != null && InStorageRoom(end.transform.position)) door = end;

        // Trying the door, unless they've gone straight for the vent.
        while (!VentNetwork.IsPlayerInside && (door == null || !Near(door)))
            yield return null;
        if (!VentNetwork.IsPlayerInside)
        {
            hud.SetObjective(objectiveVent);
            yield return TechnicianVoice.Think(technicianDoorJammed);
            if (SuitHelper.Exists)
            {
                SuitHelper pip = SuitHelper.Get();
                pip.Hush();
                pip.Tell(pipDoorJammed);
                pip.Tell(pipVentHowTo);
            }
        }
        yield return Crawl();
    }

    // Picking up from a save (ChapterTwoDirector): the door's been tried and the vent's the way out.
    public void ResumeAtVent()
    {
        SetVents(true);
        TutorialHud.Get().SetObjective(objectiveVent);
        StartCoroutine(Crawl());
    }

    // Picking up from a save once they're out of the ducts. searching: they haven't stepped out of the restroom yet
    // (PatrolReveal, CrewAftermath).
    public void ResumeOut(bool searching)
    {
        SetVents(true);
        if (!searching) return;
        TutorialHud.Get().SetObjective(objectiveOut);
        StartCoroutine(PointOut());
        Escaped?.Invoke();
    }

    // In and out of the ducts, until they come out in the restroom.
    IEnumerator Crawl()
    {
        TutorialHud hud = TutorialHud.Get();
        // In and out again, until they come out in the restroom (caught, they're back in the storage room).
        while (true)
        {
            while (!VentNetwork.IsPlayerInside) yield return null;
            while (VentNetwork.IsPlayerInside) yield return null;
            yield return null;
            if (InRoom(player.transform.position, restroomRoom)) break;
        }

        hud.SetObjective(objectiveOut);
        StartCoroutine(PointOut());
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        yield return TechnicianVoice.Think(technicianOut);
        if (SuitHelper.Exists) SuitHelper.Get().Tell(pipOut);
        Escaped?.Invoke();
    }

    // The restroom's doors out, lit up, until they've gone through one.
    IEnumerator PointOut()
    {
        var exits = new List<Teleporter>();
        foreach (Teleporter door in FindObjectsByType<Teleporter>())
            if (!door.locked && door.teleportTarget != null && InRoom(door.transform.position, restroomRoom)) exits.Add(door);
        ObjectiveHighlight.Show(exits);
        while (player == null || VentNetwork.IsPlayerInside || InRoom(player.transform.position, restroomRoom)) yield return null;
        ObjectiveHighlight.Hide();
    }

    void SetVents(bool usable)
    {
        if (storageVent != null) storageVent.enabled = usable;
        if (restroomVent != null) restroomVent.enabled = usable;
    }

    bool Near(Teleporter door)
    {
        if (player == null) return false;
        var trigger = door.GetComponent<Collider2D>();
        Vector2 at = player.transform.position;
        Vector2 closest = trigger != null ? trigger.ClosestPoint(at) : (Vector2)door.transform.position;
        return Vector2.Distance(closest, at) <= tryDoorRange;
    }

    bool InStorageRoom(Vector2 point) => storageVent != null && SameRoom(point, storageVent.transform.position);

    static bool SameRoom(Vector2 a, Vector2 b)
    {
        StationMap map = StationMap.Current;
        return map != null && map.Locate(a, out _, out StationMap.Room roomA) && map.Locate(b, out _, out StationMap.Room roomB)
            && roomA.marker == roomB.marker;
    }

    static bool InRoom(Vector2 point, char marker)
    {
        StationMap map = StationMap.Current;
        return map != null && map.Locate(point, out _, out StationMap.Room room) && room.marker == marker;
    }
}
