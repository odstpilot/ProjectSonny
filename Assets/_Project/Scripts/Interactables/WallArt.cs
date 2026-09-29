using UnityEngine;

// A picture or poster hung on a wall face: the kind of thing a crew puts up to make a station feel lived in. Each is
// drawn in code until it has art of its own. With an Inspectable beside it (ChapterOneBuilder), the technician has
// something to say about it.
// Its position is the middle of the picture, on the wall.
[ExecuteAlways]
public class WallArt : MonoBehaviour
{
    public enum Kind { EarthPhoto, Mountains, SafetyPoster, CompanyPoster, CrewPhoto, KidsDrawing, Chart }

    const float PixelsPerUnit = 16f;

    public Kind kind;
    [Tooltip("Knocked crooked, and cracked across (Chapter 2).")]
    public bool damaged;

    private SpriteRenderer art;

    void OnEnable()
    {
        if (art == null)
        {
            Transform found = transform.Find("Picture");
            art = found != null ? found.GetComponent<SpriteRenderer>() : null;
        }
        if (art == null)
        {
            var picture = new GameObject("Picture") { hideFlags = HideFlags.DontSave };
            picture.transform.SetParent(transform, false);
            art = picture.AddComponent<SpriteRenderer>();
            art.sortingLayerName = "Collision";
            art.sortingOrder = 3;
        }
        art.sprite = Make(kind, damaged);
        art.transform.localRotation = Quaternion.Euler(0f, 0f, damaged ? -9f : 0f);
    }

    static Sprite Make(Kind kind, bool damaged)
    {
        var frame = new Color32(52, 46, 40, 255);
        var paper = new Color32(232, 226, 206, 255);
        var ink = new Color32(40, 40, 52, 255);
        var hazard = new Color32(230, 180, 60, 255);
        var red = new Color32(200, 70, 60, 255);
        var sky = new Color32(90, 150, 210, 255);
        var space = new Color32(10, 12, 26, 255);
        var ocean = new Color32(40, 90, 180, 255);
        var land = new Color32(70, 150, 80, 255);
        var cloud = new Color32(240, 244, 250, 255);
        var rock = new Color32(110, 104, 120, 255);
        var snow = new Color32(245, 245, 250, 255);
        var lake = new Color32(60, 120, 170, 255);
        var skin = new Color32(214, 170, 130, 255);
        var suit = new Color32(90, 110, 130, 255);
        var crayonB = new Color32(60, 90, 230, 255);
        var crayonY = new Color32(250, 210, 40, 255);
        var green = new Color32(80, 190, 90, 255);

        bool poster = kind == Kind.SafetyPoster || kind == Kind.CompanyPoster || kind == Kind.KidsDrawing || kind == Kind.Chart;
        int w = poster ? 12 : 14, h = poster ? 15 : 11;
        Texture2D texture = PixelArt.MakeTexture(w, h, (x, y) =>
        {
            // A crack from corner to corner.
            if (damaged && Mathf.Abs((x - 1) - (h - 2 - y) * (w - 2f) / (h - 2f)) < 0.6f && x > 0 && y > 0 && x < w - 1 && y < h - 1)
                return new Color32(200, 205, 215, 255);
            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
            switch (kind)
            {
                case Kind.EarthPhoto:
                {
                    if (edge) return frame;
                    float dx = (x - 6.5f) / 4.2f, dy = (y - 5f) / 4.2f;
                    float r = dx * dx + dy * dy;
                    if (r > 1f) return (x * 7 + y * 3) % 11 == 0 ? cloud : space;
                    if ((x + y * 2) % 7 == 0) return cloud;
                    return (x * 3 + y * 5) % 4 == 0 || (x > 5 && y < 5) ? land : ocean;
                }
                case Kind.Mountains:
                {
                    if (edge) return frame;
                    int peak = 9 - Mathf.Abs(x - 4) * 2, peak2 = 8 - Mathf.Abs(x - 10) * 2;
                    int top = Mathf.Max(peak, peak2);
                    if (y <= 2) return lake;
                    if (y <= top) return y >= top - 1 && top > 5 ? snow : rock;
                    return sky;
                }
                case Kind.SafetyPoster:
                    if (edge) return hazard;
                    if (y >= 11 && y <= 12 && x >= 2 && x <= 9) return ink;                  // a heading
                    if (y >= 4 && y <= 9 && x >= 4 && x <= 7) return y >= 8 ? skin : red;   // someone in a hard hat
                    if (y == 2 && x >= 2 && x <= 9 && x % 2 == 0) return ink;
                    return paper;
                case Kind.CompanyPoster:
                    if (edge) return frame;
                    {
                        float dx = (x - 5.5f) / 3.5f, dy = (y - 8.5f) / 3.5f;
                        if (dx * dx + dy * dy <= 1f) return hazard;                         // the sun
                    }
                    if (y >= 2 && y <= 4 && x >= 2 && x <= 9) return y == 3 ? paper : sky;
                    return space;
                case Kind.CrewPhoto:
                    if (edge) return frame;
                    if (y <= 5 && (x == 3 || x == 6 || x == 9 || x == 11) ) return suit;
                    if (y >= 6 && y <= 7 && (x == 3 || x == 6 || x == 9 || x == 11)) return skin;
                    return y <= 2 ? rock : new Color32(170, 180, 190, 255);
                case Kind.KidsDrawing:
                    if (edge) return paper;
                    if (x >= 7 && x <= 10 && y >= 10 && y <= 13) return crayonY;           // the sun
                    if (x == 3 && y >= 3 && y <= 7) return crayonB;                         // someone
                    if (x >= 2 && x <= 4 && y == 8) return crayonB;
                    if (y == 1) return green;
                    return paper;
                default:
                    if (edge) return frame;
                    // A line chart going up, then sharply up.
                    int line = x < 8 ? 2 + x / 2 : 2 + x / 2 + (x - 7) * 2;
                    if (y == Mathf.Min(line, h - 2)) return red;
                    if (x == 1 || y == 1) return ink;
                    return paper;
            }
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }
}
