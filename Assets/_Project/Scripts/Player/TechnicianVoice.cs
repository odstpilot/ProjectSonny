using System.Collections;
using UnityEngine;

// The technician talking to themselves: a line over their head, on the same little panel the crew talk on (CrewSpeech),
// for what they mutter as they go. Pip does the explaining; this is just them thinking out loud. Say(line) shows a line
// and carries on; yield return Think(lines) shows them one after another and waits until the last has been up long
// enough to read. Made the first time it's needed, and gone with the scene.
public static class TechnicianVoice
{
    const float HoldBase = 0.9f;
    const float HoldPerLetter = 0.035f;
    const float Gap = 0.1f;

    static CrewSpeech speech;

    // How long a line stays up.
    public static float HoldFor(string line) => HoldBase + line.Length * HoldPerLetter;

    public static void Say(string line, float seconds = -1f)
    {
        if (string.IsNullOrEmpty(line)) return;
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (speech == null) speech = CrewSpeech.Create(player.transform);
        speech.Show(line, seconds > 0f ? seconds : HoldFor(line));
    }

    public static IEnumerator Think(params string[] lines)
    {
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            Say(line);
            float hold = HoldFor(line) + Gap;
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;
        }
    }

    public static void Hush()
    {
        if (speech != null) speech.Hide();
    }
}
