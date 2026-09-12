using System.Collections.Generic;
using UnityEngine;

// Walking, running on stamina, facing, footsteps, and the animator. The player's health is the Health component beside
// this one, and PlayerHealthHandler deals with getting hurt and dying. Enemies tagged Enemy kill on touch.
[RequireComponent(typeof(Health))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float stamina = 100f;
    public float maxStamina = 100f;
    public float staminaDrain = 25f;
    public float staminaRegen = 15f;

    public Rigidbody2D rb;
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("Footstep Settings")]
    public AudioClip[] footstepClips;
    public float baseStepRate = 0.4f; // Normal walking step interval
    private float stepTimer;
    [SerializeField] private AudioSource audioSource;
    private Vector2 movement;
    private float currentSpeed;
    private Health health;

    // Set each frame by the combat scripts: 0 stops the player mid-attack, in between slows them while charging.
    [System.NonSerialized] public float combatSpeedMultiplier = 1f;
    // Sent to the Animator's WeaponType parameter every frame. Set by PlayerCombat.
    [System.NonSerialized] public WeaponType heldWeaponType = WeaponType.None;

    // The way the player is looking: always straight up, down, left, or right.
    public Vector2 Facing { get; private set; } = Vector2.down;
    private bool hasFacingOverride;
    private Vector2 facingOverride;
    private int frozenDirection;
    private HashSet<int> animatorParameters;
    // States in the old PlayerCont controller, indexed by its Direction parameter.
    private static readonly int[] directionStates =
    {
        Animator.StringToHash("Idle"), Animator.StringToHash("Up"), Animator.StringToHash("Down"),
        Animator.StringToHash("Left"), Animator.StringToHash("Right")
    };

    private void Start()
    {
        health = GetComponent<Health>();
        stepTimer = baseStepRate;
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        movement = movement.normalized;
        if (combatSpeedMultiplier <= 0f)
            movement = Vector2.zero;

        bool isMoving = movement != Vector2.zero;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && stamina > 0 && isMoving && combatSpeedMultiplier >= 1f;

        if (isSprinting)
        {
            currentSpeed = moveSpeed * sprintMultiplier;
            stamina -= staminaDrain * Time.deltaTime;
            stamina = Mathf.Clamp(stamina, 0, maxStamina);
        }
        else
        {
            currentSpeed = moveSpeed;
            if (!Input.GetKey(KeyCode.LeftShift) || !isMoving)
            {
                stamina += staminaRegen * Time.deltaTime;
                stamina = Mathf.Clamp(stamina, 0, maxStamina);
            }
        }

        currentSpeed *= combatSpeedMultiplier;

        // Face where combat says (aiming a gun, mid-swing); otherwise face where you're walking.
        if (hasFacingOverride)
            Facing = facingOverride;
        else if (isMoving)
            Facing = SnapToFourWay(movement);

        UpdateAnimator(isMoving);

        // Footstep sound handling
        HandleFootsteps(isMoving, isSprinting);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }

    // Makes the player look a certain way no matter where they walk, until ClearFacingOverride is called.
    public void SetFacingOverride(Vector2 direction)
    {
        hasFacingOverride = true;
        facingOverride = SnapToFourWay(direction);
    }

    public void ClearFacingOverride()
    {
        hasFacingOverride = false;
    }

    public static Vector2 SnapToFourWay(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return direction.x > 0 ? Vector2.right : Vector2.left;
        return direction.y > 0 ? Vector2.up : Vector2.down;
    }

    // Animator parameters are optional: ones the controller doesn't have are skipped,
    // so animations can be added a piece at a time. See PlayerAnimParams for the full list.
    public void SetAnimTrigger(int parameter)
    {
        if (CanSetAnimParameter(parameter)) animator.SetTrigger(parameter);
    }

    public void SetAnimBool(int parameter, bool value)
    {
        if (CanSetAnimParameter(parameter)) animator.SetBool(parameter, value);
    }

    private void UpdateAnimator(bool isMoving)
    {
        if (CanSetAnimParameter(PlayerAnimParams.FaceX)) animator.SetFloat(PlayerAnimParams.FaceX, Facing.x);
        if (CanSetAnimParameter(PlayerAnimParams.FaceY)) animator.SetFloat(PlayerAnimParams.FaceY, Facing.y);
        SetAnimBool(PlayerAnimParams.IsMoving, isMoving);
        if (CanSetAnimParameter(PlayerAnimParams.WeaponType)) animator.SetInteger(PlayerAnimParams.WeaponType, (int)heldWeaponType);

        if (!CanSetAnimParameter(PlayerAnimParams.Direction)) return;

        // Old PlayerCont controller: Direction 0 is the forward idle and 1-4 are walk cycles, with no idle for
        // the other directions. Standing still facing up, left, or right holds that walk's first frame instead.
        int direction = Facing.y > 0.5f ? 1 : Facing.y < -0.5f ? 2 : Facing.x < 0f ? 3 : 4;
        bool holdWalkFrame = !isMoving && direction != 2;
        animator.SetInteger(PlayerAnimParams.Direction, isMoving || holdWalkFrame ? direction : 0);

        if (holdWalkFrame)
        {
            if (frozenDirection != direction && animator.HasState(0, directionStates[direction]))
                animator.Play(directionStates[direction], 0, 0f);
            frozenDirection = direction;
            animator.speed = 0f;
        }
        else
        {
            frozenDirection = 0;
            animator.speed = 1f;
        }
    }

    private bool CanSetAnimParameter(int parameter)
    {
        if (animator == null || !animator.isActiveAndEnabled) return false;

        if (animatorParameters == null)
        {
            animatorParameters = new HashSet<int>();
            foreach (AnimatorControllerParameter p in animator.parameters)
                animatorParameters.Add(p.nameHash);
        }
        return animatorParameters.Contains(parameter);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CaughtBy(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CaughtBy(collision.gameObject);
    }

    // The older enemies (the Warden, ghosts) don't attack: touching the player is enough.
    private void CaughtBy(GameObject other)
    {
        if (!other.CompareTag("Enemy")) return;

        if (health != null) health.Kill(other);
        if (other.TryGetComponent<Warden>(out var warden))
            warden.Reset();
    }

    void HandleFootsteps(bool isMoving, bool isSprinting)
    {
        if (!isMoving || footstepClips.Length == 0 || audioSource == null) return;

        stepTimer -= Time.deltaTime;

        float stepRate = isSprinting ? baseStepRate * 0.6f : baseStepRate;

        if (stepTimer <= 0f)
        {
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            audioSource.pitch = isSprinting ? 1.2f : 1f;
            audioSource.PlayOneShot(clip);
            stepTimer = stepRate;
        }
    }


}
