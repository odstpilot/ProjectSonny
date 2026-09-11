using UnityEngine;
using UnityEngine.Events;

// An area that tells the level when the player walks into it, like crossing the doorway of a new room.
// Levels listen for PlayerEntered in code or hook up onPlayerEntered in the Inspector.
// Needs a trigger Collider2D on this object.
[RequireComponent(typeof(Collider2D))]
public class PlayerTriggerZone : MonoBehaviour
{
    [Tooltip("Only fire the first time the player walks in.")]
    public bool once = true;
    public UnityEvent onPlayerEntered = new UnityEvent();

    public event System.Action PlayerEntered;

    public bool HasFired { get; private set; }
    public bool PlayerInside { get; private set; }

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        PlayerInside = true;
        if (once && HasFired) return;

        HasFired = true;
        PlayerEntered?.Invoke();
        onPlayerEntered.Invoke();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other)) PlayerInside = false;
    }

    static bool IsPlayer(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        return other.CompareTag("Player") || (body != null && body.CompareTag("Player"));
    }
}
