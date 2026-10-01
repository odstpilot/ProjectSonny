using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Chapter 2, past the lounge: the power's out. Stepping into one of these rooms (by their markers in the layout), the
// station's light (the global light, already down to emergency red) fades almost to nothing, and comes back up on the
// way out, while the technician's suit glows round them (SuitLight) so they can see a little way. Most of the lamps in
// them are out for good, the odd one flickering (ChapterTwoBuilder). The first time, Pip says so.
// The dark hides the technician too: the bots can't see as far in it (SightScale, PlaceholderRobot).
// Built by ChapterTwoBuilder.
public class DarkRooms : MonoBehaviour
{
    [Tooltip("The rooms with the power out, by their markers in the layout (on either floor).")]
    public string rooms = "hbvmcodgr";
    [Tooltip("How much of the light is left in them.")]
    [Range(0f, 1f)] public float darkness = 0.1f;
    [Tooltip("Seconds the light takes to go, or come back.")]
    public float fadeSeconds = 0.7f;
    [Tooltip("What the bots can see in the dark, as a fraction of their sight range.")]
    [Range(0.1f, 1f)] public float sightInDark = 0.7f;
    [Tooltip("Pip, the first time it's dark. ~ opens a line with static.")]
    [TextArea] public string[] pipFirstDark = { "~The power's out past the lounge. Your suit gives off a little light, at least. Stay close to it." };

    static DarkRooms current;

    // The player's in one of the dark rooms.
    public static bool InDark => current != null && current.inDark;
    // How far the bots can see the player, as a fraction of their sight range: 1 in the light.
    public static float SightScale => InDark ? current.sightInDark : 1f;

    private readonly List<(Light2D light, float intensity)> station = new List<(Light2D, float)>();
    private Transform player;
    private SuitLight suit;
    private bool inDark, sawDark;
    private float level = 1f;

    void OnEnable() => current = this;

    void OnDisable()
    {
        if (current == this) current = null;
    }

    void Start()
    {
        foreach (Light2D light in FindObjectsByType<Light2D>())
            if (light.lightType == Light2D.LightType.Global) station.Add((light, light.intensity));
        GameObject found = GameObject.FindWithTag("Player");
        if (found == null) return;
        player = found.transform;
        if (!found.TryGetComponent(out suit)) suit = found.AddComponent<SuitLight>();
    }

    void Update()
    {
        if (player == null) return;
        // Between rooms (a doorway, the stairs) it stays as it was.
        StationMap map = StationMap.Current;
        if (map != null && map.Locate(player.position, out _, out StationMap.Room room))
            inDark = rooms.IndexOf(room.marker) >= 0;

        if (inDark && !sawDark)
        {
            sawDark = true;
            if (SuitHelper.Exists) SuitHelper.Get().Tell(pipFirstDark);
        }
        level = Mathf.MoveTowards(level, inDark ? darkness : 1f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds) * (1f - darkness));
        foreach ((Light2D light, float intensity) in station)
            if (light != null) light.intensity = intensity * level;
        // The suit's glow, coming up as the light goes.
        if (suit != null) suit.Level = Mathf.InverseLerp(1f, darkness, level);
    }
}
