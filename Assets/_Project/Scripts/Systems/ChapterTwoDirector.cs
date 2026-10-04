using System.Collections;
using UnityEngine;

// Starts Chapter 2, a few hours after Chapter 1 ends (RestroomBreak): the technician wakes up on the storage room floor,
// where the crate off the shelf knocked them out. The scene opens on black (Chapter 1 has just shown the title card),
// the eyes open, twice, they get up off the floor holding their head, and Pip comes back on in a crackle, unable to
// raise anyone. Then
// getting out of the storage room is the objective, and StorageEscape takes it from there.
// The station is built already the way it is now (ChapterTwoBuilder): emergency lighting, the crew gone, the storage
// room door jammed with rubble behind it. Chapter 2 keeps what the suit had (the map); played straight from this scene
// in the editor, it unlocks it.
// Continuing from a save (SaveGame.Resuming), there's no waking up: the player's back on their feet where they were,
// Pip's on, and the chapter picks up after the objective that was saved (StorageEscape, PatrolReveal, BlockedWay,
// Workshop, BadgeLockout, CommsRing).
// Built by ChapterTwoBuilder.
public class ChapterTwoDirector : MonoBehaviour
{
    public PlayerController player;

    [Header("Waking up")]
    [Tooltip("Which way they're lying when the scene opens: 90 on one side, -90 the other.")]
    public float lyingAngle = 90f;
    [Tooltip("Played straight from this scene: the title card on black first, as Chapter 1 would have shown it.")]
    public string[] titleCard = { "Chapter 2", "A few hours later" };
    public float titleHoldSeconds = 1.8f;
    [Tooltip("The technician, getting up.")]
    [TextArea] public string[] technicianWakeUp = { "Ugh... my head." };
    [Tooltip("Pip, coming back on. ~ opens a line with static; ^ says it happily.")]
    [TextArea] public string[] pipWakeUp =
    {
        "~Tech. You were out for hours.",
        "~Something's wrong. Get out of this room.",
    };
    public string objectiveWakeUp = "Get out of the storage room";
    [Tooltip("Seconds the prompt for Pip's log (PipLog) stays up after waking, unless it's opened first.")]
    public float logPromptSeconds = 10f;

    // Once they're back on their feet and Pip's said its piece (StorageEscape).
    public static event System.Action WokeUp;

    // Set by whatever loads this scene straight after Chapter 1's title card, so it isn't shown twice.
    public static bool ArrivingFromChapterOne;

    IEnumerator Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        SuitFeatures.Unlock(SuitFeatures.Feature.Map);
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

        Inventory.Reset();      // from the top: nothing scavenged yet
        if (!ArrivingFromChapterOne) yield return hud.TitleCard(titleCard, titleHoldSeconds);
        ArrivingFromChapterOne = false;
        yield return WakeUp(hud);
    }

    // Continuing from a checkpoint. Which one is told mostly by the objective that came next, since the way out of the
    // storage room can be finished two ways (trying the door first, or going straight for the vent).
    IEnumerator Resume(TutorialHud hud, SaveGame.Checkpoint checkpoint)
    {
        yield return null;      // so everything else in the scene has started, and can be set up
        player.transform.rotation = Quaternion.identity;
        if (!string.IsNullOrEmpty(checkpoint.stage)) FillStage(checkpoint);
        Inventory.Load(checkpoint.inventory);
        SaveGame.PlacePlayer(checkpoint);
        if (player.TryGetComponent(out PlayerHealthHandler handler)) handler.RespawnPoint = player.transform.position;
        SuitHelper.Get().ComeOnline();

        var escape = FindAnyObjectByType<StorageEscape>();
        var reveal = FindAnyObjectByType<PatrolReveal>();
        string next = checkpoint.nextObjective;
        // The last blocked way found, if it was one of those (the exit, then the way to the control room).
        BlockedWay passed = null;
        foreach (BlockedWay way in FindObjectsByType<BlockedWay>())
            if (!string.IsNullOrEmpty(way.objective) && next == way.objective) passed = way;
        // Or somewhere in scavenging and making the EMP on the upper maintenance deck.
        var crafting = FindAnyObjectByType<Workshop>();
        bool craftingStage = crafting != null && crafting.Owns(next);
        // Or past it: locked out of the control room and up in the comms ring, or let into the control room after.
        var lockout = FindAnyObjectByType<BadgeLockout>();
        var comms = FindAnyObjectByType<CommsRing>();
        bool commsStage = comms != null && comms.Owns(next);
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
        SaveGame.DoneResuming();

        player.ClearScriptedInput();
        yield return hud.FadeTo(0f, 1.2f);
    }

    // A tester's jump (CheckpointJump): the stage as the objectives either side of it, as if it had been saved there, and
    // where the player would be standing.
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
                // The hallway's caved in and it's upstairs next; standing a few steps short of where the bot comes out.
                next = StageObjective("hallway-caved");
                var grab = FindAnyObjectByType<WallGrab>();
                at = grab != null ? grab.triggerAt + Vector2.down * (grab.triggerRange + 1.5f) : (Vector2?)null;
                break;
            case "control-door":
                // The EMP made, a few steps from the reactor core's badge door, about to be turned away by it.
                var workshop = FindAnyObjectByType<Workshop>();
                var locked = FindAnyObjectByType<BadgeLockout>();
                next = workshop != null ? workshop.afterObjective : null;
                at = locked != null ? locked.standAt : (Vector2?)null;
                break;
            case "comms-ring":
            case "comms-generator":
            case "comms-power":
            case "comms-relay":
            case "comms-done":
                // In the comms ring: just arrived (the lights about to die), in the dark at the generator, the power
                // back with no broadcasts in yet, every broadcast in and the relay to answer, or the relay passed.
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
                // Up on the deck, at the bench, with enough of every part for everything on it.
                var bench = FindAnyObjectByType<Workshop>();
                next = StageObjective("upper-deck");
                at = bench != null ? bench.benchStandAt : (Vector2?)null;
                if (bench != null)
                {
                    Inventory.Reset();
                    int part = 0;
                    foreach (Inventory.Stack[] cost in new[] { bench.empCost, bench.crowbarCost, bench.scrapGunCost })
                        foreach (Inventory.Stack need in cost)
                            Inventory.Add($"tester-{part++}", need.material, need.count);
                    checkpoint.inventory = Inventory.Save();
                }
                break;
            default:
                // Found one of the blocked ways: standing in front of it, with where it sends them next.
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

    // Where the blocked way that's this stage sends them next.
    static string StageObjective(string stage)
    {
        foreach (BlockedWay way in FindObjectsByType<BlockedWay>())
            if (way.stage == stage) return way.objective;
        return null;
    }

    // Eyes opening, twice, then up off the floor, and Pip coming back on.
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
        yield return pip.Say(pipWakeUp);
        player.ClearScriptedInput();
        hud.SetObjective(objectiveWakeUp);
        WokeUp?.Invoke();
        StartCoroutine(ShowLogPrompt(hud));
    }

    // How to read back what Pip's said: up for a while, or until they look.
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
