using System.Collections.Generic;
using UnityEngine;

// What the player has access to: credentials recovered from terminals, and later badges and keycards. Doors ask Has.
// Credentials are plain ids like "comms_ring". They last the whole play session, across scenes.
// The technician's temp badge (TempBadge) comes from the crew member at the entrance, with the control room's access on
// it. Sonny kills it when it takes over: Chapter 2 starts with the badge still on them but its access gone (Revoke),
// until the comms ring's relay switches it back on (CommsRing).
public static class AccessCredentials
{
    public const string TempBadge = "temp_badge";
    // What the temp badge opens while it works.
    public const string TempBadgeAccess = "control_room";

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

    // Carrying the temp badge, working or not.
    public static bool HasBadge => Has(TempBadge);

    // Takes access away, like a badge going dead.
    public static void Revoke(string id)
    {
        if (!string.IsNullOrEmpty(id)) held.Remove(id);
    }

    // A fresh start each time Play is pressed in the Editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        held.Clear();
        Granted = null;
    }
}
