using System.Collections;
using UnityEngine;

// dead badge at the reactor door
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
    [TextArea] public string[] technicianLines = { "My badge is dead, the reader won't even blink" };
    public string objective = "Reactivate your badge in the comms ring";
    [Tooltip("The room pinned on the map for it, by its marker in the layout (it's on floor 2), and what the pin says.")]
    public char targetRoom = 'g';
    public string targetLabel = "Comms Ring";
    [Tooltip("Where the player stands for a tester's jump (CheckpointJump): a few steps from one of the doors.")]
    public Vector2 standAt;

    public void ResumeLockedOut()
    {
        StopAllCoroutines();
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
    }

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
        if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.badgeLockout);
        TutorialHud.Get().SetObjective(objective);
        MapScreen.SetTarget(targetRoom, targetLabel);
    }
}
