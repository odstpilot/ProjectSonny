using UnityEngine;

// Something the technician's suit is showing them, not the game: its vitals (HealthHud), the objective it's tracking
// (TutorialHud), the readout on its wrist (InventoryHud). It runs on the suit's power, so where that goes, it goes. In a
// blacked-out room (DarkRooms.InBlackout) it stutters, flickering on and off a few times, and dies, leaving the
// technician with nothing but the dark; when the power's back it boots up again, flickering on. Each display comes and
// goes on its own, a moment apart, so they don't all blink as one.
// Through its own CanvasGroup, so it multiplies whatever else fades the display rather than fighting it. Prompts for
// the controls and captions aren't the suit's, and stay.
// SuitDisplay.Attach puts one on a display; Down holds every one of them off, for anything else that kills the suit.
[DisallowMultipleComponent]
public class SuitDisplay : MonoBehaviour
{
    const float DieSeconds = 0.9f;          // stuttering, on the way out
    const float BootSeconds = 0.6f;         // flickering, on the way back
    const float MaxStagger = 0.35f;         // the most one waits behind the others, either way
    const float FlickerHold = 0.06f;

    // Held off for some other reason than a blackout. Counted, so two things can each hold it.
    public static int Down { get; set; }

    // Whether the suit has the power to show anything.
    public static bool Powered => Down <= 0 && !DarkRooms.InBlackout;

    // Entering Play mode without reloading scripts keeps statics.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Down = 0;

    private CanvasGroup group;
    private bool on = true;
    private float changedAt = -10f;
    private float stagger;
    private float nextFlicker;

    // Makes this a display of the suit's, with a CanvasGroup of its own. Nothing else may be using one on it: give it a
    // rect that hasn't got one.
    public static SuitDisplay Attach(RectTransform display)
    {
        if (display == null) return null;
        if (!display.TryGetComponent(out SuitDisplay suit)) suit = display.gameObject.AddComponent<SuitDisplay>();
        return suit;
    }

    // Wraps a display that already has a CanvasGroup doing something else in one that fills its parent, and makes
    // that the suit's: so the display keeps its own anchors and fades.
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
        // Each flicker held a few frames, so it reads as a stutter, not a strobe.
        bool settled = since >= (on ? BootSeconds : DieSeconds);
        if (!settled && now < nextFlicker) return;
        nextFlicker = now + FlickerHold;

        if (!on)
        {
            // Dying: flickers on and off, less and less on, then gone.
            float t = since / DieSeconds;
            group.alpha = t >= 1f ? 0f : Random.value > t ? (Random.value < 0.3f ? 0.4f : 1f) : 0f;
        }
        else
        {
            // Booting: mostly off at first, steadying on, with a dim frame or two.
            float t = since / BootSeconds;
            group.alpha = t >= 1f ? 1f : Random.value < t ? 1f : Random.value < 0.25f ? 0.35f : 0f;
        }
    }
}
