using System.Collections.Generic;
using UnityEngine;

// what doors the player can open
public static class AccessCredentials
{
    public const string TempBadge = "temp_badge";
    public const string TempBadgeAccess = "control_room";

    static readonly HashSet<string> held = new HashSet<string>();

    public static event System.Action<string> Granted;

    public static IEnumerable<string> All => held;

    public static bool Has(string id)
    {
        return !string.IsNullOrEmpty(id) && held.Contains(id);
    }

    public static bool Grant(string id)
    {
        if (string.IsNullOrEmpty(id) || !held.Add(id)) return false;
        Granted?.Invoke(id);
        return true;
    }

    public static bool HasBadge => Has(TempBadge);

    public static void Revoke(string id)
    {
        if (!string.IsNullOrEmpty(id)) held.Remove(id);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        held.Clear();
        Granted = null;
    }
}
