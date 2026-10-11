using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the backpack grid. Press Tab to open and close it (Esc also closes it).
// The game pauses while it's open. Put this on an object that stays active (like the Canvas),
// and assign the panel that should appear and disappear.
public class BackpackUI : MonoBehaviour
{
    public PlayerInventory playerInventory;
    [Tooltip("The panel that shows and hides. Leave it switched off in the Hierarchy.")]
    public GameObject panel;
    public KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("One Image per backpack slot, in order, showing the item icon.")]
    public List<Image> slotIcons = new List<Image>();
    [Tooltip("One text per backpack slot, in order, showing the stack count.")]
    public List<TMP_Text> countTexts = new List<TMP_Text>();

    private bool isOpen;
    private float timeScaleBeforeOpen = 1f;

    private void Start()
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("BackpackUI: no PlayerInventory assigned.", this);
            return;
        }
        playerInventory.OnInventoryChanged += UpdateUI;
        if (panel != null) panel.SetActive(false);
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= UpdateUI;
    }

    private void OnDisable()
    {
        if (isOpen) Close();
    }

    private void Update()
    {
        if (isOpen)
        {
            if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.Escape))
                Close();
        }
        // Don't open while something else (a menu, the death screen) has the game paused.
        else if (Input.GetKeyDown(toggleKey) && Time.timeScale > 0f)
        {
            Open();
        }
    }

    public void Open()
    {
        if (panel == null) return;
        isOpen = true;
        panel.SetActive(true);
        timeScaleBeforeOpen = Time.timeScale;
        Time.timeScale = 0f;
        UpdateUI();
    }

    public void Close()
    {
        isOpen = false;
        if (panel != null) panel.SetActive(false);
        Time.timeScale = timeScaleBeforeOpen;
    }

    public void UpdateUI()
    {
        if (playerInventory == null) return;

        for (int i = 0; i < slotIcons.Count; i++)
        {
            InventorySlot slot = playerInventory.GetSlot(playerInventory.hotbarSize + i);
            ItemData item = slot != null ? slot.item : null;

            slotIcons[i].sprite = item != null ? item.icon : null;
            slotIcons[i].enabled = slotIcons[i].sprite != null;

            if (i < countTexts.Count && countTexts[i] != null)
                countTexts[i].text = slot != null && slot.count > 1 ? slot.count.ToString() : "";
        }
    }
}