using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState { Spawning, Chasing, Attacking, HitStun, Dead }

    [Header("Enemy Type")]
    public EnemyType enemyType;

    [Header("Stats")]
    public int maxHP = 3;
    public int currentHP = 3;
    public int damage = 1;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float detectRange = 10f;
    public float attackRange = 1.2f;

    [Header("Platform / Height Check")]
    public bool requireSameLevelToChase = true;
    public float chaseVerticalTolerance = 1.2f;

    [Tooltip("Khoảng lệch dọc cho phép khi đánh. Để 0.5 - 1.5 cho game đi ngang. Đừng để 999 nữa.")]
    public float verticalAttackTolerance = 1f;

    [Header("Platform Patrol")]
    [Tooltip("Bật lên nếu muốn quái đi qua đi lại trên platform khi Player chưa đến gần hoặc khác tầng.")]
    public bool patrolWhenPlayerFar = false;

    [Tooltip("Khoảng cách tuần tra tính từ vị trí spawn ban đầu.")]
    public float patrolDistance = 3f;

    [Tooltip("Tốc độ tuần tra khi Player chưa đến gần.")]
    public float patrolSpeed = 1.2f;

    [Tooltip("Bật lên để quái tự quay đầu khi sắp đi ra mép platform.")]
    public bool preventFallingOffLedge = true;

    [Tooltip("Khoảng check phía trước chân để phát hiện còn ground hay không.")]
    public float ledgeCheckForwardOffset = 0.35f;

    [Tooltip("Kích thước ô check mép platform.")]
    public Vector2 ledgeCheckSize = new Vector2(0.25f, 0.18f);

    [Header("Ground Check")]
    public bool requireGroundedToMove = true;
    public bool requireGroundedToAttack = true;
    public LayerMask groundLayer;
    public Vector2 groundCheckSize = new Vector2(0.75f, 0.18f);
    public float groundCheckExtraDistance = 0.08f;

    [Header("Attack")]
    public float attackCooldown = 1f;
    public float meleeDamageDelay = 0.15f;

    [Header("Ranged / Mage Projectile")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 7f;
    public float projectileLifeTime = 4f;
    public float projectileFireDelay = 0.25f;

    [Header("Tank Guard")]
    public bool tankDealsDamage = false;
    public bool tankCanGuard = true;
    public float tankGuardRadius = 3f;
    [Range(0.05f, 1f)] public float tankSelfDamageMultiplier = 0.45f;
    [Range(0.05f, 1f)] public float tankAllyDamageMultiplier = 0.6f;
    public float tankGuardCooldown = 2.5f;
    public float tankGuardDuration = 0.6f;

    [Header("Animation Timing")]
    public float spawnDuration = 1.2f;
    public float hitStunDuration = 0.25f;
    public float deathDuration = 1f;

    [Header("Animator Parameters")]
    public string isMovingBool = "IsMoving";
    public string spawnTrigger = "Spawn";
    public string hitTrigger = "Hit";
    public string deathTrigger = "Death";
    public string attackTrigger = "Attack";
    public string guardTrigger = "Guard";

    [Header("Options")]
    public bool applyDefaultStatsOnAwake = false;
    public bool autoFindPlayer = true;
    public bool flipToPlayer = true;
    public bool sideScrollerXOnly = true;
    public bool destroyAfterDeath = true;
    public bool disableColliderOnDeath = true;

    [Header("Facing Fix")]
    public bool useSpriteRendererFlip = false;
    public bool invertSpriteFacing = false;
    public SpriteRenderer visualSpriteRenderer;

    [Header("Debug")]
    public bool drawGizmos = true;

    private Rigidbody2D rb;
    private Collider2D mainCollider;
    private Animator animator;
    private Transform player;
    private bool isKnockedUp = false;
    private Collider2D playerCollider;

    private EnemyState state = EnemyState.Spawning;

    private float nextAttackTime;
    private float nextGuardTime;
    private float guardUntilTime;
    private Coroutine attackCoroutine;

    private Vector2 homePosition;
    private int patrolDirection = 1;

    public bool IsDead => state == EnemyState.Dead;
    public bool IsGuarding => enemyType == EnemyType.Tank && Time.time < guardUntilTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<Collider2D>();
        animator = GetComponentInChildren<Animator>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.freezeRotation = true;
        rb.gravityScale = Mathf.Max(1f, rb.gravityScale);
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (applyDefaultStatsOnAwake)
            ApplyDefaultStats();

        currentHP = maxHP;
    }

    private void OnEnable()
    {
        StopAllCoroutines();

        homePosition = transform.position;
        patrolDirection = 1;

        state = EnemyState.Spawning;
        StartCoroutine(SpawnRoutine());
    }

    private void Start()
    {
        if (autoFindPlayer) FindPlayer();
    }

    private IEnumerator SpawnRoutine()
    {
        StopMoveX();
        SetMoving(false);

        if (animator != null && !string.IsNullOrEmpty(spawnTrigger))
            animator.SetTrigger(spawnTrigger);

        yield return new WaitForSeconds(spawnDuration);

        if (state != EnemyState.Dead)
            state = EnemyState.Chasing;
    }

    private void Update()
    {
        if (state == EnemyState.Dead || state == EnemyState.Spawning || state == EnemyState.HitStun)
            return;

        if (isKnockedUp) 
            return;

        if (player == null && autoFindPlayer)
            FindPlayer();

        bool grounded = IsGrounded();

        if (requireGroundedToMove && !grounded)
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        if (player == null)
        {
            if (patrolWhenPlayerFar)
                PatrolPlatform();
            else
            {
                StopMoveX();
                SetMoving(false);
            }

            return;
        }

        FacePlayer();

        float horizontalGap = GetHorizontalGapToPlayer();
        float verticalGap = GetVerticalGapToPlayer();

        bool isMeleeLikeEnemy = enemyType == EnemyType.Duelist || enemyType == EnemyType.Tank;

        if (sideScrollerXOnly && requireSameLevelToChase && isMeleeLikeEnemy)
        {
            if (verticalGap > chaseVerticalTolerance)
            {
                if (patrolWhenPlayerFar)
                    PatrolPlatform();
                else
                {
                    StopMoveX();
                    SetMoving(false);
                }

                return;
            }
        }

        if (horizontalGap > detectRange)
        {
            if (patrolWhenPlayerFar)
                PatrolPlatform();
            else
            {
                StopMoveX();
                SetMoving(false);
            }

            state = EnemyState.Chasing;
            return;
        }

        if (IsPlayerInAttackRange())
        {
            StopMoveX();
            SetMoving(false);
            TryAttack();
        }
        else
        {
            state = EnemyState.Chasing;
            MoveTowardPlayer();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
            playerCollider = playerObj.GetComponentInChildren<Collider2D>();
        }
    }

    private void MoveTowardPlayer()
    {
        if (player == null) return;

        float directionX = player.position.x > transform.position.x ? 1f : -1f;

        if (preventFallingOffLedge && !HasGroundAhead(directionX))
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        if (sideScrollerXOnly)
        {
            rb.linearVelocity = new Vector2(directionX * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            Vector2 direction = ((Vector2)player.position - rb.position).normalized;
            rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);
        }

        SetMoving(true);
    }

    private void PatrolPlatform()
    {
        if (!patrolWhenPlayerFar || rb == null)
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        if (requireGroundedToMove && !IsGrounded())
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        if (Mathf.Abs(transform.position.x - homePosition.x) >= patrolDistance)
        {
            patrolDirection *= -1;
        }

        if (preventFallingOffLedge && !HasGroundAhead(patrolDirection))
        {
            patrolDirection *= -1;
        }

        FaceMoveDirection(patrolDirection);

        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);
        SetMoving(true);
    }

    private bool HasGroundAhead(float directionX)
    {
        if (groundLayer.value == 0)
            return true;

        if (mainCollider == null)
            return true;

        float x = mainCollider.bounds.center.x
                  + directionX * (mainCollider.bounds.extents.x + ledgeCheckForwardOffset);

        float y = mainCollider.bounds.min.y - groundCheckExtraDistance;

        Vector2 checkPos = new Vector2(x, y);

        return Physics2D.OverlapBox(checkPos, ledgeCheckSize, 0f, groundLayer);
    }

    private void StopMoveX()
    {
        if (rb == null) return;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime) return;
        if (state == EnemyState.Attacking) return;
        if (requireGroundedToAttack && !IsGrounded()) return;

        nextAttackTime = Time.time + attackCooldown;

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        state = EnemyState.Attacking;

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        switch (enemyType)
        {
            case EnemyType.Duelist:
                yield return new WaitForSeconds(meleeDamageDelay);
                MeleeDamagePlayer();
                break;

            case EnemyType.FastRanged:
            case EnemyType.Mage:
                yield return new WaitForSeconds(projectileFireDelay);
                ShootProjectile();
                break;

            case EnemyType.Tank:
                if (tankDealsDamage)
                {
                    yield return new WaitForSeconds(meleeDamageDelay);
                    MeleeDamagePlayer();
                }
                break;
        }

        state = EnemyState.Chasing;
    }

    private void MeleeDamagePlayer()
    {
        if (player == null) return;
        if (!IsPlayerInAttackRange()) return;

        PlayerHealth hp = player.GetComponentInParent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(damage);
        else
            player.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        Debug.Log($"{enemyType} gây damage Player = {damage}");
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null || player == null)
        {
            Debug.LogWarning($"{name}: Chưa gán projectilePrefab hoặc không tìm thấy Player.");
            return;
        }

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        GameObject projectileObj = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);

        Vector2 direction = ((Vector2)player.position - (Vector2)spawnPoint.position).normalized;

        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();

        if (projectile != null)
            projectile.Init(direction, projectileSpeed, damage, projectileLifeTime);
        else
            Debug.LogWarning($"{projectileObj.name}: Thiếu EnemyProjectile.cs trên prefab đạn.");
    }

    public void TakeDamage(int amount)
    {
        if (state == EnemyState.Dead) return;

        bool usedGuard;
        int finalDamage = CalculateFinalDamage(amount, out usedGuard);

        currentHP -= finalDamage;
        currentHP = Mathf.Max(0, currentHP);

        Debug.Log($"{enemyType} bị đánh: raw={amount}, final={finalDamage}, HP={currentHP}/{maxHP}, guarded={usedGuard}");

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (usedGuard)
        {
            if (animator != null && !string.IsNullOrEmpty(guardTrigger))
                animator.SetTrigger(guardTrigger);

            StartCoroutine(GuardStunRoutine());
        }
        else
        {
            if (animator != null && !string.IsNullOrEmpty(hitTrigger))
                animator.SetTrigger(hitTrigger);

            StartCoroutine(HitStunRoutine());
        }
    }

    private int CalculateFinalDamage(int amount, out bool usedGuard)
    {
        usedGuard = false;
        float multiplier = 1f;

        if (enemyType == EnemyType.Tank && tankCanGuard)
        {
            if (Time.time >= nextGuardTime)
            {
                usedGuard = true;
                multiplier *= tankSelfDamageMultiplier;
                StartTankGuard();
            }
        }
        else
        {
            EnemyAI guardTank = FindNearbyAvailableTank();

            if (guardTank != null)
            {
                usedGuard = true;
                multiplier *= guardTank.tankAllyDamageMultiplier;
                guardTank.StartTankGuard();
            }
        }

        return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
    }

    private EnemyAI FindNearbyAvailableTank()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, tankGuardRadius);

        foreach (Collider2D hit in hits)
        {
            EnemyAI tank = hit.GetComponentInParent<EnemyAI>();

            if (tank == null) continue;
            if (tank == this) continue;
            if (tank.enemyType != EnemyType.Tank) continue;
            if (tank.IsDead) continue;
            if (!tank.tankCanGuard) continue;
            if (Time.time < tank.nextGuardTime) continue;

            return tank;
        }

        return null;
    }

    private void StartTankGuard()
    {
        if (enemyType != EnemyType.Tank) return;

        guardUntilTime = Time.time + tankGuardDuration;
        nextGuardTime = Time.time + tankGuardCooldown;

        Debug.Log("Tank đỡ đòn / giảm sát thương.");
    }

    private IEnumerator HitStunRoutine()
    {
        state = EnemyState.HitStun;

        StopMoveX();
        SetMoving(false);

        yield return new WaitForSeconds(hitStunDuration);

        if (state != EnemyState.Dead)
            state = EnemyState.Chasing;
    }

    private IEnumerator GuardStunRoutine()
    {
        state = EnemyState.HitStun;

        StopMoveX();
        SetMoving(false);

        yield return new WaitForSeconds(tankGuardDuration);

        if (state != EnemyState.Dead)
            state = EnemyState.Chasing;
    }

    private void Die()
    {
        state = EnemyState.Dead;

        StopMoveX();
        SetMoving(false);

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (disableColliderOnDeath && mainCollider != null)
            mainCollider.enabled = false;

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        Debug.Log($"{enemyType} đã chết.");

        if (destroyAfterDeath)
            Destroy(gameObject, deathDuration);
    }

    private bool IsPlayerInAttackRange()
    {
        if (player == null) return false;
        if (requireGroundedToAttack && !IsGrounded()) return false;

        float horizontalGap = GetHorizontalGapToPlayer();
        float verticalGap = GetVerticalGapToPlayer();

        return horizontalGap <= attackRange && verticalGap <= verticalAttackTolerance;
    }

    private float GetHorizontalGapToPlayer()
    {
        if (player == null) return Mathf.Infinity;

        if (mainCollider != null && playerCollider != null)
        {
            Bounds a = mainCollider.bounds;
            Bounds b = playerCollider.bounds;

            if (a.max.x < b.min.x) return b.min.x - a.max.x;
            if (b.max.x < a.min.x) return a.min.x - b.max.x;
            return 0f;
        }

        return Mathf.Abs(player.position.x - transform.position.x);
    }

    private float GetVerticalGapToPlayer()
    {
        if (player == null) return Mathf.Infinity;

        if (mainCollider != null && playerCollider != null)
        {
            Bounds a = mainCollider.bounds;
            Bounds b = playerCollider.bounds;

            if (a.max.y < b.min.y) return b.min.y - a.max.y;
            if (b.max.y < a.min.y) return a.min.y - b.max.y;
            return 0f;
        }

        return Mathf.Abs(player.position.y - transform.position.y);
    }

    private bool IsGrounded()
    {
        if (groundLayer.value == 0)
            return true;

        Vector2 checkPos;

        if (mainCollider != null)
        {
            checkPos = new Vector2(
                mainCollider.bounds.center.x,
                mainCollider.bounds.min.y - groundCheckExtraDistance
            );
        }
        else
        {
            checkPos = new Vector2(transform.position.x, transform.position.y - 0.5f);
        }

        return Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundLayer);
    }

    private void FacePlayer()
    {
        if (!flipToPlayer || player == null) return;

        float direction = player.position.x - transform.position.x;

        if (Mathf.Abs(direction) < 0.05f) return;

        ApplyFacing(direction > 0f);
    }

    private void FaceMoveDirection(float directionX)
    {
        if (!flipToPlayer) return;
        if (Mathf.Abs(directionX) < 0.05f) return;

        ApplyFacing(directionX > 0f);
    }

    private void ApplyFacing(bool faceRight)
    {
        if (useSpriteRendererFlip)
        {
            if (visualSpriteRenderer == null)
                visualSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (visualSpriteRenderer == null) return;

            bool flip = faceRight;

            if (invertSpriteFacing)
                flip = !flip;

            visualSpriteRenderer.flipX = flip;
            return;
        }

        Vector3 scale = transform.localScale;

        if (faceRight)
            scale.x = -Mathf.Abs(scale.x);
        else
            scale.x = Mathf.Abs(scale.x);

        transform.localScale = scale;
    }

    private void SetMoving(bool isMoving)
    {
        if (animator == null || string.IsNullOrEmpty(isMovingBool)) return;
        animator.SetBool(isMovingBool, isMoving);
    }

    private void ApplyDefaultStats()
    {
        switch (enemyType)
        {
            case EnemyType.Tank:
                maxHP = 8;
                damage = 0;
                moveSpeed = 1.2f;
                detectRange = 10f;
                attackRange = 1.2f;
                attackCooldown = 1.4f;
                tankDealsDamage = false;
                break;

            case EnemyType.Duelist:
                maxHP = 3;
                damage = 1;
                moveSpeed = 2.8f;
                detectRange = 10f;
                attackRange = 1.15f;
                attackCooldown = 0.8f;
                break;

            case EnemyType.FastRanged:
                maxHP = 2;
                damage = 1;
                moveSpeed = 3.2f;
                detectRange = 12f;
                attackRange = 6f;
                attackCooldown = 1.2f;
                projectileSpeed = 9f;
                break;

            case EnemyType.Mage:
                maxHP = 2;
                damage = 2;
                moveSpeed = 1.2f;
                detectRange = 12f;
                attackRange = 7f;
                attackCooldown = 1.6f;
                projectileSpeed = 5.5f;
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.green;
        Collider2D col = GetComponent<Collider2D>();
        Vector2 checkPos;

        if (col != null)
        {
            checkPos = new Vector2(
                col.bounds.center.x,
                col.bounds.min.y - groundCheckExtraDistance
            );
        }
        else
        {
            checkPos = new Vector2(transform.position.x, transform.position.y - 0.5f);
        }

        Gizmos.DrawWireCube(checkPos, groundCheckSize);

        if (patrolWhenPlayerFar)
        {
            Vector2 center = Application.isPlaying ? homePosition : (Vector2)transform.position;

            Gizmos.color = Color.magenta;
            Vector3 left = new Vector3(center.x - patrolDistance, center.y, transform.position.z);
            Vector3 right = new Vector3(center.x + patrolDistance, center.y, transform.position.z);
            Gizmos.DrawLine(left, right);
            Gizmos.DrawWireSphere(left, 0.12f);
            Gizmos.DrawWireSphere(right, 0.12f);

            if (col != null)
            {
                float dir = Application.isPlaying ? patrolDirection : 1f;
                float x = col.bounds.center.x + dir * (col.bounds.extents.x + ledgeCheckForwardOffset);
                float y = col.bounds.min.y - groundCheckExtraDistance;

                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(new Vector2(x, y), ledgeCheckSize);
            }
        }

        if (enemyType == EnemyType.Tank)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, tankGuardRadius);
        }
    }

    public void KnockUpFromAttack4(Attack4LaunchData data)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb == null)
        {
            Debug.LogWarning("Enemy không có Rigidbody2D nên không thể bị hất tung.");
            return;
        }

        StopCoroutine(nameof(KnockUpRoutine));
        StartCoroutine(KnockUpRoutine(data));
    }

    private IEnumerator KnockUpRoutine(Attack4LaunchData data)
    {
        isKnockedUp = true;

        float direction = transform.position.x >= data.attackerPosition.x ? 1f : -1f;

        rb.linearVelocity = Vector2.zero;

        rb.AddForce(
            new Vector2(direction * data.sideForce, data.upForce),
            ForceMode2D.Impulse
        );

        yield return new WaitForSeconds(data.stunTime);

        isKnockedUp = false;
    }
}
