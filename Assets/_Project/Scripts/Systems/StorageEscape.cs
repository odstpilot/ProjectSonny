using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// chapter 2: out of the storage room by the vents
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
    [TextArea] public string[] technicianDoorJammed = { "It won't budge, something's jammed against the other side" };
    public string objectiveVent = "Crawl through the vent";

    [Header("Out the other side")]
    [Tooltip("The technician, climbing out.")]
    [TextArea] public string[] technicianOut = { "The restroom... of course it's the restroom" };
    public string objectiveOut = "Find the crew";

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
                pip.Tell(PipManager.Lines.doorJammed);
                pip.Tell(PipManager.Lines.ventHowTo);
            }
        }
        yield return Crawl();
    }

    public void ResumeAtVent()
    {
        SetVents(true);
        TutorialHud.Get().SetObjective(objectiveVent);
        StartCoroutine(Crawl());
    }

    public void ResumeOut(bool searching)
    {
        SetVents(true);
        if (!searching) return;
        TutorialHud.Get().SetObjective(objectiveOut);
        StartCoroutine(PointOut());
        Escaped?.Invoke();
    }

    IEnumerator Crawl()
    {
        TutorialHud hud = TutorialHud.Get();
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
        if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.outOfVents);
        Escaped?.Invoke();
    }

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
