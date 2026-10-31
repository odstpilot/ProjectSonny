using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// puts stair art on each stairway
public static class StairsDresser
{
    const string ArtFolder = "Assets/_Project/Art/Environment/Stairs";
    const string ChildName = "Steps";
    const float PixelsPerUnit = 32f;
    const string SortingLayer = "Floor";
    const int SortingOrder = 5;

    public static void DressOpenScene()
    {
        int dressed = 0;
        foreach (Stairway stairs in Object.FindObjectsByType<Stairway>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Dress(stairs)) dressed++;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Dressed {dressed} flights of stairs.");
    }

    public static void DressStationScenes()
    {
        foreach (string scene in new[]
                 {
                     "Assets/_Project/Scenes/Levels/Chapter1.unity",
                     "Assets/_Project/Scenes/Levels/Chapter2.unity",
                     "Assets/_Project/Scenes/Levels/Chapter3.unity",
                     "Assets/_Project/Scenes/Levels/StationBlueprint.unity",
                 })
        {
            EditorSceneManager.OpenScene(scene);
            DressOpenScene();
            EditorSceneManager.SaveOpenScenes();
        }
    }

    public static bool Dress(Stairway stairs)
    {
        var zone = stairs.GetComponent<BoxCollider2D>();
        if (zone == null) return false;

        int width = Mathf.Max(1, Mathf.RoundToInt(zone.size.x));
        int height = Mathf.Max(1, Mathf.RoundToInt(zone.size.y));
        string kind = stairs.name.Contains("Down") ? "Down" : "Up";
        Vector2 center = (Vector2)stairs.transform.position + zone.offset;
        bool runsNorth = stairs.arrival == null || stairs.arrival.position.y <= center.y;
        string path = $"{ArtFolder}/Stairs_{kind}_{width}x{height}_{(runsNorth ? "N" : "S")}.png";

        Sprite sprite = LoadSprite(path);
        if (sprite == null)
        {
            Debug.LogWarning($"No stairs art at {path} for {stairs.name}, so it's left bare. Draw one that size to dress it.", stairs);
            return false;
        }

        Transform steps = stairs.transform.Find(ChildName);
        if (steps == null)
        {
            steps = new GameObject(ChildName).transform;
            steps.SetParent(stairs.transform, false);
            Undo.RegisterCreatedObjectUndo(steps.gameObject, "Dress Stairs");
        }
        steps.position = new Vector3(center.x, center.y, stairs.transform.position.z);
        var look = steps.GetComponent<SpriteRenderer>();
        if (look == null) look = steps.gameObject.AddComponent<SpriteRenderer>();
        look.sprite = sprite;
        look.sortingLayerName = SortingLayer;
        look.sortingOrder = SortingOrder;
        EditorUtility.SetDirty(look);
        return true;
    }

    static Sprite LoadSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool changed = importer.textureType != TextureImporterType.Sprite ||
                       settings.spriteMode != (int)SpriteImportMode.Single ||
                       settings.spritePixelsPerUnit != PixelsPerUnit ||
                       settings.spriteAlignment != (int)SpriteAlignment.Center ||
                       settings.filterMode != FilterMode.Point ||
                       settings.mipmapEnabled ||
                       importer.textureCompression != TextureImporterCompression.Uncompressed;
        if (changed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
