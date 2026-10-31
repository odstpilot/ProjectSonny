using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// grate into the vents
[RequireComponent(typeof(Collider2D))]
public class VentGrate : MonoBehaviour
{
    public const KeyCode InteractKey = KeyCode.E;
    const float RattleTime = 0.18f;
    const float RattleAmount = 0.04f;

    [Tooltip("Which number on the network's map this grate is, 1 to 9.")]
    [Range(1, 9)] public int number = 1;
    [Tooltip("The ducts it opens into. Leave empty to use the only VentNetwork in the scene.")]
    public VentNetwork network;
    [Tooltip("How close the player has to get to the grate's edge to use it, in world units.")]
    public float interactRange = 0.5f;
    [Tooltip("Where the player is put when they climb out here, from the grate's middle.")]
    public Vector2 exitOffset = new Vector2(0f, -1f);
    [Tooltip("Draws the grate in code in place of the child's sprite. Off keeps the sprite as it is.")]
    public bool drawGrate = true;

    static readonly List<VentGrate> all = new List<VentGrate>();

    public Vector2 ExitPoint => (Vector2)transform.position + exitOffset;
    public VentNetwork Network => network != null ? network : (network = FindAnyObjectByType<VentNetwork>());
    Vector3 PromptPoint =>
        new Vector3(transform.position.x, (body != null ? body.bounds.max.y : transform.position.y) + 0.2f, transform.position.z);

    private Collider2D footprint;
    private SpriteRenderer body;
    private Vector3 bodyBasePosition;
    private Transform player;
    private Collider2D playerCollider;
    private Health playerHealth;
    private PlayerController playerController;

    public static VentGrate Find(VentNetwork network, int number)
    {
        foreach (VentGrate grate in all)
            if (grate.number == number && grate.Network == network) return grate;
        return null;
    }

    void Awake()
    {
        footprint = GetComponent<Collider2D>();
        body = GetComponentInChildren<SpriteRenderer>();
        if (body != null && drawGrate)
        {
            Vector3 middle = body.sprite != null ? body.bounds.center : transform.position;
            body.sprite = GrateSprite;
            body.transform.position = new Vector3(middle.x, middle.y, body.transform.position.z);
        }
        if (body != null) bodyBasePosition = body.transform.localPosition;
    }

    const int GratePixels = 32;
    static Sprite grateSprite;

    static Sprite GrateSprite => grateSprite != null ? grateSprite : (grateSprite = PixelArt.ToSprite(PixelArt.MakeTexture(GratePixels, GratePixels, GratePixel),
        GratePixels, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect));

    static readonly Color32 GrateOutline = new Color32(14, 16, 20, 255);
    static readonly Color32 GrateFrame = new Color32(92, 100, 112, 255);
    static readonly Color32 GrateFrameLit = new Color32(140, 150, 162, 255);
    static readonly Color32 GrateFrameShade = new Color32(58, 64, 74, 255);
    static readonly Color32 GrateBolt = new Color32(176, 184, 194, 255);
    static readonly Color32 GrateSlat = new Color32(76, 84, 96, 255);
    static readonly Color32 GrateSlatLit = new Color32(118, 128, 140, 255);
    static readonly Color32 GrateDuct = new Color32(8, 10, 14, 255);
    static readonly Color32 GrateDuctDeep = new Color32(18, 24, 34, 255);

    static Color32 GratePixel(int x, int y)
    {
        const int n = GratePixels;
        int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(n - 1 - x, n - 1 - y));
        if (edge == 0) return GrateOutline;
        if (edge <= 3)
        {
            bool corner = (x == 2 || x == n - 3) && (y == 2 || y == n - 3);
            if (corner) return GrateBolt;
            if (edge == 1) return x == 1 || y == n - 2 ? GrateFrameLit : GrateFrameShade;
            return GrateFrame;
        }
        if (edge == 4) return GrateOutline;
        int band = (n - 5 - y) % 5;
        if (band < 3) return band == 0 ? GrateSlatLit : GrateSlat;
        float fromMiddle = Mathf.Abs(x - (n - 1) * 0.5f) / (n * 0.5f);
        return fromMiddle < 0.45f ? GrateDuctDeep : GrateDuct;
    }

    void OnEnable()
    {
        all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
        InteractPrompt.Hide(this);
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
        if (player == null) return;

        VentNetwork vents = Network;
        bool canUse = vents != null && !vents.InUse && CanPlayerAct() && DistanceToPlayer() <= interactRange;
        if (!canUse)
        {
            InteractPrompt.Hide(this);
            return;
        }

        InteractPrompt.Show(this, PromptPoint, $"{InteractKey}  CRAWL IN");
        if (InteractPrompt.Pressed(this, InteractKey))
        {
            InteractPrompt.Hide(this);
            vents.Enter(this);
        }
    }

    public void Rattle()
    {
        if (isActiveAndEnabled && body != null) StartCoroutine(RattleRoutine());
    }

    bool CanPlayerAct()
    {
        if (Time.timeScale <= 0f || Locker.IsPlayerHidden) return false;
        if (playerController != null && !playerController.enabled) return false;
        return playerHealth == null || !playerHealth.IsDead;
    }

    float DistanceToPlayer()
    {
        if (playerCollider != null && playerCollider.enabled)
        {
            ColliderDistance2D gap = footprint.Distance(playerCollider);
            if (gap.isValid) return gap.distance;
        }
        return Vector2.Distance(footprint.ClosestPoint(player.position), player.position);
    }

    IEnumerator RattleRoutine()
    {
        for (float t = 0f; t < RattleTime; t += Time.deltaTime)
        {
            float strength = RattleAmount * (1f - t / RattleTime);
            body.transform.localPosition = bodyBasePosition + new Vector3(Random.Range(-strength, strength), 0f, 0f);
            yield return null;
        }
        body.transform.localPosition = bodyBasePosition;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ExitPoint, 0.25f);
        Gizmos.DrawLine(transform.position, ExitPoint);
    }
}
