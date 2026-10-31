using UnityEngine;

// what the player is holding
[RequireComponent(typeof(PlayerController), typeof(MeleeCombat), typeof(RangedCombat))]
public class PlayerCombat : MonoBehaviour
{
    [Tooltip("In hand when the scene starts. Leave empty to start unarmed.")]
    public WeaponData startingWeapon;

    [Tooltip("Where attacks come from, relative to the player's pivot, in world units.")]
    public Vector2 aimOffset = new Vector2(0f, -0.1f);
    [Tooltip("Where the player's hand is for each direction and frame, so melee weapons sit in it.")]
    public HandPositions hand;

    public event System.Action<WeaponData> WeaponChanged;

    public WeaponData CurrentWeapon { get; private set; }
    public bool IsArmed => CurrentWeapon != null;
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
        if (!hasEquipped)
            Equip(startingWeapon);
    }

    public void Equip(WeaponData weapon)
    {
        Initialize();
        if (hasEquipped && weapon == CurrentWeapon) return;

        hasEquipped = true;
        CurrentWeapon = weapon;

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

    void Initialize()
    {
        if (Controller != null) return;

        Controller = GetComponent<PlayerController>();
        melee = GetComponent<MeleeCombat>();
        ranged = GetComponent<RangedCombat>();
        Visual = WeaponVisual.Create(transform, aimOffset, GetComponent<SpriteRenderer>(), hand);
    }
}
