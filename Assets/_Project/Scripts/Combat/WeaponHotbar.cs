using System.Collections.Generic;
using UnityEngine;

// number keys and scroll wheel pick a weapon
[RequireComponent(typeof(PlayerCombat))]
public class WeaponHotbar : MonoBehaviour
{
    public List<WeaponData> slots = new List<WeaponData>();

    private PlayerCombat combat;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    void Start() => InventoryHud.Get().Track(this);

    public void Move(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= slots.Count || to >= slots.Count) return;
        (slots[from], slots[to]) = (slots[to], slots[from]);
    }

    void Update()
    {
        if (Time.timeScale == 0f || combat.IsBusy) return;

        for (int i = 0; i < slots.Count && i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;

            if (slots[i] != null && slots[i] == combat.CurrentWeapon)
                combat.Unequip();
            else
                combat.Equip(slots[i]);
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) Cycle(1);
        else if (scroll < 0f) Cycle(-1);
    }

    void Cycle(int step)
    {
        int positions = slots.Count + 1;
        int current = combat.IsArmed ? slots.IndexOf(combat.CurrentWeapon) + 1 : 0;
        int next = ((current + step) % positions + positions) % positions;
        combat.Equip(next == 0 ? null : slots[next - 1]);
    }
}
