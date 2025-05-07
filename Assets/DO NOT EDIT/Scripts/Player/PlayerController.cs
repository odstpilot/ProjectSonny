using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f; // Speed of the player
    public float hp;
    public float MaxHP;
    public object CanvasCont;
    public CanvasCont CurrentCanvas;
    public bool testHP;
    public Vector3 StartingPos;

    public Rigidbody2D rb; // Rigidbody2D for physics-based movement
    public Animator animator; // Animator for controlling animations
    public SpriteRenderer spriteRenderer;

    private Vector2 movement; // To store movement input

    private void Start()
    {
        hp = MaxHP; //Temp
        //CanvasCont. = GetComponent<Canvas>();
        CurrentCanvas = GameObject.Find("CanvasCont").GetComponent<CanvasCont>();
        testHP = false;
    }

    void Update()
    {
        // Get movement input
        movement.x = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right Arrow
        movement.y = Input.GetAxisRaw("Vertical");   // W/S or Up/Down Arrow

        // Normalize movement vector to avoid faster diagonal movement
        movement = movement.normalized;


        if (movement != Vector2.zero)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
            {
                // Horizontal is dominant
                if (movement.x > 0)
                    animator.SetInteger("Direction", 4); // Right
                else
                    animator.SetInteger("Direction", 3); // Left
            }
            else
            {
                // Vertical is dominant
                if (movement.y > 0)
                    animator.SetInteger("Direction", 1); // Up
                else
                    animator.SetInteger("Direction", 2); // Down
            }
        }
        else
        {
            animator.SetInteger("Direction", 0); // Idle
        }

        if (testHP)
        {
            TakeDamage(5);
            testHP = false;
        }
    }

    void FixedUpdate()
    {
        // Move the player
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    public void TakeDamage(float damage)
    {
        hp-= damage;
        testHP = false;

        if (hp <= 0)
        {
            CurrentCanvas.Death();
        }
       else
       {
            CurrentCanvas.ChangeHealth(hp);
           
        }
        
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Enemy")
        {
            ResetPos();
            
            if (collision.gameObject.GetComponent<Warden>() != null)
            {
                collision.gameObject.GetComponent<Warden>().Reset();
            }
           
        }
    }
   

    public void ResetPos()
    {
        transform.position = StartingPos;
    }
}

