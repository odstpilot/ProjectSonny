using UnityEngine;

// what the suit has unlocked
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Reset();
}
