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
//   Noise is what gives the player away. A dented panel bangs when crawled over (it only creaks if careful), hurrying
//   is loud on any panel, and so is crawling head first into a wall. It fades again if they keep quiet.
//   A tight squeeze takes one key at a time: a letter from W A S D comes up and has to be pressed before its time runs
//   out. The right key inches the player through; the wrong one, or being too slow, scrapes loudly and slips them back.
//   There's no room to turn inside a squeeze.
//
// The drone:
//   When the noise fills up, eyes stare out of the dark for a moment, and then a drone is let out of the nearest hatch
//   that isn't right on top of the player. It flies the ducts toward them. It keeps track of them while they're within
//   earshot, in a straight line of sight down a duct, or making any real noise; lose it, and it searches around where
//   it last knew they were, then goes back into its hatch. If it reaches them they're caught: the usual death, back at
//   the last checkpoint. Climbing out at a grate gets away from it. Its hum is louder the nearer it is along the
//   ducts, and comes from whichever side it's on.
//
// The map is kept as text, one character per cell, top row first:
//   # or space  solid        .  duct        d  dented panel        s  tight squeeze (keep them to straight runs)
//   r           a drone hatch in the ceiling. With none, the drone comes from somewhere a few cells away.
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
    static readonly KeyCode[] SqueezeKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
    const string SqueezeLetters = "WASD";

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
    public float turnTime = 0.35f;

    [Header("Noise (1 fills the meter)")]
    [Tooltip("Crawling over a dented panel.")]
    public float dentNoise = 0.45f;
    [Tooltip("Crawling carefully over a dented panel.")]
    public float carefulDentNoise = 0.1f;
    [Tooltip("Each cell crawled while hurrying.")]
    public float hurriedNoise = 0.12f;
    [Tooltip("Crawling head first into a wall.")]
    public float bumpNoise = 0.08f;
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
    public float squeezeKeyTime = 1.1f;

    [Header("Drone")]
    [Tooltip("Seconds the eyes stare out of the dark before the drone is let out.")]
    public float stareTime = 1.4f;
    [Tooltip("Seconds for the drone to fly one cell while chasing. Normal crawling is 0.6, so only hurrying outruns it.")]
    public float droneChaseCellTime = 0.5f;
    [Tooltip("Seconds per cell while it searches, and on its way back to its hatch.")]
    public float droneSearchCellTime = 0.85f;
    [Tooltip("It hears the player this many cells away along the ducts, however quiet they are.")]
    public int droneHearing = 2;
    [Tooltip("It sees the player this many cells straight down a duct, with nothing in the way.")]
    public int droneSight = 5;
    [Tooltip("Noises louder than this bring it straight to the player wherever it is. Bumps and careful creaks are quieter.")]
    public float droneAlertNoise = 0.1f;
    [Tooltip("Seconds it searches around where it lost the player before going back into its hatch.")]
    public float droneSearchTime = 6f;
    [Tooltip("It comes out of the nearest hatch at least this many cells from the player along the ducts.")]
    public int droneMinReleaseDistance = 5;

    [Header("Looks and Sound")]
    [Tooltip("Seconds for each half of the fade through black on the way in and out.")]
    public float fadeTime = 0.25f;
    [Tooltip("Font for the hints and the squeeze keys. Leave empty for TextMesh Pro's default.")]
    public TMP_FontAsset font;
    [Tooltip("Loops while the player is in the ducts.")]
    public AudioClip ambienceLoop;
    [Range(0f, 1f)] public float ambienceVolume = 0.5f;
    [Tooltip("A hand or knee coming down on the metal. Played pitched down.")]
    public AudioClip[] crawlClips = new AudioClip[0];
    [Tooltip("A dented panel banging. Also used, quieter, for creaks and bumps.")]
    public AudioClip dentClip;
    [Tooltip("Scraping the duct on a wrong key in a squeeze.")]
    public AudioClip scrapeClip;
    [Tooltip("The noise filling up, as the eyes appear.")]
    public AudioClip foundClip;
    [Tooltip("The drone's hatch opening and it powering up.")]
    public AudioClip droneReleaseClip;
    [Tooltip("The drone's hum, looping while it's out.")]
    public AudioClip droneLoop;
    [Range(0f, 1f)] public float droneVolume = 0.8f;
    [Tooltip("The drone reaching the player.")]
    public AudioClip caughtClip;

    // True from climbing in at a grate until standing outside again. Locker.IsPlayerHidden counts this too.
    public static bool IsPlayerInside { get; private set; }

    public bool InUse => state != State.Outside;
    public VentCell[,] Cells => cells;

    enum State { Outside, Climbing, Idle, Moving, Squeezing, Caught }
    enum Drone { None, Chasing, Searching, Returning }

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
    private Vector2Int droneCell;           // the cell it's flying from
    private Vector2Int droneNext;           // and the one it's flying to; the same when it's hovering
    private Vector2Int droneLast;           // where it came from, so a search doesn't just double back
    private Vector2Int droneHome;
    private Vector2Int droneTarget;         // where it last knew the player was
    private float droneStep;                // 0 to 1 from droneCell to droneNext
    private float droneSearchTimer;
    private Vector2 dronePosition;
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
        if (ambience != null) ambience.Stop();
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

        if (state == State.Idle) ReadInput();
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
        view.SetAlert(drone == Drone.Chasing ? VentView.Alert.Chasing
            : drone != Drone.None ? VentView.Alert.Searching
            : VentView.Alert.None);
        UpdateDroneAudio();
    }

    // --- Getting in and out ---

    IEnumerator ClimbIn(VentGrate grate, Vector2Int start)
    {
        state = State.Climbing;
        IsPlayerInside = true;
        entrance = grate;
        HidePlayer(grate.transform.position);
        grate.Rattle();
        Play(dentClip, 0.35f, 0.8f);

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
        if (ambienceLoop != null)
        {
            ambience.clip = ambienceLoop;
            ambience.volume = ambienceVolume;
            ambience.Play();
        }

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
            Play(dentClip, 0.25f, 1.3f);
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
        ambience.Stop();
        view.Close();
        ShowPlayer(grate.ExitPoint);
        IsPlayerInside = false;
        entrance = null;
        grate.Rattle();
        Play(dentClip, 0.35f, 0.8f);

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
                Play(dentClip, 0.2f, 0.6f);
                view.Shake(0.15f);
                AddNoise(carefulDentNoise, 0.3f);
            }
            else
            {
                Play(dentClip, 1f, Random.Range(0.85f, 1f));
                view.Shake(hurried ? 1f : 0.7f);
                AddNoise(dentNoise + (hurried ? hurriedNoise : 0f), 1f);
            }
        }
        else if (hurried)
        {
            AddNoise(hurriedNoise, 0.35f);
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
                Play(dentClip, 0.3f, 1.5f);
                view.Shake(0.35f);
                AddNoise(bumpNoise, 0.3f);
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
            Play(scrapeClip, 1f, Random.Range(0.9f, 1.1f));
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

        // A drone that's already out hears anything much and comes straight for it.
        if (drone != Drone.None)
        {
            if (amount > droneAlertNoise) AlertDrone();
            return;
        }
        if (noise >= 1f && alarm == null) alarm = StartCoroutine(Stare());
    }

    // The noise filled up: eyes look back out of the dark down the duct for a moment (right up against the wall, if
    // that's all there is ahead), and then the drone is let out. The player can keep moving the whole time.
    IEnumerator Stare()
    {
        view.SetPanic(true);
        Play(foundClip, 1f);
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
    // the map, out of the dark somewhere about that far away.
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
            int wanted = droneMinReleaseDistance + 3;
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

        drone = Drone.Chasing;
        droneHome = droneCell = droneNext = droneLast = best;
        droneTarget = cell;
        droneStep = 0f;
        dronePosition = Center(best);

        if (droneLoop != null)
        {
            droneAudio.clip = droneLoop;
            droneAudio.volume = 0f;
            droneAudio.Play();
        }
        UpdateDroneAudio();
        if (droneReleaseClip != null) droneAudio.PlayOneShot(droneReleaseClip, 1f);
    }

    void RemoveDrone()
    {
        drone = Drone.None;
        if (droneAudio != null) droneAudio.Stop();
        if (view != null) view.SetDrone(false, dronePosition);
    }

    void AlertDrone()
    {
        drone = Drone.Chasing;
        droneTarget = cell;
    }

    void UpdateDrone()
    {
        if (drone == Drone.Searching) droneSearchTimer -= Time.deltaTime;

        if (droneNext == droneCell)
        {
            ChooseDroneStep();
        }
        else
        {
            float cellTime = drone == Drone.Chasing ? droneChaseCellTime : droneSearchCellTime;
            droneStep += Time.deltaTime / Mathf.Max(0.05f, cellTime);
            if (droneStep >= 1f)
            {
                droneLast = droneCell;
                droneCell = droneNext;
                droneStep = 0f;
                ChooseDroneStep();
            }
        }
        if (drone == Drone.None) return;

        dronePosition = Vector2.Lerp(Center(droneCell), Center(droneNext), droneStep);
        if (Vector2.Distance(dronePosition, eye) < CatchDistance)
        {
            StopAllCoroutines();
            alarm = null;
            StartCoroutine(Caught());
        }
    }

    // At each cell: listen and look for the player, then pick the next cell.
    void ChooseDroneStep()
    {
        Flood(cell, playerDistances);
        int toPlayer = playerDistances[droneCell.x, droneCell.y];
        if (toPlayer >= 0 && (toPlayer <= droneHearing || DroneCanSee())) AlertDrone();

        if (drone == Drone.Chasing && droneCell == droneTarget)
        {
            // Got to where it last knew they were, and they're not here.
            drone = Drone.Searching;
            droneSearchTimer = droneSearchTime;
        }
        if (drone == Drone.Searching && droneSearchTimer <= 0f) drone = Drone.Returning;
        if (drone == Drone.Returning && droneCell == droneHome)
        {
            RemoveDrone();
            return;
        }

        droneNext = drone == Drone.Chasing ? StepToward(droneTarget)
            : drone == Drone.Returning ? StepToward(droneHome)
            : Wander();
    }

    // Straight down a duct in any direction, as far as its sight reaches and nothing solid is in the way.
    bool DroneCanSee()
    {
        foreach (Vector2Int step in Directions)
        {
            Vector2Int at = droneCell;
            for (int i = 0; i < droneSight; i++)
            {
                at += step;
                if (CellAt(at) == VentCell.Solid) break;
                if (at == cell) return true;
            }
        }
        return false;
    }

    // The neighbouring cell that's one step nearer to where it's going along the ducts.
    Vector2Int StepToward(Vector2Int goal)
    {
        Flood(goal, pathDistances);
        int here = pathDistances[droneCell.x, droneCell.y];
        if (here <= 0) return droneCell;
        foreach (Vector2Int step in Directions)
        {
            Vector2Int next = droneCell + step;
            if (CellAt(next) != VentCell.Solid && pathDistances[next.x, next.y] == here - 1) return next;
        }
        return droneCell;
    }

    // Any way on but back, unless it's a dead end.
    Vector2Int Wander()
    {
        choices.Clear();
        foreach (Vector2Int step in Directions)
        {
            Vector2Int next = droneCell + step;
            if (CellAt(next) != VentCell.Solid && next != droneLast) choices.Add(next);
        }
        if (choices.Count == 0) return CellAt(droneLast) != VentCell.Solid ? droneLast : droneCell;
        return choices[Random.Range(0, choices.Count)];
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

    // Its hum: louder the nearer it is along the ducts, not through walls, and from whichever side it's on.
    void UpdateDroneAudio()
    {
        if (drone == Drone.None || droneAudio == null) return;

        int away = playerDistances[droneCell.x, droneCell.y];
        float closeness = away < 0 ? 0f : Mathf.Clamp01(1f - (away - droneStep) / DroneAudibleCells);
        droneAudio.volume = droneVolume * Mathf.Max(0.08f, closeness * closeness);

        Vector2 toDrone = dronePosition - eye;
        float radians = angle * Mathf.Deg2Rad;
        var right = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        droneAudio.panStereo = toDrone.sqrMagnitude > 0.01f ? Mathf.Clamp(Vector2.Dot(toDrone.normalized, right), -1f, 1f) * 0.8f : 0f;
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
        Play(caughtClip, 1f);

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
        ambience.Stop();
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
                    '.' => VentCell.Duct,
                    'd' => VentCell.Dent,
                    's' => VentCell.Squeeze,
                    'r' => VentCell.Hatch,
                    >= '1' and <= '9' => VentCell.Grate,
                    _ => VentCell.Solid,
                };
                if (cells[x, y] == VentCell.Grate) grateNumbers[x, y] = mark - '0';
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

    void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume);
    }

    // Footsteps pitched down sound like hands and knees on sheet metal.
    void PlayCrawl(float volume)
    {
        if (crawlClips == null || crawlClips.Length == 0) return;
        Play(crawlClips[Random.Range(0, crawlClips.Length)], volume, Random.Range(0.6f, 0.75f));
    }
}
