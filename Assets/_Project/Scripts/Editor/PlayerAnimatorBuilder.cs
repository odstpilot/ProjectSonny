using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Menu: Sonny > Build Player Animator
//
// First turns the sprite folders under Art/Characters/Player into clips (PlayerClipBuilder), then builds the player's
// Animator Controller from animation clips named  Hold_Action_Direction:
//   Hold:      Unarmed, Melee, Ranged          (the WeaponType parameter picks one: 0, 1, 2)
//   Action:    Idle, Walk                      (every hold)
//              Charge, Swing, ChargedSwing     (Melee only)
//              Shoot                           (Ranged only)
//              Sprint, Crouch, CrouchIdle      (Unarmed only, used whatever is held; see below)
//   Direction: Up, UpRight, Right, DownRight,  (the FaceX/FaceY parameters pick one)
//              Down, DownLeft, Left, UpLeft
// For example Melee_Walk_Left or Ranged_Shoot_Up. Clips can be in any folder under Animation/Clips.
//
// A clip that doesn't exist yet is filled in with the closest one that does (see ResolveClip), so the
// controller always works and gets better as art arrives. Run it again after adding or renaming clips:
// the controller is rebuilt in place, and the Console lists every slot and which clip it's using.
//
// Sprint and Crouch states only appear once their art exists. Until then sprinting plays the walk and crouching
// plays idle and walk (PlayerController squashes the sprite). Crouch comes in Left and Right only, picked by the
// FaceSide parameter; standing still while crouched shows CrouchIdle, or the first frame of Crouch if there's none.
public static class PlayerAnimatorBuilder
{
    const string ClipsFolder = "Assets/_Project/Animation/Clips";
    const string GeneratedFolder = "Assets/_Project/Animation/Clips/Player/_Generated";
    const string ControllerPath = "Assets/_Project/Animation/Controllers/Player.controller";
    const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player 1.prefab";

    // Position in this array is the WeaponType value for that hold.
    static readonly string[] Holds = { "Unarmed", "Melee", "Ranged" };
    static readonly string[] Directions = { "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft" };
    static readonly Vector2[] RawDirections =
    {
        new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, -1),
        new Vector2(0, -1), new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1)
    };
    static readonly string[] Sides = { "Left", "Right" };

    // Parameter names are read from PlayerAnimParams so the code and the controller can't drift apart.
    static class Param
    {
        public static readonly string FaceX = nameof(PlayerAnimParams.FaceX);
        public static readonly string FaceY = nameof(PlayerAnimParams.FaceY);
        public static readonly string FaceSide = nameof(PlayerAnimParams.FaceSide);
        public static readonly string IsMoving = nameof(PlayerAnimParams.IsMoving);
        public static readonly string Sprinting = nameof(PlayerAnimParams.Sprinting);
        public static readonly string Crouching = nameof(PlayerAnimParams.Crouching);
        public static readonly string WeaponType = nameof(PlayerAnimParams.WeaponType);
        public static readonly string Charging = nameof(PlayerAnimParams.Charging);
        public static readonly string Swing = nameof(PlayerAnimParams.Swing);
        public static readonly string ChargedSwing = nameof(PlayerAnimParams.ChargedSwing);
        public static readonly string Shoot = nameof(PlayerAnimParams.Shoot);
    }

    // Where each direction's clip sits in a blend tree: the exact values PlayerController sends to FaceX/FaceY.
    static Vector2 DirectionPoint(int dir) => PlayerController.SnapToEightWay(RawDirections[dir]);

    // The straight direction a diagonal falls back to when it has no art: the one PlayerController.Facing uses.
    static string StraightFor(int dir)
    {
        Vector2 straight = PlayerController.SnapToFourWay(RawDirections[dir]);
        return Directions[System.Array.IndexOf(RawDirections, straight)];
    }

    static bool IsDiagonal(int dir) => RawDirections[dir].x != 0 && RawDirections[dir].y != 0;

    // State for one run of the builder.
    static Dictionary<string, AnimationClip> clips;
    static Dictionary<AnimationClip, AnimationClip> firstFrames;
    static StringBuilder report;
    static int slotCount;
    static int ownClipCount;

    [MenuItem("Sonny/Build Player Animator")]
    public static void Build()
    {
        int builtClips = PlayerClipBuilder.Build();
        clips = FindClips();
        firstFrames = new Dictionary<AnimationClip, AnimationClip>();
        report = new StringBuilder();
        slotCount = 0;
        ownClipCount = 0;

        AnimatorController controller = LoadEmptyController();
        AddParameters(controller);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // --- States ---
        // One state per hold and action, laid out in columns: Unarmed, Melee, Ranged, then sprint and crouch.
        // Each state is a blend tree holding that action's eight directional clips (two for crouching).
        var idle = new AnimatorState[Holds.Length];
        var walk = new AnimatorState[Holds.Length];
        for (int hold = 0; hold < Holds.Length; hold++)
        {
            idle[hold] = AddState(controller, machine, Holds[hold], "Idle", Cell(hold, 0));
            walk[hold] = AddState(controller, machine, Holds[hold], "Walk", Cell(hold, 1));
        }
        AnimatorState charge = AddState(controller, machine, "Melee", "Charge", Cell(1, 2));
        AnimatorState swing = AddState(controller, machine, "Melee", "Swing", Cell(1, 3));
        AnimatorState chargedSwing = AddState(controller, machine, "Melee", "ChargedSwing", Cell(1, 4));
        AnimatorState shoot = AddState(controller, machine, "Ranged", "Shoot", Cell(2, 2));
        machine.defaultState = idle[0];

        AnimatorState sprint = HasArt("Sprint") ? AddState(controller, machine, "Unarmed", "Sprint", Cell(3, 1)) : null;
        AnimatorState crouch = null, crouchIdle = null;
        if (HasArt("Crouch"))
        {
            crouchIdle = AddSideState(controller, machine, "CrouchIdle", Cell(3, 3));
            crouch = AddSideState(controller, machine, "Crouch", Cell(3, 4));
        }

        // --- Transitions ---
        // Unity checks a state's transitions in the order they were added and takes the first one that matches,
        // so attacks are added first, then crouching and sprinting, then walking, then weapon switching.
        var shared = new List<AnimatorState>();
        if (sprint != null) shared.Add(sprint);
        if (crouch != null) shared.AddRange(new[] { crouch, crouchIdle });

        // Melee attacks can start from standing, walking, charging, partway through another swing, or a sprint or
        // crouch (those only reach here with a melee weapon in hand, since only then are these parameters set).
        var meleeStarts = new List<AnimatorState> { idle[1], walk[1], charge, swing, chargedSwing };
        meleeStarts.AddRange(shared);
        foreach (AnimatorState from in meleeStarts)
        {
            Connect(from, swing).AddCondition(AnimatorConditionMode.If, 0f, Param.Swing);
            Connect(from, chargedSwing).AddCondition(AnimatorConditionMode.If, 0f, Param.ChargedSwing);
            if (from != charge)
                Connect(from, charge).AddCondition(AnimatorConditionMode.If, 0f, Param.Charging);
        }
        Connect(charge, idle[1]).AddCondition(AnimatorConditionMode.IfNot, 0f, Param.Charging);
        Connect(swing, idle[1], waitForClipToEnd: true);
        Connect(chargedSwing, idle[1], waitForClipToEnd: true);

        // Shooting can start from standing, walking, the previous shot (for fast fire), or a sprint or crouch.
        var shootStarts = new List<AnimatorState> { idle[2], walk[2], shoot };
        shootStarts.AddRange(shared);
        foreach (AnimatorState from in shootStarts)
            Connect(from, shoot).AddCondition(AnimatorConditionMode.If, 0f, Param.Shoot);
        Connect(shoot, idle[2], waitForClipToEnd: true);

        for (int hold = 0; hold < Holds.Length; hold++)
        {
            foreach (AnimatorState from in new[] { idle[hold], walk[hold] })
            {
                if (crouch != null) ConnectToCrouch(from, crouch, crouchIdle);
                if (sprint != null) Connect(from, sprint).AddCondition(AnimatorConditionMode.If, 0f, Param.Sprinting);
            }

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

        if (sprint != null)
        {
            if (crouch != null) ConnectToCrouch(sprint, crouch, crouchIdle);
            ConnectToStanding(sprint, idle, walk, Param.Sprinting);
        }
        if (crouch != null)
        {
            Connect(crouchIdle, crouch).AddCondition(AnimatorConditionMode.If, 0f, Param.IsMoving);
            Connect(crouch, crouchIdle).AddCondition(AnimatorConditionMode.IfNot, 0f, Param.IsMoving);
            ConnectToStanding(crouch, idle, walk, Param.Crouching);
            ConnectToStanding(crouchIdle, idle, walk, Param.Crouching);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        bool assigned = AssignToPlayerPrefab(controller);

        string summary = $"Made {builtClips} clips from the sprite folders. " +
                         $"{ownClipCount} of {slotCount} animation slots have their own clip. The rest are filled in with fallbacks.";
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
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = Param.FaceSide,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
        controller.AddParameter(Param.IsMoving, AnimatorControllerParameterType.Bool);
        controller.AddParameter(Param.Sprinting, AnimatorControllerParameterType.Bool);
        controller.AddParameter(Param.Crouching, AnimatorControllerParameterType.Bool);
        controller.AddParameter(Param.WeaponType, AnimatorControllerParameterType.Int);
        controller.AddParameter(Param.Charging, AnimatorControllerParameterType.Bool);
        controller.AddParameter(Param.Swing, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(Param.ChargedSwing, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(Param.Shoot, AnimatorControllerParameterType.Trigger);
    }

    // Where a state sits in the Animator window.
    static Vector3 Cell(int column, int row) => new Vector3(250f + column * 260f, row * 70f, 0f);

    // Whether any Unarmed clip exists for this action, in any direction.
    static bool HasArt(string action)
    {
        string prefix = $"Unarmed_{action}_";
        foreach (string name in clips.Keys)
        {
            if (name.StartsWith(prefix)) return true;
        }
        return false;
    }

    // Adds a state whose motion is a 2D blend tree: FaceX/FaceY choose which of the eight directional clips plays.
    static AnimatorState AddState(AnimatorController controller, AnimatorStateMachine machine, string hold, string action, Vector3 position)
    {
        // Sprint is one state whatever is held, so its name has no hold.
        string stateName = action == "Sprint" ? action : $"{hold} {action}";
        AnimatorState state = machine.AddState(stateName, position);
        BlendTree tree = AddBlendTree(controller, stateName, BlendTreeType.SimpleDirectional2D);
        tree.blendParameter = Param.FaceX;
        tree.blendParameterY = Param.FaceY;

        report.AppendLine(stateName);
        for (int dir = 0; dir < Directions.Length; dir++)
        {
            AnimationClip clip = ResolveClip(hold, action, dir);
            tree.AddChild(clip, DirectionPoint(dir));
            ReportSlot($"{hold}_{action}_{Directions[dir]}", clip);
        }

        state.motion = tree;
        return state;
    }

    // Adds a state whose motion is a 1D blend tree on FaceSide: -1 plays the Left clip, 1 the Right.
    static AnimatorState AddSideState(AnimatorController controller, AnimatorStateMachine machine, string action, Vector3 position)
    {
        string stateName = action == "CrouchIdle" ? "Crouch Idle" : action;
        AnimatorState state = machine.AddState(stateName, position);
        BlendTree tree = AddBlendTree(controller, stateName, BlendTreeType.Simple1D);
        tree.blendParameter = Param.FaceSide;

        report.AppendLine(stateName);
        for (int side = 0; side < Sides.Length; side++)
        {
            AnimationClip clip = ResolveSideClip(action, Sides[side]);
            tree.AddChild(clip, side == 0 ? -1f : 1f);
            ReportSlot($"Unarmed_{action}_{Sides[side]}", clip);
        }

        state.motion = tree;
        return state;
    }

    static BlendTree AddBlendTree(AnimatorController controller, string name, BlendTreeType type)
    {
        var tree = new BlendTree
        {
            name = name,
            blendType = type,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    static void ReportSlot(string wanted, AnimationClip clip)
    {
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

    // Crouching, moving or still. Checked before sprinting and walking.
    static void ConnectToCrouch(AnimatorState from, AnimatorState crouch, AnimatorState crouchIdle)
    {
        AnimatorStateTransition moving = Connect(from, crouch);
        moving.AddCondition(AnimatorConditionMode.If, 0f, Param.Crouching);
        moving.AddCondition(AnimatorConditionMode.If, 0f, Param.IsMoving);
        AnimatorStateTransition still = Connect(from, crouchIdle);
        still.AddCondition(AnimatorConditionMode.If, 0f, Param.Crouching);
        still.AddCondition(AnimatorConditionMode.IfNot, 0f, Param.IsMoving);
    }

    // Leaves a shared state (sprint, crouch) once `whileTrue` goes false, straight to the held weapon's idle or walk,
    // so there's never a frame of the wrong one in between.
    static void ConnectToStanding(AnimatorState from, AnimatorState[] idle, AnimatorState[] walk, string whileTrue)
    {
        for (int hold = 0; hold < Holds.Length; hold++)
        {
            AnimatorStateTransition toWalk = Connect(from, walk[hold]);
            toWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, whileTrue);
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, Param.IsMoving);
            toWalk.AddCondition(AnimatorConditionMode.Equals, hold, Param.WeaponType);

            AnimatorStateTransition toIdle = Connect(from, idle[hold]);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, whileTrue);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, Param.IsMoving);
            toIdle.AddCondition(AnimatorConditionMode.Equals, hold, Param.WeaponType);
        }
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
    //   Sprint                -> Walk
    //   a diagonal            -> the straight direction Facing uses (e.g. DownLeft -> Down), for attacks and sprints,
    //                            and for a Melee/Ranged Idle or Walk that has its own clip that way
    //   Idle                  -> first frame of the same hold's Walk
    //   Melee/Ranged Idle/Walk -> the Unarmed version
    //   Unarmed Walk          -> straight version if diagonal, else the old walk clips named Up, Down, Left, Right
    //   Unarmed Idle          -> straight version if diagonal, else the old Idle clip when facing down, else the
    //                            first frame of Unarmed Walk
    static AnimationClip ResolveClip(string hold, string action, int dir)
    {
        string direction = Directions[dir];
        if (clips.TryGetValue($"{hold}_{action}_{direction}", out AnimationClip exact))
            return exact;

        // Attacks and sprints come in four directions at most, so a diagonal borrows the straight one.
        bool standing = action == "Idle" || action == "Walk";
        int straight = System.Array.IndexOf(Directions, StraightFor(dir));
        if (IsDiagonal(dir) && (!standing || (hold != "Unarmed" && clips.ContainsKey($"{hold}_{action}_{Directions[straight]}"))))
            return ResolveClip(hold, action, straight);

        switch (action)
        {
            case "ChargedSwing":
                return ResolveClip(hold, "Swing", dir);
            case "Charge":
                return clips.TryGetValue($"{hold}_Swing_{direction}", out AnimationClip swing)
                    ? FirstFrameOf(swing)
                    : ResolveClip(hold, "Idle", dir);
            case "Swing":
            case "Shoot":
                return ResolveClip(hold, "Idle", dir);
            case "Sprint":
                return ResolveClip(hold, "Walk", dir);
        }

        // Only Idle and Walk reach this point.
        if (action == "Idle" && clips.TryGetValue($"{hold}_Walk_{direction}", out AnimationClip walk))
            return FirstFrameOf(walk);
        if (hold != "Unarmed")
            return ResolveClip("Unarmed", action, dir);
        if (IsDiagonal(dir))
            return ResolveClip("Unarmed", action, straight);

        if (action == "Walk")
            return clips.TryGetValue(direction, out AnimationClip oldWalk) ? oldWalk : null;

        if (direction == "Down" && clips.TryGetValue("Idle", out AnimationClip oldIdle))
            return oldIdle;
        AnimationClip unarmedWalk = ResolveClip("Unarmed", "Walk", dir);
        return unarmedWalk != null ? FirstFrameOf(unarmedWalk) : null;
    }

    // Crouch and CrouchIdle come in Left and Right. A missing CrouchIdle holds the first frame of Crouch facing the
    // same way; failing that, a missing side uses the other one.
    static AnimationClip ResolveSideClip(string action, string side)
    {
        if (clips.TryGetValue($"Unarmed_{action}_{side}", out AnimationClip exact))
            return exact;
        if (action == "CrouchIdle" && clips.TryGetValue($"Unarmed_Crouch_{side}", out AnimationClip sameSide))
            return FirstFrameOf(sameSide);

        string otherSide = side == "Left" ? "Right" : "Left";
        if (clips.TryGetValue($"Unarmed_{action}_{otherSide}", out AnimationClip other))
            return other;
        if (action == "CrouchIdle")
        {
            AnimationClip crouch = ResolveSideClip("Crouch", side);
            return crouch != null ? FirstFrameOf(crouch) : null;
        }
        return null;
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
