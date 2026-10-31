using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// builds the blueprint test scene
public static class StationBlueprintBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/StationBlueprint.unity";

    [MenuItem("Sonny/Build Station Blueprint (Test)")]
    public static void BuildFromMenu()
    {
        bool rebuild = EditorUtility.DisplayDialog("Build Station Blueprint",
            "Rebuild StationBlueprint.unity from Floor1Layout.txt and Floor2Layout.txt, as drawn, for testing?\n\nThis replaces everything in the scene except the Sound Manager.",
            "Rebuild", "Cancel");
        if (rebuild && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            Build();
    }

    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build() ? 0 : 1);
    }

    public static bool Build()
    {
        bool built = FloorOneBuilder.Build(ScenePath, null, drawn: true);
        if (built) Debug.Log($"Station blueprint builder: saved {ScenePath}.");
        return built;
    }
}
