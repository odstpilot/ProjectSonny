using System.Collections.Generic;
using UnityEngine;

// Stand-in for the inventory. Number keys 1-9 equip a slot, pressing the key of the weapon already in hand
// puts it away, and the scroll wheel cycles through empty hands and every slot.
// The real inventory can replace this by calling PlayerCombat.Equip and PlayerCombat.Unequip itself.
[RequireComponent(typeof(PlayerCombat))]
public class WeaponHotbar : MonoBehaviour
{
    public List<WeaponData> slots = new List<WeaponData>();

    private PlayerCombat combat;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        // No swapping while paused or in the middle of an attack.
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

    // Order: empty hands, slot 1, slot 2, ..., then back to empty hands.
    void Cycle(int step)
    {
        int positions = slots.Count + 1;
        int current = combat.IsArmed ? slots.IndexOf(combat.CurrentWeapon) + 1 : 0;
        int next = ((current + step) % positions + positions) % positions;
        combat.Equip(next == 0 ? null : slots[next - 1]);
    }
}
