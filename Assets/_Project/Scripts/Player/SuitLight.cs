using UnityEngine;
using UnityEngine.Rendering.Universal;

// The suit's own light: a soft glow all round the technician, so they can see a little way around them where the
// power's out. It comes up as the room goes dark (DarkRooms sets Level) and fades away again in the light, and it's out
// while they're hidden in a locker or crawling through the ducts.
// Added to the player by DarkRooms.
[RequireComponent(typeof(PlayerController))]
public class SuitLight : MonoBehaviour
{
    [Tooltip("How far the glow reaches: fully lit inside the first, fading out to the second.")]
    public float innerRadius = 0.8f;
    public float radius = 4.5f;
    public float intensity = 0.9f;
    public Color color = new Color(1f, 0.94f, 0.82f);

    public static SuitLight Current { get; private set; }

    // How much of the glow is showing, 0 to 1: up in the dark, down in the light (DarkRooms).
    public float Level { get; set; }

    private Light2D glow;

    void Awake()
    {
        Current = this;
        glow = new GameObject("Suit Glow").AddComponent<Light2D>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        glow.lightType = Light2D.LightType.Point;
        glow.color = color;
        glow.pointLightInnerRadius = innerRadius;
        glow.pointLightOuterRadius = radius;
        glow.falloffIntensity = 0.6f;
        glow.shadowsEnabled = false;
        glow.enabled = false;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Update()
    {
        float power = Locker.IsPlayerHidden ? 0f : Level;
        glow.enabled = power > 0.001f;
        glow.intensity = intensity * power;
    }
}
