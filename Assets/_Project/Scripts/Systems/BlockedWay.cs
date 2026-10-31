using System.Collections;
using UnityEngine;

// a way that turns out to be shut, or a room reached
public class BlockedWay : MonoBehaviour
{
    public PlayerController player;
    [Tooltip("Points the player comes near to find it, in the world.")]
    public Vector2[] spots = new Vector2[0];
    public float range = 2.8f;
    [Tooltip("Or instead, getting into this room, by its marker in the layout (on whichever floor it's on).")]
    public char arriveInRoom;
    [Tooltip("Only while this is the objective. Empty for any time.")]
    public string afterObjective = "";

    [Tooltip("The technician, finding it.")]
    [TextArea] public string[] technicianLines = new string[0];
    [Tooltip("What Pip says after that: its lines for this moment, in the Pip Manager (PipManager). None for nothing.")]
    public PipManager.Moment pip = PipManager.Moment.None;
    [Tooltip("The objective after. Empty leaves it as it is.")]
    public string objective = "";
    [Tooltip("The room pinned on the map for it, by its marker in the layout, and what the pin says.")]
    public char targetRoom;
    public string targetLabel = "";
    [Tooltip("Once it's found, the player comes back here after dying, for the stage it starts.")]
    public bool setsRespawn;
    public Vector2 respawnAt;

    [Header("Testing")]
    [Tooltip("Its name as a stage for the Checkpoint Tester (CheckpointJump), and where the player stands for it.")]
    public string stage = "";
    public Vector2 standAt;

    IEnumerator Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null || (spots.Length == 0 && arriveInRoom == '\0')) yield break;
        while (!Found()) yield return null;

        TechnicianVoice.Hush();
        if (SuitHelper.Exists) SuitHelper.Get().Hush();
        yield return TechnicianVoice.Think(technicianLines);
        string[] pipLines = PipManager.Lines.For(pip);
        if (SuitHelper.Exists && pipLines.Length > 0) yield return SuitHelper.Get().Say(pipLines);
        SetObjective();
    }

    bool Found()
    {
        if (!string.IsNullOrEmpty(afterObjective) && TutorialHud.Get().Objective != afterObjective) return false;
        Vector2 at = player.transform.position;
        if (arriveInRoom != '\0')
        {
            StationMap map = StationMap.Current;
            return map != null && map.Locate(at, out _, out StationMap.Room room) && room.marker == arriveInRoom;
        }
        foreach (Vector2 spot in spots)
            if (Vector2.Distance(at, spot) <= range) return true;
        return false;
    }

    public void ResumePassed()
    {
        StopAllCoroutines();
        SetObjective();
    }

    void SetObjective()
    {
        if (setsRespawn && player != null && player.TryGetComponent(out PlayerHealthHandler handler))
            handler.RespawnPoint = new Vector3(respawnAt.x, respawnAt.y, player.transform.position.z);
        if (string.IsNullOrEmpty(objective)) return;
        TutorialHud.Get().SetObjective(objective);
        if (targetRoom != '\0') MapScreen.SetTarget(targetRoom, targetLabel);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f);
        foreach (Vector2 spot in spots) Gizmos.DrawWireSphere(spot, range);
    }
}
