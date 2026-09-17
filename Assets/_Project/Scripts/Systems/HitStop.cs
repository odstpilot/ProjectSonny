using UnityEngine;

// A split-second freeze that makes hits feel heavy: HitStop.Freeze(0.05f) from any script.
// Counts real time, so it ends on schedule even though the game is nearly stopped, and it never
// overrides a pause menu or anything else that has changed the time scale.
public class HitStop : MonoBehaviour
{
    public const float FrozenTimeScale = 0.05f;

    static HitStop instance;

    private float resumeAt;
    private bool frozen;

    public static void Freeze(float duration)
    {
        if (duration <= 0f || !GameSettings.FreezeOnHit) return;

        bool alreadyFrozen = instance != null && instance.frozen;
        if (!alreadyFrozen && Time.timeScale < 1f) return; // paused or in slow motion

        if (instance == null)
            instance = new GameObject("[HitStop]").AddComponent<HitStop>();

        // Overlapping hits extend the freeze instead of stacking.
        instance.resumeAt = Mathf.Max(alreadyFrozen ? instance.resumeAt : 0f, Time.unscaledTime + duration);
        instance.frozen = true;
        Time.timeScale = FrozenTimeScale;
    }

    void Update()
    {
        if (frozen && Time.unscaledTime >= resumeAt)
            Release();
    }

    void OnDisable()
    {
        if (frozen) Release();
    }

    void Release()
    {
        frozen = false;
        // Only undo our own change; if a pause menu set the time scale meanwhile, leave it alone.
        if (Time.timeScale == FrozenTimeScale)
            Time.timeScale = 1f;
    }
}
