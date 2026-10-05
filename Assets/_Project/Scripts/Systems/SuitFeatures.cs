using UnityEngine;

// What the technician's suit can do so far. Pip, the helper in the suit (SuitHelper), shows them each thing as they need
// it, and it isn't there until then: the map first, then the inventory and the pause menu later on. Anything that opens
// one of these checks here first (MapScreen checks Map).
// It lasts from scene to scene, so a chapter keeps what the ones before it unlocked. Starting a new game should call
// Reset.
public static class SuitFeatures
{
    [System.Flags]
    public enum Feature
    {
        None = 0,
        Map = 1 << 0,
        Inventory = 1 << 1,
        Pause = 1 << 2,
        All = Map | Inventory | Pause,
    }

    public static Feature Unlocked { get; private set; }

    public static event System.Action<Feature> OnUnlocked;

    public static bool Has(Feature feature) => (Unlocked & feature) == feature;

    public static void Unlock(Feature feature)
    {
        Feature added = feature & ~Unlocked;
        if (added == Feature.None) return;
        Unlocked |= added;
        OnUnlocked?.Invoke(added);
    }

    public static void Reset() => Unlocked = Feature.None;

    // Entering Play mode without reloading scripts keeps statics; start each run with nothing unlocked.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Reset();
}
