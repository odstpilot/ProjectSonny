using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// What's in each cell of a VentNetwork's map.
public enum VentCell : byte { Solid, Duct, Dent, Squeeze, Grate, Hatch }

// A network of air ducts the player crawls through in first person, from one VentGrate to another.
// The ducts aren't built in the level: they're a map painted in this component's inspector (VentNetworkEditor), so
// they can wind about as much as they like. Climbing in at a grate takes the player out of the world the way a Locker does (enemies treat
// them as hidden) and puts them on the map at that grate's number; climbing out at another number puts them at the
// VentGrate with that number.
//
// In the ducts:
//   W crawls forward a cell, S backs up one, A and D turn. Hold C or Ctrl to crawl carefully (slow and quiet), or
//   Shift to hurry (fast and loud). E climbs out over a grate.
//   The flashlight only reaches a couple of cells (VentView). Past that it's black, though light from a grate shows further.
//   Noise is what gives the player away. Every cell crawled makes a little (none crawling carefully), a dented panel
//   bangs when crawled over (it only creaks if careful), hurrying is loud on any panel, and crawling head first into a
//   wall is a hollow bang that rings down the ducts. It fades again if they keep quiet.
//   A tight squeeze takes one key at a time: a letter from anywhere on the keyboard comes up (SqueezeLetters: every
//   letter but the ones the game already uses everywhere, like M for the map) and has to be pressed before its time
//   runs out. There's a good while to find it. The right key inches the player through; the wrong one, or being too
//   slow, scrapes loudly and slips them back.
//   There's no room to turn inside a squeeze.
//
// The drone:
//   When the noise fills up, eyes stare out of the dark for a moment, and then a drone drops out of the nearest hatch
//   that isn't right on top of the player. It doesn't chase. It hangs there and looks around, turning its red
//   searchlight down one duct and then another (toward any real noise first), and after a while it rises back into its
//   hatch. If its light finds the player straight down a duct, or they're right under it, it locks on with a shriek;
//   still in its sight when the lock's done, and it's on them: caught, the usual death, back at the last checkpoint.
//   Out of its sight before then, and it goes back to looking. Its hum is louder the nearer it is along the ducts, and
//   comes from whichever side it's on.
//
// The dark:
//   The whole time they're in, the ducts make noises from one side or the other (scareSounds): groans,
//   knocking, something skittering, something breathing. A deeper loop sits under the ambience, and their heartbeat
//   gets louder as the noise fills and while the drone is out.
//   On an x cell, the first time, facing down a duct, a rat bolts out of the dark straight at them, squealing: a
//   jump scare, and a lot of noise.
//
// The map is kept as text, one character per cell, top row first:
//   # or space  solid        .  duct        d  dented panel        s  tight squeeze (keep them to straight runs)
//   r           a drone hatch in the ceiling. With none, the drone comes from somewhere a few cells away.
//   x           a duct where the rat comes at them
//   1 - 9       a grate, where the VentGrate with that number opens into the ducts
public class VentNetwork : MonoBehaviour
{
    public const KeyCode ClimbOutKey = KeyCode.E;
    const float BumpTime = 0.28f;
    const float SqueezeWedgeTime = 0.4f;    // a moment to get wedged in before the first key counts
    const float SqueezeInchSpeed = 3f;      // how fast the view catches up with the keys pressed, in squeezes per second
    const float FinishSqueezeTime = 0.2f;
    const float CatchDistance = 0.55f;      // cells between the drone and the player's eyes that count as caught
    const float CaughtTime = 0.6f;
    const int DroneAudibleCells = 12;       // its hum fades out this many cells away along the ducts

    // Up, right, down, left the map, clockwise, so a direction times 90 is the angle the view faces.
    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    // Every letter but M (the map), L (Pip's log), P (pause), Q (looking at things), and F.
    const string SqueezeLetters = "ABCDEGHIJKNORSTUVWXYZ";
    static readonly KeyCode[] SqueezeKeys = MakeSqueezeKeys();

    static KeyCode[] MakeSqueezeKeys()
    {
        var keys = new KeyCode[SqueezeLetters.Length];
        for (int i = 0; i < keys.Length; i++) keys[i] = KeyCode.A + (SqueezeLetters[i] - 'A');
        return keys;
    }

    // What a new network starts with, and what the inspector's Example button puts back.
    public const string ExampleLayout =
        "###########\n" +
        "#1..d.....#\n" +
        "#.#######.#\n" +
        "#.#r....#s#\n" +
        "#.#####.#.#\n" +
        "#...d.....#\n" +
        "#####.###.#\n" +
        "#r....#..2#\n" +
        "###########";

    [Tooltip("The ducts, one character per cell, top row first. Painted in the inspector's grid.")]
    [TextArea(8, 30)] public string layout = ExampleLayout;

    [Header("Crawling")]
    [Tooltip("Seconds to crawl forward one cell.")]
    public float crawlTime = 0.6f;
    [Tooltip("Seconds per cell while holding C or Ctrl. Slow, but a dented panel only creaks.")]
    public float carefulCrawlTime = 1.4f;
    [Tooltip("Seconds per cell while holding Shift. Fast, but loud on every panel.")]
    public float hurriedCrawlTime = 0.32f;
    [Tooltip("Seconds to back up one cell.")]
    public float backUpTime = 0.9f;
    [Tooltip("Seconds to turn a quarter of the way round.")]
    public float turnTime = 0.15f;

    [Header("Noise (1 fills the meter)")]
    [Tooltip("Each cell crawled at a normal pace. Crawling carefully makes none.")]
    public float crawlNoise = 0.035f;
    [Tooltip("Crawling over a dented panel.")]
    public float dentNoise = 0.45f;
    [Tooltip("Crawling carefully over a dented panel.")]
    public float carefulDentNoise = 0.1f;
    [Tooltip("Each cell crawled while hurrying.")]
    public float hurriedNoise = 0.12f;
    [Tooltip("Crawling head first into a wall: a bang that rings down the ducts.")]
    public float bumpNoise = 0.35f;
    [Tooltip("Each wrong key, or too slow a key, in a squeeze.")]
    public float squeezeMistakeNoise = 0.5f;
    [Tooltip("Seconds of quiet before the noise starts to fade.")]
    public float noiseFadeDelay = 2f;
    [Tooltip("How much the noise fades each second after that.")]
    public float noiseFade = 0.08f;

    [Header("Tight Squeezes")]
    [Tooltip("Keys to press to get through one squeeze cell.")]
    [Range(1, 20)] public int squeezeKeys = 6;
    [Tooltip("Seconds to press each one.")]
    public float squeezeKeyTime = 2.4f;

    [Header("Drone")]
    [Tooltip("Seconds the eyes stare out of the dark before the drone is let out.")]
    public float stareTime = 1.4f;
    [Tooltip("It senses the player this many cells away, whichever way it's looking.")]
    public int droneHearing = 1;
    [Tooltip("Its light finds the player this many cells straight down the duct it's looking along, with nothing in the way.")]
    public int droneSight = 5;
    [Tooltip("Noises louder than this turn it to look the way they came from. Bumps and careful creaks are quieter.")]
    public float droneAlertNoise = 0.1f;
    [Tooltip("Seconds it looks around before rising back into its hatch.")]
    public float droneSearchTime = 9f;
    [Tooltip("Seconds it looks down each duct before turning to another.")]
    public float droneLookEvery = 1.7f;
    [Tooltip("Seconds of being seen before it's on them. Out of its sight before then, and it goes back to looking.")]
    public float droneLockTime = 0.9f;
    [Tooltip("It comes out of the nearest hatch at least this many cells from the player along the ducts.")]
    public int droneMinReleaseDistance = 3;

    [Header("Looks and Sound")]
    [Tooltip("Seconds for each half of the fade through black on the way in and out.")]
    public float fadeTime = 0.25f;
    [Tooltip("Font for the hints and the squeeze keys. Leave empty for TextMesh Pro's default.")]
    public TMP_FontAsset font;
    [Header("Sounds (the scene's SoundManager, by name)")]
    [Tooltip("Loops while the player is in the ducts.")]
    [SoundName] public string ambienceSound = "Vent Ambience";
    [Tooltip("A deeper loop under the ambience.")]
    [SoundName] public string deepSound = "Vent Deep Loop";
    [Tooltip("A hand or knee coming down on the metal. Played pitched down.")]
    [SoundName] public string crawlSound = "Vent Crawl";
    [Tooltip("A dented panel banging. Also used, quieter, for creaks and bumps.")]
    [SoundName] public string dentSound = "Vent Dent";
    [Tooltip("Crawling head first into a duct wall: a hollow metal bang.")]
    [SoundName] public string bangSound = "Vent Bang";
    [Tooltip("Scraping the duct on a wrong key in a squeeze.")]
    [SoundName] public string scrapeSound = "Vent Scrape";
    [Tooltip("The noise filling up, as the eyes appear.")]
    [SoundName] public string foundSound = "Vent Found";
    [Tooltip("The player's heartbeat: louder as the noise fills, and while the drone is out. Loops.")]
    [SoundName] public string heartbeatSound = "Vent Heartbeat";
    [Tooltip("The drone's hatch opening and it powering up.")]
    [SoundName] public string droneReleaseSound = "Drone Release";
    [Tooltip("The drone's hum, looping while it's out.")]
    [SoundName] public string droneHumSound = "Drone Hum";
    [Tooltip("The drone turning to look somewhere else.")]
    [SoundName] public string droneServoSound = "Drone Servo";
    [Tooltip("The drone locking on.")]
    [SoundName] public string droneLockSound = "Drone Lock";
    [Tooltip("The drone reaching the player.")]
    [SoundName] public string caughtSound = "Drone Caught";
    [Tooltip("The rat's feet, and its squeal.")]
    [SoundName] public string ratScurrySound = "Rat Scurry";
    [SoundName] public string ratSquealSound = "Rat Squeal";

    [Header("Scares")]
    [Tooltip("Played now and then, one at random, from one side or the other while the player's in the ducts. Listed twice comes up twice as often.")]
    [SoundName] public string[] scareSounds = { "Vent Scare", "Vent Scare", "Vent Creak", "Vent Creak", "Vent Skitter", "Vent Breath", "Vent Knock", "Vent Whisper" };
    [Tooltip("Seconds between them, at least and at most.")]
    public Vector2 scareEvery = new Vector2(4f, 10f);
    [Tooltip("How much noise the rat makes bolting past (1 fills the meter).")]
    public float ratNoise = 0.7f;
    [Tooltip("Cells a second the rat runs.")]
    public float ratSpeed = 7f;

    // True from climbing in at a grate until standing outside again. Locker.IsPlayerHidden counts this too.
    public static bool IsPlayerInside { get; private set; }

    public bool InUse => state != State.Outside;
    public VentCell[,] Cells => cells;

    enum State { Outside, Climbing, Idle, Moving, Squeezing, Caught }
    enum Drone { None, Looking, Locking, Returning }

    private VentCell[,] cells;
    private int[,] grateNumbers;
    private State state = State.Outside;
    private VentView view;
    private VentGrate entrance;
    private AudioSource sfx;
    private AudioSource ambience;
    private AudioSource droneAudio;

    private Transform player;
    private Rigidbody2D playerBody;
    private Health playerHealth;
    private PlayerHealthHandler playerHealthHandler;
    private List<Behaviour> frozen;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private bool playerWasSimulated;

    private Vector2Int cell;
    private int direction;
    private Vector2 eye;
    private float angle;
    private float bob;
    private bool waitForRelease;            // ignore W A S D until they're let go, so a held key doesn't carry on by itself
    private float noise;
    private float quietTime;
    private Coroutine alarm;

    private Vector2Int squeezeFrom;
    private Vector2Int squeezeTo;
    private int squeezeDone;
    private float squeezeShown;
    private int squeezeKey;
    private float squeezeTimer;
    private float squeezeGrace;

    private Drone drone;
    private Vector2Int droneCell;           // its hatch, where it hangs
    private int droneFacing;                // which way it's looking, as an index into Directions
    private float droneAngle;               // and the angle its light is at, turning toward that
    private float droneLookTimer;           // until it goes back up
    private float droneTurnTimer;           // until it looks somewhere else
    private float droneLock;                // seconds into locking on
    private float droneRise;                // 0 up in its hatch, 1 hanging in the duct
    private Vector2 dronePosition;

    private AudioSource scareAudio;
    private AudioSource deepAudio;
    private AudioSource heartAudio;
    private Coroutine haunting;
    private string lastScare;
    private readonly HashSet<Vector2Int> ratSpots = new HashSet<Vector2Int>();
    private bool ratDone;
    private int[,] playerDistances;         // cells along the ducts from the player to everywhere
    private int[,] pathDistances;           // and from wherever the drone is heading
    private readonly Queue<Vector2Int> floodQueue = new Queue<Vector2Int>();
    private readonly List<Vector2Int> choices = new List<Vector2Int>();

    void Awake()
    {
        ReadLayout();
        sfx = NewAudioSource(false);
        ambience = NewAudioSource(true);
        droneAudio = NewAudioSource(true);
        scareAudio = NewAudioSource(false);
        deepAudio = NewAudioSource(true);
        heartAudio = NewAudioSource(true);
    }

    // Edits to the map while playing take effect the next time the player climbs in.
    void OnValidate()
    {
        if (Application.isPlaying && state == State.Outside) ReadLayout();
    }

    void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return;

        player = found.transform;
        playerBody = found.GetComponent<Rigidbody2D>();
        playerHealth = found.GetComponent<Health>();
        playerHealthHandler = found.GetComponent<PlayerHealthHandler>();
    }


    void OnDisable()
    {
        if (state == State.Outside) return;

        // Switched off or unloaded with the player inside: put everything back at once instead of trapping them.
        StopAllCoroutines();
        alarm = null;
        RemoveDrone();
        if (view != null)
        {
            view.HideKey();
            view.SetEyes(0f);
            view.SetPanic(false);
            view.Close();
            view.SetFade(0f);
        }
        StopDark();
        if (player != null) ShowPlayer(entrance != null ? entrance.ExitPoint : (Vector2)player.position);
        IsPlayerInside = false;
        entrance = null;
        state = State.Outside;
    }

    // Called by a VentGrate when the player uses it.
    public void Enter(VentGrate grate)
    {
        if (state != State.Outside || IsPlayerInside || player == null || grate == null) return;

        if (!TryFindGrate(grate.number, out Vector2Int start))
        {
            Debug.LogWarning($"{name}: there's no {grate.number} on the vent map, so {grate.name} doesn't lead anywhere.", grate);
            return;
        }
        StartCoroutine(ClimbIn(grate, start));
    }

    void Update()
    {
        // timeScale 0 means a menu paused the game.
        if (state == State.Outside || Time.timeScale <= 0f) return;

        bool crawling = state == State.Idle || state == State.Moving || state == State.Squeezing;
        if (!crawling) return;

        quietTime += Time.deltaTime;
        if (quietTime >= noiseFadeDelay) noise = Mathf.MoveTowards(noise, 0f, noiseFade * Time.deltaTime);

        if (state == State.Idle)
        {
            TryRat();
            ReadInput();
        }
        else if (state == State.Squeezing) UpdateSqueeze();

        if (drone != Drone.None && state != State.Caught) UpdateDrone();
    }

    void LateUpdate()
    {
        if (state == State.Outside || view == null) return;

        view.SetCamera(eye, angle, bob);
        view.SetNoise(noise);
        view.SetTight(state == State.Squeezing || (state != State.Caught && CellAt(cell) == VentCell.Squeeze));
        view.SetHint(state == State.Idle && CellAt(cell) == VentCell.Grate ? $"Press {ClimbOutKey} to climb out" : null);
        view.SetDrone(drone != Drone.None, dronePosition);
        view.SetDroneLook(droneAngle, drone == Drone.Locking ? Mathf.Clamp01(droneLock / Mathf.Max(0.05f, droneLockTime)) : 0f, droneRise);
        view.SetAlert(drone == Drone.Locking || state == State.Caught ? VentView.Alert.Chasing
            : drone != Drone.None ? VentView.Alert.Searching
            : VentView.Alert.None);
        UpdateDroneAudio();
        UpdateHeartbeat();
    }

    // --- Getting in and out ---

    IEnumerator ClimbIn(VentGrate grate, Vector2Int start)
    {
        state = State.Climbing;
        IsPlayerInside = true;
        entrance = grate;
        HidePlayer(grate.transform.position);
        grate.Rattle();
        Play(dentSound, 0.35f, 0.8f);

        view = VentView.Get(font);
        yield return view.FadeTo(1f, fadeTime);

        cell = start;
        direction = OpenDirection(start);
        eye = Center(start);
        angle = direction * 90f;
        bob = 0f;
        noise = 0f;
        quietTime = 0f;
        waitForRelease = true;
        RemoveDrone();
        view.Open(this);
        view.SetCamera(eye, angle, bob);
        StartDark();

        yield return view.FadeTo(0f, fadeTime);
        state = State.Idle;
    }

    void ClimbOut()
    {
        VentGrate grate = VentGrate.Find(this, grateNumbers[cell.x, cell.y]);
        if (grate == null)
        {
            // A number on the map with no grate in the level.
            view.FlashHint("Bolted shut", 1.5f);
            Play(dentSound, 0.25f, 1.3f);
            return;
        }
        StartCoroutine(ClimbOutThrough(grate));
    }

    IEnumerator ClimbOutThrough(VentGrate grate)
    {
        state = State.Climbing;
        yield return view.FadeTo(1f, fadeTime);

        // Out of its reach. Whatever was after them stays in the ducts.
        StopAlarm();
        RemoveDrone();
        StopDark();
        view.Close();
        ShowPlayer(grate.ExitPoint);
        IsPlayerInside = false;
        entrance = null;
        grate.Rattle();
        Play(dentSound, 0.35f, 0.8f);

        yield return view.FadeTo(0f, fadeTime);
        state = State.Outside;
    }

    // Takes the player out of the world like a Locker does: no controls, no sprite, no physics, so nothing out there
    // can see, touch, or hit them. Whatever gets turned off is remembered so exactly that comes back.
    void HidePlayer(Vector2 at)
    {
        frozen = PlayerControls.Freeze(player);

        hiddenRenderers.Clear();
        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            renderer.enabled = false;
            hiddenRenderers.Add(renderer);
        }

        if (playerBody != null)
        {
            playerWasSimulated = playerBody.simulated;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.simulated = false;
        }
        player.position = new Vector3(at.x, at.y, player.position.z);
    }

    void ShowPlayer(Vector2 at)
    {
        if (player == null) return;

        player.position = new Vector3(at.x, at.y, player.position.z);
        if (playerBody != null)
        {
            playerBody.simulated = playerWasSimulated;
            playerBody.position = at;
            playerBody.linearVelocity = Vector2.zero;
        }

        foreach (Renderer renderer in hiddenRenderers)
            if (renderer != null) renderer.enabled = true;
        hiddenRenderers.Clear();
        PlayerControls.Release(frozen);
    }

    // --- Crawling ---

    void ReadInput()
    {
        bool anyHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
        if (waitForRelease)
        {
            if (anyHeld) return;
            waitForRelease = false;
        }

        if (CellAt(cell) == VentCell.Grate && Input.GetKeyDown(ClimbOutKey))
        {
            ClimbOut();
            return;
        }

        bool careful = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool hurried = !careful && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        if (Input.GetKey(KeyCode.W))
        {
            Move(direction, false, careful, hurried, Input.GetKeyDown(KeyCode.W));
        }
        else if (Input.GetKey(KeyCode.S))
        {
            Move((direction + 2) % 4, true, careful, hurried, Input.GetKeyDown(KeyCode.S));
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
        {
            if (CellAt(cell) != VentCell.Squeeze)
                StartCoroutine(Turn(Input.GetKey(KeyCode.A) ? -1 : 1));
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D))
                view.FlashHint("No room to turn", 1.2f);
        }
    }

    // One cell forward or back, if there's duct there. Into a squeeze, it takes the keys.
    void Move(int way, bool backwards, bool careful, bool hurried, bool pressedNow)
    {
        Vector2Int to = cell + Directions[way];
        VentCell next = CellAt(to);

        if (next == VentCell.Solid)
        {
            // Once per press, so holding W at a dead end doesn't keep banging the player's head.
            if (pressedNow) StartCoroutine(Bump(way));
            return;
        }

        if (next == VentCell.Squeeze)
        {
            StartSqueeze(to);
            return;
        }

        float time = backwards ? backUpTime : careful ? carefulCrawlTime : hurried ? hurriedCrawlTime : crawlTime;
        if (backwards && careful) time *= carefulCrawlTime / Mathf.Max(0.01f, crawlTime);
        StartCoroutine(Crawl(to, time, careful, hurried && !backwards));
    }

    IEnumerator Crawl(Vector2Int to, float time, bool careful, bool hurried)
    {
        state = State.Moving;
        Vector2 from = eye;
        Vector2 target = Center(to);
        float volume = careful ? 0.15f : hurried ? 0.8f : 0.4f;
        PlayCrawl(volume);

        bool arrived = false;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float progress = t / time;
            eye = Vector2.Lerp(from, target, Ease(progress));
            // A hand comes down at the start and again halfway, and the head dips between.
            bob = -Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 2f)) * (hurried ? 0.06f : 0.035f);

            if (!arrived && progress >= 0.5f)
            {
                arrived = true;
                PlayCrawl(volume);
                Arrive(to, careful, hurried);
            }
            yield return null;
            if (state != State.Moving) yield break;     // caught on the way
        }

        if (!arrived) Arrive(to, careful, hurried);
        eye = target;
        bob = 0f;
        state = State.Idle;
    }

    // The player's weight comes down on the new cell.
    void Arrive(Vector2Int to, bool careful, bool hurried)
    {
        cell = to;
        if (CellAt(to) == VentCell.Dent)
        {
            if (careful)
            {
                Play(dentSound, 0.2f, 0.6f);
                view.Shake(0.15f);
                AddNoise(carefulDentNoise, 0.3f);
            }
            else
            {
                Play(dentSound, 1f, Random.Range(0.85f, 1f));
                view.Shake(hurried ? 1f : 0.7f);
                AddNoise(dentNoise + (hurried ? hurriedNoise : 0f), 1f);
            }
        }
        else if (hurried)
        {
            AddNoise(hurriedNoise, 0.35f);
        }
        else if (!careful)
        {
            AddNoise(crawlNoise, 0.15f);
        }
    }

    IEnumerator Turn(int way)
    {
        state = State.Moving;
        PlayCrawl(0.12f);
        float from = direction * 90f;
        float to = from + way * 90f;
        for (float t = 0f; t < turnTime; t += Time.deltaTime)
        {
            angle = Mathf.Lerp(from, to, Ease(t / turnTime));
            yield return null;
            if (state != State.Moving) yield break;
        }
        direction = (direction + way + 4) % 4;
        angle = direction * 90f;
        state = State.Idle;
    }

    // Crawling into a wall in the dark: the head knocks against it.
    IEnumerator Bump(int way)
    {
        state = State.Moving;
        Vector2 home = Center(cell);
        Vector2 push = (Vector2)Directions[way] * 0.28f;
        bool hit = false;
        for (float t = 0f; t < BumpTime; t += Time.deltaTime)
        {
            float progress = t / BumpTime;
            eye = home + push * Mathf.Sin(progress * Mathf.PI);
            if (!hit && progress >= 0.5f)
            {
                hit = true;
                Play(bangSound, 1f, Random.Range(0.9f, 1.08f));
                view.Shake(0.75f);
                AddNoise(bumpNoise, 0.9f);
            }
            yield return null;
            if (state != State.Moving) yield break;
        }
        eye = home;
        state = State.Idle;
    }

    // --- Tight squeezes ---

    void StartSqueeze(Vector2Int to)
    {
        state = State.Squeezing;
        squeezeFrom = cell;
        squeezeTo = to;
        squeezeDone = 0;
        squeezeShown = 0f;
        squeezeKey = -1;
        squeezeGrace = SqueezeWedgeTime;
        NextSqueezeKey();
        PlayCrawl(0.3f);
        view.ShowKey(SqueezeLetters[squeezeKey], 1f);
    }

    void UpdateSqueeze()
    {
        // Inch through as the keys go in.
        squeezeShown = Mathf.MoveTowards(squeezeShown, (float)squeezeDone / squeezeKeys, SqueezeInchSpeed * Time.deltaTime);
        eye = Vector2.Lerp(Center(squeezeFrom), Center(squeezeTo), squeezeShown);
        bob = Mathf.Sin(Time.time * 9f) * 0.008f;   // straining

        if (squeezeGrace > 0f)
        {
            squeezeGrace -= Time.deltaTime;
            return;
        }

        int pressed = -1;
        for (int i = 0; i < SqueezeKeys.Length; i++)
        {
            if (!Input.GetKeyDown(SqueezeKeys[i])) continue;
            pressed = i;
            break;
        }

        squeezeTimer -= Time.deltaTime;
        if (pressed == squeezeKey)
        {
            squeezeDone++;
            PlayCrawl(0.25f);
            if (squeezeDone >= squeezeKeys)
            {
                StartCoroutine(FinishSqueeze());
                return;
            }
            NextSqueezeKey();
        }
        else if (pressed >= 0 || squeezeTimer <= 0f)
        {
            // The wrong key, or too slow: the duct scrapes, and the player slips back a little.
            squeezeDone = Mathf.Max(0, squeezeDone - 1);
            Play(scrapeSound, 1f, Random.Range(0.9f, 1.1f));
            view.KeyMistake();
            AddNoise(squeezeMistakeNoise, 1f);
            NextSqueezeKey();
        }

        view.ShowKey(SqueezeLetters[squeezeKey], squeezeTimer / squeezeKeyTime);
    }

    // A different key from the last one, so every press is a fresh one to read.
    void NextSqueezeKey()
    {
        if (squeezeKey < 0)
        {
            squeezeKey = Random.Range(0, SqueezeKeys.Length);
        }
        else
        {
            int next = Random.Range(0, SqueezeKeys.Length - 1);
            squeezeKey = next >= squeezeKey ? next + 1 : next;
        }
        squeezeTimer = squeezeKeyTime;
    }

    IEnumerator FinishSqueeze()
    {
        state = State.Moving;
        view.HideKey();
        cell = squeezeTo;
        Vector2 from = eye;
        Vector2 target = Center(squeezeTo);
        for (float t = 0f; t < FinishSqueezeTime; t += Time.deltaTime)
        {
            eye = Vector2.Lerp(from, target, Ease(t / FinishSqueezeTime));
            yield return null;
            if (state != State.Moving) yield break;
        }
        eye = target;
        bob = 0f;
        waitForRelease = true;  // the last key is probably still down
        state = State.Idle;
    }

    // --- Noise ---

    void AddNoise(float amount, float kick)
    {
        if (amount <= 0f || state == State.Outside || state == State.Climbing || state == State.Caught) return;

        noise = Mathf.Min(1f, noise + amount);
        quietTime = 0f;
        view.NoiseSpike(kick);

        // A drone that's already out hears anything much and turns to look that way.
        if (drone != Drone.None)
        {
            if (amount > droneAlertNoise && drone == Drone.Looking) LookToward(cell);
            return;
        }
        if (noise >= 1f && alarm == null) alarm = StartCoroutine(Stare());
    }

    // The noise filled up: eyes look back out of the dark down the duct for a moment (right up against the wall, if
    // that's all there is ahead), and then the drone is let out. The player can keep moving the whole time.
    IEnumerator Stare()
    {
        view.SetPanic(true);
        Play(foundSound, 1f);
        for (float t = 0f; t < stareTime; t += Time.deltaTime)
        {
            view.SetEyes(Mathf.Clamp(OpenCellsAhead(), 0.49f, 6f));
            yield return null;
        }
        view.SetEyes(0f);
        view.SetPanic(false);
        alarm = null;
        ReleaseDrone();
    }

    void StopAlarm()
    {
        if (alarm != null) StopCoroutine(alarm);
        alarm = null;
        view.SetEyes(0f);
        view.SetPanic(false);
    }

    // --- The drone ---

    // Out of the nearest hatch that isn't right on top of the player, or failing that the furthest. With no hatches on
    // the map, out of the ceiling somewhere about that far away. It drops down looking the way the noise came from.
    void ReleaseDrone()
    {
        Flood(cell, playerDistances);
        Vector2Int best = cell;
        int bestScore = int.MaxValue;
        bool anyHatch = false;
        for (int x = 0; x < cells.GetLength(0); x++)
        {
            for (int y = 0; y < cells.GetLength(1); y++)
            {
                int away = playerDistances[x, y];
                if (away < 0 || cells[x, y] != VentCell.Hatch) continue;
                anyHatch = true;
                // Far enough scores by nearness; too near always scores worse, and the furthest of those scores best.
                int score = away >= droneMinReleaseDistance ? away : 10000 - away;
                if (score >= bestScore) continue;
                best = new Vector2Int(x, y);
                bestScore = score;
            }
        }

        if (!anyHatch)
        {
            int wanted = droneMinReleaseDistance + 1;
            for (int x = 0; x < cells.GetLength(0); x++)
            {
                for (int y = 0; y < cells.GetLength(1); y++)
                {
                    int away = playerDistances[x, y];
                    if (away < 0) continue;
                    int score = Mathf.Abs(away - wanted) + (away < droneMinReleaseDistance ? 1000 : 0);
                    if (score >= bestScore) continue;
                    best = new Vector2Int(x, y);
                    bestScore = score;
                }
            }
        }

        drone = Drone.Looking;
        droneCell = best;
        dronePosition = Center(best);
        droneRise = 0f;
        droneLock = 0f;
        droneLookTimer = droneSearchTime;
        droneFacing = OpenDirection(best);
        LookToward(cell);
        droneAngle = droneFacing * 90f;

        SoundManager.Setup(droneAudio, droneHumSound);
        droneAudio.loop = true;
        droneAudio.volume = 0f;
        if (droneAudio.clip != null) droneAudio.Play();
        UpdateDroneAudio();
        PlayDrone(droneReleaseSound, 1f);
    }

    void RemoveDrone()
    {
        drone = Drone.None;
        droneLock = 0f;
        if (droneAudio != null) droneAudio.Stop();
        if (view != null) view.SetDrone(false, dronePosition);
    }

    // Turns it to look down whichever duct leads toward this cell.
    void LookToward(Vector2Int goal)
    {
        Flood(goal, pathDistances);
        int here = pathDistances[droneCell.x, droneCell.y];
        for (int i = 0; i < Directions.Length && here > 0; i++)
        {
            Vector2Int next = droneCell + Directions[i];
            if (CellAt(next) == VentCell.Solid || pathDistances[next.x, next.y] != here - 1) continue;
            if (i != droneFacing) PlayDrone(droneServoSound, 0.5f);
            droneFacing = i;
            break;
        }
        droneTurnTimer = droneLookEvery * 1.4f;
    }

    void UpdateDrone()
    {
        float dt = Time.deltaTime;
        if (drone == Drone.Returning)
        {
            droneRise = Mathf.MoveTowards(droneRise, 0f, dt / 0.8f);
            if (droneRise <= 0f) RemoveDrone();
            return;
        }

        droneRise = Mathf.MoveTowards(droneRise, 1f, dt / 0.8f);
        droneAngle = Mathf.MoveTowardsAngle(droneAngle, droneFacing * 90f, dt * 220f);
        dronePosition = Center(droneCell)
            + new Vector2(Mathf.PerlinNoise(Time.time * 0.7f, 3f) - 0.5f, Mathf.PerlinNoise(Time.time * 0.7f, 8f) - 0.5f) * 0.12f;
        bool sees = droneRise > 0.9f && DroneSees();

        if (drone == Drone.Looking)
        {
            droneLookTimer -= dt;
            droneTurnTimer -= dt;
            if (sees)
            {
                drone = Drone.Locking;
                droneLock = 0f;
                PlayDrone(droneLockSound, 1f);
                view.Shake(0.5f);
                view.SetPanic(true);
            }
            else if (droneLookTimer <= 0f)
            {
                drone = Drone.Returning;
                PlayDrone(droneServoSound, 0.6f);
            }
            else if (droneTurnTimer <= 0f)
            {
                LookSomewhereElse();
            }
        }
        else if (drone == Drone.Locking)
        {
            if (sees)
            {
                droneLock += dt;
                view.Shake(0.15f + 0.5f * droneLock / Mathf.Max(0.05f, droneLockTime));
                if (droneLock >= droneLockTime)
                {
                    StopAllCoroutines();
                    alarm = null;
                    haunting = null;
                    StartCoroutine(Caught());
                }
            }
            else
            {
                // Lost them: back to looking, a little longer than it would have.
                drone = Drone.Looking;
                droneLock = 0f;
                view.SetPanic(false);
                droneTurnTimer = droneLookEvery;
                droneLookTimer = Mathf.Max(droneLookTimer, 3f);
            }
        }
    }

    // Down another duct from its hatch, picked at random, never the one it's looking down already if there's another.
    void LookSomewhereElse()
    {
        choices.Clear();
        for (int i = 0; i < Directions.Length; i++)
            if (i != droneFacing && CellAt(droneCell + Directions[i]) != VentCell.Solid) choices.Add(new Vector2Int(i, 0));
        if (choices.Count > 0)
        {
            droneFacing = choices[Random.Range(0, choices.Count)].x;
            PlayDrone(droneServoSound, 0.45f);
        }
        droneTurnTimer = droneLookEvery * Random.Range(0.8f, 1.3f);
    }

    // Right under it, or straight down the duct its light is on (once it's turned that way), with nothing in between.
    bool DroneSees()
    {
        Vector2Int offset = cell - droneCell;
        if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) <= droneHearing) return true;
        if (Mathf.Abs(Mathf.DeltaAngle(droneAngle, droneFacing * 90f)) > 25f) return false;
        Vector2Int at = droneCell;
        for (int i = 0; i < droneSight; i++)
        {
            at += Directions[droneFacing];
            if (CellAt(at) == VentCell.Solid) return false;
            if (at == cell) return true;
        }
        return false;
    }

    // One of the drone's sounds, from where it is (its hum's source, panned its way). Its pitch is the hum's to keep.
    void PlayDrone(string sound, float volume)
    {
        if (droneAudio == null) return;
        float pitch = droneAudio.pitch;
        SoundManager.PlayOneShot(droneAudio, sound, volume, 1f, VentSounds.Fallback(sound));
        droneAudio.pitch = pitch;
    }

    // How many cells along the ducts every cell is from this one. -1 where it can't be reached.
    void Flood(Vector2Int from, int[,] distances)
    {
        for (int x = 0; x < distances.GetLength(0); x++)
            for (int y = 0; y < distances.GetLength(1); y++)
                distances[x, y] = -1;
        if (CellAt(from) == VentCell.Solid) return;

        distances[from.x, from.y] = 0;
        floodQueue.Clear();
        floodQueue.Enqueue(from);
        while (floodQueue.Count > 0)
        {
            Vector2Int at = floodQueue.Dequeue();
            foreach (Vector2Int step in Directions)
            {
                Vector2Int next = at + step;
                if (CellAt(next) == VentCell.Solid || distances[next.x, next.y] >= 0) continue;
                distances[next.x, next.y] = distances[at.x, at.y] + 1;
                floodQueue.Enqueue(next);
            }
        }
    }

    // Its hum: louder the nearer it is along the ducts, not through walls, and from whichever side it's on. Pitched
    // down and wavering, and dragged higher as it locks on.
    void UpdateDroneAudio()
    {
        if (drone == Drone.None || droneAudio == null) return;

        Flood(cell, playerDistances);
        int away = playerDistances[droneCell.x, droneCell.y];
        float closeness = away < 0 ? 0f : Mathf.Clamp01(1f - away / (float)DroneAudibleCells);
        droneAudio.volume = SoundManager.Volume(droneHumSound) * Mathf.Max(0.08f, closeness * closeness) * Mathf.Lerp(0.3f, 1f, droneRise);
        float locking = drone == Drone.Locking ? droneLock / Mathf.Max(0.05f, droneLockTime) : 0f;
        droneAudio.pitch = 0.78f + 0.06f * Mathf.Sin(Time.time * 1.7f) + 0.5f * locking;

        Vector2 toDrone = dronePosition - eye;
        float radians = angle * Mathf.Deg2Rad;
        var right = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        droneAudio.panStereo = toDrone.sqrMagnitude > 0.01f ? Mathf.Clamp(Vector2.Dot(toDrone.normalized, right), -1f, 1f) * 0.8f : 0f;
    }

    // --- The dark ---

    void StartDark()
    {
        StartLoop(ambience, ambienceSound, 1f);
        StartLoop(deepAudio, deepSound, 1f);
        deepAudio.pitch *= 0.8f;       // a little slow, deeper
        StartLoop(heartAudio, heartbeatSound, 0f);
        if (haunting != null) StopCoroutine(haunting);
        haunting = StartCoroutine(Haunt());
    }

    void StopDark()
    {
        if (haunting != null) StopCoroutine(haunting);
        haunting = null;
        if (ambience != null) ambience.Stop();
        if (deepAudio != null) deepAudio.Stop();
        if (heartAudio != null) heartAudio.Stop();
        if (scareAudio != null) scareAudio.Stop();
        if (view != null) view.SetRat(false, Vector2.zero);
    }

    // Now and then, from one side or the other: something in the ducts that isn't them.
    IEnumerator Haunt()
    {
        yield return new WaitForSeconds(Random.Range(1.5f, 3f));
        while (true)
        {
            if (scareSounds.Length > 0 && (state == State.Idle || state == State.Moving || state == State.Squeezing))
            {
                string sound = scareSounds[Random.Range(0, scareSounds.Length)];
                if (sound == lastScare) sound = scareSounds[Random.Range(0, scareSounds.Length)];
                lastScare = sound;
                scareAudio.panStereo = Random.Range(-0.9f, 0.9f);
                SoundManager.PlayOneShot(scareAudio, sound, Random.Range(0.55f, 1f), Random.Range(0.85f, 1.05f), VentSounds.Fallback(sound));
                if (sound == "Vent Knock") view.Shake(0.12f);
            }
            yield return new WaitForSeconds(Random.Range(scareEvery.x, scareEvery.y));
        }
    }

    // Louder and quicker the closer they are to being found.
    void UpdateHeartbeat()
    {
        if (heartAudio == null || heartAudio.clip == null) return;
        float fear = Mathf.Max(noise * 0.8f, drone == Drone.Locking ? 1f : drone != Drone.None ? 0.7f : 0f);
        heartAudio.volume = Mathf.MoveTowards(heartAudio.volume, SoundManager.Volume(heartbeatSound) * fear, Time.deltaTime * 0.8f);
        heartAudio.pitch = 0.95f + 0.35f * fear;
    }

    // --- The rat ---

    // On one of its cells for the first time, looking down a duct with room for it to run.
    void TryRat()
    {
        if (ratDone || !ratSpots.Contains(cell) || OpenCellsAhead() < 2) return;
        ratDone = true;
        StartCoroutine(Rat());
    }

    // Out of the dark ahead, straight at them, squealing as it reaches the light, then under them and gone.
    IEnumerator Rat()
    {
        Vector2 way = Directions[direction];
        Vector2 side = new Vector2(way.y, -way.x);
        float distance = Mathf.Min(OpenCellsAhead(), 5) + 0.3f;
        scareAudio.panStereo = 0f;
        SoundManager.PlayOneShot(scareAudio, ratScurrySound, 1f, 1f, VentSounds.Fallback(ratScurrySound));
        bool squealed = false;
        while (distance > -0.4f && state != State.Outside && state != State.Caught)
        {
            distance -= ratSpeed * Time.deltaTime;
            view.SetRat(true, eye + way * distance + side * Mathf.Sin(distance * 5f) * 0.12f);
            if (!squealed && distance < 1.6f)
            {
                squealed = true;
                Play(ratSquealSound, 1f, Random.Range(0.95f, 1.1f));
                view.Shake(1f);
                view.SetPanic(true);
                AddNoise(ratNoise, 1f);
            }
            yield return null;
        }
        view.SetRat(false, Vector2.zero);
        yield return new WaitForSeconds(0.5f);
        if (alarm == null && drone == Drone.None) view.SetPanic(false);
        view.FlashHint("...Just a rat.", 1.8f);
    }

    // It got them. The view whips round to it, it comes at the player, and it's the usual death, back at the last
    // checkpoint. The screen stays black while they go down, until Sonny's monitor has come and gone.
    IEnumerator Caught()
    {
        state = State.Caught;
        bob = 0f;
        view.HideKey();
        view.SetEyes(0f);
        view.SetPanic(true);
        view.SetAlert(VentView.Alert.Chasing);
        Play(caughtSound, 1f);

        Vector2 toDrone = dronePosition - eye;
        float from = angle;
        float to = toDrone.sqrMagnitude > 0.0001f
            ? from + Mathf.DeltaAngle(from, Mathf.Atan2(toDrone.x, toDrone.y) * Mathf.Rad2Deg)
            : from;
        Vector2 start = dronePosition;
        Vector2 end = eye + (toDrone.sqrMagnitude > 0.0001f ? toDrone.normalized : new Vector2(Mathf.Sin(from * Mathf.Deg2Rad), Mathf.Cos(from * Mathf.Deg2Rad))) * 0.25f;
        for (float t = 0f; t < CaughtTime; t += Time.deltaTime)
        {
            float progress = t / CaughtTime;
            angle = Mathf.Lerp(from, to, Ease(progress * 2.5f));
            dronePosition = Vector2.Lerp(start, end, Ease(progress));
            view.Shake(progress);
            yield return null;
        }

        yield return view.FadeTo(1f, 0.06f);
        RemoveDrone();
        view.SetPanic(false);
        view.Close();
        StopDark();
        ShowPlayer(entrance != null ? entrance.ExitPoint : (Vector2)player.position);
        IsPlayerInside = false;
        entrance = null;
        state = State.Outside;

        if (playerHealth != null) playerHealth.Kill(gameObject);
        if (playerHealthHandler != null)
        {
            yield return null;
            yield return new WaitUntil(() => !playerHealthHandler.IsDying);
        }
        yield return view.FadeTo(0f, 0.4f);
    }

    // --- The map ---

    void ReadLayout()
    {
        var rows = new List<string>((layout ?? "").Replace("\r", "").Split('\n'));
        while (rows.Count > 0 && rows[rows.Count - 1].Trim().Length == 0)
            rows.RemoveAt(rows.Count - 1);

        int mapHeight = rows.Count;
        int mapWidth = 0;
        foreach (string row in rows)
            mapWidth = Mathf.Max(mapWidth, row.Length);

        // Row 0 of the text is the top, so it's the highest y on the map.
        cells = new VentCell[mapWidth, mapHeight];
        ratSpots.Clear();
        grateNumbers = new int[mapWidth, mapHeight];
        playerDistances = new int[mapWidth, mapHeight];
        pathDistances = new int[mapWidth, mapHeight];
        for (int r = 0; r < mapHeight; r++)
        {
            for (int x = 0; x < rows[r].Length; x++)
            {
                char mark = rows[r][x];
                int y = mapHeight - 1 - r;
                cells[x, y] = mark switch
                {
                    '.' or 'x' => VentCell.Duct,
                    'd' => VentCell.Dent,
                    's' => VentCell.Squeeze,
                    'r' => VentCell.Hatch,
                    >= '1' and <= '9' => VentCell.Grate,
                    _ => VentCell.Solid,
                };
                if (cells[x, y] == VentCell.Grate) grateNumbers[x, y] = mark - '0';
                if (mark == 'x') ratSpots.Add(new Vector2Int(x, y));
            }
        }
    }

    public VentCell CellAt(Vector2Int at)
    {
        if (cells == null || at.x < 0 || at.y < 0 || at.x >= cells.GetLength(0) || at.y >= cells.GetLength(1)) return VentCell.Solid;
        return cells[at.x, at.y];
    }

    bool TryFindGrate(int number, out Vector2Int at)
    {
        for (int x = 0; x < grateNumbers.GetLength(0); x++)
        {
            for (int y = 0; y < grateNumbers.GetLength(1); y++)
            {
                if (grateNumbers[x, y] != number) continue;
                at = new Vector2Int(x, y);
                return true;
            }
        }
        at = default;
        return false;
    }

    // The first way out of a cell, so climbing in faces down the duct instead of into a wall.
    int OpenDirection(Vector2Int at)
    {
        for (int i = 0; i < Directions.Length; i++)
            if (CellAt(at + Directions[i]) != VentCell.Solid) return i;
        return 0;
    }

    int OpenCellsAhead()
    {
        int count = 0;
        Vector2Int at = cell;
        while (count < 8)
        {
            at += Directions[direction];
            if (CellAt(at) == VentCell.Solid) break;
            count++;
        }
        return count;
    }

    static Vector2 Center(Vector2Int at) => new Vector2(at.x + 0.5f, at.y + 0.5f);

    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    AudioSource NewAudioSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    // One of the scene's sounds (SoundManager), at this share of its volume and this pitch. The ones made in code
    // (VentSounds) play even when the scene doesn't list them yet.
    void Play(string sound, float volume, float pitch = 1f)
    {
        SoundManager.PlayOneShot(sfx, sound, volume, pitch, VentSounds.Fallback(sound));
    }

    // A loop on its own source, at this share of its volume.
    void StartLoop(AudioSource source, string sound, float volume)
    {
        SoundManager.Setup(source, sound, VentSounds.Fallback(sound));
        source.loop = true;
        source.volume = SoundManager.Volume(sound) * volume;
        if (source.clip != null) source.Play();
    }

    // Footsteps pitched down sound like hands and knees on sheet metal.
    void PlayCrawl(float volume) => Play(crawlSound, volume, Random.Range(0.6f, 0.75f));
}
