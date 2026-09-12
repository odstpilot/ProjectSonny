using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// One shot of an EMP (EmpWeaponData). EmpPulse.Fire stuns every weeping angel in the cone right away, then plays out
// the look: bands of electricity sweeping out (EmpWave), a cone of electric-blue light that crackles and sputters out,
// and sparks crawling over each stunned angel until it recovers. The object removes itself once all of that is done.
public class EmpPulse : MonoBehaviour
{
    private EmpWeaponData data;
    private Transform holder;
    private Vector2 holderOffset;
    private int running;

    // origin is where the cone is measured from; muzzle is where the pulse visibly comes out. The light follows holder.
    public static EmpPulse Fire(EmpWeaponData data, Vector2 origin, Vector2 muzzle, Vector2 direction, Transform holder)
    {
        var pulse = new GameObject("[EmpPulse]").AddComponent<EmpPulse>();
        pulse.data = data;
        pulse.holder = holder;
        pulse.holderOffset = holder != null ? muzzle - (Vector2)holder.position : Vector2.zero;
        // A point light's cone opens toward its up.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        pulse.transform.SetPositionAndRotation(muzzle, Quaternion.Euler(0f, 0f, angle - 90f));

        float halfAngle = data.coneAngle * 0.5f;
        PlaySound(data.fireClip, data.volume);
        pulse.StunAngels(origin, direction, halfAngle);

        EmpWave.Spawn(muzzle, direction, halfAngle, data.stunRadius, data.empColor, data.empCoreColor, data.waveCount, data.waveDuration);
        HitEffects.Ring(muzzle, data.empCoreColor, 1.3f);
        HitEffects.Sparks(muzzle, direction, 16, 9f, data.coneAngle, data.empCoreColor, data.empColor);
        CameraShake.Shake(data.pulseShake);

        pulse.Run(pulse.LightCone());
        return pulse;
    }

    // A little crackle so the player knows the EMP can fire again.
    public static void PlayReady(EmpWeaponData data, Vector2 at)
    {
        HitEffects.Sparks(at, Vector2.up, 8, 3f, 360f, data.empCoreColor, data.empColor);
        LightFlash.Spawn(at, data.empColor, 1.2f, 0.8f, 0.25f);
        PlaySound(data.readyClip, data.volume);
    }

    void StunAngels(Vector2 origin, Vector2 direction, float halfAngle)
    {
        // An angel with more than one collider still only gets stunned once.
        var stunned = new HashSet<WeepingAngel>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(origin, data.stunRadius, data.stunLayers))
        {
            Vector2 toTarget = (Vector2)hit.transform.position - origin;
            if (Vector2.Angle(direction, toTarget) > halfAngle) continue;

            var angel = hit.GetComponentInParent<WeepingAngel>();
            if (angel == null || !stunned.Add(angel)) continue;

            angel.Stun();
            float arrival = EmpWave.ArrivalTime(toTarget.magnitude, data.stunRadius, data.waveDuration);
            Run(Electrify(angel, arrival, Time.time + angel.stunDuration));
        }
    }

    IEnumerator LightCone()
    {
        var cone = gameObject.AddComponent<Light2D>();
        cone.lightType = Light2D.LightType.Point;
        cone.pointLightInnerAngle = data.coneAngle * 0.65f;
        cone.pointLightOuterAngle = data.coneAngle;
        cone.pointLightInnerRadius = 0f;
        cone.pointLightOuterRadius = data.stunRadius * 1.3f;
        cone.falloffIntensity = 0.5f;
        cone.color = data.empColor;

        // Crackle at full strength...
        for (float t = 0f; t < data.lightDuration; t += Time.deltaTime)
        {
            FollowHolder();
            cone.intensity = data.lightIntensity * Random.Range(0.75f, 1.15f);
            yield return null;
        }

        // ...then sputter out like shorting electronics.
        for (float t = 0f; t < data.lightFadeDuration; t += Time.deltaTime)
        {
            FollowHolder();
            float remaining = 1f - t / data.lightFadeDuration;
            float sputter = Random.value < 0.25f ? Random.Range(0.2f, 0.6f) : 1f;
            cone.intensity = data.lightIntensity * remaining * remaining * sputter;
            yield return null;
        }

        cone.enabled = false;
    }

    // When the wave reaches a stunned angel it lights up, then sparks crawl over it until it recovers.
    IEnumerator Electrify(WeepingAngel angel, float delay, float stunEndsAt)
    {
        yield return new WaitForSeconds(delay);
        if (angel == null) yield break;

        Vector2 center = angel.transform.position;
        HitEffects.Ring(center, data.empCoreColor, 1.2f);
        HitEffects.Sparks(center, Vector2.up, 14, 6f, 360f, data.empCoreColor, data.empColor);
        LightFlash.Spawn(center, data.empColor, 2f, 1.4f, 0.3f);

        while (angel != null && Time.time < stunEndsAt)
        {
            Vector2 at = (Vector2)angel.transform.position + Random.insideUnitCircle * 0.35f;
            HitEffects.Sparks(at, Random.insideUnitCircle.normalized, 3, 3f, 140f, data.empCoreColor, data.empColor);
            yield return new WaitForSeconds(Random.Range(0.06f, 0.18f));
        }
    }

    void FollowHolder()
    {
        if (holder != null)
            transform.position = (Vector2)holder.position + holderOffset;
    }

    // Starts a routine and removes this object once every routine has finished.
    void Run(IEnumerator routine)
    {
        running++;
        StartCoroutine(Track(routine));
    }

    IEnumerator Track(IEnumerator routine)
    {
        yield return routine;
        if (--running == 0) Destroy(gameObject);
    }

    static void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null) return;

        var source = new GameObject("[EmpSound]").AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.PlayOneShot(clip, volume);
        Destroy(source.gameObject, clip.length + 0.1f);
    }
}
