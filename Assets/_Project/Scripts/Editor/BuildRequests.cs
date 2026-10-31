using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// runs builders and captures from outside the editor
[InitializeOnLoad]
static class BuildRequests
{
    const string RequestPath = "Temp/SonnyBuild.request";
    const string ResultPath = "Temp/SonnyBuild.result";

    const string CapturePath = "Temp/SonnyCapture.png";
    static readonly Vector2Int CaptureSize = new Vector2Int(1600, 900);

    static readonly Dictionary<string, string> Scenes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Tutorial", "Assets/_Project/Scenes/Levels/Tutorial.unity" },
        { "Chapter1", "Assets/_Project/Scenes/Levels/Chapter1.unity" },
        { "Chapter2", "Assets/_Project/Scenes/Levels/Chapter2.unity" },
        { "Chapter3", "Assets/_Project/Scenes/Levels/Chapter3.unity" },
        { "Blueprint", "Assets/_Project/Scenes/Levels/StationBlueprint.unity" },
    };

    static readonly Dictionary<string, Func<bool>> Builders = new Dictionary<string, Func<bool>>(StringComparer.OrdinalIgnoreCase)
    {
        { "Tutorial", TutorialLevelBuilder.Build },
        { "Chapter1", ChapterOneBuilder.Build },
        { "Chapter2", ChapterTwoBuilder.Build },
        { "Chapter3", ChapterThreeBuilder.Build },
        { "Blueprint", StationBlueprintBuilder.Build },
    };

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

        if (!string.IsNullOrEmpty(previous) && (capture || SceneManager.GetActiveScene().path != previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        else if (capture)
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Finish(ok, log.ToString());
    }

    public static void CaptureFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-sonnyCapture");
        bool ok = at >= 0 && at + 1 < args.Length && Capture(("Capture " + args[at + 1]).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
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
        RenderToFile(cam, words.Any(w => w.Equals("colliders", StringComparison.OrdinalIgnoreCase)));

        return true;
    }

    static void RenderToFile(Camera cam, bool showColliders = false)
    {
        List<GameObject> outlines = showColliders ? DrawColliders() : new List<GameObject>();
        var target = new RenderTexture(CaptureSize.x, CaptureSize.y, 24);
        cam.targetTexture = target;
        var overlays = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (Canvas canvas in overlays)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.1f;
            canvas.sortingLayerName = "UI";
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
        foreach (GameObject outline in outlines) UnityEngine.Object.DestroyImmediate(outline);
        RenderTexture.active = target;
        var image = new Texture2D(CaptureSize.x, CaptureSize.y, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, CaptureSize.x, CaptureSize.y), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(CapturePath, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
    }

    static List<GameObject> DrawColliders()
    {
        var made = new List<GameObject>();
        var material = new Material(Shader.Find("Sprites/Default"));
        GameObject player = GameObject.FindWithTag("Player");
        void Outline(Collider2D collider, Vector3[] corners)
        {
            var line = new GameObject("Collider Outline").AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.045f;
            Color color = player != null && collider.transform.IsChildOf(player.transform) ? Color.yellow : new Color(0.3f, 1f, 0.3f);
            line.startColor = line.endColor = color;
            line.sortingLayerName = "UI";
            line.sortingOrder = 50;
            line.positionCount = corners.Length;
            line.SetPositions(corners);
            made.Add(line.gameObject);
        }
        foreach (BoxCollider2D box in UnityEngine.Object.FindObjectsByType<BoxCollider2D>())
        {
            if (!box.enabled || box.isTrigger) continue;
            Vector2 half = box.size * 0.5f;
            Outline(box, new[] { new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y), new Vector2(half.x, half.y), new Vector2(half.x, -half.y) }
                .Select(c => box.transform.TransformPoint(box.offset + c)).Select(v => new Vector3(v.x, v.y, 0f)).ToArray());
        }
        foreach (CircleCollider2D circle in UnityEngine.Object.FindObjectsByType<CircleCollider2D>())
        {
            if (!circle.enabled || circle.isTrigger) continue;
            Vector3 center = circle.transform.TransformPoint(circle.offset);
            float radius = circle.radius * Mathf.Abs(circle.transform.lossyScale.x);
            Outline(circle, Enumerable.Range(0, 16).Select(i => new Vector3(center.x + Mathf.Cos(i * Mathf.PI / 8f) * radius, center.y + Mathf.Sin(i * Mathf.PI / 8f) * radius, 0f)).ToArray());
        }
        return made;
    }

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
        int jumpAt = Array.FindIndex(words, w => w.Equals("jump", StringComparison.OrdinalIgnoreCase));
        if (jumpAt >= 1 && jumpAt + 1 < words.Length)
        {
            SessionState.SetString(CheckpointJump.EditorRequestKey, $"{words[1]}|{words[jumpAt + 1]}");
            words = words.Where((w, i) => i != jumpAt && i != jumpAt + 1).ToArray();
        }
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
        SessionState.SetBool(PlayKey + "Called", false);
        Application.logMessageReceived += RecordPlayLog;

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

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
            float seconds = float.Parse(words[2]);
            float played = Time.timeSinceLevelLoad;
            int forAt = extras.FindIndex(w => w.Equals("for", StringComparison.OrdinalIgnoreCase));
            float lead = forAt >= 0 && forAt + 1 < extras.Count && float.TryParse(extras[forAt + 1], out float given) ? given : 1f;
            int callAt = extras.FindIndex(w => w.Equals("call", StringComparison.OrdinalIgnoreCase));
            if (callAt >= 0 && callAt + 1 < extras.Count && !SessionState.GetBool(PlayKey + "Called", false) && played >= seconds - lead)
            {
                SessionState.SetBool(PlayKey + "Called", true);
                string[] parts = extras[callAt + 1].Split('.');
                Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(parts[0])).FirstOrDefault(t => t != null);
                UnityEngine.Object target = type != null ? UnityEngine.Object.FindAnyObjectByType(type) : null;
                var method = type?.GetMethod(parts.Length > 1 ? parts[1] : "", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (target != null && method != null) method.Invoke(target, null);
                else RecordPlayLog($"Couldn't call {extras[callAt + 1]}: no such object or method.", "", LogType.Warning);
            }
            if (placeAt.HasValue && !SessionState.GetBool(PlayKey + "Placed", false) && played >= seconds - lead)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    player.transform.position = new Vector3(placeAt.Value.x, placeAt.Value.y, player.transform.position.z);
                    if (player.TryGetComponent(out Rigidbody2D body)) body.position = placeAt.Value;
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
                RenderToFile(cam, words.Concat(extras).Any(w => w.Equals("colliders", StringComparison.OrdinalIgnoreCase)));
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
        if (!taken && started < 0f && now - SessionState.GetFloat(PlayKey + "Requested", 0f) < PlayStartTimeout) return;

        string log = SessionState.GetString(PlayKey + "Log", "");
        string previous = SessionState.GetString(PlayKey + "Previous", "");
        foreach (string key in new[] { "Words", "Previous", "Log" }) SessionState.EraseString(PlayKey + key);
        foreach (string key in new[] { "Requested", "Started" }) SessionState.EraseFloat(PlayKey + key);
        SessionState.EraseBool(PlayKey + "Taken");
        SessionState.EraseBool(PlayKey + "Placed");
        SessionState.EraseBool(PlayKey + "Called");
        SessionState.EraseBool(PlayKey + "Active");
        Application.logMessageReceived -= RecordPlayLog;

        if (!string.IsNullOrEmpty(previous) && SceneManager.GetActiveScene().path != previous)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Finish(taken, taken ? log : "Play mode never started.\n" + log);
    }

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
