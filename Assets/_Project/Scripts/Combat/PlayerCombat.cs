using System.Collections.Generic;
using UnityEngine;

// The player's weapons. One is in hand at a time; its type decides whether MeleeCombat or RangedCombat
// runs, and is sent to the Animator as WeaponType so the right holding animations play.
[RequireComponent(typeof(PlayerController), typeof(MeleeCombat), typeof(RangedCombat))]
public class PlayerCombat : MonoBehaviour
{
    [Tooltip("Number keys 1-9 or the scroll wheel switch between these.")]
    public List<WeaponData> weapons = new List<WeaponData>();
    public int startingWeaponIndex = 0;

    [Tooltip("Where attacks come from, relative to the player's pivot, in world units.")]
    public Vector2 aimOffset = new Vector2(0f, -0.1f);
    [Tooltip("Where the player's hand is for each direction and frame, so melee weapons sit in it.")]
    public HandPositions hand;

    public WeaponData CurrentWeapon { get; private set; }
    public PlayerController Controller { get; private set; }
    public WeaponVisual Visual { get; private set; }
    public Vector2 AttackOrigin => (Vector2)transform.position + aimOffset;

    private MeleeCombat melee;
    private RangedCombat ranged;
    private int currentIndex;

    void Awake()
    {
        Controller = GetComponent<PlayerController>();
        melee = GetComponent<MeleeCombat>();
        ranged = GetComponent<RangedCombat>();
        Visual = WeaponVisual.Create(transform, aimOffset, GetComponent<SpriteRenderer>(), hand);
    }

    void Start()
    {
        if (weapons.Count > 0)
            Equip(Mathf.Clamp(startingWeaponIndex, 0, weapons.Count - 1));
        else
            SetWeaponInHand(null);
    }

    void Update()
    {
        // No switching while paused or mid-attack.
        if (Time.timeScale == 0f || melee.IsBusy || ranged.IsBusy || weapons.Count < 2) return;

        for (int i = 0; i < weapons.Count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                Equip(i);
                return;
            }
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) Equip((currentIndex + 1) % weapons.Count);
        else if (scroll < 0f) Equip((currentIndex - 1 + weapons.Count) % weapons.Count);
    }

    public void Equip(int index)
    {
        if (index < 0 || index >= weapons.Count) return;

        currentIndex = index;
        SetWeaponInHand(weapons[index]);
    }

    // For pickups and crafting: adds the weapon if it's new, then switches to it.
    public void AddWeapon(WeaponData weapon)
    {
        if (weapon == null) return;
        if (!weapons.Contains(weapon)) weapons.Add(weapon);
        Equip(weapons.IndexOf(weapon));
    }

    void SetWeaponInHand(WeaponData weapon)
    {
        CurrentWeapon = weapon;

        // Turn both off first so the old one releases the player (movement, facing) before the new one takes over.
        melee.enabled = false;
        ranged.enabled = false;
        melee.Weapon = weapon as MeleeWeaponData;
        ranged.Weapon = weapon as RangedWeaponData;
        melee.enabled = melee.Weapon != null;
        ranged.enabled = ranged.Weapon != null;

        Visual.Equip(weapon);
        Controller.heldWeaponType = weapon != null ? weapon.Type : WeaponType.None;
    }
}
