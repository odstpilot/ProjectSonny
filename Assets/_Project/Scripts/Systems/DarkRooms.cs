using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// rooms with the power out
public class DarkRooms : MonoBehaviour
{
    [Tooltip("The rooms with the power out, by their markers in the layout (on either floor).")]
    public string rooms = "hbvmcodgr";
    [Tooltip("How much of the light is left in them.")]
    [Range(0f, 1f)] public float darkness = 0.2f;
    [Tooltip("How much of the station's light there is anywhere, dark rooms or not (StationGloom turns it down).")]
    [Range(0f, 1f)] public float lit = 1f;
    [Tooltip("Seconds the light takes to go, or come back.")]
    public float fadeSeconds = 0.7f;
    [Tooltip("What the bots can see in the dark, as a fraction of their sight range.")]
    [Range(0.1f, 1f)] public float sightInDark = 0.7f;
    [Tooltip("And in a blacked-out room.")]
    [Range(0.1f, 1f)] public float sightInBlackout = 0.5f;

    static DarkRooms current;
    static readonly HashSet<char> blackedOut = new HashSet<char>();

    public static void SetBlackout(char room, bool on)
    {
        if (on) blackedOut.Add(room);
        else blackedOut.Remove(room);
    }

    public static bool IsBlackedOut(char room) => blackedOut.Contains(room);

    public static bool InDark => current != null && current.inDark;
    public static float SightScale => !InDark ? 1f : current.inBlackout ? current.sightInBlackout : current.sightInDark;
    public static bool InBlackout => current != null && current.inBlackout;

    private readonly List<(Light2D light, float intensity)> station = new List<(Light2D, float)>();
    private Transform player;
    private SuitLight suit;
    private bool inDark, inBlackout, sawDark;
    private float level = 1f;

    void OnEnable() => current = this;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay() => blackedOut.Clear();

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
        StationMap map = StationMap.Current;
        if (map != null && map.Locate(player.position, out _, out StationMap.Room room))
        {
            inBlackout = blackedOut.Contains(room.marker);
            inDark = inBlackout || rooms.IndexOf(room.marker) >= 0;
        }

        if (inDark && !sawDark)
        {
            sawDark = true;
            if (SuitHelper.Exists) SuitHelper.Get().Tell(PipManager.Lines.firstDark);
        }
        float target = inBlackout ? 0f : inDark ? darkness : 1f;
        level = Mathf.MoveTowards(level, target, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds) * (1f - darkness));
        foreach ((Light2D light, float intensity) in station)
            if (light != null) light.intensity = intensity * level * lit;
        if (suit != null)
        {
            suit.Level = inBlackout ? 0f : Mathf.InverseLerp(1f, darkness, level);
            suit.Blackout = inBlackout;
        }
    }
}
