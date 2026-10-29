using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The weeping angel's jumpscare, the moment it catches the player (WeepingAngel): its face slams up to fill the screen,
// shaking hard, with a red flash and a slam, then the screen cuts to black and fades back up with the player already
// sent back. The face is its own sprite, crushed down to chunky pixels so it matches the game. Unscaled time, so the
// hit stop on the catch can't stall it. Built from code the first time it's needed.
public class AngelJumpscare : MonoBehaviour
{
    const int SortingOrder = 118;           // over the fade and terminal screens (110), under the death screen (120)
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    const int FacePixels = 72;              // how many pixels across the face is crushed to
    const float LungeSeconds = 0.32f;
    const float HoldSeconds = 0.38f;
    const float BlackSeconds = 0.22f;
    const float FadeSeconds = 0.45f;
    const float ShakePixels = 60f;

    static AngelJumpscare instance;

    private RawImage backing, face, ghost, flash;
    private RenderTexture crushed;
    private Coroutine playing;

    public static bool Playing => instance != null && instance.playing != null;

    // Real seconds from the catch until the face is gone and it's black, for anything to be done out of sight then.
    public const float ToBlack = LungeSeconds + HoldSeconds;

    public static void Play(Sprite faceSprite)
    {
        if (instance == null)
        {
            instance = new GameObject("Angel Jumpscare").AddComponent<AngelJumpscare>();
            instance.Build();
        }
        if (instance.playing != null) return;
        instance.playing = instance.StartCoroutine(instance.Run(faceSprite));
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (crushed != null) crushed.Release();
    }

    IEnumerator Run(Sprite faceSprite)
    {
        Crush(faceSprite);
        backing.enabled = face.enabled = ghost.enabled = flash.enabled = true;
        RectTransform faceRect = face.rectTransform, ghostRect = ghost.rectTransform;

        // It comes at the screen: from a little way off to right up against it, the whole view jolting.
        for (float t = 0f; t < LungeSeconds + HoldSeconds; t += Time.unscaledDeltaTime)
        {
            float lunge = Mathf.Clamp01(t / LungeSeconds);
            float eased = 1f - (1f - lunge) * (1f - lunge) * (1f - lunge);
            float held = Mathf.Clamp01((t - LungeSeconds) / HoldSeconds);
            float scale = Mathf.Lerp(0.35f, 1.25f, eased) + held * 0.12f;
            Vector2 shake = Random.insideUnitCircle * ShakePixels * Mathf.Lerp(1f, 0.55f, held);
            faceRect.localScale = Vector3.one * scale;
            faceRect.anchoredPosition = shake;
            // A red double of it, jittering off to one side, like the picture's breaking up.
            ghostRect.localScale = Vector3.one * scale * 1.04f;
            ghostRect.anchoredPosition = shake + new Vector2(Random.Range(-28f, 28f), Random.Range(-10f, 10f));
            ghost.color = new Color(1f, 0.1f, 0.05f, Random.value < 0.5f ? 0.45f : 0.15f);
            backing.color = new Color(0f, 0f, 0f, Mathf.Clamp01(lunge * 3f));
            flash.color = new Color(0.85f, 0.05f, 0.03f, (lunge < 1f ? 0.5f : 0.35f * (1f - held)) * (Random.value < 0.25f ? 0.3f : 1f));
            yield return null;
        }

        // Gone: a beat of black, then the room comes back.
        face.enabled = ghost.enabled = flash.enabled = false;
        backing.color = Color.black;
        for (float t = 0f; t < BlackSeconds; t += Time.unscaledDeltaTime) yield return null;
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            backing.color = new Color(0f, 0f, 0f, 1f - t / FadeSeconds);
            yield return null;
        }
        backing.enabled = false;
        playing = null;
    }

    // The face shrunk to a few chunky pixels and drawn back up without smoothing, so it's pixel art like everything else.
    void Crush(Sprite faceSprite)
    {
        Texture source = faceSprite != null ? faceSprite.texture : null;
        if (source == null)
        {
            face.texture = ghost.texture = Texture2D.blackTexture;
            return;
        }
        float aspect = faceSprite.rect.height / Mathf.Max(1f, faceSprite.rect.width);
        int height = Mathf.Max(1, Mathf.RoundToInt(FacePixels * aspect));
        if (crushed == null || crushed.height != height)
        {
            if (crushed != null) crushed.Release();
            crushed = new RenderTexture(FacePixels, height, 0) { filterMode = FilterMode.Point };
        }
        Rect r = faceSprite.textureRect;
        var scale = new Vector2(r.width / source.width, r.height / source.height);
        var offset = new Vector2(r.x / source.width, r.y / source.height);
        Graphics.Blit(source, crushed, scale, offset);
        face.texture = ghost.texture = crushed;
        // Square to the screen's height, whatever its shape.
        var size = new Vector2(ReferenceResolution.y / aspect, ReferenceResolution.y);
        face.rectTransform.sizeDelta = ghost.rectTransform.sizeDelta = size;
    }

    void Build()
    {
        DontDestroyOnLoad(gameObject);
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 1f;

        backing = Layer("Backing", true);
        backing.texture = Texture2D.whiteTexture;
        ghost = Layer("Ghost", false);
        face = Layer("Face", false);
        flash = Layer("Flash", true);
        flash.texture = Texture2D.whiteTexture;
    }

    RawImage Layer(string layerName, bool fill)
    {
        var rect = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        if (fill)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-ShakePixels * 2f, -ShakePixels * 2f);
            rect.offsetMax = new Vector2(ShakePixels * 2f, ShakePixels * 2f);
        }
        else rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        var image = rect.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }
}
