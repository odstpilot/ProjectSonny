using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// door or opening that moves the player
[RequireComponent(typeof(Collider2D))]
public class Teleporter : MonoBehaviour
{
    public enum Mode { WalkThrough, Interact }
    public enum Look { None, Door, Opening }

    const float PixelsPerUnit = 32f;
    const float DoorHeight = 2f;
    const int SlabThickness = 10;
    const float DarkMoment = 0.08f;
    public const float SwingSeconds = 0.35f;
    const float OpenScale = 0.1f;
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
    [Tooltip("Makes it a badge door: it only opens if the player holds this credential (AccessCredentials), or a crew member with a badge is close by. Empty for any door.")]
    public string credential = "";
    [Tooltip("How close a crew member with a badge has to be to the player to let them through.")]
    public float escortRange = 4.5f;
    public string badgeText = "BADGE REQUIRED";
    [Tooltip("Said instead when the player carries a badge that won't open it: the temp badge, once Sonny's killed it.")]
    public string inactiveBadgeText = "BADGE INACTIVE";
    [Tooltip("Shown instead while every door's held shut (HoldAll).")]
    public string heldText = "CHECK IN FIRST";

    [Header("Going Through")]
    [Tooltip("Seconds for each half of the fade through black.")]
    public float fadeTime = 0.25f;
    [Tooltip("The scene's sound played as the screen goes dark. Empty for none.")]
    [SoundName] public string sound = "";
    [Tooltip("Loads this scene instead, once the screen's black, with the player coming out at teleportTarget in it. Empty stays in this one.")]
    public string nextScene = "";

    [Header("Look")]
    public Look look = Look.None;
    [Tooltip("Which side of the doorway the wall is on: up for a door in the wall above it.")]
    public Vector2 wallDirection = Vector2.up;

    public static bool IsTravelling { get; private set; }

    static readonly List<Teleporter> all = new List<Teleporter>();

    public static string HeldLine { get; private set; }
    public static bool Held => HeldLine != null;
    public static event System.Action HeldDoorTried;
    static Vector2? arrival;
    public static Vector2? TakeArrival()
    {
        Vector2? at = arrival;
        arrival = null;
        return at;
    }

    public static void HoldAll(string line) => HeldLine = line ?? "";
    public static void ReleaseAll() => HeldLine = null;
    const float HeldTryCooldown = 1.5f;
    static float lastHeldTry = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHold()
    {
        HeldLine = null;
        HeldDoorTried = null;
        arrival = null;
        lastHeldTry = float.NegativeInfinity;
    }

    private Collider2D doorway;
    private Transform player;
    private Collider2D playerCollider;
    private Health playerHealth;
    private PlayerController playerController;
    private SpriteRenderer lamp;
    private Light2D lampLight;
    private bool travelling;
    private Transform[] panels;
    private bool panelsAcrossX;
    private float openness;
    private Coroutine swinging;
    private readonly List<Behaviour> frozen = new List<Behaviour>();

    bool CanOpen => !Held && !locked && teleportTarget != null && HasAccess;
    public bool HasAccess => string.IsNullOrEmpty(credential) || AccessCredentials.Has(credential) || Escorted();
    public bool NeedsBadge => !locked && teleportTarget != null && !HasAccess;
    Vector2 WallDirection => PlayerController.SnapToFourWay(wallDirection.sqrMagnitude > 0f ? wallDirection : Vector2.up);
    public Vector2 Wall => WallDirection;
    public Vector2 Mouth => doorway != null ? (Vector2)doorway.bounds.center : (Vector2)transform.position;

    public void Swing(bool open)
    {
        if (panels == null || !isActiveAndEnabled) return;
        if (swinging != null) StopCoroutine(swinging);
        swinging = StartCoroutine(SwingTo(open ? 1f : 0f));
    }

    IEnumerator SwingTo(float to)
    {
        float from = openness;
        for (float t = 0f; t < SwingSeconds; t += Time.deltaTime)
        {
            SetOpenness(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / SwingSeconds)));
            yield return null;
        }
        SetOpenness(to);
        swinging = null;
    }

    void SetOpenness(float amount)
    {
        openness = amount;
        float scale = Mathf.Lerp(1f, OpenScale, amount);
        foreach (Transform panel in panels)
            panel.localScale = panelsAcrossX ? new Vector3(scale, 1f, 1f) : new Vector3(1f, scale, 1f);
    }

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

        bool usable = player != null && !IsTravelling && !DialogueBox.Busy && Time.timeScale > 0f && !Locker.IsPlayerHidden && PlayerCanAct() && IsClosestInRange();
        if (!usable)
        {
            InteractPrompt.Hide(this);
            return;
        }

        bool held = Held && !locked && teleportTarget != null;
        InteractPrompt.Show(this, PromptPoint, CanOpen ? promptText : held ? heldText
            : NeedsBadge ? (AccessCredentials.HasBadge ? inactiveBadgeText : badgeText) : lockedText);
        if (!InteractPrompt.Pressed(this, Locker.InteractKey)) return;
        if (CanOpen) StartCoroutine(Travel(player));
        else if (held) TriedWhileHeld();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (mode != Mode.WalkThrough || IsTravelling || DialogueBox.IsOpen || teleportTarget == null || !other.CompareTag("Player")) return;
        if (Held)
        {
            TriedWhileHeld();
            return;
        }
        StartCoroutine(Travel(other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform));
    }

    static void TriedWhileHeld()
    {
        if (Time.time - lastHeldTry < HeldTryCooldown) return;
        lastHeldTry = Time.time;
        TechnicianVoice.Say(HeldLine);
        HeldDoorTried?.Invoke();
    }

    IEnumerator Travel(Transform traveller)
    {
        IsTravelling = true;
        travelling = true;
        InteractPrompt.Hide(this);
        Freeze(traveller);
        if (animator != null) animator.SetTrigger("Teleport");

        ScreenFade fade = ScreenFade.Get();
        fade.PlaySound(sound);
        yield return fade.FadeTo(1f, fadeTime);

        Vector3 to = teleportTarget.position;
        if (!string.IsNullOrEmpty(nextScene))
        {
            arrival = to;
            travelling = false;
            IsTravelling = false;
            SceneManager.LoadScene(nextScene);
            yield break;
        }
        traveller.position = new Vector3(to.x, to.y, traveller.position.z);
        if (traveller.TryGetComponent(out Rigidbody2D body))
        {
            body.position = to;
            body.linearVelocity = Vector2.zero;
        }
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraFallow follow))
            follow.SnapToPlayer();

        for (float t = 0f; t < DarkMoment; t += Time.unscaledDeltaTime)
            yield return null;
        Unfreeze();
        yield return fade.FadeTo(0f, fadeTime);

        travelling = false;
        IsTravelling = false;
    }

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

    bool Escorted()
    {
        if (player == null) return false;
        foreach (CrewMember crew in CrewMember.Everyone)
            if (crew.isActiveAndEnabled && Vector2.Distance(crew.Position, player.position) <= escortRange) return true;
        return false;
    }

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

    float DistanceToPlayer()
    {
        if (playerCollider != null && playerCollider.enabled)
        {
            ColliderDistance2D gap = doorway.Distance(playerCollider);
            if (gap.isValid) return Mathf.Max(0f, gap.distance);
        }
        return Vector2.Distance(doorway.ClosestPoint(player.position), player.position);
    }

    void BuildDoor()
    {
        Bounds bounds = doorway.bounds;
        Vector2 wall = WallDirection;
        float z = transform.position.z;

        Vector2 lampAt;
        Texture2D texture;
        Vector2 center;
        Vector2 size;
        if (wall == Vector2.up)
        {
            int width = Mathf.Max(8, Mathf.RoundToInt(bounds.size.x * PixelsPerUnit));
            texture = FaceDoorTexture(width, Mathf.RoundToInt(DoorHeight * PixelsPerUnit));
            center = new Vector2(bounds.center.x, bounds.max.y + DoorHeight * 0.5f);
            lampAt = new Vector2(bounds.center.x, bounds.max.y + DoorHeight + 0.12f);
            panelsAcrossX = true;
        }
        else
        {
            bool upright = wall.x != 0f;
            int length = Mathf.Max(8, Mathf.RoundToInt((upright ? bounds.size.y : bounds.size.x) * PixelsPerUnit));
            texture = SlabTexture(length, upright);
            center = (Vector2)bounds.center + Vector2.Scale(wall, bounds.extents) + wall * (SlabThickness / PixelsPerUnit * 0.5f);
            lampAt = upright ? new Vector2(center.x, bounds.max.y + 0.3f) : new Vector2(bounds.min.x - 0.3f, center.y);
            panelsAcrossX = !upright;
        }
        size = new Vector2(texture.width, texture.height) / PixelsPerUnit;

        var gap = new GameObject("Doorway Dark").AddComponent<SpriteRenderer>();
        gap.transform.SetParent(transform, false);
        gap.sprite = PixelArt.ToSprite(PixelArt.MakeTexture(1, 1, (x, y) => new Color32(6, 7, 9, 255)), 1f, new Vector2(0.5f, 0.5f));
        gap.transform.position = new Vector3(center.x, center.y, z);
        gap.transform.localScale = new Vector3(size.x * 0.9f, size.y * 0.9f, 1f);
        gap.sortingLayerName = "On the wall";
        gap.sortingOrder = -1;

        int split = panelsAcrossX ? texture.width / 2 : texture.height / 2;
        panels = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            Rect part = panelsAcrossX
                ? (i == 0 ? new Rect(0, 0, split, texture.height) : new Rect(split, 0, texture.width - split, texture.height))
                : (i == 0 ? new Rect(0, 0, texture.width, split) : new Rect(0, split, texture.width, texture.height - split));
            Vector2 pivot = panelsAcrossX ? new Vector2(i, 0.5f) : new Vector2(0.5f, i);
            Vector2 hinge = panelsAcrossX ? new Vector2(center.x + (i == 0 ? -1 : 1) * size.x * 0.5f, center.y)
                : new Vector2(center.x, center.y + (i == 0 ? -1 : 1) * size.y * 0.5f);
            var art = new GameObject("Door").AddComponent<SpriteRenderer>();
            art.transform.SetParent(transform, false);
            art.sprite = Sprite.Create(texture, part, pivot, PixelsPerUnit);
            art.transform.position = new Vector3(hinge.x, hinge.y, z);
            art.sortingLayerName = "On the wall";
            panels[i] = art.transform;
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

    void OnDrawGizmosSelected()
    {
        if (teleportTarget == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, teleportTarget.position);
        Gizmos.DrawWireSphere(teleportTarget.position, 0.3f);
    }
}
