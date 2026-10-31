using UnityEngine;

// chapter 2 station made darker
public class StationGloom : MonoBehaviour
{
    [Tooltip("Rooms made dark besides the ones already (DarkRooms.rooms), by their markers in the layout.")]
    public string alsoDark = "k";
    [Tooltip("How much light's left in the dark rooms.")]
    [Range(0f, 1f)] public float darkness = 0.2f;
    [Tooltip("How much of the station's light there is anywhere.")]
    [Range(0f, 1f)] public float lit = 0.8f;
    [Tooltip("The rooms whose lamps it takes to, by their markers.")]
    public string rooms = "hkbmcdows";
    [Tooltip("Share of the working lamps there that go out for good, and of the rest that flicker.")]
    [Range(0f, 1f)] public float lampsOut = 0.3f;
    [Range(0f, 1f)] public float lampsFailing = 0.25f;
    [Tooltip("How far the suit's glow reaches, in world units.")]
    public float suitReach = 3.4f;
    [Tooltip("The security cameras (StationCamera): how much of their cone's drawn, and of the light they throw, is left. " +
             "Faint, so a dark room stays dark, but still there to show where they're looking.")]
    [Range(0f, 1f)] public float cameraCone = 0.45f;
    [Range(0f, 1f)] public float cameraLight = 0.35f;

    private bool suitDone;

    void Awake()
    {
        var dark = FindAnyObjectByType<DarkRooms>();
        if (dark != null)
        {
            foreach (char room in alsoDark)
                if (dark.rooms.IndexOf(room) < 0) dark.rooms += room;
            dark.darkness = darkness;
            dark.lit = lit;
        }
    }

    void Start()
    {
        foreach (StationCamera watcher in StationCamera.All)
        {
            watcher.coneAlpha *= cameraCone;
            watcher.lightIntensity *= cameraLight;
        }
        foreach (StationLight lamp in FindObjectsByType<StationLight>())
        {
            if (lamp.mode != StationLight.Mode.Steady && lamp.mode != StationLight.Mode.Flicker) continue;
            if (!lamp.IsOn || lamp.IsBroken || rooms.IndexOf(SpiderRoutes.RoomAt(lamp.transform.position)) < 0) continue;
            float roll = Random.value;
            if (roll < lampsOut) lamp.PowerOff();
            else if (roll < lampsOut + lampsFailing) lamp.mode = StationLight.Mode.Flicker;
        }
    }

    void Update()
    {
        if (suitDone || SuitLight.Current == null) return;
        SuitLight.Current.radius = Mathf.Min(SuitLight.Current.radius, suitReach);
        suitDone = true;
        enabled = false;
    }
}
