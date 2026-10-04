using UnityEngine;

// Scrap parts, ammo, and other stackable things that live in the backpack instead of the hotbar.
[CreateAssetMenu(fileName = "NewMaterial", menuName = "Sonny/Items/Material Item")]
public class MaterialItemData : ItemData
{
    public int maxStack = 99;
}