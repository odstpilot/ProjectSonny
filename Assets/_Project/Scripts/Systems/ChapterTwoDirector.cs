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
// Pip's on, and the chapter picks up after the objective that was saved (StorageEscape, CrewAftermath, BadgeLockout).
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
        "~Tech! You've been out for hours.",
        "~Nobody's answering on the crew channel. Nobody. And we're on emergency power.",
    };
    public string objectiveWakeUp = "Get out of the storage room";

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
        SaveGame.PlacePlayer(checkpoint);
        if (player.TryGetComponent(out PlayerHealthHandler handler)) handler.RespawnPoint = player.transform.position;
        SuitHelper.Get().ComeOnline();

        var escape = FindAnyObjectByType<StorageEscape>();
        var aftermath = FindAnyObjectByType<CrewAftermath>();
        var lockout = FindAnyObjectByType<BadgeLockout>();
        string done = checkpoint.finishedObjective, next = checkpoint.nextObjective;
        bool lockedOut = lockout != null && (next == lockout.objective || done == lockout.objective);
        bool realized = lockedOut || (aftermath != null && next == aftermath.objectiveNext);
        bool escaped = realized || (escape != null && next == escape.objectiveOut);

        if (escape != null)
        {
            if (!escaped) escape.ResumeAtVent();
            else escape.ResumeOut(!realized);
        }
        if (realized && !lockedOut && aftermath != null) aftermath.ResumeRealized();
        if (lockedOut) lockout.ResumeLockedOut();
        SaveGame.DoneResuming();

        player.ClearScriptedInput();
        yield return hud.FadeTo(0f, 1.2f);
    }

    // A tester's jump (CheckpointJump): the stage as the objectives either side of it, as if it had been saved there, and
    // where the player would be standing.
    void FillStage(SaveGame.Checkpoint checkpoint)
    {
        var escape = FindAnyObjectByType<StorageEscape>();
        var aftermath = FindAnyObjectByType<CrewAftermath>();
        var lockout = FindAnyObjectByType<BadgeLockout>();
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
            case "to-control-room":
                next = aftermath != null ? aftermath.objectiveNext : null;
                at = escape != null ? OutOf(escape.restroomVent) : null;
                break;
            case "lockout":
                next = lockout != null ? lockout.objective : null;
                if (lockout != null && lockout.door != null)
                    at = (Vector2)lockout.door.transform.position - lockout.door.wallDirection * 1.5f;
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
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }
}
