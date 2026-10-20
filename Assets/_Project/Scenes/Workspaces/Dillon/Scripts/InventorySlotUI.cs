
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public PlayerInventory inventory;
    public BackpackUI backpackUI;
    public int slotIndex;

    private static int draggedSlot = -1;
    private static PlayerInventory draggedInventory;

    private GameObject dragIcon;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (backpackUI == null || !backpackUI.IsOpen)
            return;

        if (inventory == null)
            return;

        InventorySlot slot = inventory.GetSlot(slotIndex);

        if (slot == null || slot.IsEmpty)
            return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        draggedSlot = slotIndex;
        draggedInventory = inventory;

        // Create an icon that follows the mouse.
        dragIcon = new GameObject("Dragged Item");
        dragIcon.transform.SetParent(
            GetComponentInParent<Canvas>().rootCanvas.transform, false);

        Image image = dragIcon.AddComponent<Image>();
        image.sprite = slot.item.icon;
        image.raycastTarget = false;
        image.preserveAspect = true;

        RectTransform rect = dragIcon.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(60, 60);

        MoveIcon(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
            MoveIcon(eventData);
    }

    private void MoveIcon(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;

        RectTransform canvasRect = canvas.transform as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera,
            out Vector2 position);

        dragIcon.GetComponent<RectTransform>().anchoredPosition = position;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (backpackUI == null || !backpackUI.IsOpen)
            return;

        if (draggedSlot < 0 || draggedInventory != inventory)
            return;

        inventory.MoveOrSwap(draggedSlot, slotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
            Destroy(dragIcon);

        draggedSlot = -1;
        draggedInventory = null;
    }
}