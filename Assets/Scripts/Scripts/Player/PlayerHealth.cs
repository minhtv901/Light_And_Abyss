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
    private bool isInvincible = false;

    private PlayerAttack playerAttack;
    private PlayerGuard playerGuard;

    public bool IsDead => isDead;

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
        if (isInvincible) return;

        // Nếu đang guard
        if (playerGuard != null && playerGuard.IsGuarding)
        {
            int guardDamage = playerGuard.ProcessGuardDamage(amount);

            // Nếu guard vẫn bị mất một ít máu
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

            // Quan trọng:
            // Đang guard thì chỉ chạy GuardHit, không chạy Hurt/GetHit
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
        isInvincible = true;
        yield return new WaitForSeconds(invincibleTime);
        isInvincible = false;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isInvincible = true;

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