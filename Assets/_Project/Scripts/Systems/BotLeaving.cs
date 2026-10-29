using UnityEngine;

// The bot that dragged the player, done with them: still at first, then rolling off the way it's told, treads turning,
// fading into the dark as it goes, and gone.
public class BotLeaving : MonoBehaviour
{
    const float Speed = 1.6f;
    const float WalkSeconds = 3.2f;
    const float FadeFrom = 0.45f;           // how far through the walk it starts fading

    public Sprite[] frames;

    private SpriteRenderer body;
    private Vector2 heading;
    private float walked = -1f;
    private float frameTimer;
    private int frame;

    public void Go(Vector2 away)
    {
        heading = away.normalized;
        walked = 0f;
    }

    void Awake()
    {
        body = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (body == null || frames == null) return;
        if (walked < 0f) return;

        walked += Time.unscaledDeltaTime;
        transform.position += (Vector3)(heading * Speed * Time.unscaledDeltaTime);

        frameTimer -= Time.unscaledDeltaTime;
        if (frameTimer <= 0f && frames.Length > 1 && frames[1] != null)
        {
            frameTimer = 0.12f;
            frame = 1 - frame;
            body.sprite = frames[frame];
        }

        float fade = Mathf.InverseLerp(WalkSeconds * FadeFrom, WalkSeconds, walked);
        body.color = new Color(1f, 1f, 1f, 1f - fade);
        if (walked >= WalkSeconds) Destroy(gameObject);
    }
}
