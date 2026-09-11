using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// What happens to the player through the new Health system: on each hit the screen shakes, time freezes for a
// moment, and a red vignette flashes at the screen edges (and keeps pulsing while health is low). On death the
// player freezes, then respawns where they started with full health. Also shows a temporary HP readout.
// PlayerController's old hp and death screen are left as they were.
[RequireComponent(typeof(Health))]
public class PlayerHealthHandler : MonoBehaviour
{
    [Tooltip("Screen shake on each hit, 0 to 1.")]
    [Range(0f, 1f)] public float hitShake = 0.6f;
    [Tooltip("Freeze on each hit, in seconds.")]
    public float hitStop = 0.08f;
    [Tooltip("Seconds frozen after dying before respawning.")]
    public float respawnDelay = 1.5f;
    public bool showDebugReadout = true;

    [Header("Hurt Vignette")]
    public Color vignetteColor = new Color(0.75f, 0f, 0f);
    [Tooltip("How strong the vignette flashes on a hit, 0 to 1.")]
    [Range(0f, 1f)] public float hitVignette = 0.7f;
    public float vignetteFadeTime = 0.5f;
    [Tooltip("At or below this much health, the vignette pulses.")]
    public float lowHealthThreshold = 1f;
    [Range(0f, 1f)] public float lowHealthVignette = 0.35f;

    private Health health;
    private Rigidbody2D rb;
    private Vector3 spawnPosition;
    private float vignetteAlpha;
    private readonly List<Behaviour> frozen = new List<Behaviour>();
    private GUIStyle readoutStyle;

    void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        spawnPosition = transform.position;
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

    void Update()
    {
        // Real time, so the vignette keeps fading during hit stop.
        float fadeSpeed = hitVignette / Mathf.Max(0.01f, vignetteFadeTime);
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
    }

    void OnDamaged(DamageInfo info)
    {
        CameraShake.Shake(hitShake);
        HitStop.Freeze(hitStop);
        vignetteAlpha = Mathf.Max(vignetteAlpha, hitVignette);
    }

    void OnDied()
    {
        CameraShake.Shake(1f);
        vignetteAlpha = 1f;
        StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        SetFrozen(true);
        yield return new WaitForSeconds(respawnDelay);

        transform.position = spawnPosition;
        if (rb != null)
        {
            rb.position = spawnPosition;
            rb.linearVelocity = Vector2.zero;
        }
        health.Revive();
        SetFrozen(false);
    }

    // Turns off movement, combat, and animation, remembering what was on so exactly that comes back.
    void SetFrozen(bool freeze)
    {
        if (!freeze)
        {
            foreach (Behaviour behaviour in frozen)
                if (behaviour != null) behaviour.enabled = true;
            frozen.Clear();
            return;
        }

        foreach (Behaviour behaviour in GetComponents<Behaviour>())
        {
            bool isScriptOrAnimator = behaviour is MonoBehaviour || behaviour is Animator;
            if (!isScriptOrAnimator || behaviour == this || behaviour == health || !behaviour.enabled) continue;

            behaviour.enabled = false;
            frozen.Add(behaviour);
        }
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OnGUI()
    {
        if (health == null) return;

        if (Event.current.type == EventType.Repaint)
            DrawVignette();

        if (!showDebugReadout) return;
        if (readoutStyle == null)
            readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };

        string text = health.IsDead
            ? "DEAD"
            : $"HP {health.CurrentHealth:0.#} / {health.maxHealth:0.#}{(health.IsInvincible ? "  (invincible)" : "")}";
        GUI.Label(new Rect(16f, 12f, 400f, 32f), text, readoutStyle);
    }

    void DrawVignette()
    {
        float alpha = vignetteAlpha;
        if (!health.IsDead && health.CurrentHealth <= lowHealthThreshold)
        {
            // A slow pulse while close to death.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.1f);
            alpha = Mathf.Max(alpha, lowHealthVignette * (0.4f + 0.6f * pulse));
        }
        if (alpha <= 0.001f) return;

        Color previous = GUI.color;
        GUI.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), CombatSprites.VignetteTexture, ScaleMode.StretchToFill);
        GUI.color = previous;
    }
}
