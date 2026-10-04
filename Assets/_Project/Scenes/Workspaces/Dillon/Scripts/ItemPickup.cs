using UnityEngine;

// Put on an object with a Collider2D set to Trigger. The player (or the Rigidbody2D) needs a collider too.
public class ItemPickup : MonoBehaviour
{
    public ItemData itemData;
    [Tooltip("For materials: how many you get.")]
    public int amount = 1;
    [Tooltip("On: walk over it to grab it (handy for testing). Off: stand near it and press E, like the GDD says.")]
    public bool pickupOnTouch = true;

    private PlayerInventory nearby;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null) return;

        nearby = inventory;
        if (pickupOnTouch) TryPickup();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (nearby != null && other.GetComponentInParent<PlayerInventory>() == nearby)
            nearby = null;
    }

    private void Update()
    {
        if (!pickupOnTouch && nearby != null && Time.timeScale > 0f && Input.GetKeyDown(KeyCode.E))
            TryPickup();
    }

    private void TryPickup()
    {
        if (nearby != null && nearby.AddItem(itemData, amount))
            Destroy(gameObject);
    }
}