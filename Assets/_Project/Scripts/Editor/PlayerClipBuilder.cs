using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Run by Sonny > Build Player Animator before it builds the controller.
//
// Turns the player's sprite folders into animation clips, one clip per folder:
//   Art/Characters/Player/<Action>/<Direction>/          -> Animation/Clips/Player/<Action>/Unarmed_<Action>_<Direction>.anim
//   Art/Characters/Player/<Hold>/<Action>/<Direction>/   -> Animation/Clips/Player/<Action>/<Hold>_<Action>_<Direction>.anim
// Hold is Melee or Ranged. Direction is one of Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft.
// Frames play in filename order, so number them with leading zeros (Player_Walk_Up_00.png, _01, ...).
//
// To add an animation, drop its frames into a new folder and run the menu item. For example crouching is
// Crouch/Left and Crouch/Right, and sprinting is Sprint/Up, Sprint/Down, Sprint/Left, Sprint/Right.
// Frames are also given the player's import settings, so new art doesn't need setting up by hand.
public static class PlayerClipBuilder
{
    const string ArtFolder = "Assets/_Project/Art/Characters/Player";
    const string ClipsFolder = "Assets/_Project/Animation/Clips/Player";

    // Every frame is 392x488 (49x61 art pixels drawn at 8x) with the feet on the same row, so one pivot fits all of them.
    const float PixelsPerUnit = 100f;

    static readonly string[] Holds = { "Melee", "Ranged" };
    static readonly HashSet<string> Directions = new HashSet<string>
    {
        "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft"
    };

    // Seconds for one loop of each action, however many frames it has. Every direction of an action is the same
    // length, so turning mid-stride carries on from the same point in the step. Walk, Sprint and Crouch are two
    // steps each, timed to PlayerController's footsteps (baseStepRate 0.4s; x0.6 sprinting, x1.4 crouching).
    static readonly Dictionary<string, float> LoopSeconds = new Dictionary<string, float>
    {
        { "Idle", 1f },
        { "Walk", 0.8f },
        { "Sprint", 0.48f },
        { "Crouch", 1.12f },
        { "CrouchIdle", 1f },
    };
    const float DefaultFramesPerSecond = 8f;

    // Builds or updates every clip, and returns how many there are.
    public static int Build()
    {
        int built = 0;
        foreach (string actionFolder in AssetDatabase.GetSubFolders(ArtFolder))
        {
            string name = Path.GetFileName(actionFolder);
            if (System.Array.IndexOf(Holds, name) >= 0)
            {
                foreach (string holdAction in AssetDatabase.GetSubFolders(actionFolder))
                    built += BuildAction(name, holdAction);
            }
            else
            {
                built += BuildAction("Unarmed", actionFolder);
            }
        }
        AssetDatabase.SaveAssets();
        return built;
    }

    static int BuildAction(string hold, string actionFolder)
    {
        string action = Path.GetFileName(actionFolder);
        int built = 0;
        foreach (string directionFolder in AssetDatabase.GetSubFolders(actionFolder))
        {
            string direction = Path.GetFileName(directionFolder);
            if (!Directions.Contains(direction))
            {
                Debug.LogWarning($"{directionFolder} isn't a direction the player uses, so it was skipped. " +
                                 $"Use one of: {string.Join(", ", Directions)}.");
                continue;
            }

            List<Sprite> frames = LoadFrames(directionFolder);
            if (frames.Count == 0) continue;

            string folder = $"{ClipsFolder}/{action}";
            EnsureFolder(folder);
            WriteClip($"{folder}/{hold}_{action}_{direction}.anim", action, frames);
            built++;
        }
        return built;
    }

    static List<Sprite> LoadFrames(string folder)
    {
        var files = new List<string>(Directory.GetFiles(folder, "*.png"));
        files.Sort(System.StringComparer.Ordinal);

        var frames = new List<Sprite>();
        foreach (string file in files)
        {
            string path = file.Replace('\\', '/');
            ApplyImportSettings(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) frames.Add(sprite);
        }
        return frames;
    }

    // Crisp pixels (no smoothing or compression), centre pivot, and the same size in the world as every other frame.
    static void ApplyImportSettings(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool changed =
            importer.textureType != TextureImporterType.Sprite ||
            settings.spriteMode != (int)SpriteImportMode.Single ||
            settings.spritePixelsPerUnit != PixelsPerUnit ||
            settings.spriteAlignment != (int)SpriteAlignment.Center ||
            settings.spriteMeshType != SpriteMeshType.FullRect ||
            settings.filterMode != FilterMode.Point ||
            settings.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed;
        if (!changed) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spritePixelsPerUnit = PixelsPerUnit;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.filterMode = FilterMode.Point;
        settings.mipmapEnabled = false;
        settings.alphaIsTransparency = true;
        importer.SetTextureSettings(settings);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    // Updates the clip in place if it exists, so it keeps its GUID and the controller stays pointed at it.
    static void WriteClip(string path, string action, List<Sprite> frames)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();
        clip.ClearCurves();

        float frameTime = LoopSeconds.TryGetValue(action, out float loop) ? loop / frames.Count : 1f / DefaultFramesPerSecond;
        clip.frameRate = 1f / frameTime;

        // One key per frame. Unity gives a sprite clip one extra frame of length after its last key, so the last frame
        // is on screen as long as the others and the loop has no hitch.
        var keys = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * frameTime, value = frames[i] };

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);
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
