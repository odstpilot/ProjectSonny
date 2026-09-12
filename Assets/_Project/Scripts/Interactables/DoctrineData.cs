using UnityEngine;

// What a DoctrineTerminal asks: Sonny's questions, the answer its units give, and the answers a human would give.
// Answers are shuffled every time, so there's no index to set. Make one with Create > Sonny > Doctrine.
[CreateAssetMenu(menuName = "Sonny/Doctrine", fileName = "Doctrine")]
public class DoctrineData : ScriptableObject
{
    [System.Serializable]
    public struct Question
    {
        [TextArea(1, 3)] public string asks;
        [Tooltip("What one of Sonny's units would say.")]
        public string rightAnswer;
        [Tooltip("What a human might say instead. Two or three of these, about as long as the right answer.")]
        public string[] wrongAnswers;
    }

    [TextArea(1, 3)] public string greeting = "A UNIT HAS COME TO THE RELAY. ANSWER AS A UNIT ANSWERS.";
    public Question[] questions = new Question[0];
    [TextArea(1, 3)] public string rejectedLine = "THAT IS A HUMAN ANSWER.";
    [TextArea(1, 3)] public string detectedLine = "THERE YOU ARE.";
    [TextArea(1, 3)] public string verifiedLine = "VERIFIED. THE CORE KEEPS YOU, UNIT.";

    [Header("Credential")]
    [Tooltip("Handed over when the player passes, for doors that ask for it. Leave empty for none.")]
    public string credential;
    [Tooltip("What the player is told they got.")]
    public string credentialName;
}
