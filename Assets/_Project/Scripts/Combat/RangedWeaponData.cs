using UnityEngine;

public enum AimSnap
{
    Free,       // shoots exactly at the mouse
    EightWay,   // straight or diagonal
    FourWay     // straight ahead only; matches art where the gun is drawn pointing the way the player faces
}

[CreateAssetMenu(menuName = "Sonny/Weapons/Ranged Weapon", fileName = "NewRangedWeapon")]
public class RangedWeaponData : WeaponData
{
    public override WeaponType Type => WeaponType.Ranged;

    [Header("Aim")]
    [Tooltip("The player always faces the mouse while holding this. This limits which angles the gun itself can point.")]
    public AimSnap aimSnap = AimSnap.Free;
    [Tooltip("How far from the player's center the gun is held, in world units.")]
    public float holdDistance = 0.3f;

    [Header("Firing")]
    [Tooltip("Hold the button to keep firing instead of clicking for each shot.")]
    public bool automatic = false;
    [Tooltip("More than 1 fans the shots out across the spread, like a shotgun.")]
    public int projectilesPerShot = 1;
    [Tooltip("Width of the cone shots can go in, in degrees.")]
    public float spreadAngle = 0f;
    public float recoilDistance = 0.1f;

    [Header("Projectile")]
    [Tooltip("Leave empty to draw a placeholder rectangle. Art should point to the right.")]
    public Sprite projectileSprite;
    public Vector2 projectileSize = new Vector2(0.22f, 0.07f);
    public Color projectileColor = new Color(1f, 0.85f, 0.4f);
    public float projectileSpeed = 14f;
    public float projectileRange = 9f;
    [Tooltip("Collision radius. Bigger is more forgiving.")]
    public float projectileRadius = 0.08f;
}
