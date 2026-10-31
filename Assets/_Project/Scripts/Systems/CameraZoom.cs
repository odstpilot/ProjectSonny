using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// camera zooms in during fights
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    const float EngagedZoom = 0.9f;
    const float EngagedRange = 9f;
    const float EaseSpeed = 2.2f;
    const float KillPunch = 0.1f;
    const float PunchStiffness = 120f;
    const float PunchDamping = 14f;
    const float ScanEvery = 0.25f;

    private Camera cam;
    private float engaged = 1f, engagedTarget = 1f;
    private float punch, punchVelocity;
    private float scanTimer;
    private float savedSize;
    private bool applied;
    private readonly Dictionary<object, float> holds = new Dictionary<object, float>();

    public float Factor
    {
        get
        {
            float held = 1f;
            foreach (float factor in holds.Values) held = Mathf.Min(held, factor);
            float motion = GameSettings.ScreenShake;
            return Mathf.Clamp((1f - (1f - engaged) * motion) * held * (1f - punch * motion), 0.4f, 1.2f);
        }
    }

    static bool TryGetMain(out CameraZoom zoom)
    {
        zoom = null;
        Camera main = Camera.main;
        if (main == null) return false;
        if (!main.TryGetComponent(out zoom)) zoom = main.gameObject.AddComponent<CameraZoom>();
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Listen()
    {
        Health.AnyDied -= OnAnyDied;
        Health.AnyDied += OnAnyDied;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryGetMain(out _);

    static void OnAnyDied(Health health)
    {
        if (health == null || health.CompareTag("Player")) return;
        if (health.GetComponent<PlaceholderRobot>() == null && health.GetComponent<Warden>() == null && health.GetComponent<MaintenanceBot>() == null) return;
        Punch(KillPunch);
    }

    public static void Punch(float amount)
    {
        if (TryGetMain(out CameraZoom zoom)) zoom.punchVelocity += amount * PunchStiffness * 0.12f;
    }

    public static void Hold(object key, float factor)
    {
        if (TryGetMain(out CameraZoom zoom)) zoom.holds[key] = factor;
    }

    public static void Release(object key)
    {
        if (TryGetMain(out CameraZoom zoom)) zoom.holds.Remove(key);
    }

    public static Vector3 ToScreen(Camera cam, Vector3 worldPoint)
    {
        Vector3 screen = cam.WorldToScreenPoint(worldPoint);
        if (!cam.TryGetComponent(out CameraZoom zoom)) return screen;
        float factor = zoom.Factor;
        if (Mathf.Approximately(factor, 1f)) return screen;
        var middle = new Vector3(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f, 0f);
        Vector3 offset = (screen - middle) / factor;
        return new Vector3(middle.x + offset.x, middle.y + offset.y, screen.z);
    }

    void Awake() => cam = GetComponent<Camera>();

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += ApplyZoom;
        RenderPipelineManager.endCameraRendering += RemoveZoom;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= ApplyZoom;
        RenderPipelineManager.endCameraRendering -= RemoveZoom;
        holds.Clear();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        scanTimer -= dt;
        if (scanTimer <= 0f)
        {
            scanTimer = ScanEvery;
            engagedTarget = Engaged() ? EngagedZoom : 1f;
        }
        engaged = Mathf.Lerp(engaged, engagedTarget, 1f - Mathf.Exp(-EaseSpeed * dt));

        punchVelocity += (-PunchStiffness * punch - PunchDamping * punchVelocity) * dt;
        punch += punchVelocity * dt;
        if (Mathf.Abs(punch) < 0.0005f && Mathf.Abs(punchVelocity) < 0.005f) punch = punchVelocity = 0f;
    }

    static bool Engaged()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return false;
        Vector2 at = player.transform.position;
        foreach (PlaceholderRobot robot in PlaceholderRobot.All)
        {
            if (robot == null || !robot.isActiveAndEnabled || robot.blind || robot.IsShorted) continue;
            if (robot.TryGetComponent(out Health health) && health.IsDead) continue;
            if (robot.IsHunting || Vector2.Distance(robot.transform.position, at) < EngagedRange) return true;
        }
        return false;
    }

    void ApplyZoom(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != cam || applied || !cam.orthographic) return;
        float factor = Factor;
        if (Mathf.Approximately(factor, 1f)) return;
        savedSize = cam.orthographicSize;
        cam.orthographicSize = savedSize * factor;
        applied = true;
    }

    void RemoveZoom(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != cam || !applied) return;
        cam.orthographicSize = savedSize;
        applied = false;
    }
}
