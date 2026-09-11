using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Menu: Sonny > Build Player Animator
//
// Builds the player's Animator Controller from animation clips named  Hold_Action_Direction:
//   Hold:      Unarmed, Melee, Ranged          (the WeaponType parameter picks one: 0, 1, 2)
//   Action:    Idle, Walk                      (every hold)
//              Charge, Swing, ChargedSwing     (Melee only)
//              Shoot                           (Ranged only)
//   Direction: Up, Down, Left, Right           (the FaceX/FaceY parameters pick one)
// For example Melee_Walk_Left or Ranged_Shoot_Up. Clips can be in any folder under Animation/Clips.
//
// A clip that doesn't exist yet is filled in with the closest one that does (see ResolveClip), so the
// controller always works and gets better as art arrives. Run it again after adding or renaming clips:
// the controller is rebuilt in place, and the Console lists every slot and which clip it's using.
public static class PlayerAnimatorBuilder
{
    const string ClipsFolder = "Assets/_Project/Animation/Clips";
    const string GeneratedFolder = "Assets/_Project/Animation/Clips/Player/_Generated";
    const string ControllerPath = "Assets/_Project/Animation/Controllers/Player.controller";
    const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player 1.prefab";

    // Position in this array is the WeaponType value for that hold.
    static readonly string[] Holds = { "Unarmed", "Melee", "Ranged" };
    static readonly string[] Directions = { "Up", "Down", "Left", "Right" };
    // Where each direction's clip sits in a blend tree. FaceX/FaceY always land exactly on one of these points.
    static readonly Vector2[] DirectionPoints = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    // Parameter names are read from PlayerAnimParams so the code and the controller can't drift apart.
    static class Param
    {
        public static readonly string FaceX = nameof(PlayerAnimParams.FaceX);
        public static readonly string FaceY = nameof(PlayerAnimParams.FaceY);
        public static readonly string IsMoving = nameof(PlayerAnimParams.IsMoving);
        public static readonly string WeaponType = nameof(PlayerAnimParams.WeaponType);
        public static readonly string Charging = nameof(PlayerAnimParams.Charging);
        public static readonly string Swing = nameof(PlayerAnimParams.Swing);
        public static readonly string ChargedSwing = nameof(PlayerAnimParams.ChargedSwing);
        public static readonly string Shoot = nameof(PlayerAnimParams.Shoot);
    }

    // State for one run of the builder.
    static Dictionary<string, AnimationClip> clips;
    static Dictionary<AnimationClip, AnimationClip> firstFrames;
    static StringBuilder report;
    static int slotCount;
    static int ownClipCount;

    [MenuItem("Sonny/Build Player Animator")]
    public static void Build()
    {
        clips = FindClips();
        firstFrames = new Dictionary<AnimationClip, AnimationClip>();
        report = new StringBuilder();
        slotCount = 0;
        ownClipCount = 0;

        AnimatorController controller = LoadEmptyController();
        AddParameters(controller);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // --- States ---
        // One state per hold and action, laid out in columns: Unarmed, Melee, Ranged.
        // Each state is a blend tree holding that action's four directional clips.
        var idle = new AnimatorState[Holds.Length];
        var walk = new AnimatorState[Holds.Length];
        for (int hold = 0; hold < Holds.Length; hold++)
        {
            idle[hold] = AddState(controller, machine, hold, "Idle", 0);
            walk[hold] = AddState(controller, machine, hold, "Walk", 1);
        }
        AnimatorState charge = AddState(controller, machine, 1, "Charge", 2);
        AnimatorState swing = AddState(controller, machine, 1, "Swing", 3);
        AnimatorState chargedSwing = AddState(controller, machine, 1, "ChargedSwing", 4);
        AnimatorState shoot = AddState(controller, machine, 2, "Shoot", 2);
        machine.defaultState = idle[0];

        // --- Transitions ---
        // Unity checks a state's transitions in the order they were added and takes the first one that matches,
        // so attacks are added first, then walking, then weapon switching.

        // Melee attacks can start from standing, walking, charging, or partway through another swing.
        foreach (AnimatorState from in new[] { idle[1], walk[1], charge, swing, chargedSwing })
        {
            Connect(from, swing).AddCondition(AnimatorConditionMode.If, 0f, Param.Swing);
            Connect(from, chargedSwing).AddCondition(AnimatorConditionMode.If, 0f, Param.ChargedSwing);
        }
        foreach (AnimatorState from in new[] { idle[1], walk[1], swing, chargedSwing })
            Connect(from, charge).AddCondition(AnimatorConditionMode.If, 0f, Param.Charging);
        Connect(charge, idle[1]).AddCondition(AnimatorConditionMode.IfNot, 0f, Param.Charging);
        Connect(swing, idle[1], waitForClipToEnd: true);
        Connect(chargedSwing, idle[1], waitForClipToEnd: true);

        // Shooting can start from standing, walking, or the previous shot (for fast fire).
        foreach (AnimatorState from in new[] { idle[2], walk[2], shoot })
            Connect(from, shoot).AddCondition(AnimatorConditionMode.If, 0f, Param.Shoot);
        Connect(shoot, idle[2], waitForClipToEnd: true);

        for (int hold = 0; hold < Holds.Length; hold++)
        {
            Connect(idle[hold], walk[hold]).AddCondition(AnimatorConditionMode.If, 0f, Param.IsMoving);
            Connect(walk[hold], idle[hold]).AddCondition(AnimatorConditionMode.IfNot, 0f, Param.IsMoving);

            // Switching weapons jumps straight to the new hold's matching state.
            for (int other = 0; other < Holds.Length; other++)
            {
                if (other == hold) continue;
                Connect(idle[hold], idle[other]).AddCondition(AnimatorConditionMode.Equals, other, Param.WeaponType);
                Connect(walk[hold], walk[other]).AddCondition(AnimatorConditionMode.Equals, other, Param.WeaponType);
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        bool assigned = AssignToPlayerPrefab(controller);

        string summary = $"{ownClipCount} of {slotCount} animation slots have their own clip. The rest are filled in with fallbacks.";
        Debug.Log($"Built {ControllerPath}. {summary}\n\n{report}", controller);
        EditorUtility.DisplayDialog("Player Animator built",
            summary + "\n\n" +
            (assigned ? "Player 1 now uses this controller." : $"Couldn't find {PlayerPrefabPath}. Drag {ControllerPath} onto the player's Animator.") +
            "\n\nThe Console lists every slot and which clip it's using.",
            "OK");
        EditorGUIUtility.PingObject(controller);
    }

    static AnimatorController LoadEmptyController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Empty it instead of deleting the file, so it keeps its GUID and everything pointing at it stays connected.
        foreach (Object part in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (part != null && part != controller)
                Object.DestroyImmediate(part, true);
        }
        controller.parameters = new AnimatorControllerParameter[0];
        controller.layers = new AnimatorControllerLayer[0];
        controller.AddLayer("Base Layer");
        return controller;
    }

    static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter(Param.FaceX, AnimatorControllerParameterType.Float);
        // Start facing down, toward the camera.
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = Param.FaceY,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = -1f
        });
        controller.AddParameter(Param.IsMoving, AnimatorControllerParameterType.Bool);
        controller.AddParameter(Param.WeaponType, AnimatorControllerParameterType.Int);
        controller.AddParameter(Param.Charging, AnimatorControllerParameterType.Bool);
        controller.AddParameter(Param.Swing, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(Param.ChargedSwing, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(Param.Shoot, AnimatorControllerParameterType.Trigger);
    }

    // Adds a state whose motion is a 2D blend tree: FaceX/FaceY choose which of the four directional clips plays.
    static AnimatorState AddState(AnimatorController controller, AnimatorStateMachine machine, int hold, string action, int row)
    {
        string stateName = $"{Holds[hold]} {action}";
        AnimatorState state = machine.AddState(stateName, new Vector3(250f + hold * 260f, row * 70f, 0f));

        var tree = new BlendTree
        {
            name = stateName,
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = Param.FaceX,
            blendParameterY = Param.FaceY,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        report.AppendLine(stateName);
        for (int dir = 0; dir < Directions.Length; dir++)
        {
            string wanted = $"{Holds[hold]}_{action}_{Directions[dir]}";
            AnimationClip clip = ResolveClip(Holds[hold], action, Directions[dir]);
            tree.AddChild(clip, DirectionPoints[dir]);

            slotCount++;
            if (clip != null && clip.name == wanted)
            {
                ownClipCount++;
                report.AppendLine($"    {wanted}");
            }
            else
            {
                report.AppendLine($"    {wanted}  missing, using {(clip != null ? clip.name : "nothing")}");
            }
        }

        state.motion = tree;
        return state;
    }

    // Sprite frames can't blend into each other, so every transition is instant.
    // waitForClipToEnd: leave only once the clip finishes (attacks). Otherwise leave as soon as conditions match.
    static AnimatorStateTransition Connect(AnimatorState from, AnimatorState to, bool waitForClipToEnd = false)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.duration = 0f;
        transition.hasFixedDuration = true;
        transition.hasExitTime = waitForClipToEnd;
        transition.exitTime = 1f;
        return transition;
    }

    // Finds the clip for one slot. If it doesn't exist, falls back step by step:
    //   ChargedSwing          -> Swing
    //   Charge                -> first frame of Swing (the raised pose), else Idle
    //   Swing, Shoot          -> Idle
    //   Idle                  -> first frame of the same hold's Walk
    //   Melee/Ranged Idle/Walk -> the Unarmed version
    //   Unarmed Walk          -> the old walk clips named Up, Down, Left, Right
    //   Unarmed Idle          -> the old Idle clip when facing down, else the first frame of Unarmed Walk
    static AnimationClip ResolveClip(string hold, string action, string direction)
    {
        if (clips.TryGetValue($"{hold}_{action}_{direction}", out AnimationClip exact))
            return exact;

        switch (action)
        {
            case "ChargedSwing":
                return ResolveClip(hold, "Swing", direction);
            case "Charge":
                return clips.TryGetValue($"{hold}_Swing_{direction}", out AnimationClip swing)
                    ? FirstFrameOf(swing)
                    : ResolveClip(hold, "Idle", direction);
            case "Swing":
            case "Shoot":
                return ResolveClip(hold, "Idle", direction);
        }

        // Only Idle and Walk reach this point.
        if (action == "Idle" && clips.TryGetValue($"{hold}_Walk_{direction}", out AnimationClip walk))
            return FirstFrameOf(walk);
        if (hold != "Unarmed")
            return ResolveClip("Unarmed", action, direction);

        if (action == "Walk")
            return clips.TryGetValue(direction, out AnimationClip oldWalk) ? oldWalk : null;

        if (direction == "Down" && clips.TryGetValue("Idle", out AnimationClip oldIdle))
            return oldIdle;
        AnimationClip unarmedWalk = ResolveClip("Unarmed", "Walk", direction);
        return unarmedWalk != null ? FirstFrameOf(unarmedWalk) : null;
    }

    // A one-frame clip showing the first frame of another clip, saved in the _Generated folder.
    // Stands in for idle and windup poses that haven't been drawn yet.
    static AnimationClip FirstFrameOf(AnimationClip source)
    {
        if (firstFrames.TryGetValue(source, out AnimationClip cached))
            return cached;

        EditorCurveBinding spriteBinding = default;
        ObjectReferenceKeyframe[] keys = null;
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
            {
                spriteBinding = binding;
                keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                break;
            }
        }
        if (keys == null || keys.Length == 0)
            return source; // not a sprite animation, so use it as-is

        EnsureFolder(GeneratedFolder);
        string path = $"{GeneratedFolder}/{source.name}_FirstFrame.anim";
        var still = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = still == null;
        if (isNew) still = new AnimationClip();

        // Two keys one frame apart give the clip a real length, so "wait for clip to end" transitions behave.
        float frameRate = source.frameRate > 0f ? source.frameRate : 12f;
        still.frameRate = frameRate;
        AnimationUtility.SetObjectReferenceCurve(still, spriteBinding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = keys[0].value },
            new ObjectReferenceKeyframe { time = 1f / frameRate, value = keys[0].value }
        });

        if (isNew)
            AssetDatabase.CreateAsset(still, path);
        else
            EditorUtility.SetDirty(still);

        firstFrames[source] = still;
        return still;
    }

    static Dictionary<string, AnimationClip> FindClips()
    {
        var found = new Dictionary<string, AnimationClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(GeneratedFolder)) continue; // the builder's own stand-ins aren't real art

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            if (found.TryGetValue(clip.name, out AnimationClip existing))
                Debug.LogWarning($"Two animation clips are named {clip.name}. Using {AssetDatabase.GetAssetPath(existing)} and ignoring {path}.");
            else
                found.Add(clip.name, clip);
        }
        return found;
    }

    static bool AssignToPlayerPrefab(AnimatorController controller)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null) return false;

            if (animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        int slash = folder.LastIndexOf('/');
        string parent = folder.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
    }
}
