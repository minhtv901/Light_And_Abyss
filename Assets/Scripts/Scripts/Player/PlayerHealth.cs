using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHP = 10;
    public int currentHP;

    [Header("Invincible")]
    public float invincibleTime = 0.5f;

    [Header("Animator")]
    public Animator animator;

    private bool isDead = false;

    // Invincible sau khi bị đánh
    private bool isHitInvincible = false;

    // Invincible khi dash
    private bool isDashInvincible = false;

    private PlayerAttack playerAttack;
    private PlayerGuard playerGuard;

    public bool IsDead => isDead;

    public bool IsInvincible
    {
        get
        {
            return isDead || isHitInvincible || isDashInvincible;
        }
    }

    private void Awake()
    {
        currentHP = maxHP;
        playerAttack = GetComponent<PlayerAttack>();
        playerGuard = GetComponent<PlayerGuard>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;
        if (IsInvincible) return;

        // Nếu đang guard
        if (playerGuard != null && playerGuard.IsGuarding)
        {
            int guardDamage = playerGuard.ProcessGuardDamage(amount);

            if (guardDamage > 0)
            {
                currentHP -= guardDamage;
                currentHP = Mathf.Clamp(currentHP, 0, maxHP);

                Debug.Log("Player đỡ đòn nhưng vẫn mất damage = " + guardDamage + " | HP = " + currentHP);

                if (currentHP <= 0)
                {
                    Die();
                    return;
                }
            }
            else
            {
                Debug.Log("Player guard thành công, không mất máu.");
            }

            StartCoroutine(InvincibleRoutine());
            return;
        }

        // Không guard thì ăn damage bình thường
        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        Debug.Log("Player bị damage = " + amount + " | HP = " + currentHP);

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        Hurt();
    }

    // Hàm này để PlayerMovement gọi khi dash
    public void SetInvincible(bool value)
    {
        isDashInvincible = value;
    }

    private void Hurt()
    {
        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }

        StartCoroutine(InvincibleRoutine());
    }

    private IEnumerator InvincibleRoutine()
    {
        isHitInvincible = true;
        yield return new WaitForSeconds(invincibleTime);
        isHitInvincible = false;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isHitInvincible = true;
        isDashInvincible = true;

        StopAllCoroutines();

        if (playerAttack != null)
        {
            playerAttack.ForceStopAttack();
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (animator != null)
        {
            animator.ResetTrigger("Hurt");
            animator.ResetTrigger("GuardHit");
            animator.ResetTrigger("Attack1");
            animator.ResetTrigger("Attack2");
            animator.ResetTrigger("Attack3");
            animator.ResetTrigger("Attack4");
            animator.ResetTrigger("Jump");
            animator.ResetTrigger("Land");
            animator.ResetTrigger("Dash");

            animator.SetBool("IsGuarding", false);
            animator.SetBool("IsAttacking", false);
            animator.SetBool("IsDead", true);

            animator.Play("Player_Defeat_New", 0, 0f);
        }

        Debug.Log("Player chết. Chạy Player_Defeat_New.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            currentHP = 0;
            Die();
        }
    }
}