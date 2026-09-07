using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadNextSceneOnPlayerCollision : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(currentSceneIndex + 1);
            Debug.Log("aaaaaaaaaghhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh");
        }
        Debug.Log("peepoopeepooo");
    }
    
}

