using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    [Tooltip("One Image per slot, in order, showing the item icon.")]
    public List<Image> slotIcons = new List<Image>();
    [Tooltip("Optional: one Image per slot for the frame/background. The selected one is tinted.")]
    public List<Image> slotFrames = new List<Image>();
    public Color normalColor = new Color(1f, 1f, 1f, 0.4f);
    public Color selectedColor = new Color(1f, 0.45f, 0.1f, 1f);   // the GDD's orange-red
    [Tooltip("GDD style: show only the equipped slot. Turn on once the UI is final.")]
    public bool showOnlySelected = false;

    private void Start()
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("HotbarUI: no PlayerInventory assigned.", this);
            return;
        }
        playerInventory.OnInventoryChanged += UpdateUI;
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= UpdateUI;
    }

    public void UpdateUI()
    {
        if (playerInventory == null) return;

        for (int i = 0; i < slotIcons.Count; i++)
        {
            InventorySlot slot = i < playerInventory.hotbarSize ? playerInventory.GetSlot(i) : null;
            ItemData item = slot != null ? slot.item : null;
            bool selected = i == playerInventory.SelectedSlot;
            bool visible = !showOnlySelected || selected;

            slotIcons[i].sprite = item != null ? item.icon : null;
            slotIcons[i].enabled = visible && slotIcons[i].sprite != null;

            if (i < slotFrames.Count && slotFrames[i] != null)
            {
                slotFrames[i].enabled = visible;
                slotFrames[i].color = selected ? selectedColor : normalColor;
            }
        }
    }
}