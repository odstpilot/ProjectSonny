using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A locker the player can hide in. Walk up to it and press E: the player climbs in and is taken out of the world
// (no sprite, no physics, so enemies can't see, touch, or hit them), and the screen switches to the view from
// inside, looking out through the door's vent slats (see LockerView). Press E again to step back out the door.
// Enemy scripts can check Locker.IsPlayerHidden to decide whether to keep hunting, Locker.Occupied for which locker
// the player is in, and call PullPlayerOut on it if they saw the player climb in.
// Setup: a solid Collider2D on this object for the locker's footprint, and the sprite on a child so it can rattle.
[RequireComponent(typeof(Collider2D))]
public class Locker : MonoBehaviour
{
    public const KeyCode InteractKey = KeyCode.E;
    const float RattleTime = 0.18f;
    const float RattleAmount = 0.04f;

    [Header("Getting In and Out")]
    [Tooltip("How close the player has to get to the locker's edge to use it, in world units.")]
    public float interactRange = 0.5f;
    [Tooltip("The side the door is on. The player steps out and looks out this way.")]
    public Vector2 doorDirection = Vector2.down;
    [Tooltip("Where the player is put when they step out: this far from the locker's center, through the door.")]
    public float exitDistance = 1.25f;

    [Header("View From Inside")]
    [Tooltip("Seconds for each half of the fade through black on the way in and out.")]
    public float fadeTime = 0.2f;
    [Tooltip("Font for the prompt and hint. Leave empty for TextMesh Pro's default.")]
    public TMP_FontAsset font;

    // True from the moment the player climbs in until they are standing outside again, and while they're crawling
    // through the air ducts (VentNetwork), which hide them from everything the same way.
    public static bool IsPlayerHidden => inLocker || VentNetwork.IsPlayerInside;
    // The locker the player is hiding in, or null.
    public static Locker Occupied => inLocker ? inUse : null;

    public Vector2 DoorDirection => doorDirection.sqrMagnitude > 0f ? doorDirection.normalized : Vector2.down;
    public Vector2 ExitPoint => (Vector2)transform.position + DoorDirection * exitDistance;
    public SpriteRenderer Body => body;
    // Where the player looks out from while inside: the middle of the door, on the edge of the footprint.
    public Vector2 EyePoint => (Vector2)footprint.bounds.center + Vector2.Scale(DoorDirection, footprint.bounds.extents);
    // Just above the top of the sprite, where the "Hide" prompt sits.
    public Vector3 PromptPoint =>
        new Vector3(transform.position.x, (body != null ? body.bounds.max.y : transform.position.y) + 0.2f, transform.position.z);

    enum State { Outside, GettingIn, Inside, GettingOut }

    static readonly List<Locker> all = new List<Locker>();
    // The locker the player is in or on the way in or out of. The others ignore the player meanwhile.
    static Locker inUse;
    static bool inLocker;

    private Collider2D footprint;
    private SpriteRenderer body;
    private Vector3 bodyBasePosition;
    private State state = State.Outside;

    private Transform player;
    private Rigidbody2D playerBody;
    private Collider2D playerCollider;
    private Health playerHealth;
    private PlayerController playerController;
    private bool playerWasSimulated;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

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
        LockerView.SetPrompt(this, false, font);

        // Switched off or unloaded with the player inside: put everything back at once instead of trapping them.
        if (inUse == this) ForceOut();
    }

    void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return;

        player = found.transform;
        playerBody = found.GetComponent<Rigidbody2D>();
        playerCollider = found.GetComponent<Collider2D>();
        playerHealth = found.GetComponent<Health>();
        playerController = found.GetComponent<PlayerController>();
    }

    void Update()
    {
        if (player == null) return;

        // timeScale 0 means a menu paused the game.
        if (state == State.Inside)
        {
            if (Input.GetKeyDown(InteractKey) && Time.timeScale > 0f)
                StartCoroutine(GetOut());
            return;
        }

        bool canUse = state == State.Outside && inUse == null && Time.timeScale > 0f && IsClosestUsable();
        LockerView.SetPrompt(this, canUse, font);
        if (canUse && Input.GetKeyDown(InteractKey))
            StartCoroutine(GetIn());
    }

    // For an enemy that saw the player climb in: the door flies open and the player is back outside at once, no fade.
    public void PullPlayerOut()
    {
        if (inUse != this || !inLocker) return;
        ForceOut();
        StartCoroutine(Rattle());
    }

    IEnumerator GetIn()
    {
        inUse = this;
        state = State.GettingIn;
        HidePlayer();
        StartCoroutine(Rattle());

        LockerView view = LockerView.Get(font);
        yield return view.FadeTo(1f, fadeTime);
        view.Open(this);
        yield return view.FadeTo(0f, fadeTime);
        state = State.Inside;
    }

    IEnumerator GetOut()
    {
        state = State.GettingOut;

        LockerView view = LockerView.Get(font);
        yield return view.FadeTo(1f, fadeTime);
        ShowPlayer();
        view.Close();
        yield return view.FadeTo(0f, fadeTime);
        StartCoroutine(Rattle());

        inUse = null;
        state = State.Outside;
    }

    // Puts everything back immediately, skipping any transition: the player outside and the normal view.
    void ForceOut()
    {
        StopAllCoroutines();
        if (inLocker) ShowPlayer();
        LockerView.ForceClose();
        if (body != null) body.transform.localPosition = bodyBasePosition;
        inUse = null;
        state = State.Outside;
    }

    // In range, able to act (not dead or frozen by the death respawn), and the nearest locker if several are in reach.
    bool IsClosestUsable()
    {
        if (playerController != null && !playerController.enabled) return false;
        if (playerHealth != null && playerHealth.IsDead) return false;

        float distance = DistanceToPlayer();
        if (distance > interactRange) return false;

        foreach (Locker other in all)
        {
            if (other != this && other.player != null && other.DistanceToPlayer() < distance)
                return false;
        }
        return true;
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

    // Takes the player out of the world without destroying anything. Whatever gets turned off is remembered so
    // exactly that comes back, the same way PlayerHealthHandler freezes the player on death.
    void HidePlayer()
    {
        inLocker = true;

        // Scripts stop, so no moving, attacking, or weapon swapping. Health and its handler stay on for the HUD,
        // and lights stay on so there's something to see out the vent in a dark room.
        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>())
        {
            if (!behaviour.enabled || behaviour is Health || behaviour is PlayerHealthHandler || behaviour is Light2D) continue;
            behaviour.enabled = false;
            disabledBehaviours.Add(behaviour);
        }

        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            renderer.enabled = false;
            hiddenRenderers.Add(renderer);
        }

        // No simulation means no collisions, triggers, raycast hits, or overlap checks: invisible to every enemy.
        if (playerBody != null)
        {
            playerWasSimulated = playerBody.simulated;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.simulated = false;
        }
        player.position = new Vector3(transform.position.x, transform.position.y, player.position.z);
    }

    void ShowPlayer()
    {
        inLocker = false;

        if (player != null)
        {
            Vector2 exit = ExitPoint;
            player.position = new Vector3(exit.x, exit.y, player.position.z);
            if (playerBody != null)
            {
                playerBody.simulated = playerWasSimulated;
                playerBody.position = exit;
                playerBody.linearVelocity = Vector2.zero;
            }
        }

        foreach (Renderer renderer in hiddenRenderers)
            if (renderer != null) renderer.enabled = true;
        foreach (Behaviour behaviour in disabledBehaviours)
            if (behaviour != null) behaviour.enabled = true;
        hiddenRenderers.Clear();
        disabledBehaviours.Clear();
    }

    // The door shakes when someone climbs in or out.
    IEnumerator Rattle()
    {
        if (body == null) yield break;

        for (float t = 0f; t < RattleTime; t += Time.deltaTime)
        {
            float strength = RattleAmount * (1f - t / RattleTime);
            body.transform.localPosition = bodyBasePosition + new Vector3(Random.Range(-strength, strength), 0f, 0f);
            yield return null;
        }
        body.transform.localPosition = bodyBasePosition;
    }

    // Select the locker to see where the player steps out (green) and which way the view from inside looks (cyan).
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ExitPoint, 0.25f);
        if (footprint == null) footprint = GetComponent<Collider2D>();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(EyePoint, EyePoint + DoorDirection * 2f);
    }
}
