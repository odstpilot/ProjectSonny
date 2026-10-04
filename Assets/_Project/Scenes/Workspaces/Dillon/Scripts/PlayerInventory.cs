using System.Collections.Generic;
using UnityEngine;

// One slot in the inventory. Weapons always have a count of 1; materials stack.
[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int count;

    public bool IsEmpty => item == null;

    public void Clear()
    {
        item = null;
        count = 0;
    }
}

// The single source of truth for what the player owns.
// Slots 0 to hotbarSize-1 are the hotbar (weapons only, keys 1-9). The rest are the backpack grid.
// Equipping goes through PlayerCombat.Equip / Unequip, so the combat scripts are untouched.
[RequireComponent(typeof(PlayerCombat))]
public class PlayerInventory : MonoBehaviour
{
    [Range(1, 9)] public int hotbarSize = 6;
    public int backpackSize = 12;
    [Tooltip("Items the player owns when the scene starts, e.g. the toolkit.")]
    public ItemData[] startingItems;
    [Tooltip("Hotbar slot in hand when the scene starts. -1 = empty hands.")]
    public int startingSelectedSlot = -1;
    [Tooltip("Picking up a weapon onto the hotbar with empty hands equips it.")]
    public bool autoEquipWhenEmptyHanded = true;

    [SerializeField] private InventorySlot[] slots = new InventorySlot[0];

    // Fires on any change to the slots or the selected slot.
    public System.Action OnInventoryChanged;

    // -1 means empty hands. Otherwise a hotbar slot index.
    public int SelectedSlot { get; private set; } = -1;
    public WeaponItemData SelectedItem => SelectedSlot >= 0 ? slots[SelectedSlot].item as WeaponItemData : null;
    public int TotalSlots => hotbarSize + backpackSize;

    private PlayerCombat combat;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        EnsureSlots();

        if (startingItems != null)
            foreach (ItemData item in startingItems)
                Add(item, 1, out _);
    }

    void Start()
    {
        SelectSlot(startingSelectedSlot);
    }

    void Update()
    {
        // No swapping while paused or mid-attack.
        if (Time.timeScale == 0f || combat.IsBusy) return;

        int keys = Mathf.Min(hotbarSize, 9);
        for (int i = 0; i < keys; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) || !(slots[i].item is WeaponItemData)) continue;
            SelectSlot(i == SelectedSlot ? -1 : i);
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) Cycle(1);
        else if (scroll < 0f) Cycle(-1);
    }

    void EnsureSlots()
    {
        var resized = new InventorySlot[TotalSlots];
        for (int i = 0; i < resized.Length; i++)
            resized[i] = i < slots.Length && slots[i] != null ? slots[i] : new InventorySlot();
        slots = resized;
    }

    bool Valid(int index)
    {
        return index >= 0 && index < slots.Length;
    }

    // ---------- Reading ----------

    public InventorySlot GetSlot(int index)
    {
        return Valid(index) ? slots[index] : null;
    }

    public bool IsHotbarSlot(int index)
    {
        return index >= 0 && index < hotbarSize;
    }

    // Hotbar slots only take weapons. Backpack slots take anything.
    public bool CanHold(int index, ItemData item)
    {
        return index >= hotbarSize || item is WeaponItemData;
    }

    public bool HasWeapon(WeaponItemData item)
    {
        foreach (InventorySlot slot in slots)
            if (slot.item == item) return true;
        return false;
    }

    public int CountOf(MaterialItemData item)
    {
        int total = 0;
        foreach (InventorySlot slot in slots)
            if (slot.item == item) total += slot.count;
        return total;
    }

    public bool HasMaterial(MaterialItemData item, int amount)
    {
        return CountOf(item) >= amount;
    }

    // ---------- Hotbar selection ----------

    // -1 puts the weapon away. Otherwise the slot must hold a weapon.
    public void SelectSlot(int index)
    {
        if (index < -1 || index >= hotbarSize) return;

        WeaponItemData weaponItem = null;
        if (index >= 0)
        {
            weaponItem = slots[index].item as WeaponItemData;
            if (weaponItem == null) return;
        }

        SelectedSlot = index;
        combat.Equip(weaponItem != null ? weaponItem.weapon : null);
        OnInventoryChanged?.Invoke();
    }

    // Order: empty hands, then each hotbar weapon, then back to empty hands.
    void Cycle(int step)
    {
        var order = new List<int> { -1 };
        for (int i = 0; i < hotbarSize; i++)
            if (slots[i].item is WeaponItemData) order.Add(i);

        int current = order.IndexOf(SelectedSlot);
        int next = ((current + step) % order.Count + order.Count) % order.Count;
        SelectSlot(order[next]);
    }

    // ---------- Moving things around (the drag and drop will call this) ----------

    // Moves the item in "from" to "to". Swaps if "to" is occupied, merges if both hold the same material.
    // Returns false if the move isn't allowed (for example, scrap onto a hotbar slot).
    public bool MoveOrSwap(int from, int to)
    {
        if (from == to || !Valid(from) || !Valid(to) || slots[from].IsEmpty) return false;

        InventorySlot a = slots[from];
        InventorySlot b = slots[to];
        WeaponItemData held = SelectedItem;

        if (a.item is MaterialItemData material && b.item == a.item)
        {
            int moved = Mathf.Min(a.count, material.maxStack - b.count);
            if (moved <= 0) return false;
            b.count += moved;
            a.count -= moved;
            if (a.count <= 0) a.Clear();
        }
        else
        {
            if (!CanHold(to, a.item)) return false;
            if (!b.IsEmpty && !CanHold(from, b.item)) return false;

            ItemData tempItem = a.item;
            int tempCount = a.count;
            a.item = b.item;
            a.count = b.count;
            b.item = tempItem;
            b.count = tempCount;
        }

        RefreshSelection(held);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // Keeps the equipped weapon in step after slots move: follows it to its new hotbar slot,
    // or puts it away if it left the hotbar.
    void RefreshSelection(WeaponItemData held)
    {
        if (held == null) return;

        int found = -1;
        for (int i = 0; i < hotbarSize; i++)
        {
            if (slots[i].item != held) continue;
            found = i;
            break;
        }

        if (found == SelectedSlot) return;
        SelectedSlot = found;
        if (found < 0) combat.Unequip();
    }

    // ---------- Adding and removing ----------

    // Weapons go to the first empty hotbar slot, then the first empty backpack slot.
    // Materials stack onto existing piles, then fill empty backpack slots.
    // All or nothing: returns false and adds nothing if it won't all fit.
    public bool AddItem(ItemData item, int amount = 1)
    {
        if (!Add(item, amount, out int weaponSlot)) return false;

        Debug.Log("Added " + item.itemName + (amount > 1 ? " x" + amount : ""));

        bool onHotbar = weaponSlot >= 0 && weaponSlot < hotbarSize;
        if (onHotbar && autoEquipWhenEmptyHanded && SelectedSlot == -1 && !combat.IsBusy)
            SelectSlot(weaponSlot);
        else
            OnInventoryChanged?.Invoke();
        return true;
    }

    bool Add(ItemData item, int amount, out int weaponSlot)
    {
        weaponSlot = -1;
        if (item == null) return false;

        if (item is WeaponItemData weaponItem)
        {
            if (weaponItem.weapon == null)
            {
                Debug.LogWarning("PlayerInventory: " + item.name + " has no WeaponData assigned.");
                return false;
            }
            if (HasWeapon(weaponItem))
            {
                Debug.Log("Already carrying " + weaponItem.itemName);
                return false;
            }

            int slot = FirstEmpty(0, hotbarSize);
            if (slot < 0) slot = FirstEmpty(hotbarSize, TotalSlots);
            if (slot < 0)
            {
                Debug.Log("Inventory is full!");
                return false;
            }

            slots[slot].item = weaponItem;
            slots[slot].count = 1;
            weaponSlot = slot;
            return true;
        }

        if (item is MaterialItemData material)
        {
            if (amount <= 0) return false;

            int room = 0;
            for (int i = hotbarSize; i < TotalSlots; i++)
            {
                if (slots[i].IsEmpty) room += material.maxStack;
                else if (slots[i].item == material) room += Mathf.Max(0, material.maxStack - slots[i].count);
            }
            if (room < amount)
            {
                Debug.Log("Backpack is full!");
                return false;
            }

            int left = amount;
            for (int i = hotbarSize; i < TotalSlots && left > 0; i++)
            {
                if (slots[i].item != material) continue;
                int add = Mathf.Min(left, material.maxStack - slots[i].count);
                if (add <= 0) continue;
                slots[i].count += add;
                left -= add;
            }
            for (int i = hotbarSize; i < TotalSlots && left > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;
                int add = Mathf.Min(left, material.maxStack);
                slots[i].item = material;
                slots[i].count = add;
                left -= add;
            }
            return true;
        }

        Debug.LogWarning("PlayerInventory: " + item.name + " is neither a WeaponItemData nor a MaterialItemData.");
        return false;
    }

    int FirstEmpty(int from, int to)
    {
        for (int i = from; i < to; i++)
            if (slots[i].IsEmpty) return i;
        return -1;
    }

    public void RemoveWeapon(WeaponItemData item)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].item != item) continue;
            slots[i].Clear();
            if (SelectedSlot == i) SelectSlot(-1);
            else OnInventoryChanged?.Invoke();
            return;
        }
    }

    // For crafting: spends materials, returns false and spends nothing if there isn't enough.
    public bool RemoveMaterial(MaterialItemData item, int amount)
    {
        if (amount <= 0 || !HasMaterial(item, amount)) return false;

        int left = amount;
        for (int i = TotalSlots - 1; i >= hotbarSize && left > 0; i--)
        {
            if (slots[i].item != item) continue;
            int take = Mathf.Min(left, slots[i].count);
            slots[i].count -= take;
            left -= take;
            if (slots[i].count <= 0) slots[i].Clear();
        }
        OnInventoryChanged?.Invoke();
        return true;
    }
}