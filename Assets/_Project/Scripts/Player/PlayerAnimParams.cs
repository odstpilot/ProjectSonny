using UnityEngine;

// names of the player animator's parameters
public static class PlayerAnimParams
{
    public static readonly int FaceX = Animator.StringToHash("FaceX");
    public static readonly int FaceY = Animator.StringToHash("FaceY");

    public static readonly int FaceSide = Animator.StringToHash("FaceSide");

    public static readonly int IsMoving = Animator.StringToHash("IsMoving");

    public static readonly int Sprinting = Animator.StringToHash("Sprinting");

    public static readonly int WeaponType = Animator.StringToHash("WeaponType");

    public static readonly int Charging = Animator.StringToHash("Charging");

    public static readonly int Crouching = Animator.StringToHash("Crouching");

    public static readonly int Swing = Animator.StringToHash("Swing");
    public static readonly int ChargedSwing = Animator.StringToHash("ChargedSwing");
    public static readonly int Shoot = Animator.StringToHash("Shoot");

    public static readonly int Direction = Animator.StringToHash("Direction");
}
