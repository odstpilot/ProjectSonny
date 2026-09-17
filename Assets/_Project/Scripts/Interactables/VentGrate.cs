using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A grate into the air ducts. Walk up to it and press E to crawl in: the player comes out on the VentNetwork's map at
// this grate's number. Climbing out over a grate on the map brings them out at the VentGrate with that number, which
// can be anywhere in the level.
// Setup: a Collider2D on this object for the grate's footprint (a trigger is fine), and the sprite on a child so it can rattle.
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

    static readonly List<VentGrate> all = new List<VentGrate>();

    public Vector2 ExitPoint => (Vector2)transform.position + exitOffset;
    public VentNetwork Network => network != null ? network : (network = FindAnyObjectByType<VentNetwork>());
    // Just above the top of the sprite, where the prompt sits.
    Vector3 PromptPoint =>
        new Vector3(transform.position.x, (body != null ? body.bounds.max.y : transform.position.y) + 0.2f, transform.position.z);

    private Collider2D footprint;
    private SpriteRenderer body;
    private Vector3 bodyBasePosition;
    private Transform player;
    private Collider2D playerCollider;
    private Health playerHealth;
    private PlayerController playerController;

    // The grate with this number into these ducts, or null.
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
        if (body != null) bodyBasePosition = body.transform.localPosition;
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
        if (Input.GetKeyDown(InteractKey))
        {
            InteractPrompt.Hide(this);
            vents.Enter(this);
        }
    }

    // Shakes when someone climbs in or out.
    public void Rattle()
    {
        if (isActiveAndEnabled && body != null) StartCoroutine(RattleRoutine());
    }

    // Not paused, not hidden somewhere already, not dead, and not frozen by a terminal or the death respawn.
    bool CanPlayerAct()
    {
        if (Time.timeScale <= 0f || Locker.IsPlayerHidden) return false;
        if (playerController != null && !playerController.enabled) return false;
        return playerHealth == null || !playerHealth.IsDead;
    }

    // Edge to edge, so it doesn't matter which side the player walks up from.
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

    // Select the grate to see where the player climbs out (green).
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ExitPoint, 0.25f);
        Gizmos.DrawLine(transform.position, ExitPoint);
    }
}
