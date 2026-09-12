using System.Collections.Generic;
using UnityEngine;

// Something the player walks up to and presses E to use, like a terminal. While the player is close enough and free to
// act, it shows a prompt over itself (InteractPrompt), and pressing E uses it. Only one terminal can be in use at a time.
// Subclasses say what the prompt says, whether E does anything right now, and what using it opens.
// Setup: a solid Collider2D on this object for its footprint.
[RequireComponent(typeof(Collider2D))]
public abstract class Terminal : MonoBehaviour
{
    public const KeyCode InteractKey = KeyCode.E;

    [Tooltip("How close the player has to get to its edge to use it.")]
    public float interactRange = 0.6f;
    [Tooltip("Font for its screen and prompt. Leave empty for TextMesh Pro's default.")]
    public Font screenFont;

    static readonly List<Terminal> all = new List<Terminal>();

    // True while its screen is up.
    public abstract bool InUse { get; }
    // What the prompt says after the key, like "TUNE IN".
    protected abstract string PromptText { get; }
    protected abstract Vector3 PromptPoint { get; }
    // False keeps the prompt up but ignores E, for something like a lockout.
    protected virtual bool Available => true;

    protected Transform Player { get; private set; }
    protected Collider2D Footprint { get; private set; }

    private Collider2D playerCollider;
    private PlayerController playerController;
    private Health playerHealth;

    protected virtual void Awake()
    {
        Footprint = GetComponent<Collider2D>();
    }

    protected virtual void OnEnable()
    {
        all.Add(this);
    }

    protected virtual void OnDisable()
    {
        all.Remove(this);
        InteractPrompt.Hide(this);
    }

    protected virtual void Start()
    {
        FindPlayer();
    }

    protected virtual void Update()
    {
        if (Player == null) return;

        bool free = !AnyInUse()
            && Time.timeScale > 0f
            && !Locker.IsPlayerHidden
            && (playerController == null || playerController.enabled)
            && (playerHealth == null || !playerHealth.IsDead);

        if (free && DistanceToPlayer() <= interactRange)
        {
            InteractPrompt.Show(this, PromptPoint, $"{InteractKey}  {PromptText}", screenFont);
            if (Input.GetKeyDown(InteractKey)) Interact();
        }
        else
        {
            InteractPrompt.Hide(this);
        }
    }

    // Uses it now, wherever the player is standing. The player normally presses E; this is for scripts and tests.
    public void Interact()
    {
        if (Player == null) FindPlayer();
        if (Player == null || AnyInUse() || !Available) return;
        InteractPrompt.Hide(this);
        Use(Player);
    }

    protected abstract void Use(Transform player);

    static bool AnyInUse()
    {
        foreach (Terminal terminal in all)
            if (terminal.InUse) return true;
        return false;
    }

    void FindPlayer()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return;
        Player = found.transform;
        playerCollider = found.GetComponent<Collider2D>();
        playerController = found.GetComponent<PlayerController>();
        playerHealth = found.GetComponent<Health>();
    }

    // Edge to edge, so it doesn't matter which side the player walks up from.
    float DistanceToPlayer()
    {
        if (playerCollider != null && playerCollider.enabled)
        {
            ColliderDistance2D gap = Footprint.Distance(playerCollider);
            if (gap.isValid) return gap.distance;
        }
        return Vector2.Distance(Footprint.ClosestPoint(Player.position), Player.position);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        var footprint = GetComponent<Collider2D>();
        if (footprint == null) return;
        Gizmos.color = new Color(1f, 0.62f, 0.25f);
        Bounds bounds = footprint.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size + Vector3.one * interactRange * 2f);
    }
}
