using System.Collections;
using UnityEngine;

public class PlayerMainSkill : MonoBehaviour
{
    [Header("Input")]
    public KeyCode skillKey = KeyCode.I;
    public KeyCode upKey = KeyCode.W;

    [Header("Projectile Prefabs")]
    public GameObject mainSkillProjectilePrefab;
    public GameObject mainSkillUpProjectilePrefab;

    [Header("Spawn")]
    public Transform projectileSpawnPoint;

    [Header("Skill Stats")]
    public int skillDamage = 2;
    public int upSkillDamage = 2;

    [Header("Projectile Speed")]
    public float normalSkillSpeed = 8.5f;
    public float upSkillSpeed = 8f;

    [Header("Timing")]
    public float startupDelay = 0.22f;
    public float cooldown = 0.6f;

    [Header("Screen Signal")]
    public ScreenDimmer screenDimmer;
    public float dimAlpha = 0.35f;
    public float dimFadeIn = 0.06f;
    public float dimHold = 0.08f;
    public float dimFadeOut = 0.12f;

    [Header("References")]
    public Animator animator;

    private PlayerMovement playerMovement;
    private PlayerAttack playerAttack;
    private PlayerHealth playerHealth;
    private RageSystem rageSystem;

    private bool isCasting = false;
    private float nextUseTime = 0f;

    public bool IsCasting => isCasting;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerAttack = GetComponent<PlayerAttack>();
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

        if (Input.GetKeyDown(skillKey))
        {
            TryUseSkill();
        }
    }

    private void TryUseSkill()
    {
        if (Time.time < nextUseTime) return;
        if (isCasting) return;

        if (playerAttack != null && playerAttack.IsAttacking) return;

        if (rageSystem == null)
        {
            Debug.LogWarning("Player thiếu RageSystem.");
            return;
        }

        // I và W+I đều tốn 1 stack
        if (!rageSystem.TrySpendSkill())
        {
            Debug.Log("Không đủ nộ để dùng skill.");
            return;
        }

        bool useUpSkill = Input.GetKey(upKey);

        StartCoroutine(MainSkillRoutine(useUpSkill));
    }

    private IEnumerator MainSkillRoutine(bool useUpSkill)
    {
        isCasting = true;

        // 1. Vừa bấm I là chạy animation chuẩn bị ngay
        if (animator != null)
        {
            animator.ResetTrigger("Skill");
            animator.ResetTrigger("SkillUp");

            if (useUpSkill)
            {
                animator.SetTrigger("SkillUp");
            }
            else
            {
                animator.SetTrigger("Skill");
            }
        }

        // 2. Đồng thời bật hiệu ứng tối màn
        if (screenDimmer != null)
        {
            screenDimmer.PlayDim(dimAlpha, dimFadeIn, dimHold, dimFadeOut);
        }

        // 3. Chờ thời gian tụ lực / khựng
        yield return new WaitForSeconds(startupDelay);

        // 4. Bắn projectile
        SpawnSkillProjectile(useUpSkill);

        nextUseTime = Time.time + cooldown;
        isCasting = false;
    }

    private void SpawnSkillProjectile(bool useUpSkill)
    {
        if (projectileSpawnPoint == null)
        {
            Debug.LogWarning("Thiếu ProjectileSpawnPoint.");
            return;
        }

        GameObject prefab = useUpSkill ? mainSkillUpProjectilePrefab : mainSkillProjectilePrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Thiếu prefab skill projectile.");
            return;
        }

        bool facingRight = true;

        if (playerMovement != null)
        {
            facingRight = playerMovement.IsFacingRight;
        }

        Vector2 direction;

        if (useUpSkill)
        {
            direction = facingRight ? new Vector2(1f, 1f) : new Vector2(-1f, 1f);
        }
        else
        {
            direction = facingRight ? Vector2.right : Vector2.left;
        }

        GameObject projectile = Instantiate(
            prefab,
            projectileSpawnPoint.position,
            Quaternion.identity
        );

        SwordWaveProjectile swordWave = projectile.GetComponent<SwordWaveProjectile>();

        if (swordWave != null)
        {
            swordWave.speed = useUpSkill ? upSkillSpeed : normalSkillSpeed;

            swordWave.Init(
                direction,
                useUpSkill ? upSkillDamage : skillDamage,
                rageSystem,
                0f
            );
        }
    }
}