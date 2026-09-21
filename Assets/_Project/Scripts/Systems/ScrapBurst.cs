using UnityEngine;

// A robot coming apart: pieces of it thrown out across the floor, spinning, skidding to a stop, and lying there a while
// before they fade. DamageEffects sets one off when something is destroyed; the art comes from WreckageArt.
public class ScrapBurst : MonoBehaviour
{
    const float Drag = 4.5f;            // how quickly a piece slides to a stop
    const float SpinDrag = 3.5f;
    const float RestSpeed = 0.35f;      // below this it has settled
    const float FadeTime = 1.2f;

    private Sprite sprite;
    private Vector2 velocity;
    private float spin;
    private float lifetime;
    private float restingFor = -1f;
    private SpriteRenderer piece;

    // count pieces thrown from point, flung hardest along away.
    public static void Burst(Vector2 point, int count, Vector2 away, float lifetime = 6f)
    {
        var dice = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        Vector2 heading = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized;

        for (int i = 0; i < count; i++)
        {
            var scrap = new GameObject("Scrap").AddComponent<ScrapBurst>();
            scrap.transform.position = point + Random.insideUnitCircle * 0.2f;
            scrap.sprite = WreckageArt.RobotPart(dice).ToSprite(new Vector2(0.5f, 0.5f));
            // Mostly the way the hit was going, some of it every which way.
            Vector2 direction = Vector2.Lerp(Random.insideUnitCircle.normalized, heading, Random.Range(0.1f, 0.75f)).normalized;
            scrap.velocity = direction * Random.Range(2.5f, 7f);
            scrap.spin = Random.Range(-720f, 720f);
            scrap.lifetime = lifetime * Random.Range(0.7f, 1.3f);
        }
    }

    void Start()
    {
        piece = gameObject.AddComponent<SpriteRenderer>();
        piece.sprite = sprite;
        piece.sortingLayerName = "Top";     // in the air, over everything
        piece.sortingOrder = 12;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
    }

    void OnDestroy() => PixelCanvas.Destroy(sprite);

    void Update()
    {
        if (restingFor < 0f)
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, spin * Time.deltaTime);
            velocity = Vector2.MoveTowards(velocity, Vector2.zero, Drag * velocity.magnitude * Time.deltaTime);
            spin = Mathf.MoveTowards(spin, 0f, SpinDrag * Mathf.Abs(spin) * Time.deltaTime);

            if (velocity.magnitude > RestSpeed) return;
            // Settled: it lies on the floor with everything else that came down.
            restingFor = 0f;
            piece.sortingLayerName = "FloorObject";
            piece.sortingOrder = 7;
            return;
        }

        restingFor += Time.deltaTime;
        if (restingFor < lifetime) return;

        float fading = (restingFor - lifetime) / FadeTime;
        if (fading >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        piece.color = new Color(1f, 1f, 1f, 1f - fading);
    }
}
