using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Something Sonny built out of the station's security cameras: a squat round hub on eight long thin legs, with a ring of
// camera eyes round its rim, each on its own swivel. It walks a room's floor (walkArea, a CrewWalkArea), and goes from
// room to room through the doors (SpiderRoutes): folding itself up small and squeezing through, the door heard going on
// both sides, so a door in the technician's room going on its own means it's coming in. It keeps to the rooms it
// searches (roams), passing through others to get between them (passesThrough), never into the ones left safe.
//   Searching, it's slow. It picks its way somewhere, one leg at a time, and stops there a long while, while its eyes
//   turn this way and that on their own, each one's narrow beam of light sweeping the room. Out of every beam, it can't
//   see the player; caught in one, it starts to make them out (the ring on the floor, StealthMeter).
//   It hears, too: running footsteps from across the room, walking close by, a fight. Crouching makes no sound.
//   Then it changes. It freezes, every eye snaps round to the noise at once with a whine of servos, and it goes to see,
//   not walking but skittering: bursts too fast to follow, dead stops, a shiver of its legs, off at an angle, again.
//   Finding nothing, its eyes jerk about wildly a while before it settles and goes slow again.
//   Sure of them, it shrieks and comes for them the same way, only faster, and catching them is the end of them.
//   Out of sight long enough, it goes to where it last saw them. A locker or the ducts hide them from it. Running out
//   through a door doesn't: right behind them, it follows them through.
// An EMP (Emp) shorts it out: its eyes go dark and its legs fold up under it for a while.
// Made in code, art and all (no prefab): its body's lit by the station's light like anything else, so in the dark
// there's little to see of it but its eyes. Its sounds are the scene's own when it has them (SoundManager: "Spider Step",
// "Spider Crawl", "Spider Twitch", "Spider Shriek"); until then they're made in code. Placed by StationOccupants.
public class CameraSpider : MonoBehaviour
{
    const int LegCount = 8;
    const int Pixels = 32;                  // pixels to a world unit, as the tileset's drawn
    const float HipRadius = 0.28f;
    const float LegReach = 1.05f;
    const float Squash = 0.62f;             // the floor's seen at an angle: round things on it are flatter top to bottom
    const float KneeLift = 0.32f;
    const float LegThickness = 0.06f;
    const float EyeRimX = 0.4f, EyeRimY = 0.26f, EyeLift = 0.12f;
    const float HearFrom = 16f;             // as far off as it can be heard

    public static readonly List<CameraSpider> All = new List<CameraSpider>();

    [Tooltip("The floor of the room it's in.")]
    public CrewWalkArea walkArea;
    [Tooltip("The room it's in, by its marker in the layout.")]
    public char room;
    [Tooltip("The rooms it searches, by their markers. It goes through any door (Teleporter) that isn't locked to get to them.")]
    public string roams = "kbmc";
    [Tooltip("Rooms it'll go through to get somewhere, but never stays in.")]
    public string passesThrough = "h";
    [Tooltip("Chance, each time it moves on while searching, that it heads through a door into another room.")]
    [Range(0f, 1f)] public float wanderChance = 0.35f;
    [Tooltip("How much faster than searching it crosses a room it's only passing through.")]
    public float transitPace = 2.5f;

    [Header("Moving")]
    [Tooltip("Picking its way about, searching. World units a second.")]
    public float searchSpeed = 0.55f;
    [Tooltip("Seconds it stops for at each place it goes, looking about.")]
    public Vector2 pauseSeconds = new Vector2(3f, 7f);
    [Tooltip("Its bursts, going to see. World units a second.")]
    public float burstSpeed = 6f;
    [Tooltip("And coming for the player.")]
    public float huntSpeed = 7.5f;

    [Header("Eyes")]
    [Range(1, 8)] public int eyeCount = 6;
    [Tooltip("How far each eye sees, in the light. The dark cuts it down (DarkRooms.SightScale).")]
    public float sightRange = 6.5f;
    [Tooltip("Half the width of each eye's view, in degrees.")]
    public float eyeHalfAngle = 14f;
    [Tooltip("How far each eye turns either way of where it's mounted, searching, and how fast.")]
    public float sweepDegrees = 80f;
    public float sweepSpeed = 0.3f;
    [Tooltip("How far it sees a crouching player, as a fraction, past arm's length.")]
    [Range(0.1f, 1f)] public float crouchedSight = 0.6f;
    public Color searchColor = new Color(0.75f, 0.92f, 1f);
    public Color alertColor = new Color(1f, 0.66f, 0.25f);
    public Color huntColor = new Color(1f, 0.16f, 0.12f);

    [Header("Ears")]
    [Tooltip("How far off it hears running, walking (not crouched), and fighting.")]
    public float sprintHearing = 8f;
    public float walkHearing = 3.2f;
    public float fightHearing = 9f;

    [Header("Catching")]
    public float catchRadius = 0.6f;
    [Tooltip("What catching them does. More than they've got: it's the end of them.")]
    public float catchDamage = 99f;
    [Tooltip("Seconds out of its sight before it stops coming for them and goes to where it last saw them.")]
    public float loseAfter = 3.5f;
    [Tooltip("Seconds its eyes jerk about where it lost them, or where the noise was, before it settles.")]
    public float franticSeconds = 3.5f;

    [Header("Sounds")]
    [SoundName] public string stepSound = "Spider Step";
    [Tooltip("Its legs going all at once: with each burst when it's skittering, and now and then, quietly, as it creeps.")]
    [SoundName] public string crawlSound = "Spider Crawl";
    [SoundName] public string twitchSound = "Spider Twitch";
    [SoundName] public string shriekSound = "Spider Shriek";

    [Tooltip("Sees and hears nothing: while the technician's taking things in (PatrolReveal).")]
    public bool blind;

    public bool IsHunting => state == State.Hunt;

    // In the round, for the view from inside a locker (FirstPersonView): x and y on the floor, z the height off it.
    // Whether it's there to be seen (not squeezing through a door), its hub, each leg's joints, and each eye.
    public bool Shown => !hidden && isActiveAndEnabled;
    public Vector3 Hub => new Vector3(Ground.x, Ground.y, HubHeight);
    public int LegTotal => LegCount;
    public void LegAt(int i, out Vector3 hip, out Vector3 knee, out Vector3 foot)
    {
        hip = legs[i].hip;
        knee = legs[i].knee;
        foot = legs[i].sole;
    }
    public int EyeTotal => eyes != null ? eyes.Length : 0;
    public void EyeAt(int i, out Vector3 at, out Vector2 look, out Color color, out float beam)
    {
        Eye eye = eyes[i];
        float a = eye.mountAngle * Mathf.Deg2Rad;
        at = Hub + new Vector3(Mathf.Cos(a) * EyeRimX, Mathf.Sin(a) * EyeRimX, 0.08f) * (1f - folded * 0.45f);
        look = Direction(eye.angle);
        color = eye.pupil.color;
        beam = eye.beam.enabled ? eye.beam.intensity : 0f;
    }
    public float BeamReach => sightRange * DarkRooms.SightScale;
    public float BeamHalfAngle => eyeHalfAngle;
    public Color CoreColor => core != null ? core.color : Color.black;

    // Where it stands on the floor (its position is where it's drawn from, a little above), and how high its hub rides.
    Vector2 Ground => (Vector2)transform.position - Vector2.up * (walkArea != null ? walkArea.standHeight : 0.24f);
    float HubHeight => (state == State.Stunned ? 0.18f : 0.42f) * (1f - folded * 0.5f) + (bodyArt != null ? bodyArt.localPosition.y - EyeLift : 0f);

    enum State { Search, Listen, Investigate, Frantic, Hunt, Stunned }

    class Leg
    {
        public Vector2 mount;               // the direction it's set off the hub at
        public Vector2 foot, from, to;      // where the foot is, and stepping, from where to where
        public float step = -1f;            // 0 to 1 through a step, -1 standing
        public float twitch;                // seconds left lifted in a twitch
        public SpriteRenderer upper, lower;
        public Vector3 hip, knee, sole;     // in the round, for the view from a locker (LegAt)
    }

    class Eye
    {
        public float mountAngle, angle, aim, phase, rate;
        public SpriteRenderer housing, pupil;
        public Light2D beam;
    }

    private State state = State.Search;
    private float stateUntil, pauseUntil;
    private float awareness;
    private Vector2 target, lastSeen;
    private float lastSeenAt = -100f, catchCooldownUntil, nextRepath, nextFrantic;
    private float burstLeft, haltLeft, jitterAngle;
    private Vector2 velocity;
    private readonly List<Vector2> path = new List<Vector2>();
    private readonly Leg[] legs = new Leg[LegCount];
    private Eye[] eyes;
    private Transform bodyArt;
    private SpriteRenderer core;
    private PlayerController player;
    private Collider2D playerCollider;
    private Rigidbody2D playerBody;
    private PlayerCombat combat;
    private AudioSource voice;
    private AudioClip crawlClip, twitchClip, shriekClip;
    private float nextCrawl;
    private readonly List<RaycastHit2D> sightHits = new List<RaycastHit2D>();
    private SpiderRoutes.Door door;             // the door it's making for, if it's leaving the room
    private bool travelling, hidden;
    private float folded;                       // 0 standing, 1 folded up small to squeeze through a door
    private char cameFrom;

    string Allowed => roams + passesThrough;

    static Sprite pixel, bodySprite, housingSprite;
    static AudioClip[] taps;
    static AudioClip crawl, servo, screech;

    void Awake()
    {
        All.Add(this);
        DepthSort.Group(gameObject);
        player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
            playerBody = player.GetComponent<Rigidbody2D>();
            combat = player.GetComponent<PlayerCombat>();
        }
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        if (taps == null) taps = Taps();
        if (crawl == null) crawl = Crawl();
        if (servo == null) servo = Servo();
        if (screech == null) screech = Screech();
        crawlClip = crawl;
        twitchClip = servo;
        shriekClip = screech;
        BuildArt();
    }

    void OnDestroy()
    {
        All.Remove(this);
        StealthMeter.Clear(this);
    }

    void Start()
    {
        // Somewhere in its room as far as it can be from the player, so it isn't on top of them to begin with.
        if (walkArea != null)
        {
            Vector2 from = player != null ? (Vector2)player.transform.position : (Vector2)transform.position;
            Vector2 best = transform.position;
            float furthest = -1f;
            for (int i = 0; i < 24; i++)
            {
                if (!walkArea.TryPickDestination(walkArea.CellAt(transform.position), 0f, out Vector2Int cell)) break;
                Vector2 at = walkArea.CenterOf(cell);
                float distance = Vector2.Distance(at, from);
                if (distance > furthest)
                {
                    furthest = distance;
                    best = at;
                }
            }
            transform.position = best;
        }
        if (room == '\0') room = SpiderRoutes.RoomAt(transform.position);
        foreach (Leg leg in legs) leg.foot = Rest(leg);
        pauseUntil = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
    }

    // --- Being dealt with ---

    // Shorted out by an EMP: eyes dark, legs folded, for seconds.
    public void Emp(float seconds)
    {
        Enter(State.Stunned);
        stateUntil = Time.time + seconds;
        awareness = 0f;
        path.Clear();
        HitEffects.Sparks(transform.position, Vector2.up, 14, 4f, 360f, new Color(0.5f, 0.85f, 1f), Color.white);
    }

    // Something told it there was a noise there (the station's speakers hearing them, CrewEchoes, or a camera's alarm,
    // StationCamera): it goes to see. Told again on the way, it keeps going, to the new place.
    public void Hear(Vector2 at)
    {
        if (blind || state == State.Stunned || state == State.Hunt) return;
        awareness = Mathf.Max(awareness, 0.5f);
        if (state == State.Investigate && !travelling)
        {
            target = at;
            path.Clear();
            door = null;
            nextRepath = 0f;
        }
        else if (state != State.Investigate) Notice(at);
    }

    // Forgets the player and goes back to searching, slowly: after they've been caught and come back.
    public void CalmDown()
    {
        awareness = 0f;
        path.Clear();
        Enter(State.Search);
        pauseUntil = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
        StealthMeter.Clear(this);
    }

    // --- Every frame ---

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (travelling)
        {
            StealthMeter.Clear(this);
            return;
        }
        if (state == State.Stunned)
        {
            velocity = Vector2.zero;
            if (Random.value < dt * 3f) HitEffects.Sparks((Vector2)transform.position + Random.insideUnitCircle * 0.4f, Random.insideUnitCircle.normalized, 3, 3f, 140f, new Color(0.5f, 0.85f, 1f), Color.white);
            if (Time.time >= stateUntil) Enter(State.Frantic);
            StealthMeter.Clear(this);
            return;
        }

        Perceive(dt);
        Think();
        Move(dt);
        TryCatch();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        UpdateEyes(dt);
        UpdateLegs(dt);
        UpdateBody();
    }

    // --- Senses ---

    bool PlayerAvailable =>
        player != null && player.isActiveAndEnabled && !blind && !Locker.IsPlayerHidden && !VentNetwork.IsPlayerInside
        && !DraggedBack.Running && !(player.TryGetComponent(out PlayerHealthHandler handler) && handler.IsDying);

    Vector2 PlayerPoint => playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;

    void Perceive(float dt)
    {
        // Through a door with it after them: it knows where they've gone, and keeps after them (Think takes it through).
        if (state == State.Hunt && Teleporter.IsTravelling && player != null && !blind)
        {
            lastSeen = PlayerPoint;
            lastSeenAt = Time.time;
            return;
        }
        if (!PlayerAvailable)
        {
            awareness = Mathf.MoveTowards(awareness, 0f, dt * 0.3f);
            if (state == State.Hunt) Lose();
            StealthMeter.Clear(this);
            return;
        }

        Vector2 at = PlayerPoint;
        float seen = Seeing(at);
        if (seen > 0f)
        {
            awareness = Mathf.Min(1f, awareness + seen * dt);
            lastSeen = at;
            lastSeenAt = Time.time;
            if (state != State.Hunt && state != State.Listen && awareness >= 0.35f) Notice(at);
        }
        else if (state != State.Hunt) awareness = Mathf.MoveTowards(awareness, 0f, dt * 0.25f);

        if (Hearing(at))
        {
            awareness = Mathf.Max(awareness, 0.5f);
            if (state == State.Search || state == State.Frantic) Notice(at);
            else if (state == State.Investigate) target = at;
        }

        if (awareness >= 1f && state != State.Hunt) Enter(State.Hunt);
        // Gone through a door with it right behind them: it knows which, and follows them through.
        if (state == State.Hunt && Time.time - lastSeenAt < loseAfter * 0.5f)
        {
            char theirs = SpiderRoutes.RoomAt(at);
            if (theirs != '\0' && theirs != room) lastSeen = at;
        }
        if (state == State.Hunt && Time.time - lastSeenAt > loseAfter) Lose();

        if (awareness > 0.02f || state == State.Hunt) StealthMeter.Report(this, transform.position, awareness, state == State.Hunt);
        else StealthMeter.Clear(this);
    }

    // How fast it's making them out (0, not at all): caught in an eye's beam, or close by while it's after them.
    float Seeing(Vector2 at)
    {
        Vector2 from = transform.position;
        float distance = Vector2.Distance(from, at);
        float range = sightRange * DarkRooms.SightScale;
        if (player.IsCrouching && distance > 1.5f) range *= crouchedSight;
        bool after = state == State.Hunt || state == State.Investigate;
        if (distance > range * (after ? 1.2f : 1f)) return 0f;

        bool inBeam = after && distance < range;
        if (!inBeam && eyes != null)
            foreach (Eye eye in eyes)
            {
                Vector2 eyeAt = EyePosition(eye);
                Vector2 to = at - eyeAt;
                if (Vector2.Angle(Direction(eye.angle), to) <= eyeHalfAngle) inBeam = true;
            }
        if (!inBeam || !LineOfSight(from, at)) return 0f;
        return Mathf.Lerp(3f, 0.7f, distance / Mathf.Max(0.01f, range));
    }

    bool Hearing(Vector2 at)
    {
        float distance = Vector2.Distance(transform.position, at);
        if (player.IsSprinting && distance <= sprintHearing) return true;
        bool moving = playerBody != null && playerBody.linearVelocity.sqrMagnitude > 0.05f;
        if (moving && !player.IsCrouching && distance <= walkHearing) return true;
        return combat != null && combat.IsBusy && distance <= fightHearing;
    }

    // Nothing solid between, but for the player, the robots, and the crew, standing or fallen.
    bool LineOfSight(Vector2 from, Vector2 to)
    {
        var solidOnly = new ContactFilter2D { useTriggers = false };
        Physics2D.Linecast(from, to, solidOnly, sightHits);
        foreach (RaycastHit2D hit in sightHits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.transform.IsChildOf(player.transform)) return true;
            if (hit.collider.GetComponentInParent<PlaceholderRobot>() != null) continue;
            if (hit.collider.GetComponentInParent<CrewMember>() != null) continue;
            return false;
        }
        return true;
    }

    // Something caught its attention: it freezes, and every eye snaps round to it.
    void Notice(Vector2 at)
    {
        target = at;
        Enter(State.Listen);
    }

    void Lose()
    {
        target = lastSeen;
        awareness = Mathf.Min(awareness, 0.6f);
        Enter(State.Investigate);
    }

    void Enter(State next)
    {
        State was = state;
        state = next;
        path.Clear();
        if (!travelling) door = null;
        nextRepath = 0f;
        burstLeft = haltLeft = 0f;
        switch (next)
        {
            case State.Listen:
                stateUntil = Time.time + Random.Range(0.35f, 0.7f);
                SnapEyes(target);
                Play(twitchSound, twitchClip, 1f, Random.Range(0.9f, 1.15f));
                break;
            case State.Frantic:
                stateUntil = Time.time + franticSeconds;
                break;
            case State.Hunt:
                if (was != State.Hunt) Play(shriekSound, shriekClip, 1f, Random.Range(0.9f, 1.05f));
                SnapEyes(PlayerPoint);
                break;
            case State.Search:
                pauseUntil = Time.time + Random.Range(1f, 2.5f);
                break;
        }
    }

    // --- Deciding where to go ---

    void Think()
    {
        // At the door it was making for: through it.
        if (door != null && path.Count == 0)
        {
            StartCoroutine(GoThrough());
            return;
        }
        switch (state)
        {
            case State.Listen:
                if (Time.time >= stateUntil) Enter(State.Investigate);
                break;
            case State.Investigate:
                if (path.Count == 0 && Time.time >= nextRepath)
                {
                    nextRepath = Time.time + 0.4f;
                    if (!PathTo(target) || Vector2.Distance(transform.position, target) < 0.5f) Enter(State.Frantic);
                }
                break;
            case State.Frantic:
                if (Time.time >= stateUntil)
                {
                    awareness = Mathf.Min(awareness, 0.2f);
                    Enter(State.Search);
                }
                break;
            case State.Hunt:
                if (Time.time >= nextRepath)
                {
                    nextRepath = Time.time + 0.25f;
                    target = lastSeen;
                    PathTo(target);
                }
                break;
            case State.Search:
                if (path.Count > 0 || Time.time < pauseUntil || walkArea == null) break;
                // Only passing through, sharing the room with the other one, or now and then anyway: off through a door
                // into another room.
                bool passing = passesThrough.IndexOf(room) >= 0 || Taken(room);
                if ((passing || Random.value < wanderChance) && HeadThroughADoor()) break;
                if (walkArea.TryPickDestination(walkArea.CellAt(transform.position), 4f, out Vector2Int cell))
                    walkArea.FindPath(walkArea.CellAt(transform.position), cell, path);
                break;
        }
    }

    // Off to one of the doors out of here into a room it's allowed in, and that no other spider's in or making for (they
    // search apart): not straight back where it came from, if there's anywhere else.
    bool HeadThroughADoor()
    {
        List<SpiderRoutes.Door> ways = SpiderRoutes.From(room, Allowed).FindAll(way => !Taken(way.to));
        if (ways.Count == 0) return false;
        List<SpiderRoutes.Door> onward = ways.FindAll(way => way.to != cameFrom);
        List<SpiderRoutes.Door> pick = onward.Count > 0 ? onward : ways;
        return HeadFor(pick[Random.Range(0, pick.Count)]);
    }

    // Another spider's in that room, or on its way there. Only searching keeps them apart: after the player, they'll
    // both come.
    bool Taken(char where)
    {
        foreach (CameraSpider other in All)
            if (other != this && (other.room == where || (other.door != null && other.door.to == where))) return true;
        return false;
    }

    bool HeadFor(SpiderRoutes.Door way)
    {
        path.Clear();
        door = null;
        Vector2Int from = walkArea.CellAt(transform.position), to = walkArea.CellAt(way.at);
        if (from != to && !walkArea.FindPath(from, to, path)) return false;
        door = way;
        return true;
    }

    bool PathTo(Vector2 point)
    {
        path.Clear();
        door = null;
        if (walkArea == null) return false;
        // Somewhere in another room: the way there, a door at a time.
        char goal = SpiderRoutes.RoomAt(point);
        if (goal != '\0' && goal != room)
        {
            // Next door: the one that comes out nearest there, so it's out right behind them.
            SpiderRoutes.Door way = null;
            foreach (SpiderRoutes.Door next in SpiderRoutes.From(room, Allowed))
                if (next.to == goal && (way == null || Vector2.Distance(next.exit, point) < Vector2.Distance(way.exit, point))) way = next;
            if (way == null) way = SpiderRoutes.NextTowards(room, goal, Allowed);
            return way != null && HeadFor(way);
        }
        Vector2Int from = walkArea.CellAt(transform.position);
        if (!Nearest(walkArea.CellAt(point), out Vector2Int to)) return false;
        return walkArea.FindPath(from, to, path) && path.Count > 0;
    }

    // The nearest cell of its floor to this one: the player's often just off it, in a doorway.
    bool Nearest(Vector2Int cell, out Vector2Int found)
    {
        found = cell;
        if (walkArea.IsWalkable(cell)) return true;
        for (int r = 1; r <= 8; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                    var near = new Vector2Int(cell.x + x, cell.y + y);
                    if (!walkArea.IsWalkable(near)) continue;
                    found = near;
                    return true;
                }
        return false;
    }

    // --- Moving ---

    bool Erratic => state == State.Investigate || state == State.Hunt || state == State.Frantic;

    void Move(float dt)
    {
        float speed = 0f;
        // Only passing through, it doesn't dawdle: it scuttles across to the next door.
        if (state == State.Search) speed = passesThrough.IndexOf(room) >= 0 ? searchSpeed * transitPace : searchSpeed;
        else if (state == State.Investigate || state == State.Hunt)
        {
            // Bursts too fast to follow, and dead stops.
            bool hunting = state == State.Hunt;
            if (burstLeft > 0f)
            {
                burstLeft -= dt;
                speed = hunting ? huntSpeed : burstSpeed;
                if (burstLeft <= 0f)
                {
                    haltLeft = hunting ? Random.Range(0.04f, 0.2f) : Random.Range(0.1f, 0.45f);
                    if (Random.value < 0.5f) Play(twitchSound, twitchClip, 0.6f, Random.Range(1f, 1.4f));
                    TwitchLegs(2);
                }
            }
            else if ((haltLeft -= dt) <= 0f)
            {
                burstLeft = hunting ? Random.Range(0.18f, 0.45f) : Random.Range(0.1f, 0.3f);
                jitterAngle = Random.Range(-40f, 40f);
                Play(crawlSound, crawlClip, hunting ? 0.9f : 0.7f, Random.Range(1.05f, 1.35f));
            }
        }

        velocity = Vector2.zero;
        if (speed <= 0f || path.Count == 0) return;
        // Creeping, its legs scrape and tick together now and then, low and quiet.
        if (state == State.Search && Time.time >= nextCrawl)
        {
            nextCrawl = Time.time + Random.Range(2.5f, 5f);
            Play(crawlSound, crawlClip, 0.25f, Random.Range(0.7f, 0.85f));
        }
        Vector2 at = transform.position;
        Vector2 toNext = path[0] - at;
        float step = speed * dt;
        if (toNext.magnitude <= step)
        {
            transform.position = path[0];
            path.RemoveAt(0);
            if (path.Count == 0 && state == State.Search && door == null) pauseUntil = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
            velocity = toNext / dt;
            return;
        }
        Vector2 direction = toNext.normalized;
        // Skittering, it goes off at an angle, where there's floor to.
        if (Erratic && walkArea != null)
        {
            Vector2 skew = Quaternion.Euler(0f, 0f, jitterAngle) * direction;
            if (OnFloor(at + skew * Mathf.Max(step, 0.3f))) direction = skew;
        }
        Vector2 next = at + direction * step;
        // Never into a wall or through furniture: knocked off its line (skittering), the straight way on to the next turn
        // can clip a corner, so it gets back to the middle of the cell it's in and finds its way again from there.
        if (walkArea != null && !OnFloor(next))
        {
            Vector2Int cell = walkArea.CellAt(at);
            if (!walkArea.IsWalkable(cell) && !Nearest(cell, out cell)) return;
            next = Vector2.MoveTowards(at, walkArea.CenterOf(cell), step);
            direction = (next - at).sqrMagnitude > 0f ? (next - at).normalized : Vector2.zero;
            if (path[0] != walkArea.CenterOf(cell)) path.Insert(0, walkArea.CenterOf(cell));
        }
        transform.position = next;
        velocity = direction * speed;
    }

    bool OnFloor(Vector2 point) => walkArea == null || walkArea.IsWalkable(walkArea.CellAt(point));

    // Through the door: it folds itself up small and squeezes into the doorway as the door's heard going, and a moment
    // later unfolds out of the door on the other side, heard there too. Quicker when it's after someone.
    IEnumerator GoThrough()
    {
        SpiderRoutes.Door way = door;
        door = null;
        path.Clear();
        travelling = true;
        velocity = Vector2.zero;
        StealthMeter.Clear(this);
        DoorSound(way.door);
        yield return Fold(1f);
        SetHidden(true);
        yield return new WaitForSeconds(state == State.Hunt ? Random.Range(0.3f, 0.6f) : Random.Range(0.8f, 1.8f));

        cameFrom = room;
        room = way.to;
        walkArea = way.toFloor;
        transform.position = way.exit;
        foreach (Leg leg in legs) leg.foot = Rest(leg);
        DoorSound(way.door);
        SetHidden(false);
        yield return Fold(0f);
        travelling = false;
        if (state == State.Search) pauseUntil = Time.time + Random.Range(0.5f, 2f);
    }

    IEnumerator Fold(float to)
    {
        float from = folded;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            folded = Mathf.Lerp(from, to, t / 0.3f);
            yield return null;
        }
        folded = to;
    }

    void SetHidden(bool hide)
    {
        hidden = hide;
        foreach (SpriteRenderer part in GetComponentsInChildren<SpriteRenderer>(true)) part.enabled = !hide;
    }

    // The door it's going through: its own sound if it has one, otherwise the clank of it squeezing past.
    void DoorSound(Teleporter through)
    {
        if (through != null && !string.IsNullOrEmpty(through.sound)) Sfx.At(through.sound, transform.position, 0.8f);
        else Play(twitchSound, twitchClip, 0.9f, 0.55f);
    }

    void TryCatch()
    {
        if (!(state == State.Hunt || state == State.Investigate) || Time.time < catchCooldownUntil || !PlayerAvailable) return;
        Vector2 at = PlayerPoint;
        Vector2 to = at - (Vector2)transform.position;
        if (to.magnitude > catchRadius) return;
        catchCooldownUntil = Time.time + 1.5f;
        IDamageable victim = Damageable.FromCollider(playerCollider);
        if (victim != null) victim.TakeDamage(new DamageInfo(catchDamage, to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.up, 0f, gameObject, at));
        CameraShake.Shake(0.8f);
        Play(shriekSound, shriekClip, 1f, 0.8f);
        awareness = 0f;
        Enter(State.Frantic);
    }

    // --- Eyes ---

    Vector2 EyePosition(Eye eye)
    {
        float a = eye.mountAngle * Mathf.Deg2Rad;
        return (Vector2)bodyArt.position + new Vector2(Mathf.Cos(a) * EyeRimX, Mathf.Sin(a) * EyeRimY + EyeLift);
    }

    static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    void SnapEyes(Vector2 at)
    {
        if (eyes == null) return;
        foreach (Eye eye in eyes)
        {
            Vector2 to = at - EyePosition(eye);
            eye.angle = eye.aim = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg + Random.Range(-6f, 6f);
        }
    }

    void UpdateEyes(float dt)
    {
        if (eyes == null) return;
        float now = Time.time;
        // Frantic, every so often its eyes all jerk somewhere new at once; between times, now and then one on its own.
        bool jerk = state == State.Frantic && now >= nextFrantic;
        if (jerk) nextFrantic = now + Random.Range(0.12f, 0.4f);
        Color color = state == State.Hunt ? huntColor : state == State.Search ? searchColor : alertColor;
        bool dark = state == State.Stunned;

        foreach (Eye eye in eyes)
        {
            switch (state)
            {
                case State.Search:
                    // Each on its own slow sweep either way of where it's mounted.
                    eye.aim = eye.mountAngle + Mathf.Sin(now * sweepSpeed * eye.rate + eye.phase) * sweepDegrees;
                    eye.angle = Mathf.MoveTowardsAngle(eye.angle, eye.aim, dt * 45f);
                    break;
                case State.Frantic:
                    // Jerking about, each its own way, snapping straight to where it's looking.
                    if (jerk || Random.value < dt * 3f) eye.aim = Random.Range(0f, 360f);
                    eye.angle = Mathf.MoveTowardsAngle(eye.angle, eye.aim, dt * 1400f);
                    break;
                case State.Stunned:
                    break;
                default:
                    // On the noise, or on them.
                    Vector2 look = state == State.Hunt && player != null ? PlayerPoint : target;
                    Vector2 to = look - EyePosition(eye);
                    eye.aim = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                    eye.angle = Mathf.MoveTowardsAngle(eye.angle, eye.aim, dt * 900f);
                    break;
            }

            Vector2 dir = Direction(eye.angle);
            eye.pupil.transform.localPosition = new Vector3(dir.x * 1.2f, dir.y * 0.8f, 0f) / Pixels;
            eye.pupil.color = dark ? new Color(0.1f, 0.1f, 0.12f) : color;
            eye.beam.enabled = !dark && !hidden;
            eye.beam.color = color;
            eye.beam.intensity = (state == State.Search ? 0.55f : 0.85f) * (state == State.Frantic && Random.value < 0.15f ? 0.2f : 1f);
            eye.beam.transform.rotation = Quaternion.Euler(0f, 0f, eye.angle - 90f);
            eye.beam.pointLightOuterRadius = sightRange * DarkRooms.SightScale;
        }
        if (core != null)
            core.color = dark ? new Color(0.12f, 0.12f, 0.14f) : Color.Lerp(new Color(0.4f, 0.05f, 0.05f), color, 0.35f + 0.25f * Mathf.Sin(now * (state == State.Search ? 2f : 9f)));
    }

    // --- Legs ---

    Vector2 Rest(Leg leg)
    {
        float reach = (state == State.Stunned ? LegReach * 0.5f : LegReach) * (1f - folded * 0.75f);
        Vector2 center = transform.position;
        Vector2 foot = center + new Vector2(leg.mount.x * reach, leg.mount.y * reach * Squash);
        // Up against a wall or something solid, the leg draws in and sets its foot down short of it.
        for (int i = 0; i < 3 && !FootRoom(foot); i++)
            foot = Vector2.Lerp(center, foot, 0.6f);
        return foot;
    }

    // Floor a foot can go on: its own floor, or under something it walks over (SpiderRoutes leaves only the walls and
    // furniture out of it).
    bool FootRoom(Vector2 foot) => walkArea == null || walkArea.IsWalkable(walkArea.CellAt(foot + Vector2.up * walkArea.standHeight));

    void TwitchLegs(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Leg leg = legs[Random.Range(0, LegCount)];
            if (leg.step < 0f) leg.twitch = Random.Range(0.06f, 0.14f);
        }
    }

    void UpdateLegs(float dt)
    {
        bool quick = Erratic;
        float stepTime = quick ? 0.07f : 0.34f;
        float threshold = quick ? 0.16f : 0.3f;
        // Shivering in place, while it's frantic or stopped dead.
        if (state == State.Frantic || state == State.Listen) TwitchLegs(Random.value < dt * 8f ? 1 : 0);

        for (int i = 0; i < LegCount; i++)
        {
            Leg leg = legs[i];
            Vector2 rest = Rest(leg);
            if (leg.step >= 0f)
            {
                leg.step += dt / stepTime;
                if (leg.step >= 1f)
                {
                    leg.step = -1f;
                    leg.foot = leg.to;
                    Play(stepSound, taps[Random.Range(0, taps.Length)], quick ? 0.45f : 0.22f, Random.Range(0.9f, 1.15f));
                }
                else leg.foot = Vector2.Lerp(leg.from, leg.to, Mathf.SmoothStep(0f, 1f, leg.step));
            }
            else
            {
                bool neighbourStepping = legs[(i + 1) % LegCount].step >= 0f || legs[(i + LegCount - 1) % LegCount].step >= 0f;
                if (Vector2.Distance(leg.foot, rest) > threshold && !neighbourStepping)
                {
                    leg.from = leg.foot;
                    leg.to = rest + velocity * stepTime * 1.5f;
                    leg.step = 0f;
                }
            }
            leg.twitch = Mathf.Max(0f, leg.twitch - dt);
            float lift = leg.step >= 0f ? Mathf.Sin(leg.step * Mathf.PI) * 0.12f : 0f;
            if (leg.twitch > 0f) lift = 0.1f;
            DrawLeg(leg, lift);
        }
    }

    void DrawLeg(Leg leg, float lift)
    {
        Vector2 center = transform.position;
        Vector2 hip = center + new Vector2(leg.mount.x * HipRadius, leg.mount.y * HipRadius * Squash + EyeLift * 0.5f);
        Vector2 foot = leg.foot + Vector2.up * lift;
        Vector2 out_ = new Vector2(leg.mount.x, leg.mount.y * Squash).normalized;
        float fold = state == State.Stunned ? 0.4f : 1f;
        Vector2 knee = (hip + foot) * 0.5f + out_ * 0.12f + Vector2.up * (KneeLift * fold + lift * 0.6f);
        Segment(leg.upper, hip, knee);
        Segment(leg.lower, knee, foot);

        // The same in the round: the floor's not squashed, and the lifts are heights.
        Vector2 ground = Ground;
        Vector2 offFoot = leg.foot - center;
        Vector2 flatOut = leg.mount;
        leg.hip = new Vector3(ground.x + flatOut.x * HipRadius, ground.y + flatOut.y * HipRadius, HubHeight);
        leg.sole = new Vector3(ground.x + offFoot.x, ground.y + offFoot.y / Squash, lift);
        Vector3 middle = (leg.hip + leg.sole) * 0.5f;
        leg.knee = new Vector3(middle.x + flatOut.x * 0.12f, middle.y + flatOut.y * 0.12f, HubHeight + KneeLift * fold + lift * 0.6f);
    }

    static void Segment(SpriteRenderer part, Vector2 from, Vector2 to)
    {
        Vector2 along = to - from;
        part.transform.position = new Vector3(from.x, from.y, part.transform.position.z);
        part.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
        part.transform.localScale = new Vector3(along.magnitude, LegThickness, 1f);
    }

    // --- Body ---

    void UpdateBody()
    {
        if (bodyArt == null) return;
        // A slow bob as it walks; a shiver while it's stopped dead and listening, or frantic.
        float bob = state == State.Search && velocity.sqrMagnitude > 0f ? Mathf.Sin(Time.time * 7f) * 0.015f : 0f;
        bool shiver = state == State.Listen || state == State.Frantic || (Erratic && burstLeft <= 0f);
        Vector2 shake = shiver ? Random.insideUnitCircle * 0.025f : Vector2.zero;
        float sink = state == State.Stunned ? -0.1f : 0f;
        bodyArt.localPosition = new Vector3(shake.x, EyeLift + bob + shake.y + sink - folded * 0.1f, 0f);
        bodyArt.localScale = Vector3.one * (1f - folded * 0.45f);
    }

    // --- Sound ---

    // Louder the nearer the player, and from the side it's on.
    void Play(string soundName, AudioClip fallback, float volume, float pitch)
    {
        if (player == null) return;
        Vector2 offset = transform.position - player.transform.position;
        float near = 1f - Mathf.Clamp01(offset.magnitude / HearFrom);
        if (near <= 0f) return;
        voice.panStereo = Mathf.Clamp(offset.x / 8f, -0.8f, 0.8f);
        SoundManager.PlayOneShot(voice, soundName, volume * near * near, pitch, fallback);
    }

    // How a struck piece of steel rings: a handful of modes, not in tune with each other, each dying away at its own rate.
    // A thin plate's, roughly, as the deck plating is.
    static readonly float[] PlateModes = { 1f, 2.32f, 4.25f, 6.63f, 9.38f };
    static readonly float[] PlateLoudness = { 1f, 0.6f, 0.42f, 0.28f, 0.18f };
    static readonly float[] PlateDecay = { 26f, 38f, 52f, 68f, 90f };

    // A metal hit added into data at a moment: a short scratchy contact, the plate ringing out at about this pitch, and
    // under it the dull thump of the weight landing.
    static void Strike(float[] data, float at, float pitch, float loud, float ring, System.Random random)
    {
        int start = Mathf.FloorToInt(at * TitleUI.SampleRate);
        int length = Mathf.Min(data.Length - start, Mathf.CeilToInt(0.3f * TitleUI.SampleRate));
        var detune = new float[PlateModes.Length];
        for (int m = 0; m < detune.Length; m++) detune[m] = 1f + ((float)random.NextDouble() - 0.5f) * 0.04f;
        float last = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            float contact = (noise - last) * Mathf.Exp(-t * 900f);      // noise with its lows taken out: a scrape-click
            last = noise;
            float plate = 0f;
            for (int m = 0; m < PlateModes.Length; m++)
                plate += Mathf.Sin(2f * Mathf.PI * pitch * PlateModes[m] * detune[m] * t) * PlateLoudness[m] * Mathf.Exp(-t * PlateDecay[m] / ring);
            float thump = Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t * 40f);
            data[start + i] += (contact * 0.7f + plate * 0.35f + thump * 0.5f) * loud;
        }
    }

    // A metal foot set down on the deck plating: six of them, none quite like another.
    static AudioClip[] Taps()
    {
        var taps = new AudioClip[6];
        for (int i = 0; i < taps.Length; i++)
        {
            var random = new System.Random(40 + i);
            var data = new float[Mathf.CeilToInt(0.3f * TitleUI.SampleRate)];
            Strike(data, 0f, 800f + 110f * i, 0.8f, 0.8f, random);
            // Now and then the foot skids a little and taps twice.
            if (i % 3 == 2) Strike(data, 0.018f, 900f + 90f * i, 0.4f, 0.5f, random);
            taps[i] = Clip("Spider Step", data);
        }
        return taps;
    }

    // Skittering: a clatter of feet one on top of another, too fast to count, with a dry scrape of metal under them.
    static AudioClip Crawl()
    {
        var random = new System.Random(77);
        var data = new float[Mathf.CeilToInt(1.1f * TitleUI.SampleRate)];
        for (float at = 0.01f; at < 0.95f; at += 0.02f + (float)random.NextDouble() * 0.05f)
        {
            float swell = Mathf.Sin(at / 0.95f * Mathf.PI);
            Strike(data, at, 700f + (float)random.NextDouble() * 700f, (0.35f + 0.5f * (float)random.NextDouble()) * swell, 0.6f, random);
        }
        float low = 0f, last = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float p = i / (float)data.Length;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = Mathf.Lerp(low, noise, 0.3f);
            data[i] += (low - last) * 0.25f * Mathf.Sin(p * Mathf.PI);
            last = low;
        }
        return Clip("Spider Crawl", data);
    }

    // Its eyes snapping round: a geared motor whining up and stopping hard, a clunk as it starts and as it stops.
    static AudioClip Servo()
    {
        var random = new System.Random(9);
        var data = new float[Mathf.CeilToInt(0.32f * TitleUI.SampleRate)];
        float phase = 0f;
        const float run = 0.22f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)TitleUI.SampleRate;
            if (t > run + 0.01f) break;
            float p = t / run;
            float f = Mathf.Lerp(180f, 620f, 1f - (1f - p) * (1f - p));
            phase += f / TitleUI.SampleRate;
            float saw = (phase % 1f) * 2f - 1f;
            float square = (phase * 2f) % 1f < 0.5f ? 0.4f : -0.4f;
            float teeth = Mathf.Sqrt(Mathf.Abs(Mathf.Sin(2f * Mathf.PI * f * 0.18f * t)));   // the gear train's teeth, chattering
            float envelope = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((run - t) / 0.015f);
            data[i] = (saw * 0.5f + square) * teeth * envelope * 0.22f;
        }
        Strike(data, 0f, 420f, 0.5f, 0.6f, random);
        Strike(data, run, 360f, 0.8f, 0.9f, random);
        return Clip("Spider Twitch", data);
    }

    // Sure of them: metal on metal, grinding and squealing, the servos straining behind it, falling away.
    static AudioClip Screech()
    {
        var random = new System.Random(13);
        var data = new float[Mathf.CeilToInt(0.85f * TitleUI.SampleRate)];
        float[] partials = { 1f, 1.47f, 2.09f, 2.81f };
        var phases = new float[partials.Length];
        float drift = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float p = i / (float)data.Length;
            float t = i / (float)TitleUI.SampleRate;
            // A bowed edge: its pitch catching and slipping, jittering all the while.
            drift = Mathf.Lerp(drift, ((float)random.NextDouble() - 0.5f) * 0.12f, 0.01f);
            float f = Mathf.Lerp(1500f, 820f, p) * (1f + drift + 0.015f * Mathf.Sin(t * 260f));
            float squeal = 0f;
            for (int k = 0; k < partials.Length; k++)
            {
                phases[k] += f * partials[k] / TitleUI.SampleRate;
                squeal += Mathf.Sin(2f * Mathf.PI * phases[k]) / (k + 1);
            }
            float rasp = ((float)random.NextDouble() * 2f - 1f) * (Mathf.Sin(t * 2f * Mathf.PI * 34f) > 0f ? 1f : 0.3f);
            float envelope = Mathf.Clamp01(p * 25f) * (1f - p) * (1f - p);
            data[i] = Mathf.Clamp(squeal * 0.9f + rasp * 0.3f, -1f, 1f) * envelope * 0.55f;
        }
        return Clip("Spider Shriek", data);
    }

    static AudioClip Clip(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, TitleUI.SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // --- Building ---

    void BuildArt()
    {
        Material lit = null;
        if (player != null)
        {
            var drawn = player.GetComponentInChildren<SpriteRenderer>();
            if (drawn != null) lit = drawn.sharedMaterial;
        }
        Material glow = CombatSprites.EffectMaterial;
        if (pixel == null) pixel = Sprite.Create(PixelArt.MakeTexture(1, 1, (x, y) => (Color32)Color.white), new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        if (bodySprite == null) bodySprite = BodySprite();
        if (housingSprite == null) housingSprite = HousingSprite();

        for (int i = 0; i < LegCount; i++)
        {
            float angle = (i * 360f / LegCount + 22.5f) * Mathf.Deg2Rad;
            var leg = new Leg { mount = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) };
            leg.upper = Part("Leg", pixel, lit, -2, new Color(0.2f, 0.22f, 0.26f));
            leg.lower = Part("Leg", pixel, lit, -2, new Color(0.15f, 0.16f, 0.19f));
            legs[i] = leg;
        }

        bodyArt = new GameObject("Body").transform;
        bodyArt.SetParent(transform, false);
        bodyArt.localPosition = new Vector3(0f, EyeLift, 0f);
        var shell = bodyArt.gameObject.AddComponent<SpriteRenderer>();
        shell.sprite = bodySprite;
        if (lit != null) shell.sharedMaterial = lit;
        core = Part("Core", PixelSprite(3), glow, 1, new Color(0.4f, 0.05f, 0.05f), bodyArt);
        core.transform.localPosition = new Vector3(0f, 0.02f, 0f);

        eyes = new Eye[eyeCount];
        for (int i = 0; i < eyeCount; i++)
        {
            var eye = new Eye
            {
                mountAngle = i * 360f / eyeCount + 90f,
                phase = Random.Range(0f, Mathf.PI * 2f),
                rate = Random.Range(0.6f, 1.4f),
            };
            eye.angle = eye.aim = eye.mountAngle;
            float a = eye.mountAngle * Mathf.Deg2Rad;
            var mount = new Vector3(Mathf.Cos(a) * EyeRimX, Mathf.Sin(a) * EyeRimY, 0f);
            eye.housing = Part("Eye", housingSprite, lit, 2, Color.white, bodyArt);
            eye.housing.transform.localPosition = mount;
            eye.pupil = Part("Lens", PixelSprite(2), glow, 3, searchColor, eye.housing.transform);
            eye.beam = new GameObject("Beam").AddComponent<Light2D>();
            eye.beam.transform.SetParent(eye.housing.transform, false);
            eye.beam.lightType = Light2D.LightType.Point;
            eye.beam.pointLightInnerRadius = 0f;
            eye.beam.pointLightOuterRadius = sightRange;
            eye.beam.pointLightOuterAngle = eyeHalfAngle * 2f;
            eye.beam.pointLightInnerAngle = eyeHalfAngle * 1.2f;
            eye.beam.falloffIntensity = 0.75f;
            eye.beam.shadowsEnabled = true;
            eye.beam.color = searchColor;
            eye.beam.intensity = 0.55f;
            eyes[i] = eye;
        }
    }

    SpriteRenderer Part(string partName, Sprite sprite, Material material, int order, Color color, Transform parent = null)
    {
        var part = new GameObject(partName).AddComponent<SpriteRenderer>();
        part.transform.SetParent(parent != null ? parent : transform, false);
        part.sprite = sprite;
        if (material != null) part.sharedMaterial = material;
        part.sortingOrder = order;
        part.color = color;
        return part;
    }

    static readonly Dictionary<int, Sprite> squares = new Dictionary<int, Sprite>();

    // A square of so many pixels, centred, for a lens or the core.
    static Sprite PixelSprite(int size)
    {
        if (squares.TryGetValue(size, out Sprite sprite)) return sprite;
        Texture2D texture = PixelArt.MakeTexture(size, size, (x, y) => (Color32)Color.white);
        texture.filterMode = FilterMode.Point;
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Pixels);
        squares[size] = sprite;
        return sprite;
    }

    // The hub, seen from a little above: a dark domed shell, lit from the top left, with plating lines round it and a
    // thick dark underside below.
    static Sprite BodySprite()
    {
        const int w = 28, h = 22;
        var top = new Color32(58, 64, 74, 255);
        var light = new Color32(96, 104, 116, 255);
        var rim = new Color32(132, 140, 152, 255);
        var seam = new Color32(34, 38, 45, 255);
        var under = new Color32(22, 24, 29, 255);
        Texture2D texture = PixelArt.MakeTexture(w, h, (x, y) =>
        {
            float dx = (x + 0.5f - w * 0.5f) / 12.5f;
            float dTop = (y + 0.5f - 12.5f) / 8f;
            float dSide = (y + 0.5f - 9.5f) / 8.5f;
            float topDistance = dx * dx + dTop * dTop;
            if (topDistance <= 1f)
            {
                float r = Mathf.Sqrt(topDistance);
                if (r > 0.86f && dx - dTop < -0.2f) return rim;
                if (Mathf.Abs(r - 0.58f) < 0.07f) return seam;
                if (Mathf.Abs(dx) < 0.05f && dTop > 0.6f) return seam;
                return dx - dTop < -0.35f ? light : top;
            }
            if (dx * dx + dSide * dSide <= 1f) return under;
            return default;
        });
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.55f), Pixels);
    }

    // A camera eye's housing: a dark ring round dark glass, the lens drawn over it.
    static Sprite HousingSprite()
    {
        const int size = 6;
        var ring = new Color32(26, 28, 33, 255);
        var glass = new Color32(36, 52, 62, 255);
        var glint = new Color32(150, 170, 185, 255);
        Texture2D texture = PixelArt.MakeTexture(size, size, (x, y) =>
        {
            bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
            if (corner) return default;
            bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
            if (edge) return ring;
            return x == 1 && y == size - 2 ? glint : glass;
        });
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Pixels);
    }
}
