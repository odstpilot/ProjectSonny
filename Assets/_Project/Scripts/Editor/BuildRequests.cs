using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lets the level builders be run from outside the editor while it's open. Unity won't open a project a second time in
// batch mode, so -executeMethod can't be used then; instead, write one of the names below to Temp/SonnyBuild.request
// and the open editor picks it up within a second or so, runs it, and writes how it went, with everything it logged,
// to Temp/SonnyBuild.result. "Refresh" imports changed files (and recompiles scripts) without anyone switching to Unity.
// "Capture <scene> <x> <y> [size]" renders what the scene's camera sees from there (world units; size is the camera's
// half height, left out for the camera's own) to Temp/SonnyCapture.png, for checking a build without pressing Play.
// Nothing runs in a capture, so it shows the scene as it's saved, before any script has started.
// "Play <scene> <seconds> [x y [size]]" is the same, but plays the scene for that many seconds first, so the capture
// shows the game as it runs; the camera stays where the game put it unless given a point. It comes back out of Play
// mode afterwards, and the result lists every warning and error logged while it played. Ending it with
// "player <x> <y>" puts the player there a second before the capture, to see them somewhere in particular, and
// "player <x> <y> walk <dx> <dy>" has them walk that way from there until the capture.
//
// It won't build over unsaved scene changes, or while playing or compiling, and it puts back the scene that was open.
[InitializeOnLoad]
static class BuildRequests
{
    const string RequestPath = "Temp/SonnyBuild.request";
    const string ResultPath = "Temp/SonnyBuild.result";

    const string CapturePath = "Temp/SonnyCapture.png";
    static readonly Vector2Int CaptureSize = new Vector2Int(1600, 900);

    // The scenes a capture can be asked for by name. Anything else is taken as a path.
    static readonly Dictionary<string, string> Scenes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Tutorial", "Assets/_Project/Scenes/Levels/Tutorial.unity" },
        { "Chapter1", "Assets/_Project/Scenes/Levels/Chapter1.unity" },
        { "Chapter2", "Assets/_Project/Scenes/Levels/Chapter2.unity" },
        { "Blueprint", "Assets/_Project/Scenes/Levels/StationBlueprint.unity" },
    };

    static readonly Dictionary<string, Func<bool>> Builders = new Dictionary<string, Func<bool>>(StringComparer.OrdinalIgnoreCase)
    {
        { "Tutorial", TutorialLevelBuilder.Build },
        { "Chapter1", ChapterOneBuilder.Build },
        { "Chapter2", ChapterTwoBuilder.Build },
        { "Blueprint", StationBlueprintBuilder.Build },
    };

    // A play capture in progress. It's kept in SessionState, since going in and out of Play mode reloads the scripts.
    const string PlayKey = "Sonny.PlayCapture.";
    const double PlayStartTimeout = 60.0;

    static double nextCheck;

    static BuildRequests()
    {
        EditorApplication.update += Poll;
        if (SessionState.GetBool(PlayKey + "Active", false)) Application.logMessageReceived += RecordPlayLog;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + (SessionState.GetBool(PlayKey + "Active", false) ? 0.2 : 1.0);
        if (SessionState.GetBool(PlayKey + "Active", false))
        {
            UpdatePlayCapture();
            return;
        }
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

        string request = File.ReadAllText(RequestPath).Trim();
        File.Delete(RequestPath);

        if (request.Equals("Refresh", StringComparison.OrdinalIgnoreCase))
        {
            AssetDatabase.Refresh();
            Finish(true, "Refreshed. Scripts recompile next if any changed.\n");
            return;
        }
        string[] words = request.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        bool capture = words.Length > 0 && words[0].Equals("Capture", StringComparison.OrdinalIgnoreCase);
        bool play = words.Length > 0 && words[0].Equals("Play", StringComparison.OrdinalIgnoreCase);
        Func<bool> build = null;
        if (!capture && !play && !Builders.TryGetValue(request, out build))
        {
            Finish(false, $"There's no builder called \"{request}\". Try one of: {string.Join(", ", Builders.Keys)}, or Refresh.\n");
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Finish(false, $"{open.name} has unsaved changes. Save or discard them in Unity first.\n");
                return;
            }
        }

        string previous = SceneManager.GetActiveScene().path;
        if (play)
        {
            StartPlayCapture(words, previous);
            return;
        }

        var log = new StringBuilder();
        void Record(string message, string stack, LogType type) => log.AppendLine($"{type}: {message}");
        Application.logMessageReceived += Record;
        bool ok;
        try
        {
            ok = capture ? Capture(words) : build();
        }
        catch (Exception e)
        {
            ok = false;
            log.AppendLine("Exception: " + e);
        }
        finally
        {
            Application.logMessageReceived -= Record;
        }

        // A capture always leaves its scene changed, so it's always put back, even over the same scene.
        if (!string.IsNullOrEmpty(previous) && (capture || SceneManager.GetActiveScene().path != previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        else if (capture)
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Finish(ok, log.ToString());
    }

    // For batch mode, with Unity closed: Unity -batchmode -projectPath <project> -executeMethod BuildRequests.CaptureFromCommandLine
    // -sonnyCapture "Chapter1 12 30 [size]" renders the same as a Capture request to Temp/SonnyCapture.png.
    public static void CaptureFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-sonnyCapture");
        bool ok = at >= 0 && at + 1 < args.Length && Capture(("Capture " + args[at + 1]).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        // Unity empties Temp on the way out, so -sonnyCaptureOut <path> keeps a copy somewhere else.
        int outAt = Array.IndexOf(args, "-sonnyCaptureOut");
        if (ok && outAt >= 0 && outAt + 1 < args.Length) File.Copy(CapturePath, args[outAt + 1], true);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Capture(string[] words)
    {
        if (words.Length < 4 || !float.TryParse(words[2], out float x) || !float.TryParse(words[3], out float y))
        {
            Debug.LogError("Capture needs a scene and a point: Capture Chapter1 12 30 [size]");
            return false;
        }
        string path = Scenes.TryGetValue(words[1], out string known) ? known : words[1];
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        Camera cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogError($"There's no camera in {path}.");
            return false;
        }
        cam.transform.position = new Vector3(x, y, cam.transform.position.z);
        if (words.Length > 4 && float.TryParse(words[4], out float size)) cam.orthographicSize = size;
        RenderToFile(cam);

        // Moving the camera marked the scene changed; the scene put back afterwards replaces it without saving.
        return true;
    }

    // The camera's view, with the on-screen UI (overlay canvases: the HUD, prompts, dialogue) drawn over it too: for the
    // shot, each is put in front of the camera and laid out again for the capture's size.
    static void RenderToFile(Camera cam)
    {
        var target = new RenderTexture(CaptureSize.x, CaptureSize.y, 24);
        cam.targetTexture = target;
        var overlays = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (Canvas canvas in overlays)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.1f;
            canvas.sortingLayerName = "UI";     // over the level, as it is on screen
        }
        Canvas.ForceUpdateCanvases();
        foreach (Canvas canvas in overlays) canvas.gameObject.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
        Canvas.ForceUpdateCanvases();
        cam.Render();
        foreach (Canvas canvas in overlays)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.sortingLayerName = "Default";
        }
        cam.targetTexture = null;
        RenderTexture.active = target;
        var image = new Texture2D(CaptureSize.x, CaptureSize.y, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, CaptureSize.x, CaptureSize.y), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(CapturePath, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
    }

    // --- Play captures ---

    // For batch mode, with Unity closed: Unity -batchmode -projectPath <project> -executeMethod BuildRequests.PlayFromCommandLine
    // -sonnyPlay "Chapter1 8" [-sonnyJump <stage>] -sonnyCaptureOut <path.png>. Plays the scene (from that stage, as the
    // Checkpoint Tester would), captures as a Play request does, writes what was logged to <path.png>.txt, and quits.
    const string CommandLineKey = "Sonny.PlayCapture.CommandLineOut";

    public static void PlayFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string Arg(string name)
        {
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
        string play = Arg("-sonnyPlay"), jump = Arg("-sonnyJump"), output = Arg("-sonnyCaptureOut");
        if (play == null || output == null)
        {
            Debug.LogError("PlayFromCommandLine needs -sonnyPlay \"<scene> <seconds> ...\" and -sonnyCaptureOut <path>.");
            EditorApplication.Exit(1);
            return;
        }
        string[] words = ("Play " + play).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        SessionState.SetString(CommandLineKey, output);
        if (!string.IsNullOrEmpty(jump)) SessionState.SetString(CheckpointJump.EditorRequestKey, $"{words[1]}|{jump}");
        StartPlayCapture(words, "");
    }

    static void StartPlayCapture(string[] words, string previous)
    {
        if (words.Length < 3 || !float.TryParse(words[2], out _))
        {
            Finish(false, "Play needs a scene and how long to play it: Play Chapter1 10 [x y [size]]\n");
            return;
        }
        string path = Scenes.TryGetValue(words[1], out string known) ? known : words[1];
        SessionState.SetBool(PlayKey + "Active", true);
        SessionState.SetString(PlayKey + "Words", string.Join(" ", words));
        SessionState.SetString(PlayKey + "Previous", previous);
        SessionState.SetString(PlayKey + "Log", "");
        SessionState.SetFloat(PlayKey + "Requested", (float)EditorApplication.timeSinceStartup);
        SessionState.SetFloat(PlayKey + "Started", -1f);
        SessionState.SetBool(PlayKey + "Taken", false);
        SessionState.SetBool(PlayKey + "Placed", false);
        Application.logMessageReceived += RecordPlayLog;

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    // Waits for Play mode, plays for the time asked, captures, leaves Play mode, and reports.
    static void UpdatePlayCapture()
    {
        List<string> words = SessionState.GetString(PlayKey + "Words", "").Split(' ').ToList();
        int playerAt = words.FindIndex(w => w.Equals("player", StringComparison.OrdinalIgnoreCase));
        Vector2? placeAt = null;
        if (playerAt >= 0 && words.Count > playerAt + 2 && float.TryParse(words[playerAt + 1], out float px) && float.TryParse(words[playerAt + 2], out float py))
            placeAt = new Vector2(px, py);
        List<string> extras = playerAt >= 0 ? words.GetRange(playerAt, words.Count - playerAt) : new List<string>();
        if (playerAt >= 0) words.RemoveRange(playerAt, words.Count - playerAt);
        float started = SessionState.GetFloat(PlayKey + "Started", -1f);
        bool taken = SessionState.GetBool(PlayKey + "Taken", false);
        double now = EditorApplication.timeSinceStartup;

        if (EditorApplication.isPlaying)
        {
            if (started < 0f)
            {
                SessionState.SetFloat(PlayKey + "Started", (float)now);
                return;
            }
            // Timed in the game's own seconds: an editor in the background may run the game slowly.
            float seconds = float.Parse(words[2]);
            float played = Time.timeSinceLevelLoad;
            if (placeAt.HasValue && !SessionState.GetBool(PlayKey + "Placed", false) && played >= seconds - 1f)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    player.transform.position = new Vector3(placeAt.Value.x, placeAt.Value.y, player.transform.position.z);
                    if (player.TryGetComponent(out Rigidbody2D body)) body.position = placeAt.Value;
                    // "walk <dx> <dy>" after the point has them walk that way until the capture.
                    int walkAt = extras.FindIndex(w => w.Equals("walk", StringComparison.OrdinalIgnoreCase));
                    if (walkAt >= 0 && walkAt + 2 < extras.Count && float.TryParse(extras[walkAt + 1], out float wx) && float.TryParse(extras[walkAt + 2], out float wy)
                        && player.TryGetComponent(out PlayerController controller))
                        controller.SetScriptedInput(new Vector2(wx, wy), false);
                }
                SessionState.SetBool(PlayKey + "Placed", true);
            }
            if (taken || played < seconds) return;

            Camera cam = Camera.main;
            if (cam != null)
            {
                if (words.Count > 4 && float.TryParse(words[3], out float x) && float.TryParse(words[4], out float y))
                    cam.transform.position = new Vector3(x, y, cam.transform.position.z);
                if (words.Count > 5 && float.TryParse(words[5], out float size)) cam.orthographicSize = size;
                RenderToFile(cam);
            }
            else
            {
                RecordPlayLog("There was no main camera to capture from.", "", LogType.Error);
            }
            SessionState.SetBool(PlayKey + "Taken", true);
            EditorApplication.ExitPlaymode();
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        // Not playing yet: keep waiting, unless it's plainly never going to start (a compile error stops Play mode).
        if (!taken && started < 0f && now - SessionState.GetFloat(PlayKey + "Requested", 0f) < PlayStartTimeout) return;

        string log = SessionState.GetString(PlayKey + "Log", "");
        string previous = SessionState.GetString(PlayKey + "Previous", "");
        foreach (string key in new[] { "Words", "Previous", "Log" }) SessionState.EraseString(PlayKey + key);
        foreach (string key in new[] { "Requested", "Started" }) SessionState.EraseFloat(PlayKey + key);
        SessionState.EraseBool(PlayKey + "Taken");
        SessionState.EraseBool(PlayKey + "Placed");
        SessionState.EraseBool(PlayKey + "Active");
        Application.logMessageReceived -= RecordPlayLog;

        if (!string.IsNullOrEmpty(previous) && SceneManager.GetActiveScene().path != previous)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Finish(taken, taken ? log : "Play mode never started.\n" + log);
    }

    // Warnings and errors while a play capture runs, with where an exception came from.
    static void RecordPlayLog(string message, string stack, LogType type)
    {
        if (type == LogType.Log) return;
        string where = type == LogType.Exception && !string.IsNullOrEmpty(stack) ? "\n    at " + stack.Split('\n')[0] : "";
        SessionState.SetString(PlayKey + "Log", SessionState.GetString(PlayKey + "Log", "") + $"{type}: {message}{where}\n");
    }

    static void Finish(bool ok, string details)
    {
        File.WriteAllText(ResultPath, (ok ? "OK" : "FAILED") + "\n" + details);
        string output = SessionState.GetString(CommandLineKey, "");
        if (string.IsNullOrEmpty(output)) return;
        SessionState.EraseString(CommandLineKey);
        if (ok && File.Exists(CapturePath)) File.Copy(CapturePath, output, true);
        File.WriteAllText(output + ".txt", (ok ? "OK" : "FAILED") + "\n" + details);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
