using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class LightManager : MonoBehaviour
{
    public Light2D globalLight;
    private Coroutine lightOffCoroutine;

    public void TriggerLightOff()
    {
        // If already running, restart it
        if (lightOffCoroutine != null)
        {
            StopCoroutine(lightOffCoroutine);
        }

        lightOffCoroutine = StartCoroutine(LightsOffRoutine());
    }

    private IEnumerator LightsOffRoutine()
    {
        globalLight.intensity = 0f;

        yield return new WaitForSeconds(15f);

        globalLight.intensity = 1f;
        lightOffCoroutine = null;
    }
}