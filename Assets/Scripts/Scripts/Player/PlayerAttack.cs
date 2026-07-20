using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Attack4LaunchData
{
    public Vector2 attackerPosition;
    public float upForce;
    public float sideForce;
    public float stunTime;

    public Attack4LaunchData(Vector2 attackerPosition, float upForce, float sideForce, float stunTime)
    {
        this.attackerPosition = attackerPosition;
        this.upForce = upForce;
        this.sideForce = sideForce;
        this.stunTime = stunTime;
    }
}

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Hitbox")]
    public Transform attackPoint;
    public float attackRadius = 0.8f;
    public LayerMask attackLayer;

    [Header("Damage")]
    public int baseDamage = 1;

    [Header("Input")]
    public KeyCode attackKey = KeyCode.J;

    [Header("Combo")]
    public int maxCombo = 4;
    public float attackDuration = 0.22f;
    public float hitDelay = 0.08f;
    public float comboResetTime = 0.6f;
    public float attackCooldownAfterCombo = 0.1f;

    [Header("Attack 4 Launcher")]
    public float attack4KnockUpForce = 7f;
    public float attack4KnockBackForce = 1.5f;
    public float attack4StunTime = 0.6f;

    [Header("Attack VFX")]
    public Transform attackEffectPoint;
    public GameObject[] attackEffectPrefabs = new GameObject[4];
    public float attackEffectLifeTime = 0.35f;

    [Header("Rage Gain")]
    public float rageGainPerNormalHit = 0.25f;

    [Header("References")]
    public Animator animator;

    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private RageSystem rageSystem;

    private int comboIndex = 0;
    private bool isAttacking = false;
    private bool queuedNextAttack = false;
    private float lastAttackEndTime = -999f;
    private float nextAttackTime = 0f;

    public bool IsAttacking => isAttacking;
    public int CurrentCombo => comboIndex;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        rageSystem = GetComponent<RageSystem>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (playerMovement != null && playerMovement.IsEvolving) return;

        HandleComboReset();
        HandleAttackInput();

        // Emergency reset for testing.
        if (Input.GetKeyDown(KeyCode.R))
        {
            ForceStopAttack();
        }
    }

    private void HandleAttackInput()
    {
        if (!Input.GetKeyDown(attackKey)) return;

        if (isAttacking)
        {
            if (comboIndex < maxCombo)
            {
                queuedNextAttack = true;
            }

            return;
        }

        if (Time.time < nextAttackTime) return;

        StartNextAttack();
    }

    private void StartNextAttack()
    {
        comboIndex++;

        if (comboIndex > maxCombo)
        {
            comboIndex = 1;
        }

        StartCoroutine(AttackRoutine(comboIndex));
    }

    private IEnumerator AttackRoutine(int attackNumber)
    {
        isAttacking = true;
        queuedNextAttack = false;

        PlayAttackAnimation(attackNumber);
        SpawnAttackEffect(attackNumber);

        yield return new WaitForSeconds(hitDelay);

        int hitCount = DoHit(attackNumber);

        if (hitCount > 0 && rageSystem != null)
        {
            rageSystem.AddRage(rageGainPerNormalHit * hitCount);
        }

        float remainTime = attackDuration - hitDelay;

        if (remainTime > 0f)
        {
            yield return new WaitForSeconds(remainTime);
        }

        isAttacking = false;
        lastAttackEndTime = Time.time;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        if (queuedNextAttack && attackNumber < maxCombo)
        {
            StartNextAttack();
        }
        else
        {
            if (attackNumber >= maxCombo)
            {
                comboIndex = 0;
                nextAttackTime = Time.time + attackCooldownAfterCombo;
            }
        }
    }

    private void PlayAttackAnimation(int attackNumber)
    {
        if (animator == null) return;

        animator.SetBool("IsAttacking", true);

        animator.ResetTrigger("Attack1");
        animator.ResetTrigger("Attack2");
        animator.ResetTrigger("Attack3");
        animator.ResetTrigger("Attack4");

        animator.SetTrigger("Attack" + attackNumber);
    }

    private int DoHit(int attackNumber)
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("PlayerAttack is missing AttackPoint.");
            return 0;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRadius,
            attackLayer
        );

        HashSet<GameObject> damagedObjects = new HashSet<GameObject>();
        int hitCount = 0;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            int finalDamage = GetFinalDamage();

            // Normal enemies.
            EnemyAI enemy = hit.GetComponentInParent<EnemyAI>();

            if (enemy != null && !damagedObjects.Contains(enemy.gameObject))
            {
                enemy.TakeDamage(finalDamage);

                if (attackNumber == 4)
                {
                    Attack4LaunchData launchData = new Attack4LaunchData(
                        transform.position,
                        attack4KnockUpForce,
                        attack4KnockBackForce,
                        attack4StunTime
                    );

                    enemy.SendMessage(
                        "KnockUpFromAttack4",
                        launchData,
                        SendMessageOptions.DontRequireReceiver
                    );
                }

                damagedObjects.Add(enemy.gameObject);
                hitCount++;
                continue;
            }

            // Boss.
            StationaryGreenFlameBossAI boss = hit.GetComponentInParent<StationaryGreenFlameBossAI>();

            if (boss != null && !damagedObjects.Contains(boss.gameObject))
            {
                boss.TakeDamage(finalDamage);

                // Boss should not be launched by Attack 4.
                damagedObjects.Add(boss.gameObject);
                hitCount++;
                continue;
            }

            // Breakable props.
            BreakableBarrel barrel = hit.GetComponentInParent<BreakableBarrel>();

            if (barrel != null && !damagedObjects.Contains(barrel.gameObject))
            {
                barrel.TakeDamage(finalDamage);
                damagedObjects.Add(barrel.gameObject);
            }
        }

        return hitCount;
    }

    private int GetFinalDamage()
    {
        int finalDamage = baseDamage;

        if (playerMovement != null && playerMovement.currentWeapon != null)
        {
            finalDamage += Mathf.RoundToInt(playerMovement.currentWeapon.damageBoost);
        }

        return finalDamage;
    }

    public void ForceStopAttack()
    {
        StopAllCoroutines();

        isAttacking = false;
        queuedNextAttack = false;
        comboIndex = 0;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);

            animator.ResetTrigger("Attack1");
            animator.ResetTrigger("Attack2");
            animator.ResetTrigger("Attack3");
            animator.ResetTrigger("Attack4");
        }
    }

    private void HandleComboReset()
    {
        if (isAttacking) return;
        if (comboIndex <= 0) return;

        if (Time.time - lastAttackEndTime > comboResetTime)
        {
            comboIndex = 0;
        }
    }

    private void SpawnAttackEffect(int attackNumber)
    {
        if (attackEffectPoint == null) return;
        if (attackEffectPrefabs == null) return;

        int index = attackNumber - 1;

        if (index < 0 || index >= attackEffectPrefabs.Length) return;
        if (attackEffectPrefabs[index] == null) return;

        GameObject effect = Instantiate(
            attackEffectPrefabs[index],
            attackEffectPoint.position,
            attackEffectPoint.rotation
        );

        if (playerMovement != null && !playerMovement.IsFacingRight)
        {
            Vector3 scale = effect.transform.localScale;
            scale.x *= -1f;
            effect.transform.localScale = scale;
        }

        Destroy(effect, attackEffectLifeTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}