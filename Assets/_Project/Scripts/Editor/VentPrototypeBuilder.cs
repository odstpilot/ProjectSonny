using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Menu: Sonny > Add Vent Prototype To Scene
//
// Puts a VentNetwork with the example ducts into whichever scene is open, with its sounds and font filled in, and two
// VentGrates beside the player: 1, where the example ducts start, and 2, where they come out. Drag grate 2 to wherever
// the ducts should lead, and edit the map on the Vents object. Nothing is saved, so look it over and save the scene
// yourself; undo takes it all back out.
//
// Menu: Sonny > Fill In Vent Sounds
//
// Adds the ducts' sounds (SoundDefaults.Vents) to the open scene's SoundManager, any it doesn't list yet, and fills in
// the font on every VentNetwork that hasn't one.
public static class VentPrototypeBuilder
{
    const string FontPath = "Assets/_Project/Art/Fonts/SdAsteroidB612-wo3e2 SDF.asset";
    const string GrateTilePath = "Assets/_Project/Art/Environment/Tilesets/ShipTiles/tileset_105.asset"; // a wall panel with a vent in it
    const int IgnoreRaycastLayer = 2;   // so a grate never blocks a robot's line of sight

    [MenuItem("Sonny/Add Vent Prototype To Scene")]
    static void AddToScene()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 origin = player != null ? player.transform.position
            : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;
        origin.z = 0f;

        var root = new GameObject("Vents");
        Undo.RegisterCreatedObjectUndo(root, "Add Vent Prototype");
        root.transform.position = origin;

        var network = root.AddComponent<VentNetwork>();
        FillEmptySlots(network);

        SpriteRenderer playerSprite = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        MakeGrate(root.transform, network, 1, origin + new Vector3(1.5f, 0f, 0f), playerSprite);
        MakeGrate(root.transform, network, 2, origin + new Vector3(-1.5f, 0f, 0f), playerSprite);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Vent prototype added: crawl in at Vent Grate 1. Move Vent Grate 2 to wherever the ducts should come out.", root);
    }

    [MenuItem("Sonny/Fill In Vent Sounds")]
    static void FillInSounds()
    {
        VentNetwork[] networks = Object.FindObjectsByType<VentNetwork>();
        foreach (VentNetwork network in networks)
        {
            Undo.RecordObject(network, "Fill In Vent Sounds");
            FillEmptySlots(network);
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(network.gameObject.scene);
        }
        Debug.Log(networks.Length == 0 ? "There's no VentNetwork in the open scene." : $"Filled in the empty sound slots on {networks.Length} VentNetwork(s).");
    }

    // Also used by ChapterOneBuilder for its ducts: the font, and the ducts' sounds in the scene's SoundManager.
    internal static void FillEmptySlots(VentNetwork network)
    {
        if (network.font == null) network.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        SoundDefaults.Fill(network.gameObject.scene, SoundDefaults.Vents);
    }

    static void MakeGrate(Transform parent, VentNetwork network, int number, Vector3 position, SpriteRenderer playerSprite)
    {
        var grate = new GameObject($"Vent Grate {number}") { layer = IgnoreRaycastLayer };
        grate.transform.SetParent(parent, false);
        grate.transform.position = position;

        var footprint = grate.AddComponent<BoxCollider2D>();
        footprint.isTrigger = true;
        footprint.size = Vector2.one;

        var art = new GameObject("Sprite") { layer = IgnoreRaycastLayer };
        art.transform.SetParent(grate.transform, false);
        var sprite = art.AddComponent<SpriteRenderer>();
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(GrateTilePath);
        if (tile != null) sprite.sprite = tile.sprite;
        else Debug.LogWarning($"No tile at {GrateTilePath} for the vent grate's sprite.");
        if (playerSprite != null)
        {
            // On the player's layer, just under them.
            sprite.sortingLayerID = playerSprite.sortingLayerID;
            sprite.sortingOrder = playerSprite.sortingOrder - 2;
        }

        var vent = grate.AddComponent<VentGrate>();
        vent.number = number;
        vent.network = network;
    }
}
