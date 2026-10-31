using UnityEngine;

// one broadcast to intercept
[CreateAssetMenu(menuName = "Sonny/Transmission", fileName = "Transmission")]
public class TransmissionData : ScriptableObject
{
    [System.Serializable]
    public struct Line
    {
        [Tooltip("Who's speaking, like SONNY or UNIT 12.")]
        public string speaker;
        [TextArea(2, 4)] public string text;
    }

    public string title = "INTERCEPTED BROADCAST";
    public Line[] lines = new Line[0];
    [Tooltip("Plays while the lines type out. Optional.")]
    public AudioClip recording;

    [Header("Credential")]
    [Tooltip("Recovered by tuning this in, for doors that ask for it. Leave empty for none.")]
    public string credential;
    [Tooltip("What the player is told they found.")]
    public string credentialName;
}
