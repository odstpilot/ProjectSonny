using UnityEditor;
using UnityEngine;

// builds chapter 3 from the same station as chapter 2
public static class ChapterThreeBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Levels/Chapter3.unity";
    const string ChapterTwoPath = "Assets/_Project/Scenes/Levels/Chapter2.unity";

    [MenuItem("Sonny/Build Chapter 3")]
    public static void BuildFromMenu()
    {
        bool rebuild = EditorUtility.DisplayDialog("Build Chapter 3",
            "Rebuild Chapter3.unity from Floor1Layout.txt and Floor2Layout.txt?\n\nThis replaces everything in the scene except the Sound Manager. Anything else added to it by hand will be lost.",
            "Rebuild", "Cancel");
        if (rebuild && UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            Build();
    }

    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build() ? 0 : 1);
    }

    public static bool Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset(ChapterTwoPath, ScenePath))
        {
            Debug.LogError($"Chapter 3 builder: couldn't make {ScenePath} from {ChapterTwoPath}.");
            return false;
        }
        if (!ChapterTwoBuilder.BuildStation(ScenePath, true)) return false;
        Debug.Log($"Chapter 3 builder: saved {ScenePath}.");
        return true;
    }
}
