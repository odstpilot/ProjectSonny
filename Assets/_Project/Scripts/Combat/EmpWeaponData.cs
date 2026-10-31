using UnityEngine;

// gun that fires an emp instead of bullets
[CreateAssetMenu(menuName = "Sonny/Weapons/EMP", fileName = "NewEmp")]
public class EmpWeaponData : RangedWeaponData
{
    [Header("Pulse")]
    [Tooltip("Layers the pulse can stun (Angel).")]
    public LayerMask stunLayers;
    [Tooltip("How far the pulse reaches, in world units.")]
    public float stunRadius = 5f;
    [Tooltip("How wide the pulse is, in degrees, centered on the aim.")]
    [Range(10f, 360f)] public float coneAngle = 95f;
    [Tooltip("Seconds it shorts out a patrol robot it reaches (PlaceholderRobot.Emp).")]
    public float robotShortSeconds = 4f;
    [Tooltip("Seconds it shorts out a security camera it reaches (StationCamera.Shutdown).")]
    public float cameraShortSeconds = 6f;

    [Header("EMP Look")]
    [Tooltip("Color of the light, the wave's glow, and sparks.")]
    public Color empColor = new Color(0.3f, 0.8f, 1f);
    [Tooltip("Color of the bright middle of each band of the wave.")]
    public Color empCoreColor = new Color(0.85f, 0.97f, 1f);
    [Tooltip("How bright the cone of light is.")]
    public float lightIntensity = 2.2f;
    [Tooltip("How far the light reaches, in world units, and how wide it spreads, in degrees. Only the light: what the pulse stuns is still stunRadius and coneAngle.")]
    public float lightRadius = 13f;
    [Range(10f, 360f)] public float lightAngle = 170f;
    [Tooltip("Seconds the light crackles at full strength.")]
    public float lightDuration = 0.2f;
    [Tooltip("Seconds the light takes to sputter out after that.")]
    public float lightFadeDuration = 0.4f;
    [Tooltip("Bands of electricity that sweep out per pulse.")]
    [Range(1, 6)] public int waveCount = 3;
    [Tooltip("Seconds for a band to reach the edge of the stun radius.")]
    public float waveDuration = 0.35f;
    [Tooltip("Screen shake when it fires, 0 to 1.")]
    [Range(0f, 1f)] public float pulseShake = 0.2f;

    [Header("Sound")]
    public AudioClip fireClip;
    [Tooltip("Plays when it has recharged, if it's in hand. Optional.")]
    public AudioClip readyClip;
    [Range(0f, 1f)] public float volume = 0.2f;
}
