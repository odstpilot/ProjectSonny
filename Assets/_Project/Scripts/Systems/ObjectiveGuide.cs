using UnityEngine;

// how-to line and hints for each objective
public class ObjectiveGuide : MonoBehaviour
{
    const float FirstHintAfter = 40f;
    const float HintEvery = 35f;
    const float RepeatLastEvery = 60f;

    class Entry
    {
        public string objective;
        public string step;
        public string[] pip;
        public string[] self;

        public Entry(string objective, string step, string[] pip, string[] self = null)
        {
            this.objective = objective;
            this.step = step;
            this.pip = pip ?? new string[0];
            this.self = self ?? new string[0];
        }
    }

    static readonly Entry[] Entries =
    {
        new Entry("Reach the control room", "Keep heading east", null),

        new Entry("Talk to the quartermaster by the entrance", "Walk up to them and press [E]", null, new[]
        {
            "The quartermaster... that's the one waving at me by the entrance",
            "Okay, just walk up and say hi... E, I guess",
        }),
        new Entry("Open your map", "Press [M]", new[] { "~Hit [M] to open your map!" }),
        new Entry("Report to the control room", "Follow the arrows, or check your map [M]", new[]
        {
            "~The control room's the one marked in orange on your map, [M]",
            "~Through the maintenance deck and keep going, the arrows on the floor will take you",
        }),
        new Entry("Find the restroom", "It's off the lounge. Follow the arrows", new[] { "~Restroom's off the lounge, it's pinned on your map" }),
        new Entry("Get the spare data chip from the storage room", "Storage room's down the hall. Follow the arrows", new[]
        {
            "~Storage room's right off the hallway outside the control room",
            "~It's pinned on your map, [M], and the arrows point the way",
        }),
        new Entry("Find the spare data chip", "Open the tool crate [E]", new[] { "~Spares usually live in the tool crate, try that" }),
        new Entry("Check under the shelf", "Reach under the shelf [E]", new[] { "~Something's shining under that shelf, get down and look" }),

        new Entry("Get out of the storage room", "The door's jammed. Find another way out", new[]
        {
            "~The door's not budging... what about that vent grate on the floor?",
            "~Walk over to the vent grate and press [E] to crawl in",
        }),
        new Entry("Crawl through the vent", "[W] crawl  [A] [D] turn  [E] climb out at a grate", new[]
        {
            "~Find the grate that comes up in the restroom, that's our way out",
            "~If it's getting loud in there, hold [C] and crawl carefully",
        }),
        new Entry("Find the crew", "Step out of the restroom", new[] { "~The door's right there, tech" }),
        new Entry("Get to the control room", "Stay unseen and unheard: crouch [C], hide in lockers [E]", new[]
        {
            "~Stay low with [C] and keep walls between you and anything that moves",
            "~That thing with the eyes only sees down its beams, but it hears footsteps, so crouch past it",
            "~If something spots you, break its line of sight or get in a locker",
            "~The control room's pinned on your map, [M]",
        }),
        new Entry("Go upstairs to the maintenance deck", "The stairs are at the far end of the maintenance deck", new[]
        {
            "~Stairs are at the top of the maintenance deck, follow the arrows",
        }),
        new Entry("Find parts for an EMP", "Walk up to the blue glows and take the scrap [E]", new[]
        {
            "~Scrap glows blue, check the corners of the deck",
            "~We need a battery, a circuit board, and some wire",
        }),
        new Entry("Make an EMP at the workbench", "Use the workbench [E] and make the EMP", new[] { "~The workbench is on this deck, press [E] at it" }),
        new Entry("Reactivate your badge in the comms ring", "The comms ring is next to this deck. Follow the arrows", new[]
        {
            "~The comms ring's pinned on your map, [M]",
        }),
        new Entry("Find the generator and get the power back on", "Fire the EMP [LEFT CLICK]. Its flash lights up the dark", null, new[]
        {
            "Every time the EMP goes off I can see a bit... the generator's gotta be in here somewhere",
            "Keep moving, flash it ahead of me... maybe it's over in a corner",
        }),
        new Entry("Intercept Sonny's broadcasts", "Tune in two comms nodes [E] and match the wave", new[]
        {
            "~The comms nodes are the orange screens around the ring, walk up and press [E]",
            "~Line your wave up with the red dashed one: [A] [D] picks a knob, [W] [S] turns it",
        }),
        new Entry("Answer Sonny at its relay", "Answer like a bot would. The broadcasts have the answers", new[]
        {
            "~The relay's the pillar with the red eye, in the middle of the ring",
            "~Think about what the units said in the broadcasts, and answer like them, not like you",
        }),
        new Entry("Get into the control room", "Through the reactor core and down its stairs", new[]
        {
            "~The reactor's badge door should open for you now",
            "~Down the reactor's stairs, the control room's at the bottom",
        }),
    };

    public static string StepFor(string objective) => Find(objective)?.step ?? "";

    public static string Keys(string text) =>
        string.IsNullOrEmpty(text) ? text : System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+)\]", "<color=#FFAE52>[$1]</color>");

    static Entry Find(string objective)
    {
        if (string.IsNullOrEmpty(objective)) return null;
        foreach (Entry entry in Entries)
            if (objective.StartsWith(entry.objective, System.StringComparison.Ordinal)) return entry;
        return null;
    }

    public static PipManager.Nudge[] DefaultNudges()
    {
        var nudges = new System.Collections.Generic.List<PipManager.Nudge>();
        foreach (Entry entry in Entries)
            if (entry.pip.Length > 0) nudges.Add(new PipManager.Nudge { objective = entry.objective, lines = entry.pip });
        return nudges.ToArray();
    }

    private string watching = "";
    private float stuckFor, nextHintAt;
    private int given;
    private PlayerController player;

    void Update()
    {
        TutorialHud hud = TutorialHud.Get();
        string objective = hud.Objective;
        if (objective != watching)
        {
            watching = objective;
            stuckFor = 0f;
            given = 0;
            nextHintAt = FirstHintAfter;
        }
        Entry entry = Find(objective);
        if (entry == null || !Free()) return;

        stuckFor += Time.deltaTime;
        if (stuckFor < nextHintAt) return;

        bool pipOn = SuitHelper.Exists;
        string[] lines = pipOn ? PipManager.Lines.NudgesFor(entry.objective) : entry.self;
        if (lines.Length == 0) return;
        if (pipOn && SuitHelper.Get().Talking) return;

        string line = lines[Mathf.Min(given, lines.Length - 1)];
        if (pipOn) SuitHelper.Get().Tell(line);
        else TechnicianVoice.Say(line);
        given++;
        nextHintAt = stuckFor + (given < lines.Length ? HintEvery : RepeatLastEvery);
    }

    bool Free()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null || player.IsScripted || !player.enabled) return false;
        if (Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || MapScreen.IsOpen) return false;
        return !GeneratorScreen.AnyOpen && !CraftingScreen.IsOpen && !PipLog.IsOpen;
    }
}
