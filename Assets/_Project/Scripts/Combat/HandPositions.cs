using System.Collections.Generic;
using UnityEngine;

public enum WeaponLayer
{
    InFront,            // drawn over the player
    Behind,             // hidden behind the player's body
    BehindWhenRaised    // behind while pointing upward (over the head), in front otherwise
}

// Where the player's hand is, so a held melee weapon sits in it.
// Positions are world units from the player's center. Select the player and drag the hand circle in the
// Scene view to set them. Changes made during Play mode are kept, because this is an asset.
[CreateAssetMenu(menuName = "Sonny/Hand Positions", fileName = "PlayerHand")]
public class HandPositions : ScriptableObject
{
    [System.Serializable]
    public class DirectionHand
    {
        public Vector2 position;
        [Tooltip("Whether the weapon is drawn over or under the player when facing this way.")]
        public WeaponLayer layer;

        public DirectionHand() { }

        public DirectionHand(Vector2 position, WeaponLayer layer)
        {
            this.position = position;
            this.layer = layer;
        }
    }

    [System.Serializable]
    public class FrameHand
    {
        [Tooltip("One animation frame of the player.")]
        public Sprite frame;
        public Vector2 position;
        [Tooltip("Extra weapon tilt on this frame, in degrees, for arm swing while walking.")]
        public float angle;
    }

    [Header("Default hand spot for each way the player faces")]
    public DirectionHand facingUp = new DirectionHand(new Vector2(0.26f, -0.12f), WeaponLayer.BehindWhenRaised);
    public DirectionHand facingDown = new DirectionHand(new Vector2(-0.26f, -0.18f), WeaponLayer.BehindWhenRaised);
    public DirectionHand facingLeft = new DirectionHand(new Vector2(-0.08f, -0.2f), WeaponLayer.InFront);
    public DirectionHand facingRight = new DirectionHand(new Vector2(0.08f, -0.2f), WeaponLayer.InFront);

    [Header("Exact hand spot on specific frames, so the weapon moves with the hand while walking")]
    public List<FrameHand> frames = new List<FrameHand>();

    public DirectionHand For(Vector2 facing)
    {
        if (facing.y > 0.5f) return facingUp;
        if (facing.y < -0.5f) return facingDown;
        return facing.x < 0f ? facingLeft : facingRight;
    }

    // The frame's own spot if it has one, otherwise the default for the direction. angle is the frame's extra tilt.
    public Vector2 PositionFor(Vector2 facing, Sprite frame, out float angle)
    {
        FrameHand entry = FindFrame(frame);
        angle = entry != null ? entry.angle : 0f;
        return entry != null ? entry.position : For(facing).position;
    }

    public FrameHand FindFrame(Sprite frame)
    {
        if (frame == null) return null;
        foreach (FrameHand entry in frames)
            if (entry.frame == frame) return entry;
        return null;
    }

    public void SetFramePosition(Sprite frame, Vector2 position)
    {
        FrameHand entry = FindFrame(frame);
        if (entry == null)
        {
            entry = new FrameHand { frame = frame };
            frames.Add(entry);
        }
        entry.position = position;
    }

    public void RemoveFrame(Sprite frame)
    {
        frames.RemoveAll(entry => entry.frame == frame);
    }
}
