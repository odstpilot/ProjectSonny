using UnityEngine;

// Keeps the settings that need a running object working in every scene: it silences the game while its window is in
// the background, if the player asked for that, and draws the frame rate counter. GameSettings creates it before the
// first scene, so nothing needs setting up.
public class SettingsRunner : MonoBehaviour
{
    static SettingsRunner instance;

    private float smoothedDelta = 1f / 60f;
    private GUIStyle style;

    public static void Ensure()
    {
        if (instance != null) return;
        var host = new GameObject("[Settings]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SettingsRunner>();
    }

    void OnApplicationFocus(bool focused)
    {
        GameSettings.SetFocused(focused);
    }

    void Update()
    {
        smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.1f);
    }

    void OnGUI()
    {
        if (!GameSettings.ShowFps) return;
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        string text = $"{Mathf.RoundToInt(1f / Mathf.Max(0.0001f, smoothedDelta))} FPS";
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(13f, 11f, 160f, 28f), text, style);
        style.normal.textColor = new Color(0.55f, 1f, 0.95f);
        GUI.Label(new Rect(12f, 10f, 160f, 28f), text, style);
    }
}
