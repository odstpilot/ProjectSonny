using System.Collections;
using UnityEngine;

// runs chapter 3, from the reactor down to sonny
public class ChapterThreeDirector : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The rooms with a TV on the wall (StationTV), by their markers in the layout: Sonny's eyes, and its voice.")]
    public string tvRooms = "hkbmcdgro";
    [Tooltip("Across the top of the screen as the chapter starts.")]
    public string heading = "Chapter 3";
    public float headingSeconds = 3f;
    [Tooltip("The reactor core's marker in the layout: walking in from Chapter 2, or straight from this scene, it starts there.")]
    public char reactorRoom = 'r';

    IEnumerator Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        ChapterTwoDirector.DressStation(gameObject, tvRooms);
        TutorialHud hud = TutorialHud.Get();
        hud.SetFade(1f);
        player.SetScriptedInput(Vector2.zero, false);
        yield return null;

        bool resuming = SaveGame.ResumePending;
        SaveGame.Checkpoint checkpoint = resuming ? SaveGame.Resuming : null;
        Vector2? walkedIn = Teleporter.TakeArrival();
        if (resuming && !string.IsNullOrEmpty(checkpoint.stage)) FillStage(checkpoint);
        if (resuming) Inventory.Load(checkpoint.inventory);
        else if (walkedIn == null) Inventory.Reset();

        var comms = FindAnyObjectByType<CommsRing>();
        string next = resuming ? checkpoint.nextObjective : comms != null ? comms.afterObjective : "";
        if (string.IsNullOrEmpty(next) && comms != null) next = comms.afterObjective;
        BlockedWay passed = ChapterTwoDirector.CatchUp(next);
        if (passed != null && passed.arriveInRoom != '\0' && IsSonnysRoom(passed.arriveInRoom))
        {
            var lockdown = FindAnyObjectByType<ReactorLockdown>();
            if (lockdown != null) lockdown.SkipToCleared();
            else SuitHelper.Wear = SuitHelper.Condition.Gone;
        }
        else if (resuming && ReactorArrival() is BlockedWay arrived) arrived.ResumePassed();

        if (resuming) SaveGame.PlacePlayer(checkpoint);
        else if (walkedIn is Vector2 at) Place(at);
        else if (ReactorArrival() is BlockedWay inside) Place(inside.standAt);
        if (SuitHelper.Wear != SuitHelper.Condition.Gone) SuitHelper.Get().ComeOnline();
        else StartCoroutine(SuitHelper.Get().GoOffline(true));
        if (resuming) SaveGame.DoneResuming();

        player.ClearScriptedInput();
        if (resuming)
        {
            yield return hud.FadeTo(0f, 1.2f);
            yield break;
        }
        SaveGame.ChapterStarted(next);
        StartCoroutine(hud.FadeTo(0f, 0.8f));
        yield return new WaitForSeconds(0.4f);
        yield return hud.Heading(heading, headingSeconds);
    }

    void Place(Vector2 at)
    {
        player.transform.position = new Vector3(at.x, at.y, player.transform.position.z);
        if (player.TryGetComponent(out Rigidbody2D body)) body.position = at;
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraFallow follow)) follow.SnapToPlayer();
    }

    BlockedWay ReactorArrival() => System.Array.Find(FindObjectsByType<BlockedWay>(), w => w.arriveInRoom == reactorRoom);

    static bool IsSonnysRoom(char marker)
    {
        var sonny = FindAnyObjectByType<SonnyBox>();
        StationMap map = StationMap.Current;
        return sonny != null && map != null && map.Locate(sonny.transform.position, out _, out StationMap.Room room) && room.marker == marker;
    }

    void FillStage(SaveGame.Checkpoint checkpoint)
    {
        var comms = FindAnyObjectByType<CommsRing>();
        var lockdown = FindAnyObjectByType<ReactorLockdown>();
        BlockedWay control = System.Array.Find(FindObjectsByType<BlockedWay>(), w => w.stage == "control-room");
        Vector2? at = null;
        string next = comms != null ? comms.afterObjective : null;
        switch (checkpoint.stage)
        {
            case "reactor-lockdown":
                at = lockdown != null ? lockdown.standAt : (Vector2?)null;
                break;
            case "reactor-cleared":
                at = lockdown != null ? lockdown.clearedStandAt : (Vector2?)null;
                if (lockdown != null) lockdown.SkipToCleared();
                break;
            case "control-room":
                next = control != null ? control.objective : null;
                at = control != null ? control.standAt : (Vector2?)null;
                break;
            default:
                next = null;
                break;
        }
        if (next == null) Debug.LogWarning($"{name}: there's no stage called \"{checkpoint.stage}\" in Chapter 3 (or what it needs isn't in the scene).", this);
        Inventory.Reset();
        foreach (string made in new[] { Workshop.EmpId, Workshop.CrowbarId, Workshop.BlasterId }) Inventory.Grant(made);
        checkpoint.inventory = Inventory.Save();
        checkpoint.nextObjective = next ?? "";
        checkpoint.hasPosition = at.HasValue;
        if (at.HasValue)
        {
            checkpoint.x = at.Value.x;
            checkpoint.y = at.Value.y;
        }
    }
}
