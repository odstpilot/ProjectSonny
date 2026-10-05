using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// The view closing in and easing back out with the fighting. With robots after the player (hunting them, or close and
// awake), it eases in a little, tightening up; one destroyed, it punches in and springs back out, and once they're all
// gone it settles back out to the normal view. Set pieces can hold it in closer for a while (Hold, Release: the wall
// grab). The main camera gets this component automatically.
// Like CameraShake, it's only applied while the camera is drawing, so everything that sets the camera's size itself
// (cutscenes, dying) and everything that reads it (the camera follow script) sees the real size. Anything that puts UI
// over a point in the world goes through ToScreen, so it lands where that point is drawn.
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    const float EngagedZoom = 0.9f;         // the view's size with robots after the player
    const float EngagedRange = 9f;          // how close an awake robot has to be to count, hunting or not
    const float EaseSpeed = 2.2f;           // how fast it eases toward the zoom it wants
    const float KillPunch = 0.1f;           // how far in it punches on a kill
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

    // How much smaller the view is drawn than the camera's own size, right now: 1 for not zoomed.
    public float Factor
    {
        get
        {
            float held = 1f;
            foreach (float factor in holds.Values) held = Mathf.Min(held, factor);
            // The fighting's zoom goes with the screen shake setting; a set piece's hold doesn't.
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

    // Makes sure each scene's main camera has one, so it starts watching for robots without anything else calling it.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryGetMain(out _);

    static void OnAnyDied(Health health)
    {
        if (health == null || health.CompareTag("Player")) return;
        if (health.GetComponent<PlaceholderRobot>() == null && health.GetComponent<Warden>() == null && health.GetComponent<MaintenanceBot>() == null) return;
        Punch(KillPunch);
    }

    // A quick push in that springs back out.
    public static void Punch(float amount)
    {
        if (TryGetMain(out CameraZoom zoom)) zoom.punchVelocity += amount * PunchStiffness * 0.12f;
    }

    // Holds the view in to this fraction of its size until Release with the same key (the closest hold wins).
    public static void Hold(object key, float factor)
    {
        if (TryGetMain(out CameraZoom zoom)) zoom.holds[key] = factor;
    }

    public static void Release(object key)
    {
        if (TryGetMain(out CameraZoom zoom)) zoom.holds.Remove(key);
    }

    // Where a point in the world is drawn on screen, zoom included.
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

    // Robots after the player: hunting them, or awake and close by.
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
