using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// autosave and continue
public static class SaveGame
{
    [Serializable]
    public class Checkpoint
    {
        public string scene;
        public int number;
        public string finishedObjective;
        public string nextObjective;
        public bool hasPosition;
        public float x, y;
        public string savedAt;
        public string stage;
        public string inventory;
    }

    const string FileName = "save.json";

    public static bool Enabled = true;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(FilePath);

    public static Checkpoint Last { get; private set; }

    public static Checkpoint Resuming { get; private set; }

    public static bool ResumePending => Resuming != null && Resuming.scene == SceneManager.GetActiveScene().name;

    public static event Action<Checkpoint> Saved;

    public static void ObjectiveFinished(string finished, string next)
    {
        if (!Enabled || string.IsNullOrEmpty(finished) || ResumePending) return;
        string scene = SceneManager.GetActiveScene().name;
        GameObject player = GameObject.FindWithTag("Player");
        var checkpoint = new Checkpoint
        {
            scene = scene,
            number = Last != null && Last.scene == scene ? Last.number + 1 : 1,
            finishedObjective = finished,
            nextObjective = next ?? "",
            hasPosition = player != null,
            x = player != null ? player.transform.position.x : 0f,
            y = player != null ? player.transform.position.y : 0f,
            savedAt = DateTime.Now.ToString("s"),
            inventory = Inventory.Save(),
        };

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(checkpoint, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveGame: couldn't write {FilePath}: {e.Message}");
            return;
        }
        Last = checkpoint;
        SaveIcon.Show();
        Saved?.Invoke(checkpoint);
    }

    public static void ChapterStarted(string next) => ObjectiveFinished(SceneManager.GetActiveScene().name, next);

    public static Checkpoint Load()
    {
        if (!HasSave) return null;
        try
        {
            var checkpoint = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(FilePath));
            if (checkpoint == null || string.IsNullOrEmpty(checkpoint.scene)) return null;
            Last = checkpoint;
            return checkpoint;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveGame: couldn't read {FilePath}: {e.Message}");
            return null;
        }
    }

    public static string Continue()
    {
        Checkpoint checkpoint = Load();
        if (checkpoint == null) return null;
        Resuming = checkpoint;
        SuitFeatures.Reset();
        SuitHelper.ResetWear();
        return checkpoint.scene;
    }

    public static void JumpTo(string scene, string stage)
    {
        Resuming = new Checkpoint { scene = scene, stage = stage };
        SuitFeatures.Reset();
        SuitHelper.ResetWear();
    }

    public static void DoneResuming() => Resuming = null;

    public static void PlacePlayer(Checkpoint checkpoint)
    {
        if (checkpoint == null || !checkpoint.hasPosition) return;
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;
        var at = new Vector2(checkpoint.x, checkpoint.y);
        player.transform.position = new Vector3(at.x, at.y, player.transform.position.z);
        if (player.TryGetComponent(out Rigidbody2D body))
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
        }
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out CameraFallow follow)) follow.SnapToPlayer();
    }

    public static void NewGame()
    {
        Last = null;
        Resuming = null;
        SuitFeatures.Reset();
        SuitHelper.ResetWear();
        Inventory.Reset();
        try
        {
            if (HasSave) File.Delete(FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveGame: couldn't delete {FilePath}: {e.Message}");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Resuming = null;
        Last = null;
    }
}
