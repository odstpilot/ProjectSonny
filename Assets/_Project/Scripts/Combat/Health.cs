using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Hit points for enemies and breakable props. Needs a Collider2D on this object or a child.
public class Health : MonoBehaviour, IDamageable
{
    public float maxHealth = 10f;
    public bool destroyOnDeath = true;

    [Header("Hit Feedback")]
    public Color hitFlashColor = new Color(1f, 0.35f, 0.35f);
    public float hitFlashDuration = 0.1f;
    public float knockbackDuration = 0.12f;
    [Range(0f, 1f)] public float knockbackResistance = 0f;

    [Header("Events")]
    public UnityEvent onDamaged = new UnityEvent();
    public UnityEvent onDied = new UnityEvent();

    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private NavMeshAgent agent;
    private Rigidbody2D rb;
    private Coroutine flashRoutine;
    private Coroutine knockbackRoutine;

    void Awake()
    {
        CurrentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead) return;

        CurrentHealth -= info.amount;
        onDamaged.Invoke();

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());

        float distance = info.knockback * (1f - knockbackResistance);
        if (distance > 0f)
        {
            if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
            knockbackRoutine = StartCoroutine(Knockback(info.direction.normalized * distance));
        }

        if (IsDead) Die();
    }

    private void Die()
    {
        // Stop blocking attacks right away, even though the object lingers for the hit flash.
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        onDied.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, hitFlashDuration);
    }

    private IEnumerator Flash()
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = baseColors[i];
    }

    private IEnumerator Knockback(Vector2 offset)
    {
        float elapsed = 0f;
        Vector2 moved = Vector2.zero;

        while (elapsed < knockbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / knockbackDuration);
            Vector2 target = offset * (1f - (1f - t) * (1f - t)); // ease out
            Vector2 step = target - moved;
            moved = target;

            // NavMeshAgent.Move keeps robots on the navmesh instead of shoving them into walls.
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Move(step);
            else if (rb != null)
                rb.position += step;
            else
                transform.position += (Vector3)step;

            yield return null;
        }
    }
}
