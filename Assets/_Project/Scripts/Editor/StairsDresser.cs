using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Sonny > Dress Stairs (in the open scene). FloorOneBuilder does it for every flight it places.
//
// Gives each Stairway its steps: a sprite over its trigger, from Art/Environment/Stairs, named
//   Stairs_<Up|Down>_<width>x<height>_<N|S>.png
// Up or Down by the stairs' name ("Stairs Up 1"), the size in cells by its trigger, and N or S by which way the flight
// runs away from the floor it's reached from (its arrival spot is on that floor). The art is drawn at the tileset's own
// scale, 32 pixels to a cell, so it's imported here the same way: crisp, uncompressed, 32 pixels per unit.
// Run it again any time: each flight's steps are made once and updated in place.
public static class StairsDresser
{
    const string ArtFolder = "Assets/_Project/Art/Environment/Stairs";
    const string ChildName = "Steps";
    const float PixelsPerUnit = 32f;
    // On the floor, over its rugs and details (FloorOneBuilder's floor tilemaps go up to 3), under anyone standing on them.
    const string SortingLayer = "Floor";
    const int SortingOrder = 5;

    [MenuItem("Sonny/Dress Stairs")]
    public static void DressOpenScene()
    {
        int dressed = 0;
        foreach (Stairway stairs in Object.FindObjectsByType<Stairway>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Dress(stairs)) dressed++;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Dressed {dressed} flights of stairs.");
    }

    // For a batch run over the scenes with stairs in them: opens, dresses, and saves each.
    public static void DressStationScenes()
    {
        foreach (string scene in new[]
                 {
                     "Assets/_Project/Scenes/Levels/Chapter1.unity",
                     "Assets/_Project/Scenes/Levels/Chapter2.unity",
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

    // Crisp pixels, no compression, centre pivot, 32 pixels to the cell like the tileset.
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
