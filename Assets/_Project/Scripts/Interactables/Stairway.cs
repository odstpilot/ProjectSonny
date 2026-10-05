using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Stairs to another floor. Walking onto them fades the screen to black and puts the player at the other end, standing
// just off those stairs on the floor (arrival). The other end is either destination, stairs on another floor in the same
// scene (every chapter has the whole station in it, one floor beside the other), or, with no destination, the stairs
// with the same index in targetScene, which is loaded. The floor builders number each floor's stairs from left to right,
// the way the blueprint lines them up, so the first stairs on floor 1 lead to the first on floor 2, and so on. With
// neither they go nowhere (a chapter that keeps the player on one floor).
// Setup: a trigger Collider2D over the stairs, and arrival on the floor beside them, clear of the trigger.
[RequireComponent(typeof(Collider2D))]
public class Stairway : MonoBehaviour
{
    [Tooltip("The stairs these lead to, on another floor in this scene. Takes the place of targetScene.")]
    public Stairway destination;
    [Tooltip("The scene the stairs lead to, by name, when there's no destination. Empty goes nowhere.")]
    public string targetScene = "";
    [Tooltip("Which stairs these are on their floor, counted from the left. They lead to the ones with the same number.")]
    public int index;
    [Tooltip("Where the player stands after coming up or down these stairs.")]
    public Transform arrival;
    public float fadeTime = 0.35f;
    [Tooltip("The scene's sound played as the screen goes dark. Empty for none.")]
    [SoundName] public string sound = "Stairs";

    // The stairs the player is on their way to, across the scene load.
    static string arrivingScene;
    static int arrivingIndex = -1;

    private bool leaving;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        if (arrivingIndex != index || arrivingScene != gameObject.scene.name) return;
        arrivingIndex = -1;
        arrivingScene = null;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Vector3 at = arrival != null ? arrival.position : transform.position;
            player.transform.position = new Vector3(at.x, at.y, player.transform.position.z);
            if (player.TryGetComponent(out Rigidbody2D body)) body.position = at;
            Camera cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out CameraFallow follow)) follow.SnapToPlayer();
        }

        // In from black, as the last floor went out.
        ScreenFade fade = ScreenFade.Get();
        fade.SetAlpha(1f);
        StartCoroutine(fade.FadeTo(0f, fadeTime));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (leaving || (destination == null && string.IsNullOrEmpty(targetScene)) || !other.CompareTag("Player")) return;
        if (Teleporter.IsTravelling || DialogueBox.IsOpen) return;
        StartCoroutine(Leave(other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject));
    }

    IEnumerator Leave(GameObject player)
    {
        leaving = true;
        if (player.TryGetComponent(out PlayerController controller)) controller.SetScriptedInput(Vector2.zero, false);
        ScreenFade fade = ScreenFade.Get();
        fade.PlaySound(sound);
        yield return fade.FadeTo(1f, fadeTime);

        if (destination != null)
        {
            // Out beside the other stairs, clear of them, so they don't send the player straight back.
            Vector3 at = destination.arrival != null ? destination.arrival.position : destination.transform.position;
            player.transform.position = new Vector3(at.x, at.y, player.transform.position.z);
            if (player.TryGetComponent(out Rigidbody2D body))
            {
                body.position = at;
                body.linearVelocity = Vector2.zero;
            }
            Camera cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out CameraFallow follow)) follow.SnapToPlayer();
            yield return null;
            if (controller != null) controller.ClearScriptedInput();
            yield return fade.FadeTo(0f, fadeTime);
            leaving = false;
            yield break;
        }

        arrivingScene = targetScene;
        arrivingIndex = index;
        SceneManager.LoadScene(targetScene);
    }

    void OnDrawGizmosSelected()
    {
        if (arrival == null) return;
        Gizmos.color = new Color(0.4f, 0.9f, 1f);
        Gizmos.DrawLine(transform.position, arrival.position);
        Gizmos.DrawWireSphere(arrival.position, 0.3f);
    }
}
