using UnityEngine;

// Base for anything that can be picked up. Weapons and materials have their own subclasses.
[CreateAssetMenu(fileName = "NewItem", menuName = "Dillon/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
}