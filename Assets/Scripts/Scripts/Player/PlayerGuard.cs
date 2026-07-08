using System.Collections;
using UnityEngine;

public class PlayerGuard : MonoBehaviour
{
    [Header("Input")]
    public KeyCode guardKey = KeyCode.S;

    [Header("Guard Settings")]
    public bool reduceDamageInsteadOfBlock = false;
    public float damageReduceRate = 0.7f;
    public float guardHitLockTime = 0.18f;

    [Header("References")]
    public Animator animator;

    private PlayerAttack playerAttack;
    private PlayerHealth playerHealth;
    private PlayerMainSkill playerMainSkill;
    private PlayerSubSkill playerSubSkill;

    private bool isGuarding = false;
    private bool isGuardHitLocked = false;

    public bool IsGuarding => isGuarding;
    public bool IsGuardHitLocked => isGuardHitLocked;

    private void Awake()
    {
        playerAttack = GetComponent<PlayerAttack>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMainSkill = GetComponent<PlayerMainSkill>();
        playerSubSkill = GetComponent<PlayerSubSkill>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            SetGuard(false);
            return;
        }

        bool canGuard = CanGuard();
        bool pressingGuard = Input.GetKey(guardKey);

        SetGuard(pressingGuard && canGuard);
    }

    private bool CanGuard()
    {
        if (isGuardHitLocked) return true;

        if (playerAttack != null && playerAttack.IsAttacking) return false;
        if (playerMainSkill != null && playerMainSkill.IsCasting) return false;
        if (playerSubSkill != null && playerSubSkill.IsCasting) return false;

        return true;
    }

    private void SetGuard(bool value)
    {
        if (isGuarding == value) return;

        isGuarding = value;

        if (animator != null)
        {
            animator.SetBool("IsGuarding", isGuarding);
        }
    }

    public int ProcessGuardDamage(int incomingDamage)
    {
        if (!isGuarding)
        {
            return incomingDamage;
        }

        PlayGuardHit();

        if (reduceDamageInsteadOfBlock)
        {
            int reducedDamage = Mathf.CeilToInt(incomingDamage * (1f - damageReduceRate));
            return Mathf.Max(0, reducedDamage);
        }

        return 0;
    }

    public void PlayGuardHit()
    {
        if (animator != null)
        {
            animator.ResetTrigger("GuardHit");
            animator.SetTrigger("GuardHit");
        }

        StopCoroutine(nameof(GuardHitLockRoutine));
        StartCoroutine(GuardHitLockRoutine());
    }

    private IEnumerator GuardHitLockRoutine()
    {
        isGuardHitLocked = true;

        yield return new WaitForSeconds(guardHitLockTime);

        isGuardHitLocked = false;
    }
}