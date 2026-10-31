using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// locker to hide in
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

    public static bool IsPlayerHidden => inLocker || VentNetwork.IsPlayerInside;
    public static Locker Occupied => inLocker ? inUse : null;

    public Vector2 DoorDirection => doorDirection.sqrMagnitude > 0f ? doorDirection.normalized : Vector2.down;
    public Vector2 ExitPoint => (Vector2)transform.position + DoorDirection * exitDistance;
    public SpriteRenderer Body => body;
    public Vector2 EyePoint => (Vector2)footprint.bounds.center + Vector2.Scale(DoorDirection, footprint.bounds.extents);
    public Vector3 PromptPoint =>
        new Vector3(transform.position.x, (body != null ? body.bounds.max.y : transform.position.y) + 0.2f, transform.position.z);

    enum State { Outside, GettingIn, Inside, GettingOut }

    static readonly List<Locker> all = new List<Locker>();
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
        InteractPrompt.Hide(this);

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

        if (state == State.Inside)
        {
            if (Input.GetKeyDown(InteractKey) && Time.timeScale > 0f)
                StartCoroutine(GetOut());
            return;
        }

        bool canUse = state == State.Outside && inUse == null && Time.timeScale > 0f && !DialogueBox.Busy && IsClosestUsable();
        if (!canUse)
        {
            InteractPrompt.Hide(this);
            return;
        }
        InteractPrompt.Show(this, PromptPoint, $"{InteractKey}  HIDE");
        if (InteractPrompt.Pressed(this, InteractKey))
        {
            InteractPrompt.Hide(this);
            StartCoroutine(GetIn());
        }
    }

    public void PullPlayerOut(System.Action whenOut = null)
    {
        if (inUse != this || !inLocker)
        {
            whenOut?.Invoke();
            return;
        }
        StopAllCoroutines();
        state = State.GettingOut;
        StartCoroutine(PulledOut(whenOut));
    }

    IEnumerator PulledOut(System.Action whenOut)
    {
        yield return LockerView.Get(font).PullOut();

        ShowPlayer();
        LockerView.ForceClose();
        if (body != null) body.transform.localPosition = bodyBasePosition;
        inUse = null;
        state = State.Outside;
        whenOut?.Invoke();
        yield return Rattle();
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

    void ForceOut()
    {
        StopAllCoroutines();
        if (inLocker) ShowPlayer();
        LockerView.ForceClose();
        if (body != null) body.transform.localPosition = bodyBasePosition;
        inUse = null;
        state = State.Outside;
    }

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

    float DistanceToPlayer()
    {
        if (playerCollider != null && playerCollider.enabled)
        {
            ColliderDistance2D gap = footprint.Distance(playerCollider);
            if (gap.isValid) return gap.distance;
        }
        return Vector2.Distance(footprint.ClosestPoint(player.position), player.position);
    }

    void HidePlayer()
    {
        inLocker = true;

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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ExitPoint, 0.25f);
        if (footprint == null) footprint = GetComponent<Collider2D>();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(EyePoint, EyePoint + DoorDirection * 2f);
    }
}
