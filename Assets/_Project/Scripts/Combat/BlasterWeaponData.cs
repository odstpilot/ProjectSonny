using UnityEngine;

// A ranged weapon that has to charge before it fires. Hold the attack button to build a ball of energy at the muzzle,
// and let go once it's full to launch it. Letting go early fizzles and nothing comes out. The ball blows up on
// whatever it hits, hurting everything around the impact.
[CreateAssetMenu(menuName = "Sonny/Weapons/Blaster", fileName = "NewBlaster")]
public class BlasterWeaponData : RangedWeaponData
{
    [Header("Charge (hold the attack button, let go when full)")]
    [Tooltip("Seconds of holding to fully charge. Letting go before then fizzles.")]
    public float chargeTime = 1.2f;
    [Tooltip("Movement speed while charging. 0 = can't move.")]
    [Range(0f, 1f)] public float chargeMoveSpeedMultiplier = 0.35f;
    [Tooltip("Seconds before charging can start again after a fizzle.")]
    public float fizzleCooldown = 0.35f;
    [Tooltip("Fire the moment the charge is full instead of waiting for the button to be let go.")]
    public bool fireWhenFull = false;

    [Header("Energy Ball")]
    [Tooltip("Color of the glow, the light it casts, and its sparks.")]
    public Color energyColor = new Color(0.35f, 0.9f, 1f);
    [Tooltip("Color of the bright middle.")]
    public Color coreColor = new Color(0.9f, 1f, 1f);
    [Tooltip("Size of the ball once fully charged, in world units. The glow spreads out past it.")]
    public float ballSize = 0.55f;
    [Tooltip("How far the ball lights up the room around it, in world units. 0 = no light.")]
    public float glowRadius = 2.5f;
    public float glowIntensity = 1.2f;
    [Tooltip("Screen shake when it fires, 0 to 1.")]
    [Range(0f, 1f)] public float fireShake = 0.25f;

    [Header("Blast (where the ball lands)")]
    [Tooltip("Everything within this many world units of the impact is hurt. 0 = only what the ball touches.")]
    public float blastRadius = 1.4f;
    [Tooltip("Damage at the edge of the blast, as a fraction of Damage. Whatever the ball touches takes full damage.")]
    [Range(0f, 1f)] public float blastEdgeDamage = 0.4f;
    [Tooltip("Layers that shield targets from the blast (walls).")]
    public LayerMask blastObstacleMask = ~0;
    [Range(0f, 1f)] public float blastShake = 0.45f;
    [Tooltip("Freeze when the blast hurts something, in seconds.")]
    public float blastHitStop = 0.06f;
}
