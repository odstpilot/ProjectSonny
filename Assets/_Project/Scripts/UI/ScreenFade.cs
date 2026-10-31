using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// fade to black and back
public class ScreenFade : MonoBehaviour
{
    const int SortingOrder = 110;
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

    public void PlaySound(string soundName, AudioClip fallback = null)
    {
        if (!string.IsNullOrEmpty(soundName)) SoundManager.PlayOneShot(audioSource, soundName, fallback: fallback);
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
