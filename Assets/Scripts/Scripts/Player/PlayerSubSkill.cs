using System.Collections;
using UnityEngine;

public class PlayerSubSkill : MonoBehaviour
{
    [Header("Input")]
    public KeyCode subSkillKey = KeyCode.U;
    public KeyCode upKey = KeyCode.W;

    [Header("Projectile Prefabs")]
    public GameObject swordWavePrefab;
    public GameObject upwardSwordWavePrefab;

    [Header("Spawn")]
    public Transform projectileSpawnPoint;

    [Header("Projectile Stats")]
    public int subSkillDamage = 1;
    public float rageGainOnHit = 0.35f;

    [Header("Timing")]
    public float castTime = 0.08f;
    public float cooldown = 0.25f;

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

        if (Input.GetKeyDown(subSkillKey))
        {
            TryUseSubSkill();
        }
    }

    private void TryUseSubSkill()
    {
        if (Time.time < nextUseTime) return;
        if (isCasting) return;

        // Tạm thời không cho dùng U khi đang đánh thường.
        // Sau này làm combo link thì có thể nới điều kiện này.
        if (playerAttack != null && playerAttack.IsAttacking) return;

        bool useUpVersion = Input.GetKey(upKey);

        StartCoroutine(SubSkillRoutine(useUpVersion));
    }

    private IEnumerator SubSkillRoutine(bool useUpVersion)
    {
        isCasting = true;

        if (animator != null)
        {
            animator.ResetTrigger("SubSkill");
            animator.ResetTrigger("SubSkillUp");

            if (useUpVersion)
            {
                animator.SetTrigger("SubSkillUp");
            }
            else
            {
                animator.SetTrigger("SubSkill");
            }
        }

        yield return new WaitForSeconds(castTime);

        SpawnProjectile(useUpVersion);

        nextUseTime = Time.time + cooldown;
        isCasting = false;
    }

    private void SpawnProjectile(bool useUpVersion)
    {
        if (projectileSpawnPoint == null)
        {
            Debug.LogWarning("Thiếu ProjectileSpawnPoint.");
            return;
        }

        GameObject prefab = useUpVersion ? upwardSwordWavePrefab : swordWavePrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Thiếu prefab kiếm khí.");
            return;
        }

        bool facingRight = true;

        if (playerMovement != null)
        {
            facingRight = playerMovement.IsFacingRight;
        }

        Vector2 direction;

        if (useUpVersion)
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
            swordWave.Init(
                direction,
                subSkillDamage,
                rageSystem,
                rageGainOnHit
            );
        }
    }
}