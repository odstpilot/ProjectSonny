using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// turns player sprite folders into clips
public static class PlayerClipBuilder
{
    const string ArtFolder = "Assets/_Project/Art/Characters/Player";
    const string ClipsFolder = "Assets/_Project/Animation/Clips/Player";

    const float PixelsPerUnit = 100f;

    static readonly string[] Holds = { "Melee", "Ranged" };
    static readonly HashSet<string> Directions = new HashSet<string>
    {
        "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft"
    };

    static readonly Dictionary<string, float> LoopSeconds = new Dictionary<string, float>
    {
        { "Idle", 1f },
        { "Walk", 0.8f },
        { "Sprint", 0.48f },
        { "Crouch", 1.12f },
        { "CrouchIdle", 1f },
        { "Swing", 0.42f },
        { "ChargedSwing", 0.42f },
    };
    static readonly HashSet<string> PlayOnce = new HashSet<string> { "Swing", "ChargedSwing", "Shoot" };
    const float DefaultFramesPerSecond = 8f;

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

    static void WriteClip(string path, string action, List<Sprite> frames)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();
        clip.ClearCurves();

        float frameTime = LoopSeconds.TryGetValue(action, out float loop) ? loop / frames.Count : 1f / DefaultFramesPerSecond;
        clip.frameRate = 1f / frameTime;

        var keys = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * frameTime, value = frames[i] };

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = !PlayOnce.Contains(action);
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
