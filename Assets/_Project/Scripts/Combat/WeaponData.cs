using UnityEngine;

// What kind of weapon is in hand. The numbers are what the Animator's WeaponType parameter receives.
public enum WeaponType
{
    None = 0,
    Melee = 1,
    Ranged = 2
}

// Settings shared by every weapon. Make new ones with Create > Sonny > Weapons.
public abstract class WeaponData : ScriptableObject
{
    public string displayName = "Weapon";

    public abstract WeaponType Type { get; }

    [Header("Look")]
    [Tooltip("Draw the weapon as its own sprite in the player's hand. Turn off once the player's animations draw it.")]
    public bool showHeldSprite = true;
    [Tooltip("Leave empty to draw a placeholder rectangle. Weapon art should point to the right.")]
    public Sprite sprite;
    public float spriteScale = 1f;
    [Tooltip("Placeholder rectangle size in world units (length, thickness).")]
    public Vector2 placeholderSize = new Vector2(0.6f, 0.1f);
    public Color placeholderColor = new Color(0.75f, 0.75f, 0.8f);

    [Header("Damage")]
    public float damage = 3f;
    [Tooltip("How far a hit pushes the target, in world units.")]
    public float knockback = 0.3f;

    [Header("Timing")]
    [Tooltip("Seconds before this weapon can attack again.")]
    public float cooldown = 0.35f;
    [Tooltip("Seconds the player stands still, facing the attack, after attacking.")]
    public float lockDuration = 0.25f;

    public float Length => sprite != null ? sprite.bounds.size.x * spriteScale : placeholderSize.x;
}
