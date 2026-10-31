using UnityEngine;

// sparks and rings when something gets hit
[RequireComponent(typeof(Health))]
public class DamageEffects : MonoBehaviour
{
    [Header("On Hit")]
    public Color sparkColor = new Color(1f, 0.9f, 0.45f);
    public Color sparkColorAlt = new Color(1f, 0.45f, 0.1f);
    public int sparkCount = 12;
    public float sparkSpeed = 7f;
    [Tooltip("How wide the spray of sparks is, in degrees.")]
    public float sparkSpread = 80f;
    public Color ringColor = Color.white;
    public float ringSize = 1.1f;

    [Header("On Death")]
    public bool explodeOnDeath = true;
    public Color debrisColor = new Color(0.45f, 0.5f, 0.56f);
    public int debrisCount = 14;
    [Range(0f, 1f)] public float deathShake = 0.5f;
    [Tooltip("Freeze when this dies, in seconds.")]
    public float deathHitStop = 0.1f;
    [Tooltip("Pieces of it thrown out across the floor when it's destroyed. 0 for none.")]
    public int partsOnDeath = 7;
    [Tooltip("Seconds the pieces lie there before they fade.")]
    public float partLifetime = 6f;

    private Health health;
    private Vector2 lastHitDirection = Vector2.up;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    void OnDamaged(DamageInfo info)
    {
        Vector2 direction = info.direction.sqrMagnitude > 0.0001f ? info.direction.normalized : Vector2.up;
        lastHitDirection = direction;

        Vector2 point = info.point ?? (Vector2)transform.position - direction * 0.25f;
        HitEffects.Sparks(point, direction, sparkCount, sparkSpeed, sparkSpread, sparkColor, sparkColorAlt);
        HitEffects.Ring(point, ringColor, ringSize);
    }

    void OnDied()
    {
        if (!explodeOnDeath) return;

        HitEffects.Explosion(transform.position, lastHitDirection, debrisColor, debrisCount);
        if (partsOnDeath > 0) ScrapBurst.Burst(transform.position, partsOnDeath, lastHitDirection, partLifetime);
        CameraShake.Shake(deathShake);
        HitStop.Freeze(deathHitStop);
    }
}
