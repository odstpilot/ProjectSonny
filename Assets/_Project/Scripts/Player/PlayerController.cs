using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Walking, running on stamina, crouching, facing, footsteps, and the animator. The player's health is the Health component beside
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

    [Header("Crouch (C or Ctrl)")]
    [Tooltip("Speed while crouching, as a fraction of walking speed. No sprinting while crouched.")]
    [Range(0.1f, 1f)] public float crouchSpeedMultiplier = 0.5f;
    [Tooltip("Tap C or Ctrl to crouch and tap again to stand, instead of holding the key down.")]
    public bool crouchToggles = false;
    [Tooltip("How loud footsteps are while crouching, as a fraction of normal.")]
    [Range(0f, 1f)] public float crouchFootstepVolume = 0.35f;
    [Tooltip("How tall the sprite is while crouched, as a fraction of standing. Its feet stay planted.")]
    [Range(0.5f, 1f)] public float crouchHeight = 0.8f;
    [Tooltip("How much wider the sprite gets while crouched, so it reads as squatting rather than shrinking.")]
    [Range(1f, 1.5f)] public float crouchWidth = 1.08f;
    [Tooltip("Seconds to get down into a crouch, or back up out of one.")]
    public float crouchTransitionTime = 0.12f;

    [Header("Sprint Lean")]
    [Tooltip("How far the sprite tips forward while sprinting sideways, in degrees. Its feet stay planted.")]
    [Range(0f, 25f)] public float sprintLeanAngle = 8f;
    [Tooltip("How tall the sprite is while sprinting, as a fraction of standing.")]
    [Range(0.8f, 1.3f)] public float sprintHeight = 1.05f;
    [Tooltip("How wide the sprite is while sprinting, as a fraction of standing.")]
    [Range(0.7f, 1.2f)] public float sprintWidth = 0.95f;
    [Tooltip("Seconds to lean into a sprint, or straighten back up out of one.")]
    public float sprintLeanTime = 0.15f;

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
    // Robots see a crouching player from less far away and take longer to be sure of them (PlaceholderRobot).
    public bool IsCrouching { get; private set; }
    // Robots see a sprinting player from further away, make them out faster, and hear them coming from behind.
    public bool IsSprinting { get; private set; }
    // How squashed or stretched the sprite is right now: (1, 1) standing. WeaponVisual follows it so a held weapon
    // stays in the posed hand.
    public Vector2 BodySquash => squash;
    // How far the sprite is tipped by a sprint lean, in degrees. WeaponVisual takes it back out of a gun's aim.
    public float LeanAngle => lean;
    // How far the body has been shifted to keep the feet planted, in world units. StealthMeter takes it back off so
    // its arcs stay on the floor.
    public Vector2 PoseOffset => poseOffset;

    private float crouchAmount;             // 0 standing, 1 fully crouched, easing between
    private float sprintAmount;             // 0 standing, 1 fully leaned into a sprint, easing between
    private float leanDirection;            // -1 leaning left, 1 leaning right
    private Vector2 squash = Vector2.one;
    private float lean;
    private Vector2 poseOffset;
    private Vector3 baseScale = Vector3.one;
    private float spriteHalfHeight;
    private BoxCollider2D bodyCollider;
    private Vector2 bodyBaseSize;
    private Vector2 bodyBaseOffset;
    private readonly List<Transform> carriedLights = new List<Transform>();
    private readonly List<Vector3> carriedLightScales = new List<Vector3>();
    private bool hasFacingOverride;
    private Vector2 facingOverride;
    private int frozenDirection;
    private bool scripted;
    private Vector2 scriptedMove;
    private bool scriptedSprint;
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
        CaptureStandingPose();
        DepthSort.Group(gameObject);
    }

    // For cutscenes: moves the player as if these keys were held, walking or sprinting, until ClearScriptedInput. The
    // player's own keys are ignored meanwhile, and a scripted sprint doesn't use up stamina.
    public void SetScriptedInput(Vector2 move, bool sprint)
    {
        scripted = true;
        scriptedMove = move;
        scriptedSprint = sprint;
    }

    public void ClearScriptedInput()
    {
        scripted = false;
    }

    void Update()
    {
        movement = scripted ? scriptedMove : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        movement = movement.normalized;
        if (combatSpeedMultiplier <= 0f)
            movement = Vector2.zero;

        if (scripted) IsCrouching = false;
        else UpdateCrouch();

        bool isMoving = movement != Vector2.zero;
        bool sprintHeld = scripted ? scriptedSprint : Input.GetKey(KeyCode.LeftShift);
        bool isSprinting = !IsCrouching && sprintHeld && stamina > 0 && isMoving && combatSpeedMultiplier >= 1f;
        IsSprinting = isSprinting;
        UpdatePose();

        if (isSprinting)
        {
            currentSpeed = moveSpeed * sprintMultiplier;
            if (!scripted) stamina -= staminaDrain * Time.deltaTime;
            stamina = Mathf.Clamp(stamina, 0, maxStamina);
        }
        else
        {
            currentSpeed = IsCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed;
            if (!sprintHeld || !isMoving)
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

    // Moves by velocity rather than MovePosition: easing into a sprint or a crouch nudges the body every frame to keep the
    // feet planted (ApplyPose), and each nudge would throw away a pending MovePosition, stalling the player for as long as
    // the pose took to settle. A velocity carries on through those nudges.
    void FixedUpdate()
    {
        rb.linearVelocity = movement * currentSpeed;
    }

    // Hold C or Ctrl to crouch, or tap to switch it on and off when crouchToggles is set.
    void UpdateCrouch()
    {
        if (crouchToggles)
        {
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
                IsCrouching = !IsCrouching;
        }
        else
        {
            IsCrouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }
    }

    // Controls taken away (a terminal, dying): stand back up, so nothing is left thinking the player is hidden.
    void OnDisable()
    {
        IsCrouching = false;
        IsSprinting = false;
        SetAnimBool(PlayerAnimParams.Crouching, false);
        crouchAmount = 0f;
        sprintAmount = 0f;
        leanDirection = 0f;
        ApplyPose(Vector2.one, 0f);
        // Nothing is steering now, so don't let the last step carry the player on.
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    // What standing looks like, so a crouch can be measured against it and undone exactly.
    void CaptureStandingPose()
    {
        baseScale = transform.localScale;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            spriteHalfHeight = spriteRenderer.sprite.bounds.extents.y;

        bodyCollider = GetComponent<BoxCollider2D>();
        if (bodyCollider != null)
        {
            bodyBaseSize = bodyCollider.size;
            bodyBaseOffset = bodyCollider.offset;
        }

        // Lights carried on the player (its lantern) keep their shape instead of squashing with the sprite.
        foreach (Transform child in transform)
        {
            if (!child.TryGetComponent(out Light2D _)) continue;
            carriedLights.Add(child);
            carriedLightScales.Add(child.localScale);
        }
    }

    // Eases between standing, crouching, and leaning into a sprint.
    void UpdatePose()
    {
        float crouchStep = crouchTransitionTime > 0f ? Time.deltaTime / crouchTransitionTime : 1f;
        float sprintStep = sprintLeanTime > 0f ? Time.deltaTime / sprintLeanTime : 1f;
        crouchAmount = Mathf.MoveTowards(crouchAmount, IsCrouching ? 1f : 0f, crouchStep);
        sprintAmount = Mathf.MoveTowards(sprintAmount, IsSprinting ? 1f : 0f, sprintStep);

        // Tips toward the way they're running: diagonals lean part way, and straight up or down doesn't tip at all.
        if (IsSprinting) leanDirection = Mathf.MoveTowards(leanDirection, movement.x, sprintStep * 2f);

        float crouch = Mathf.SmoothStep(0f, 1f, crouchAmount);
        float sprint = Mathf.SmoothStep(0f, 1f, sprintAmount);
        Vector2 crouchSquash = Vector2.Lerp(Vector2.one, new Vector2(crouchWidth, crouchHeight), crouch);
        Vector2 sprintStretch = Vector2.Lerp(Vector2.one, new Vector2(sprintWidth, sprintHeight), sprint);

        // Negative turns clockwise, tipping the top of the sprite toward the right.
        ApplyPose(Vector2.Scale(crouchSquash, sprintStretch), -leanDirection * sprintLeanAngle * sprint);
    }

    // Poses the sprite: squashed or stretched, and tipped by lean degrees, always around its planted feet. The sprite,
    // animator, and collider all live on this object and the art is centre-pivoted, so the body is shifted to keep the
    // feet where they stood, and the collider is sized and placed back over exactly the ground it covered standing:
    // posing changes how the player looks, never where they fit or get hit. (The box does tip along with a lean.)
    void ApplyPose(Vector2 newSquash, float newLean)
    {
        if (newSquash == squash && Mathf.Approximately(newLean, lean)) return;
        squash = newSquash;
        lean = newLean;

        Quaternion tip = Quaternion.Euler(0f, 0f, lean);
        transform.localScale = new Vector3(baseScale.x * squash.x, baseScale.y * squash.y, baseScale.z);
        transform.localRotation = tip;

        // Shift the body by however far the pose would move the feet from where they stood.
        Vector2 standingFeet = new Vector2(0f, -spriteHalfHeight * baseScale.y);
        Vector2 posedFeet = tip * new Vector2(0f, -spriteHalfHeight * baseScale.y * squash.y);
        Vector2 offset = standingFeet - posedFeet;
        Vector2 change = offset - poseOffset;
        poseOffset = offset;

        if (rb != null)
        {
            // Set both from the body's own position, so it moves once whether or not transforms auto-sync.
            Vector2 moved = rb.position + change;
            rb.position = moved;
            transform.position = new Vector3(moved.x, moved.y, transform.position.z);
        }
        else
        {
            transform.position += (Vector3)change;
        }

        if (bodyCollider != null && baseScale.x != 0f && baseScale.y != 0f)
        {
            // The same world size, and the same world centre measured back through the new rotation and scale.
            bodyCollider.size = new Vector2(bodyBaseSize.x / squash.x, bodyBaseSize.y / squash.y);
            Vector2 standingCentre = new Vector2(bodyBaseOffset.x * baseScale.x, bodyBaseOffset.y * baseScale.y);
            Vector2 local = Quaternion.Inverse(tip) * (standingCentre - offset);
            bodyCollider.offset = new Vector2(local.x / (baseScale.x * squash.x), local.y / (baseScale.y * squash.y));
        }

        for (int i = 0; i < carriedLights.Count; i++)
        {
            if (carriedLights[i] == null) continue;
            Vector3 scale = carriedLightScales[i];
            carriedLights[i].localScale = new Vector3(scale.x / squash.x, scale.y / squash.y, scale.z);
        }
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
        SetAnimBool(PlayerAnimParams.Crouching, IsCrouching);
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

        // Crouching: slower, softer steps.
        float stepRate = isSprinting ? baseStepRate * 0.6f : IsCrouching ? baseStepRate * 1.4f : baseStepRate;

        if (stepTimer <= 0f)
        {
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            audioSource.pitch = isSprinting ? 1.2f : IsCrouching ? 0.9f : 1f;
            audioSource.PlayOneShot(clip, IsCrouching ? crouchFootstepVolume : 1f);
            stepTimer = stepRate;
        }
    }


}
