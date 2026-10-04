using UnityEngine;
using UnityEngine.SceneManagement;

// For testers: go straight to any stage of a chapter, with everything before it done and its cutscenes skipped, and the
// player standing where that stage happens. Each stage is a checkpoint (SaveGame.JumpTo) that the chapter's director
// fills in for itself (ChapterOneDirector.FillStage, ChapterTwoDirector.FillStage), then sets up the way it does when
// continuing a save. "start" plays the scene from the top.
//
// Two ways in:
//  - in the editor, Sonny > Checkpoint Tester (CheckpointTesterWindow): a button per stage, which opens the scene and
//    presses Play, or jumps there if it's already playing
//  - while playing, in the editor or a development build, F9 opens a list of the stages over the game
// Keep Stages in step with the directors' FillStage.
public static class CheckpointJump
{
    public const KeyCode PanelKey = KeyCode.F9;

    public class Stage
    {
        public readonly string scene, id, label;
        public Stage(string scene, string id, string label)
        {
            this.scene = scene;
            this.id = id;
            this.label = label;
        }
    }

    public const string Start = "start";

    public static readonly Stage[] Stages =
    {
        new Stage("Tutorial", Start, "From the top"),

        new Stage("Chapter1", Start, "From the top (title card)"),
        new Stage("Chapter1", "greeted", "Greeted: Pip boots, control room pinned"),
        new Stage("Chapter1", "to-control-room", "On the way to the control room"),
        new Stage("Chapter1", "install", "In the control room: the install (skippable after)"),
        new Stage("Chapter1", "breather", "After the install: sent for the spare coupling"),
        new Stage("Chapter1", "storage", "In the storage room: find the spare coupling"),
        new Stage("Chapter1", "crate", "Searching the tool crate"),
        new Stage("Chapter1", "shelf", "Under the shelf: the quake, the end of the chapter"),

        new Stage("Chapter2", Start, "From the top (waking up)"),
        new Stage("Chapter2", "vent", "Door jammed: into the vents (rat, drone)"),
        new Stage("Chapter2", "out", "Out in the restroom: find the crew"),
        new Stage("Chapter2", "patrols", "Crew found, bots roaming: find the escape pods"),
        new Stage("Chapter2", "exit-sealed", "Exit sealed, no pods: get to the control room"),
        new Stage("Chapter2", "hallway-caved", "Hallway caved in: upstairs to the maintenance deck"),
        new Stage("Chapter2", "wall-grab", "Just short of the stairs: the bot in the wall (mash E)"),
        new Stage("Chapter2", "upper-deck", "Upper maintenance deck: scavenge for the EMP"),
        new Stage("Chapter2", "bench-stocked", "At the workbench with parts for everything (EMP, crowbar, scrap gun)"),
        new Stage("Chapter2", "emp-built", "EMP built: find the control room"),
        new Stage("Chapter2", "control-door", "At the reactor core's badge door: locked out, sent to the comms ring"),
        new Stage("Chapter2", "comms-ring", "Comms ring: arriving (the lights die, Pip goes offline)"),
        new Stage("Chapter2", "comms-generator", "Comms ring, pitch dark: at the generator (its minigame)"),
        new Stage("Chapter2", "comms-power", "Comms ring, power back: tune in the broadcasts (bots through the walls)"),
        new Stage("Chapter2", "comms-relay", "Comms ring: every broadcast in, answer Sonny's relay"),
        new Stage("Chapter2", "comms-done", "Relay passed: a unit's access, back to the control room"),
        new Stage("Chapter2", "control-room", "In the control room, let in as a unit"),
    };

    // Loads the scene set up at that stage.
    public static void Go(string scene, string stage)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"CheckpointJump: there's no scene called \"{scene}\" in the build settings.");
            return;
        }
        Time.timeScale = 1f;
        if (stage == Start) SaveGame.DoneResuming();
        else SaveGame.JumpTo(scene, stage);
        SceneManager.LoadScene(scene);
    }

#if UNITY_EDITOR
    public const string EditorRequestKey = "Sonny.CheckpointJump";

    // The Checkpoint Tester window asks for a stage before pressing Play: picked up here, before the scene wakes.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void FromEditor()
    {
        string request = UnityEditor.SessionState.GetString(EditorRequestKey, "");
        UnityEditor.SessionState.EraseString(EditorRequestKey);
        string[] parts = request.Split('|');
        if (parts.Length != 2 || parts[1] == Start) return;
        SaveGame.JumpTo(parts[0], parts[1]);
    }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MakePanel()
    {
        var panel = new GameObject("Checkpoint Jump (F9)").AddComponent<CheckpointJumpPanel>();
        Object.DontDestroyOnLoad(panel.gameObject);
    }
#endif
}

// The F9 list of stages, drawn plainly over the game: it's a tester's tool, not part of it. Pauses the game while it's
// open.
public class CheckpointJumpPanel : MonoBehaviour
{
    private bool open;
    private float timeScaleWas = 1f;
    private Vector2 scroll;

    void Update()
    {
        if (!Input.GetKeyDown(CheckpointJump.PanelKey)) return;
        open = !open;
        if (open)
        {
            timeScaleWas = Time.timeScale;
            Time.timeScale = 0f;
        }
        else Time.timeScale = timeScaleWas;
    }

    void OnGUI()
    {
        if (!open) return;
        float scale = Screen.height / 900f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        var area = new Rect(20f, 20f, 520f, 860f);
        GUI.Box(area, "");
        GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 10f, area.width - 20f, area.height - 20f));
        GUILayout.Label($"<b>Jump to a stage</b>   ({CheckpointJump.PanelKey} closes)", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
        scroll = GUILayout.BeginScrollView(scroll);
        string scene = null;
        foreach (CheckpointJump.Stage stage in CheckpointJump.Stages)
        {
            if (stage.scene != scene)
            {
                scene = stage.scene;
                GUILayout.Space(8f);
                GUILayout.Label($"<b>{scene}</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
            }
            if (GUILayout.Button(stage.label, GUILayout.Height(28f)))
            {
                open = false;
                CheckpointJump.Go(stage.scene, stage.id);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
