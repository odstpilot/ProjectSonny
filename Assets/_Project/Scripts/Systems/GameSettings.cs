using System.Collections.Generic;
using UnityEngine;

// The player's settings, kept between sessions. They're loaded before the first scene, so they apply in every scene,
// the title screen or not. Change them through the setters, which apply and store them; Save writes them to disk.
// Display mode and resolution are here too, but Unity remembers those between sessions itself.
// Music and Effects volumes aren't applied to every sound by themselves: an audio source follows them if its script
// multiplies its volume by MusicVolume or EffectsVolume (AudioManager and the title screen do). Master volume covers
// everything.
public static class GameSettings
{
    const string Prefix = "Sonny.";

    public static readonly string[] FrameRateNames = { "30", "60", "120", "144", "240", "UNLIMITED" };
    static readonly int[] FrameRates = { 30, 60, 120, 144, 240, -1 };

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;
    public static float EffectsVolume { get; private set; } = 1f;
    public static bool MuteInBackground { get; private set; }
    public static bool VSync { get; private set; } = true;
    public static int FrameRateIndex { get; private set; } = FrameRates.Length - 1;
    // CameraShake is multiplied by this: 0 turns it off.
    public static float ScreenShake { get; private set; } = 1f;
    // Whether HitStop freezes the game for a moment when hits land.
    public static bool FreezeOnHit { get; private set; } = true;
    // Scanlines and film grain on screens and menus.
    public static bool ScreenEffects { get; private set; } = true;
    public static bool ShowFps { get; private set; }

    static bool focused = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", 1f);
        MusicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", 1f);
        EffectsVolume = PlayerPrefs.GetFloat(Prefix + "EffectsVolume", 1f);
        MuteInBackground = PlayerPrefs.GetInt(Prefix + "MuteInBackground", 0) == 1;
        VSync = PlayerPrefs.GetInt(Prefix + "VSync", 1) == 1;
        FrameRateIndex = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "FrameRate", FrameRates.Length - 1), 0, FrameRates.Length - 1);
        ScreenShake = PlayerPrefs.GetFloat(Prefix + "ScreenShake", 1f);
        FreezeOnHit = PlayerPrefs.GetInt(Prefix + "HitStop", 1) == 1;
        ScreenEffects = PlayerPrefs.GetInt(Prefix + "ScreenEffects", 1) == 1;
        ShowFps = PlayerPrefs.GetInt(Prefix + "ShowFps", 0) == 1;
        ApplyVolume();
        ApplyFrameRate();
        SettingsRunner.Ensure();
    }

    // Everything back to how it started, except the display mode and resolution.
    public static void ResetToDefaults()
    {
        SetMasterVolume(1f);
        SetMusicVolume(1f);
        SetEffectsVolume(1f);
        SetMuteInBackground(false);
        SetVSync(true);
        SetFrameRateIndex(FrameRates.Length - 1);
        SetScreenShake(1f);
        SetFreezeOnHit(true);
        SetScreenEffects(true);
        SetShowFps(false);
    }

    public static void Save() => PlayerPrefs.Save();

    public static void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(Prefix + "MasterVolume", MasterVolume);
        ApplyVolume();
    }

    public static void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(Prefix + "MusicVolume", MusicVolume);
    }

    public static void SetEffectsVolume(float volume)
    {
        EffectsVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(Prefix + "EffectsVolume", EffectsVolume);
    }

    public static void SetMuteInBackground(bool on)
    {
        MuteInBackground = on;
        PlayerPrefs.SetInt(Prefix + "MuteInBackground", on ? 1 : 0);
        ApplyVolume();
    }

    public static void SetVSync(bool on)
    {
        VSync = on;
        PlayerPrefs.SetInt(Prefix + "VSync", on ? 1 : 0);
        ApplyFrameRate();
    }

    public static void SetFrameRateIndex(int index)
    {
        FrameRateIndex = Mathf.Clamp(index, 0, FrameRates.Length - 1);
        PlayerPrefs.SetInt(Prefix + "FrameRate", FrameRateIndex);
        ApplyFrameRate();
    }

    public static void SetScreenShake(float amount)
    {
        ScreenShake = Mathf.Clamp01(amount);
        PlayerPrefs.SetFloat(Prefix + "ScreenShake", ScreenShake);
    }

    public static void SetFreezeOnHit(bool on)
    {
        FreezeOnHit = on;
        PlayerPrefs.SetInt(Prefix + "HitStop", on ? 1 : 0);
    }

    public static void SetScreenEffects(bool on)
    {
        ScreenEffects = on;
        PlayerPrefs.SetInt(Prefix + "ScreenEffects", on ? 1 : 0);
    }

    public static void SetShowFps(bool on)
    {
        ShowFps = on;
        PlayerPrefs.SetInt(Prefix + "ShowFps", on ? 1 : 0);
    }

    // SettingsRunner calls this when the game's window gains or loses focus.
    public static void SetFocused(bool isFocused)
    {
        focused = isFocused;
        ApplyVolume();
    }

    static void ApplyVolume()
    {
        AudioListener.volume = MuteInBackground && !focused ? 0f : MasterVolume;
    }

    static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = VSync ? -1 : FrameRates[FrameRateIndex];
    }

    // --- Display ---

    public static void SetDisplayMode(FullScreenMode mode)
    {
        Screen.fullScreenMode = mode;
    }

    // Every size the display offers, smallest first, each once whatever its refresh rates. Never empty.
    public static List<Vector2Int> Resolutions()
    {
        var sizes = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (!sizes.Contains(size)) sizes.Add(size);
        }
        var current = new Vector2Int(Screen.width, Screen.height);
        if (!sizes.Contains(current)) sizes.Add(current);
        sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return sizes;
    }

    public static void SetResolution(Vector2Int size)
    {
        Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
    }
}
