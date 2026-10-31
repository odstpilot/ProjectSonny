using System.Collections;
using UnityEngine;

// runs chapter 1
public class ChapterOneDirector : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The rooms with a TV on the wall (StationTV), by their markers in the layout.")]
    public string tvRooms = "ohkm";
    [Tooltip("Where the player walks to on their way in, before they're given control.")]
    public Transform walkInTarget;
    public CrewMember greeter;

    [Header("Arrival")]
    [Tooltip("Shown on black, one line after another, before the scene fades up.")]
    public string[] titleCard = { "Chapter 1", "Arrival" };
    public float titleHoldSeconds = 1.2f;
    public float fadeUpSeconds = 1.4f;
    [Tooltip("The scene's sound played as the entrance opens and the player steps through. Empty for none.")]
    [SoundName] public string entranceSound = "Entrance Door";
    [Tooltip("Plays the pod docking with the station after the title card.")]
    public bool playArrivalCutscene = true;
    [Tooltip("Skips the title card and the cutscene in the editor, for quick testing.")]
    public bool skipTitleInEditor = false;

    [Header("Objectives")]
    public string objectiveBeforeGreeting = "Talk to the quartermaster by the entrance";
    [Tooltip("The technician, trying a door before they've talked to the greeter: every door's shut till then.")]
    public string heldDoorLine = "Uhh I should probably check in with the quartermaster first... the guy waving at me over there";
    public string objectiveOpenMap = "Open your map";
    public string objectiveAfterGreeting = "Report to the control room";

    [Header("Pip")]
    [Tooltip("Has the technician open the map and look before the control room's the objective. Off to keep Chapter 1 short.")]
    public bool requireMapLook = false;
    [Tooltip("The technician, to themselves, once they've closed the map.")]
    [TextArea] public string[] technicianAfterMap = { "Through the maintenance deck, got it" };
    [Tooltip("The room Pip marks on the map, by its marker in the layout, and what the pin over it says.")]
    public char mapTarget = 'o';
    public string mapTargetLabel = "Control Room";

    private Coroutine meetPip;

    void OnEnable() => ControlRoomCutscene.Started += StandDown;

    void OnDisable()
    {
        ControlRoomCutscene.Started -= StandDown;
        ReleaseDoors();
    }

    void StandDown()
    {
        if (meetPip != null) StopCoroutine(meetPip);
        meetPip = null;
        if (greeter != null) greeter.Greeted -= OnGreeted;
        ReleaseDoors();
        SuitFeatures.Unlock(SuitFeatures.Feature.Map);
    }

    void HoldDoors()
    {
        Teleporter.HoldAll(heldDoorLine);
        Teleporter.HeldDoorTried -= CallOverAgain;
        Teleporter.HeldDoorTried += CallOverAgain;
    }

    void ReleaseDoors()
    {
        Teleporter.HeldDoorTried -= CallOverAgain;
        Teleporter.ReleaseAll();
    }

    void CallOverAgain()
    {
        if (greeter != null) greeter.CallOut();
    }

    IEnumerator Start()
    {
        SonnyBroadcast.PlaceTVs(tvRooms, false);
        TutorialHud hud = TutorialHud.Get();
        hud.SetFade(1f);
        if (player != null) player.SetScriptedInput(Vector2.zero, false);
        if (SaveGame.ResumePending)
        {
            yield return Resume(hud, SaveGame.Resuming);
            yield break;
        }

        if (greeter != null && !greeter.HasGreeted) HoldDoors();
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
            greeter.CallOut();
        }
    }

    IEnumerator Resume(TutorialHud hud, SaveGame.Checkpoint checkpoint)
    {
        yield return null;
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
            case "urge": finished = cutscene == null ? null : string.IsNullOrEmpty(cutscene.objectiveAfter) ? RestroomBreak.Breather : cutscene.objectiveAfter; at = controlRoom; break;
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
        ReleaseDoors();
        meetPip = StartCoroutine(MeetPip());
    }

    IEnumerator MeetPip()
    {
        TutorialHud hud = TutorialHud.Get();
        hud.SetObjective("");
        while (DialogueBox.Busy) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);

        SuitHelper pip = SuitHelper.Get();
        yield return pip.StartCoroutine(pip.Boot());
        MapScreen.SetTarget(mapTarget, mapTargetLabel);
        bool opened = false;
        void Looked() => opened = true;
        MapScreen.Opened += Looked;
        yield return pip.Say(PipManager.Lines.hello);

        SuitFeatures.Unlock(SuitFeatures.Feature.Map);
        if (!requireMapLook)
        {
            MapScreen.Opened -= Looked;
            hud.SetObjective(objectiveAfterGreeting);
            TechnicianVoice.Say(technicianAfterMap.Length > 0 ? technicianAfterMap[0] : "");
            meetPip = null;
            yield break;
        }
        if (!opened && !MapScreen.IsOpen)
        {
            hud.SetObjective(objectiveOpenMap);
            hud.ShowPrompt("Open map", "M");
            pip.Tell(PipManager.Lines.openMap);
            while (!opened && !MapScreen.IsOpen) yield return null;
        }
        MapScreen.Opened -= Looked;
        hud.CompletePrompt();
        pip.Hush();
        pip.Tell(PipManager.Lines.onMap);

        while (MapScreen.IsOpen) yield return null;
        pip.Hush();
        hud.SetObjective(objectiveAfterGreeting);
        if (PipManager.Lines.afterMap.Length > 0) pip.Tell(PipManager.Lines.afterMap);
        TechnicianVoice.Say(technicianAfterMap.Length > 0 ? technicianAfterMap[0] : "");
        meetPip = null;
    }
}
