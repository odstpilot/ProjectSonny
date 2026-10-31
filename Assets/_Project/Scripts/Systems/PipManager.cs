using UnityEngine;

// every pip line, edited in one place
public class PipManager : MonoBehaviour
{
    public enum Moment { None, HallwayCavedIn, UpperDeck, UpperDeckNoBench, ReactorArrival, ControlRoomLetIn }

    [System.Serializable]
    public class Nudge
    {
        [Tooltip("The objective, or how it starts: \"Intercept Sonny's broadcasts\" covers \"... (1/2)\" too.")]
        public string objective;
        [TextArea] public string[] lines;
    }

    const string ResourcePath = "PipManager";

    [Header("Start a line with ! to show it in the middle of the screen, ~ for static, ^ for happy")]
    [Header("Chapter 1 - Arrival")]
    [Tooltip("Switched on by the quartermaster, coming aboard.")]
    [TextArea] public string[] hello =
    {
        "^Bzzt! Pip online! I'm your suit's Personal Integrated Pal",
        "!Control room's pinned on your map, so hit [M] if you get lost!",
    };
    [Tooltip("Asking them to open the map.")]
    [TextArea] public string[] openMap = { "Press [M] to open your map!" };
    [Tooltip("Once the map's open.")]
    [TextArea] public string[] onMap =
    {
        "^Blinking dot's you and the control room's marked in orange... [W] [A] [S] [D] to look around, scroll to zoom",
        "[M] again to close it",
    };
    [Tooltip("Once the map's closed again. Empty for nothing.")]
    [TextArea] public string[] afterMap = new string[0];
    [Tooltip("Once Sonny's installed.")]
    [TextArea] public string[] afterInstall = { "~^Sonny's up and running! Nice work, tech!" };
    [Tooltip("The technician needing the restroom.")]
    [TextArea] public string[] restroomUrge = { "^Restroom's off the lounge, I pinned it on your map!" };
    [Tooltip("Under the shelf, as the station shakes: cut off.")]
    [TextArea] public string[] shelfWobble = { "~Tech? Something big's moving through the sta-" };

    [Header("Chapter 2 - A Few Hours Later")]
    [Tooltip("Coming back on as the technician wakes on the storage room floor.")]
    [TextArea] public string[] wakeUp =
    {
        "~Tech? You were out for hours",
        "~Something's wrong, get out of this room",
    };
    [Tooltip("Trying the storage room's jammed door.")]
    [TextArea] public string[] doorJammed = { "~It's jammed, take the vent on the floor" };
    [Tooltip("How to crawl through the vents.")]
    [TextArea] public string[] ventHowTo = { "![W] crawls and [A] and [D] turn, hold [C] to go slow and quiet" };
    [Tooltip("Out of the vents in the restroom. Empty for nothing.")]
    [TextArea] public string[] outOfVents = new string[0];
    [Tooltip("The first room with the power out.")]
    [TextArea] public string[] firstDark = { "~Power's out ahead, so your suit light's all you've got" };
    [Tooltip("Finding the crew. Empty for nothing.")]
    [TextArea] public string[] crewFound = new string[0];
    [Tooltip("The bots shown hunting, and no escape pods.")]
    [TextArea] public string[] botsRevealed =
    {
        "~Stay out of their sight",
        "~And don't count on the escape pods, every one launched hours ago and there's no way off",
        "~So we go to Sonny, in the control room",
    };
    [Tooltip("Torn free of the bot in the wall, by the maintenance deck's stairs.")]
    [TextArea] public string[] wallGrabFree = { "~Keep moving, Tech, stairs!" };
    [Tooltip("Picking up the first scrap.")]
    [TextArea] public string[] firstScrap =
    {
        "^Scrap! It goes in your pack, and the count's down in the corner",
        "Press [Tab] to open the pack and see everything you're carrying",
    };
    [Tooltip("Everything for the EMP found.")]
    [TextArea] public string[] enoughScrap = { "^That's everything for the EMP, get it to the workbench!" };
    [Tooltip("The EMP made at the workbench.")]
    [TextArea] public string[] empMade =
    {
        "!^It works! One pulse shorts out any bot or camera in front of you for a few seconds",
        "~It needs a sec to recharge between pulses though, so make them count",
        "~Okay so, the way down to the control room's through the reactor core, and the reactor's badge-only... and Sonny killed every badge on the station, yours too",
        "~But the comms ring's next door and its relay talks to every badge aboard, so if anything can switch yours back on, it's that",
    };
    [Tooltip("The crowbar picked up (or made).")]
    [TextArea] public string[] crowbarMade = { "~!A crowbar! If a bot gets too close, swing first, and the number keys switch what you're holding" };
    [Tooltip("The blaster made.")]
    [TextArea] public string[] blasterMade = { "!^The blaster! Hold to charge it and let go when it's full" };
    [Tooltip("Servo braces made.")]
    [TextArea] public string[] servoMade = { "^Servo braces, you'll move way faster now" };
    [Tooltip("Armor plating made.")]
    [TextArea] public string[] platingMade = { "^Armor plating, so you can take a few more hits" };
    [Tooltip("The rest of the workbench opening up, with a unit's access.")]
    [TextArea] public string[] benchUnlocked = { "^Oh, and a unit's access opens the workbench's full plans too, so the blaster, all of it" };
    [Tooltip("The reactor's door turning the dead badge away.")]
    [TextArea] public string[] badgeLockout =
    {
        "~Sonny cut off every badge on the station, yours included",
        "~But the comms ring's next door and its relay talks to every badge aboard, so if anything can switch yours back on, it's that",
    };
    [Tooltip("In the comms ring, with the power back: the relay, and the broadcasts.")]
    [TextArea] public string[] commsArrive =
    {
        "~See that pillar with the red eye? That's Sonny's relay, it's how it checks on its units, and it talks to every badge aboard",
        "~If it thinks you're one of them it'll switch your badge back on with a unit's access, and units get into the control room",
        "~But it'll quiz you first... the comms nodes are carrying its broadcasts, so tune them in and listen to how the bots talk",
    };
    [Tooltip("The comms ring going dark, as Pip loses the suit's power: cut off.")]
    [TextArea] public string[] commsBlackout =
    {
        "~Tech, something's draining the suit and I'm gonna lose you",
        "~!Use the EMP, its flash lights up whatever's in front of you",
        "~Find the generator and get the power back on, then I can come b-",
    };
    [Tooltip("Back on once the generator's running.")]
    [TextArea] public string[] commsBack = { "~...Tech? There you are! You got the power back on" };
    [Tooltip("After each broadcast tuned in, one line each, in turn.")]
    [TextArea] public string[] commsIntercepted =
    {
        "~Uh oh, it heard that... something's moving in the walls",
        "~!One more, and remember what they said, 'cause that's what it'll ask",
    };
    [Tooltip("Every broadcast in: answer the relay.")]
    [TextArea] public string[] commsAllIntercepted =
    {
        "~That's all of them, the relay's listening now",
        "~!Answer like a bot would, not like you would, and don't hesitate 'cause it notices",
    };
    [Tooltip("The relay fooled: the badge back on as a unit's.")]
    [TextArea] public string[] commsVerified =
    {
        "^Your badge is back on as a unit's, so the reactor and the control room will open for you",
        "~Through the reactor and down its stairs, go!",
    };
    [Tooltip("The relay not fooled.")]
    [TextArea] public string[] commsAlarm = { "~It knows! Get away from there, it'll let you try again in a bit." };
    [Tooltip("The hallway to the control room, caved in.")]
    [TextArea] public string[] hallwayCavedIn =
    {
        "~Then we go over it, there's stairs at the top of the deck",
    };
    [Tooltip("Up the stairs onto the upper maintenance deck, with its workbench.")]
    [TextArea] public string[] upperDeck =
    {
        "~A workbench! Bring me scrap and we'll build an EMP",
        "~Scrap glows blue, so check between the racks",
    };
    [Tooltip("The same, in a build with no workbench on the deck.")]
    [TextArea] public string[] upperDeckNoBench =
    {
        "~The reactor core's the way down but your badge is dead... the comms ring's relay might switch it back on though",
    };

    [Header("Chapter 3 - The Reactor")]
    [Tooltip("The core's first surge.")]
    [TextArea] public string[] surgeWarning = { "~!The core's surging! Get behind one of those lead walls, now!" };
    [Tooltip("Once the first surge has passed.")]
    [TextArea] public string[] afterFirstSurge = { "~And the bots just... bowed to it... okay that's creepy, but it's a breather, use it" };
    [Tooltip("The lockdown starting, behind the reactor.")]
    [TextArea] public string[] lockdownStarts = { "~It's locking you in back here, and the containment gates are opening... here they come!" };
    [Tooltip("Each wave after the first, one line each, in turn.")]
    [TextArea] public string[] lockdownNextWave = { "~More coming out!", "~!Another lot, watch the coil ones, they call down lightning", "~Last of them, I think, hang in there" };
    [Tooltip("Pip's last words, once the last bot's down (SuitHelper.Die): cut off partway, wherever it falls. Then it's gone for good.")]
    [TextArea] public string lastWords = "~Tech... that lightning got into the suit, I can't hold on, you'll have to do the rest without me";
    [Tooltip("Walking into the reactor core: the start of Chapter 3.")]
    [TextArea] public string[] reactorArrival =
    {
        "~Radiation's leaking out all over, and wading through that yellow stuff's gonna slow you right down",
        "~Stairs down are round the back of the reactor... and uhh, there's a lot of bots in here",
        "~And if the core starts flaring, get behind one of those lead walls",
    };
    [Tooltip("Let into the control room as one of Sonny's units.")]
    [TextArea] public string[] controlRoomLetIn =
    {
        "~It thinks a unit just walked in, so don't give it a reason to look twice",
    };

    [Header("Anywhere - when they're stuck")]
    [Tooltip("For each objective, what Pip says if it hasn't moved on for a while.")]
    public Nudge[] stuckNudges = ObjectiveGuide.DefaultNudges();

    static PipManager lines;

    public static PipManager Lines
    {
        get
        {
            if (lines != null) return lines;
            lines = Resources.Load<PipManager>(ResourcePath);
            if (lines == null)
            {
                Debug.LogWarning("PipManager: there's no Resources/PipManager prefab, so Pip says what it starts with. Sonny > Pip Manager makes one.");
                var holder = new GameObject("Pip Manager (defaults)") { hideFlags = HideFlags.HideAndDontSave };
                lines = holder.AddComponent<PipManager>();
            }
            return lines;
        }
    }

    public string[] For(Moment moment) => moment switch
    {
        Moment.HallwayCavedIn => hallwayCavedIn,
        Moment.UpperDeck => upperDeck,
        Moment.UpperDeckNoBench => upperDeckNoBench,
        Moment.ReactorArrival => reactorArrival,
        Moment.ControlRoomLetIn => controlRoomLetIn,
        _ => new string[0],
    };

    public string[] NudgesFor(string objective)
    {
        foreach (Nudge nudge in stuckNudges)
            if (!string.IsNullOrEmpty(nudge.objective) && !string.IsNullOrEmpty(objective)
                && objective.StartsWith(nudge.objective, System.StringComparison.Ordinal)) return nudge.lines ?? new string[0];
        return new string[0];
    }
}
