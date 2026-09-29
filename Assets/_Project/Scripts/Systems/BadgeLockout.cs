using System.Collections;
using UnityEngine;

// Chapter 2, at a badge door (Teleporter.credential) the technician can't open: the control room's. In Chapter 1 the
// guard beside it badged them through; now there's nobody alive to, and the technician never had a badge of their own.
// The first time they walk up to it, Pip works out why every badge went off at once (Sonny cut them all off), and points
// them to where access comes from: the comms ring, upstairs. That's the objective after, pinned on the map.
// Built by ChapterTwoBuilder.
public class BadgeLockout : MonoBehaviour
{
    public PlayerController player;
    public Teleporter door;
    [Tooltip("How close to the door counts as trying it.")]
    public float tryRange = 1.6f;

    [Tooltip("The technician, at the door.")]
    [TextArea] public string[] technicianLines = { "Badge-only. Of course it is." };
    [Tooltip("Pip, after that. ~ opens a line with static; ^ says it happily.")]
    [TextArea] public string[] pipLines =
    {
        "~Sonny didn't just use the badges to find them. It cut every badge on the station off.",
        "~But access runs through the comms ring, upstairs. If I can get into its systems, maybe I can get you in.",
    };
    public string objective = "Find a way into the control room through the comms ring";
    [Tooltip("The room pinned on the map for it, by its marker in the layout (it's on floor 2), and what the pin says.")]
    public char targetRoom = 'g';
    public string targetLabel = "Comms Ring";

    // Picking up from a save after it's happened (ChapterTwoDirector): no waiting at the door, just where to go.
    public void ResumeLockedOut()
    {
        StopAllCoroutines();
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
    }

    IEnumerator Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (door == null || player == null) yield break;
        var trigger = door.GetComponent<Collider2D>();

        while (true)
        {
            if (door.NeedsBadge)
            {
                Vector2 at = player.transform.position;
                Vector2 closest = trigger != null ? trigger.ClosestPoint(at) : (Vector2)door.transform.position;
                if (Vector2.Distance(closest, at) <= tryRange) break;
            }
            yield return null;
        }

        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        yield return TechnicianVoice.Think(technicianLines);
        if (SuitHelper.Exists) SuitHelper.Get().Tell(pipLines);
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
    }
}
