using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float stamina = 100f;
    public float maxStamina = 100f;
    public float staminaDrain = 25f;
    public float staminaRegen = 15f;

    public AudioSource deathSound;

    public float hp;
    public float MaxHP;
    public object CanvasCont;
    public CanvasCont CurrentCanvas;
    public bool testHP;
    public Vector3 StartingPos;

    public Rigidbody2D rb;
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("Footstep Settings")]
    public AudioClip[] footstepClips;
    public float baseStepRate = 0.4f; // Normal walking step interval
    private float stepTimer;
    [SerializeField] private AudioSource audioSource;
    private Vector2 movement;
    private float currentSpeed;

    private void Start()
    {
        hp = MaxHP;
        CurrentCanvas = GameObject.Find("CanvasCont").GetComponent<CanvasCont>();
        testHP = false;
        stepTimer = baseStepRate;
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        movement = movement.normalized;

        bool isMoving = movement != Vector2.zero;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && stamina > 0 && isMoving;

        if (isSprinting)
        {
            currentSpeed = moveSpeed * sprintMultiplier;
            stamina -= staminaDrain * Time.deltaTime;
            stamina = Mathf.Clamp(stamina, 0, maxStamina);
        }
        else
        {
            currentSpeed = moveSpeed;
            if (!Input.GetKey(KeyCode.LeftShift) || !isMoving)
            {
                stamina += staminaRegen * Time.deltaTime;
                stamina = Mathf.Clamp(stamina, 0, maxStamina);
            }
        }

        // Directional animation
        if (isMoving)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                animator.SetInteger("Direction", movement.x > 0 ? 4 : 3);
            else
                animator.SetInteger("Direction", movement.y > 0 ? 1 : 2);
        }
        else
        {
            animator.SetInteger("Direction", 0);
        }

        // Footstep sound handling
        HandleFootsteps(isMoving, isSprinting);

        if (testHP)
        {
            TakeDamage(5);
            testHP = false;
        }

        if (hp <= 0)
        {
            CurrentCanvas.Death();
        }
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }

    public void TakeDamage(float damage)
    {
        hp -= damage;
        testHP = false;

        if (hp <= 0)
            CurrentCanvas.Death();
        else
            CurrentCanvas.ChangeHealth(hp);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            ResetPos();
            CurrentCanvas.Death();
            if (collision.gameObject.TryGetComponent<Warden>(out var warden))
                warden.Reset();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            if (deathSound != null)
                deathSound.Play();

            ResetPos();
            CurrentCanvas.Death();
            if (collision.gameObject.TryGetComponent<Warden>(out var warden))
                warden.Reset();
        }
    }

    public void ResetPos()
    {
        transform.position = StartingPos;
    }

    void HandleFootsteps(bool isMoving, bool isSprinting)
    {
        if (!isMoving || footstepClips.Length == 0 || audioSource == null) return;

        stepTimer -= Time.deltaTime;

        float stepRate = isSprinting ? baseStepRate * 0.6f : baseStepRate;

        if (stepTimer <= 0f)
        {
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            audioSource.pitch = isSprinting ? 1.2f : 1f;
            audioSource.PlayOneShot(clip);
            stepTimer = stepRate;
        }
    }


}
