using System.Collections;
using UnityEngine;

// Chapter 2, the moment the technician steps out of the restroom (StorageEscape): the search for the crew is over before
// it starts. They're all out here, dead, and the maintenance bots are roaming the rooms among them, fast and at random
// (PlaceholderRobot.roamArea). The technician stops, Pip says what they're looking at, and the objective becomes
// getting off the station, by the escape pods off the ship entrance, pinned on the map (it's shut: BlockedWay). The
// bots and the cameras (StationCamera) are blind until then, so nobody's caught standing in the doorway taking it in,
// and for a moment after (grace) so there's a chance to move.
// Caught from then on, the technician comes back in the restroom, where the bots never go, until a later stage moves
// that on (BlockedWay.respawnAt); coming back, the bots have forgotten them (OnRespawned).
// Built by ChapterOneBuilder for ChapterTwoBuilder, which lays out the bodies and puts the bots in the rooms.
public class PatrolReveal : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The bots roaming the common grounds and the maintenance deck.")]
    public PlaceholderRobot[] robots = new PlaceholderRobot[0];
    [Tooltip("The restroom, by its marker in the layout, so leaving it is known.")]
    public char restroomRoom = 'w';
    [Tooltip("Seconds the bots stay blind once the player can move again.")]
    public float grace = 1.5f;

    [Header("Stepping out")]
    [Tooltip("The technician, seeing the bodies.")]
    [TextArea] public string[] technicianSees = { "Oh no. No, no, no..." };
    [Tooltip("Pip, after that. ~ opens a line with static; [C] shows a key.")]
    [TextArea] public string[] pipCrewFound =
    {
        "~Tech... I found the crew.",
        "~All of them. They're all out here.",
    };
    [Tooltip("The technician, seeing the bots.")]
    [TextArea] public string[] technicianBots = { "The maintenance bots... what are they doing?" };
    [TextArea] public string[] pipBots =
    {
        "~They're not on their rounds. They're hunting.",
        "~The escape pods are off the ship entrance. Let's get you off this station.",
    };
    [Tooltip("The card that pops up after that, on how sneaking about works (TipCard).")]
    public string tipsTitle = "Staying hidden";
    public string objective = "Find the escape pods";
    [Tooltip("The room pinned on the map for it, by its marker in the layout, and what the pin says.")]
    public char targetRoom = 'h';
    public string targetLabel = "Ship Entrance";

    // How sneaking about works, on the card that pops up once the bots have been seen.
    static readonly TipCard.Row[] StealthTips =
    {
        new TipCard.Row(TipCard.Picture.Eye, "They see what's in front of them",
            "Bots look where they're facing. Walls, shelves, and furniture block their view; out of the corner of their eye they're slower to notice you."),
        new TipCard.Row(TipCard.Picture.Ring, "Detection",
            "A ring fills around you as something notices you. Half full, it comes to look. Full and red, it's coming for you. Break line of sight to lose it."),
        new TipCard.Row("Crouch", "Hold to move slowly and quietly. Harder to spot from a distance, and they won't hear you, but up close they'll still see you.", "C"),
        new TipCard.Row("Sprint", "Fast, but loud: bots hear running footsteps behind them, and see you from further away.", "SHIFT"),
        new TipCard.Row(TipCard.Picture.Locker, "Hide in lockers",
            "Press E at a locker to climb in. A bot walks right past, unless it saw you get in."),
        new TipCard.Row(TipCard.Picture.Camera, "Cameras",
            "They sweep the room. Red light: Sonny's watching. If one spots you, every bot in the room comes running."),
    };

    // While the technician's taking it in (CrewAftermath holds Pip's lines about the bodies until it's over).
    public static bool Playing { get; private set; }

    private bool revealed;

    void OnEnable()
    {
        StorageEscape.Escaped += BeginWatch;
        PlayerHealthHandler.Respawned += OnRespawned;
    }

    void OnDisable()
    {
        StorageEscape.Escaped -= BeginWatch;
        PlayerHealthHandler.Respawned -= OnRespawned;
        Playing = false;
    }

    // Caught and back at the last respawn point (the restroom, or wherever the last blocked way put it): the bots
    // forget them, and don't see them for a moment, so nobody's caught again the second they're back on their feet.
    void OnRespawned()
    {
        if (!revealed) return;
        foreach (PlaceholderRobot robot in robots)
            if (robot != null) robot.CalmDown();
        foreach (StationCamera watcher in StationCamera.All)
            watcher.CalmDown();
        SetBlind(true);
        StartCoroutine(OpenEyesAfterGrace());
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        SetBlind(true);
    }

    void BeginWatch() => StartCoroutine(WatchTheDoor());

    // In the restroom until they walk out of it (the vents count as neither).
    IEnumerator WatchTheDoor()
    {
        while (player == null || VentNetwork.IsPlayerInside || InRoom(player.transform.position, restroomRoom) || !Located(player.transform.position))
            yield return null;
        yield return Reveal();
    }

    IEnumerator Reveal()
    {
        Playing = true;
        RespawnInRestroom();
        player.SetScriptedInput(Vector2.zero, false);
        CameraShake.Shake(0.1f);
        TechnicianVoice.Hush();
        if (SuitHelper.Exists) SuitHelper.Get().Hush();

        yield return Wait(0.5f);
        yield return TechnicianVoice.Think(technicianSees);
        if (SuitHelper.Exists) yield return SuitHelper.Get().Say(pipCrewFound);
        yield return TechnicianVoice.Think(technicianBots);
        if (SuitHelper.Exists) yield return SuitHelper.Get().Say(pipBots);
        yield return TipCard.Show(tipsTitle, StealthTips);

        player.ClearScriptedInput();
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
        Playing = false;
        revealed = true;
        yield return Wait(grace);
        SetBlind(false);
    }

    // Picking up from a save after it's happened (ChapterTwoDirector). setObjective: nothing later has moved it on.
    public void ResumeRevealed(bool setObjective)
    {
        StopAllCoroutines();
        Playing = false;
        revealed = true;
        RespawnInRestroom();
        if (setObjective)
        {
            TutorialHud.Get().SetObjective(objective);
            MapScreen.SetTarget(targetRoom, targetLabel);
        }
        StartCoroutine(OpenEyesAfterGrace());
    }

    IEnumerator OpenEyesAfterGrace()
    {
        yield return Wait(grace);
        SetBlind(false);
    }

    // Where they climbed out: the bots keep to their own rooms, so it's safe.
    void RespawnInRestroom()
    {
        if (player == null || !player.TryGetComponent(out PlayerHealthHandler handler)) return;
        var escape = FindAnyObjectByType<StorageEscape>();
        VentGrate vent = escape != null ? escape.restroomVent : null;
        if (vent == null) return;
        Vector2 at = (Vector2)vent.transform.position + vent.exitOffset;
        handler.RespawnPoint = new Vector3(at.x, at.y, player.transform.position.z);
    }

    void SetBlind(bool blind)
    {
        foreach (PlaceholderRobot robot in robots)
            if (robot != null) robot.blind = blind;
        // The cameras too (StationCamera): the station's own, Sonny's now.
        foreach (StationCamera watcher in StationCamera.All)
            if (watcher.hostile) watcher.blind = blind;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    static bool Located(Vector2 point) => StationMap.Current != null && StationMap.Current.Locate(point, out _, out _);

    static bool InRoom(Vector2 point, char marker)
    {
        StationMap map = StationMap.Current;
        return map != null && map.Locate(point, out _, out StationMap.Room room) && room.marker == marker;
    }
}
