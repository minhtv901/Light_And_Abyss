using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Spawning,
        Chasing,
        Attacking,
        HitStun,
        Dead
    }

    [Header("Enemy Type")]
    public EnemyType enemyType;

    [Header("Stats")]
    public int maxHP = 3;
    public int currentHP = 3;
    public int damage = 1;

    [Header("Runtime Tracking")]
    [Tooltip("Time when this enemy was spawned/enabled. Used by the combat spawn algorithm.")]
    public float spawnTime;

    [Tooltip("Final lifetime recorded when this enemy dies.")]
    public float lifeTime;

    [Tooltip("Total damage this enemy attempted to deal to the Player. Projectile damage can be made more accurate by forwarding hit results from EnemyProjectile.")]
    public int damageDealtToPlayer;

    [Header("Detection")]
    public float detectRange = 10f;

    [Tooltip("If enabled, melee enemies only chase the Player when both are on the same floor/platform surface.")]
    public bool requireSameSurfaceToChase = true;

    [Tooltip("If enabled, melee enemies only chase when the vertical gap is small enough.")]
    public bool requireSameLevelToChase = true;

    public float chaseVerticalTolerance = 1f;

    [Tooltip("If enabled, ranged enemies need a clear ray to attack.")]
    public bool requireLineOfSightToAttack = false;

    [Tooltip("Layers that block ranged line of sight, usually Ground + Wall. Do not include decorative gate/door triggers unless they should block arrows.")]
    public LayerMask lineOfSightBlockerLayer;

    [Header("Target Stability")]
    [Tooltip("Prevents rapid target valid/invalid flickering near platform edges, gates or LOS borders.")]
    public float targetDecisionStabilityTime = 0.18f;

    [Tooltip("How often the expensive target validity check is refreshed.")]
    public float targetRecheckInterval = 0.06f;

    [Tooltip("Small horizontal dead zone for facing so enemies do not flip rapidly when the Player is nearly aligned.")]
    public float facingDeadZone = 0.18f;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float attackRange = 1.2f;

    [Tooltip("Vertical tolerance allowed when attacking. Melee should usually be around 0.7 - 1.2. Ranged can be much higher.")]
    public float verticalAttackTolerance = 1f;

    [Tooltip("If enabled, the enemy patrols when the Player is not a valid target.")]
    public bool patrolWhenPlayerFar = true;

    [Tooltip("If enabled, the enemy keeps moving freely instead of freezing when no valid target exists.")]
    public bool roamWhenNoValidTarget = true;

    public float patrolDistance = 3f;
    public float patrolSpeed = 1.2f;

    [Tooltip("If enabled, ground enemies will turn around before walking off a ledge.")]
    public bool preventFallingOffLedge = true;

    public float ledgeCheckForwardOffset = 0.35f;
    public Vector2 ledgeCheckSize = new Vector2(0.25f, 0.18f);

    [Header("Ground / Surface Check")]
    public bool requireGroundedToMove = true;
    public bool requireGroundedToAttack = true;
    public LayerMask groundLayer;
    public Vector2 groundCheckSize = new Vector2(0.75f, 0.18f);
    public float groundCheckExtraDistance = 0.08f;

    [Tooltip("Ray length used to detect the floor/platform directly below this enemy or the Player.")]
    public float surfaceCheckDistance = 1.0f;

    [Header("Attack Timing")]
    public float attackCooldown = 1f;
    public float meleeWindUpTime = 0.25f;
    public float meleeActiveTime = 0.08f;
    public float meleeRecoveryTime = 0.25f;

    [Header("Ranged / Mage Projectile")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 7f;
    public float projectileLifeTime = 4f;
    public float projectileFireDelay = 0.35f;
    public float projectileRecoveryTime = 0.25f;

    [Tooltip("Aim error in degrees. Use 0 for perfect aim.")]
    public float rangedAimErrorDegrees = 5f;

    [Tooltip("Ranged enemies retreat when the Player is closer than this distance.")]
    public float rangedMinDistance = 2.4f;

    [Tooltip("Extra gap before leaving retreat mode. Prevents ranged enemies from flickering between retreat/attack near the distance border.")]
    public float rangedRetreatExitBuffer = 0.45f;

    public bool rangedRetreatWhenTooClose = true;
    public float rangedRetreatSpeedMultiplier = 0.9f;

    [Header("Tank Guard")]
    public bool tankDealsDamage = false;
    public bool tankCanGuard = true;
    public float tankGuardRadius = 3f;

    [Range(0.05f, 1f)]
    public float tankSelfDamageMultiplier = 0.45f;

    [Range(0.05f, 1f)]
    public float tankAllyDamageMultiplier = 0.6f;

    public float tankGuardCooldown = 2.5f;
    public float tankGuardDuration = 0.6f;

    [Header("Tank Protect Role")]
    [Tooltip("Tank prioritizes standing in front of Bow/Mage allies when they are on the same surface.")]
    public bool tankProtectRangedAllies = true;

    [Tooltip("If no valid ranged ally is found, Tank patrols/roams instead of freezing.")]
    public bool tankRoamsWhenNoAlly = true;

    public float tankProtectSearchRadius = 8f;
    public float tankFrontOffsetFromAlly = 1.2f;
    public float tankProtectPositionTolerance = 0.15f;
    public float tankProtectMoveSpeedMultiplier = 1.6f;
    public bool tankAutoGuardWhenInPosition = true;

    [Header("Tank Guard Damage")]
    public int minDamageWhileGuarding = 0;

    [Header("Animation Timing")]
    public float spawnDuration = 1.2f;

    [Tooltip("Short hit pause/stun when hit. 0.08 - 0.1 feels heavy without making enemies look frozen.")]
    public float hitStunDuration = 0.1f;

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
    private bool isRetreating;

    private bool stableTargetValid;
    private bool pendingTargetValue;
    private bool hasPendingTargetChange;
    private float pendingTargetChangeStartTime;
    private float nextTargetRecheckTime;
    private bool cachedRawTargetValid;

    private float nextAttackTime;
    private float nextGuardTime;
    private float guardUntilTime;

    private Coroutine attackCoroutine;
    private Coroutine hitStunCoroutine;
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
        {
            ApplyDefaultStats();
        }

        currentHP = maxHP;
    }

    private void OnEnable()
    {
        StopAllCoroutines();

        attackCoroutine = null;
        hitStunCoroutine = null;
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
        isRetreating = false;
        lastGuardProvider = null;

        stableTargetValid = false;
        pendingTargetValue = false;
        hasPendingTargetChange = false;
        pendingTargetChangeStartTime = 0f;
        nextTargetRecheckTime = 0f;
        cachedRawTargetValid = false;

        currentHP = maxHP;

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
        }

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
        {
            FindPlayer();
        }
    }

    private IEnumerator SpawnRoutine()
    {
        StopMoveX();
        SetMoving(false);

        if (animator != null && !string.IsNullOrEmpty(spawnTrigger))
        {
            animator.SetTrigger(spawnTrigger);
        }

        yield return new WaitForSeconds(spawnDuration);

        if (state != EnemyState.Dead)
        {
            state = EnemyState.Chasing;
        }
    }

    private void Update()
    {
        if (state == EnemyState.Dead ||
            state == EnemyState.Spawning ||
            state == EnemyState.Attacking ||
            state == EnemyState.HitStun)
        {
            return;
        }

        if (isKnockedUp)
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        if (player == null && autoFindPlayer)
        {
            FindPlayer();
        }

        if (requireGroundedToMove && !IsGrounded())
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        if (player == null)
        {
            PatrolOrRoam();
            return;
        }

        if (enemyType == EnemyType.Tank && tankProtectRangedAllies && !tankDealsDamage)
        {
            TankProtectUpdate();
            return;
        }

        if (!HasStableValidTarget())
        {
            PatrolOrRoam();
            return;
        }

        FacePlayer();

        if (IsRangedEnemy() && rangedRetreatWhenTooClose)
        {
            float horizontalGap = GetHorizontalGapToPlayer();

            if (!isRetreating && horizontalGap < rangedMinDistance)
            {
                isRetreating = true;
            }
            else if (isRetreating && horizontalGap > rangedMinDistance + rangedRetreatExitBuffer)
            {
                isRetreating = false;
            }

            if (isRetreating)
            {
                RetreatFromPlayer();
                return;
            }
        }

        if (IsPlayerInAttackRange())
        {
            StopMoveX();
            SetMoving(false);
            TryAttack();
            return;
        }

        state = EnemyState.Chasing;
        MoveTowardPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj == null)
        {
            player = null;
            playerCollider = null;
            return;
        }

        player = playerObj.transform;

        // Prefer the Player root collider. Do not use attack/guard/skill trigger colliders.
        Collider2D rootCollider = playerObj.GetComponent<Collider2D>();

        if (rootCollider != null && !rootCollider.isTrigger)
        {
            playerCollider = rootCollider;
            return;
        }

        Collider2D[] colliders = playerObj.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null) continue;
            if (colliders[i].isTrigger) continue;

            playerCollider = colliders[i];
            return;
        }

        playerCollider = playerObj.GetComponentInChildren<Collider2D>();
    }

    private bool HasStableValidTarget()
    {
        if (Time.time >= nextTargetRecheckTime)
        {
            cachedRawTargetValid = IsPlayerValidTargetRaw();
            nextTargetRecheckTime = Time.time + targetRecheckInterval;
        }

        if (cachedRawTargetValid == stableTargetValid)
        {
            hasPendingTargetChange = false;
            return stableTargetValid;
        }

        if (!hasPendingTargetChange || pendingTargetValue != cachedRawTargetValid)
        {
            hasPendingTargetChange = true;
            pendingTargetValue = cachedRawTargetValid;
            pendingTargetChangeStartTime = Time.time;
            return stableTargetValid;
        }

        if (Time.time - pendingTargetChangeStartTime >= targetDecisionStabilityTime)
        {
            stableTargetValid = pendingTargetValue;
            hasPendingTargetChange = false;
        }

        return stableTargetValid;
    }

    private bool IsPlayerValidTargetRaw()
    {
        if (player == null) return false;

        if (!IsPlayerInsideDetectRange())
        {
            return false;
        }

        bool meleeLike = IsMeleeLikeEnemy();

        if (sideScrollerXOnly && meleeLike)
        {
            if (requireSameLevelToChase && GetVerticalGapToPlayer() > chaseVerticalTolerance)
            {
                return false;
            }

            if (requireSameSurfaceToChase && !IsPlayerOnSameSurface())
            {
                return false;
            }
        }

        if (IsRangedEnemy() && requireLineOfSightToAttack && !HasLineOfSightToPlayer())
        {
            return false;
        }

        return true;
    }

    private bool IsPlayerInsideDetectRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) <= detectRange;
    }

    private bool IsPlayerOnSameSurface()
    {
        if (player == null) return false;

        Collider2D mySurface = GetSurfaceBelow(transform, mainCollider);
        Collider2D playerSurface = GetSurfaceBelow(player, playerCollider);

        if (mySurface != null && playerSurface != null)
        {
            return mySurface == playerSurface;
        }

        return GetVerticalGapToPlayer() <= chaseVerticalTolerance;
    }

    private bool IsEnemyOnSameSurface(EnemyAI other)
    {
        if (other == null) return false;

        Collider2D mySurface = GetSurfaceBelow(transform, mainCollider);
        Collider2D otherSurface = GetSurfaceBelow(other.transform, other.mainCollider);

        if (mySurface != null && otherSurface != null)
        {
            return mySurface == otherSurface;
        }

        return Mathf.Abs(transform.position.y - other.transform.position.y) <= chaseVerticalTolerance;
    }

    private Collider2D GetSurfaceBelow(Transform targetTransform, Collider2D targetCollider)
    {
        if (groundLayer.value == 0 || targetTransform == null)
        {
            return null;
        }

        Vector2 origin;

        if (targetCollider != null)
        {
            origin = new Vector2(
                targetCollider.bounds.center.x,
                targetCollider.bounds.min.y + 0.05f
            );
        }
        else
        {
            origin = targetTransform.position;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            surfaceCheckDistance,
            groundLayer
        );

        return hit.collider;
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;
        if (lineOfSightBlockerLayer.value == 0) return true;

        Vector2 origin = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        Vector2 target = playerCollider != null
            ? playerCollider.bounds.center
            : (Vector2)player.position;

        Vector2 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f) return true;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction.normalized,
            distance,
            lineOfSightBlockerLayer
        );

        return hit.collider == null;
    }

    private bool IsMeleeLikeEnemy()
    {
        return enemyType == EnemyType.Duelist || enemyType == EnemyType.Tank;
    }

    private bool IsRangedEnemy()
    {
        return enemyType == EnemyType.FastRanged || enemyType == EnemyType.Mage;
    }

    private void PatrolOrRoam()
    {
        if (patrolWhenPlayerFar || roamWhenNoValidTarget)
        {
            PatrolPlatform();
            return;
        }

        StopMoveX();
        SetMoving(false);
    }

    private void TankProtectUpdate()
    {
        EnemyAI ally = FindRangedAllyToProtect();

        if (ally == null)
        {
            if (tankRoamsWhenNoAlly)
            {
                PatrolOrRoam();
            }
            else
            {
                StopMoveX();
                SetMoving(false);
            }

            return;
        }

        float sideToPlayer = player != null && player.position.x >= ally.transform.position.x ? 1f : -1f;
        float targetX = ally.transform.position.x + sideToPlayer * tankFrontOffsetFromAlly;
        float diffX = targetX - transform.position.x;

        if (Mathf.Abs(diffX) <= tankProtectPositionTolerance)
        {
            StopMoveX();
            SetMoving(false);

            if (player != null && IsPlayerInsideDetectRange())
            {
                FacePlayer();
            }

            if (tankAutoGuardWhenInPosition && Time.time >= nextGuardTime)
            {
                float distanceToPlayer = player != null
                    ? Mathf.Abs(player.position.x - transform.position.x)
                    : Mathf.Infinity;

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
            PatrolOrRoam();
            return;
        }

        rb.linearVelocity = new Vector2(directionX * moveSpeed * tankProtectMoveSpeedMultiplier, rb.linearVelocity.y);
        FaceMoveDirection(directionX);
        SetMoving(true);
    }

    private EnemyAI FindRangedAllyToProtect()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, tankProtectSearchRadius);

        EnemyAI bestAlly = null;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyAI ally = hits[i].GetComponentInParent<EnemyAI>();

            if (ally == null) continue;
            if (ally == this) continue;
            if (ally.IsDead) continue;
            if (!ally.IsRangedEnemy()) continue;

            // Tank only protects ranged allies on the same floor/platform.
            if (!IsEnemyOnSameSurface(ally)) continue;

            float distanceToTank = Vector2.Distance(transform.position, ally.transform.position);
            float distanceToPlayer = player != null
                ? Mathf.Abs(player.position.x - ally.transform.position.x)
                : 0f;

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
            PatrolOrRoam();
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

        FaceMoveDirection(directionX);
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

        rb.linearVelocity = new Vector2(directionX * moveSpeed * rangedRetreatSpeedMultiplier, rb.linearVelocity.y);
        FacePlayer();
        SetMoving(true);
    }

    private void PatrolPlatform()
    {
        if (rb == null)
        {
            SetMoving(false);
            return;
        }

        if (requireGroundedToMove && !IsGrounded())
        {
            StopMoveX();
            SetMoving(false);
            return;
        }

        float offsetFromHome = transform.position.x - homePosition.x;

        // Deterministic patrol direction prevents rapid direction flipping at patrol borders.
        if (offsetFromHome >= patrolDistance)
        {
            patrolDirection = -1;
        }
        else if (offsetFromHome <= -patrolDistance)
        {
            patrolDirection = 1;
        }

        if (preventFallingOffLedge && !HasGroundAhead(patrolDirection))
        {
            patrolDirection *= -1;

            if (preventFallingOffLedge && !HasGroundAhead(patrolDirection))
            {
                StopMoveX();
                SetMoving(false);
                return;
            }
        }

        FaceMoveDirection(patrolDirection);
        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);
        SetMoving(true);
    }

    private bool HasGroundAhead(float directionX)
    {
        if (groundLayer.value == 0) return true;
        if (mainCollider == null) return true;

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
        {
            StopCoroutine(attackCoroutine);
        }

        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        state = EnemyState.Attacking;

        StopMoveX();
        SetMoving(false);
        FacePlayer();
        attackFacingLocked = true;

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
        {
            animator.SetTrigger(attackTrigger);
        }

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
                    yield return new WaitForSeconds(meleeActiveTime + meleeRecoveryTime);
                }
                break;
        }

        attackFacingLocked = false;
        attackCoroutine = null;

        if (state != EnemyState.Dead && state != EnemyState.HitStun)
        {
            state = EnemyState.Chasing;
        }
    }

    private void MeleeDamagePlayer()
    {
        if (player == null) return;
        if (!IsPlayerInAttackRange()) return;

        PlayerHealth hp = player.GetComponentInParent<PlayerHealth>();

        if (hp != null)
        {
            hp.TakeDamage(damage);
        }
        else
        {
            player.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }

        RegisterDamageAttempt(damage);
        Debug.Log($"{enemyType} attempted melee damage: {damage}");
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null || player == null)
        {
            Debug.LogWarning($"{name}: Missing projectilePrefab or Player.");
            return;
        }

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        GameObject projectileObj = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);

        Vector2 target = playerCollider != null
            ? playerCollider.bounds.center
            : (Vector2)player.position;

        Vector2 direction = (target - (Vector2)spawnPoint.position).normalized;
        direction = ApplyAimError(direction);

        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();

        if (projectile != null)
        {
            projectile.Init(direction, projectileSpeed, damage, projectileLifeTime);
            RegisterDamageAttempt(damage);
        }
        else
        {
            Debug.LogWarning($"{projectileObj.name}: Missing EnemyProjectile.cs.");
        }
    }

    private Vector2 ApplyAimError(Vector2 direction)
    {
        if (rangedAimErrorDegrees <= 0f) return direction;

        float angle = UnityEngine.Random.Range(-rangedAimErrorDegrees, rangedAimErrorDegrees);
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        return (rotation * direction).normalized;
    }

    public void RegisterDamageAttempt(int amount)
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

        Debug.Log($"{enemyType} hit: raw={amount}, final={finalDamage}, HP={currentHP}/{maxHP}, guarded={usedGuard}");

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

        if (usedGuard)
        {
            EnemyAI guardUser = lastGuardProvider != null ? lastGuardProvider : this;

            if (guardUser.animator != null && !string.IsNullOrEmpty(guardUser.guardTrigger))
            {
                guardUser.animator.SetTrigger(guardUser.guardTrigger);
            }

            guardUser.StartGuardStun();
            return;
        }

        if (animator != null && !string.IsNullOrEmpty(hitTrigger))
        {
            animator.SetTrigger(hitTrigger);
        }

        StartHitStun();
    }

    private int CalculateFinalDamage(int amount, out bool usedGuard)
    {
        usedGuard = false;
        lastGuardProvider = null;

        float multiplier = 1f;
        int guardMinDamage = minDamageWhileGuarding;

        if (enemyType == EnemyType.Tank && tankCanGuard)
        {
            if (IsGuarding)
            {
                usedGuard = true;
                lastGuardProvider = this;
                multiplier *= tankSelfDamageMultiplier;
                guardMinDamage = minDamageWhileGuarding;
            }
            else if (Time.time >= nextGuardTime)
            {
                usedGuard = true;
                lastGuardProvider = this;
                multiplier *= tankSelfDamageMultiplier;
                guardMinDamage = minDamageWhileGuarding;
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
                guardMinDamage = guardTank.minDamageWhileGuarding;

                if (!guardTank.IsGuarding)
                {
                    guardTank.StartTankGuard();
                }
            }
        }

        if (usedGuard)
        {
            int reducedDamage = Mathf.RoundToInt(amount * multiplier);
            return Mathf.Max(guardMinDamage, reducedDamage);
        }

        return Mathf.Max(1, amount);
    }

    private EnemyAI FindNearbyAvailableTank()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, tankGuardRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyAI tank = hits[i].GetComponentInParent<EnemyAI>();

            if (tank == null) continue;
            if (tank == this) continue;
            if (tank.enemyType != EnemyType.Tank) continue;
            if (tank.IsDead) continue;
            if (!tank.tankCanGuard) continue;

            // Tanks only guard allies on the same floor/platform.
            if (!IsEnemyOnSameSurface(tank)) continue;

            if (tank.IsGuarding)
            {
                return tank;
            }

            if (Time.time >= tank.nextGuardTime)
            {
                return tank;
            }
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
        {
            animator.SetTrigger(guardTrigger);
        }

        Debug.Log("Tank started Guard.");
    }

    private void StartHitStun()
    {
        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
        }

        hitStunCoroutine = StartCoroutine(HitStunRoutine());
    }

    private void StartGuardStun()
    {
        if (guardStunCoroutine != null)
        {
            StopCoroutine(guardStunCoroutine);
        }

        guardStunCoroutine = StartCoroutine(GuardStunRoutine());
    }

    private IEnumerator HitStunRoutine()
    {
        state = EnemyState.HitStun;
        StopMoveX();
        SetMoving(false);

        yield return new WaitForSeconds(hitStunDuration);

        hitStunCoroutine = null;

        if (state != EnemyState.Dead)
        {
            state = EnemyState.Chasing;
        }
    }

    private IEnumerator GuardStunRoutine()
    {
        state = EnemyState.HitStun;
        StopMoveX();
        SetMoving(false);

        while (state != EnemyState.Dead && Time.time < guardUntilTime)
        {
            yield return null;
        }

        guardStunCoroutine = null;

        if (state != EnemyState.Dead)
        {
            state = EnemyState.Chasing;
        }
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

        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
            hitStunCoroutine = null;
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
        {
            mainCollider.enabled = false;
        }

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
        {
            animator.SetTrigger(deathTrigger);
        }

        Debug.Log($"{enemyType} died. LifeTime={lifeTime:F2}s | DamageAttempted={damageDealtToPlayer}");

        OnEnemyDied?.Invoke(this);

        if (destroyAfterDeath)
        {
            Destroy(gameObject, deathDuration);
        }
    }

    private bool IsPlayerInAttackRange()
    {
        if (player == null) return false;
        if (requireGroundedToAttack && !IsGrounded()) return false;
        if (!HasStableValidTarget()) return false;

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

        if (Mathf.Abs(direction) < facingDeadZone) return;

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
            {
                visualSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (visualSpriteRenderer == null) return;

            bool flip = faceRight;

            if (invertSpriteFacing)
            {
                flip = !flip;
            }

            visualSpriteRenderer.flipX = flip;
            return;
        }

        Vector3 scale = transform.localScale;

        if (faceRight)
        {
            scale.x = -Mathf.Abs(scale.x);
        }
        else
        {
            scale.x = Mathf.Abs(scale.x);
        }

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
                tankRoamsWhenNoAlly = true;
                tankProtectSearchRadius = 8f;
                tankFrontOffsetFromAlly = 1.2f;
                tankProtectMoveSpeedMultiplier = 1.6f;
                tankGuardDuration = 1f;
                tankGuardCooldown = 2f;
                tankSelfDamageMultiplier = 0.35f;
                tankAllyDamageMultiplier = 0.6f;
                minDamageWhileGuarding = 0;
                hitStunDuration = 0.1f;
                break;

            case EnemyType.Duelist:
                maxHP = 3;
                damage = 1;
                moveSpeed = 2.8f;
                detectRange = 8f;
                attackRange = 1.15f;
                attackCooldown = 1.05f;
                requireSameSurfaceToChase = true;
                requireSameLevelToChase = true;
                chaseVerticalTolerance = 0.9f;
                verticalAttackTolerance = 0.9f;
                meleeWindUpTime = 0.28f;
                meleeRecoveryTime = 0.28f;
                hitStunDuration = 0.1f;
                break;

            case EnemyType.FastRanged:
                maxHP = 2;
                damage = 1;
                moveSpeed = 3.0f;
                detectRange = 12f;
                attackRange = 6f;
                attackCooldown = 1.45f;
                requireSameSurfaceToChase = false;
                requireSameLevelToChase = false;
                verticalAttackTolerance = 6f;
                projectileSpeed = 7.5f;
                projectileFireDelay = 0.35f;
                rangedAimErrorDegrees = 5f;
                rangedMinDistance = 2.4f;
                rangedRetreatWhenTooClose = true;
                hitStunDuration = 0.1f;
                break;

            case EnemyType.Mage:
                maxHP = 2;
                damage = 2;
                moveSpeed = 1.2f;
                detectRange = 12f;
                attackRange = 7f;
                attackCooldown = 1.9f;
                requireSameSurfaceToChase = false;
                requireSameLevelToChase = false;
                verticalAttackTolerance = 6f;
                projectileSpeed = 4.8f;
                projectileFireDelay = 0.55f;
                rangedAimErrorDegrees = 8f;
                rangedMinDistance = 3f;
                rangedRetreatWhenTooClose = true;
                hitStunDuration = 0.1f;
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
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb == null)
        {
            Debug.LogWarning("Enemy has no Rigidbody2D, cannot apply knock-up.");
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