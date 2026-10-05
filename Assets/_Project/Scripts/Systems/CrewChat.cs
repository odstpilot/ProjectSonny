using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A few crew standing in a ring, talking. They take the lines of the conversation in turn round the ring: whoever's
// talking looks at the one they're talking to, and the rest look at them. The conversation starts when the player comes
// near enough to overhear, waits for them whenever they wander off, and is only had once: after it they fall quiet,
// facing each other, so the player never hears the same thing twice, until something gives them something new to talk
// about (Converse). Built by ChapterOneBuilder, with its members set to Role.Chat.
public class CrewChat : MonoBehaviour
{
    public List<CrewMember> members = new List<CrewMember>();
    [Tooltip("Said in order, each by the next one round the ring.")]
    [TextArea] public string[] lines = new string[0];
    [Tooltip("How close the player has to be, from the middle of the ring, to hear what's said.")]
    public float hearingRange = 6f;
    [Tooltip("Seconds between one line and the next, from and to.")]
    public Vector2 gapSeconds = new Vector2(0.3f, 1f);

    private Transform player;

    void Start()
    {
        members.RemoveAll(member => member == null);
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null) player = found.transform;
        StartCoroutine(Run());
    }

    // A new conversation, in place of whatever they were saying (or had finished saying).
    public void Converse(string[] newLines)
    {
        StopAllCoroutines();
        lines = newLines;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (members.Count < 2 || lines.Length == 0) yield break;
        FaceMiddle();
        for (int i = 0; i < lines.Length; i++)
        {
            // Nothing's said out of the player's hearing, so they don't miss any of it.
            while (!Overheard()) yield return null;

            CrewMember speaker = members[i % members.Count];
            CrewMember listener = members[(i + 1) % members.Count];
            speaker.FaceTowards(listener.Position);
            foreach (CrewMember member in members)
                if (member != speaker) member.FaceTowards(speaker.Position);

            // Long enough to read.
            float seconds = Mathf.Clamp(1.6f + lines[i].Length * 0.06f, 2.4f, 6f);
            speaker.Say(lines[i], seconds);
            yield return new WaitForSeconds(seconds + Random.Range(gapSeconds.x, gapSeconds.y));
        }
        FaceMiddle();
    }

    void FaceMiddle()
    {
        foreach (CrewMember member in members) member.FaceTowards(transform.position);
    }

    bool Overheard() => player != null && ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude <= hearingRange * hearingRange;
}
