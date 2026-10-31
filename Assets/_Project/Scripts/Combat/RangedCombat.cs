using System.Collections.Generic;
using UnityEngine;

// aiming and shooting the held gun
[RequireComponent(typeof(PlayerCombat))]
public class RangedCombat : MonoBehaviour
{
    const float HolsterDelay = 0.15f;
    const float TurnHysteresis = 10f;

    public RangedWeaponData Weapon { get; set; }
    public bool IsBusy => charging || lockTimer > 0f;
    public bool IsDrawn => charging || Time.time < drawnUntil;
    public float ChargePercent { get; private set; }
    public event System.Action Fired;

    private PlayerCombat combat;
    private Camera cam;
    private float lockTimer;
    private float drawnUntil;
    private bool charging;
    private float chargeStartTime;
    private Vector2 bodyFacing = Vector2.down;
    private readonly Dictionary<RangedWeaponData, float> readyTimes = new Dictionary<RangedWeaponData, float>();
    private EmpWeaponData recharging;

    private bool IsReady => !readyTimes.TryGetValue(Weapon, out float readyTime) || Time.time >= readyTime;

    public float Readiness(RangedWeaponData weapon)
    {
        if (weapon == null || weapon.cooldown <= 0f || !readyTimes.TryGetValue(weapon, out float readyTime)) return 1f;
        return Mathf.Clamp01(1f - (readyTime - Time.time) / weapon.cooldown);
    }

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        if (Weapon == null || Time.timeScale == 0f) return;

        lockTimer -= Time.deltaTime;

        if (recharging != null && IsReady)
        {
            EmpPulse.PlayReady(recharging, combat.AttackOrigin);
            recharging = null;
        }

        var blaster = Weapon as BlasterWeaponData;
        if (blaster != null)
        {
            UpdateCharge(blaster);
        }
        else
        {
            bool fire = Weapon.automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (fire && IsReady && !PipLog.MouseOverIcon)
                Fire();
        }

        PlayerController controller = combat.Controller;
        if (lockTimer > 0f)
            controller.combatSpeedMultiplier = 0f;
        else
            controller.combatSpeedMultiplier = charging && blaster != null ? blaster.chargeMoveSpeedMultiplier : 1f;

        if (IsDrawn)
            controller.SetFacingOverride(bodyFacing);
        else
            controller.ClearFacingOverride();
    }

    void UpdateCharge(BlasterWeaponData blaster)
    {
        if (!charging)
        {
            if (!Input.GetMouseButton(0) || !IsReady) return;

            AimAtMouse();
            charging = true;
            chargeStartTime = Time.time;
            Sfx.At("Blaster Charge", transform.position);
        }

        ChargePercent = blaster.chargeTime > 0f ? Mathf.Clamp01((Time.time - chargeStartTime) / blaster.chargeTime) : 1f;
        bool full = ChargePercent >= 1f;

        if (!Input.GetMouseButton(0) || (full && blaster.fireWhenFull))
        {
            if (full)
                Fire();
            else
                Fizzle(blaster);
            StopCharging();
            return;
        }

        AimAtMouse();
        combat.Visual.ShowRangedCharge(ChargePercent);
    }

    void Fire()
    {
        Vector2 attackDirection = AimAtMouse();

        readyTimes[Weapon] = Time.time + Weapon.cooldown;
        lockTimer = Weapon.lockDuration;
        drawnUntil = Time.time + Weapon.lockDuration + HolsterDelay;

        combat.Controller.SetAnimTrigger(PlayerAnimParams.Shoot);
        combat.Visual.PlayRecoil(Weapon.recoilDistance, drawnUntil - Time.time);
        Fired?.Invoke();

        Vector2 attackOrigin = combat.AttackOrigin;
        float launchDistance = combat.Visual.LaunchDistance;
        Vector2 muzzle = attackOrigin + attackDirection * launchDistance;

        if (Weapon is EmpWeaponData emp)
        {
            EmpPulse.Fire(emp, attackOrigin, muzzle, attackDirection, transform);
            recharging = emp;
            return;
        }

        Sfx.At(Weapon is BlasterWeaponData ? "Blaster Fire" : "Gun Shot", muzzle);
        Vector3 origin = new Vector3(attackOrigin.x, attackOrigin.y, transform.position.z);
        int count = Mathf.Max(1, Weapon.projectilesPerShot);
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? Random.value : i / (float)(count - 1);
            float angle = (t - 0.5f) * Weapon.spreadAngle;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * attackDirection;
            Projectile.Spawn(Weapon, origin, direction, launchDistance, gameObject, combat.Visual.WeaponRenderer);
        }

        if (Weapon is BlasterWeaponData blaster)
        {
            CameraShake.Shake(blaster.fireShake);
            CameraShake.Kick(-attackDirection, blaster.recoilDistance * 0.5f);
            HitEffects.Ring(muzzle, blaster.coreColor, blaster.ballSize * 3f);
            LightFlash.Spawn(muzzle, blaster.energyColor, blaster.glowRadius * 1.5f, blaster.glowIntensity * 2f, 0.2f);
        }
    }

    void Fizzle(BlasterWeaponData blaster)
    {
        readyTimes[Weapon] = Time.time + blaster.fizzleCooldown;
        drawnUntil = Time.time + HolsterDelay;
        combat.Visual.PlayFizzle(HolsterDelay);
    }

    Vector2 AimAtMouse()
    {
        PlayerController controller = combat.Controller;
        Vector2 mouseDirection = MouseDirection(controller.Facing);

        if (!IsDrawn || Vector2.Angle(mouseDirection, bodyFacing) > 45f + TurnHysteresis)
            bodyFacing = PlayerController.SnapToFourWay(mouseDirection);

        Vector2 attackDirection;
        switch (Weapon.aimSnap)
        {
            case AimSnap.FourWay:
                attackDirection = bodyFacing;
                break;
            case AimSnap.EightWay:
                attackDirection = SnapToAngle(mouseDirection, 45f);
                break;
            default:
                attackDirection = mouseDirection;
                break;
        }

        controller.SetFacingOverride(bodyFacing);
        combat.Visual.SetFacing(bodyFacing);
        combat.Visual.Aim(attackDirection);
        return attackDirection;
    }

    Vector2 MouseDirection(Vector2 fallback)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return fallback;

        Vector2 toMouse = (Vector2)cam.ScreenToWorldPoint(Input.mousePosition) - combat.AttackOrigin;
        return toMouse.sqrMagnitude > 0.0001f ? toMouse.normalized : fallback;
    }

    void StopCharging()
    {
        charging = false;
        ChargePercent = 0f;
    }

    void OnDisable()
    {
        StopCharging();
        lockTimer = 0f;
        drawnUntil = 0f;
        recharging = null;

        if (combat == null || combat.Controller == null) return;
        combat.Controller.combatSpeedMultiplier = 1f;
        combat.Controller.ClearFacingOverride();
    }

    static Vector2 SnapToAngle(Vector2 direction, float step)
    {
        float angle = Mathf.Round(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg / step) * step * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
