using UnityEngine;

public class Teleporter : MonoBehaviour
{
    public Transform teleportTarget; // Target to teleport the player to
    public Animator animator;        // Animator to play animation

    private bool hasTeleported = false; // Prevent multiple triggers (optional)

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasTeleported && other.CompareTag("Player"))
        {
            hasTeleported = true;

            
            animator.SetTrigger("Teleport");
            

            // Optionally wait for animation delay (requires coroutine)
            StartCoroutine(TeleportAfterDelay(other.transform, 0.3f));
        }
    }

    private System.Collections.IEnumerator TeleportAfterDelay(Transform player, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (teleportTarget != null)
        {
            player.position = teleportTarget.position;
        }

        hasTeleported = false;
    }
}