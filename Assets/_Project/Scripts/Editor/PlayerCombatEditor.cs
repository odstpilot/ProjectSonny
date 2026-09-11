using UnityEditor;
using UnityEngine;

// Adds a draggable hand marker to the Scene view when the player is selected,
// so melee weapon placement is set by eye instead of by typing numbers.
[CustomEditor(typeof(PlayerCombat))]
public class PlayerCombatEditor : Editor
{
    static readonly string[] DirectionNames = { "Up", "Down", "Left", "Right" };
    static readonly Vector2[] DirectionVectors = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    static int editDirection = 1;

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var combat = (PlayerCombat)target;
        HandPositions hand = combat.hand;
        if (hand == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hand Position", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag the circle in the Scene view onto the player's hand.\n" +
            "Yellow: moves the default spot for the direction the player faces.\n" +
            "Cyan: moves the spot for only the animation frame on screen right now.",
            MessageType.Info);

        Vector2 facing = CurrentFacing(combat);
        if (Application.isPlaying)
            EditorGUILayout.LabelField("Facing", DirectionName(facing));
        else
            editDirection = GUILayout.Toolbar(editDirection, DirectionNames);

        Sprite frame = CurrentFrame(combat);
        if (frame == null) return;

        EditorGUILayout.LabelField("Frame on screen", frame.name);
        if (hand.FindFrame(frame) == null)
        {
            if (GUILayout.Button("Give this frame its own hand spot"))
            {
                Undo.RecordObject(hand, "Add Frame Hand Spot");
                hand.SetFramePosition(frame, hand.For(facing).position);
                EditorUtility.SetDirty(hand);
            }
        }
        else if (GUILayout.Button("Remove this frame's own hand spot"))
        {
            Undo.RecordObject(hand, "Remove Frame Hand Spot");
            hand.RemoveFrame(frame);
            EditorUtility.SetDirty(hand);
        }
    }

    void OnSceneGUI()
    {
        var combat = (PlayerCombat)target;
        HandPositions hand = combat.hand;
        if (hand == null) return;

        Vector2 facing = CurrentFacing(combat);
        Sprite frame = CurrentFrame(combat);
        bool frameSpot = hand.FindFrame(frame) != null;

        Vector3 center = combat.transform.position;
        Vector3 handPosition = center + (Vector3)hand.PositionFor(facing, frame, out _);
        float size = HandleUtility.GetHandleSize(handPosition) * 0.08f;

        Handles.color = frameSpot ? Color.cyan : Color.yellow;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(handPosition, size, Vector3.zero, Handles.CircleHandleCap);
        Handles.Label(handPosition + Vector3.up * size * 3f, frameSpot ? $"Hand: {frame.name}" : $"Hand: facing {DirectionName(facing)}");

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(hand, "Move Hand Spot");
            Vector2 position = moved - center;
            if (frameSpot)
                hand.SetFramePosition(frame, position);
            else
                hand.For(facing).position = position;
            EditorUtility.SetDirty(hand);
        }
    }

    static Vector2 CurrentFacing(PlayerCombat combat)
    {
        return Application.isPlaying && combat.Controller != null ? combat.Controller.Facing : DirectionVectors[editDirection];
    }

    static Sprite CurrentFrame(PlayerCombat combat)
    {
        var body = combat.GetComponent<SpriteRenderer>();
        return body != null ? body.sprite : null;
    }

    static string DirectionName(Vector2 facing)
    {
        if (facing.y > 0.5f) return "Up";
        if (facing.y < -0.5f) return "Down";
        return facing.x < 0f ? "Left" : "Right";
    }
}
