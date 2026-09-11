using UnityEngine;

// A heavy shutter across a corridor. Closed, it's solid and its lamp is red. Opening, it grinds up into the ceiling
// (or aside, for a door wider than it is tall) and the lamp turns green. Levels call Open and Close; a door can also
// open by itself when the player comes near. It never closes on anyone: it waits until the doorway is clear.
// Setup: a solid BoxCollider2D on this object, sized to the doorway. The shutter art is made in code unless one is set.
[RequireComponent(typeof(BoxCollider2D))]
public class BlastDoor : MonoBehaviour
{
    const float PixelsPerUnit = 20f;
    const int Thickness = 12;           // pixels across the shutter
    const float OpenLip = 0.08f;        // how much of the shutter still shows when fully open
    static readonly Color32 Frame = new Color32(30, 33, 38, 255);
    static readonly Color32 Metal = new Color32(74, 80, 88, 255);
    static readonly Color32 MetalLit = new Color32(104, 111, 120, 255);
    static readonly Color32 Stripe = new Color32(222, 170, 40, 255);
    static readonly Color32 StripeDark = new Color32(30, 30, 32, 255);
    static readonly Color ClosedLamp = new Color(1f, 0.2f, 0.15f);
    static readonly Color OpenLamp = new Color(0.3f, 1f, 0.45f);

    public bool startsOpen;
    [Tooltip("Seconds to open or close all the way.")]
    public float moveTime = 0.7f;
    [Tooltip("Opens by itself when the player comes this close, unless locked. 0 turns it off.")]
    public float openWhenPlayerWithin;
    [Tooltip("A locked door ignores the player coming near. Open and Close still work.")]
    public bool locked;
    [Tooltip("Leave empty for the placeholder shutter. Put the pivot on the edge it retracts into.")]
    public Sprite shutterSprite;
    public AudioClip moveClip;
    [Range(0f, 1f)] public float volume = 0.7f;

    public event System.Action Opened;
    public event System.Action Closed;

    public bool IsOpen => wantOpen && progress >= 1f;
    public bool IsClosed => !wantOpen && progress <= 0f;

    private BoxCollider2D doorway;
    private Transform shutter;
    private Vector3 shutterScale = Vector3.one;
    private SpriteRenderer lamp;
    private AudioSource audioSource;
    private Transform player;
    private bool vertical;
    private bool wantOpen;
    private float progress;     // 0 closed, 1 open

    void Awake()
    {
        doorway = GetComponent<BoxCollider2D>();
        vertical = doorway.size.y >= doorway.size.x;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        BuildVisuals();

        wantOpen = startsOpen;
        progress = startsOpen ? 1f : 0f;
        Apply();
    }

    void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;
    }

    public void Open()
    {
        if (wantOpen) return;
        wantOpen = true;
        PlayMoveSound();
    }

    public void Close()
    {
        if (!wantOpen) return;
        wantOpen = false;
        PlayMoveSound();
    }

    void Update()
    {
        if (!wantOpen && !locked && openWhenPlayerWithin > 0f && player != null &&
            Vector2.Distance(player.position, transform.position) <= openWhenPlayerWithin)
        {
            Open();
        }

        float target = wantOpen ? 1f : 0f;
        if (progress == target) return;
        if (!wantOpen && DoorwayOccupied()) return;

        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.01f, moveTime));
        Apply();

        if (progress == target)
        {
            if (wantOpen) Opened?.Invoke();
            else Closed?.Invoke();
        }
    }

    void Apply()
    {
        float eased = progress * progress * (3f - 2f * progress);
        float showing = Mathf.Lerp(1f, OpenLip, eased);
        shutter.localScale = vertical
            ? new Vector3(shutterScale.x, shutterScale.y * showing, 1f)
            : new Vector3(shutterScale.x * showing, shutterScale.y, 1f);
        doorway.enabled = progress < 0.8f;
        lamp.color = wantOpen ? OpenLamp : ClosedLamp;
    }

    // Someone (the player or a robot) standing in the doorway.
    bool DoorwayOccupied()
    {
        Vector2 center = transform.TransformPoint(doorway.offset);
        foreach (Collider2D col in Physics2D.OverlapBoxAll(center, doorway.size, 0f))
        {
            if (col == doorway || col.isTrigger) continue;
            Rigidbody2D body = col.attachedRigidbody;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic) return true;
        }
        return false;
    }

    void PlayMoveSound()
    {
        if (moveClip == null) return;
        Camera cam = Camera.main;
        float distance = cam != null ? Vector2.Distance(cam.transform.position, transform.position) : 0f;
        float nearness = Mathf.Clamp01(1.4f - distance / 12f);
        if (nearness > 0f) audioSource.PlayOneShot(moveClip, volume * nearness);
    }

    void BuildVisuals()
    {
        Vector2 size = doorway.size;

        var shutterRenderer = new GameObject("Shutter").AddComponent<SpriteRenderer>();
        shutter = shutterRenderer.transform;
        shutter.SetParent(transform, false);
        // The pivot sits on the edge it retracts into: the top of a tall door, the left of a wide one.
        shutter.localPosition = doorway.offset + (vertical ? new Vector2(0f, size.y * 0.5f) : new Vector2(-size.x * 0.5f, 0f));
        shutterRenderer.sortingLayerName = "Collision";
        shutterRenderer.sortingOrder = 5;

        if (shutterSprite != null)
        {
            shutterRenderer.sprite = shutterSprite;
            Vector2 spriteSize = shutterSprite.bounds.size;
            shutterScale = new Vector3(
                vertical ? 1f : size.x / Mathf.Max(0.01f, spriteSize.x),
                vertical ? size.y / Mathf.Max(0.01f, spriteSize.y) : 1f, 1f);
        }
        else
        {
            shutterRenderer.sprite = MakeShutter(vertical ? size.y : size.x);
        }

        lamp = new GameObject("Lamp").AddComponent<SpriteRenderer>();
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = doorway.offset + (vertical ? new Vector2(0f, size.y * 0.5f + 0.3f) : new Vector2(-size.x * 0.5f - 0.3f, 0f));
        lamp.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
        lamp.sprite = CombatSprites.Square;
        if (CombatSprites.EffectMaterial != null) lamp.sharedMaterial = CombatSprites.EffectMaterial;
        lamp.sortingLayerName = "Top";
    }

    // Dark metal edged in hazard stripes down the middle, lit a little on one side.
    Sprite MakeShutter(float lengthInUnits)
    {
        int length = Mathf.Max(8, Mathf.RoundToInt(lengthInUnits * PixelsPerUnit));
        Texture2D texture = PixelArt.MakeTexture(vertical ? Thickness : length, vertical ? length : Thickness, (x, y) =>
        {
            int across = vertical ? x : y;
            int along = vertical ? y : x;
            if (across == 0 || across == Thickness - 1 || along == 0 || along == length - 1) return Frame;
            if (across >= 4 && across <= 7) return (along + across) / 3 % 2 == 0 ? Stripe : StripeDark;
            if (along % 10 == 5 && (across == 2 || across == 9)) return Frame;
            return across < 4 ? MetalLit : Metal;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, vertical ? new Vector2(0.5f, 1f) : new Vector2(0f, 0.5f));
    }
}
