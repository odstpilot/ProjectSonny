using System.Collections;
using UnityEngine;

public class CanvasCont : MonoBehaviour
{
    public GameObject aliveCanvas;
    public GameObject deadCanvas;
    public RobotQuoteManager text;

    void Start()
    {
        aliveCanvas = GameObject.Find("LiveCanvas");
        deadCanvas = GameObject.Find("DeathCanvas");
        deadCanvas.SetActive(false);
    }

    public void Death()
    {
        Debug.Log("hh");
        StartCoroutine(ShowDeathScreen());
        text.DisplayRandomQuote();

    }

    private IEnumerator ShowDeathScreen()
    {
        aliveCanvas.SetActive(false);
        deadCanvas.SetActive(true);

        yield return new WaitForSeconds(3f); // wait for 5 seconds

        deadCanvas.SetActive(false);
        aliveCanvas.SetActive(true);
    }

    public void ChangeHealth(float hp)
    {
        aliveCanvas.GetComponent<LiveCanvas>().ChangeHP(hp);
    }
}
