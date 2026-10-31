using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// reactor heat on screen
public class ReactorAmbience : MonoBehaviour
{
    const int SortingOrder = 75;

    [Tooltip("The room, by its marker in the layout.")]
    public char room = 'r';
    public Color stationColor = new Color(1f, 0.62f, 0.28f);
    [Tooltip("The glow over the whole room: where it's centered (this object), how far it reaches, and how bright.")]
    public float glowRadius = 40f;
    public float glowIntensity = 0.6f;
    public Color glowColor = new Color(1f, 0.7f, 0.32f);
    [Tooltip("The wash over the picture, and how much of it, and the burning edge.")]
    public Color washColor = new Color(1f, 0.55f, 0.12f);
    [Range(0f, 1f)] public float washAlpha = 0.13f;
    [Range(0f, 1f)] public float edgeAlpha = 0.45f;
    [Tooltip("Wading through radiation: the wash and edge go this color, this much stronger.")]
    public Color irradiatedColor = new Color(1f, 0.88f, 0.2f);
    [Range(0f, 1f)] public float irradiatedBoost = 0.18f;
    public float fadeSeconds = 1.2f;

    private readonly List<(Light2D light, Color color)> station = new List<(Light2D, Color)>();
    private Transform player;
    private Light2D glow;
    private Image wash;
    private RawImage edge;
    private float blend;
    private float irradiated;

    void Start()
    {
        foreach (Light2D light in FindObjectsByType<Light2D>())
            if (light.lightType == Light2D.LightType.Global) station.Add((light, light.color));
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null) player = found.transform;

        glow = gameObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = glowColor;
        glow.pointLightInnerRadius = glowRadius * 0.55f;
        glow.pointLightOuterRadius = glowRadius;
        glow.falloffIntensity = 0.3f;
        glow.shadowsEnabled = false;
        glow.intensity = 0f;

        var canvasObject = new GameObject("Reactor Heat Overlay");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        wash = Fill(canvasObject.transform, "Wash").gameObject.AddComponent<Image>();
        edge = Fill(canvasObject.transform, "Edge").gameObject.AddComponent<RawImage>();
        edge.texture = CombatSprites.VignetteTexture;
        wash.raycastTarget = edge.raycastTarget = false;
        Apply();
    }

    static RectTransform Fill(Transform parent, string childName)
    {
        var rect = new GameObject(childName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    void Update()
    {
        if (player == null) return;
        StationMap map = StationMap.Current;
        bool inside = blend > 0.5f;
        if (map != null && map.Locate(player.position, out _, out StationMap.Room here)) inside = here.marker == room;
        blend = Mathf.MoveTowards(blend, inside ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
        irradiated = Mathf.MoveTowards(irradiated, inside && RadiationLeak.PlayerIrradiated ? 1f : 0f, Time.deltaTime * 3f);
        Apply();
    }

    void Apply()
    {
        foreach ((Light2D light, Color color) in station)
            if (light != null) light.color = Color.Lerp(color, stationColor, blend);
        float flicker = 1f + 0.06f * (Mathf.PerlinNoise(Time.time * 1.3f, 0.7f) - 0.5f);
        if (glow != null) glow.intensity = glowIntensity * blend * flicker;
        if (wash == null) return;
        Color tint = Color.Lerp(washColor, irradiatedColor, irradiated);
        float pulse = irradiated * (0.6f + 0.4f * Mathf.Sin(Time.time * 7f));
        float building = ReactorSurge.Warning;
        float surge = building * building * (0.65f + 0.35f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, building))) * 0.3f
                      + ReactorSurge.Flash * 0.55f;
        tint = Color.Lerp(tint, new Color(1f, 0.92f, 0.6f), ReactorSurge.Flash);
        wash.color = new Color(tint.r, tint.g, tint.b, (washAlpha + irradiatedBoost * pulse + surge) * blend);
        edge.color = new Color(tint.r, tint.g * 0.8f, tint.b * 0.5f, (edgeAlpha + irradiatedBoost * 1.5f * pulse) * blend);
        wash.enabled = edge.enabled = blend > 0.001f;
    }
}
