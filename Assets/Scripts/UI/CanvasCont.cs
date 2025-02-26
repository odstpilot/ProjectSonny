using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasCont : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject aliveCanvas;
    public GameObject deadCanvas;
    // public GameObject pauseCanvas; //For this you must keep track of what canvas is currently active, and track player input)

    void Start()
    {
        aliveCanvas = GameObject.Find("LiveCanvas");
        deadCanvas = GameObject.Find("DeathCanvas");
        deadCanvas.SetActive(false);
    }

    // Update is called once per frame
    public void Death()
    {
        aliveCanvas.SetActive(false);
        deadCanvas.SetActive(true);
    }

    public void ChangeHealth(float hp)
    {
        
            aliveCanvas.GetComponent<LiveCanvas>().ChangeHP(hp);
        
    }
}
