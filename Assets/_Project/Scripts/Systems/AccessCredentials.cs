using System.Collections.Generic;
using UnityEngine;

// What the player has access to: credentials recovered from terminals, and later badges and keycards. Doors ask Has.
// Credentials are plain ids like "comms_ring". They last the whole play session, across scenes.
public static class AccessCredentials
{
    static readonly HashSet<string> held = new HashSet<string>();

    // The id, whenever a new credential is recovered.
    public static event System.Action<string> Granted;

    public static IEnumerable<string> All => held;

    public static bool Has(string id)
    {
        return !string.IsNullOrEmpty(id) && held.Contains(id);
    }

    // True if it's new.
    public static bool Grant(string id)
    {
        if (string.IsNullOrEmpty(id) || !held.Add(id)) return false;
        Granted?.Invoke(id);
        return true;
    }

    // A fresh start each time Play is pressed in the Editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        held.Clear();
        Granted = null;
    }
}
