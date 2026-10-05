using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Chapter 1's break, after the install (ControlRoomCutscene). With skipRestroom (the default, to keep Chapter 1 short),
// the first two parts are skipped: the chief asks for the spare coupling for Sonny as the cutscene ends, and it picks up at the storage room. (The
// storage room lines and objectives below are written for the coupling; with skipRestroom off, Okafor asks for a wrench.)
//  - A breather: the technician is free to walk about and talk to the crew, who all have something new to say now that
//    Sonny's on. After a while of it (exploreSeconds of free time, or talking to talksBeforeUrge of them), the technician
//    realizes they need the restroom, badly: the screen gives a jolt, they say so, and the restroom (its own room, off
//    the kitchen and lounge) is pinned on the map. Every so often they mutter about it until they get there.
//  - The restroom has a row of stalls: the one nearest the door out of order all week (its puddle out), the next one
//    shut with someone's feet showing under the door (Okafor, from life support; knocking gets "Occupied!"), and a free
//    one at the end. Using the free one (Toilet, E): a fade to black and a flush. Back up, the stall next door flushes,
//    gurgles, rattles, and floods. Out comes Okafor, who works out that the technician is the new technician and asks
//    them to fix it. They can say yes or no, but Okafor won't let it go, and either way the technician decides to do it:
//    the new tech who fixes things on day one will look good to the chief.
//  - The storage room: the tool crate first (ToolCrate, E), where the wrench should be, and isn't, just a note. Then a
//    glint under the shelf (ToolShelf, E): down on one knee, reaching (E, a few times) until they've got it. A far-off
//    rumble; a moment of quiet; then something big rumbles through the whole station (Sonny taking over, though nobody
//    says so yet): a deep boom, the picture shaking harder and harder, every lamp stuttering. It rocks the rack until the
//    crate on top tips off and lands on the technician's head. They go down, and it all goes black: "Chapter 2",
//    "A few hours later", and Chapter 2's scene loads (wakeUpScene), where they wake up on the storage room floor
//    (ChapterTwoDirector).
// Using a toilet before they need to, or the crate or the shelf before anyone's asked for a wrench, just gets a mutter.
// Continuing from a save partway through (SaveGame), ResumeFrom sets it all up the way it was just after the objective
// that was saved, and picks up from there.
// Built by ChapterOneBuilder, which puts the stalls in the restroom and Okafor in the middle one (inactive until they
// come out), the spot in front of it they come out to, and the crate and the shelf in the storage room.
public class RestroomBreak : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The free stall, the one the technician uses.")]
    public Toilet toilet;
    [Tooltip("The stall Okafor's in. It breaks after the technician's been.")]
    public Toilet occupiedToilet;
    [Tooltip("Okafor, in the occupied stall: inactive until they come out.")]
    public CrewMember attendant;
    [Tooltip("Where Okafor stands once they've come out of the stall.")]
    public Transform attendantComesIn;
    public ToolCrate crate;
    public ToolShelf shelf;

    [Tooltip("Straight to the storage room once Sonny's on: the chief asks for it (ControlRoomCutscene.sendOff), with no breather, " +
        "no urge and no restroom scene. The toilet's broken already and Okafor's out of the stall, as if they'd asked. " +
        "Off plays the whole break.")]
    public bool skipRestroom = true;

    [Header("The breather")]
    [Tooltip("How long they get to walk about before the urge comes on, counted only while they're free to.")]
    public float exploreSeconds = 50f;
    [Tooltip("Or, sooner, once they've talked to this many of the crew.")]
    public int talksBeforeUrge = 3;

    [Header("The urge")]
    public float urgeShake = 0.35f;
    [Tooltip("The technician, when it comes on.")]
    [TextArea] public string[] urgeLines = { "Oof.", "I've been holding that since before the install. Restroom. Now." };
    [Tooltip("Pip, after that. ~ opens a line with static; ^ says it happily.")]
    [TextArea] public string[] pipUrge = { "^Restroom's off the lounge. I pinned it on your map!" };
    [Tooltip("The technician, every nagSeconds until they get there, one after another, the last one again and again.")]
    [TextArea] public string[] nags = { "Walk faster. Walk faster.", "Think dry thoughts. Think dry thoughts." };
    public float nagSeconds = 22f;
    public string objectiveUrge = "Find the restroom";
    [Tooltip("The room pinned on the map for the restroom, by its marker in the layout, and what the pin says.")]
    public char restroomRoom = 'w';
    public string restroomLabel = "Restroom";
    [Tooltip("The technician, if they use the toilet before they need to, and after they've been.")]
    public string notYet = "I don't need to go. Not yet, anyway.";
    public string alreadyWent = "Already went. I'm good.";

    [Header("Next door")]
    [SoundName] public string flushSound = "Toilet Flush";
    public float blackoutSeconds = 2.2f;
    [Tooltip("Okafor, from inside the stall, before and as it goes.")]
    public string beforeFlush = "Here goes nothing...";
    public string asItBreaks = "No. No no no no no!";
    [Tooltip("The technician, as it gives out.")]
    public string technicianHearsIt = "...That didn't sound good.";

    [Header("Asked to fix it")]
    [Tooltip("Okafor, as they come out.")]
    public string attendantCallOut = "Oh, come ON. Not this one too!";
    [Tooltip("Okafor, as a DialogueBox script: * lines are replies for the technician to pick. Keep the last choice as fix it (first) or don't (second): what the technician says after goes by it.")]
    [TextArea] public string[] askToFix =
    {
        "That's two down. Two! There's one toilet left on this whole deck, and I'm not sharing it with the night shift.",
        "Hang on. The suit, no badge... You're the new technician. The one who brought the AI up.",
        "* That's me. = Then you're exactly who I need.",
        "* Who's asking? = Okafor. Life support. The man who's about to ask you a favor.",
        "Could you fix it up for me? Please?",
        "* Sure, I'll fix it. = You're a lifesaver. Tools are in the storage room, the door on the east side of the lounge.",
        "* Not really my job. = Please. Nobody else up here knows which end of a wrench to hold. = And the chief hears about everything that happens on this deck. Everything. = Tools are in the storage room, the door on the east side of the lounge. I'll owe you.",
    };
    [Tooltip("What Okafor says if the player comes back to them.")]
    [TextArea] public string[] attendantAfter = { "Don't mind me. I'll guard the last working one." };
    [Tooltip("The technician, to themselves, after saying yes, and after saying no.")]
    [TextArea] public string[] agreed = { "Toilet duty. On day one.", "...Still. The new tech who fixes things. That'll look good to the chief." };
    [TextArea] public string[] refused = { "...He's not going to let this go, is he.", "Fine. The new tech fixing things on day one? That'll look good to the chief." };
    public string objectiveFix = "Get the spare coupling from the storage room";
    public char toolsRoom = 's';
    public string toolsLabel = "Storage Room";

    [Header("The storage room")]
    [Tooltip("The technician, walking in.")]
    public string enterStorage = "Spare coupling... where would they keep it?";
    public string objectiveSearch = "Find the spare coupling";
    [Tooltip("The technician, at the crate or the shelf before anyone's asked for a wrench.")]
    public string nothingNeeded = "Nothing I need in there.";
    [Tooltip("The technician, opening the crate, and at it again after.")]
    [TextArea] public string[] crateEmpty = { "\"Took the spare coupling. Back soon. -D.\"", "...Wait. Something's shining under that shelf." };
    public string crateStillEmpty = "Still just the note.";
    public string objectiveShelf = "Check under the shelf";
    [Tooltip("The technician, at the shelf before they've looked in the crate.")]
    public string crateFirst = "Tool crate first. That's where the spares live.";

    [Header("Reaching under")]
    [Tooltip("How many presses of E it takes to reach the wrench, and what they say along the way (one per press, from the first).")]
    public int reachPresses = 3;
    [TextArea] public string[] reachLines = { "Come on...", "Almost...", "Got... it..." };
    public string gotIt = "Got it!";

    [Header("The quake")]
    [SoundName] public string rumbleSound = "Distant Rumble";
    public float rumbleShake = 0.18f;
    [Tooltip("The technician, after the far-off rumble.")]
    public string whatWasThat = "...What was that?";
    [Tooltip("Pip, as the rack starts rocking. Cut off by the crate.")]
    [TextArea] public string[] pipShelfWobble = { "~Tech? Something big's moving through the sta-" };
    [Tooltip("The scene's sound for it, a deep boom through the whole station.")]
    [SoundName] public string quakeSound = "Station Quake";
    [Tooltip("How hard the picture shakes at the worst of it, and how long it goes on past the crate hitting.")]
    public float quakeShake = 0.55f;
    public float quakeAfterSeconds = 0.8f;

    [Header("Knocked out")]
    public float hitShake = 1f;
    [Tooltip("How long they lie there with the screen going dark before it cuts to black.")]
    public float goingDownSeconds = 0.9f;
    [Tooltip("Shown on black while they're out.")]
    public string[] timeSkip = { "Chapter 2", "A few hours later" };
    public float timeSkipHoldSeconds = 1.8f;
    [Tooltip("The scene loaded once they're out: Chapter 2, where they wake up.")]
    public string wakeUpScene = "Chapter2";

    // The moment the crate hits.
    public static event System.Action KnockedOut;

    private bool breakBegun, urgent, relieved, asked;
    private readonly HashSet<CrewMember> talkedTo = new HashSet<CrewMember>();

    void OnEnable()
    {
        ControlRoomCutscene.Finished += BeginBreak;
        CrewMember.Talked += OnTalked;
        if (toilet != null) toilet.Used += OnToiletUsed;
        if (crate != null) crate.Used += OnCrateUsed;
        if (shelf != null) shelf.Used += OnShelfUsed;
    }

    void OnDisable()
    {
        ControlRoomCutscene.Finished -= BeginBreak;
        CrewMember.Talked -= OnTalked;
        if (toilet != null) toilet.Used -= OnToiletUsed;
        if (crate != null) crate.Used -= OnCrateUsed;
        if (shelf != null) shelf.Used -= OnShelfUsed;
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (attendant != null) attendant.gameObject.SetActive(false);     // in the stall, until they come out
    }

    void BeginBreak()
    {
        breakBegun = true;
        if (skipRestroom) SendForCoupling();
        else StartCoroutine(Urge());
    }

    // The short way (skipRestroom): the restroom as it is once Okafor's asked, and off to the storage room.
    void SendForCoupling()
    {
        AlreadyAsked();
        if (toilet != null) toilet.Done();
        TutorialHud.Get().SetObjective(objectiveFix);
        MapScreen.SetTarget(toolsRoom, toolsLabel);
        StartCoroutine(WalkIntoStorage());
    }

    void OnTalked(CrewMember who)
    {
        if (breakBegun && !urgent) talkedTo.Add(who);
    }

    // A while into the break, it comes on; then they mutter about it every so often until they get there.
    IEnumerator Urge()
    {
        for (float t = 0f; t < exploreSeconds && talkedTo.Count < talksBeforeUrge; )
        {
            if (Free()) t += Time.deltaTime;
            yield return null;
        }
        while (!Free() || (SuitHelper.Exists && SuitHelper.Get().Talking)) yield return null;

        urgent = true;
        CameraShake.Shake(urgeShake);
        yield return TechnicianVoice.Think(urgeLines);
        if (relieved) yield break;
        FindTheRestroom();
        if (SuitHelper.Exists) SuitHelper.Get().Tell(pipUrge);
        yield return Nag();
    }

    void FindTheRestroom()
    {
        TutorialHud.Get().SetObjective(objectiveUrge);
        MapScreen.SetTarget(restroomRoom, restroomLabel);
    }

    IEnumerator Nag()
    {
        for (int nag = 0; !relieved; nag++)
        {
            for (float t = 0f; t < nagSeconds && !relieved; )
            {
                if (Free()) t += Time.deltaTime;
                yield return null;
            }
            if (relieved || nags.Length == 0) break;
            CameraShake.Shake(urgeShake * 0.6f);
            TechnicianVoice.Say(nags[Mathf.Min(nag, nags.Length - 1)]);
        }
    }

    // --- Picking up from a save ---

    // Not an objective: a tester's jump to the start of the breather (CheckpointJump), before the urge.
    public const string Breather = "(the breather)";

    // Whether finishing this objective is somewhere in the break (breather is the objective the break starts with,
    // ControlRoomCutscene.objectiveAfter), so ResumeFrom can pick up after it.
    public bool Knows(string finished, string breather) =>
        finished == Breather || (!string.IsNullOrEmpty(breather) && finished == breather) || finished == objectiveUrge || finished == objectiveFix || finished == objectiveSearch || finished == objectiveShelf;

    // Everything the way it was just after that objective was finished, and on from there: the urge having just come on,
    // the restroom about to be used, the storage room just walked into, the crate about to be opened, or the shelf about
    // to be reached under. The player's already back where they were (SaveGame.PlacePlayer).
    public void ResumeFrom(string finished, string breather)
    {
        if (finished == Breather)
        {
            TutorialHud.Get().SetObjective(breather);
            BeginBreak();
            return;
        }
        breakBegun = true;
        if (skipRestroom && (finished == breather || finished == objectiveUrge))
        {
            SendForCoupling();
        }
        else if (finished == breather)
        {
            urgent = true;
            FindTheRestroom();
            StartCoroutine(Nag());
        }
        else if (finished == objectiveUrge)
        {
            urgent = true;
            toilet.Occupy();
            StartCoroutine(UseRestroom());
        }
        else
        {
            AlreadyAsked();
            if (finished == objectiveFix) TutorialHud.Get().SetObjective(objectiveSearch);
            else if (finished == objectiveSearch && crate != null)
            {
                crate.Occupy();
                StartCoroutine(SearchCrate());
            }
            else if (finished == objectiveShelf && shelf != null)
            {
                if (crate != null) crate.OpenNow();
                shelf.Occupy();
                StartCoroutine(GetWrench());
            }
        }
    }

    // The restroom as it was once Okafor had asked: their toilet broken, them out of the stall, and the wrench wanted.
    void AlreadyAsked()
    {
        urgent = relieved = asked = true;
        if (occupiedToilet != null)
        {
            occupiedToilet.BreakNow();
            occupiedToilet.Vacate();
        }
        if (attendant == null) return;
        if (attendantComesIn != null)
        {
            attendant.transform.position = attendantComesIn.position;
            if (attendant.TryGetComponent(out Rigidbody2D body)) body.position = attendantComesIn.position;
        }
        attendant.talkLines = attendantAfter;
        attendant.gameObject.SetActive(true);
    }

    // --- The restroom ---

    void OnToiletUsed()
    {
        if (urgent && !relieved) StartCoroutine(UseRestroom());
        else StartCoroutine(Mutter(relieved ? alreadyWent : notYet, toilet.Done));
    }

    IEnumerator UseRestroom()
    {
        relieved = true;
        TutorialHud hud = TutorialHud.Get();
        hud.HidePrompt();
        hud.SetObjective("");
        MapScreen.ClearTarget();
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        TechnicianVoice.Hush();
        Hold();

        // Some privacy.
        ScreenFade fade = ScreenFade.Get();
        yield return fade.FadeTo(1f, 0.6f);
        yield return Wait(blackoutSeconds * 0.6f);
        fade.PlaySound(flushSound, FlushNoise);
        yield return Wait(blackoutSeconds * 0.4f);
        yield return fade.FadeTo(0f, 0.6f);
        yield return Wait(0.8f);

        // Next door goes.
        if (occupiedToilet != null)
        {
            occupiedToilet.SayFromInside(beforeFlush, 1.6f);
            yield return Wait(1.8f);
            fade.PlaySound(flushSound, FlushNoise);
            yield return Wait(0.5f);
            occupiedToilet.SayFromInside(asItBreaks, 1.8f);
            StartCoroutine(TechnicianVoice.Think(technicianHearsIt));
            yield return occupiedToilet.Break();
            yield return Wait(0.4f);
            occupiedToilet.Vacate();
        }

        // Out comes Okafor.
        if (attendant != null)
        {
            if (attendantComesIn != null)
            {
                attendant.transform.position = attendantComesIn.position;
                if (attendant.TryGetComponent(out Rigidbody2D body)) body.position = attendantComesIn.position;
            }
            attendant.gameObject.SetActive(true);
            yield return null;
            attendant.FaceTowards(player.transform.position);
            attendant.Say(attendantCallOut, 2.2f);
            yield return Wait(2.4f);
            yield return Talk(attendant, askToFix);
            bool said = DialogueBox.LastPick != 1;
            attendant.talkLines = attendantAfter;
            Hold();
            yield return TechnicianVoice.Think(said ? agreed : refused);
        }

        asked = true;
        hud.SetObjective(objectiveFix);
        MapScreen.SetTarget(toolsRoom, toolsLabel);
        player.ClearScriptedInput();
        toilet.Done();
        StartCoroutine(WalkIntoStorage());
    }

    // --- The storage room ---

    IEnumerator WalkIntoStorage()
    {
        while (player == null || !InRoom(player.transform.position, toolsRoom)) yield return null;
        MapScreen.ClearTarget();
        if (crate == null || crate.IsOpen || shelf == null || shelf.Taken) yield break;
        TutorialHud.Get().SetObjective(objectiveSearch);
        TechnicianVoice.Say(enterStorage);
    }

    void OnCrateUsed()
    {
        if (!asked) StartCoroutine(Mutter(nothingNeeded, crate.Done));
        else if (crate.IsOpen) StartCoroutine(Mutter(crateStillEmpty, crate.Done));
        else StartCoroutine(SearchCrate());
    }

    IEnumerator SearchCrate()
    {
        TutorialHud hud = TutorialHud.Get();
        hud.SetObjective("");
        Hold();
        yield return crate.Open();
        player.ClearScriptedInput();
        crate.Done();
        yield return TechnicianVoice.Think(crateEmpty);
        hud.SetObjective(objectiveShelf);
    }

    void OnShelfUsed()
    {
        if (!asked || shelf.Taken) StartCoroutine(Mutter(nothingNeeded, shelf.Done));
        else if (crate != null && !crate.IsOpen) StartCoroutine(Mutter(crateFirst, shelf.Done));
        else StartCoroutine(GetWrench());
    }

    // Down on one knee, an arm under the rack, the wrench found; a rumble far off, then the big one, and the crate on top
    // comes down on their head.
    IEnumerator GetWrench()
    {
        TutorialHud hud = TutorialHud.Get();
        hud.HidePrompt();
        hud.SetObjective("");
        MapScreen.ClearTarget();
        SuitHelper pip = SuitHelper.Exists ? SuitHelper.Get() : null;
        if (pip != null) pip.Hush();
        Hold();
        yield return Wait(0.3f);

        // Reaching, a press at a time.
        hud.ShowPrompt("Reach", Terminal.InteractKey.ToString());
        yield return null;      // the press that got here doesn't count
        for (int press = 0; press < reachPresses; )
        {
            if (Input.GetKeyDown(Terminal.InteractKey) && Time.timeScale > 0f)
            {
                if (press < reachLines.Length && !string.IsNullOrEmpty(reachLines[press])) TechnicianVoice.Say(reachLines[press], 1.2f);
                press++;
                CameraShake.Shake(0.05f);
                StartCoroutine(shelf.Nudge(0.4f + 0.6f * press / reachPresses));
            }
            yield return null;
        }
        hud.CompletePrompt();
        yield return shelf.Grab();
        TechnicianVoice.Say(gotIt, 1.4f);
        yield return Wait(1.3f);

        // Far off, something.
        yield return DistantRumble();
        yield return Wait(1.2f);
        yield return TechnicianVoice.Think(whatWasThat);
        yield return Wait(0.6f);

        Vector2 at = player.transform.position;
        float feetY = at.y - DepthSort.FeetToCenter;
        if (pip != null) pip.Tell(pipShelfWobble);
        bool hit = false;
        StartCoroutine(Quake(() => hit));
        yield return shelf.Topple(at + new Vector2(0f, 0.45f), feetY);
        hit = true;
        KnockedOut?.Invoke();

        // Down they go, the picture closing in and going dark.
        if (pip != null) pip.Hush();
        TechnicianVoice.Hush();
        CameraShake.Shake(hitShake);
        CameraShake.Kick(Vector2.down, 0.3f);
        Camera cam = Camera.main;
        float normalSize = cam != null ? cam.orthographicSize : 0f;
        float fall = Random.value < 0.5f ? 90f : -90f;
        for (float t = 0f; t < goingDownSeconds; t += Time.unscaledDeltaTime)
        {
            float p = t / goingDownSeconds;
            player.transform.rotation = Quaternion.Euler(0f, 0f, fall * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 2.5f)));
            if (cam != null && cam.orthographic) cam.orthographicSize = Mathf.Lerp(normalSize, normalSize * 0.8f, p);
            hud.SetFade(Mathf.Clamp01((p - 0.35f) / 0.65f) * 0.85f);
            yield return null;
        }
        hud.SetFade(1f);
        shelf.Done();
        yield return Wait(1.2f);
        if (cam != null && cam.orthographic) cam.orthographicSize = normalSize;

        yield return hud.TitleCard(timeSkip, timeSkipHoldSeconds);
        if (string.IsNullOrEmpty(wakeUpScene)) Debug.LogWarning($"{name}: there's no wakeUpScene to load, so Chapter 1 ends on black.", this);
        else
        {
            ChapterTwoDirector.ArrivingFromChapterOne = true;     // the title card's just been shown
            UnityEngine.SceneManagement.SceneManager.LoadScene(wakeUpScene);
        }
    }

    // A low rumble from somewhere else in the station: a little shake, and the lamps in here dip once.
    IEnumerator DistantRumble()
    {
        ScreenFade.Get().PlaySound(rumbleSound, RumbleTone);
        var lamps = new List<StationLight>();
        CameraRoom here = CameraRoom.At(player.transform.position);
        foreach (StationLight lamp in FindObjectsByType<StationLight>())
            if (lamp.IsOn && CameraRoom.At(lamp.transform.position) == here) lamps.Add(lamp);
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            CameraShake.ShakeAtLeast(rumbleShake * (1f - t / 1.2f));
            if (t > 0.3f && t < 0.42f) foreach (StationLight lamp in lamps) lamp.SetOn(false);
            else if (t >= 0.42f) foreach (StationLight lamp in lamps) if (!lamp.IsOn) lamp.SetOn(true);
            yield return null;
        }
        foreach (StationLight lamp in lamps) if (!lamp.IsOn) lamp.SetOn(true);
    }

    // Starting just after the clink of the wrench: the boom, then the shaking and the lamps stuttering, building until
    // the crate comes down and dying away after.
    IEnumerator Quake(System.Func<bool> hit)
    {
        yield return Wait(0.3f);
        ScreenFade.Get().PlaySound(quakeSound, QuakeTone);
        StationLight[] lamps = FindObjectsByType<StationLight>();
        float built = 0f, after = 0f;
        while (after < quakeAfterSeconds)
        {
            float dt = Time.unscaledDeltaTime;
            if (hit()) after += dt;
            else built = Mathf.Min(1f, built + dt / Mathf.Max(0.1f, shelf.rattleSeconds));
            float strength = built * (1f - after / quakeAfterSeconds);
            CameraShake.ShakeAtLeast(quakeShake * strength);
            // A few lamps at a time cut out for a moment.
            foreach (StationLight lamp in lamps)
                if (lamp != null && Random.value < strength * 0.08f) lamp.SetOn(!lamp.IsOn);
            yield return null;
        }
        foreach (StationLight lamp in lamps)
            if (lamp != null && !lamp.IsOn) lamp.SetOn(true);
    }

    // --- Helpers ---

    // A line to themselves, said as they carry on, letting go of whatever they were using straight away.
    IEnumerator Mutter(string line, System.Action done)
    {
        TechnicianVoice.Say(line);
        done();
        yield break;
    }

    // The boom, made in code until the scene's Station Quake sound has a clip.
    static AudioClip quakeTone;
    static AudioClip QuakeTone => quakeTone != null ? quakeTone : (quakeTone = TitleUI.Tone("Station Quake", 36f, 2.4f, 1f));
    static AudioClip rumbleTone;
    static AudioClip RumbleTone => rumbleTone != null ? rumbleTone : (rumbleTone = TitleUI.Tone("Distant Rumble", 30f, 1.4f, 0.5f));

    bool Free() =>
        player != null && !player.IsScripted && !DialogueBox.Busy && !MapScreen.IsOpen && !TutorialHud.ScreenCovered && Time.timeScale > 0f;

    void Hold() => player.SetScriptedInput(Vector2.zero, false);

    static bool InRoom(Vector2 point, char marker)
    {
        StationMap map = StationMap.Current;
        return map != null && map.Locate(point, out _, out StationMap.Room room) && room.marker == marker;
    }

    IEnumerator Talk(CrewMember who, string[] script)
    {
        bool done = false;
        void Ended(bool _) => done = true;
        who.talkLines = script;
        who.TalkEnded += Ended;
        while (DialogueBox.Busy) yield return null;
        who.Talk();
        while (!done) yield return null;
        who.TalkEnded -= Ended;
    }

    // The flush, made in code until the scene's Toilet Flush sound has a clip.
    static AudioClip flushNoise;
    static AudioClip FlushNoise => flushNoise != null ? flushNoise : (flushNoise = TitleUI.Crackle("Toilet Flush", 1.4f, 0.6f));

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }
}
