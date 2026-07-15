using System;
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

    [Header("Runtime Tracking")]
    [Tooltip("Thời điểm quái bắt đầu spawn/enable. Dùng cho thuật toán spawn phase sau.")]
    public float spawnTime;

    [Tooltip("Thời gian sống cuối cùng sau khi quái chết.")]
    public float lifeTime;

    [Tooltip("Tổng damage quái gây/cố gây lên Player. Projectile sẽ cộng qua RegisterDamageDealt().")]
    public int damageDealtToPlayer;

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

    [Header("Attack Base")]
    public float attackCooldown = 1f;

    [Header("Duelist / Melee Fairness")]
    [Tooltip("Thời gian vung tay trước khi damage thật sự xảy ra. Tăng lên để Player dễ né hơn.")]
    public float meleeWindUpTime = 0.28f;

    [Tooltip("Giữ trạng thái attack thêm một đoạn ngắn sau frame gây damage.")]
    public float meleeActiveTime = 0.08f;

    [Tooltip("Độ trễ sau khi chém xong, giúp Duelist không bám damage quá dính.")]
    public float meleeRecoveryTime = 0.28f;

    [Tooltip("Không xoay mặt liên tục trong lúc đang attack. Nên bật để attack công bằng hơn.")]
    public bool lockFacingDuringAttack = true;

    [Header("Ranged / Mage Projectile Fairness")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 7f;
    public float projectileLifeTime = 4f;

    [Tooltip("Delay từ lúc animation attack bắt đầu đến lúc bắn đạn. Đây là telegraph để Player né/guard.")]
    public float projectileFireDelay = 0.35f;

    [Tooltip("Recovery sau khi bắn, giúp ranged/mage không spam quá khó né.")]
    public float projectileRecoveryTime = 0.2f;

    [Tooltip("Sai lệch aim theo độ. 0 = bắn chuẩn tuyệt đối. 5-10 giúp dễ né hơn.")]
    public float rangedAimErrorDegrees = 6f;

    [Tooltip("Nếu Player áp sát dưới khoảng này, ranged/mage sẽ lùi thay vì bắn ngay.")]
    public float rangedMinDistance = 2.2f;

    public bool rangedRetreatWhenTooClose = true;
    public float rangedRetreatSpeedMultiplier = 0.9f;

    [Header("Tank Guard")]
    public bool tankDealsDamage = false;
    public bool tankCanGuard = true;
    public float tankGuardRadius = 3f;
    [Range(0.05f, 1f)] public float tankSelfDamageMultiplier = 0.45f;
    [Range(0.05f, 1f)] public float tankAllyDamageMultiplier = 0.6f;
    public float tankGuardCooldown = 2.5f;
    public float tankGuardDuration = 0.6f;

    [Header("Tank Protect Role")]
    [Tooltip("Bật để Tank ưu tiên chạy lên trước Mage/FastRanged thay vì lao vào Player.")]
    public bool tankProtectRangedAllies = true;

    [Tooltip("Bán kính Tank tìm Mage/FastRanged để bảo vệ.")]
    public float tankProtectSearchRadius = 8f;

    [Tooltip("Tank sẽ đứng lệch về phía Player so với quái đánh xa bao xa.")]
    public float tankFrontOffsetFromAlly = 1.2f;

    [Tooltip("Khoảng sai số vị trí khi Tank đã đứng đúng chỗ bảo kê.")]
    public float tankProtectPositionTolerance = 0.15f;

    [Tooltip("Tank chạy nhanh hơn khi đi lên chắn cho quái đánh xa.")]
    public float tankProtectMoveSpeedMultiplier = 1.6f;

    [Tooltip("Khi Tank đã đứng trước quái đánh xa và Player ở gần, Tank tự bật Guard.")]
    public bool tankAutoGuardWhenInPosition = true;

    [Header("Tank Guard Damage")]
    [Tooltip("Damage tối thiểu khi đang Guard. Để 0 nếu muốn hit 1 damage có thể bị chặn về 0.")]
    public int minDamageWhileGuarding = 0;

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
    private Collider2D playerCollider;

    private EnemyState state = EnemyState.Spawning;
    private bool isKnockedUp;
    private bool attackFacingLocked;

    private float nextAttackTime;
    private float nextGuardTime;
    private float guardUntilTime;
    private Coroutine attackCoroutine;
    private Coroutine guardStunCoroutine;

    private Vector2 homePosition;
    private int patrolDirection = 1;
    private EnemyAI lastGuardProvider;

    public bool IsDead => state == EnemyState.Dead;
    public bool IsGuarding => enemyType == EnemyType.Tank && Time.time < guardUntilTime;
    public float CurrentLifeTime => IsDead ? lifeTime : Time.time - spawnTime;

    public event Action<EnemyAI> OnEnemyDied;

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
        attackCoroutine = null;
        guardStunCoroutine = null;

        homePosition = transform.position;
        patrolDirection = 1;

        spawnTime = Time.time;
        lifeTime = 0f;
        damageDealtToPlayer = 0;
        guardUntilTime = 0f;
        nextGuardTime = 0f;
        nextAttackTime = 0f;
        isKnockedUp = false;
        attackFacingLocked = false;
        lastGuardProvider = null;
        currentHP = maxHP;

        if (mainCollider != null)
            mainCollider.enabled = true;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = Mathf.Max(1f, rb.gravityScale);
            rb.linearVelocity = Vector2.zero;
        }

        state = EnemyState.Spawning;
        StartCoroutine(SpawnRoutine());
    }

    private void Start()
    {
        if (autoFindPlayer)
            FindPlayer();
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
        if (state == EnemyState.Dead || state == EnemyState.Spawning || state == EnemyState.HitStun || state == EnemyState.Attacking)
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

        if (enemyType == EnemyType.Tank && tankProtectRangedAllies && !tankDealsDamage)
        {
            TankProtectUpdate();
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

        if (IsRangedEnemy() && rangedRetreatWhenTooClose && horizontalGap < rangedMinDistance)
        {
            RetreatFromPlayer();
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

    private bool IsRangedEnemy()
    {
        return enemyType == EnemyType.FastRanged || enemyType == EnemyType.Mage;
    }

    private void TankProtectUpdate()
    {
        if (player == null)
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        EnemyAI ally = FindRangedAllyToProtect();

        if (ally == null)
        {
            FacePlayer();
            StopMoveX();
            SetMoving(false);
            return;
        }

        float sideToPlayer = player.position.x >= ally.transform.position.x ? 1f : -1f;
        float targetX = ally.transform.position.x + sideToPlayer * tankFrontOffsetFromAlly;
        float diffX = targetX - transform.position.x;

        FacePlayer();

        if (Mathf.Abs(diffX) <= tankProtectPositionTolerance)
        {
            StopMoveX();
            SetMoving(false);

            if (tankAutoGuardWhenInPosition && Time.time >= nextGuardTime)
            {
                float distanceToPlayer = Mathf.Abs(player.position.x - transform.position.x);

                if (distanceToPlayer <= tankGuardRadius)
                {
                    StartTankGuard();
                    StartGuardStun();
                }
            }

            return;
        }

        float directionX = Mathf.Sign(diffX);

        if (preventFallingOffLedge && !HasGroundAhead(directionX))
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        float protectSpeed = moveSpeed * tankProtectMoveSpeedMultiplier;
        rb.linearVelocity = new Vector2(directionX * protectSpeed, rb.linearVelocity.y);
        SetMoving(true);
    }

    private EnemyAI FindRangedAllyToProtect()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, tankProtectSearchRadius);

        EnemyAI bestAlly = null;
        float bestScore = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            EnemyAI ally = hit.GetComponentInParent<EnemyAI>();

            if (ally == null) continue;
            if (ally == this) continue;
            if (ally.IsDead) continue;
            if (!ally.IsRangedEnemy()) continue;

            float distanceToTank = Vector2.Distance(transform.position, ally.transform.position);
            float distanceToPlayer = player != null ? Mathf.Abs(player.position.x - ally.transform.position.x) : 0f;
            float score = distanceToTank + distanceToPlayer * 0.25f;

            if (score < bestScore)
            {
                bestScore = score;
                bestAlly = ally;
            }
        }

        return bestAlly;
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

    private void RetreatFromPlayer()
    {
        if (player == null) return;

        float directionX = player.position.x > transform.position.x ? -1f : 1f;

        if (preventFallingOffLedge && !HasGroundAhead(directionX))
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        FacePlayer();
        rb.linearVelocity = new Vector2(directionX * moveSpeed * rangedRetreatSpeedMultiplier, rb.linearVelocity.y);
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
            patrolDirection *= -1;

        if (preventFallingOffLedge && !HasGroundAhead(patrolDirection))
            patrolDirection *= -1;

        FaceMoveDirection(patrolDirection);

        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);
        SetMoving(true);
    }

    private bool HasGroundAhead(float directionX)
    {
        if (groundLayer.value == 0) return true;
        if (mainCollider == null) return true;

        float x = mainCollider.bounds.center.x + directionX * (mainCollider.bounds.extents.x + ledgeCheckForwardOffset);
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
        StopMoveX();
        SetMoving(false);

        if (!lockFacingDuringAttack)
            FacePlayer();
        else
        {
            FacePlayer();
            attackFacingLocked = true;
        }

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        switch (enemyType)
        {
            case EnemyType.Duelist:
                yield return new WaitForSeconds(meleeWindUpTime);
                MeleeDamagePlayer();
                yield return new WaitForSeconds(meleeActiveTime + meleeRecoveryTime);
                break;

            case EnemyType.FastRanged:
            case EnemyType.Mage:
                yield return new WaitForSeconds(projectileFireDelay);
                ShootProjectile();
                yield return new WaitForSeconds(projectileRecoveryTime);
                break;

            case EnemyType.Tank:
                if (tankDealsDamage)
                {
                    yield return new WaitForSeconds(meleeWindUpTime);
                    MeleeDamagePlayer();
                    yield return new WaitForSeconds(meleeRecoveryTime);
                }
                break;
        }

        attackFacingLocked = false;
        attackCoroutine = null;

        if (state != EnemyState.Dead && state != EnemyState.HitStun)
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

        RegisterDamageDealt(damage);
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
        direction = ApplyAimError(direction);

        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();

        if (projectile != null)
            projectile.Init(direction, projectileSpeed, damage, projectileLifeTime, this);
        else
            Debug.LogWarning($"{projectileObj.name}: Thiếu EnemyProjectile.cs trên prefab đạn.");
    }

    private Vector2 ApplyAimError(Vector2 direction)
    {
        if (rangedAimErrorDegrees <= 0f) return direction;

        float angle = UnityEngine.Random.Range(-rangedAimErrorDegrees, rangedAimErrorDegrees);
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        return (rotation * direction).normalized;
    }

    public void RegisterDamageDealt(int amount)
    {
        if (amount <= 0) return;
        damageDealtToPlayer += amount;
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
            attackFacingLocked = false;
        }

        if (usedGuard && lastGuardProvider == this && enemyType == EnemyType.Tank)
        {
            if (animator != null && !string.IsNullOrEmpty(guardTrigger))
                animator.SetTrigger(guardTrigger);

            StartGuardStun();
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
        lastGuardProvider = null;
        float multiplier = 1f;

        if (enemyType == EnemyType.Tank && tankCanGuard)
        {
            if (IsGuarding)
            {
                usedGuard = true;
                lastGuardProvider = this;
                multiplier *= tankSelfDamageMultiplier;
            }
            else if (Time.time >= nextGuardTime)
            {
                usedGuard = true;
                lastGuardProvider = this;
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
                lastGuardProvider = guardTank;
                multiplier *= guardTank.tankAllyDamageMultiplier;

                if (!guardTank.IsGuarding)
                {
                    guardTank.StartTankGuard();
                    guardTank.StartGuardStun();
                }
            }
        }

        if (usedGuard)
        {
            int reducedDamage = Mathf.RoundToInt(amount * multiplier);
            return Mathf.Max(minDamageWhileGuarding, reducedDamage);
        }

        return Mathf.Max(1, amount);
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

            if (tank.IsGuarding)
                return tank;

            if (Time.time >= tank.nextGuardTime)
                return tank;
        }

        return null;
    }

    private void StartTankGuard()
    {
        if (enemyType != EnemyType.Tank) return;
        if (IsGuarding) return;

        guardUntilTime = Time.time + tankGuardDuration;
        nextGuardTime = guardUntilTime + tankGuardCooldown;

        if (animator != null && !string.IsNullOrEmpty(guardTrigger))
            animator.SetTrigger(guardTrigger);

        Debug.Log("Tank bật trạng thái Guard.");
    }

    private void StartGuardStun()
    {
        if (guardStunCoroutine != null)
            StopCoroutine(guardStunCoroutine);

        guardStunCoroutine = StartCoroutine(GuardStunRoutine());
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

        while (state != EnemyState.Dead && Time.time < guardUntilTime)
            yield return null;

        guardStunCoroutine = null;

        if (state != EnemyState.Dead)
            state = EnemyState.Chasing;
    }

    private void Die()
    {
        state = EnemyState.Dead;
        lifeTime = Time.time - spawnTime;

        StopMoveX();
        SetMoving(false);

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (guardStunCoroutine != null)
        {
            StopCoroutine(guardStunCoroutine);
            guardStunCoroutine = null;
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

        Debug.Log($"{enemyType} đã chết. LifeTime = {lifeTime:F2}s | DamageDealt = {damageDealtToPlayer}");

        OnEnemyDied?.Invoke(this);

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
        if (groundLayer.value == 0) return true;

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
        if (attackFacingLocked) return;

        float direction = player.position.x - transform.position.x;
        if (Mathf.Abs(direction) < 0.05f) return;

        ApplyFacing(direction > 0f);
    }

    private void FaceMoveDirection(float directionX)
    {
        if (!flipToPlayer) return;
        if (attackFacingLocked) return;
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
                moveSpeed = 1.8f;
                detectRange = 10f;
                attackRange = 1.2f;
                attackCooldown = 1.4f;
                tankDealsDamage = false;
                tankCanGuard = true;
                tankProtectRangedAllies = true;
                tankProtectSearchRadius = 8f;
                tankFrontOffsetFromAlly = 1.2f;
                tankProtectMoveSpeedMultiplier = 1.6f;
                tankGuardDuration = 1f;
                tankGuardCooldown = 2f;
                tankSelfDamageMultiplier = 0.35f;
                tankAllyDamageMultiplier = 0.6f;
                minDamageWhileGuarding = 0;
                break;

            case EnemyType.Duelist:
                maxHP = 3;
                damage = 1;
                moveSpeed = 2.8f;
                detectRange = 10f;
                attackRange = 1.05f;
                attackCooldown = 1.05f;
                meleeWindUpTime = 0.32f;
                meleeActiveTime = 0.08f;
                meleeRecoveryTime = 0.32f;
                lockFacingDuringAttack = true;
                break;

            case EnemyType.FastRanged:
                maxHP = 2;
                damage = 1;
                moveSpeed = 3.0f;
                detectRange = 12f;
                attackRange = 6f;
                attackCooldown = 1.45f;
                projectileSpeed = 7.5f;
                projectileFireDelay = 0.35f;
                projectileRecoveryTime = 0.2f;
                rangedAimErrorDegrees = 5f;
                rangedMinDistance = 2.4f;
                rangedRetreatWhenTooClose = true;
                break;

            case EnemyType.Mage:
                maxHP = 2;
                damage = 2;
                moveSpeed = 1.2f;
                detectRange = 12f;
                attackRange = 7f;
                attackCooldown = 1.9f;
                projectileSpeed = 4.8f;
                projectileFireDelay = 0.55f;
                projectileRecoveryTime = 0.35f;
                rangedAimErrorDegrees = 8f;
                rangedMinDistance = 3f;
                rangedRetreatWhenTooClose = true;
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

        if (enemyType == EnemyType.FastRanged || enemyType == EnemyType.Mage)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, rangedMinDistance);
        }

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

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, tankProtectSearchRadius);
        }
    }

    public void KnockUpFromAttack4(Attack4LaunchData data)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

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
        rb.AddForce(new Vector2(direction * data.sideForce, data.upForce), ForceMode2D.Impulse);

        yield return new WaitForSeconds(data.stunTime);

        isKnockedUp = false;
    }
}
