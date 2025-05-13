using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Ghost : MonoBehaviour
{
    public Transform player;
    public float speed = 2f;
    public float fadeRate = 0.2f;

    private SpriteRenderer spriteRenderer;
    private bool isInFlashlight = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        GetComponent<Collider2D>().isTrigger = true;

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null)
            {
                player = found.transform;
            }
        }

        // Spawn at a random point far from player
        if (player != null)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized * Random.Range(8f, 12f);
            transform.position = player.position + new Vector3(randomDir.x, randomDir.y, 0);
            Debug.Log("[Ghost] Spawned far from player.");
        }
    }

    void Update()
    {
        if (player != null)
        {
            Vector3 dir = (player.position - transform.position).normalized;
            transform.position += dir * speed * Time.deltaTime;
        }

        Color color = spriteRenderer.color;
        if (isInFlashlight)
        {
            color.a -= fadeRate * Time.deltaTime;
            color.a = Mathf.Clamp01(color.a);
            spriteRenderer.color = color;

            if (color.a <= 0f)
            {
                Debug.Log("[Ghost] Destroyed by flashlight.");
                Destroy(gameObject);
            }
        }

        isInFlashlight = false;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Light"))
        {
            isInFlashlight = true;
            Debug.Log("[Ghost] In flashlight.");
        }
    }
}