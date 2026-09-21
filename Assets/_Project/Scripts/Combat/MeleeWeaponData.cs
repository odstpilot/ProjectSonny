using UnityEngine;

// Weapon angles for one facing direction, in degrees: 0 = pointing right, 90 = up, 180 = left, 270 = down.
[System.Serializable]
public class SwingAngles
{
    [Tooltip("Angle while walking or standing.")]
    public float rest;
    [Tooltip("Top of the swing, where it starts.")]
    public float raised;
    [Tooltip("Bottom of the swing, where it ends. The weapon turns from Raised toward End, so 70 to 230 sweeps counterclockwise through 180.")]
    public float end;
    [Tooltip("1 = the weapon turns in a full circle, swinging out to the side. Lower squashes the circle sideways so the " +
             "weapon shortens as it passes the middle, making the swing go toward or away from the camera. About 0.3 suits facing up or down.")]
    [Range(0.05f, 1f)] public float arcWidth = 1f;
    [Tooltip("1 = a full circle. Lower flattens it top to bottom, so a swing that passes in front of or behind the player " +
             "reads as a sweep across the floor, seen from above. About 0.55 suits facing up or down.")]
    [Range(0.05f, 1f)] public float arcHeight = 1f;

    public SwingAngles() { }

    public SwingAngles(float rest, float raised, float end, float arcWidth = 1f, float arcHeight = 1f)
    {
        this.rest = rest;
        this.raised = raised;
        this.end = end;
        this.arcWidth = arcWidth;
        this.arcHeight = arcHeight;
    }
}

[CreateAssetMenu(menuName = "Sonny/Weapons/Melee Weapon", fileName = "NewMeleeWeapon")]
public class MeleeWeaponData : WeaponData
{
    public override WeaponType Type => WeaponType.Melee;

    [Header("Holding (angles: 0 = right, 90 = up, 180 = left, 270 = down)")]
    [Tooltip("How far up from the weapon's bottom end the hand grips it, in world units.")]
    public float gripInset = 0.1f;
    // Up and down sweep side to side across the way faced: facing down, from the right across in front of the player to
    // the left; facing up, from the left across behind them to the right. arcHeight flattens the sweep so it lies along
    // the floor instead of wheeling up over the player's head.
    public SwingAngles facingUp = new SwingAngles(70f, 175f, -5f, 1f, 0.55f);
    public SwingAngles facingDown = new SwingAngles(120f, 5f, -185f, 1f, 0.55f);
    public SwingAngles facingLeft = new SwingAngles(120f, 65f, 265f);
    public SwingAngles facingRight = new SwingAngles(60f, 115f, -85f);

    [Header("Hit Area")]
    [Tooltip("Reach from the player's center, in world units.")]
    public float range = 1.1f;
    [Tooltip("Width of the hit area in degrees, centered on the way the player faces.")]
    public float arcAngle = 150f;
    public float swingDuration = 0.14f;

    [Header("Charge (hold the attack button)")]
    public bool canCharge = true;
    [Tooltip("Letting go before this many seconds counts as a quick swing.")]
    public float tapThreshold = 0.15f;
    [Tooltip("Seconds of holding, after the tap threshold, to fully charge.")]
    public float chargeTime = 0.8f;
    [Tooltip("Movement speed while charging. 0 = can't move.")]
    [Range(0f, 1f)] public float chargeMoveSpeedMultiplier = 0.4f;
    [Tooltip("How far past Raised the weapon pulls back while charging, in degrees.")]
    public float windupAngle = 40f;

    [Header("Charged Swing (released at full charge)")]
    public float chargedDamageMultiplier = 2.5f;
    public float chargedRangeMultiplier = 1.35f;
    public float chargedArcAngle = 260f;
    public float chargedKnockback = 0.8f;
    public float chargedSwingDuration = 0.22f;
    public float chargedLockDuration = 0.45f;

    public SwingAngles AnglesFor(Vector2 facing)
    {
        if (facing.y > 0.5f) return facingUp;
        if (facing.y < -0.5f) return facingDown;
        return facing.x < 0f ? facingLeft : facingRight;
    }
}
