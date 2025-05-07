using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{
    private bool isOpen = false;

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
        }
    }
}
