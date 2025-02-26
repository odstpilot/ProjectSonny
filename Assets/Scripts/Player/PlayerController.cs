using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f; // Speed of the player
    public float hp;
    public float MaxHP;
    public object CanvasCont;
    public CanvasCont CurrentCanvas;
    public bool testHP;

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

        if (movement.x != 0 || movement.y != 0)
        {
            animator.SetBool("Moving", true);
            Debug.Log("ffff");
        }
        else
        {
            animator.SetBool("Moving", false);
        }

        // Update animator parameters
        animator.SetFloat("X", movement.x);
        animator.SetFloat("Y", movement.y);
        //animator.SetFloat("speed", movement.sqrMagnitude); // Used for idle animation

        if (movement.x > 0)
        {
            spriteRenderer.flipX = true; // Flip sprite to face right
        }
        else if (movement.x < 0)
        {
            spriteRenderer.flipX = false; // Flip sprite to face left (default)
        }
        if(testHP)
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
}

