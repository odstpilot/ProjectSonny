using System.Collections;
using UnityEngine;

// Runs Chapter 1's arrival. A title card on black, then the pod crossing to the station and docking (ArrivalCutscene),
// then the fade up on the ship entrance as the technician steps aboard, unarmed, into a common grounds full of crew going
// about their day. The crew member waiting by the entrance (the
// greeter) calls them over; talking to them (E) gets the hello, and they switch on Pip, the helper in the technician's
// suit (SuitHelper). Pip boots up in the top right corner, says hello, unlocks the suit's map, and has the technician
// open it (M) to see where the control room is; once they've looked and closed it, getting there is the objective.
// Walking into the control room plays the install (ControlRoomCutscene); if that happens before Pip's finished showing
// the map, Pip's tour stops there, with the map left unlocked.
// Continuing from a save (SaveGame.Resuming), it skips the arrival and sets the chapter up the way it was just after
// the objective that was saved: Pip's tour, the way to the control room, or the break after the install
// (ControlRoomCutscene.SkipToAfter, RestroomBreak.ResumeFrom). The player's put back where they were.
// Built by ChapterOneBuilder.
public class ChapterOneDirector : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("Where the player walks to on their way in, before they're given control.")]
    public Transform walkInTarget;
    public CrewMember greeter;

    [Header("Arrival")]
    [Tooltip("Shown on black, one line after another, before the scene fades up.")]
    public string[] titleCard = { "Chapter 1", "Arrival" };
    public float titleHoldSeconds = 1.6f;
    public float fadeUpSeconds = 1.4f;
    [Tooltip("The scene's sound played as the entrance opens and the player steps through. Empty for none.")]
    [SoundName] public string entranceSound = "Entrance Door";
    [Tooltip("Plays the pod docking with the station after the title card.")]
    public bool playArrivalCutscene = true;
    [Tooltip("Skips the title card and the cutscene in the editor, for quick testing.")]
    public bool skipTitleInEditor = false;

    [Header("Objectives")]
    public string objectiveBeforeGreeting = "Check in with the crew by the entrance";
    public string objectiveOpenMap = "Open your map";
    public string objectiveAfterGreeting = "Report to the control room";

    [Header("Pip")]
    [Tooltip("Pip's first words, once it's booted. A line starting with ^ is said happily; [M] shows a key.")]
    [TextArea] public string[] pipHello =
    {
        "^Bzzt! Pip online! Your suit's Personal Integrated Pal.",
        "Control room's clear across the station. Let's get you a map.",
    };
    [TextArea] public string[] pipOpenMap = { "Press [M] to open your map!" };
    [Tooltip("Said while the map's open for the first time.")]
    [TextArea] public string[] pipOnMap =
    {
        "^Blinking dot's you. Control room's marked in orange. [W] [A] [S] [D] to look around, scroll to zoom.",
        "[M] again to close it.",
    };
    [TextArea] public string[] pipAfterMap = new string[0];
    [Tooltip("The technician, to themselves, once they've closed the map.")]
    [TextArea] public string[] technicianAfterMap = { "East hallway, then the maintenance deck. Got it." };
    [Tooltip("The room Pip marks on the map, by its marker in the layout, and what the pin over it says.")]
    public char mapTarget = 'o';
    public string mapTargetLabel = "Control Room";

    private Coroutine meetPip;

    void OnEnable() => ControlRoomCutscene.Started += StandDown;
    void OnDisable() => ControlRoomCutscene.Started -= StandDown;

    void StandDown()
    {
        if (meetPip != null) StopCoroutine(meetPip);
        meetPip = null;
        if (greeter != null) greeter.Greeted -= OnGreeted;
        SuitFeatures.Unlock(SuitFeatures.Feature.Map);
    }

    IEnumerator Start()
    {
        TutorialHud hud = TutorialHud.Get();
        hud.SetFade(1f);
        if (player != null) player.SetScriptedInput(Vector2.zero, false);
        if (SaveGame.ResumePending)
        {
            yield return Resume(hud, SaveGame.Resuming);
            yield break;
        }

        if (!(skipTitleInEditor && Application.isEditor))
        {
            yield return hud.TitleCard(titleCard, titleHoldSeconds);
            if (playArrivalCutscene) yield return ArrivalCutscene.Play();
        }

        ScreenFade.Get().PlaySound(entranceSound);
        StartCoroutine(hud.FadeTo(0f, fadeUpSeconds));
        yield return WalkIn();
        if (player != null) player.ClearScriptedInput();

        if (greeter == null) meetPip = StartCoroutine(MeetPip());
        else if (greeter.HasGreeted) OnGreeted();
        else
        {
            hud.SetObjective(objectiveBeforeGreeting);
            greeter.Greeted += OnGreeted;
        }
    }

    // Continuing from a checkpoint: everything the way it was just after that objective was finished, the player back
    // where they were, and whatever came next picked up from there. An objective it doesn't know (renamed since the
    // save, say) picks up on the way to the control room.
    IEnumerator Resume(TutorialHud hud, SaveGame.Checkpoint checkpoint)
    {
        yield return null;      // so everything else in the scene has started, and can be set up
        string done = checkpoint.finishedObjective;
        var cutscene = FindAnyObjectByType<ControlRoomCutscene>();
        var restroomBreak = FindAnyObjectByType<RestroomBreak>();
        if (!string.IsNullOrEmpty(checkpoint.stage)) FillStage(checkpoint, cutscene, restroomBreak);
        done = checkpoint.finishedObjective;
        SaveGame.PlacePlayer(checkpoint);
        if (greeter != null) greeter.MarkGreeted();
        if (player != null) player.ClearScriptedInput();

        if (done == objectiveBeforeGreeting)
        {
            SaveGame.DoneResuming();
            meetPip = StartCoroutine(MeetPip());
        }
        else
        {
            SuitHelper.Get().ComeOnline();
            SuitFeatures.Unlock(SuitFeatures.Feature.Map);
            bool installed = cutscene != null && restroomBreak != null && restroomBreak.Knows(done, cutscene.objectiveAfter);
            if (installed)
            {
                cutscene.SkipToAfter();
                restroomBreak.ResumeFrom(done, cutscene.objectiveAfter);
            }
            else
            {
                MapScreen.SetTarget(mapTarget, mapTargetLabel);
                hud.SetObjective(objectiveAfterGreeting);
            }
            SaveGame.DoneResuming();
        }
        yield return hud.FadeTo(0f, fadeUpSeconds);
    }

    // A tester's jump (CheckpointJump): the stage as the objective that would just have been finished, and where the
    // player would be standing, as if it had been saved there.
    void FillStage(SaveGame.Checkpoint checkpoint, ControlRoomCutscene cutscene, RestroomBreak restroomBreak)
    {
        Vector2? at = null;
        Vector2? controlRoom = cutscene != null && cutscene.sonny != null ? (Vector2)cutscene.sonny.transform.position + cutscene.workSpot : (Vector2?)null;
        Vector2? nearGreeter = greeter != null ? greeter.Position + Vector2.left * 1.2f : (Vector2?)null;
        Vector2? InFront(Component thing) => thing != null ? (Vector2)thing.transform.position + Vector2.down * 1.3f : (Vector2?)null;
        string finished = null;
        switch (checkpoint.stage)
        {
            case "greeted": finished = objectiveBeforeGreeting; at = nearGreeter; break;
            case "to-control-room": finished = objectiveOpenMap; at = nearGreeter; break;
            case "install": finished = objectiveAfterGreeting; at = controlRoom + Vector2.down; break;
            case "breather": finished = RestroomBreak.Breather; at = controlRoom; break;
            case "urge": finished = cutscene != null ? cutscene.objectiveAfter : null; at = controlRoom; break;
        }
        if (restroomBreak != null)
        {
            switch (checkpoint.stage)
            {
                case "restroom": finished = restroomBreak.objectiveUrge; at = InFront(restroomBreak.toilet); break;
                case "storage": finished = restroomBreak.objectiveFix; at = InFront(restroomBreak.crate != null ? (Component)restroomBreak.crate : restroomBreak.shelf); break;
                case "crate": finished = restroomBreak.objectiveSearch; at = InFront(restroomBreak.crate); break;
                case "shelf": finished = restroomBreak.objectiveShelf; at = InFront(restroomBreak.shelf); break;
            }
        }
        if (finished == null) Debug.LogWarning($"{name}: there's no stage called \"{checkpoint.stage}\" in Chapter 1 (or what it needs isn't in the scene).", this);
        checkpoint.finishedObjective = finished ?? "";
        checkpoint.hasPosition = at.HasValue;
        if (at.HasValue)
        {
            checkpoint.x = at.Value.x;
            checkpoint.y = at.Value.y;
        }
    }

    // Walks the player in off the entrance to walkInTarget, giving up after a few seconds if something's in the way.
    IEnumerator WalkIn()
    {
        if (player == null || walkInTarget == null) yield break;
        const float GiveUpAfter = 3f;
        for (float t = 0f; t < GiveUpAfter; t += Time.deltaTime)
        {
            Vector2 offset = walkInTarget.position - player.transform.position;
            if (offset.magnitude < 0.15f) break;
            player.SetScriptedInput(offset.normalized, false);
            yield return null;
        }
        player.SetScriptedInput(Vector2.zero, false);
    }

    void OnGreeted()
    {
        if (greeter != null) greeter.Greeted -= OnGreeted;
        meetPip = StartCoroutine(MeetPip());
    }

    // Pip boots, says hello, and shows the technician the map. The map's there from the moment Pip mentions it, so
    // pressing M early is fine; the objective moves on once they've had a look and closed it.
    IEnumerator MeetPip()
    {
        TutorialHud hud = TutorialHud.Get();
        hud.SetObjective("");
        while (DialogueBox.Busy) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);

        SuitHelper pip = SuitHelper.Get();
        // Run by Pip itself, so it finishes booting even if the control room cutscene stops this tour partway.
        yield return pip.StartCoroutine(pip.Boot());
        MapScreen.SetTarget(mapTarget, mapTargetLabel);
        bool opened = false;
        void Looked() => opened = true;
        MapScreen.Opened += Looked;
        yield return pip.Say(pipHello);

        SuitFeatures.Unlock(SuitFeatures.Feature.Map);
        if (!opened && !MapScreen.IsOpen)
        {
            hud.SetObjective(objectiveOpenMap);
            hud.ShowPrompt("Open map", "M");
            pip.Tell(pipOpenMap);
            while (!opened && !MapScreen.IsOpen) yield return null;
        }
        MapScreen.Opened -= Looked;
        hud.CompletePrompt();
        pip.Hush();
        pip.Tell(pipOnMap);

        while (MapScreen.IsOpen) yield return null;
        pip.Hush();
        hud.SetObjective(objectiveAfterGreeting);
        if (pipAfterMap.Length > 0) pip.Tell(pipAfterMap);
        TechnicianVoice.Say(technicianAfterMap.Length > 0 ? technicianAfterMap[0] : "");
        meetPip = null;
    }
}
