using UnityEngine;

// The ranged half of combat. While the gun is put away, the player faces wherever they walk.
// Clicking turns the player toward the mouse, draws the gun, and fires; the gun goes away again shortly after.
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
    public bool IsBusy => lockTimer > 0f;
    public bool IsDrawn => Time.time < drawnUntil;

    private PlayerCombat combat;
    private Camera cam;
    private float cooldownTimer;
    private float lockTimer;
    private float drawnUntil;
    private Vector2 bodyFacing = Vector2.down;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        // timeScale 0 means a menu paused the game; clicking its buttons shouldn't shoot.
        if (Weapon == null || Time.timeScale == 0f) return;

        cooldownTimer -= Time.deltaTime;
        lockTimer -= Time.deltaTime;

        bool fire = Weapon.automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (fire && cooldownTimer <= 0f)
            Fire();

        PlayerController controller = combat.Controller;
        controller.combatSpeedMultiplier = lockTimer > 0f ? 0f : 1f;

        // Face the shot only while the gun is out; otherwise face where you walk.
        if (IsDrawn)
            controller.SetFacingOverride(bodyFacing);
        else
            controller.ClearFacingOverride();
    }

    void Fire()
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

        cooldownTimer = Weapon.cooldown;
        lockTimer = Weapon.lockDuration;
        drawnUntil = Time.time + Weapon.lockDuration + HolsterDelay;

        controller.SetFacingOverride(bodyFacing);
        controller.SetAnimTrigger(PlayerAnimParams.Shoot);
        combat.Visual.SetFacing(bodyFacing);
        combat.Visual.Aim(attackDirection);
        combat.Visual.PlayRecoil(Weapon.recoilDistance, drawnUntil - Time.time);

        Vector2 attackOrigin = combat.AttackOrigin;
        Vector3 origin = new Vector3(attackOrigin.x, attackOrigin.y, transform.position.z);
        int count = Mathf.Max(1, Weapon.projectilesPerShot);
        for (int i = 0; i < count; i++)
        {
            // A single shot lands randomly inside the spread; several shots fan out evenly across it.
            float t = count == 1 ? Random.value : i / (float)(count - 1);
            float angle = (t - 0.5f) * Weapon.spreadAngle;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * attackDirection;
            Projectile.Spawn(Weapon, origin, direction, combat.Visual.MuzzleDistance, gameObject, combat.Visual.WeaponRenderer);
        }
    }

    Vector2 MouseDirection(Vector2 fallback)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return fallback;

        Vector2 toMouse = (Vector2)cam.ScreenToWorldPoint(Input.mousePosition) - combat.AttackOrigin;
        return toMouse.sqrMagnitude > 0.0001f ? toMouse.normalized : fallback;
    }

    void OnDisable()
    {
        lockTimer = 0f;
        drawnUntil = 0f;

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
