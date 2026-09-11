using UnityEngine;

// Particles for when this object's Health is hit or killed: sparks spraying away from the attacker and a ring at
// the impact point, plus an optional burst of debris and smoke on death. The flash, blink, and knockback are
// handled by Health itself.
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

        // If the attacker didn't say where it hit, use the side facing the attacker.
        Vector2 point = info.point ?? (Vector2)transform.position - direction * 0.25f;
        HitEffects.Sparks(point, direction, sparkCount, sparkSpeed, sparkSpread, sparkColor, sparkColorAlt);
        HitEffects.Ring(point, ringColor, ringSize);
    }

    void OnDied()
    {
        if (!explodeOnDeath) return;

        HitEffects.Explosion(transform.position, lastHitDirection, debrisColor, debrisCount);
        CameraShake.Shake(deathShake);
        HitStop.Freeze(deathHitStop);
    }
}
