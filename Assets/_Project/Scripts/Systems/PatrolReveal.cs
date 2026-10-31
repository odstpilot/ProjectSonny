using System.Collections;
using UnityEngine;

// chapter 2: the bots shown hunting
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
    [TextArea] public string[] technicianSees = { "Oh no... no, no, no..." };
    [Tooltip("The technician, seeing the bots.")]
    [TextArea] public string[] technicianBots = { "The bots... they're hunting" };
    [Tooltip("The card that pops up after that, on how sneaking about works (TipCard).")]
    public string tipsTitle = "Staying hidden";
    public string objective = "Get to the control room";
    [Tooltip("The room pinned on the map for it, by its marker in the layout, and what the pin says.")]
    public char targetRoom = 'o';
    public string targetLabel = "Control Room";

    static readonly TipCard.Row[] StealthTips =
    {
        new TipCard.Row(TipCard.Picture.Eye, "They see what's in front of them",
            "Bots look where they're facing. Walls, shelves, and furniture block their view; out of the corner of their eye they're slower to notice you."),
        new TipCard.Row(TipCard.Picture.Ring, "Detection",
            "A ring fills around you as something notices you. Half full, it comes to look. Full and red, it's coming for you. Break line of sight to lose it."),
        new TipCard.Row("Crouch", "Tap to move slowly and quietly, and again to stand. Harder to spot from a distance, and they won't hear you, but up close they'll still see you.", "C"),
        new TipCard.Row("Sprint", "Fast, but loud: bots hear running footsteps behind them, and see you from further away.", "SHIFT"),
        new TipCard.Row(TipCard.Picture.Locker, "Hide in lockers",
            "Press E at a locker to climb in. A bot walks right past, unless it saw you get in."),
        new TipCard.Row(TipCard.Picture.Camera, "Cameras",
            "They sweep the room. Red light: Sonny's watching. If one spots you, every bot in the room comes running."),
        new TipCard.Row(TipCard.Picture.Eye, "The thing with the eyes",
            "It only sees down its beams of light. It hears much more: footsteps, running, a fight. When it goes still, so should you."),
    };

    public static bool Playing { get; private set; }

    private bool revealed;

    public bool Revealed => revealed;

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

    void OnRespawned()
    {
        if (!revealed) return;
        foreach (PlaceholderRobot robot in robots)
            if (robot != null) robot.CalmDown();
        foreach (CameraSpider spider in CameraSpider.All)
            spider.CalmDown();
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

        yield return Wait(0.3f);
        yield return TechnicianVoice.Think(technicianSees);
        if (SuitHelper.Exists) yield return SuitHelper.Get().Say(PipManager.Lines.crewFound);
        TipCard.Log(tipsTitle, StealthTips);

        player.ClearScriptedInput();
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
        if (technicianBots.Length > 0) TechnicianVoice.Say(technicianBots[0]);
        if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.botsRevealed);
        Playing = false;
        revealed = true;
        StartCoroutine(TeachSneaking());
        yield return Wait(grace);
        SetBlind(false);
    }

    IEnumerator TeachSneaking()
    {
        TutorialHud hud = TutorialHud.Get();
        yield return ControlHints.WhenFree(hud);
        hud.ShowPromptWithHint("Crouch to sneak", "Slower and quieter. Harder to spot from a distance", GameSettings.ToggleCrouch ? "C" : "HOLD C");
        while (player != null && !player.IsCrouching) yield return null;
        hud.CompletePrompt();

        while (!AnyHunting()) yield return null;
        yield return ControlHints.WhenFree(hud);
        hud.ShowPromptWithHint("Get out of sight", "Put a wall between you, or hide in a locker", "E");
        while (AnyHunting()) yield return null;
        hud.CompletePrompt();
    }

    bool AnyHunting()
    {
        foreach (PlaceholderRobot robot in robots)
            if (robot != null && robot.isActiveAndEnabled && robot.IsHunting) return true;
        foreach (CameraSpider spider in CameraSpider.All)
            if (spider.IsHunting) return true;
        return false;
    }

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
        foreach (CameraSpider spider in CameraSpider.All)
            spider.blind = blind;
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
