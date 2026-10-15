using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Sonny > Make Bodies Solid (in the open scene). ChapterOneBuilder does it for every body it lays out (MakeBody).
//
// The crew lying where they fell in Chapter 2 are solid: the player and the bots walk round them, not through. Each
// body's own box collider is switched back on and fitted over the body lying down (the sprite on its side, so the
// collider's length runs along the sprite's height). Its rigidbody is kinematic, so nothing can shove it about. Bots
// still see past them (PlaceholderRobot.HasLineOfSight): they're on the floor.
public static class BodyColliders
{
    const string BodyName = "Crew Body";
    // Of the sprite: how much of its width is the thickness of the body, and how much of its height its length.
    const float Thickness = 0.5f;
    const float Length = 0.8f;

    [MenuItem("Sonny/Make Bodies Solid")]
    public static void MakeSolidInOpenScene()
    {
        int made = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == BodyName && MakeSolid(t.gameObject)) made++;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Made {made} bodies solid.");
    }

    // For a batch run: Chapter 2, where the bodies are, opened, done, and saved.
    public static void MakeChapterTwoSolid()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Levels/Chapter2.unity");
        MakeSolidInOpenScene();
        EditorSceneManager.SaveOpenScenes();
    }

    public static bool MakeSolid(GameObject body)
    {
        var box = body.GetComponent<BoxCollider2D>();
        var sprite = body.GetComponent<SpriteRenderer>();
        if (box == null || sprite == null || sprite.sprite == null) return false;

        Bounds local = sprite.sprite.bounds;
        box.enabled = true;
        box.isTrigger = false;
        box.size = new Vector2(local.size.x * Thickness, local.size.y * Length);
        box.offset = local.center;
        if (body.TryGetComponent(out Rigidbody2D rigidbody)) rigidbody.bodyType = RigidbodyType2D.Kinematic;
        EditorUtility.SetDirty(box);
        return true;
    }
}
