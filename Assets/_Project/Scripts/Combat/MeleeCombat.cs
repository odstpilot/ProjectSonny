using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The melee half of combat. Swings toward the way the player is facing, never the mouse.
// Click to swing; hold to charge a heavier swing. PlayerCombat turns this on while a melee weapon is in hand.
[RequireComponent(typeof(PlayerCombat))]
public class MeleeCombat : MonoBehaviour
{
    [Tooltip("Layers that stop a swing from reaching a target behind them (walls).")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("Tiny freeze when a hit lands, so hits feel heavy. 0 turns it off.")]
    public float hitStopDuration = 0.05f;
    [Tooltip("Screen shake when a swing lands, 0 to 1. Charged swings shake twice as hard.")]
    [Range(0f, 1f)] public float hitShake = 0.25f;

    public MeleeWeaponData Weapon { get; set; }
    public bool IsBusy => charging || lockTimer > 0f;
    public float ChargePercent { get; private set; }

    private PlayerCombat combat;
    private bool charging;
    private bool chargeVisible;
    private float chargeStartTime;
    private float cooldownTimer;
    private float lockTimer;
    private Vector2 attackDirection = Vector2.down;
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        // timeScale 0 means a menu paused the game; clicking its buttons shouldn't attack.
        if (Weapon == null || Time.timeScale == 0f) return;

        cooldownTimer -= Time.deltaTime;
        lockTimer -= Time.deltaTime;

        if (!charging)
        {
            if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0f)
            {
                if (Weapon.canCharge)
                {
                    charging = true;
                    chargeStartTime = Time.time;
                }
                else
                {
                    StartSwing(false);
                }
            }
        }
        else
        {
            float held = Time.time - chargeStartTime;
            ChargePercent = Weapon.chargeTime > 0f ? Mathf.Clamp01((held - Weapon.tapThreshold) / Weapon.chargeTime) : 1f;
            chargeVisible = held >= Weapon.tapThreshold;

            if (!Input.GetMouseButton(0))
            {
                bool fullyCharged = ChargePercent >= 1f;
                StopCharging();
                StartSwing(fullyCharged);
            }
        }

        UpdatePose();
    }

    void UpdatePose()
    {
        PlayerController controller = combat.Controller;

        if (lockTimer > 0f)
        {
            // Mid-swing: stand still and keep facing the swing.
            controller.combatSpeedMultiplier = 0f;
            controller.SetFacingOverride(attackDirection);
        }
        else
        {
            // Otherwise the player turns by walking, even while charging, and the swing goes that way.
            controller.combatSpeedMultiplier = chargeVisible ? Weapon.chargeMoveSpeedMultiplier : 1f;
            controller.ClearFacingOverride();
        }

        controller.SetAnimBool(PlayerAnimParams.Charging, chargeVisible);
        combat.Visual.SetFacing(lockTimer > 0f ? attackDirection : controller.Facing);
        if (chargeVisible)
            combat.Visual.ShowCharge(ChargePercent, Weapon.windupAngle);
    }

    void StartSwing(bool charged)
    {
        float duration = charged ? Weapon.chargedSwingDuration : Weapon.swingDuration;

        attackDirection = combat.Controller.Facing;
        cooldownTimer = Mathf.Max(Weapon.cooldown, duration);
        lockTimer = charged ? Weapon.chargedLockDuration : Weapon.lockDuration;

        combat.Controller.SetAnimTrigger(charged ? PlayerAnimParams.ChargedSwing : PlayerAnimParams.Swing);
        combat.Visual.SetFacing(attackDirection);
        combat.Visual.PlaySwing(duration, charged);

        // Land the hit partway through the swing, when the weapon is crossing in front of the player.
        StartCoroutine(DealDamage(Weapon, charged, attackDirection, duration * 0.4f));
    }

    IEnumerator DealDamage(MeleeWeaponData weapon, bool charged, Vector2 direction, float delay)
    {
        yield return new WaitForSeconds(delay);

        float range = weapon.range * (charged ? weapon.chargedRangeMultiplier : 1f);
        float halfArc = (charged ? weapon.chargedArcAngle : weapon.arcAngle) * 0.5f;
        float damage = weapon.damage * (charged ? weapon.chargedDamageMultiplier : 1f);
        float knockback = charged ? weapon.chargedKnockback : weapon.knockback;

        Vector2 origin = combat.AttackOrigin;
        bool landedHit = false;
        hitThisSwing.Clear();

        foreach (Collider2D col in Physics2D.OverlapCircleAll(origin, range))
        {
            if (IsOwnCollider(col)) continue;

            IDamageable target = Damageable.FromCollider(col);
            if (target == null || hitThisSwing.Contains(target)) continue;

            // Aim at the nearest edge so big targets that stick into the arc still get hit.
            Vector2 closest = col.ClosestPoint(origin);
            Vector2 toTarget = closest - origin;
            bool overlapping = toTarget.sqrMagnitude < 0.0001f;
            if (!overlapping && Vector2.Angle(direction, toTarget) > halfArc) continue;
            if (!overlapping && IsBlocked(origin, closest, col)) continue;

            hitThisSwing.Add(target);
            Vector2 pushDirection = (Vector2)col.bounds.center - origin;
            if (pushDirection.sqrMagnitude < 0.0001f) pushDirection = direction;
            Vector2 hitPoint = overlapping ? (Vector2)col.bounds.center : closest;
            target.TakeDamage(new DamageInfo(damage, pushDirection.normalized, knockback, gameObject, hitPoint));
            landedHit = true;
        }

        if (!landedHit) yield break;

        CameraShake.Shake(charged ? hitShake * 2f : hitShake);
        HitStop.Freeze(hitStopDuration);
    }

    bool IsOwnCollider(Collider2D col)
    {
        return col.transform.IsChildOf(transform);
    }

    bool IsBlocked(Vector2 from, Vector2 to, Collider2D target)
    {
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(from, to, obstacleMask))
        {
            if (hit.collider == target) return false;
            if (hit.collider.isTrigger || IsOwnCollider(hit.collider)) continue;
            if (Damageable.FromCollider(hit.collider) != null) continue; // other enemies don't shield each other
            return true;
        }
        return false;
    }

    void StopCharging()
    {
        charging = false;
        chargeVisible = false;
        ChargePercent = 0f;
    }

    void OnDisable()
    {
        StopCharging();
        lockTimer = 0f;
        StopAllCoroutines();

        if (combat == null || combat.Controller == null) return;
        combat.Controller.combatSpeedMultiplier = 1f;
        combat.Controller.ClearFacingOverride();
        combat.Controller.SetAnimBool(PlayerAnimParams.Charging, false);
    }

    // Select the player in the Scene view to see reach: yellow = quick swing, orange = charged.
    void OnDrawGizmosSelected()
    {
        var playerCombat = GetComponent<PlayerCombat>();
        if (playerCombat == null) return;

        // Outside Play mode nothing is in hand, so preview the starting weapon or the hotbar's first melee weapon.
        MeleeWeaponData weapon = Application.isPlaying ? Weapon : playerCombat.startingWeapon as MeleeWeaponData;
        var hotbar = GetComponent<WeaponHotbar>();
        if (weapon == null && !Application.isPlaying && hotbar != null)
        {
            foreach (WeaponData candidate in hotbar.slots)
            {
                if (candidate is MeleeWeaponData meleeWeapon)
                {
                    weapon = meleeWeapon;
                    break;
                }
            }
        }
        if (weapon == null) return;

        Vector2 direction = Application.isPlaying && playerCombat.Controller != null ? playerCombat.Controller.Facing : Vector2.right;
        DrawArcGizmo(playerCombat.AttackOrigin, weapon.range, weapon.arcAngle, direction, Color.yellow);
        if (weapon.canCharge)
            DrawArcGizmo(playerCombat.AttackOrigin, weapon.range * weapon.chargedRangeMultiplier, weapon.chargedArcAngle, direction, new Color(1f, 0.5f, 0f));
    }

    static void DrawArcGizmo(Vector2 center, float radius, float arcAngle, Vector2 direction, Color color)
    {
        const int segments = 24;
        Gizmos.color = color;
        Vector3 origin = center;
        Vector3 previous = origin;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-arcAngle, arcAngle, i / (float)segments) * 0.5f;
            Vector3 point = origin + Quaternion.Euler(0f, 0f, angle) * direction * radius;
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
        Gizmos.DrawLine(previous, origin);
    }
}
