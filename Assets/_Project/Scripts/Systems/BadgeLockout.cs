using System.Collections;
using UnityEngine;

// Chapter 2, at a badge door (Teleporter.credential) the technician can't open: the reactor core's, the only way left
// down to the control room. Nobody's alive to badge them through, and the technician never had a badge of their own.
// Any of its doors will do (doors).
// The first time they walk up to one while they're looking for the control room (afterObjective), Pip says why (Sonny
// cut every badge off), and sends them to the comms ring next door, hoping there's a badge there (there isn't, but
// there's a way to get a unit's access: CommsRing). That's the objective after, pinned on the map.
// Built by ChapterOneBuilder for ChapterTwoBuilder.
public class BadgeLockout : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("The badge doors that set it off, whichever they try first.")]
    public Teleporter[] doors = new Teleporter[0];
    [Tooltip("How close to the door counts as trying it.")]
    public float tryRange = 1.6f;
    [Tooltip("Only while this is the objective. Empty for any time.")]
    public string afterObjective = "";

    [Tooltip("The technician, at the door.")]
    [TextArea] public string[] technicianLines = { "The reactor core's badge-only. Of course it is." };
    [Tooltip("Pip, after that. ~ opens a line with static; ^ says it happily.")]
    [TextArea] public string[] pipLines =
    {
        "~Sonny cut off every badge on the station.",
        "~Try the comms ring, next door. If there's a badge left anywhere, it's there.",
    };
    public string objective = "Find a badge in the comms ring";
    [Tooltip("The room pinned on the map for it, by its marker in the layout (it's on floor 2), and what the pin says.")]
    public char targetRoom = 'g';
    public string targetLabel = "Comms Ring";
    [Tooltip("Where the player stands for a tester's jump (CheckpointJump): a few steps from one of the doors.")]
    public Vector2 standAt;

    // Picking up from a save after it's happened (ChapterTwoDirector): no waiting at the door, just where to go.
    public void ResumeLockedOut()
    {
        StopAllCoroutines();
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
    }

    // Close enough to one of the doors that won't open for them, while it matters.
    bool TriedOne()
    {
        if (!string.IsNullOrEmpty(afterObjective) && TutorialHud.Get().Objective != afterObjective) return false;
        Vector2 at = player.transform.position;
        foreach (Teleporter door in doors)
        {
            if (door == null || !door.NeedsBadge) continue;
            var trigger = door.GetComponent<Collider2D>();
            Vector2 closest = trigger != null ? trigger.ClosestPoint(at) : (Vector2)door.transform.position;
            if (Vector2.Distance(closest, at) <= tryRange) return true;
        }
        return false;
    }

    IEnumerator Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (doors.Length == 0 || player == null) yield break;

        while (!TriedOne()) yield return null;

        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        yield return TechnicianVoice.Think(technicianLines);
        if (SuitHelper.Exists) SuitHelper.Get().Tell(pipLines);
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
    }
}
