using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// turns the player's controls off and on
public static class PlayerControls
{
    public static List<Behaviour> Freeze(Transform player)
    {
        var frozen = new List<Behaviour>();
        if (player == null) return frozen;

        var controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.SetAnimBool(PlayerAnimParams.IsMoving, false);

        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>())
        {
            if (!behaviour.enabled || behaviour is Health || behaviour is PlayerHealthHandler || behaviour is Light2D) continue;
            behaviour.enabled = false;
            frozen.Add(behaviour);
        }

        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        return frozen;
    }

    public static void Release(List<Behaviour> frozen)
    {
        if (frozen == null) return;
        foreach (Behaviour behaviour in frozen)
            if (behaviour != null) behaviour.enabled = true;
        frozen.Clear();
    }
}
