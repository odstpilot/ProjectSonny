using System.Collections.Generic;
using UnityEngine;

// The ranged half of combat. While the gun is put away, the player faces wherever they walk.
// Clicking turns the player toward the mouse, draws the gun, and fires; the gun goes away again shortly after.
// Blasters have to charge first: hold to build a ball of energy, and let go once it's full to fire. Letting go early fizzles.
// EMPs fire a pulse that stuns weeping angels instead of projectiles.
// Every weapon recharges on its own, even while put away, so swapping never shares or pauses a cooldown.
// PlayerCombat turns this on while a ranged weapon is in hand.
[RequireComponent(typeof(PlayerCombat))]
public class RangedCombat : MonoBehaviour
{
    // How long the gun stays out after the stand-still time ends, so quick repeated clicks don't flicker it.
    const float HolsterDelay = 0.15f;
    // While the gun is already out, a new shot only turns the body if it's this many degrees past a diagonal,
    // so rapid fire near a diagonal doesn't flip the player back and forth.
    const float TurnHysteresis = 10f;

    public RangedWeaponData Weapon { get; set; }
    public bool IsBusy => charging || lockTimer > 0f;
    public bool IsDrawn => charging || Time.time < drawnUntil;
    // How full a blaster's charge is, 0 to 1. For the HUD.
    public float ChargePercent { get; private set; }

    private PlayerCombat combat;
    private Camera cam;
    private float lockTimer;
    private float drawnUntil;
    private bool charging;
    private float chargeStartTime;
    private Vector2 bodyFacing = Vector2.down;
    // The Time.time each weapon can fire again.
    private readonly Dictionary<RangedWeaponData, float> readyTimes = new Dictionary<RangedWeaponData, float>();
    // An EMP in hand that's recharging; it crackles once it's ready.
    private EmpWeaponData recharging;

    private bool IsReady => !readyTimes.TryGetValue(Weapon, out float readyTime) || Time.time >= readyTime;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        // timeScale 0 means a menu paused the game; clicking its buttons shouldn't shoot.
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
            if (fire && IsReady)
                Fire();
        }

        PlayerController controller = combat.Controller;
        if (lockTimer > 0f)
            controller.combatSpeedMultiplier = 0f;
        else
            controller.combatSpeedMultiplier = charging && blaster != null ? blaster.chargeMoveSpeedMultiplier : 1f;

        // Face the shot only while the gun is out; otherwise face where you walk.
        if (IsDrawn)
            controller.SetFacingOverride(bodyFacing);
        else
            controller.ClearFacingOverride();
    }

    // Hold to charge, let go at full charge to fire. Letting go early fizzles and nothing comes out.
    void UpdateCharge(BlasterWeaponData blaster)
    {
        if (!charging)
        {
            if (!Input.GetMouseButton(0) || !IsReady) return;

            // Turn before charging counts as drawn, so the first turn toward the mouse isn't held back.
            AimAtMouse();
            charging = true;
            chargeStartTime = Time.time;
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

        // Keep aiming while charging, so the shot goes wherever the mouse is when it's let go.
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

        Vector2 attackOrigin = combat.AttackOrigin;
        float launchDistance = combat.Visual.LaunchDistance;
        Vector2 muzzle = attackOrigin + attackDirection * launchDistance;

        if (Weapon is EmpWeaponData emp)
        {
            EmpPulse.Fire(emp, attackOrigin, muzzle, attackDirection, transform);
            recharging = emp;
            return;
        }

        Vector3 origin = new Vector3(attackOrigin.x, attackOrigin.y, transform.position.z);
        int count = Mathf.Max(1, Weapon.projectilesPerShot);
        for (int i = 0; i < count; i++)
        {
            // A single shot lands randomly inside the spread; several shots fan out evenly across it.
            float t = count == 1 ? Random.value : i / (float)(count - 1);
            float angle = (t - 0.5f) * Weapon.spreadAngle;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * attackDirection;
            Projectile.Spawn(Weapon, origin, direction, launchDistance, gameObject, combat.Visual.WeaponRenderer);
        }

        if (Weapon is BlasterWeaponData blaster)
        {
            // A blaster shot has weight: the view jolts back from it and the muzzle lights up the room.
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

    // Turns the body toward the mouse and points the gun at it. Returns the way shots should go.
    Vector2 AimAtMouse()
    {
        PlayerController controller = combat.Controller;
        Vector2 mouseDirection = MouseDirection(controller.Facing);

        // Turn toward the shot. The gun then points within 45 degrees of the way the body faces, never behind it.
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
