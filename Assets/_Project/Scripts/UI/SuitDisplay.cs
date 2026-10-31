using UnityEngine;

// look for things the suit shows
[DisallowMultipleComponent]
public class SuitDisplay : MonoBehaviour
{
    const float DieSeconds = 0.9f;
    const float BootSeconds = 0.6f;
    const float MaxStagger = 0.35f;
    const float FlickerHold = 0.06f;

    public static int Down { get; set; }

    public static bool Powered => Down <= 0 && !DarkRooms.InBlackout;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Down = 0;

    private CanvasGroup group;
    private bool on = true;
    private float changedAt = -10f;
    private float stagger;
    private float nextFlicker;

    public static SuitDisplay Attach(RectTransform display)
    {
        if (display == null) return null;
        if (!display.TryGetComponent(out SuitDisplay suit)) suit = display.gameObject.AddComponent<SuitDisplay>();
        return suit;
    }

    public static SuitDisplay Wrap(RectTransform display)
    {
        if (display == null) return null;
        var wrapper = new GameObject("Suit Display", typeof(RectTransform)).GetComponent<RectTransform>();
        wrapper.SetParent(display.parent, false);
        wrapper.SetSiblingIndex(display.GetSiblingIndex());
        wrapper.anchorMin = Vector2.zero;
        wrapper.anchorMax = Vector2.one;
        wrapper.offsetMin = wrapper.offsetMax = Vector2.zero;
        display.SetParent(wrapper, false);
        return Attach(wrapper);
    }

    void Awake()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        on = Powered;
        group.alpha = on ? 1f : 0f;
        stagger = Random.Range(0f, MaxStagger);
    }

    void Update()
    {
        float now = Time.unscaledTime;
        bool powered = Powered;
        if (powered != on)
        {
            on = powered;
            changedAt = now;
            stagger = Random.Range(0f, MaxStagger);
        }
        float since = now - changedAt - stagger;
        if (since < 0f) return;
        bool settled = since >= (on ? BootSeconds : DieSeconds);
        if (!settled && now < nextFlicker) return;
        nextFlicker = now + FlickerHold;

        if (!on)
        {
            float t = since / DieSeconds;
            group.alpha = t >= 1f ? 0f : Random.value > t ? (Random.value < 0.3f ? 0.4f : 1f) : 0f;
        }
        else
        {
            float t = since / BootSeconds;
            group.alpha = t >= 1f ? 1f : Random.value < t ? 1f : Random.value < 0.25f ? 0.35f : 0f;
        }
    }
}
