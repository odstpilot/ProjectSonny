using UnityEngine;

// Every Animator parameter the player's code sets. Add any of these to the player's Animator Controller
// with the same name and type and it starts receiving values; ones the controller doesn't have are skipped.
public static class PlayerAnimParams
{
    // Float. The way the player faces, always one of eight directions: (0,1) up, (0,-1) down, (-1,0) left, (1,0) right,
    // or a diagonal between them, normalized (PlayerController.SnapToEightWay). Use these as the X and Y of a
    // 2D Simple Directional blend tree.
    public static readonly int FaceX = Animator.StringToHash("FaceX");
    public static readonly int FaceY = Animator.StringToHash("FaceY");

    // Float. -1 or 1: the side the player last faced, left or right. Facing straight up or down keeps the last one.
    // For animations that only come in left and right, like crouching.
    public static readonly int FaceSide = Animator.StringToHash("FaceSide");

    // Bool. True while walking.
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");

    // Bool. True while sprinting.
    public static readonly int Sprinting = Animator.StringToHash("Sprinting");

    // Int. What's in hand: 0 = nothing, 1 = melee, 2 = ranged (the WeaponType enum).
    public static readonly int WeaponType = Animator.StringToHash("WeaponType");

    // Bool. True while holding the attack button to charge a melee swing.
    public static readonly int Charging = Animator.StringToHash("Charging");

    // Bool. True while crouching (C or Ctrl).
    public static readonly int Crouching = Animator.StringToHash("Crouching");

    // Triggers. Fired the moment an attack starts.
    public static readonly int Swing = Animator.StringToHash("Swing");
    public static readonly int ChargedSwing = Animator.StringToHash("ChargedSwing");
    public static readonly int Shoot = Animator.StringToHash("Shoot");

    // Int, only used by the old PlayerCont controller: 0 = idle, 1 = up, 2 = down, 3 = left, 4 = right.
    // Delete it from the controller once idle and walk are blend trees on FaceX/FaceY.
    public static readonly int Direction = Animator.StringToHash("Direction");
}
