using UnityEngine;

// A weapon as an inventory item. Points at the WeaponData asset the combat system uses.
// Use exactly ONE WeaponData asset per weapon: RangedCombat tracks cooldowns per asset.
[CreateAssetMenu(fileName = "NewWeaponItem", menuName = "Sonny/Items/Weapon Item")]
public class WeaponItemData : ItemData
{
    public WeaponData weapon;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(itemName) && weapon != null)
            itemName = weapon.displayName;
    }
}