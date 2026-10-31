using System.Collections;
using UnityEngine;

// runs chapter 2, up to the reactor door
public class ChapterTwoDirector : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The rooms with a TV on the wall (StationTV), by their markers in the layout: Sonny's eyes, and its voice.")]
    public string tvRooms = "hkbmcdgro";

    [Header("Waking up")]
    [Tooltip("Which way they're lying when the scene opens: 90 on one side, -90 the other.")]
    public float lyingAngle = 90f;
    [Tooltip("Played straight from this scene: the title card on black first, as Chapter 1 would have shown it.")]
    public string[] titleCard = { "Chapter 2", "A few hours later" };
    public float titleHoldSeconds = 1.8f;
    [Tooltip("The technician, getting up.")]
    [TextArea] public string[] technicianWakeUp = { "Ugh... my head.", "...And my badge is dead, light's out" };
    public string objectiveWakeUp = "Get out of the storage room";
    [Tooltip("Seconds the prompt for Pip's log (PipLog) stays up after waking, unless it's opened first.")]
    public float logPromptSeconds = 10f;

    public static event System.Action WokeUp;

    public static bool ArrivingFromChapterOne;

    IEnumerator Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        DressStation(gameObject, tvRooms);
        BreakBadge();
        TutorialHud hud = TutorialHud.Get();
        hud.SetFade(1f);
        player.SetScriptedInput(Vector2.zero, false);
        player.transform.rotation = Quaternion.Euler(0f, 0f, lyingAngle);
        if (SaveGame.ResumePending)
        {
            ArrivingFromChapterOne = false;
            yield return Resume(hud, SaveGame.Resuming);
            yield break;
        }

        Inventory.Reset();
        if (!ArrivingFromChapterOne) yield return hud.TitleCard(titleCard, titleHoldSeconds);
        ArrivingFromChapterOne = false;
        yield return WakeUp(hud);
    }

    public static void DressStation(GameObject on, string tvRooms)
    {
        if (!on.TryGetComponent(out StationGloom _)) on.AddComponent<StationGloom>();
        SonnyBroadcast.PlaceTVs(tvRooms, true);
        if (!on.TryGetComponent(out StationOccupants _)) on.AddComponent<StationOccupants>();
        if (!on.TryGetComponent(out Unexplained _)) on.AddComponent<Unexplained>();
        if (!on.TryGetComponent(out CrewEchoes _)) on.AddComponent<CrewEchoes>();
        SuitFeatures.Unlock(SuitFeatures.Feature.Map);
    }

    void BreakBadge()
    {
        AccessCredentials.Grant(AccessCredentials.TempBadge);
        AccessCredentials.Revoke(AccessCredentials.TempBadgeAccess);
        var comms = FindAnyObjectByType<CommsRing>();
        if (comms == null) return;
        foreach (string credential in comms.alsoGrants) AccessCredentials.Revoke(credential);
        if (comms.relay != null && comms.relay.doctrine != null) AccessCredentials.Revoke(comms.relay.doctrine.credential);
    }

    IEnumerator Resume(TutorialHud hud, SaveGame.Checkpoint checkpoint)
    {
        yield return null;
        player.transform.rotation = Quaternion.identity;
        if (!string.IsNullOrEmpty(checkpoint.stage)) FillStage(checkpoint);
        Inventory.Load(checkpoint.inventory);
        SaveGame.PlacePlayer(checkpoint);
        if (player.TryGetComponent(out PlayerHealthHandler handler)) handler.RespawnPoint = player.transform.position;
        SuitHelper.Get().ComeOnline();

        CatchUp(checkpoint.nextObjective);
        SaveGame.DoneResuming();

        player.ClearScriptedInput();
        yield return hud.FadeTo(0f, 1.2f);
    }

    public static BlockedWay CatchUp(string next)
    {
        var escape = FindAnyObjectByType<StorageEscape>();
        var reveal = FindAnyObjectByType<PatrolReveal>();
        BlockedWay passed = null;
        foreach (BlockedWay way in FindObjectsByType<BlockedWay>())
            if (!string.IsNullOrEmpty(way.objective) && next == way.objective) passed = way;
        var lockout = FindAnyObjectByType<BadgeLockout>();
        var comms = FindAnyObjectByType<CommsRing>();
        bool commsStage = comms != null && comms.Owns(next);
        var crafting = FindAnyObjectByType<Workshop>();
        bool craftingStage = crafting != null && crafting.Owns(next) && !commsStage;
        bool pastComms = passed != null && comms != null && passed.afterObjective == comms.afterObjective;
        bool pastDeck = commsStage || pastComms;
        bool revealed = craftingStage || pastDeck || passed != null || (reveal != null && next == reveal.objective);
        bool escaped = revealed || (escape != null && next == escape.objectiveOut);

        if (escape != null)
        {
            if (!escaped) escape.ResumeAtVent();
            else escape.ResumeOut(!revealed);
        }
        if (revealed && reveal != null) reveal.ResumeRevealed(passed == null && !craftingStage && !pastDeck);
        if (craftingStage) crafting.Resume(next);
        else if (pastDeck && crafting != null) crafting.ResumePast();
        if (pastDeck && lockout != null) lockout.ResumeLockedOut();
        if (commsStage) comms.Resume(next);
        else if (pastComms) comms.Resume(next, true);
        if (passed != null) passed.ResumePassed();
        if (pastDeck && crafting != null) crafting.SkipUnlockedLine();
        if (pastComms) SuitHelper.Worsen(SuitHelper.Condition.Damaged);
        return passed;
    }

    void FillStage(SaveGame.Checkpoint checkpoint)
    {
        var escape = FindAnyObjectByType<StorageEscape>();
        var reveal = FindAnyObjectByType<PatrolReveal>();
        Vector2? OutOf(VentGrate vent) => vent != null ? (Vector2)vent.transform.position + vent.exitOffset : (Vector2?)null;
        Vector2? at = null;
        string next = null;
        switch (checkpoint.stage)
        {
            case "vent":
                next = escape != null ? escape.objectiveVent : null;
                at = escape != null ? OutOf(escape.storageVent) : null;
                break;
            case "out":
                next = escape != null ? escape.objectiveOut : null;
                at = escape != null ? OutOf(escape.restroomVent) : null;
                break;
            case "patrols":
                next = reveal != null ? reveal.objective : null;
                at = escape != null ? OutOf(escape.restroomVent) : null;
                break;
            case "emp-built":
                var crafting = FindAnyObjectByType<Workshop>();
                next = crafting != null ? crafting.afterObjective : null;
                at = crafting != null ? crafting.benchStandAt : (Vector2?)null;
                break;
            case "wall-grab":
                next = StageObjective("hallway-caved");
                var grab = FindAnyObjectByType<WallGrab>();
                at = grab != null ? grab.triggerAt + Vector2.down * (grab.triggerRange + 1.5f) : (Vector2?)null;
                break;
            case "comms-ring":
            case "comms-generator":
            case "comms-power":
            case "comms-relay":
            case "comms-done":
                var ring = FindAnyObjectByType<CommsRing>();
                if (ring == null) break;
                switch (checkpoint.stage)
                {
                    case "comms-ring": next = ring.arriveObjective; at = ring.arriveStandAt; break;
                    case "comms-generator": next = ring.generatorObjective; at = ring.generatorStandAt; break;
                    case "comms-power": next = ring.InterceptText(0); at = ring.arriveStandAt; break;
                    case "comms-relay": next = ring.answerObjective; at = ring.relayStandAt; break;
                    default: next = ring.afterObjective; at = ring.relayStandAt; break;
                }
                break;
            case "bench-stocked":
                var bench = FindAnyObjectByType<Workshop>();
                next = StageObjective("upper-deck");
                at = bench != null ? bench.benchStandAt : (Vector2?)null;
                if (bench != null)
                {
                    Inventory.Reset();
                    int part = 0;
                    foreach (Inventory.Stack[] cost in bench.crowbarOnBench ? new[] { bench.empCost, bench.crowbarCost, bench.blasterCost } : new[] { bench.empCost, bench.blasterCost })
                        foreach (Inventory.Stack need in cost)
                            Inventory.Add($"tester-{part++}", need.material, need.count);
                    checkpoint.inventory = Inventory.Save();
                }
                break;
            default:
                foreach (BlockedWay way in FindObjectsByType<BlockedWay>())
                {
                    if (way.stage != checkpoint.stage) continue;
                    next = way.objective;
                    at = way.standAt;
                }
                break;
        }
        if (next == null) Debug.LogWarning($"{name}: there's no stage called \"{checkpoint.stage}\" in Chapter 2 (or what it needs isn't in the scene).", this);
        checkpoint.finishedObjective = objectiveWakeUp;
        checkpoint.nextObjective = next ?? "";
        checkpoint.hasPosition = at.HasValue;
        if (at.HasValue)
        {
            checkpoint.x = at.Value.x;
            checkpoint.y = at.Value.y;
        }
    }

    static string StageObjective(string stage)
    {
        foreach (BlockedWay way in FindObjectsByType<BlockedWay>())
            if (way.stage == stage) return way.objective;
        return null;
    }

    IEnumerator WakeUp(TutorialHud hud)
    {
        yield return Wait(0.6f);
        yield return hud.FadeTo(0.55f, 1.2f);
        yield return hud.FadeTo(0.95f, 0.5f);
        yield return Wait(0.4f);
        yield return hud.FadeTo(0.3f, 1f);
        yield return Wait(0.3f);
        StartCoroutine(hud.FadeTo(0f, 1.2f));

        for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
        {
            player.transform.rotation = Quaternion.Euler(0f, 0f, lyingAngle * (1f - Mathf.SmoothStep(0f, 1f, t / 1.1f)));
            yield return null;
        }
        player.transform.rotation = Quaternion.identity;
        CameraShake.Shake(0.15f);
        yield return Wait(0.4f);
        yield return TechnicianVoice.Think(technicianWakeUp);

        bool wasOn = SuitHelper.Exists;
        SuitHelper pip = SuitHelper.Get();
        if (!wasOn) pip.ComeOnline();
        pip.Glitch(0.6f);
        yield return pip.Say(PipManager.Lines.wakeUp);
        player.ClearScriptedInput();
        hud.SetObjective(objectiveWakeUp);
        WokeUp?.Invoke();
        StartCoroutine(ShowLogPrompt(hud));
    }

    IEnumerator ShowLogPrompt(TutorialHud hud)
    {
        hud.ShowPromptWithHint("Pip's log", "Read back anything Pip's said", PipLog.LogKey.ToString());
        for (float t = 0f; t < logPromptSeconds && !PipLog.IsOpen; t += Time.unscaledDeltaTime) yield return null;
        hud.CompletePrompt();
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }
}
