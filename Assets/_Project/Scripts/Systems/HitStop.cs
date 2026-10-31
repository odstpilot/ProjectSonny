using UnityEngine;

// short slow-down when a hit lands
public class HitStop : MonoBehaviour
{
    public const float FrozenTimeScale = 0.05f;
    const float EaseBackTime = 0.25f;

    static HitStop instance;

    private float resumeAt;
    private float scale = 1f;
    private bool holding;

    public static void Freeze(float duration) => Hold(FrozenTimeScale, duration);

    public static void Hold(float timeScale, float realSeconds)
    {
        if (realSeconds <= 0f || !GameSettings.FreezeOnHit) return;

        bool alreadyHolding = instance != null && instance.holding;
        if (!alreadyHolding && Time.timeScale < 1f) return;

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
        if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
    }
}
