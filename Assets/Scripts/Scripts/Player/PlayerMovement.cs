using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 7f;
    public float jumpForce = 6f;

    [Header("Jump Settings")]
    public int maxJumpCount = 2;
    public float doubleJumpForceMultiplier = 0.9f;

    private int jumpCount = 0;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.25f);
    public float groundCheckExtraDistance = 0.1f;

    [Header("Animator")]
    public Animator animator;

    [Header("Weapon System")]
    public WeaponData currentWeapon;
    public SpriteRenderer weaponRenderer;

    [Header("Light Route Settings")]
    public GameObject strikerPrefab;
    public float summonCooldown = 2f;

    [Header("Route Weapons")]
    public WeaponData swordData;
    public WeaponData staffData;

    [Header("Evolution Settings")]
    public float evolveDuration = 1.5f;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private PlayerAttack playerAttack;
    private PlayerHealth playerHealth;

    private float moveInput;
    private bool isGrounded;
    private bool wasGrounded;
    private bool hasCheckedGround;
    private bool isFacingRight = true;
    private bool isEvolving = false;
    private float nextSummonTime = 0f;
    private PlayerGuard playerGuard;

    public bool IsGrounded => isGrounded;
    public bool IsFacingRight => isFacingRight;
    public bool IsEvolving => isEvolving;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        playerAttack = GetComponent<PlayerAttack>();
        playerHealth = GetComponent<PlayerHealth>();
        playerGuard = GetComponent<PlayerGuard>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        UpdateWeaponVisual();
    }

    private void Update()
    {
        CheckGround();

        if (!CanControl())
        {
            moveInput = 0f;
            UpdateAnimator();
            return;
        }

        ReadMoveInput();
        Jump();
        Summon();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (!CanControl())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
    }

    private bool CanControl()
    {
        if (isEvolving) return false;
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (playerAttack != null && playerAttack.IsAttacking) return false;

        bool guarding = playerGuard != null && playerGuard.IsGuarding;

        if (guarding)
        {
            return false;
        }

        return true;
    }

    private void ReadMoveInput()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (moveInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (moveInput < 0 && isFacingRight)
        {
            Flip();
        }
    }

    void Jump()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;

        Debug.Log("Bấm Space. isGrounded = " + isGrounded + " | jumpCount = " + jumpCount);

        if (jumpCount >= maxJumpCount) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        float finalJumpForce = jumpForce;

        // Nhảy lần 2 yếu hơn một chút cho tự nhiên
        if (jumpCount > 0)
        {
            finalJumpForce *= doubleJumpForceMultiplier;
        }

        rb.AddForce(Vector2.up * finalJumpForce, ForceMode2D.Impulse);

        jumpCount++;

        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    private void CheckGround()
    {
        wasGrounded = isGrounded;

        Vector2 checkPos;

        if (playerCollider != null)
        {
            checkPos = new Vector2(
                playerCollider.bounds.center.x,
                playerCollider.bounds.min.y - groundCheckExtraDistance
            );
        }
        else
        {
            checkPos = new Vector2(transform.position.x, transform.position.y - 0.6f);
        }

        isGrounded = Physics2D.OverlapBox(
            checkPos,
            groundCheckSize,
            0f,
            groundLayer
        );

        if (hasCheckedGround && !wasGrounded && isGrounded)
        {
            jumpCount = 0;

            if (animator != null)
            {
                animator.SetTrigger("Land");
            }
        }

        hasCheckedGround = true;
    }

    private void UpdateAnimator()
    {
        if (animator == null || rb == null) return;

        animator.SetFloat("Speed", Mathf.Abs(moveInput));
        animator.SetFloat("YVelocity", rb.linearVelocity.y);
        animator.SetBool("IsGrounded", isGrounded);
    }

    private void Summon()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (GameManager.instance != null &&
                GameManager.instance.currentRoute == GameManager.GameRoute.Light)
            {
                if (Time.time >= nextSummonTime)
                {
                    SummonStriker();
                    nextSummonTime = Time.time + summonCooldown;
                }
            }
            else
            {
                Debug.Log("Route Abyss không gọi đệ được đâu đại ca!");
            }
        }
    }

    public void UpdateWeaponVisual()
    {
        if (currentWeapon != null && weaponRenderer != null)
        {
            weaponRenderer.sprite = currentWeapon.weaponSprite;
        }
    }

    public IEnumerator EvolveRoutine(WeaponData newData)
    {
        isEvolving = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log("ĐANG TIẾN HÓA...");

        yield return new WaitForSeconds(evolveDuration);

        currentWeapon = newData;
        UpdateWeaponVisual();

        Debug.Log("TIẾN HÓA THÀNH CÔNG!");
        isEvolving = false;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    private void SummonStriker()
    {
        Vector3 spawnPos = transform.position + new Vector3(isFacingRight ? 1.5f : -1.5f, 0.5f, 0f);
        GameObject striker = Instantiate(strikerPrefab, spawnPos, Quaternion.identity);
        Destroy(striker, 1f);

        Debug.Log("Assist time!");
    }

    public void ChangeWeaponByRoute(GameManager.GameRoute route)
    {
        if (route == GameManager.GameRoute.Light)
        {
            currentWeapon = swordData;
        }
        else if (route == GameManager.GameRoute.Abyss)
        {
            currentWeapon = staffData;
        }

        UpdateWeaponVisual();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Collider2D col = GetComponent<Collider2D>();
        Vector2 groundPos;

        if (col != null)
        {
            groundPos = new Vector2(
                col.bounds.center.x,
                col.bounds.min.y - groundCheckExtraDistance
            );
        }
        else
        {
            groundPos = new Vector2(transform.position.x, transform.position.y - 0.6f);
        }

        Gizmos.DrawWireCube(groundPos, groundCheckSize);
    }
}