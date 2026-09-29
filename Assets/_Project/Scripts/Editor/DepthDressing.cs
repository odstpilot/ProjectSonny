using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// What the builders use to make the floor read as a floor people stand on (FloorOneBuilder, ChapterOneBuilder):
//  - characters are solid only where they stand: a footprint on the floor about as wide as their body. Like people in
//    a 3D game, two can stand close, one behind the other, and the one in front covers the one behind (the 2D renderer
//    sorts by height on screen along a custom axis, Settings/Renderer2D: see DepthSort), but never inside each other
//  - a flat shadow under each character's feet, and under each piece of furniture
//  - a band of shade along the floor at the foot of every wall
// The shadow art is in Art/Environment/Shadows, drawn at the tiles' 32 pixels to a unit; repaint it there.
static class DepthDressing
{
    const string ShadowFolder = "Assets/_Project/Art/Environment/Shadows";
    const int PixelsPerUnit = 32;
    const string WallShadeTilePath = ShadowFolder + "/WallShade.asset";

    // How far above the middle of a floor cell a character stands, so their feet are just inside it rather than
    // hanging over the cell below (their sprite is centered, about 0.64 above their feet).
    public const float FeetLift = 0.24f;

    public static Sprite CharacterShadow => LoadSprite("CharacterShadow.png", Vector4.zero);
    public static Sprite FurnitureShadow => LoadSprite("FurnitureShadow.png", new Vector4(4f, 4f, 4f, 4f));

    // How much of the floor a character stands on, in world units: about as wide as their body, and as deep as a pair of
    // feet seen from above.
    public static readonly Vector2 Footprint = new Vector2(0.8f, 0.4f);

    // The body is solid for its footprint, with its bottom on the bottom of the art. Sized in the sprite's own units, so
    // it follows the character's scale.
    public static void SetFootprint(BoxCollider2D box, Sprite art)
    {
        Bounds bounds = art.bounds;
        Vector3 scale = box.transform.lossyScale;
        box.size = new Vector2(Footprint.x / (scale.x != 0f ? scale.x : 1f), Footprint.y / (scale.y != 0f ? scale.y : 1f));
        box.offset = new Vector2(bounds.center.x, bounds.min.y + box.size.y * 0.5f);
    }

    // A flat shadow at the character's feet, drawn just under them inside their sorting group (DepthSort.Group puts one
    // on every character), so it goes in front of and behind things with them. Replaces one that's already there.
    public static void AddShadow(GameObject character, Sprite art)
    {
        Transform old = character.transform.Find("Shadow");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        shadow.transform.SetParent(character.transform, false);
        Vector3 scale = character.transform.lossyScale;
        // Drawn at the tiles' pixel size whatever the character is scaled to.
        shadow.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f);
        shadow.transform.localPosition = new Vector3(art.bounds.center.x, art.bounds.min.y + 0.03f / scale.y, 0f);
        shadow.sprite = CharacterShadow;
        shadow.sortingLayerName = DepthSort.Layer;
        shadow.sortingOrder = -1;
    }

    // The same shade on every floor cell with a wall face above it.
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

    // One of the shadow images as a sprite, imported as crisp pixel art at the tiles' size. Fixes the import settings
    // the first time it's used, so a repainted image keeps them.
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
            // Stretched shadows (9-sliced under furniture) need the whole rectangle as their mesh.
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
