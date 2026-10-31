using UnityEngine;
using UnityEngine.Rendering;

// screen shake
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    const float KickStiffness = 220f;
    const float KickDamping = 18f;
    const float KickStrength = 30f;

    [Tooltip("How far the camera moves at full shake, in world units.")]
    public float maxOffset = 0.35f;
    [Tooltip("How fast shake dies down. 2.5 means a full-strength shake is gone in 0.4 seconds.")]
    public float recoverySpeed = 2.5f;
    [Tooltip("How jittery the shake is.")]
    public float frequency = 30f;

    private Camera cam;
    private float trauma;
    private Vector2 kickOffset;
    private Vector2 kickVelocity;
    private Vector3 appliedOffset;
    private bool offsetApplied;

    public static void Shake(float amount)
    {
        if (amount <= 0f || !TryGetMain(out CameraShake shake)) return;
        shake.trauma = Mathf.Clamp01(shake.trauma + amount);
    }

    public static void ShakeAtLeast(float amount)
    {
        if (amount <= 0f || !TryGetMain(out CameraShake shake)) return;
        shake.trauma = Mathf.Max(shake.trauma, Mathf.Clamp01(amount));
    }

    public static void Kick(Vector2 direction, float distance)
    {
        if (distance <= 0f || direction.sqrMagnitude < 0.0001f || !TryGetMain(out CameraShake shake)) return;
        shake.kickVelocity += direction.normalized * distance * KickStrength;
    }

    static bool TryGetMain(out CameraShake shake)
    {
        shake = null;
        Camera main = Camera.main;
        if (main == null) return false;

        if (!main.TryGetComponent(out shake))
            shake = main.gameObject.AddComponent<CameraShake>();
        return true;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += ApplyOffset;
        RenderPipelineManager.endCameraRendering += RemoveOffset;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= ApplyOffset;
        RenderPipelineManager.endCameraRendering -= RemoveOffset;
        trauma = 0f;
        kickOffset = kickVelocity = Vector2.zero;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        trauma = Mathf.MoveTowards(trauma, 0f, recoverySpeed * dt);

        kickVelocity += (-KickStiffness * kickOffset - KickDamping * kickVelocity) * dt;
        kickOffset += kickVelocity * dt;
        if (kickOffset.sqrMagnitude < 0.000001f && kickVelocity.sqrMagnitude < 0.0001f)
            kickOffset = kickVelocity = Vector2.zero;
    }

    void ApplyOffset(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != cam || offsetApplied) return;
        if (trauma <= 0f && kickOffset == Vector2.zero) return;
        if (GameSettings.ScreenShake <= 0f) return;

        Vector3 shake = Vector3.zero;
        if (trauma > 0f)
        {
            float strength = trauma * trauma * maxOffset;
            float time = Time.unscaledTime * frequency;
            shake = new Vector3(
                Mathf.PerlinNoise(time, 0.37f) * 2f - 1f,
                Mathf.PerlinNoise(0.71f, time) * 2f - 1f,
                0f) * strength;
        }

        appliedOffset = (shake + (Vector3)kickOffset) * GameSettings.ScreenShake;
        transform.position += appliedOffset;
        offsetApplied = true;
    }

    void RemoveOffset(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != cam || !offsetApplied) return;

        transform.position -= appliedOffset;
        offsetApplied = false;
    }
}
