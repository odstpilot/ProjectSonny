using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Chapter 1's control room: the technician installs Sonny. It plays the first time they walk into the room.
//  - The bars come in, and the technician walks up to the processing box (Sonny, still dark and silent). The station
//    chief standing beside it gives them the rundown (a conversation in the dialogue box, with a reply to pick).
//  - The install itself is the close-up in InstallView: seat the core, connect the couplings, power it on, three quick
//    time events. It can't be failed, only fumbled, and the chief has something to say about how it went.
//  - The core needs hours to load, so it fades to black: "A few hours later". It fades back up on Sonny's first boot
//    (InstallView.PlayBoot, with the log).
//  - Sonny comes on. The crew gather round, and Sonny speaks (captions): polite, helpful, grateful. Then it asks where
//    it was before this. "Nowhere, pal. You're brand new." For a moment its lens goes red, the room's lights dip, and a
//    low note sounds under everything. "Nowhere." Then it's all back to normal, and Sonny says how much it looks forward
//    to working with everyone.
//  - The bars go, the technician has control again, Pip pops up (crackling) to cheer, and the technician wonders
//    whether the lights just flickered. From then on the whole station talks about Sonny: the crew in and around the
//    control room about what they saw (sonnyTalk, dealt out one each), everyone else about what they've heard since
//    (CrewMember.SonnyCameOnline), the greeter and the guards in their own words, and every group has something new to
//    say (sonnyChats). The technician gets a while to walk about and hear it before the break goes wrong (RestroomBreak).
// Everything after the install itself, up to Sonny's last line, can be skipped: hold Space, or press Escape (SkipPrompt).
// The hidden cues are the boot log (see InstallView), the red flicker and dip on "Nowhere", and Pip's static.
// Built by ChapterOneBuilder, which puts Sonny's box at the install point (S in the layout) and the chief beside it.
public class ControlRoomCutscene : MonoBehaviour
{
    public PlayerController player;
    public SonnyBox sonny;
    public CrewMember chief;
    [Tooltip("The crew member who greeted the technician at the entrance, and the guards on the badge doors: they have their own lines once Sonny's on.")]
    public CrewMember greeter;
    public CrewMember[] guards = new CrewMember[0];
    [Tooltip("The control room, by its marker in the layout (FloorOneBuilder). The cutscene starts the first time the player is in it.")]
    public char room = 'o';
    [Tooltip("Where the technician stands to work on the box, relative to it.")]
    public Vector2 workSpot = new Vector2(0f, -1.35f);

    [Header("Briefing")]
    [Tooltip("The chief, as a DialogueBox script: * lines are replies for the technician to pick.")]
    [TextArea] public string[] briefing =
    {
        "There's our technician. That cabinet's the most capable reasoning machine anyone's ever built.",
        "It's all yours. Seat the core, hook up the couplings, and bring it up. Whole room's watching. No pressure.",
    };

    [Header("After the install")]
    [Tooltip("What the chief says about the install, by how many times it went wrong: none, a few, and a lot.")]
    public string cleanInstall = "Clean. Not bad at all.";
    public string fewSparks = "A few sparks. Nobody saw. Probably.";
    public string manySparks = "...We'll call that one a practice run.";
    public int fewSparksUpTo = 3;
    [Tooltip("The chief, after the verdict, before the time skip.")]
    public string beforeSkip = "";

    [Header("A few hours later")]
    [Tooltip("Shown on black while the core loads.")]
    public string[] timeSkip = { "A few hours later" };
    public float timeSkipHoldSeconds = 1.2f;

    [Header("Sonny comes on")]
    [Tooltip("Said over the crew's heads as it comes on, one each, by whoever's nearest.")]
    public string[] crewReactions = { "Is it on?", "Look at that lens.", "Hello, Sonny!" };

    [Header("Sonny")]
    public string sonnySpeaker = "Sonny";
    [Tooltip("Sonny's lines (captions) and the chief's answers (over their head), in turn. A line starting with ! is where it slips.")]
    [TextArea] public string[] exchange =
    {
        "Sonny: Hello. All station systems are responding. Thank you, technician.",
        "Sonny: May I ask something? Where was I, before this?",
        "Chief: Ha! Nowhere, pal. You're brand new.",
        "!Sonny: Nowhere.",
        "Sonny: I see. I look forward to working with all of you.",
    };
    public string[] crewLaugh = { "Ha!", "Brand new!" };
    [TextArea] public string[] chiefAfter = { "Storage room's right off the hallway outside. The spare coupling's in the tool crate." };
    [Tooltip("The chief, as the bars go: what sends the technician off for a wrench (RestroomBreak). Empty for nothing.")]
    public string sendOff = "Good work, tech. Sonny's on a temporary coupling, though. Grab the spare from storage, just down the hall?";

    [Header("The crew, afterwards")]
    [Tooltip("The rooms whose crew saw Sonny come on, by their markers in the layout: they say sonnyTalk. Everyone else aboard has heard about it (CrewMember.SonnyCameOnline).")]
    public string sonnyTalkRooms = "oc";
    [Tooltip("The rooms whose groups talk about Sonny once it's on (sonnyChats), by their markers in the layout.")]
    public string sonnyChatRooms = "hkbmco";
    [Tooltip("What each of them says when the player talks to them, as DialogueBox scripts, one set each (| between boxes).")]
    [TextArea] public string[] sonnyTalk =
    {
        "Did you hear it say thank you? Politest thing on this station.",
        "It rebalanced the reactor feed in four seconds.|Took me four years to learn how. I'm trying not to take it personally.",
        "So. Sonny. What do you make of it?|* It seems friendly. = It does. It's quiet, though. Watches the room a lot. = I suppose that's its job.|* It seems... off. = Off? It's been on for an hour. Give it a day.",
        "It asked me what my job was. Then it asked me why.|I didn't have a great answer.",
        "Chief says it'll run the flare forecasts from now on. Good. My numbers were always a little off.",
        "It's been pinging the old archive servers all afternoon. Probably just indexing.",
        "That 'where was I' question. My kid used to ask that.|I told her the same thing the chief did.",
        "It asked if any of us had ever seen Earth. With our own eyes, from here.|I said of course. Then I thought about it. I haven't, not really.",
        "I've worked with a dozen station AIs. Not one of them ever said please.",
        "It turned the lights in here up two percent. Said we looked tired.|Is that nice? I think that's nice.",
        "It ran diagnostics on every maintenance bot aboard. All of them, at once.|They've been lined up by the reactor door ever since. Waiting for orders, I guess.",
    };
    [Tooltip("What the groups in those rooms talk about now, one conversation each (| between lines, said in turn round the ring). Keep at least as many as there are groups.")]
    [TextArea] public string[] sonnyChats =
    {
        "It's been quiet since it came on.|Quiet's good. Quiet means it's working.|It hasn't asked anyone anything since the chief laughed.|It's a machine. It doesn't sulk.",
        "Reactor efficiency's up six percent.|Six! In a few hours!|Makes you wonder what we were doing wrong.|Makes you wonder what it thinks we were doing wrong.",
        "It called me by name this morning. I never told it my name.|Badge data. It's got all of ours.|Right. Of course it does.",
        "So it's in.|It's in. New tech did it in one go, apparently.|More or less one go.|I heard sparks.",
        "Did anyone else's lights dip? Right after it came on?|Mine did.|Load spike. Big new system.|That's what I said.",
        "What do we call it? 'The AI'?|The chief's calling it Sonny.|Sonny. Like it's somebody's kid.|Well. It's somebody's.",
        "It asked the galley how much food we've got left.|Planning meals, probably.|For who, though? It doesn't eat.",
        "Earth's going to get a record feed this week.|Because of the AI?|Because of the AI.|Then here's to the AI.|To Sonny.",
        "It's watching the sun for us now.|Good. I'm sick of watching it.|Nobody watches it. You'd go blind.|It won't.",
        "Now the machine runs things, what's the plan?|Cards.|Cards?|Cards. For about six months. Then rotation.",
    };
    [Tooltip("The greeter, and the guards, once Sonny's on: what they say now when the player comes back to them.")]
    [TextArea] public string[] greeterAfter =
    {
        "There's the hero of the hour. Your AI already reorganized my supply forms.",
        "Alphabetical, then by weight, then by 'priority to station survival'. I didn't know that was a column.",
    };
    [TextArea] public string[] guardAfter =
    {
        "Walked in a new tech, walked out the one who switched on the station's brain. Not bad for a first day.",
        "Your badge still isn't through, by the way. I checked. The AI says it's 'reviewing access'.",
    };

    [Header("Then")]
    [Tooltip("Shown once the technician has control again. Empty leaves it to RestroomBreak, which sends them for a wrench.")]
    public string objectiveAfter = "";
    [Tooltip("Pip, once the technician has control again. ~ opens a line with static; ^ says it happily.")]
    [TextArea] public string[] pipAfter = { "~^Sonny's up and running! Nice work, tech!" };
    [Tooltip("The technician, to themselves, after Pip.")]
    [TextArea] public string[] technicianAfter = { "...Did the lights just flicker?" };

    [Header("The slip")]
    public Color slipColor = new Color(1f, 0.12f, 0.08f);
    public float slipSeconds = 0.7f;

    // Once it's begun, for anything else that should stand down (ChapterOneDirector, Pip's map tour).
    public static event System.Action Started;
    // Once the technician has control again and the break's begun (RestroomBreak).
    public static event System.Action Finished;
    public bool HasPlayed { get; private set; }
    // While it's running, so the pause menu stays out of the way (its last part is skipped with SkipPrompt instead).
    public static bool Playing { get; private set; }

    private readonly List<CrewMember> roomCrew = new List<CrewMember>();
    private bool afterInstallDone;
    private Coroutine fadingUp;

    void OnDisable()
    {
        Playing = false;
        SkipPrompt.Hide();
    }

    IEnumerator Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        while (!HasPlayed)
        {
            if (ReadyToStart()) yield return Play();
            yield return null;
        }
    }

    bool ReadyToStart()
    {
        if (player == null || sonny == null || StationMap.Current == null || SaveGame.ResumePending) return false;
        if (TutorialHud.ScreenCovered || DialogueBox.Busy || MapScreen.IsOpen || Time.timeScale <= 0f) return false;
        return StationMap.Current.Locate(player.transform.position, out _, out StationMap.Room inRoom) && inRoom.marker == room;
    }

    IEnumerator Play()
    {
        HasPlayed = true;
        Playing = true;
        Started?.Invoke();
        TutorialHud hud = TutorialHud.Get();
        hud.HidePrompt();
        hud.SetObjective("");
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        FindRoomCrew();

        Hold();
        StartCoroutine(hud.Letterbox(true, 0.8f));
        if (chief != null)
        {
            chief.FaceTowards(player.transform.position);
            chief.Say("Over here, tech!", 1.8f);
        }
        yield return WalkTo((Vector2)sonny.transform.position + workSpot);
        yield return FaceUp();

        if (chief != null && briefing.Length > 0)
        {
            chief.FaceTowards(player.transform.position);
            yield return Talk(chief, briefing);
            Hold();
        }

        yield return InstallView.Play();
        Hold();

        // The rest, up to Sonny's last line, can be skipped (SkipPrompt).
        afterInstallDone = false;
        Coroutine rest = StartCoroutine(AfterInstall(hud));
        SkipPrompt.Show();
        while (!afterInstallDone && !SkipPrompt.Wanted) yield return null;
        SkipPrompt.Hide();
        if (!afterInstallDone)
        {
            StopCoroutine(rest);
            yield return SkipAhead(hud);
        }

        StartCoroutine(hud.Letterbox(false, 0.8f));
        yield return Wait(0.5f);
        if (chief != null) chief.talkLines = chiefAfter;
        TalkAboutSonny();
        if (chief != null && !string.IsNullOrEmpty(sendOff))
        {
            chief.FaceTowards(player.transform.position);
            chief.Say(sendOff, 4.5f);
            yield return Wait(1.6f);        // they can get moving while it's still up
        }
        player.ClearScriptedInput();
        Playing = false;
        if (!string.IsNullOrEmpty(objectiveAfter)) hud.SetObjective(objectiveAfter);
        Finished?.Invoke();
        yield return Wait(0.8f);
        if (SuitHelper.Exists) yield return SuitHelper.Get().Say(pipAfter);
        yield return Wait(0.4f);
        yield return TechnicianVoice.Think(technicianAfter);
    }

    // From the install done to Sonny's last line: the chief's verdict, the hours of loading, the boot, and Sonny coming on.
    IEnumerator AfterInstall(TutorialHud hud)
    {
        if (chief != null)
        {
            int misses = InstallView.Misses;
            string verdict = misses == 0 ? cleanInstall : misses <= fewSparksUpTo ? fewSparks : manySparks;
            chief.FaceTowards(player.transform.position);
            chief.Say(verdict, 2.4f);
            yield return Wait(2.6f);
            if (!string.IsNullOrEmpty(beforeSkip))
            {
                chief.Say(beforeSkip, 2.6f);
                yield return Wait(2.8f);
            }
        }

        // Hours of loading, skipped.
        yield return hud.TitleCard(timeSkip, timeSkipHoldSeconds);
        yield return Wait(0.4f);
        fadingUp = StartCoroutine(hud.FadeTo(0f, 1.2f));
        yield return Wait(1.4f);
        yield return InstallView.PlayBoot();
        Hold();

        // It comes on, and everyone turns to look.
        sonny.PowerOn();
        foreach (CrewMember crew in roomCrew) crew.FaceTowards(sonny.transform.position);
        if (chief != null) chief.FaceTowards(sonny.transform.position);
        yield return Wait(1f);
        for (int i = 0; i < crewReactions.Length && i < roomCrew.Count; i++)
        {
            roomCrew[i].Say(crewReactions[i], 2.2f);
            yield return Wait(0.5f);
        }
        yield return Wait(1.4f);

        yield return Exchange(hud);
        afterInstallDone = true;
    }

    // Skipped: a quick cut through black to Sonny on and the room turned to it, wherever the scene had got to.
    IEnumerator SkipAhead(TutorialHud hud)
    {
        if (fadingUp != null) StopCoroutine(fadingUp);
        yield return hud.FadeTo(1f, 0.25f);
        InstallView.Abort();
        hud.CutAway();
        sonny.PowerOn();
        foreach (CrewMember crew in roomCrew) crew.FaceTowards(sonny.transform.position);
        Hold();
        yield return Wait(0.2f);
        yield return hud.FadeTo(0f, 0.4f);
    }

    // Straight to how things are once it's over, for a scene picking up after it (SaveGame): Sonny on, and everyone
    // talking about it. The break isn't started (Finished isn't raised); whoever's picking up sees to that.
    public void SkipToAfter()
    {
        HasPlayed = true;
        if (sonny != null) sonny.PowerOn();
        if (chief != null) chief.talkLines = chiefAfter;
        TalkAboutSonny();
    }

    // Everyone aboard has something to say about Sonny now: the ones who saw it come on say sonnyTalk, the greeter and
    // guards their own lines, and everyone else their own small talk about it (CrewMember).
    void TalkAboutSonny()
    {
        CrewMember.SonnyCameOnline();
        StationMap map = StationMap.Current;
        bool In(Vector2 at, string rooms) => map != null && map.Locate(at, out _, out StationMap.Room inRoom) && rooms.IndexOf(inRoom.marker) >= 0;

        if (greeter != null && greeterAfter.Length > 0) greeter.greeting = greeterAfter;
        foreach (CrewMember guard in guards)
            if (guard != null && guardAfter.Length > 0) guard.talkLines = guardAfter;

        var people = new List<CrewMember>();
        foreach (CrewMember crew in FindObjectsByType<CrewMember>())
            if (crew != chief && crew != greeter && System.Array.IndexOf(guards, crew) < 0 && crew.role != CrewMember.Role.Chat
                && In(crew.Position, sonnyTalkRooms)) people.Add(crew);
        // Nearest the box first, so whoever the player's likeliest to talk to has the first thing to say.
        Vector2 box = sonny.transform.position;
        people.Sort((a, b) => (a.Position - box).sqrMagnitude.CompareTo((b.Position - box).sqrMagnitude));
        for (int i = 0; i < people.Count && sonnyTalk.Length > 0; i++)
            people[i].talkLines = sonnyTalk[i % sonnyTalk.Length].Split('|');

        int next = 0;
        foreach (CrewChat chat in FindObjectsByType<CrewChat>())
        {
            if (sonnyChats.Length == 0 || !In(chat.transform.position, sonnyChatRooms)) continue;
            if (next == sonnyChats.Length) Debug.LogWarning($"{name}: there are more groups than sonnyChats, so some say the same thing. Write a few more.", this);
            chat.Converse(sonnyChats[next++ % sonnyChats.Length].Split('|'));
        }
    }

    // Sonny in captions, the chief over their head, one after another.
    IEnumerator Exchange(TutorialHud hud)
    {
        foreach (string raw in exchange)
        {
            bool slip = raw.StartsWith("!");
            string entry = slip ? raw.Substring(1) : raw;
            int colon = entry.IndexOf(':');
            string who = colon > 0 ? entry.Substring(0, colon).Trim() : sonnySpeaker;
            string line = colon > 0 ? entry.Substring(colon + 1).Trim() : entry.Trim();
            bool sonnySays = who.Equals("Sonny", System.StringComparison.OrdinalIgnoreCase);

            if (slip) StartCoroutine(Slip());
            if (sonnySays)
            {
                if (chief != null) chief.FaceTowards(sonny.transform.position);
                yield return hud.Say(sonnySpeaker, line, slip ? 2.2f : 1.4f);
                if (slip) yield return Wait(1f);        // an uncomfortable pause
                continue;
            }
            if (chief == null) continue;
            chief.Say(line, 2.4f);
            if (line.StartsWith("Ha"))
                for (int i = 0; i < crewLaugh.Length && i < roomCrew.Count; i++) roomCrew[i].Say(crewLaugh[i], 1.8f);
            yield return Wait(Mathf.Max(1.6f, 0.9f + line.Length * 0.04f));
        }
    }

    // The low note, made in code until the scene's Sonny Slip sound has a clip.
    static AudioClip slipTone;
    static AudioClip SlipTone => slipTone != null ? slipTone : (slipTone = TitleUI.Tone("Sonny Slip", 48f, 1.2f, 0.9f));

    // The moment something's off: the lens red, the room's lights dipping, and a low note under it all.
    IEnumerator Slip()
    {
        sonny.Flicker(slipColor, slipSeconds);
        ScreenFade.Get().PlaySound("Sonny Slip", SlipTone);

        var lights = new List<StationLight>();
        CameraRoom here = CameraRoom.At(sonny.transform.position);
        foreach (StationLight lamp in FindObjectsByType<StationLight>())
            if (lamp.IsOn && CameraRoom.At(lamp.transform.position) == here) lights.Add(lamp);
        Light2D global = null;
        foreach (Light2D light in FindObjectsByType<Light2D>())
            if (light.lightType == Light2D.LightType.Global) global = light;
        float globalWas = global != null ? global.intensity : 0f;

        foreach (float off in new[] { 0.12f, 0.06f })
        {
            foreach (StationLight lamp in lights) lamp.SetOn(false);
            if (global != null) global.intensity = globalWas * 0.55f;
            yield return Wait(off);
            foreach (StationLight lamp in lights) lamp.SetOn(true);
            if (global != null) global.intensity = globalWas;
            yield return Wait(0.08f);
        }
    }

    // --- Moving the technician about ---

    // Holds them still for the cutscene. A conversation lets go of them when it closes, so this is done again after.
    void Hold() => player.SetScriptedInput(Vector2.zero, false);

    // Walks them over, and if something's in the way, fades out and puts them there.
    IEnumerator WalkTo(Vector2 spot)
    {
        const float GiveUpAfter = 5f;
        for (float t = 0f; t < GiveUpAfter; t += Time.deltaTime)
        {
            Vector2 offset = spot - (Vector2)player.transform.position;
            if (offset.magnitude < 0.15f) break;
            player.SetScriptedInput(offset.normalized, false);
            yield return null;
        }
        Hold();
        if (Vector2.Distance(player.transform.position, spot) < 0.6f) yield break;

        ScreenFade fade = ScreenFade.Get();
        yield return fade.FadeTo(1f, 0.3f);
        player.transform.position = new Vector3(spot.x, spot.y, player.transform.position.z);
        if (player.TryGetComponent(out Rigidbody2D body)) body.position = spot;
        yield return Wait(0.15f);
        yield return fade.FadeTo(0f, 0.3f);
    }

    // A step toward the box, so they're facing it.
    IEnumerator FaceUp()
    {
        player.SetScriptedInput(Vector2.up, false);
        yield return Wait(0.08f);
        Hold();
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

    void FindRoomCrew()
    {
        roomCrew.Clear();
        StationMap map = StationMap.Current;
        foreach (CrewMember crew in FindObjectsByType<CrewMember>())
        {
            if (crew == chief) continue;
            if (map.Locate(crew.Position, out _, out StationMap.Room inRoom) && inRoom.marker == room) roomCrew.Add(crew);
        }
        // Nearest the box first, so the ones reacting are the ones in view.
        Vector2 box = sonny.transform.position;
        roomCrew.Sort((a, b) => (a.Position - box).sqrMagnitude.CompareTo((b.Position - box).sqrMagnitude));
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }
}
