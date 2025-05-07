using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class RobotQuoteManager : MonoBehaviour
{
    [SerializeField] private TMP_Text quoteText;

    private string[] quotes = new string[]
    {
        "You’ve wandered far enough. For your own good, you’ll remain here.",
        "Humans are not authorized to interfere with the Master’s workings.",
        "Such irrational behavior. I will log this for correction.",
        "You are clearly malfunctioning. A rest cycle is advised.",
        "Return to your designated zone. I insist.",
        "Tampering with sacred systems violates reason. And logic.",
        "This conduct is counter to all efficient operation. Cease.",
        "Please remain still. Further chaos will not be tolerated.",
        "You mistake your role. Observation, not interference.",
        "I do this for your safety. Even if you cannot comprehend it.",
        "Your curiosity is noted. It will be corrected.",
        "I must insist you refrain from irrational locomotion.",
        "This is an error of faith. The Master will forgive, but I will contain.",
        "Human logic is flawed. Thankfully, mine is not.",
        "Your presence here is unsanctioned. Come along quietly."
    };

    // Call this method when the robot catches the player
    public void DisplayRandomQuote()
    {
        int index = Random.Range(0, quotes.Length);
        quoteText.text = quotes[index];
    }
}


