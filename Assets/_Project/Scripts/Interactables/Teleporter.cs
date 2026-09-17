using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A way from one place to another: it takes the player to teleportTarget through a quick fade to black, with the camera
// cut straight to the new spot, so going through never shows as a jump. Two kinds:
//   Walk Through: the player walks into the trigger and is taken through, for hallways that carry on somewhere else.
//   Interact: a closed door. Walk up to it and press E; a locked door (or one with nowhere to go) just says it's locked.
// Levels can draw the doorway in code: a door in the wall with a lamp (green if it opens, red if not), or darkness
// deepening toward the wall, so a hallway seems to carry on out of sight. wallDirection says which side the wall is on.
// FloorOneBuilder places these in pairs, each one's target just inside the other.
// Setup: a trigger Collider2D over the doorway, and teleportTarget where the player comes out, clear of the trigger at
// the other end (or the player walks straight back through).
[RequireComponent(typeof(Collider2D))]
public class Teleporter : MonoBehaviour
{
    public enum Mode { WalkThrough, Interact }
    public enum Look { None, Door, Opening }

    const float PixelsPerUnit = 32f;
    const float DoorHeight = 2f;        // how far up the wall a door in the wall above reaches
    const int SlabThickness = 10;       // pixels across a door seen edge-on, in a side or bottom wall
    const float DarkMoment = 0.08f;     // seconds fully black before the picture comes back
    static readonly Color32 Frame = new Color32(30, 33, 38, 255);
    static readonly Color32 Metal = new Color32(74, 80, 88, 255);
    static readonly Color32 MetalLit = new Color32(104, 111, 120, 255);
    static readonly Color32 Glass = new Color32(22, 42, 54, 255);
    static readonly Color32 Stripe = new Color32(222, 170, 40, 255);
    static readonly Color32 StripeDark = new Color32(30, 30, 32, 255);
    static readonly Color ReadyLamp = new Color(0.3f, 1f, 0.45f);
    static readonly Color LockedLamp = new Color(1f, 0.2f, 0.15f);

    [Tooltip("Where the player comes out.")]
    public Transform teleportTarget;
    [Tooltip("Optional: gets a \"Teleport\" trigger as the player goes through.")]
    public Animator animator;
    public Mode mode = Mode.WalkThrough;

    [Header("Closed Door")]
    [Tooltip("How close the player has to get to the doorway's edge to use it, in world units.")]
    public float interactRange = 0.6f;
    public string promptText = "E  OPEN";
    [Tooltip("A locked door won't open; walking up to it only shows the locked text.")]
    public bool locked;
    public string lockedText = "LOCKED";

    [Header("Going Through")]
    [Tooltip("Seconds for each half of the fade through black.")]
    public float fadeTime = 0.25f;
    [Tooltip("Played as the screen goes dark.")]
    public AudioClip sound;
    [Range(0f, 1f)] public float volume = 0.7f;

    [Header("Look")]
    public Look look = Look.None;
    [Tooltip("Which side of the doorway the wall is on: up for a door in the wall above it.")]
    public Vector2 wallDirection = Vector2.up;

    // True while anyone is going through a teleporter. Others ignore the player meanwhile.
    public static bool IsTravelling { get; private set; }

    static readonly List<Teleporter> all = new List<Teleporter>();

    private Collider2D doorway;
    private Transform player;
    private Collider2D playerCollider;
    private Health playerHealth;
    private PlayerController playerController;
    private SpriteRenderer lamp;
    private Light2D lampLight;
    private bool travelling;
    private readonly List<Behaviour> frozen = new List<Behaviour>();

    bool CanOpen => !locked && teleportTarget != null;
    Vector2 WallDirection => PlayerController.SnapToFourWay(wallDirection.sqrMagnitude > 0f ? wallDirection : Vector2.up);

    // Above the door, or just above the doorway.
    Vector3 PromptPoint
    {
        get
        {
            Bounds bounds = doorway.bounds;
            float top = look == Look.Door && WallDirection == Vector2.up ? bounds.max.y + DoorHeight + 0.35f : bounds.max.y + 0.6f;
            return new Vector3(bounds.center.x, top, transform.position.z);
        }
    }

    void Awake()
    {
        doorway = GetComponent<Collider2D>();
        if (look == Look.Door) BuildDoor();
        else if (look == Look.Opening) BuildOpening();
    }

    void OnEnable()
    {
        all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
        InteractPrompt.Hide(this);

        // Switched off partway through: give the player back and take the fade away rather than leave them stuck.
        if (travelling)
        {
            Unfreeze();
            travelling = false;
            IsTravelling = false;
            ScreenFade.Clear();
        }
    }

    void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return;

        player = found.transform;
        playerCollider = found.GetComponent<Collider2D>();
        playerHealth = found.GetComponent<Health>();
        playerController = found.GetComponent<PlayerController>();
    }

    void Update()
    {
        if (lamp != null)
        {
            Color color = CanOpen ? ReadyLamp : LockedLamp;
            lamp.color = color;
            lampLight.color = color;
        }
        if (mode != Mode.Interact) return;

        // timeScale 0 means a menu paused the game.
        bool usable = player != null && !IsTravelling && Time.timeScale > 0f && !Locker.IsPlayerHidden && PlayerCanAct() && IsClosestInRange();
        if (!usable)
        {
            InteractPrompt.Hide(this);
            return;
        }

        InteractPrompt.Show(this, PromptPoint, CanOpen ? promptText : lockedText);
        if (CanOpen && Input.GetKeyDown(Locker.InteractKey))
            StartCoroutine(Travel(player));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (mode != Mode.WalkThrough || IsTravelling || teleportTarget == null || !other.CompareTag("Player")) return;
        StartCoroutine(Travel(other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform));
    }

    IEnumerator Travel(Transform traveller)
    {
        IsTravelling = true;
        travelling = true;
        InteractPrompt.Hide(this);
        Freeze(traveller);
        if (animator != null) animator.SetTrigger("Teleport");

        ScreenFade fade = ScreenFade.Get();
        fade.PlaySound(sound, volume);
        yield return fade.FadeTo(1f, fadeTime);

        Vector3 arrival = teleportTarget.position;
        traveller.position = new Vector3(arrival.x, arrival.y, traveller.position.z);
        if (traveller.TryGetComponent(out Rigidbody2D body))
        {
            body.position = arrival;
            body.linearVelocity = Vector2.zero;
        }
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraFallow follow))
            follow.SnapToPlayer();

        // A moment in the dark, then the player can move again while the picture comes back.
        for (float t = 0f; t < DarkMoment; t += Time.unscaledDeltaTime)
            yield return null;
        Unfreeze();
        yield return fade.FadeTo(0f, fadeTime);

        travelling = false;
        IsTravelling = false;
    }

    // Stops the player's scripts for the trip, so no moving or attacking in the dark. Health, its handler, and lights
    // stay on. Exactly what was turned off comes back, the same way Locker does it.
    void Freeze(Transform traveller)
    {
        foreach (MonoBehaviour behaviour in traveller.GetComponentsInChildren<MonoBehaviour>())
        {
            if (!behaviour.enabled || behaviour is Health || behaviour is PlayerHealthHandler || behaviour is Light2D) continue;
            behaviour.enabled = false;
            frozen.Add(behaviour);
        }
        if (traveller.TryGetComponent(out Rigidbody2D body))
            body.linearVelocity = Vector2.zero;
    }

    void Unfreeze()
    {
        foreach (Behaviour behaviour in frozen)
            if (behaviour != null) behaviour.enabled = true;
        frozen.Clear();
    }

    // Alive, not frozen by something else, and the nearest closed door in reach if several are.
    bool PlayerCanAct()
    {
        if (playerController != null && !playerController.enabled) return false;
        return playerHealth == null || !playerHealth.IsDead;
    }

    bool IsClosestInRange()
    {
        float distance = DistanceToPlayer();
        if (distance > interactRange) return false;

        foreach (Teleporter other in all)
        {
            if (other != this && other.mode == Mode.Interact && other.player != null && other.DistanceToPlayer() < distance)
                return false;
        }
        return true;
    }

    // Edge to edge, so it doesn't matter which side the player walks up from.
    float DistanceToPlayer()
    {
        if (playerCollider != null && playerCollider.enabled)
        {
            ColliderDistance2D gap = doorway.Distance(playerCollider);
            if (gap.isValid) return Mathf.Max(0f, gap.distance);
        }
        return Vector2.Distance(doorway.ClosestPoint(player.position), player.position);
    }

    // --- Looks ---

    void BuildDoor()
    {
        Bounds bounds = doorway.bounds;
        Vector2 wall = WallDirection;
        float z = transform.position.z;

        var art = new GameObject("Door").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.sortingLayerName = "On the wall";

        Vector2 lampAt;
        if (wall == Vector2.up)
        {
            int width = Mathf.Max(8, Mathf.RoundToInt(bounds.size.x * PixelsPerUnit));
            art.sprite = PixelArt.ToSprite(FaceDoorTexture(width, Mathf.RoundToInt(DoorHeight * PixelsPerUnit)), PixelsPerUnit, new Vector2(0.5f, 0f));
            art.transform.position = new Vector3(bounds.center.x, bounds.max.y, z);
            lampAt = new Vector2(bounds.center.x, bounds.max.y + DoorHeight + 0.12f);
        }
        else
        {
            // Seen edge-on, in the wall line just past the doorway.
            bool upright = wall.x != 0f;
            int length = Mathf.Max(8, Mathf.RoundToInt((upright ? bounds.size.y : bounds.size.x) * PixelsPerUnit));
            art.sprite = PixelArt.ToSprite(SlabTexture(length, upright), PixelsPerUnit, new Vector2(0.5f, 0.5f));
            Vector2 edge = (Vector2)bounds.center + Vector2.Scale(wall, bounds.extents) + wall * (SlabThickness / PixelsPerUnit * 0.5f);
            art.transform.position = new Vector3(edge.x, edge.y, z);
            lampAt = upright ? new Vector2(edge.x, bounds.max.y + 0.3f) : new Vector2(bounds.min.x - 0.3f, edge.y);
        }

        lamp = new GameObject("Lamp").AddComponent<SpriteRenderer>();
        lamp.transform.SetParent(transform, false);
        lamp.transform.position = new Vector3(lampAt.x, lampAt.y, z);
        lamp.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
        lamp.sprite = CombatSprites.Square;
        if (CombatSprites.EffectMaterial != null) lamp.sharedMaterial = CombatSprites.EffectMaterial;
        lamp.sortingLayerName = "Top";

        var glow = new GameObject("Lamp Light");
        glow.transform.SetParent(transform, false);
        glow.transform.position = new Vector3(lampAt.x, lampAt.y, z);
        lampLight = glow.AddComponent<Light2D>();
        lampLight.lightType = Light2D.LightType.Point;
        lampLight.pointLightInnerRadius = 0.2f;
        lampLight.pointLightOuterRadius = 1.8f;
        lampLight.intensity = 0.7f;
        lampLight.falloffIntensity = 0.6f;
        lampLight.color = CanOpen ? ReadyLamp : LockedLamp;
    }

    // Darkness deepening toward the wall and a little past it, so a hallway seems to carry on out of sight.
    void BuildOpening()
    {
        Bounds bounds = doorway.bounds;
        Vector2 wall = WallDirection;
        bool alongY = wall.y != 0f;
        bool towardLow = wall.x < 0f || wall.y < 0f;

        Texture2D texture = PixelArt.MakeTexture(alongY ? 1 : 32, alongY ? 32 : 1, (x, y) =>
        {
            float t = (alongY ? y : x) / 31f;
            if (towardLow) t = 1f - t;
            return new Color32(0, 0, 0, (byte)(Mathf.Pow(t, 1.3f) * 255f));
        });
        texture.filterMode = FilterMode.Bilinear;

        var shade = new GameObject("Darkness").AddComponent<SpriteRenderer>();
        shade.transform.SetParent(transform, false);
        shade.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        shade.sortingLayerName = "On the wall";
        shade.sortingOrder = 1;

        var depth = new Vector2(Mathf.Abs(wall.x), Mathf.Abs(wall.y));
        Vector2 size = (Vector2)bounds.size + depth + new Vector2(depth.y, depth.x) * 0.2f;
        Vector2 center = (Vector2)bounds.center + wall * 0.5f;
        Vector2 spriteSize = shade.sprite.bounds.size;
        shade.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        shade.transform.position = new Vector3(center.x, center.y, transform.position.z);
    }

    // A door in the wall above: two panels meeting in the middle, a window in each, and hazard stripes along the sill.
    static Texture2D FaceDoorTexture(int width, int height)
    {
        int middle = width / 2;
        return PixelArt.MakeTexture(width, height, (x, y) =>
        {
            if (x < 2 || x >= width - 2 || y >= height - 2) return Frame;
            if (y < 4) return (x + y) / 3 % 2 == 0 ? Stripe : StripeDark;
            if (x == middle || x == middle - 1) return Frame;
            int panelLeft = x < middle ? 2 : middle + 1;
            int panelRight = x < middle ? middle - 2 : width - 3;
            if (y > height * 0.55f && y < height * 0.8f && x > panelLeft + 3 && x < panelRight - 3) return Glass;
            return x - panelLeft < 3 ? MetalLit : Metal;
        });
    }

    // A door seen edge-on: dark metal edged in hazard stripes, split where it opens.
    static Texture2D SlabTexture(int length, bool upright)
    {
        return PixelArt.MakeTexture(upright ? SlabThickness : length, upright ? length : SlabThickness, (x, y) =>
        {
            int across = upright ? x : y;
            int along = upright ? y : x;
            if (across == 0 || across == SlabThickness - 1 || along == 0 || along == length - 1) return Frame;
            if (along == length / 2 || along == length / 2 - 1) return Frame;
            if (across >= 3 && across <= 6) return (along + across) / 3 % 2 == 0 ? Stripe : StripeDark;
            return across < 3 ? MetalLit : Metal;
        });
    }

    // Select a teleporter to see where it sends the player.
    void OnDrawGizmosSelected()
    {
        if (teleportTarget == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, teleportTarget.position);
        Gizmos.DrawWireSphere(teleportTarget.position, 0.3f);
    }
}
