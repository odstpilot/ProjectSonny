using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// shadows and footprints so things stand on the floor
static class DepthDressing
{
    const string ShadowFolder = "Assets/_Project/Art/Environment/Shadows";
    const int PixelsPerUnit = 32;
    const string WallShadeTilePath = ShadowFolder + "/WallShade.asset";

    public const float FeetLift = 0.24f;

    public static Sprite CharacterShadow => LoadSprite("CharacterShadow.png", Vector4.zero);
    public static Sprite FurnitureShadow => LoadSprite("FurnitureShadow.png", new Vector4(4f, 4f, 4f, 4f));

    public static readonly Vector2 Footprint = new Vector2(0.8f, 0.4f);

    public static void SetFootprint(BoxCollider2D box, Sprite art)
    {
        Bounds bounds = art.bounds;
        Vector3 scale = box.transform.lossyScale;
        box.size = new Vector2(Footprint.x / (scale.x != 0f ? scale.x : 1f), Footprint.y / (scale.y != 0f ? scale.y : 1f));
        box.offset = new Vector2(bounds.center.x, bounds.min.y + box.size.y * 0.5f);
    }

    public static void AddShadow(GameObject character, Sprite art)
    {
        Transform old = character.transform.Find("Shadow");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        shadow.transform.SetParent(character.transform, false);
        Vector3 scale = character.transform.lossyScale;
        shadow.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f);
        shadow.transform.localPosition = new Vector3(art.bounds.center.x, art.bounds.min.y + 0.03f / scale.y, 0f);
        shadow.sprite = CharacterShadow;
        shadow.sortingLayerName = DepthSort.Layer;
        shadow.sortingOrder = -1;
    }

    public static TileBase WallShadeTile
    {
        get
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(WallShadeTilePath);
            if (tile != null) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = LoadSprite("WallShade.png", Vector4.zero);
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, WallShadeTilePath);
            return tile;
        }
    }

    static Sprite LoadSprite(string file, Vector4 border)
    {
        string path = $"{ShadowFolder}/{file}";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Depth dressing: {path} is missing, so there's no shadow from it.");
            return null;
        }
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != PixelsPerUnit ||
            importer.filterMode != FilterMode.Point || importer.spriteBorder != border || settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
