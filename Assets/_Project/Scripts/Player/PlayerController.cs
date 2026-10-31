using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// player movement
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
    [Tooltip("Tap C or Ctrl to crouch and tap again to stand, instead of holding the key down. The player can also turn this on in the settings (GameSettings.ToggleCrouch).")]
    public bool crouchToggles = false;
    [Tooltip("How loud footsteps are while crouching, as a fraction of normal.")]
    [Range(0f, 1f)] public float crouchFootstepVolume = 0.35f;
    [Tooltip("How tall the sprite is while crouched, as a fraction of standing. Its feet stay planted.")]
    [Range(0.5f, 1f)] public float crouchHeight = 0.8f;
    [Tooltip("How much wider the sprite gets while crouched, so it reads as squatting rather than shrinking.")]
    [Range(1f, 1.5f)] public float crouchWidth = 1.08f;
    [Tooltip("Seconds to get down into a crouch, or back up out of one.")]
    public float crouchTransitionTime = 0.12f;

    [Header("Facing")]
    [Tooltip("Seconds to keep facing a diagonal after one of its two keys is let go. Letting go of both keys never " +
             "happens on quite the same frame, so without this the player would nearly always stop facing straight.")]
    public float diagonalReleaseGrace = 0.08f;

    [Header("Sprint Lean")]
    [Tooltip("How far the sprite tips forward while sprinting sideways, in degrees. Its feet stay planted.")]
    [Range(0f, 25f)] public float sprintLeanAngle = 8f;
    [Tooltip("How tall the sprite is while sprinting, as a fraction of standing.")]
    [Range(0.8f, 1.3f)] public float sprintHeight = 1.05f;
    [Tooltip("How wide the sprite is while sprinting, as a fraction of standing.")]
    [Range(0.7f, 1.2f)] public float sprintWidth = 0.95f;
    [Tooltip("Seconds to lean into a sprint, or straighten back up out of one.")]
    public float sprintLeanTime = 0.15f;

    [Header("Charging a Swing")]
    [Tooltip("How far the player trembles either way, in degrees, at full charge (MeleeCombat sets how charged).")]
    [Range(0f, 10f)] public float chargeShakeAngle = 4f;
    [System.NonSerialized] public float chargeShake;
    const float TrembleInterval = 0.035f;
    private float nextTremble, trembleAngle, trembleSquash = 1f;

    public Rigidbody2D rb;
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("Footstep Settings")]
    [Tooltip("The scene's sound for a footstep.")]
    [SoundName] public string footstepSound = "Footsteps";
    public float baseStepRate = 0.4f;
    private float stepTimer;
    [SerializeField] private AudioSource audioSource;
    private Vector2 movement;
    private float currentSpeed;
    private Health health;

    [System.NonSerialized] public float combatSpeedMultiplier = 1f;
    [System.NonSerialized] public WeaponType heldWeaponType = WeaponType.None;

    public Vector2 Facing { get; private set; } = Vector2.down;
    public Vector2 FacingEightWay { get; private set; } = Vector2.down;
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public Vector2 BodySquash => squash;
    public float LeanAngle => lean;
    public Vector2 PoseOffset => poseOffset;

    private float crouchAmount;
    private float sprintAmount;
    private float leanDirection;
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
    [System.NonSerialized] public bool sideFacingOnly;
    public Vector2 SideFacing => new Vector2(faceSide, 0f);
    private Vector2 pendingFacing;
    private float pendingFacingTime;
    private float faceSide = 1f;
    private static readonly Vector2[] eightWay =
    {
        Vector2.right, new Vector2(1f, 1f).normalized, Vector2.up, new Vector2(-1f, 1f).normalized,
        Vector2.left, new Vector2(-1f, -1f).normalized, Vector2.down, new Vector2(1f, -1f).normalized
    };
    private int frozenDirection;
    private bool scripted;
    private Vector2 scriptedMove;
    private bool scriptedSprint;
    private HashSet<int> animatorParameters;
    private bool hasCrouchArt, hasSprintArt;
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
        hasCrouchArt = animator != null && animator.HasState(0, Animator.StringToHash("Crouch"));
        hasSprintArt = animator != null && animator.HasState(0, Animator.StringToHash("Sprint"));
        DepthSort.Group(gameObject);
    }

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

    public bool IsScripted => scripted;

    void Update()
    {
        if (PauseMenu.IsPaused)
        {
            movement = Vector2.zero;
            return;
        }

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
        currentSpeed *= RadiationLeak.SpeedFactor;

        UpdateFacing(isMoving);
        Facing = hasFacingOverride ? facingOverride : SnapToFourWay(FacingEightWay);

        UpdateAnimator(isMoving);

        HandleFootsteps(isMoving, isSprinting);
    }

    void FixedUpdate()
    {
        rb.linearVelocity = movement * currentSpeed;
    }

    void UpdateCrouch()
    {
        if (crouchToggles || GameSettings.ToggleCrouch)
        {
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
                IsCrouching = !IsCrouching;
        }
        else
        {
            IsCrouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }
    }

    void OnDisable()
    {
        IsCrouching = false;
        IsSprinting = false;
        SetAnimBool(PlayerAnimParams.Crouching, false);
        SetAnimBool(PlayerAnimParams.Sprinting, false);
        crouchAmount = 0f;
        sprintAmount = 0f;
        leanDirection = 0f;
        ApplyPose(Vector2.one, 0f);
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

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

        foreach (Transform child in transform)
        {
            if (!child.TryGetComponent(out Light2D _)) continue;
            carriedLights.Add(child);
            carriedLightScales.Add(child.localScale);
        }
    }

    void UpdatePose()
    {
        float crouchStep = crouchTransitionTime > 0f ? Time.deltaTime / crouchTransitionTime : 1f;
        float sprintStep = sprintLeanTime > 0f ? Time.deltaTime / sprintLeanTime : 1f;
        crouchAmount = Mathf.MoveTowards(crouchAmount, IsCrouching && !hasCrouchArt ? 1f : 0f, crouchStep);
        sprintAmount = Mathf.MoveTowards(sprintAmount, IsSprinting && !hasSprintArt ? 1f : 0f, sprintStep);

        if (IsSprinting) leanDirection = Mathf.MoveTowards(leanDirection, movement.x, sprintStep * 2f);

        float crouch = Mathf.SmoothStep(0f, 1f, crouchAmount);
        float sprint = Mathf.SmoothStep(0f, 1f, sprintAmount);
        Vector2 crouchSquash = Vector2.Lerp(Vector2.one, new Vector2(crouchWidth, crouchHeight), crouch);
        Vector2 sprintStretch = Vector2.Lerp(Vector2.one, new Vector2(sprintWidth, sprintHeight), sprint);

        Vector2 strain = Vector2.one;
        if (chargeShake > 0f)
        {
            if (Time.time >= nextTremble)
            {
                nextTremble = Time.time + TrembleInterval;
                trembleAngle = Random.Range(-1f, 1f) * chargeShakeAngle * chargeShake;
                trembleSquash = 1f - Random.Range(0f, 0.035f) * chargeShake;
            }
            strain = new Vector2(2f - trembleSquash, trembleSquash);
        }
        else
        {
            trembleAngle = 0f;
        }

        ApplyPose(Vector2.Scale(Vector2.Scale(crouchSquash, sprintStretch), strain), -leanDirection * sprintLeanAngle * sprint + trembleAngle);
    }

    void ApplyPose(Vector2 newSquash, float newLean)
    {
        if (newSquash == squash && Mathf.Approximately(newLean, lean)) return;
        squash = newSquash;
        lean = newLean;

        Quaternion tip = Quaternion.Euler(0f, 0f, lean);
        transform.localScale = new Vector3(baseScale.x * squash.x, baseScale.y * squash.y, baseScale.z);
        transform.localRotation = tip;

        Vector2 standingFeet = new Vector2(0f, -spriteHalfHeight * baseScale.y);
        Vector2 posedFeet = tip * new Vector2(0f, -spriteHalfHeight * baseScale.y * squash.y);
        Vector2 offset = standingFeet - posedFeet;
        Vector2 change = offset - poseOffset;
        poseOffset = offset;

        if (rb != null)
        {
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

    public static Vector2 SnapToEightWay(Vector2 direction)
    {
        if (direction == Vector2.zero) return Vector2.down;
        int octant = Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI / 4f));
        return eightWay[(octant + 8) % 8];
    }

    void UpdateFacing(bool isMoving)
    {
        Vector2 target;
        if (hasFacingOverride) target = facingOverride;
        else if (isMoving) target = SnapToEightWay(movement);
        else target = FacingEightWay;
        if (sideFacingOnly && !hasFacingOverride)
            target = target.x != 0f ? new Vector2(Mathf.Sign(target.x), 0f) : SideFacing;

        bool easingOffDiagonal = !hasFacingOverride && FacingEightWay.x != 0f && FacingEightWay.y != 0f &&
                                 Vector2.Dot(target, FacingEightWay) > 0.5f && (target.x == 0f || target.y == 0f);
        if (target == FacingEightWay || !easingOffDiagonal)
        {
            FacingEightWay = target;
            pendingFacingTime = 0f;
        }
        else
        {
            if (target != pendingFacing) pendingFacingTime = 0f;
            pendingFacing = target;
            pendingFacingTime += Time.deltaTime;
            if (pendingFacingTime >= diagonalReleaseGrace)
            {
                FacingEightWay = target;
                pendingFacingTime = 0f;
            }
        }

        if (FacingEightWay.x != 0f) faceSide = Mathf.Sign(FacingEightWay.x);
    }

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
        if (CanSetAnimParameter(PlayerAnimParams.FaceX)) animator.SetFloat(PlayerAnimParams.FaceX, FacingEightWay.x);
        if (CanSetAnimParameter(PlayerAnimParams.FaceY)) animator.SetFloat(PlayerAnimParams.FaceY, FacingEightWay.y);
        if (CanSetAnimParameter(PlayerAnimParams.FaceSide)) animator.SetFloat(PlayerAnimParams.FaceSide, faceSide);
        SetAnimBool(PlayerAnimParams.IsMoving, isMoving);
        SetAnimBool(PlayerAnimParams.Sprinting, IsSprinting);
        SetAnimBool(PlayerAnimParams.Crouching, IsCrouching);
        if (CanSetAnimParameter(PlayerAnimParams.WeaponType)) animator.SetInteger(PlayerAnimParams.WeaponType, (int)heldWeaponType);

        if (!CanSetAnimParameter(PlayerAnimParams.Direction)) return;

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

    private void CaughtBy(GameObject other)
    {
        if (!other.CompareTag("Enemy")) return;

        if (health != null) health.Kill(other);
        if (other.TryGetComponent<Warden>(out var warden))
        {
        }
    }

    void HandleFootsteps(bool isMoving, bool isSprinting)
    {
        if (!isMoving || string.IsNullOrEmpty(footstepSound) || audioSource == null) return;

        stepTimer -= Time.deltaTime;

        float stepRate = isSprinting ? baseStepRate * 0.6f : IsCrouching ? baseStepRate * 1.4f : baseStepRate;

        if (stepTimer <= 0f)
        {
            SoundManager.PlayOneShot(audioSource, footstepSound, IsCrouching ? crouchFootstepVolume : 1f,
                isSprinting ? 1.2f : IsCrouching ? 0.9f : 1f);
            stepTimer = stepRate;
        }
    }
}
