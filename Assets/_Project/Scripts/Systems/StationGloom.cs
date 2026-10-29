using UnityEngine;

// Chapter 2, darker than it was built: the station's light turned down everywhere (DarkRooms.lit), more of its rooms
// dark (the lounge too, where the crew lie), the dark ones darker still, a good share of the lamps gone out for good and
// more of the rest failing, the security cameras' cones and light faint (StationCamera), and the technician's suit
// throwing its glow less far (SuitLight). The comms ring and the
// reactor are left to sort out their own light.
// Added by ChapterTwoDirector.
public class StationGloom : MonoBehaviour
{
    [Tooltip("Rooms made dark besides the ones already (DarkRooms.rooms), by their markers in the layout.")]
    public string alsoDark = "k";
    [Tooltip("How much light's left in the dark rooms.")]
    [Range(0f, 1f)] public float darkness = 0.05f;
    [Tooltip("How much of the station's light there is anywhere.")]
    [Range(0f, 1f)] public float lit = 0.6f;
    [Tooltip("The rooms whose lamps it takes to, by their markers.")]
    public string rooms = "hkbmcdows";
    [Tooltip("Share of the working lamps there that go out for good, and of the rest that flicker.")]
    [Range(0f, 1f)] public float lampsOut = 0.4f;
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
            dark.darkness = Mathf.Min(dark.darkness, darkness);
            dark.lit = lit;
        }
    }

    // Once every lamp's switched itself on: some of them out, some failing. And the cameras dimmed.
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

    // The suit's light only turns up once the player's in the dark for the first time (DarkRooms adds it).
    void Update()
    {
        if (suitDone || SuitLight.Current == null) return;
        SuitLight.Current.radius = Mathf.Min(SuitLight.Current.radius, suitReach);
        suitDone = true;
        enabled = false;
    }
}
