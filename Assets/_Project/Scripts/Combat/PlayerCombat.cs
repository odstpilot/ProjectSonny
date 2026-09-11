using UnityEngine;

// What the player has in hand. Equip a weapon, or nothing, and the matching combat script takes over:
// MeleeCombat for melee, RangedCombat for ranged, neither when unarmed. The held weapon's type is sent to the
// Animator as WeaponType (0 when unarmed). This doesn't decide which weapons the player owns; the inventory does
// that (WeaponHotbar stands in for it for now) and calls Equip / Unequip.
[RequireComponent(typeof(PlayerController), typeof(MeleeCombat), typeof(RangedCombat))]
public class PlayerCombat : MonoBehaviour
{
    [Tooltip("In hand when the scene starts. Leave empty to start unarmed.")]
    public WeaponData startingWeapon;

    [Tooltip("Where attacks come from, relative to the player's pivot, in world units.")]
    public Vector2 aimOffset = new Vector2(0f, -0.1f);
    [Tooltip("Where the player's hand is for each direction and frame, so melee weapons sit in it.")]
    public HandPositions hand;

    // Fires whenever the weapon in hand changes, including to null when unequipped. For the inventory and HUD.
    public event System.Action<WeaponData> WeaponChanged;

    public WeaponData CurrentWeapon { get; private set; }
    public bool IsArmed => CurrentWeapon != null;
    // True mid-swing, mid-charge, or mid-shot. Equipping still works then, but cancels the attack.
    public bool IsBusy => melee.IsBusy || ranged.IsBusy;

    public PlayerController Controller { get; private set; }
    public WeaponVisual Visual { get; private set; }
    public Vector2 AttackOrigin => (Vector2)transform.position + aimOffset;

    private MeleeCombat melee;
    private RangedCombat ranged;
    private bool hasEquipped;

    void Awake()
    {
        Initialize();
    }

    void Start()
    {
        // Another script, like the inventory, may have equipped something already.
        if (!hasEquipped)
            Equip(startingWeapon);
    }

    // Puts a weapon in hand, replacing whatever was there. Pass null to unequip.
    public void Equip(WeaponData weapon)
    {
        Initialize();
        if (hasEquipped && weapon == CurrentWeapon) return;

        hasEquipped = true;
        CurrentWeapon = weapon;

        // Turn both off first so the old one releases the player (movement, facing, a half-finished charge)
        // before the new one takes over.
        melee.enabled = false;
        ranged.enabled = false;
        melee.Weapon = weapon as MeleeWeaponData;
        ranged.Weapon = weapon as RangedWeaponData;
        melee.enabled = melee.Weapon != null;
        ranged.enabled = ranged.Weapon != null;

        Visual.Equip(weapon);
        Controller.heldWeaponType = weapon != null ? weapon.Type : WeaponType.None;
        WeaponChanged?.Invoke(weapon);
    }

    public void Unequip()
    {
        Equip(null);
    }

    // Runs from Awake, or earlier if another script calls Equip before this object has woken up.
    void Initialize()
    {
        if (Controller != null) return;

        Controller = GetComponent<PlayerController>();
        melee = GetComponent<MeleeCombat>();
        ranged = GetComponent<RangedCombat>();
        Visual = WeaponVisual.Create(transform, aimOffset, GetComponent<SpriteRenderer>(), hand);
    }
}
