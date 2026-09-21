using UnityEngine;

// A mark on the floor from the station coming apart: a scorch where something burned, or a pool of leaked coolant.
// Drawn by WreckageArt when the scene starts, picked and shaped by its position so the level looks the same every time.
// Lies flat on the floor, under everything standing on it.
public class FloorStain : MonoBehaviour
{
    public enum Kind { Any, Scorch, Coolant }

    public Kind kind = Kind.Any;

    private Sprite sprite;

    void Start()
    {
        Vector2 at = transform.position;
        var dice = new System.Random(Mathf.RoundToInt(at.x * 4f) * 73856093 ^ Mathf.RoundToInt(at.y * 4f) * 19349663);
        Kind picked = kind == Kind.Any ? (dice.NextDouble() < 0.6 ? Kind.Scorch : Kind.Coolant) : kind;

        sprite = (picked == Kind.Scorch ? WreckageArt.Scorch(dice) : WreckageArt.CoolantSpill(dice)).ToSprite(new Vector2(0.5f, 0.5f));
        var stain = gameObject.AddComponent<SpriteRenderer>();
        stain.sprite = sprite;
        stain.flipX = dice.NextDouble() < 0.5;
        stain.sortingLayerName = "FloorObject";
        stain.sortingOrder = 2;     // under wreckage lying on the floor (6)
    }

    void OnDestroy() => PixelCanvas.Destroy(sprite);

    // Only drawn in Play mode, so mark it in the Scene view.
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = new Color(0.3f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
