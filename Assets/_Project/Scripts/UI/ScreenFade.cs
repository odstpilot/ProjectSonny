using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Black over the whole screen, faded in and out, for moving the player somewhere else without the jump showing.
// Teleporter uses it for doors. Built from code the first time it's needed; it runs on real time, so it works whatever
// the time scale is, and it has its own sound source for whatever plays during the fade.
public class ScreenFade : MonoBehaviour
{
    const int SortingOrder = 110;           // over the HUD and prompts, under the death screen
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    static ScreenFade instance;

    private Image cover;
    private AudioSource audioSource;

    public float Alpha => cover.color.a;

    public static ScreenFade Get()
    {
        if (instance == null)
        {
            instance = new GameObject("ScreenFade", typeof(RectTransform)).AddComponent<ScreenFade>();
            instance.Build();
        }
        return instance;
    }

    // Takes the fade away at once, if there is one.
    public static void Clear()
    {
        if (instance != null) instance.SetAlpha(0f);
    }

    public void SetAlpha(float alpha)
    {
        cover.color = new Color(0f, 0f, 0f, alpha);
        cover.enabled = alpha > 0f;
    }

    public IEnumerator FadeTo(float alpha, float seconds)
    {
        float from = Alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float progress = t / seconds;
            SetAlpha(Mathf.Lerp(from, alpha, progress * progress * (3f - 2f * progress)));
            yield return null;
        }
        SetAlpha(alpha);
    }

    public void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null) audioSource.PlayOneShot(clip, volume);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        var rect = new GameObject("Cover", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        cover = rect.gameObject.AddComponent<Image>();
        cover.raycastTarget = false;
        SetAlpha(0f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
    }
}
