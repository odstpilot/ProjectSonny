using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A way to go, pointed out in the level: a bright arrow bobbing just in front of a doorway, pointing into it, and a
// pulse of light on the floor there, so there's no missing where to head next. Shown over any number of doorways at
// once (Show), until Hide. Made in code, so there's nothing to set up; one shared by whatever needs it.
public class ObjectiveHighlight : MonoBehaviour
{
    const float PixelsPerUnit = 16f;
    const float StandOff = 1.3f;        // how far in front of the doorway the arrow hangs
    const float BobDistance = 0.18f;
    const float BobSeconds = 0.9f;
    static readonly Color ArrowColor = new Color(1f, 0.78f, 0.25f);

    static ObjectiveHighlight instance;
    static Sprite arrowSprite;

    private readonly List<(Transform arrow, Light2D glow, Vector2 at, Vector2 toward)> markers =
        new List<(Transform, Light2D, Vector2, Vector2)>();

    static ObjectiveHighlight Get()
    {
        if (instance == null) instance = new GameObject("Objective Highlight").AddComponent<ObjectiveHighlight>();
        return instance;
    }

    // Points out these doorways, in place of whatever was pointed out before.
    public static void Show(IEnumerable<Teleporter> doors)
    {
        ObjectiveHighlight highlight = Get();
        highlight.Clear();
        foreach (Teleporter door in doors)
        {
            if (door == null) continue;
            var trigger = door.GetComponent<Collider2D>();
            Vector2 middle = trigger != null ? (Vector2)trigger.bounds.center : (Vector2)door.transform.position;
            Vector2 toward = door.wallDirection.sqrMagnitude > 0f ? door.wallDirection.normalized : Vector2.up;
            highlight.Add(middle - toward * StandOff, toward);
        }
    }

    public static void Hide()
    {
        if (instance != null) instance.Clear();
    }

    void Add(Vector2 at, Vector2 toward)
    {
        var arrow = new GameObject("Arrow").AddComponent<SpriteRenderer>();
        arrow.transform.SetParent(transform, false);
        arrow.transform.position = at;
        // The art points up.
        arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, toward);
        arrow.sprite = ArrowSprite;
        arrow.color = ArrowColor;
        if (CombatSprites.EffectMaterial != null) arrow.sharedMaterial = CombatSprites.EffectMaterial;
        arrow.sortingLayerName = "Top";

        var glow = new GameObject("Glow").AddComponent<Light2D>();
        glow.transform.SetParent(arrow.transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.pointLightInnerRadius = 0.2f;
        glow.pointLightOuterRadius = 2.2f;
        glow.falloffIntensity = 0.6f;
        glow.color = ArrowColor;
        glow.shadowsEnabled = false;
        markers.Add((arrow.transform, glow, at, toward));
    }

    void Clear()
    {
        foreach (var marker in markers)
            if (marker.arrow != null) Destroy(marker.arrow.gameObject);
        markers.Clear();
    }

    void Update()
    {
        float wave = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BobSeconds);
        foreach (var marker in markers)
        {
            if (marker.arrow == null) continue;
            marker.arrow.position = marker.at + marker.toward * (wave * BobDistance);
            marker.glow.intensity = 0.8f + 0.35f * wave;
        }
    }

    // A chunky chevron with a short stem, white so the renderer's color decides it.
    static Sprite ArrowSprite => arrowSprite != null ? arrowSprite : (arrowSprite = PixelArt.FromText(new[]
    {
        "......##......",
        ".....####.....",
        "....######....",
        "...########...",
        "..##########..",
        ".####.##.####.",
        "####..##..####",
        "###...##...###",
        "......##......",
        "......##......",
        "......##......",
        "......##......",
    }, new Dictionary<char, Color32> { { '#', new Color32(255, 255, 255, 255) } }, PixelsPerUnit, new Vector2(0.5f, 0.5f)));
}
