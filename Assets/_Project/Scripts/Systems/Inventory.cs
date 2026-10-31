using System.Collections.Generic;
using UnityEngine;

// scrap and made things the player has
public static class Inventory
{
    public enum Material { MetalSheet, Circuit, Battery, Wire }

    public static readonly Material[] Materials = { Material.MetalSheet, Material.Circuit, Material.Battery, Material.Wire };

    [System.Serializable]
    public struct Stack
    {
        public Material material;
        public int count;

        public Stack(Material material, int count)
        {
            this.material = material;
            this.count = count;
        }
    }

    public static string Name(Material material) => material switch
    {
        Material.MetalSheet => "Metal Sheet",
        Material.Circuit => "Circuit Board",
        Material.Battery => "Battery",
        _ => "Wire",
    };

    [System.Serializable]
    class Saved
    {
        public List<Stack> parts = new List<Stack>();
        public List<string> taken = new List<string>();
        public List<string> made = new List<string>();
    }

    static Saved state = new Saved();

    public static event System.Action Changed;

    public static int Count(Material material)
    {
        foreach (Stack stack in state.parts)
            if (stack.material == material) return stack.count;
        return 0;
    }

    public static bool Has(IEnumerable<Stack> cost)
    {
        foreach (Stack need in cost)
            if (Count(need.material) < need.count) return false;
        return true;
    }

    public static int Toward(IEnumerable<Stack> cost)
    {
        int have = 0;
        foreach (Stack need in cost) have += Mathf.Min(need.count, Count(need.material));
        return have;
    }

    public static bool Took(string pickup) => state.taken.Contains(pickup);

    public static bool Made(string recipe) => state.made.Contains(recipe);

    public static void Add(string pickup, Material material, int amount)
    {
        if (!string.IsNullOrEmpty(pickup) && !state.taken.Contains(pickup)) state.taken.Add(pickup);
        Set(material, Count(material) + amount);
        Changed?.Invoke();
    }

    public static bool Make(string recipe, IEnumerable<Stack> cost)
    {
        if (!Has(cost)) return false;
        foreach (Stack need in cost) Set(need.material, Count(need.material) - need.count);
        if (!state.made.Contains(recipe)) state.made.Add(recipe);
        Changed?.Invoke();
        return true;
    }

    public static void Grant(string recipe)
    {
        if (!state.made.Contains(recipe)) state.made.Add(recipe);
        Changed?.Invoke();
    }

    static void Set(Material material, int count)
    {
        state.parts.RemoveAll(stack => stack.material == material);
        if (count > 0) state.parts.Add(new Stack(material, count));
    }

    public static string Save() => JsonUtility.ToJson(state);

    public static void Load(string saved)
    {
        state = string.IsNullOrEmpty(saved) ? new Saved() : JsonUtility.FromJson<Saved>(saved) ?? new Saved();
        Changed?.Invoke();
    }

    public static void Reset() => Load(null);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        state = new Saved();
        Changed = null;
    }
}
