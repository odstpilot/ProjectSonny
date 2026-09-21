using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Hit points for anything that can be hurt: the player, robots, breakable props.
// Also handles how a hit looks and feels: a solid flash in the sprite's shape, knockback that stops at walls,
// and optional invincibility time with blinking. Needs a Collider2D on this object or a child so attacks can find it.
// Particles for hits and deaths come from DamageEffects.
// Attacks prefer Health over any other damage script on the same object (see Damageable.FromCollider).
public class Health : MonoBehaviour, IDamageable
{
    const float BlinkRate = 12f;
    const float BlinkAlpha = 0.25f;
    const float SkinWidth = 0.02f;

    public float maxHealth = 10f;
    [Tooltip("Hits still flash, knock back, and shake the screen, but no health is lost and nothing can kill it. For the tutorial.")]
    public bool cannotDie;

    [Header("When Hit")]
    [Tooltip("The sprite briefly turns solid this color.")]
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.1f;
    [Tooltip("Seconds after a hit when more damage is ignored. The player should have some; robots usually none.")]
    public float invincibilityDuration = 0f;
    [Tooltip("Blink the sprite while invincible.")]
    public bool blinkWhileInvincible = true;
    public float knockbackDuration = 0.12f;
    [Range(0f, 1f)] public float knockbackResistance = 0f;
    [Tooltip("Sprites that flash and blink. Leave empty to use the sprite on this object, or its children's if it has none.")]
    public SpriteRenderer[] flashRenderers = new SpriteRenderer[0];

    [Header("When Killed")]
    public bool destroyOnDeath = true;
    [Tooltip("Turn off this object's colliders on death so attacks and bodies pass through.")]
    public bool disableCollidersOnDeath = true;

    [Header("Events")]
    public UnityEvent onDamaged = new UnityEvent();
    public UnityEvent onDied = new UnityEvent();

    // For code: the hit that just landed.
    public event System.Action<DamageInfo> Damaged;
    public event System.Action Died;
    // (current, max). Fires on damage, healing, and revive, for health bars.
    public event System.Action<float, float> HealthChanged;

    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;
    public bool IsInvincible => Time.time < invincibleUntil;

    private SpriteRenderer[] renderers;
    private SpriteRenderer[] flashOverlays;   // same order as renderers; null where the flash shader isn't available
    private Color[] baseColors;
    private NavMeshAgent agent;
    private Rigidbody2D rb;
    private float invincibleUntil;
    private Coroutine feedbackRoutine;
    private Coroutine knockbackRoutine;
    private readonly List<Collider2D> collidersDisabledByDeath = new List<Collider2D>();
    private readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>();

    void Awake()
    {
        CurrentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody2D>();

        renderers = flashRenderers.Length > 0 ? flashRenderers : GetComponents<SpriteRenderer>();
        if (renderers.Length == 0) renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;

        // A tint can only darken a sprite, so the flash is a silhouette drawn on top in a child renderer.
        flashOverlays = new SpriteRenderer[renderers.Length];
        Material flashMaterial = CombatSprites.FlashMaterial;
        if (flashMaterial == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            var overlay = new GameObject("HitFlash").AddComponent<SpriteRenderer>();
            overlay.transform.SetParent(renderers[i].transform, false);
            overlay.sharedMaterial = flashMaterial;
            overlay.enabled = false;
            flashOverlays[i] = overlay;
        }
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || IsInvincible || info.amount <= 0f) return;

        if (!cannotDie) CurrentHealth = Mathf.Max(0f, CurrentHealth - info.amount);
        if (invincibilityDuration > 0f)
            invincibleUntil = Time.time + invincibilityDuration;

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(HitFeedback(true));

        float distance = info.knockback * (1f - knockbackResistance);
        if (distance > 0f && info.direction.sqrMagnitude > 0f)
        {
            if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
            knockbackRoutine = StartCoroutine(Knockback(info.direction.normalized * distance));
        }

        Damaged?.Invoke(info);
        onDamaged.Invoke();
        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (IsDead) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    // Back to full health, for respawning. Turns back on any colliders that death turned off.
    public void Revive()
    {
        CurrentHealth = maxHealth;
        invincibleUntil = 0f;
        foreach (Collider2D col in collidersDisabledByDeath)
            if (col != null) col.enabled = true;
        collidersDisabledByDeath.Clear();
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    // Dead at once, whatever health is left and even while invincible: for being caught, crushed, and so on.
    public void Kill(GameObject source = null)
    {
        if (IsDead || cannotDie) return;
        invincibleUntil = 0f;
        TakeDamage(new DamageInfo(CurrentHealth, Vector2.zero, 0f, source));
    }

    // Ignores damage for a while, blinking if blinkWhileInvincible, without the hit flash. For coming back after dying.
    public void MakeInvincible(float seconds)
    {
        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + seconds);
        if (!blinkWhileInvincible) return;
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(HitFeedback(false));
    }

    private void Die()
    {
        if (disableCollidersOnDeath)
        {
            foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            {
                if (!col.enabled) continue;
                col.enabled = false;
                collidersDisabledByDeath.Add(col);
            }
        }

        Died?.Invoke();
        onDied.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, hitFlashDuration);
    }

    // A solid flash in the sprite's shape, then blinking for as long as invincibility lasts.
    private IEnumerator HitFeedback(bool withFlash)
    {
        float flashStart = withFlash ? Time.time : Time.time - hitFlashDuration;
        while (true)
        {
            float flashProgress = hitFlashDuration > 0f ? (Time.time - flashStart) / hitFlashDuration : 1f;
            bool flashing = flashProgress < 1f;
            if (!flashing && !(blinkWhileInvincible && IsInvincible)) break;

            bool faded = !flashing && Mathf.Repeat(Time.time * BlinkRate, 1f) < 0.5f;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sprite = renderers[i];
                if (sprite == null) continue;

                Color color = baseColors[i];
                SpriteRenderer overlay = flashOverlays[i];
                if (overlay != null)
                {
                    overlay.enabled = flashing;
                    if (flashing)
                    {
                        CopySprite(sprite, overlay);
                        // Solid for most of the flash, fading over the last 40%.
                        Color flash = hitFlashColor;
                        flash.a *= Mathf.Clamp01((1f - flashProgress) / 0.4f);
                        overlay.color = flash;
                    }
                }
                else if (flashing)
                {
                    color = hitFlashColor; // no flash shader, so fall back to tinting
                }

                if (faded) color.a *= BlinkAlpha;
                sprite.color = color;
            }
            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].color = baseColors[i];
            if (flashOverlays[i] != null) flashOverlays[i].enabled = false;
        }
        feedbackRoutine = null;
    }

    private static void CopySprite(SpriteRenderer from, SpriteRenderer to)
    {
        to.sprite = from.sprite;
        to.flipX = from.flipX;
        to.flipY = from.flipY;
        to.sortingLayerID = from.sortingLayerID;
        to.sortingOrder = from.sortingOrder + 1;
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
            Push(target - moved);
            moved = target;
            yield return null;
        }
        knockbackRoutine = null;
    }

    // Moves by step without going through walls.
    private void Push(Vector2 step)
    {
        // NavMeshAgent.Move keeps navmesh robots on the navmesh.
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Move(step);
            return;
        }

        float distance = step.magnitude;
        if (distance <= 0f) return;
        Vector2 direction = step / distance;

        if (rb != null && rb.simulated)
        {
            // Stop just short of anything solid in the way.
            var solidOnly = new ContactFilter2D { useTriggers = false };
            if (rb.Cast(direction, solidOnly, castHits, distance + SkinWidth) > 0)
            {
                foreach (RaycastHit2D hit in castHits)
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - SkinWidth));
            }
            rb.position += direction * distance;
        }
        else
        {
            transform.position += (Vector3)(direction * distance);
        }
    }
}
