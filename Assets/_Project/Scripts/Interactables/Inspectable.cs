using System.Collections.Generic;
using UnityEngine;

// Something the technician can take a look at: a sofa, a console, a window, a poster on the wall. Walk up to it and press
// Q, and they say what they make of it over their head (TechnicianVoice): the next of its thoughts each look, the last
// one again after that. It never stops the player, and it has its own key and its own little tag ("[Q] Inspect"), so it
// never gets in the way of E and whatever that's showing over (InteractPrompt). Only the nearest one in reach shows it.
// Measured to its collider if it has one (the foot of a piece of furniture), to focus otherwise.
// Built by ChapterOneBuilder, for both chapters, with what the technician would think of each thing then.
public class Inspectable : MonoBehaviour
{
    public const KeyCode InspectKey = KeyCode.Q;

    [Tooltip("What the technician thinks, one per look, the last one again after that.")]
    [TextArea] public string[] thoughts = new string[0];
    [Tooltip("How close the player has to be.")]
    public float range = 1.2f;
    [Tooltip("The point that counts, from the object, when it has no collider.")]
    public Vector2 focus;
    [Tooltip("Where the tag shows, from the object.")]
    public Vector2 tagOffset = new Vector2(0f, 1f);

    static readonly List<Inspectable> all = new List<Inspectable>();
    static Inspectable nearest;
    static int nearestFrame = -1;
    static Transform player;
    static PlayerController playerController;

    private Collider2D footprint;
    private int looked;

    void Awake() => footprint = GetComponent<Collider2D>();
    void OnEnable() => all.Add(this);

    void OnDisable()
    {
        all.Remove(this);
        if (all.Count == 0) player = null;
    }

    void Update()
    {
        if (Nearest() != this) return;
        InspectTag.Show((Vector2)transform.position + tagOffset);
        if (Input.GetKeyDown(InspectKey)) Look();
    }

    public void Look()
    {
        if (thoughts.Length == 0) return;
        TechnicianVoice.Say(thoughts[Mathf.Min(looked, thoughts.Length - 1)]);
        looked++;
    }

    Vector2 Point(Vector2 from) => footprint != null ? footprint.ClosestPoint(from) : (Vector2)transform.position + focus;

    static Inspectable Nearest()
    {
        if (nearestFrame == Time.frameCount) return nearest;
        nearestFrame = Time.frameCount;
        nearest = null;
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found == null) return null;
            player = found.transform;
            playerController = found.GetComponent<PlayerController>();
        }
        bool free = !DialogueBox.Busy && !MapScreen.IsOpen && Time.timeScale > 0f && !Locker.IsPlayerHidden && !TutorialHud.ScreenCovered
                    && (playerController == null || (playerController.enabled && !playerController.IsScripted));
        if (!free) return null;

        Vector2 at = player.position;
        float best = float.PositiveInfinity;
        foreach (Inspectable thing in all)
        {
            float distance = (thing.Point(at) - at).magnitude;
            if (distance > thing.range || distance >= best) continue;
            best = distance;
            nearest = thing;
        }
        return nearest;
    }
}

// The "[Q] Inspect" tag over whatever can be inspected: the same slim tag as E's (PromptBadge), its words a shade softer.
public class InspectTag : MonoBehaviour
{
    const int SortingOrder = 104;           // just under E's prompt, if they ever meet
    static InspectTag instance;

    private Canvas canvas;
    private PromptBadge badge;
    private Vector3 target;
    private int shownFrame = -10;

    public static void Show(Vector2 at)
    {
        if (instance == null) instance = new GameObject("Inspect Tag", typeof(RectTransform)).AddComponent<InspectTag>();
        instance.target = at;
        instance.shownFrame = Time.frameCount;
    }

    void Awake()
    {
        canvas = PromptBadge.MakeCanvas(gameObject, SortingOrder);
        badge = new PromptBadge(transform, new Color(0.84f, 0.86f, 0.88f), 1);
        badge.SetText($"{Inspectable.InspectKey}  INSPECT");
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || shownFrame < Time.frameCount - 1 || TutorialHud.ScreenCovered) badge.Hide();
        else badge.ShowAt(cam, target, canvas);
    }
}
