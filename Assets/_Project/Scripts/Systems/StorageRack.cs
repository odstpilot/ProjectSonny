using System.Collections.Generic;
using UnityEngine;

// One cell of steel storage racking, a cell deep and taller than a person, stacked with boxes, crates, and cases. Laid
// out by ChapterOneBuilder into aisles across the upper maintenance deck (PlaceRacks); each cell knows which of its
// neighbours are racks too, so a run of them reads as one long rack: uprights only at the ends, the shelf face only
// along the front (front), the top seen from above running on into the next. It sorts like the furniture, from
// DepthSort.FeetToCenter above its base, so whoever walks behind it is hidden behind it.
// Its collider is its footprint; it's solid, and blocks what the bots and cameras can see. The art is made in code
// until it has its own.
public class StorageRack : MonoBehaviour
{
    public const int Variants = 6;
    const float PixelsPerUnit = 16f;
    const int Size = 16;
    const int FaceHeight = 24;          // taller than the technician

    [Tooltip("The open floor is below it on screen: its shelves face that way.")]
    public bool front = true;
    [Tooltip("The open floor is above it on screen: the top's back edge shows.")]
    public bool back = true;
    public bool leftEnd = true;
    public bool rightEnd = true;
    [Tooltip("Which boxes are on it.")]
    public int variant;

    static readonly Dictionary<int, Sprite> tops = new Dictionary<int, Sprite>();
    static readonly Dictionary<int, Sprite> faces = new Dictionary<int, Sprite>();

    static readonly Color32 Outline = new Color32(22, 24, 30, 255);
    static readonly Color32 Steel = new Color32(104, 110, 122, 255);
    static readonly Color32 SteelLit = new Color32(150, 158, 170, 255);
    static readonly Color32 Shadow = new Color32(30, 32, 38, 255);
    static readonly Color32 BackPanel = new Color32(42, 44, 52, 255);
    static readonly Color32 TopSteel = new Color32(70, 74, 84, 255);
    static readonly Color32[] BoxColors =
    {
        new Color32(164, 120, 74, 255),     // cardboard
        new Color32(140, 98, 60, 255),
        new Color32(74, 108, 150, 255),     // blue crate
        new Color32(112, 116, 126, 255),    // grey case
        new Color32(186, 150, 60, 255),     // hazard yellow
        new Color32(96, 132, 92, 255),      // green crate
    };

    void Awake()
    {
        DepthSort.Group(gameObject);
        int key = Key();
        var top = new GameObject("Top").AddComponent<SpriteRenderer>();
        top.transform.SetParent(transform, false);
        top.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter + FaceHeight / PixelsPerUnit, 0f);
        top.sprite = tops.TryGetValue(key, out Sprite made) && made != null ? made : (tops[key] = MakeTop());
        if (!front) return;
        var face = new GameObject("Face").AddComponent<SpriteRenderer>();
        face.transform.SetParent(transform, false);
        face.transform.localPosition = new Vector3(0f, -DepthSort.FeetToCenter, 0f);
        face.sprite = faces.TryGetValue(key, out made) && made != null ? made : (faces[key] = MakeFace());
    }

    int Key() => variant * 16 + (front ? 1 : 0) + (back ? 2 : 0) + (leftEnd ? 4 : 0) + (rightEnd ? 8 : 0);

    // A little hash, so the same variant always gets the same boxes.
    int Roll(int a, int b) => Mathf.Abs((variant * 7919 + a * 104729 + b * 1299709) ^ (a * 31 + b * 17 + variant)) % 1000;

    // Seen from above: the shelf top, with the tops of boxes on it, and its edges where it ends.
    Sprite MakeTop()
    {
        var boxes = new List<RectInt>();
        for (int k = 0; k < 3; k++)
        {
            if (Roll(k, 1) < 300) continue;
            int w = 4 + Roll(k, 2) % 5, h = 4 + Roll(k, 3) % 5;
            boxes.Add(new RectInt(1 + Roll(k, 4) % (Size - w - 1), 2 + Roll(k, 5) % (Size - h - 3), w, h));
        }
        Texture2D texture = PixelArt.MakeTexture(Size, Size, (x, y) =>
        {
            if ((leftEnd && x == 0) || (rightEnd && x == Size - 1) || (back && y == Size - 1)) return Outline;
            if (front && y == 0) return SteelLit;                                   // the lip over the front
            if ((leftEnd && x == 1) || (rightEnd && x == Size - 2)) return Steel;   // the uprights' tops
            for (int k = 0; k < boxes.Count; k++)
            {
                RectInt box = boxes[k];
                if (!box.Contains(new Vector2Int(x, y))) continue;
                Color32 color = BoxColors[Roll(k, 6) % BoxColors.Length];
                if (x == box.xMin || y == box.yMin) return Darker(color, 0.65f);
                if (y == box.yMax - 1) return Lighter(color);
                // A strip of tape across cardboard.
                return Roll(k, 6) % BoxColors.Length < 2 && x == box.xMin + box.width / 2 ? Lighter(color) : color;
            }
            return (x + y * 3) % 7 == 0 ? Darker(TopSteel, 0.85f) : TopSteel;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }

    // The front: uprights at the ends, three shelves with things on them, and the floor shelf.
    Sprite MakeFace()
    {
        int[] boards = { 0, 8, 16 };
        Texture2D texture = PixelArt.MakeTexture(Size, FaceHeight, (x, y) =>
        {
            if (y == FaceHeight - 1) return Outline;
            if ((leftEnd && x == 0) || (rightEnd && x == Size - 1)) return Outline;
            if ((leftEnd && x == 1) || (rightEnd && x == Size - 2)) return y % 4 == 0 ? Shadow : Steel;
            foreach (int board in boards)
            {
                if (y == board || y == board + 1) return y == board + 1 ? SteelLit : Steel;
                if (y == board + 2) return Shadow;
            }
            // What's on the shelf this row's on: a few boxes standing on the board below.
            int shelf = y / 8, above = y - shelf * 8 - 3;       // rows up from the shadow line
            for (int k = 0; k < 3; k++)
            {
                if (Roll(shelf, k) < 250) continue;
                int left = 2 + k * 4 + Roll(shelf, k + 10) % 2, width = 3 + Roll(shelf, k + 20) % 2, height = 2 + Roll(shelf, k + 30) % 4;
                if (x < left || x >= left + width || above < 0 || above >= height) continue;
                Color32 color = BoxColors[Roll(shelf, k + 40) % BoxColors.Length];
                if (x == left) return Darker(color, 0.7f);
                return above == height - 1 ? Lighter(color) : color;
            }
            return BackPanel;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0f));
    }

    static Color32 Darker(Color32 c, float by) => new Color32((byte)(c.r * by), (byte)(c.g * by), (byte)(c.b * by), 255);
    static Color32 Lighter(Color32 c) => new Color32((byte)Mathf.Min(255, c.r + 40), (byte)Mathf.Min(255, c.g + 40), (byte)Mathf.Min(255, c.b + 40), 255);
}
