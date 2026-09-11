using UnityEngine;

// A safety rail around a hole in the floor: nobody can walk into the hole, but shots fly over it. Rails are drawn along
// the top and bottom edges of the BoxCollider2D, which should cover the hole.
// Put this object on the Ignore Raycast layer. Projectiles skip that layer; bodies still bump into it.
[RequireComponent(typeof(BoxCollider2D))]
public class Railing : MonoBehaviour
{
    const float PixelsPerUnit = 20f;
    static readonly Color32 Bar = new Color32(214, 168, 46, 255);
    static readonly Color32 BarShadow = new Color32(120, 92, 26, 255);
    static readonly Color32 Post = new Color32(70, 74, 80, 255);

    public bool railAlongTop = true;
    public bool railAlongBottom = true;

    static Sprite railSprite;

    void Awake()
    {
        var box = GetComponent<BoxCollider2D>();
        if (railAlongTop) AddRail("Rail Top", box, box.size.y * 0.5f);
        if (railAlongBottom) AddRail("Rail Bottom", box, -box.size.y * 0.5f);
    }

    void AddRail(string railName, BoxCollider2D box, float edge)
    {
        var rail = new GameObject(railName).AddComponent<SpriteRenderer>();
        rail.transform.SetParent(transform, false);
        rail.transform.localPosition = box.offset + new Vector2(0f, edge);
        rail.sprite = RailSprite;
        rail.drawMode = SpriteDrawMode.Tiled;
        rail.size = new Vector2(box.size.x, RailSprite.bounds.size.y);
        rail.sortingLayerName = "FloorObject";
        rail.sortingOrder = 8;
    }

    // A post and a length of bar, repeated along the rail.
    static Sprite RailSprite
    {
        get
        {
            if (railSprite == null)
            {
                Texture2D texture = PixelArt.MakeTexture(20, 8, (x, y) =>
                {
                    if (y == 5 || y == 6) return Bar;
                    if (y == 4) return BarShadow;
                    if ((x == 1 || x == 2) && y < 4) return Post;
                    return default;
                });
                railSprite = PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.3f), SpriteMeshType.FullRect);
            }
            return railSprite;
        }
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        Gizmos.color = new Color(1f, 0.75f, 0.2f);
        Gizmos.DrawWireCube(transform.TransformPoint(box.offset), box.size);
    }
}
