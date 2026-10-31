using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// the comms ring: power, broadcasts, relay
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
    [Tooltip("The objective that sends the player here (Workshop.afterObjective). Getting into the ring while it's showing starts it.")]
    public string arriveObjective = "Reactivate your badge in the comms ring";
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
    [TextArea] public string[] technicianBlackout = { "No, no, no... the lights..." };
    [Tooltip("The technician, alone in the dark.")]
    [TextArea] public string[] technicianAlone = { "Pip? ...Pip" };
    [Tooltip("The technician, hearing it coming.")]
    [TextArea] public string[] technicianAngel = { "...Footsteps, and fast ones" };
    [Tooltip("The technician, once the power's back on.")]
    [TextArea] public string[] technicianPowerOn = { "Power, finally" };
    [Tooltip("The technician, getting here with the power already on.")]
    [TextArea] public string[] technicianArrive = { "Every screen up here's still running" };
    [Tooltip("Pip, once the relay's passed.")]
    [TextArea] public string[] technicianVerified = { "It... thinks I'm one of them." };

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

    public bool AllIntercepted => Intercepted >= Needed;

    int Needed => relay != null ? relay.Needed : nodes.Length;

    private bool arrived;
    private bool angelInDeck;
    private Vector2 angelHome;
    private bool angelHomeSet;
    private bool powered;
    private bool teachingEmp;
    private float nextBreach = -1f;
    private int toldIntercepted;
    private int answeredIntercepts;
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
        SetPower(generator == null, true);
        if (generator != null) generator.locked = true;
    }

    void Update()
    {
        if (player == null) return;
        TutorialHud hud = TutorialHud.Get();

        if (!SignalTuner.AnyOpen && !DoctrineScreen.AnyOpen)
        {
            if (arrived && powered && answeredIntercepts < Intercepted) CatchUpIntercepts();
            if (alarmPending)
            {
                alarmPending = false;
                if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.commsAlarm);
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

        if (!angelInDeck && angel != null && angel.gameObject.activeSelf && deckFloor != null && deckBreachSpots.Length > 0
            && hud.Objective == afterObjective && InRoom(player.transform.position, deckRoom) && !Busy())
            StartCoroutine(AngelIntoDeck());

        if (!powered || Busy() || !InRing() || hud.Objective == afterObjective) return;
        if (nextBreach < 0f) nextBreach = Time.time + Random.Range(firstBreach.x, firstBreach.y);
        if (Time.time >= nextBreach && Breach()) nextBreach = Time.time + Random.Range(breachEvery.x, breachEvery.y);
    }

    IEnumerator Arrive()
    {
        arrived = true;
        if (generator != null) generator.locked = false;
        SetRespawn();
        TechnicianVoice.Hush();
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        if (powered)
        {
            yield return TechnicianVoice.Think(technicianArrive);
            yield return TellAboutRelay();
            yield break;
        }

        Blackout(false);
        CameraShake.Shake(0.2f);
        EquipEmp();
        yield return TechnicianVoice.Think(technicianBlackout);
        if (SuitHelper.Exists)
        {
            SuitHelper pip = SuitHelper.Get();
            pip.Glitch(0.5f);
            yield return pip.Say(PipManager.Lines.commsBlackout);
            yield return pip.GoOffline();
        }
        yield return TechnicianVoice.Think(technicianAlone);
        if (powered) yield break;
        LookForGenerator();
        yield return new WaitForSeconds(angelDelay);
        if (powered) yield break;
        LetAngelLoose(true);
        TipCard.Log("Something in the dark", AngelTips);
        TutorialHud.Get().ShowPromptWithHint("Something's hunting you", "An EMP shuts it down for a while. It can't be killed", "LEFT CLICK");
        teachingEmp = true;
    }

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

    void LetAngelLoose(bool announce)
    {
        if (angel == null || angel.gameObject.activeSelf) return;
        angel.transform.position = new Vector3(angelStart.x, angelStart.y, angel.transform.position.z);
        angel.gameObject.SetActive(true);
        if (announce) TechnicianVoice.Say(technicianAngel[0]);
    }

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
        RelayObjective();
        nextBreach = Time.time + Random.Range(firstBreach.x, firstBreach.y) * 0.5f;
        yield return TechnicianVoice.Think(technicianPowerOn);
        SuitHelper.Worsen(SuitHelper.Condition.Damaged);
        SuitHelper pip = SuitHelper.Get();
        if (!pip.Online) pip.ComeOnline();
        yield return pip.Say(PipManager.Lines.commsBack);
        yield return TellAboutRelay();
    }

    IEnumerator TellAboutRelay()
    {
        RelayObjective();
        if (SuitHelper.Exists) yield return SuitHelper.Get().Say(PipManager.Lines.commsArrive);
    }

    void RelayObjective()
    {
        answeredIntercepts = Intercepted;
        TutorialHud.Get().SetObjective(Next());
        MapScreen.SetTarget(room, "Comms Ring");
    }

    void OnBackfired()
    {
        if (generator == null) return;
        foreach (PlaceholderRobot bot in FindObjectsByType<PlaceholderRobot>())
            if (InRing(bot.transform.position)) bot.Alarm(generator.transform.position);
    }

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

    void CatchUpIntercepts()
    {
        bool hadEnough = answeredIntercepts >= Needed;
        answeredIntercepts = Intercepted;
        if (hadEnough) return;
        TutorialHud.Get().SetObjective(Next());
        if (AllIntercepted)
        {
            if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.commsAllIntercepted);
        }
        else if (toldIntercepted < PipManager.Lines.commsIntercepted.Length && SuitHelper.Exists)
        {
            int from = toldIntercepted;
            toldIntercepted = Needed - Intercepted <= 1 ? PipManager.Lines.commsIntercepted.Length : toldIntercepted + 1;
            var lines = new string[toldIntercepted - from];
            System.Array.Copy(PipManager.Lines.commsIntercepted, from, lines, 0, lines.Length);
            SuitHelper.Get().Tell(lines);
        }
        Breach();
    }

    IEnumerator Verified()
    {
        TechnicianVoice.Hush();
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        yield return TechnicianVoice.Think(technicianVerified);
        if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.commsVerified);
        Done();
    }

    string Next()
    {
        if (relay != null && relay.IsVerified) return afterObjective;
        if (AllIntercepted) return answerObjective;
        return InterceptText(Intercepted);
    }

    public string InterceptText(int count) => $"{interceptObjective} ({Mathf.Min(count, Needed)}/{Needed})";

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

    void OnAlarmed()
    {
        alarmPending = true;
        Vector2 at = relay != null ? (Vector2)relay.transform.position : (Vector2)player.transform.position;
        foreach (PlaceholderRobot bot in FindObjectsByType<PlaceholderRobot>())
            if (InRing(bot.transform.position)) bot.Alarm(at);
        if (angel != null && angel.gameObject.activeInHierarchy) angel.Alarm(at);
        Breach();
    }

    void OnRespawned()
    {
        foreach (PlaceholderRobot bot in breachBots)
            if (bot != null && bot.gameObject.activeInHierarchy) bot.CalmDown();
        if (angel != null && angel.gameObject.activeSelf)
        {
            angel.Relocate(null, angelHomeSet ? angelHome : angelStart);
            angel.Stagger(2f);
        }
        if (nextBreach >= 0f) nextBreach = Time.time + Random.Range(firstBreach.x, firstBreach.y);
    }

    bool Breach()
    {
        if (!arrived || player == null || Busy()) return false;
        int up = 0;
        foreach (PlaceholderRobot bot in breachBots)
            if (bot != null && bot.gameObject.activeSelf && bot.TryGetComponent(out Health health) && !health.IsDead) up++;
        if (up >= maxAtOnce) return false;

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
        yield return null;
        if (player != null) bot.Alarm(player.transform.position);
    }

    bool Busy() => Time.timeScale <= 0f || DialogueBox.Busy || TutorialHud.ScreenCovered || Locker.IsPlayerHidden
        || WallGrab.Holding || VentNetwork.IsPlayerInside;

    bool InRing() => InRing(player.transform.position);

    bool InRing(Vector2 point) => InRoom(point, room);

    static bool InRoom(Vector2 point, char marker)
    {
        StationMap map = StationMap.Current;
        return map != null && map.Locate(point, out _, out StationMap.Room here) && here.marker == marker;
    }

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

    public bool Owns(string objective) =>
        !string.IsNullOrEmpty(objective) && (objective == arriveObjective || objective == generatorObjective || objective.StartsWith(interceptObjective)
            || objective == answerObjective || objective == afterObjective);

    public void Resume(string objective, bool past = false)
    {
        StopAllCoroutines();
        int done = past || objective == answerObjective || objective == afterObjective ? Needed
            : objective.StartsWith(interceptObjective) ? Count(objective) : 0;
        for (int i = 0; i < done && i < nodes.Length; i++)
            if (nodes[i] != null) nodes[i].MarkHacked();
        answeredIntercepts = Intercepted;
        toldIntercepted = Mathf.Min(done, PipManager.Lines.commsIntercepted.Length);
        bool verified = past || objective == afterObjective;
        if (verified && relay != null) relay.Verify();
        verifiedPending = false;
        alarmPending = false;

        bool dark = !past && (objective == arriveObjective || objective == generatorObjective);
        if (!dark)
        {
            SuitHelper.Worsen(SuitHelper.Condition.Damaged);
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
        if (generator != null) generator.locked = false;
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

    static int Count(string objective)
    {
        int open = objective.LastIndexOf('('), slash = objective.LastIndexOf('/');
        return open >= 0 && slash > open && int.TryParse(objective.Substring(open + 1, slash - open - 1), out int n) ? n : 0;
    }
}
