using UnityEngine;

// A breaker box hung on a wall: a grey panel with a row of switches, and two status lights that never settle (a green
// one blinking, a red one flickering: BlinkLight). Just dressing. The art's made in code.
public class BreakerBox : MonoBehaviour
{
    const float PixelsPerUnit = 20f;
    const int ArtWidth = 14;
    const int ArtHeight = 16;

    [Tooltip("Where it's drawn: on the wall, over the wall's own tiles.")]
    public string sortingLayer = "On the wall";
    public int sortingOrder = 7;

    void Awake()
    {
        var panel = new GameObject("Panel").AddComponent<SpriteRenderer>();
        panel.transform.SetParent(transform, false);
        panel.sprite = MakePanel();
        panel.sortingLayerName = sortingLayer;
        panel.sortingOrder = sortingOrder;

        AddLight("Status (green)", new Vector2(-3.5f, 5f), new Color(0.35f, 1f, 0.45f), BlinkLight.Pattern.Blink, 0.9f);
        AddLight("Status (red)", new Vector2(3.5f, 5f), new Color(1f, 0.22f, 0.15f), BlinkLight.Pattern.Flicker, 1.6f);
    }

    void AddLight(string lightName, Vector2 pixel, Color color, BlinkLight.Pattern pattern, float period)
    {
        var holder = new GameObject(lightName);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = pixel / PixelsPerUnit;
        holder.SetActive(false);        // set up before it wakes
        var light = holder.AddComponent<BlinkLight>();
        light.pattern = pattern;
        light.color = color;
        light.period = period;
        light.phase = Random.value;
        light.radius = 1.1f;
        light.intensity = 0.4f;
        holder.SetActive(true);
    }

    // A grey box with a dark frame and two rows of switches, centered on this object.
    static Sprite MakePanel()
    {
        var frame = new Color32(22, 22, 24, 255);
        var body = new Color32(84, 88, 92, 255);
        var bodyLit = new Color32(110, 114, 118, 255);
        var slot = new Color32(30, 30, 32, 255);
        var lever = new Color32(170, 170, 160, 255);
        var hazard = new Color32(176, 140, 40, 255);
        Texture2D texture = PixelArt.MakeTexture(ArtWidth, ArtHeight, (x, y) =>
        {
            if (x == 0 || x == ArtWidth - 1 || y == 0 || y == ArtHeight - 1) return frame;
            if (y == 1) return (x / 2) % 2 == 0 ? hazard : frame;
            bool switchRow = (y >= 4 && y <= 6) || (y >= 8 && y <= 10);
            if (switchRow && x >= 2 && x <= 11 && x % 3 != 1) return y == 5 || y == 9 ? lever : slot;
            return x == 1 || y == ArtHeight - 2 ? bodyLit : body;
        });
        return PixelArt.ToSprite(texture, PixelsPerUnit, new Vector2(0.5f, 0.5f));
    }
}
