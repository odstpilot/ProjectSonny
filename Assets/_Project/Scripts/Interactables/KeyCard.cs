using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyCard : MonoBehaviour
{
    public static bool hasKeycard = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            hasKeycard = true;
            Debug.Log("Keycard picked up!");
            Destroy(gameObject);
        }
    }

}
