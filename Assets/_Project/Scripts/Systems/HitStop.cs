using UnityEngine;

// Time slowed for a moment to make a hit land: HitStop.Freeze(0.05f) for a split-second stop, or HitStop.Hold(0.25f, 0.6f)
// to run everything at a quarter speed for a longer beat, like the last robot of a fight going down. It eases back up to
// normal at the end. Counts real time, so it ends on schedule however slowly the game is running, and it never overrides
// a pause menu or anything else that has changed the time scale.
public class HitStop : MonoBehaviour
{
    public const float FrozenTimeScale = 0.05f;
    const float EaseBackTime = 0.25f;   // real seconds spent coming back up to speed

    static HitStop instance;

    private float resumeAt;
    private float scale = 1f;
    private bool holding;

    public static void Freeze(float duration) => Hold(FrozenTimeScale, duration);

    // Runs the game at scale (0 to 1) for realSeconds. Overlapping calls take the slower of the two and the later end,
    // rather than stacking.
    public static void Hold(float timeScale, float realSeconds)
    {
        if (realSeconds <= 0f || !GameSettings.FreezeOnHit) return;

        bool alreadyHolding = instance != null && instance.holding;
        if (!alreadyHolding && Time.timeScale < 1f) return; // paused, or something else is running time slowly

        if (instance == null)
            instance = new GameObject("[HitStop]").AddComponent<HitStop>();

        instance.scale = alreadyHolding ? Mathf.Min(instance.scale, timeScale) : Mathf.Clamp01(timeScale);
        instance.resumeAt = Mathf.Max(alreadyHolding ? instance.resumeAt : 0f, Time.unscaledTime + realSeconds);
        instance.holding = true;
        Time.timeScale = instance.scale;
    }

    void Update()
    {
        if (!holding) return;

        float left = resumeAt - Time.unscaledTime;
        if (left <= 0f)
        {
            Release();
            return;
        }

        // Eased back up over the last moment, so time doesn't snap back. If something else has taken the time scale
        // lower still (a pause menu), leave it alone.
        float wanted = Mathf.Lerp(1f, scale, Mathf.Clamp01(left / EaseBackTime));
        if (Time.timeScale > 0f && Time.timeScale <= wanted + 0.001f) Time.timeScale = wanted;
    }

    void OnDisable()
    {
        if (holding) Release();
    }

    void Release()
    {
        holding = false;
        // Only undo our own change; if a pause menu stopped time meanwhile, leave it alone.
        if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
    }
}
