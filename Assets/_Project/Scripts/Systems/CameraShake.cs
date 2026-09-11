using UnityEngine;
using UnityEngine.Rendering;

// Screen shake. Call CameraShake.Shake(amount) from any script; the main camera gets this component automatically.
// The offset is only applied while the camera is drawing, so gameplay code (aiming at the mouse, the camera
// follow script) always sees the camera's real position and nothing drifts.
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    [Tooltip("How far the camera moves at full shake, in world units.")]
    public float maxOffset = 0.35f;
    [Tooltip("How fast shake dies down. 2.5 means a full-strength shake is gone in 0.4 seconds.")]
    public float recoverySpeed = 2.5f;
    [Tooltip("How jittery the shake is.")]
    public float frequency = 30f;

    private Camera cam;
    private float trauma;
    private Vector3 appliedOffset;
    private bool offsetApplied;

    // amount is 0 to 1 and adds up across hits. The shake is amount squared, so small hits stay subtle.
    public static void Shake(float amount)
    {
        if (amount <= 0f || !TryGetMain(out CameraShake shake)) return;
        shake.trauma = Mathf.Clamp01(shake.trauma + amount);
    }

    // Raises the shake to amount if it's below that, without adding up. Call it every frame to hold a steady rumble.
    public static void ShakeAtLeast(float amount)
    {
        if (amount <= 0f || !TryGetMain(out CameraShake shake)) return;
        shake.trauma = Mathf.Max(shake.trauma, Mathf.Clamp01(amount));
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
    }

    void Update()
    {
        // Unscaled so shake still fades during hit stop.
        trauma = Mathf.MoveTowards(trauma, 0f, recoverySpeed * Time.unscaledDeltaTime);
    }

    void ApplyOffset(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != cam || offsetApplied || trauma <= 0f) return;

        float strength = trauma * trauma * maxOffset;
        float time = Time.unscaledTime * frequency;
        appliedOffset = new Vector3(
            Mathf.PerlinNoise(time, 0.37f) * 2f - 1f,
            Mathf.PerlinNoise(0.71f, time) * 2f - 1f,
            0f) * strength;
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
