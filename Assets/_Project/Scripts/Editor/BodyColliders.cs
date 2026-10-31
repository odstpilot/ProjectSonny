using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// makes the dead crew solid
public static class BodyColliders
{
    const string BodyName = "Crew Body";
    const float Thickness = 0.5f;
    const float Length = 0.8f;

    public static void MakeSolidInOpenScene()
    {
        int made = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == BodyName && MakeSolid(t.gameObject)) made++;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Made {made} bodies solid.");
    }

    public static void MakeChapterTwoSolid()
    {
        foreach (string scene in new[] { "Chapter2", "Chapter3" })
        {
            EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Levels/{scene}.unity");
            MakeSolidInOpenScene();
            EditorSceneManager.SaveOpenScenes();
        }
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
