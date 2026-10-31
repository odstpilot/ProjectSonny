using System.IO;
using UnityEditor;
using UnityEngine;

// opens the pip manager, makes it if missing
public static class PipManagerMenu
{
    const string PrefabPath = "Assets/_Project/Prefabs/Systems/Resources/PipManager.prefab";

    [MenuItem("Sonny/Pip Manager")]
    public static void Open()
    {
        PipManager manager = Prefab();
        if (manager == null) return;
        Selection.activeObject = manager.gameObject;
        EditorGUIUtility.PingObject(manager.gameObject);
    }

    public static PipManager Prefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PipManager>(PrefabPath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var holder = new GameObject("PipManager");
        holder.AddComponent<PipManager>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(holder, PrefabPath);
        Object.DestroyImmediate(holder);
        if (prefab == null)
        {
            Debug.LogError($"Pip Manager: couldn't save {PrefabPath}.");
            return null;
        }
        Debug.Log($"Pip Manager: made {PrefabPath}, with the lines Pip starts with.");
        return prefab.GetComponent<PipManager>();
    }
}
