using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Chapter 2, the comms ring: getting the access to open the control room. Locked out of it at its badge door
// (BadgeLockout), Pip sends the technician up here hoping for a badge.
// The ring's power is out. Stepping in, what's left of it dies: the room goes pitch black (DarkRooms.SetBlackout), the
// lamps go out, and the suit's own light and Pip go with them, Pip just managing to say to use the EMP's flash to see
// by and find the generator (Generator), which shows up when a pulse catches it. Starting it (its own minigame,
// GeneratorScreen) brings the lamps, the terminals, and Pip back.
// Something else is in the dark with them: a weeping angel (WeepingAngel), let loose once Pip's gone, that runs them
// down a little faster than they can run, lurching about unpredictably, unseen but for its footsteps, the flash of its
// lights now and then, and what the EMP's flash shows. An EMP shuts it down for a while; nothing else stops it, and
// if it catches them, it throws them back to the ring's door. It stays, power or not. Let loose, a card says what it is
// and how to survive it (TipCard, kept in Pip's log); the only warning after that is the technician's heartbeat. And once
// they've got the access and head back out through the upper maintenance deck, it follows them, bursting through the
// deck's top wall.
// There's no badge, but there's Sonny's relay
// (DoctrineTerminal), where Sonny checks that whoever's at it is one of its units, and hands a unit's access to anyone
// who answers as one would. What it asks is in its broadcasts, so first: three comms nodes (HackingTerminal) to tune in,
// one transmission each, counted as they're done ("Intercept Sonny's broadcasts (1/3)"). The relay won't talk until
// all three are in. Answer it right and Sonny takes the technician for one of its own: the control room's credential is
// granted (the relay's DoctrineData), and the objective is the control room again, pinned on the map.
// Bots are already roaming the ring (ChapterOneBuilder's patrols), and while the player's in it more come through the
// walls (breaches): one every so often, at random, one each time a broadcast is intercepted (Sonny notices), and one if
// the relay traces them. Each bursts out of the top wall somewhere away from the player and comes looking for them, then
// roams the ring. Only so many at once, and only so many in all.
// ChapterTwoDirector picks up from any of it on a save (Owns, Resume). Built by ChapterOneBuilder for ChapterTwoBuilder.
public class CommsRing : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The comms nodes, each carrying one of Sonny's broadcasts.")]
    public HackingTerminal[] nodes = new HackingTerminal[0];
    [Tooltip("Sonny's relay, which only talks once every node's been tuned in.")]
    public DoctrineTerminal relay;
    [Tooltip("The comms ring, by its marker in the layout.")]
    public char room = 'g';
    [Tooltip("Credentials handed over along with the relay's own (its DoctrineData's), like the reactor core's.")]
    public string[] alsoGrants = new string[0];
    [Tooltip("The ring's backup generator, which brings its power back.")]
    public Generator generator;
    [Tooltip("The ring's lamps, out until the generator's started.")]
    public StationLight[] lamps = new StationLight[0];
    [Tooltip("What hunts them in the dark, switched off until then, and where it starts from.")]
    public WeepingAngel angel;
    public Vector2 angelStart;
    [Tooltip("Seconds after Pip's gone before it's let loose.")]
    public float angelDelay = 10f;
    [Tooltip("The upper maintenance deck, by its marker in the layout, its floor for the angel to hunt over, and where along its top wall it can come through (standing spots just below it).")]
    public char deckRoom = 'd';
    public CrewWalkArea deckFloor;
    public Vector2[] deckBreachSpots = new Vector2[0];
    [TextArea] public string[] technicianAngelFollows = { "It followed me!" };

    [Header("Objectives")]
    [Tooltip("The objective that sends the player here (BadgeLockout). Getting into the ring while it's showing starts it.")]
    public string arriveObjective = "Find a badge in the comms ring";
    public string generatorObjective = "Find the generator and get the power back on";
    [Tooltip("Shown with how many have been tuned in, like \"Intercept Sonny's broadcasts (1/3)\".")]
    public string interceptObjective = "Intercept Sonny's broadcasts";
    public string answerObjective = "Answer Sonny at its relay";
    public string afterObjective = "Get into the control room";
    [Tooltip("The room pinned on the map once the relay's passed, by its marker in the layout, and what the pin says.")]
    public char afterRoom = 'o';
    public string afterLabel = "Control Room";

    [Header("Lines")]
    [Tooltip("The technician, as the lights die.")]
    [TextArea] public string[] technicianBlackout = { "No, no, no. The lights..." };
    [Tooltip("Pip, as the suit's power goes, its last words before it's gone.")]
    [TextArea] public string[] pipBlackout =
    {
        "~Tech, something's draining the suit. I'm going to lose you.",
        "~Use the EMP. Its flash lights up whatever's in front of you.",
        "~Find the generator and get the power back on. Then I can come b-",
    };
    [Tooltip("The technician, alone in the dark.")]
    [TextArea] public string[] technicianAlone = { "Pip? ...Pip." };
    [Tooltip("The technician, hearing it coming.")]
    [TextArea] public string[] technicianAngel = { "...Footsteps. Fast ones." };
    [Tooltip("The technician, once the power's back on.")]
    [TextArea] public string[] technicianPowerOn = { "Power. Finally." };
    [Tooltip("Pip, coming back on with the power.")]
    [TextArea] public string[] pipBack = { "~...Tech? There you are! You got the power back on." };
    [Tooltip("The technician, getting here with the power already on.")]
    [TextArea] public string[] technicianArrive = { "Every screen up here's still running." };
    [Tooltip("Pip, getting here. ~ opens a line with static; ^ says it happily; [E] shows a key.")]
    [TextArea] public string[] pipArrive =
    {
        "~No badges up here. But that pillar with the red eye, that's Sonny's relay. It's how it checks on its units.",
        "~If it thinks you're one of them, it'll give you a unit's access. And units get into the control room.",
        "~It'll quiz you first. The comms nodes are carrying its broadcasts. Tune them in and listen to how the bots talk.",
    };
    [Tooltip("Pip, after each broadcast but the last, one line each in turn.")]
    [TextArea] public string[] pipIntercepted =
    {
        "~It heard that. Something's moving in the walls.",
        "~One more. Remember what they said: that's what it'll ask.",
    };
    [Tooltip("Pip, once every broadcast's in.")]
    [TextArea] public string[] pipAllIntercepted =
    {
        "~That's all of them. The relay's listening now.",
        "~Answer like a bot would, not like you would. And don't hesitate. It notices.",
    };
    [Tooltip("Pip, once the relay's passed.")]
    [TextArea] public string[] technicianVerified = { "It... thinks I'm one of them." };
    [TextArea] public string[] pipVerified =
    {
        "^You're a unit now. The reactor and the control room will open for you.",
        "~Through the reactor, down its stairs. Go.",
    };
    [Tooltip("Pip, if the relay traces them.")]
    [TextArea] public string[] pipAlarm = { "~It knows! Get away from there, it'll let you try again in a bit." };

    [Header("Breaches")]
    [Tooltip("The bots behind the walls, switched off until they come through, each at its own breach point.")]
    public PlaceholderRobot[] breachBots = new PlaceholderRobot[0];
    public Transform[] breachPoints = new Transform[0];
    [Tooltip("Seconds in the ring before the first comes through on its own, and then between one and the next.")]
    public Vector2 firstBreach = new Vector2(20f, 30f);
    public Vector2 breachEvery = new Vector2(35f, 60f);
    [Tooltip("No more than this many of them up and about at once.")]
    public int maxAtOnce = 2;
    [Tooltip("How far from the player they come through, nearest and furthest, so one never lands on top of them.")]
    public Vector2 breachDistance = new Vector2(6f, 16f);

    [Header("Respawn and testing")]
    [Tooltip("Where the player comes back after dying, once they've got here: just inside the ring.")]
    public Vector2 respawnAt;
    [Tooltip("Where the player stands for a tester's jump: just inside the ring, and in front of the relay.")]
    public Vector2 arriveStandAt;
    public Vector2 relayStandAt;
    public Vector2 generatorStandAt;

    public int Intercepted
    {
        get
        {
            int count = 0;
            foreach (HackingTerminal node in nodes)
                if (node != null && node.IsHacked) count++;
            return count;
        }
    }

    public bool AllIntercepted => Intercepted >= nodes.Length;

    private bool arrived;
    private bool angelInDeck;
    private Vector2 angelHome;
    private bool angelHomeSet;
    private bool powered;
    private bool teachingEmp;
    private float nextBreach = -1f;
    private int toldIntercepted;
    private int answeredIntercepts;     // broadcasts Pip and the objective have caught up with
    private bool verifiedPending, alarmPending;

    void OnEnable()
    {
        if (relay != null)
        {
            relay.Verified += OnVerified;
            relay.Alarmed += OnAlarmed;
        }
        PlayerHealthHandler.Respawned += OnRespawned;
        if (generator != null)
        {
            generator.Started += OnGeneratorStarted;
            generator.Backfired += OnBackfired;
        }
        EmpPulse.Fired += OnEmpFired;
    }

    void OnDisable()
    {
        if (relay != null)
        {
            relay.Verified -= OnVerified;
            relay.Alarmed -= OnAlarmed;
        }
        PlayerHealthHandler.Respawned -= OnRespawned;
        if (generator != null)
        {
            generator.Started -= OnGeneratorStarted;
            generator.Backfired -= OnBackfired;
        }
        EmpPulse.Fired -= OnEmpFired;
        DarkRooms.SetBlackout(room, false);
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        foreach (PlaceholderRobot bot in breachBots)
            if (bot != null) bot.gameObject.SetActive(false);
        if (angel != null) angel.gameObject.SetActive(false);
        // The terminals are dead until the generator's going (with no generator, there's nothing to wait for).
        SetPower(generator == null, true);
    }

    void Update()
    {
        if (player == null) return;
        TutorialHud hud = TutorialHud.Get();

        // What happened at a terminal, once its screen's away. The relay can be passed before ever trying the control
        // room's door (the ring's next to the upper deck), and that's the access all the same.
        if (!SignalTuner.AnyOpen && !DoctrineScreen.AnyOpen)
        {
            if (arrived && powered && answeredIntercepts < Intercepted) CatchUpIntercepts();
            if (alarmPending)
            {
                alarmPending = false;
                if (SuitHelper.Exists) SuitHelper.Get().Tell(pipAlarm);
            }
            if (verifiedPending)
            {
                verifiedPending = false;
                StartCoroutine(Verified());
            }
        }

        if (!arrived)
        {
            if (hud.Objective == arriveObjective && InRing() && !VentNetwork.IsPlayerInside) StartCoroutine(Arrive());
            return;
        }

        // Back out through the upper maintenance deck with the access, and the angel comes through its wall after them.
        if (!angelInDeck && angel != null && angel.gameObject.activeSelf && deckFloor != null && deckBreachSpots.Length > 0
            && hud.Objective == afterObjective && InRoom(player.transform.position, deckRoom) && !Busy())
            StartCoroutine(AngelIntoDeck());

        // Every so often, one through the walls, while the player's in the ring and still after the access, once the
        // power's back (the dark has bots enough).
        if (!powered || Busy() || !InRing() || hud.Objective == afterObjective) return;
        if (nextBreach < 0f) nextBreach = Time.time + Random.Range(firstBreach.x, firstBreach.y);
        if (Time.time >= nextBreach && Breach()) nextBreach = Time.time + Random.Range(breachEvery.x, breachEvery.y);
    }

    IEnumerator Arrive()
    {
        arrived = true;
        SetRespawn();
        TechnicianVoice.Hush();
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        if (powered)
        {
            // The generator was started already, on an earlier look round: straight to the relay.
            yield return TechnicianVoice.Think(technicianArrive);
            yield return TellAboutRelay();
            yield break;
        }

        // The lights die, the suit's with them, and Pip's last words.
        Blackout(false);
        CameraShake.Shake(0.2f);
        EquipEmp();
        yield return TechnicianVoice.Think(technicianBlackout);
        if (SuitHelper.Exists)
        {
            SuitHelper pip = SuitHelper.Get();
            pip.Glitch(0.5f);
            yield return pip.Say(pipBlackout);
            yield return pip.GoOffline();
        }
        yield return TechnicianVoice.Think(technicianAlone);
        LookForGenerator();
        yield return new WaitForSeconds(angelDelay);
        if (powered) yield break;
        LetAngelLoose(true);
        yield return TipCard.Show("Something in the dark", AngelTips);
    }

    // What it is and how to live through it, as it's let loose.
    static readonly TipCard.Row[] AngelTips =
    {
        new TipCard.Row(TipCard.Picture.Eye, "You won't see it coming",
            "Something hunts the comms ring. It makes no light, but now and then its lights flash. Listen: your heartbeat pounds louder and faster the closer it gets."),
        new TipCard.Row(TipCard.Picture.Ring, "It notices you like the bots do",
            "Only from much further: it sees nearly all the way round and hears footsteps a long way off. Crouching barely helps. Out of sight long enough, it gives up."),
        new TipCard.Row("EMP", "One pulse shuts it down for ten seconds. Use them to get away.", "LEFT CLICK"),
        new TipCard.Row(TipCard.Picture.None, "It can't be killed",
            "Weapons only stagger it. If it catches you, it hurts, and throws you back to the door."),
    };

    // Out of the dark, somewhere away from the player, coming for them.
    void LetAngelLoose(bool announce)
    {
        if (angel == null || angel.gameObject.activeSelf) return;
        angel.transform.position = new Vector3(angelStart.x, angelStart.y, angel.transform.position.z);
        angel.gameObject.SetActive(true);
        if (announce) TechnicianVoice.Say(technicianAngel[0]);
    }



    // The objective, and how to see, until the EMP's fired.
    void LookForGenerator()
    {
        TutorialHud hud = TutorialHud.Get();
        hud.SetObjective(generatorObjective);
        MapScreen.SetTarget(room, "Comms Ring");
        teachingEmp = true;
        int slot = EmpSlot();
        hud.ShowPromptWithHint("Fire the EMP to see", slot > 0 ? $"[{slot}] to hold it. Its flash lights up the dark" : "Its flash lights up the dark",
            "LEFT CLICK");
    }

    void OnEmpFired()
    {
        if (!teachingEmp) return;
        teachingEmp = false;
        TutorialHud.Get().CompletePrompt();
    }

    // Started: the lamps, the terminals, and Pip back on. Before the player's come for the relay (an early look round),
    // just the power.
    void OnGeneratorStarted()
    {
        SetPower(true, false);
        if (arrived) StartCoroutine(PowerBack());
    }

    IEnumerator PowerBack()
    {
        if (teachingEmp)
        {
            teachingEmp = false;
            TutorialHud.Get().CompletePrompt();
        }
        // Sonny felt that: the first through the walls before long.
        nextBreach = Time.time + Random.Range(firstBreach.x, firstBreach.y) * 0.5f;
        yield return TechnicianVoice.Think(technicianPowerOn);
        SuitHelper pip = SuitHelper.Get();
        if (!pip.Online) pip.ComeOnline();
        yield return pip.Say(pipBack);
        yield return TellAboutRelay();
    }

    IEnumerator TellAboutRelay()
    {
        if (SuitHelper.Exists) yield return SuitHelper.Get().Say(pipArrive);
        answeredIntercepts = Intercepted;
        TutorialHud.Get().SetObjective(Next());
        MapScreen.SetTarget(room, "Comms Ring");
    }

    // A backfire's bang, heard all over the ring: every bot in it comes to look.
    void OnBackfired()
    {
        if (generator == null) return;
        foreach (PlaceholderRobot bot in FindObjectsByType<PlaceholderRobot>())
            if (InRing(bot.transform.position)) bot.Alarm(generator.transform.position);
    }

    // The ring's terminals, and its lamps, on or off. instantly: no stutter, for setting up.
    void SetPower(bool on, bool instantly)
    {
        powered = on;
        foreach (HackingTerminal node in nodes)
            if (node != null) node.powered = on;
        if (relay != null) relay.powered = on;
        if (!on) return;
        DarkRooms.SetBlackout(room, false);
        foreach (StationLight lamp in lamps)
        {
            if (lamp == null) continue;
            if (instantly) lamp.SetOn(true);
            else lamp.PowerOn();
        }
    }

    // Pitch black: the last of the lamps out, and no light at all in the ring.
    void Blackout(bool instantly)
    {
        SetPower(false, instantly);
        DarkRooms.SetBlackout(room, true);
        foreach (StationLight lamp in lamps)
        {
            if (lamp == null) continue;
            if (instantly) lamp.SetOn(false);
            else lamp.PowerOff();
        }
    }

    // The EMP in hand, to see by.
    void EquipEmp()
    {
        if (player == null || !player.TryGetComponent(out WeaponHotbar hotbar) || !player.TryGetComponent(out PlayerCombat combat)) return;
        foreach (WeaponData weapon in hotbar.slots)
            if (weapon is EmpWeaponData)
            {
                combat.Equip(weapon);
                return;
            }
    }

    int EmpSlot()
    {
        if (player == null || !player.TryGetComponent(out WeaponHotbar hotbar)) return 0;
        for (int i = 0; i < hotbar.slots.Count; i++)
            if (hotbar.slots[i] is EmpWeaponData) return i + 1;
        return 0;
    }

    // A broadcast's in: the objective counts it, Pip remarks on it, and Sonny sends something to look.
    void CatchUpIntercepts()
    {
        answeredIntercepts = Intercepted;
        TutorialHud.Get().SetObjective(Next());
        if (AllIntercepted)
        {
            if (SuitHelper.Exists) SuitHelper.Get().Tell(pipAllIntercepted);
        }
        else if (toldIntercepted < pipIntercepted.Length && SuitHelper.Exists) SuitHelper.Get().Tell(pipIntercepted[toldIntercepted++]);
        Breach();
    }

    IEnumerator Verified()
    {
        TechnicianVoice.Hush();
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        yield return TechnicianVoice.Think(technicianVerified);
        if (SuitHelper.Exists) SuitHelper.Get().Tell(pipVerified);
        Done();
    }

    // The objective for where they are with it.
    string Next()
    {
        if (relay != null && relay.IsVerified) return afterObjective;
        if (AllIntercepted) return answerObjective;
        return InterceptText(Intercepted);
    }

    public string InterceptText(int count) => $"{interceptObjective} ({count}/{nodes.Length})";

    void Done()
    {
        TutorialHud.Get().SetObjective(afterObjective);
        MapScreen.SetTarget(afterRoom, afterLabel);
    }

    void OnVerified()
    {
        verifiedPending = true;
        foreach (string credential in alsoGrants) AccessCredentials.Grant(credential);
    }

    // Traced at the relay: everything in the ring comes for it, and something more through the walls.
    void OnAlarmed()
    {
        alarmPending = true;
        Vector2 at = relay != null ? (Vector2)relay.transform.position : (Vector2)player.transform.position;
        foreach (PlaceholderRobot bot in FindObjectsByType<PlaceholderRobot>())
            if (InRing(bot.transform.position)) bot.Alarm(at);
        if (angel != null && angel.gameObject.activeInHierarchy) angel.Alarm(at);
        Breach();
    }

    // Back from dying: whatever came through the walls forgets them.
    void OnRespawned()
    {
        foreach (PlaceholderRobot bot in breachBots)
            if (bot != null && bot.gameObject.activeInHierarchy) bot.CalmDown();
        // Back where it started, and still for a moment, so it isn't on them the second they're up.
        if (angel != null && angel.gameObject.activeSelf)
        {
            angel.Relocate(null, angelHomeSet ? angelHome : angelStart);
            angel.Stagger(2f);
        }
        if (nextBreach >= 0f) nextBreach = Time.time + Random.Range(firstBreach.x, firstBreach.y);
    }

    // One through the top wall, away from the player, coming for them. False if none can come now.
    bool Breach()
    {
        if (!arrived || player == null || Busy()) return false;
        int up = 0;
        foreach (PlaceholderRobot bot in breachBots)
            if (bot != null && bot.gameObject.activeSelf && bot.TryGetComponent(out Health health) && !health.IsDead) up++;
        if (up >= maxAtOnce) return false;

        // One of those far enough off but not too far, at random; or, with the player right down the other end of the
        // ring, whichever's nearest of the ones far enough off.
        Vector2 at = player.transform.position;
        var inRange = new List<int>();
        int nearest = -1;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < breachBots.Length && i < breachPoints.Length; i++)
        {
            if (breachBots[i] == null || breachPoints[i] == null || breachBots[i].gameObject.activeSelf) continue;
            float distance = Vector2.Distance(breachBots[i].transform.position, at);
            if (distance < breachDistance.x) continue;
            if (distance <= breachDistance.y) inRange.Add(i);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }
        if (nearest < 0) return false;
        int which = inRange.Count > 0 ? inRange[Random.Range(0, inRange.Count)] : nearest;
        StartCoroutine(BurstThrough(breachBots[which], breachPoints[which].position));
        return true;
    }

    IEnumerator BurstThrough(PlaceholderRobot bot, Vector2 point)
    {
        TutorialSetPieces.BlowInWall(point);
        bot.gameObject.SetActive(true);
        bot.blind = false;
        yield return null;      // so it's woken up and found the player
        if (player != null) bot.Alarm(player.transform.position);
    }

    // Not while the player's hidden, held, or caught up in something else.
    bool Busy() => Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || Locker.IsPlayerHidden
        || WallGrab.Holding || VentNetwork.IsPlayerInside;

    bool InRing() => InRing(player.transform.position);

    bool InRing(Vector2 point) => InRoom(point, room);

    static bool InRoom(Vector2 point, char marker)
    {
        StationMap map = StationMap.Current;
        return map != null && map.Locate(point, out _, out StationMap.Room here) && here.marker == marker;
    }

    // Through the deck's top wall, somewhere not on top of them but not far off, and straight for them.
    IEnumerator AngelIntoDeck()
    {
        angelInDeck = true;
        Vector2 at = player.transform.position;
        var near = new List<Vector2>();
        Vector2 fallback = deckBreachSpots[0];
        float best = float.MaxValue;
        foreach (Vector2 spot in deckBreachSpots)
        {
            float distance = Vector2.Distance(spot, at);
            if (distance >= breachDistance.x && distance <= breachDistance.y) near.Add(spot);
            float off = Mathf.Abs(distance - breachDistance.x);
            if (distance >= breachDistance.x * 0.5f && off < best)
            {
                best = off;
                fallback = spot;
            }
        }
        Vector2 through = near.Count > 0 ? near[Random.Range(0, near.Count)] : fallback;
        TutorialSetPieces.BlowInWall(through + new Vector2(0f, 1.6f));
        angel.Relocate(deckFloor, through);
        angelHome = through;
        angelHomeSet = true;
        yield return null;
        angel.Alarm(player.transform.position);
        if (technicianAngelFollows.Length > 0) TechnicianVoice.Say(technicianAngelFollows[0]);
    }

    void SetRespawn()
    {
        if (player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = new Vector3(respawnAt.x, respawnAt.y, player.transform.position.z);
    }

    // --- Picking up from a save (ChapterTwoDirector) ---

    // One of the ring's objectives, from getting here to being let in.
    public bool Owns(string objective) =>
        !string.IsNullOrEmpty(objective) && (objective == arriveObjective || objective == generatorObjective || objective.StartsWith(interceptObjective)
            || objective == answerObjective || objective == afterObjective);

    // The broadcasts already in, tuned in again quietly, and the relay passed if it was, for this objective. past: a
    // later stage, so it's all done.
    public void Resume(string objective, bool past = false)
    {
        StopAllCoroutines();
        int done = past || objective == answerObjective || objective == afterObjective ? nodes.Length
            : objective.StartsWith(interceptObjective) ? Count(objective) : 0;
        for (int i = 0; i < done && i < nodes.Length; i++)
            if (nodes[i] != null) nodes[i].MarkHacked();
        answeredIntercepts = Intercepted;
        toldIntercepted = Mathf.Min(done, pipIntercepted.Length);
        bool verified = past || objective == afterObjective;
        if (verified && relay != null) relay.Verify();
        verifiedPending = false;
        alarmPending = false;

        // Still in the dark, or the generator's been started.
        bool dark = !past && (objective == arriveObjective || objective == generatorObjective);
        if (!dark)
        {
            if (generator != null) generator.MarkStarted(true);
            SetPower(true, true);
            if (!past) LetAngelLoose(false);
        }

        if (past) return;
        if (objective == arriveObjective)
        {
            arrived = false;
            TutorialHud.Get().SetObjective(arriveObjective);
            MapScreen.SetTarget(room, "Comms Ring");
            return;
        }
        arrived = true;
        SetRespawn();
        if (objective == generatorObjective)
        {
            Blackout(true);
            EquipEmp();
            if (SuitHelper.Exists) StartCoroutine(SuitHelper.Get().GoOffline(true));
            LookForGenerator();
            LetAngelLoose(false);
            if (angel != null) angel.Stagger(angelDelay);
            return;
        }
        if (verified) Done();
        else
        {
            TutorialHud.Get().SetObjective(Next());
            MapScreen.SetTarget(room, "Comms Ring");
        }
    }

    // The n in "... (n/3)".
    static int Count(string objective)
    {
        int open = objective.LastIndexOf('('), slash = objective.LastIndexOf('/');
        return open >= 0 && slash > open && int.TryParse(objective.Substring(open + 1, slash - open - 1), out int n) ? n : 0;
    }
}
