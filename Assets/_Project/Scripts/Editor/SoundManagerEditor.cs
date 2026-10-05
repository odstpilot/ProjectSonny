using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The SoundManager inspector: the scene's sounds grouped under Music, Ambience, Sound Effects, UI and Voice, each
// with a colored tag, a play button to hear it, and a warning while it has no clip. Open a sound for its description,
// clips and audio source settings. At the top, a count of what's listed and what's still missing, buttons to add a
// sound to each category, and Copy List, which copies the whole list as text for whoever is finding the audio.
[CustomEditor(typeof(SoundManager))]
public class SoundManagerEditor : Editor
{
    static readonly SoundCategory[] Categories =
        { SoundCategory.Music, SoundCategory.Ambience, SoundCategory.SoundEffect, SoundCategory.UI, SoundCategory.Voice };

    static readonly Color[] CategoryColors =
    {
        new Color(0.55f, 0.4f, 0.9f),     // Music
        new Color(0.3f, 0.65f, 0.85f),    // Ambience
        new Color(0.9f, 0.6f, 0.25f),     // Sound Effect
        new Color(0.4f, 0.75f, 0.45f),    // UI
        new Color(0.9f, 0.4f, 0.55f),     // Voice
    };

    static readonly Color MissingColor = new Color(0.95f, 0.35f, 0.3f);

    // Static, so what's open stays open after selecting something else and coming back.
    static readonly HashSet<SoundCategory> closedCategories = new HashSet<SoundCategory>();
    static readonly HashSet<string> open3D = new HashSet<string>();
    static AudioSource preview;

    private SerializedProperty soundsProperty;
    private GUIStyle tagStyle;
    private int otherManagers;

    void OnEnable()
    {
        soundsProperty = serializedObject.FindProperty("sounds");
        var manager = (SoundManager)target;
        otherManagers = 0;
        foreach (SoundManager other in FindObjectsByType<SoundManager>(FindObjectsInactive.Include))
            if (other != manager && other.gameObject.scene == manager.gameObject.scene) otherManagers++;
    }

    void OnDisable()
    {
        StopPreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        MakeStyles();
        var manager = (SoundManager)target;

        if (otherManagers > 0)
            EditorGUILayout.HelpBox("There's another Sound Manager in this scene. Keep one per scene.", MessageType.Warning);

        DrawSummary(manager);
        DrawAddButtons(manager);
        EditorGUILayout.Space(6);

        foreach (SoundCategory category in Categories) DrawCategory(category);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawSummary(SoundManager manager)
    {
        int missing = 0;
        var names = new HashSet<string>();
        var problems = new List<string>();
        foreach (Sound sound in manager.sounds)
        {
            if (!sound.HasClip) missing++;
            if (string.IsNullOrEmpty(sound.name)) problems.Add("A sound has no name.");
            else if (!names.Add(sound.name)) problems.Add($"\"{sound.name}\" is listed more than once.");
        }

        var counts = new StringBuilder();
        foreach (SoundCategory category in Categories)
        {
            int count = manager.sounds.FindAll(s => s.category == category).Count;
            if (count == 0) continue;
            if (counts.Length > 0) counts.Append("   ");
            counts.Append($"{Label(category)} {count}");
        }

        string summary = manager.sounds.Count == 0
            ? "No sounds yet. Add every sound this scene uses, even ones without audio yet."
            : $"{manager.sounds.Count} sounds:   {counts}";
        if (missing > 0) summary += $"\n{missing} still need audio.";
        EditorGUILayout.HelpBox(summary, missing > 0 ? MessageType.Warning : MessageType.Info);
        foreach (string problem in problems) EditorGUILayout.HelpBox(problem + " Only the first of each name can be played.", MessageType.Error);
    }

    void DrawAddButtons(SoundManager manager)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Add", GUILayout.Width(28));
        foreach (SoundCategory category in Categories)
        {
            if (GUILayout.Button(Label(category), EditorStyles.miniButton)) AddSound(manager, category);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Copy List", "Copies every sound as text, by category, with its description and whether it has audio yet."), EditorStyles.miniButton))
        {
            EditorGUIUtility.systemCopyBuffer = SoundList(manager);
            Debug.Log("SoundManager: sound list copied.");
        }
        if (GUILayout.Button(new GUIContent("Sort", "Orders the list by category, then by name."), EditorStyles.miniButton))
        {
            Undo.RecordObject(manager, "Sort Sounds");
            manager.sounds.Sort((a, b) => a.category != b.category ? a.category.CompareTo(b.category) : string.CompareOrdinal(a.name, b.name));
            EditorUtility.SetDirty(manager);
            serializedObject.Update();
        }
        if (GUILayout.Button(new GUIContent("Add Defaults ▾", "Adds the sounds the game's scripts play, for a kind of scene. Sounds already listed are left alone."), EditorStyles.miniButton))
        {
            var menu = new GenericMenu();
            foreach ((string label, SoundDefaults.Entry[][] sets) in SoundDefaults.Menu)
            {
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    Undo.RecordObject(manager, "Add Default Sounds");
                    int added = SoundDefaults.Fill(manager, sets);
                    Debug.Log($"SoundManager: added {added} default sounds for {label}.", manager);
                });
            }
            menu.ShowAsContext();
        }
        if (GUILayout.Button("Stop Preview", EditorStyles.miniButton)) StopPreview();
        EditorGUILayout.EndHorizontal();
    }

    void DrawCategory(SoundCategory category)
    {
        var indices = new List<int>();
        for (int i = 0; i < soundsProperty.arraySize; i++)
            if (soundsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("category").enumValueIndex == (int)category) indices.Add(i);
        if (indices.Count == 0) return;

        bool open = !closedCategories.Contains(category);
        Rect header = EditorGUILayout.GetControlRect(false, 20);
        EditorGUI.DrawRect(header, CategoryColors[(int)category] * new Color(1f, 1f, 1f, 0.35f));
        bool nowOpen = EditorGUI.Foldout(header, open, $"{Label(category).ToUpperInvariant()}  ({indices.Count})", true, EditorStyles.foldoutHeader);
        if (nowOpen != open)
        {
            if (nowOpen) closedCategories.Remove(category);
            else closedCategories.Add(category);
        }
        if (!nowOpen) return;

        // Removing while drawing would shift the indices, so do it afterwards.
        int remove = -1;
        foreach (int index in indices)
            if (DrawSound(soundsProperty.GetArrayElementAtIndex(index), ((SoundManager)target).sounds[index])) remove = index;
        if (remove >= 0)
        {
            soundsProperty.DeleteArrayElementAtIndex(remove);
            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.Space(4);
    }

    // Returns true when the remove button was clicked.
    bool DrawSound(SerializedProperty property, Sound sound)
    {
        var category = (SoundCategory)property.FindPropertyRelative("category").enumValueIndex;
        bool removed = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        Rect row = EditorGUILayout.GetControlRect(false, 18);
        EditorGUI.DrawRect(new Rect(row.x - 3, row.y - 2, 3, row.height + 4), CategoryColors[(int)category]);

        Rect foldout = new Rect(row.x + 12, row.y, row.width - 12 - 150, row.height);
        property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, sound.name, true);

        Rect status = new Rect(row.xMax - 150, row.y + 1, 72, row.height - 2);
        if (sound.HasClip)
        {
            int count = 0;
            foreach (AudioClip clip in sound.clips) if (clip != null) count++;
            GUI.Label(status, count == 1 ? "1 clip" : $"{count} clips", tagStyle);
        }
        else
        {
            Color old = GUI.color;
            GUI.color = MissingColor;
            GUI.Label(status, "NEEDS AUDIO", tagStyle);
            GUI.color = old;
        }

        using (new EditorGUI.DisabledScope(!sound.HasClip))
        {
            if (GUI.Button(new Rect(row.xMax - 74, row.y, 24, row.height), new GUIContent("▶", "Play it"), EditorStyles.miniButtonLeft)) Preview(sound);
        }
        if (GUI.Button(new Rect(row.xMax - 50, row.y, 24, row.height), new GUIContent("■", "Stop it"), EditorStyles.miniButtonRight)) StopPreview(sound);
        if (GUI.Button(new Rect(row.xMax - 22, row.y, 22, row.height), new GUIContent("✕", "Remove this sound"), EditorStyles.miniButton))
            removed = EditorUtility.DisplayDialog("Remove Sound", $"Remove \"{sound.name}\" from the list?", "Remove", "Cancel");

        if (property.isExpanded) DrawSettings(property);
        EditorGUILayout.EndVertical();
        return removed;
    }

    void DrawSettings(SerializedProperty property)
    {
        EditorGUI.indentLevel++;
        Field(property, "name");
        Field(property, "category");
        Field(property, "description");
        Field(property, "credit");
        Field(property, "clips");
        Field(property, "playFrom");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Audio Source", EditorStyles.boldLabel);
        Field(property, "output");
        Field(property, "mute");
        Field(property, "bypassEffects");
        Field(property, "bypassListenerEffects");
        Field(property, "bypassReverbZones");
        Field(property, "playOnAwake");
        Field(property, "loop");
        Field(property, "priority");
        Field(property, "volume");
        Field(property, "pitch");
        Field(property, "stereoPan");
        Field(property, "spatialBlend");
        Field(property, "reverbZoneMix");

        string key = property.propertyPath;
        bool open = EditorGUILayout.Foldout(open3D.Contains(key), "3D Sound Settings", true);
        if (open) open3D.Add(key);
        else open3D.Remove(key);
        if (open)
        {
            EditorGUI.indentLevel++;
            if (property.FindPropertyRelative("spatialBlend").floatValue <= 0f)
                EditorGUILayout.HelpBox("Spatial Blend is 0 (2D), so these don't change anything yet.", MessageType.None);
            Field(property, "dopplerLevel");
            Field(property, "spread");
            Field(property, "volumeRolloff");
            Field(property, "minDistance");
            Field(property, "maxDistance");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Variation", EditorStyles.boldLabel);
        Field(property, "volumeVariation");
        Field(property, "pitchVariation");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Repeat", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(property.FindPropertyRelative("loop").boolValue))
        {
            Field(property, "repeat");
            if (property.FindPropertyRelative("repeat").boolValue) Field(property, "repeatWait");
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2);
    }

    static void Field(SerializedProperty property, string name)
    {
        EditorGUILayout.PropertyField(property.FindPropertyRelative(name), true);
    }

    void AddSound(SoundManager manager, SoundCategory category)
    {
        Undo.RecordObject(manager, "Add Sound");
        var sound = new Sound { category = category, name = UniqueName(manager, category == SoundCategory.SoundEffect ? "New Sound Effect" : "New " + category) };
        // Music and ambience usually loop and run from the start; UI sounds shouldn't be cut off by other sounds.
        if (category == SoundCategory.Music || category == SoundCategory.Ambience) sound.loop = true;
        if (category == SoundCategory.Music) sound.priority = 0;
        if (category == SoundCategory.UI) sound.priority = 32;
        manager.sounds.Add(sound);
        EditorUtility.SetDirty(manager);
        serializedObject.Update();
        soundsProperty.GetArrayElementAtIndex(soundsProperty.arraySize - 1).isExpanded = true;
        closedCategories.Remove(category);
    }

    static string UniqueName(SoundManager manager, string name)
    {
        string unique = name;
        for (int i = 2; manager.sounds.Exists(s => s.name == unique); i++) unique = $"{name} {i}";
        return unique;
    }

    // The list for whoever finds the audio: every sound by category, with what it's for and whether it has audio yet.
    static string SoundList(SoundManager manager)
    {
        var text = new StringBuilder();
        string scene = manager.gameObject.scene.IsValid() ? manager.gameObject.scene.name : EditorSceneManager.GetActiveScene().name;
        text.AppendLine($"Sounds for {scene}");
        foreach (SoundCategory category in Categories)
        {
            List<Sound> sounds = manager.sounds.FindAll(s => s.category == category);
            if (sounds.Count == 0) continue;
            text.AppendLine();
            text.AppendLine($"{Label(category).ToUpperInvariant()} ({sounds.Count})");
            foreach (Sound sound in sounds)
            {
                var line = new StringBuilder($"[{(sound.HasClip ? "x" : " ")}] {sound.name}");
                if (sound.loop) line.Append(" (loops)");
                else if (sound.repeat) line.Append($" (repeats every {sound.repeatWait.x:0.#}-{sound.repeatWait.y:0.#}s)");
                if (!string.IsNullOrWhiteSpace(sound.description)) line.Append(" - " + sound.description.Replace("\n", " ").Trim());
                if (!string.IsNullOrWhiteSpace(sound.credit)) line.Append($"  [credit: {sound.credit.Trim()}]");
                text.AppendLine(line.ToString());
            }
        }
        return text.ToString();
    }

    void Preview(Sound sound)
    {
        // In play mode, play it through the manager, so it's heard exactly as the game plays it.
        if (Application.isPlaying)
        {
            ((SoundManager)target).Play(sound.name);
            return;
        }

        StopPreview();
        if (preview == null)
        {
            preview = EditorUtility.CreateGameObjectWithHideFlags("[Sound Preview]", HideFlags.HideAndDontSave, typeof(AudioSource)).GetComponent<AudioSource>();
        }
        sound.ApplyTo(preview);
        preview.spatialBlend = 0f;      // there's no listener to be near outside play mode
        preview.loop = false;
        preview.pitch = sound.pitch + Random.Range(-sound.pitchVariation, sound.pitchVariation);
        preview.volume = sound.volume * (1f - Random.Range(0f, sound.volumeVariation));
        preview.clip = sound.RandomClip();
        preview.Play();
    }

    void StopPreview(Sound sound)
    {
        if (Application.isPlaying) ((SoundManager)target).Stop(sound.name);
        else StopPreview();
    }

    static void StopPreview()
    {
        if (preview != null) preview.Stop();
    }

    static string Label(SoundCategory category)
    {
        return category == SoundCategory.SoundEffect ? "Sound Effects" : category.ToString();
    }

    void MakeStyles()
    {
        if (tagStyle != null) return;
        tagStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
    }

    [MenuItem("GameObject/Audio/Sound Manager", false, 10)]
    static void Create(MenuCommand command)
    {
        var existing = FindAnyObjectByType<SoundManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("SoundManager: this scene already has one, so it's selected instead.", existing);
            return;
        }

        var manager = new GameObject("Sound Manager", typeof(SoundManager));
        GameObjectUtility.SetParentAndAlign(manager, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(manager, "Create Sound Manager");
        Selection.activeObject = manager;
    }
}
