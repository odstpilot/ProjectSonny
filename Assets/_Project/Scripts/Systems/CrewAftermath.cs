using System.Collections;
using UnityEngine;

// Chapter 2's common grounds, once the technician's out of the ducts (StorageEscape): the crew are lying where they fell,
// all through the rooms, with scorch marks beside them. Finding them isn't the objective (PatrolReveal settles that the
// moment the technician steps out of the restroom); this is what Pip says about them on the way. Walking near one the
// first time, Pip says something: a few of them have lines of their own (the ones the technician met), the rest take
// the next of the shared lines. After seeing enough of them, Pip works out what they had in common, their badges, and
// that the technician's the only one without. In Chapter 2 as it stands, Pip says nothing over them (no lines set).
// Built by ChapterOneBuilder for ChapterTwoBuilder, which places the bodies where the crew were in Chapter 1.
public class CrewAftermath : MonoBehaviour
{
    [System.Serializable]
    public class Body
    {
        public Transform body;
        [Tooltip("What Pip says on finding this one. Empty takes the next of the shared lines.")]
        [TextArea] public string[] pipLines = new string[0];
    }

    public PlayerController player;
    public Body[] bodies = new Body[0];
    [Tooltip("How close the player has to come to find one.")]
    public float findRange = 2.6f;

    [Header("Pip")]
    [Tooltip("For the bodies with no lines of their own, one set each, in turn (| between lines). ~ opens a line with static.")]
    [TextArea] public string[] sharedLines = new string[0];
    [Tooltip("How many have to be found before Pip works it out.")]
    public int realizeAfter = 3;
    [TextArea] public string[] pipRealizes = new string[0];

    private int nextShared;

    void OnEnable()
    {
        StorageEscape.Escaped += BeginSearch;
    }

    void OnDisable()
    {
        StorageEscape.Escaped -= BeginSearch;
    }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
    }

    void BeginSearch() => StartCoroutine(Search());

    IEnumerator Search()
    {
        var found = new bool[bodies.Length];
        int count = 0;
        bool realized = false;
        while (!realized)
        {
            // Not over the technician taking it all in at the restroom door.
            if (PatrolReveal.Playing)
            {
                yield return null;
                continue;
            }
            for (int i = 0; i < bodies.Length; i++)
            {
                if (found[i] || bodies[i].body == null || player == null) continue;
                if (Vector2.Distance(player.transform.position, bodies[i].body.position) > findRange) continue;
                found[i] = true;
                count++;
                Tell(LinesFor(bodies[i]));
            }
            if (count >= Mathf.Min(realizeAfter, bodies.Length))
            {
                // Once what's being said has been said.
                while (SuitHelper.Exists && SuitHelper.Get().Talking) yield return null;
                realized = true;
                Tell(pipRealizes);
            }
            yield return null;
        }
    }

    string[] LinesFor(Body found)
    {
        if (found.pipLines.Length > 0) return found.pipLines;
        if (sharedLines.Length == 0) return new string[0];
        return sharedLines[nextShared++ % sharedLines.Length].Split('|');
    }

    static void Tell(string[] lines)
    {
        if (SuitHelper.Exists && lines.Length > 0) SuitHelper.Get().Tell(lines);
    }
}
