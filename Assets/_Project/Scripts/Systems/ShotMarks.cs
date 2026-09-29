using UnityEngine;

// Blaster burns on someone lying where they were shot (CrewAftermath's bodies): a few scorched holes over their suit, each
// a black center with a dull ember ring and a smudge of soot, placed and sized by the body's position so they look the
// same every time the scene loads. Drawn in code when the scene starts, over the sprite on this object.
[RequireComponent(typeof(SpriteRenderer))]
public class ShotMarks : MonoBehaviour
{
    const float PixelsPerUnit = 32f;
    const int Size = 9;

    [Tooltip("How many burns: somewhere between these.")]
    public Vector2Int count = new Vector2Int(2, 4);
    [Tooltip("How far from the middle of the sprite they can be, as a share of its size (the torso, not the edges).")]
    [Range(0f, 1f)] public float spread = 0.45f;

    static Sprite burn;

    void Start()
    {
        var body = GetComponent<SpriteRenderer>();
        if (body.sprite == null) return;
        // One group, so the burns always draw over the suit they're on.
        DepthSort.Group(gameObject);

        Vector2 at = transform.position;
        var dice = new System.Random(Mathf.RoundToInt(at.x * 8f) * 73856093 ^ Mathf.RoundToInt(at.y * 8f) * 19349663);
        int n = dice.Next(count.x, count.y + 1);
        Bounds bounds = body.bounds;
        Vector3 scale = transform.lossyScale;
        for (int i = 0; i < n; i++)
        {
            var mark = new GameObject("Shot Mark").AddComponent<SpriteRenderer>();
            mark.transform.SetParent(transform, false);
            Vector2 offset = new Vector2(((float)dice.NextDouble() * 2f - 1f) * bounds.extents.x * spread,
                ((float)dice.NextDouble() * 2f - 1f) * bounds.extents.y * spread);
            mark.transform.position = new Vector3(bounds.center.x + offset.x, bounds.center.y + offset.y, transform.position.z);
            float size = 0.8f + (float)dice.NextDouble() * 0.5f;
            mark.transform.localScale = new Vector3(size / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), size / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 1f);
            mark.transform.rotation = Quaternion.Euler(0f, 0f, dice.Next(0, 4) * 90f);
            mark.sprite = Burn;
            mark.sortingLayerID = body.sortingLayerID;
            mark.sortingOrder = body.sortingOrder + 1;
        }
    }

    // A scorched hole: black in the middle, a dull ember ring, and soot fading out around it.
    static Sprite Burn
    {
        get
        {
            if (burn != null) return burn;
            var hole = new Color32(12, 10, 10, 255);
            var ember = new Color32(150, 70, 30, 235);
            var soot = new Color32(30, 26, 24, 150);
            float middle = (Size - 1) * 0.5f;
            Texture2D texture = PixelArt.MakeTexture(Size, Size, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(middle, middle));
                if (d < 1.3f) return hole;
                if (d < 2.3f) return ember;
                if (d < 3.6f && (x + y) % 2 == 0) return soot;
                return default;
            });
            return burn = PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.5f));
        }
    }
}
