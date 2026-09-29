using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Draws a [SoundName] string as the name typed in, with a button listing the sounds on the scene's SoundManager to
// pick from instead. The name turns red when the scene doesn't list it (it still works if a later scene does).
[CustomPropertyDrawer(typeof(SoundNameAttribute))]
public class SoundNameDrawer : PropertyDrawer
{
    static readonly Color MissingColor = new Color(1f, 0.45f, 0.4f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        SoundManager manager = ManagerFor(property);
        List<string> names = new List<string>();
        if (manager != null) foreach (Sound sound in manager.sounds) names.Add(sound.name);

        label = EditorGUI.BeginProperty(position, label, property);
        Rect field = new Rect(position.x, position.y, position.width - 22, position.height);
        Rect button = new Rect(position.xMax - 20, position.y, 20, position.height);

        Color old = GUI.color;
        if (manager != null && !string.IsNullOrEmpty(property.stringValue) && !names.Contains(property.stringValue)) GUI.color = MissingColor;
        property.stringValue = EditorGUI.TextField(field, label, property.stringValue);
        GUI.color = old;

        using (new EditorGUI.DisabledScope(names.Count == 0))
        {
            if (GUI.Button(button, new GUIContent("▾", "Pick one of this scene's sounds"), EditorStyles.miniButton))
            {
                var menu = new GenericMenu();
                SerializedProperty target = property.Copy();
                foreach (string name in names)
                {
                    string picked = name;
                    menu.AddItem(new GUIContent(picked), picked == property.stringValue, () =>
                    {
                        target.stringValue = picked;
                        target.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.DropDown(button);
            }
        }
        EditorGUI.EndProperty();
    }

    // The SoundManager in the same scene as the object being edited, or any open one (for prefabs).
    static SoundManager ManagerFor(SerializedProperty property)
    {
        if (property.serializedObject.targetObject is Component component && component.gameObject.scene.IsValid())
        {
            foreach (GameObject root in component.gameObject.scene.GetRootGameObjects())
            {
                var manager = root.GetComponentInChildren<SoundManager>(true);
                if (manager != null) return manager;
            }
        }
        return Object.FindAnyObjectByType<SoundManager>();
    }
}
