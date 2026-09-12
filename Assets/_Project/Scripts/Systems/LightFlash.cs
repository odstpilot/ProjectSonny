using UnityEngine;
using UnityEngine.Rendering.Universal;

// A burst of light that fades out and removes itself: LightFlash.Spawn(point, color, radius, intensity, duration)
// from any script. For explosions, muzzle flashes, and EMP pulses, so they light up the room and not just themselves.
public class LightFlash : MonoBehaviour
{
    private Light2D glow;
    private float startIntensity;
    private float duration;
    private float elapsed;

    public static LightFlash Spawn(Vector2 point, Color color, float radius, float intensity, float duration)
    {
        if (radius <= 0f || intensity <= 0f) return null;

        var go = new GameObject("[LightFlash]");
        go.transform.position = point;

        var flash = go.AddComponent<LightFlash>();
        flash.glow = go.AddComponent<Light2D>();
        flash.glow.lightType = Light2D.LightType.Point;
        flash.glow.pointLightInnerRadius = 0f;
        flash.glow.pointLightOuterRadius = radius;
        flash.glow.falloffIntensity = 0.6f;
        flash.glow.color = color;
        flash.glow.intensity = intensity;
        flash.startIntensity = intensity;
        flash.duration = Mathf.Max(0.01f, duration);
        return flash;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float remaining = 1f - Mathf.Clamp01(elapsed / duration);
        glow.intensity = startIntensity * remaining * remaining;
        if (remaining <= 0f) Destroy(gameObject);
    }
}
