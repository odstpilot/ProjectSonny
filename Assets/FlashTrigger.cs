using UnityEngine;

public class FlashTrigger : MonoBehaviour
{
    public LayerMask angelLayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & angelLayer) != 0)
        {
            Debug.Log("Camera hit angel: " + other.name);
            var angel = other.GetComponent<WeepingAngel>();
            if (angel != null)
            {
                angel.Stun();
            }
        }
    }
}
