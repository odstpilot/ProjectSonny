using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// The autosave. Every time an objective is finished (the objective in the corner moves on from one, TutorialHud), the
// game saves a checkpoint: which scene the player's in, which objective they just finished, which one is next, and where
// they're standing. It's written as JSON to save.json in Application.persistentDataPath, and the save icon comes up in
// the bottom right corner to say so (SaveIcon).
//
// Continuing (CONTINUE on the title screen, TitleMenu): Continue() reads the checkpoint back, marks it as the one being
// resumed (Resuming), and says which scene to load. Once it's loaded, that scene's director sees Resuming, sets the
// scene up the way it was just after the finished objective (ChapterOneDirector, ChapterTwoDirector; the tutorial,
// which has just the one objective, goes straight on to Chapter 1), puts the player back where they were (PlacePlayer),
// and calls DoneResuming. Anything that would otherwise start on its own waits while ResumePending.
// NEW GAME clears the save (NewGame).
// Testers can jump straight to any stage of a chapter (CheckpointJump): that's a checkpoint with only a stage named
// (JumpTo), which the scene's director fills in for itself, putting the player where that stage happens.
public static class SaveGame
{
    [Serializable]
    public class Checkpoint
    {
        public string scene;
        public int number;          // how many checkpoints into the scene this is, from 1
        public string finishedObjective;
        public string nextObjective;
        public bool hasPosition;
        public float x, y;          // where the player was standing, in the world
        public string savedAt;      // local time, ISO 8601
        public string stage;        // a tester's jump (CheckpointJump): the director fills in the rest
        public string inventory;    // scrap and what's been made from it (Inventory)
    }

    const string FileName = "save.json";

    // Off, nothing is saved (a test scene, say).
    public static bool Enabled = true;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(FilePath);

    // The last checkpoint saved or loaded this session.
    public static Checkpoint Last { get; private set; }

    // The checkpoint being continued from, until the scene it's in has set itself up from it.
    public static Checkpoint Resuming { get; private set; }

    // True while a checkpoint is waiting to be set up in the scene that's open: its director hasn't got to it yet.
    public static bool ResumePending => Resuming != null && Resuming.scene == SceneManager.GetActiveScene().name;

    // Every time it saves.
    public static event Action<Checkpoint> Saved;

    // An objective's done: save, and show the icon. next is the objective after it, if there is one yet.
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

    // The last checkpoint on disk, or null if there isn't one (or it can't be read).
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

    // Picks up from the last checkpoint: returns the scene to load (null if there's nothing to continue), and marks the
    // checkpoint for it to set itself up from once it's loaded.
    public static string Continue()
    {
        Checkpoint checkpoint = Load();
        if (checkpoint == null) return null;
        Resuming = checkpoint;
        SuitFeatures.Reset();
        return checkpoint.scene;
    }

    // For a tester: the scene, once it's loaded, is to set itself up at this stage (CheckpointJump). The save on disk is
    // left alone until the next objective's finished.
    public static void JumpTo(string scene, string stage)
    {
        Resuming = new Checkpoint { scene = scene, stage = stage };
        SuitFeatures.Reset();
    }

    // The scene's set itself up from the checkpoint; the game carries on from there, saving as it goes.
    public static void DoneResuming() => Resuming = null;

    // The player back where they were when it saved, if it knows.
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

    // Starts over: the save file gone, and nothing unlocked in the suit.
    public static void NewGame()
    {
        Last = null;
        Resuming = null;
        SuitFeatures.Reset();
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

    // Entering Play mode without reloading scripts keeps statics; nothing's being continued at the start of a run.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Resuming = null;
        Last = null;
    }
}
