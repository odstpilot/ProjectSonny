using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Sonny > Build Station Blueprint (Test)
//
// Builds Scenes/Levels/StationBlueprint.unity: a test scene with the whole station laid out exactly as the blueprint
// draws it (Floor1Layout.txt, and Floor2Layout.txt upstairs), for walking about and checking the walls, the furniture,
// the doorways, the stairs, and the lighting without any of the story getting in the way. Every room is its drawn size,
// in its drawn place, right next to its neighbours, and the camera isn't held inside any one room, so the rooms around
// show too (FloorOneBuilder, built as drawn). It's the same tiles, furniture, lamps, doors, and stairs the chapters are
// built from, so what collides here collides there, only the rooms there are bigger and further apart.
// Nobody else is aboard, nothing is scripted, and the player has the prefab's own kit. It isn't added to the build.
// Edit the layouts and run this again. It replaces everything in the scene but the Sound Manager.
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

    // For batch mode: Unity -batchmode -projectPath <project> -executeMethod StationBlueprintBuilder.BuildFromCommandLine
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
