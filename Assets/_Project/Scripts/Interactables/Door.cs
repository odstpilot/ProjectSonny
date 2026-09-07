using UnityEngine;
using System.Collections;

public class Door : MonoBehaviour
{
    private bool isOpen = false;
    public GameObject noKeycardText; // Drag the UI text object in the Inspector
    public GameObject objectToDisable; // Drag the GameObject to disable when the door opens

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (KeyCard.hasKeycard)
            {
                OpenDoor();
            }
            else
            {
                Debug.Log("You need a keycard to open this door.");
                StartCoroutine(ShowNoKeycardText());
            }
        }
    }

    void OpenDoor()
    {
        if (!isOpen)
        {
            isOpen = true;
            Debug.Log("Door opened.");
            GetComponent<BoxCollider2D>().enabled = false;

          
            objectToDisable.SetActive(false);
            
        }
    }

    IEnumerator ShowNoKeycardText()
    {
        noKeycardText.SetActive(true);
        yield return new WaitForSeconds(3f);
        noKeycardText.SetActive(false);
    }
}