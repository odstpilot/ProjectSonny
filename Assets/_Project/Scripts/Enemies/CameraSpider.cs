using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// spider bot made of security cameras
public class CameraSpider : MonoBehaviour
{
    const int LegCount = 8;
    const int Pixels = 32;
    const float HipRadius = 0.28f;
    const float LegReach = 1.05f;
    const float Squash = 0.62f;
    const float KneeLift = 0.32f;
    const float LegThickness = 0.06f;
    const float EyeRimX = 0.4f, EyeRimY = 0.26f, EyeLift = 0.12f;
    const float HearFrom = 16f;

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

    [Header("Doors")]
    [Tooltip("It won't settle this close to a door, so it can't wait on the player coming back through.")]
    public float doorClearance = 3f;
    [Tooltip("Seconds after the player comes through a door that it can't see, hear or catch them.")]
    public float arrivalGrace = 1.5f;

    [Header("Sounds")]
    [SoundName] public string stepSound = "Spider Step";
    [Tooltip("Its legs going all at once: with each burst when it's skittering, and now and then, quietly, as it creeps.")]
    [SoundName] public string crawlSound = "Spider Crawl";
    [SoundName] public string twitchSound = "Spider Twitch";
    [SoundName] public string shriekSound = "Spider Shriek";

    [Tooltip("Sees and hears nothing: while the technician's taking things in (PatrolReveal).")]
    public bool blind;

    public bool IsHunting => state == State.Hunt;

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

    Vector2 Ground => (Vector2)transform.position - Vector2.up * (walkArea != null ? walkArea.standHeight : 0.24f);
    float HubHeight => (state == State.Stunned ? 0.18f : 0.42f) * (1f - folded * 0.5f) + (bodyArt != null ? bodyArt.localPosition.y - EyeLift : 0f);

    enum State { Search, Listen, Investigate, Frantic, Hunt, Stunned }

    class Leg
    {
        public Vector2 mount;
        public Vector2 foot, from, to;
        public float step = -1f;
        public float twitch;
        public SpriteRenderer upper, lower;
        public Vector3 hip, knee, sole;
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
    private SpiderRoutes.Door door;
    private bool travelling, hidden;
    private float folded;
    private char cameFrom;
    private float dim, graceUntil;
    private bool playerWasTravelling;
    private readonly List<(SpriteRenderer part, Color color)> tinted = new List<(SpriteRenderer, Color)>();

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

    public void Emp(float seconds)
    {
        Enter(State.Stunned);
        stateUntil = Time.time + seconds;
        awareness = 0f;
        path.Clear();
        HitEffects.Sparks(transform.position, Vector2.up, 14, 4f, 360f, new Color(0.5f, 0.85f, 1f), Color.white);
    }

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

    public void CalmDown()
    {
        awareness = 0f;
        path.Clear();
        Enter(State.Search);
        pauseUntil = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
        StealthMeter.Clear(this);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (playerWasTravelling && !Teleporter.IsTravelling) PlayerArrived();
        playerWasTravelling = Teleporter.IsTravelling;
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
        ApplyDim();
    }

    void PlayerArrived()
    {
        graceUntil = Time.time + arrivalGrace;
        if (player == null || travelling || state == State.Hunt || state == State.Stunned) return;
        if (Vector2.Distance(transform.position, player.transform.position) > doorClearance + 1.5f) return;
        CalmDown();
        pauseUntil = Time.time;
    }

    bool NearDoor(Vector2 point)
    {
        foreach (SpiderRoutes.Door way in SpiderRoutes.In(room))
            if (Vector2.Distance(point, way.at) < doorClearance) return true;
        return false;
    }

    bool PlayerAvailable =>
        player != null && player.isActiveAndEnabled && !blind && !Locker.IsPlayerHidden && !VentNetwork.IsPlayerInside
        && !DraggedBack.Running && !(player.TryGetComponent(out PlayerHealthHandler handler) && handler.IsDying);

    Vector2 PlayerPoint => playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;

    void Perceive(float dt)
    {
        if (state == State.Hunt && Teleporter.IsTravelling && player != null && !blind)
        {
            lastSeen = PlayerPoint;
            lastSeenAt = Time.time;
            return;
        }
        if (!PlayerAvailable || Time.time < graceUntil)
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
        if (state == State.Hunt && Time.time - lastSeenAt < loseAfter * 0.5f)
        {
            char theirs = SpiderRoutes.RoomAt(at);
            if (theirs != '\0' && theirs != room) lastSeen = at;
        }
        if (state == State.Hunt && Time.time - lastSeenAt > loseAfter) Lose();

        if (awareness > 0.02f || state == State.Hunt) StealthMeter.Report(this, transform.position, awareness, state == State.Hunt);
        else StealthMeter.Clear(this);
    }

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
        if (SpiderRoutes.RoomAt(at) != room) return false;
        float distance = Vector2.Distance(transform.position, at);
        if (player.IsSprinting && distance <= sprintHearing) return true;
        bool moving = playerBody != null && playerBody.linearVelocity.sqrMagnitude > 0.05f;
        if (moving && !player.IsCrouching && distance <= walkHearing) return true;
        return combat != null && combat.IsBusy && distance <= fightHearing;
    }

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
                stateUntil = Time.time + franticSeconds * (NearDoor(transform.position) ? 0.4f : 1f);
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

    void Think()
    {
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
                if (path.Count == 0 && door == null && NearDoor(transform.position)) pauseUntil = Mathf.Min(pauseUntil, Time.time + 0.5f);
                if (path.Count > 0 || Time.time < pauseUntil || walkArea == null) break;
                bool passing = passesThrough.IndexOf(room) >= 0 || Taken(room);
                if ((passing || Random.value < wanderChance) && HeadThroughADoor()) break;
                for (int tries = 0; tries < 8; tries++)
                {
                    if (!walkArea.TryPickDestination(walkArea.CellAt(transform.position), 4f, out Vector2Int cell)) break;
                    if (NearDoor(walkArea.CenterOf(cell)) && tries < 7) continue;
                    walkArea.FindPath(walkArea.CellAt(transform.position), cell, path);
                    break;
                }
                break;
        }
    }

    bool HeadThroughADoor()
    {
        List<SpiderRoutes.Door> ways = SpiderRoutes.From(room, Allowed).FindAll(way => !Taken(way.to));
        if (ways.Count == 0) return false;
        List<SpiderRoutes.Door> onward = ways.FindAll(way => way.to != cameFrom);
        List<SpiderRoutes.Door> pick = onward.Count > 0 ? onward : ways;
        return HeadFor(pick[Random.Range(0, pick.Count)]);
    }

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
        char goal = SpiderRoutes.RoomAt(point);
        if (goal != '\0' && goal != room)
        {
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

    bool Erratic => state == State.Investigate || state == State.Hunt || state == State.Frantic;

    void Move(float dt)
    {
        float speed = 0f;
        if (state == State.Search) speed = passesThrough.IndexOf(room) >= 0 ? searchSpeed * transitPace : searchSpeed;
        else if (state == State.Investigate || state == State.Hunt)
        {
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
        if (Erratic && walkArea != null)
        {
            Vector2 skew = Quaternion.Euler(0f, 0f, jitterAngle) * direction;
            if (OnFloor(at + skew * Mathf.Max(step, 0.3f))) direction = skew;
        }
        Vector2 next = at + direction * step;
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

    IEnumerator GoThrough()
    {
        SpiderRoutes.Door way = door;
        door = null;
        path.Clear();
        travelling = true;
        velocity = Vector2.zero;
        StealthMeter.Clear(this);
        bool rushing = state == State.Hunt;
        float pace = rushing ? burstSpeed * 0.6f : searchSpeed * 2.5f;

        Teleporter near = way.door, far = way.back;
        if (near != null)
        {
            yield return Walk(near.Mouth, pace, 0f, 0f);
            DoorSound(near);
            near.Swing(true);
            yield return new WaitForSeconds(Teleporter.SwingSeconds);
            yield return Walk(near.Mouth + near.Wall * 0.9f, pace, 1f, 0.6f);
        }
        SetHidden(true);
        if (near != null) near.Swing(false);
        yield return new WaitForSeconds(rushing ? Random.Range(0.3f, 0.6f) : Random.Range(0.8f, 1.8f));

        cameFrom = room;
        room = way.to;
        walkArea = way.toFloor;
        transform.position = far != null ? far.Mouth + far.Wall * 0.9f : way.exit;
        dim = far != null ? 1f : 0f;
        folded = far != null ? 0.6f : 1f;
        foreach (Leg leg in legs) leg.foot = Rest(leg);
        if (far != null)
        {
            DoorSound(far);
            far.Swing(true);
            yield return new WaitForSeconds(Teleporter.SwingSeconds);
        }
        SetHidden(false);
        if (far != null)
        {
            yield return Walk(far.Mouth, pace, 0.2f, 0.3f);
            yield return Walk(way.exit, pace, 0f, 0f);
            far.Swing(false);
        }
        else yield return Fold(0f);
        travelling = false;
        if (state == State.Search) pauseUntil = Time.time + Random.Range(0.5f, 2f);
    }

    IEnumerator Walk(Vector2 to, float pace, float dimTo, float foldTo)
    {
        Vector2 from = transform.position;
        float dimFrom = dim, foldFrom = folded;
        float time = Mathf.Max(0.2f, Vector2.Distance(from, to) / Mathf.Max(0.1f, pace));
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            transform.position = Vector2.Lerp(from, to, k);
            velocity = (to - from) / time;
            dim = Mathf.Lerp(dimFrom, dimTo, k);
            folded = Mathf.Lerp(foldFrom, foldTo, k);
            yield return null;
        }
        transform.position = to;
        velocity = Vector2.zero;
        dim = dimTo;
        folded = foldTo;
    }

    void ApplyDim()
    {
        float lit = 1f - dim;
        foreach ((SpriteRenderer part, Color color) in tinted)
            part.color = new Color(color.r * lit, color.g * lit, color.b * lit, color.a);
        if (dim <= 0f) return;
        if (eyes != null)
            foreach (Eye eye in eyes)
            {
                Color c = eye.pupil.color;
                eye.pupil.color = new Color(c.r * lit, c.g * lit, c.b * lit, c.a);
                eye.beam.intensity *= lit;
            }
        if (core != null) core.color = new Color(core.color.r * lit, core.color.g * lit, core.color.b * lit, core.color.a);
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

    void DoorSound(Teleporter through)
    {
        if (through != null && !string.IsNullOrEmpty(through.sound)) Sfx.At(through.sound, transform.position, 0.8f);
        else Play(twitchSound, twitchClip, 0.9f, 0.55f);
    }

    void TryCatch()
    {
        if (!(state == State.Hunt || state == State.Investigate) || Time.time < catchCooldownUntil || Time.time < graceUntil || !PlayerAvailable) return;
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
        bool jerk = state == State.Frantic && now >= nextFrantic;
        if (jerk) nextFrantic = now + Random.Range(0.12f, 0.4f);
        Color color = state == State.Hunt ? huntColor : state == State.Search ? searchColor : alertColor;
        bool dark = state == State.Stunned;

        foreach (Eye eye in eyes)
        {
            switch (state)
            {
                case State.Search:
                    eye.aim = eye.mountAngle + Mathf.Sin(now * sweepSpeed * eye.rate + eye.phase) * sweepDegrees;
                    eye.angle = Mathf.MoveTowardsAngle(eye.angle, eye.aim, dt * 45f);
                    break;
                case State.Frantic:
                    if (jerk || Random.value < dt * 3f) eye.aim = Random.Range(0f, 360f);
                    eye.angle = Mathf.MoveTowardsAngle(eye.angle, eye.aim, dt * 1400f);
                    break;
                case State.Stunned:
                    break;
                default:
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

    Vector2 Rest(Leg leg)
    {
        float reach = (state == State.Stunned ? LegReach * 0.5f : LegReach) * (1f - folded * 0.75f);
        Vector2 center = transform.position;
        Vector2 foot = center + new Vector2(leg.mount.x * reach, leg.mount.y * reach * Squash);
        for (int i = 0; i < 3 && !FootRoom(foot); i++)
            foot = Vector2.Lerp(center, foot, 0.6f);
        return foot;
    }

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

    void UpdateBody()
    {
        if (bodyArt == null) return;
        float bob = state == State.Search && velocity.sqrMagnitude > 0f ? Mathf.Sin(Time.time * 7f) * 0.015f : 0f;
        bool shiver = state == State.Listen || state == State.Frantic || (Erratic && burstLeft <= 0f);
        Vector2 shake = shiver ? Random.insideUnitCircle * 0.025f : Vector2.zero;
        float sink = state == State.Stunned ? -0.1f : 0f;
        bodyArt.localPosition = new Vector3(shake.x, EyeLift + bob + shake.y + sink - folded * 0.1f, 0f);
        bodyArt.localScale = Vector3.one * (1f - folded * 0.45f);
    }

    void Play(string soundName, AudioClip fallback, float volume, float pitch)
    {
        if (player == null) return;
        Vector2 offset = transform.position - player.transform.position;
        float near = 1f - Mathf.Clamp01(offset.magnitude / HearFrom);
        if (near <= 0f) return;
        voice.panStereo = Mathf.Clamp(offset.x / 8f, -0.8f, 0.8f);
        SoundManager.PlayOneShot(voice, soundName, volume * near * near, pitch, fallback);
    }

    static readonly float[] PlateModes = { 1f, 2.32f, 4.25f, 6.63f, 9.38f };
    static readonly float[] PlateLoudness = { 1f, 0.6f, 0.42f, 0.28f, 0.18f };
    static readonly float[] PlateDecay = { 26f, 38f, 52f, 68f, 90f };

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
            float contact = (noise - last) * Mathf.Exp(-t * 900f);
            last = noise;
            float plate = 0f;
            for (int m = 0; m < PlateModes.Length; m++)
                plate += Mathf.Sin(2f * Mathf.PI * pitch * PlateModes[m] * detune[m] * t) * PlateLoudness[m] * Mathf.Exp(-t * PlateDecay[m] / ring);
            float thump = Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t * 40f);
            data[start + i] += (contact * 0.7f + plate * 0.35f + thump * 0.5f) * loud;
        }
    }

    static AudioClip[] Taps()
    {
        var taps = new AudioClip[6];
        for (int i = 0; i < taps.Length; i++)
        {
            var random = new System.Random(40 + i);
            var data = new float[Mathf.CeilToInt(0.3f * TitleUI.SampleRate)];
            Strike(data, 0f, 800f + 110f * i, 0.8f, 0.8f, random);
            if (i % 3 == 2) Strike(data, 0.018f, 900f + 90f * i, 0.4f, 0.5f, random);
            taps[i] = Clip("Spider Step", data);
        }
        return taps;
    }

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
            float teeth = Mathf.Sqrt(Mathf.Abs(Mathf.Sin(2f * Mathf.PI * f * 0.18f * t)));
            float envelope = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((run - t) / 0.015f);
            data[i] = (saw * 0.5f + square) * teeth * envelope * 0.22f;
        }
        Strike(data, 0f, 420f, 0.5f, 0.6f, random);
        Strike(data, run, 360f, 0.8f, 0.9f, random);
        return Clip("Spider Twitch", data);
    }

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
            tinted.Add((leg.upper, leg.upper.color));
            tinted.Add((leg.lower, leg.lower.color));
            legs[i] = leg;
        }

        bodyArt = new GameObject("Body").transform;
        bodyArt.SetParent(transform, false);
        bodyArt.localPosition = new Vector3(0f, EyeLift, 0f);
        var shell = bodyArt.gameObject.AddComponent<SpriteRenderer>();
        shell.sprite = bodySprite;
        if (lit != null) shell.sharedMaterial = lit;
        tinted.Add((shell, shell.color));
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
            tinted.Add((eye.housing, eye.housing.color));
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

    static Sprite PixelSprite(int size)
    {
        if (squares.TryGetValue(size, out Sprite sprite)) return sprite;
        Texture2D texture = PixelArt.MakeTexture(size, size, (x, y) => (Color32)Color.white);
        texture.filterMode = FilterMode.Point;
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Pixels);
        squares[size] = sprite;
        return sprite;
    }

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
