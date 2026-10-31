using UnityEngine;

// scorch or leak mark on the floor
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
        stain.sortingOrder = 2;
    }

    void OnDestroy() => PixelCanvas.Destroy(sprite);

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = new Color(0.3f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
