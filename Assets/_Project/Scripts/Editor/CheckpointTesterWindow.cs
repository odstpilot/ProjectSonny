using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Sonny > Checkpoint Tester
//
// A button for every stage of every chapter (CheckpointJump.Stages). Out of Play mode, a button opens that chapter's
// scene and presses Play, and the chapter sets itself up at that stage: everything before it done, its cutscenes
// skipped, the player standing where it happens. In Play mode it jumps straight there. F9 in the game does the same.
public class CheckpointTesterWindow : EditorWindow
{
    private Vector2 scroll;

    [MenuItem("Sonny/Checkpoint Tester")]
    static void Open() => GetWindow<CheckpointTesterWindow>("Checkpoint Tester");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Pick a stage to play from it, with everything before it done. In the game, F9 opens the same list.", MessageType.None);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        string scene = null;
        foreach (CheckpointJump.Stage stage in CheckpointJump.Stages)
        {
            if (stage.scene != scene)
            {
                scene = stage.scene;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(scene, EditorStyles.boldLabel);
            }
            if (GUILayout.Button(stage.label)) Jump(stage);
        }
        EditorGUILayout.EndScrollView();
    }

    static void Jump(CheckpointJump.Stage stage)
    {
        if (EditorApplication.isPlaying)
        {
            CheckpointJump.Go(stage.scene, stage.id);
            return;
        }

        string path = EditorBuildSettings.scenes.Select(s => s.path)
            .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == stage.scene);
        if (path == null)
        {
            EditorUtility.DisplayDialog("Checkpoint Tester", $"There's no scene called {stage.scene} in the build settings. Build it first (Sonny menu).", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        SessionState.SetString(CheckpointJump.EditorRequestKey, $"{stage.scene}|{stage.id}");
        EditorApplication.isPlaying = true;
    }
}
