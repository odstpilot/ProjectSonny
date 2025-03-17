using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerCanvasUpdate : MonoBehaviour
{
    // Start is called before the first frame update
    int hp;
    TextMeshPro hpDisplay;
    void UpdateHP(float Newhp)
    {
        hpDisplay.text = Newhp.ToString();
    }
}
