using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossGreenFlameAI : MonoBehaviour
{
    private enum BossState
    {
        Inactive,
        Spawning,
        Idle,
        Moving,
        Casting,
        PhaseBreak,
        Dead
    }

    [Header("References")]
    public Transform player;
    public Animator animator;
    public Rigidbody2D rb;

    [Header("Boss Health")]
    public int maxHP = 300;
    public int currentHP;

    [Header("Start")]
    public bool startOnAwake = false;
    public bool invincibleBeforeStart = true;
    public float spawnDuration = 1.5f;

    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float idealDistance = 6f;
    public float tooFarDistance = 8f;
    public float tooCloseDistance = 2.5f;
    public float repositionTime = 0.8f;

    [Header("Facing")]
    public bool isFacingRight = false;

    [Header("Animator State Names")]
    public string spawnStateName = "Boss_Spawn";
    public string idleStateName = "Boss_Idle";
    public string moveStateName = "Boss_Move";
    public string closeSkillStateName = "Boss_Closed_Skill";
    public string rangedSkillStateName = "Boss_Ranged_Skill";
    public string skill2StateName = "Boss_Skill_2";
    public string ultimateStateName = "Boss_Ultimate";
    public string phaseBreakStateName = "Boss_Hit";
    public string defeatStateName = "Boss_Defeat";

    [Header("Fire Points")]
    public Transform projectileSpawnPoint;

    [Header("Basic Fireball")]
    public GameObject basicFireballPrefab;
    public int basicFireballDamage = 1;
    public float basicFireballSpeed = 8f;
    public float basicCastDelay = 0.35f;
    public float basicRecovery = 0.45f;
    public float basicCooldown = 1.2f;

    [Header("Skill 1 - Sweep / Big Fireball")]
    public float closeSkillRange = 3.2f;
    public float skill1Cooldown = 4f;

    [Header("Sweep")]
    public GameObject sweepWarningPrefab;
    public GameObject sweepDamagePrefab;
    public int sweepDamage = 2;
    public float sweepOffsetX = 1.7f;
    public Vector2 sweepSize = new Vector2(3.2f, 2f);
    public float sweepWarningTime = 0.45f;
    public float sweepActiveTime = 0.25f;
    public float sweepRecovery = 0.6f;

    [Header("Big Fireball")]
    public GameObject bigFireballPrefab;
    public int bigFireballDamage = 2;
    public float bigFireballSpeed = 7f;
    public float bigCastDelay = 0.55f;
    public float bigRecovery = 0.75f;

    [Header("Skill 2 - Fire Pillar")]
    public GameObject pillarWarningPrefab;
    public GameObject pillarDamagePrefab;
    public int pillarDamage = 2;
    public int pillarCount = 4;
    public Vector2 pillarSize = new Vector2(1.2f, 4f);
    public float pillarCastDelay = 0.5f;
    public float pillarWarningTime = 0.75f;
    public float pillarActiveTime = 0.45f;
    public float pillarRecovery = 0.8f;
    public float pillarRandomXRange = 5f;
    public float skill2Cooldown = 6f;

    [Header("Ultimate")]
    public GameObject groundWarningPrefab;
    public GameObject groundWaveDamagePrefab;
    public GameObject platformWarningPrefab;
    public GameObject platformBurstDamagePrefab;

    public int ultimateDamage = 2;
    public int ultimateWaveCount = 4;
    public float ultimateChargeTime = 1f;
    public float ultimateWarningTime = 0.7f;
    public float ultimateWaveActiveTime = 0.35f;
    public float ultimateDelayBetweenWaves = 0.45f;
    public float ultimateRecovery = 1.2f;
    public float ultimateCooldown = 16f;

    [Header("Ultimate Ground Wave")]
    public Transform groundWaveCenter;
    public Vector2 groundWaveSize = new Vector2(18f, 1.5f);

    [Header("Ultimate Platform Burst")]
    public Vector2 platformBurstSize = new Vector2(3f, 2.5f);

    [Header("Ground / Platform Check")]
    public LayerMask groundAndPlatformLayer;
    public LayerMask platformLayer;
    public float groundRayDistance = 8f;
    public float fallbackGroundY = 0f;
    public float platformYThreshold = 1.5f;

    [Header("Phase Break")]
    public float phaseBreakDuration = 1.8f;
    public bool useUltimateImmediatelyInPhase3 = true;

    [Header("Phase 2")]
    public bool phase2AllowBasicFireball = true;

    [Header("Debug")]
    public bool debugLog = true;

    private BossState state = BossState.Inactive;

    private bool isInvincible;
    private bool phase2Started = false;
    private bool phase3Started = false;
    private bool mustUseUltimate = false;

    private float nextBasicTime = 0f;
    private float nextSkill1Time = 0f;
    private float nextSkill2Time = 0f;
    private float nextUltimateTime = 0f;

    private Coroutine currentRoutine;

    public bool IsDead => state == BossState.Dead;
    public bool IsInvincible => isInvincible;

    private void Awake()
    {
        currentHP = maxHP;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
                player = playerObj.transform;
        }

        isInvincible = invincibleBeforeStart;
    }

    private void Start()
    {
        if (startOnAwake)
        {
            BeginBossFight();
        }
    }

    public void BeginBossFight()
    {
        if (state != BossState.Inactive) return;
        StartNewRoutine(SpawnRoutine());
    }

    private void StartNewRoutine(IEnumerator routine)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(routine);
    }

    private IEnumerator SpawnRoutine()
    {
        state = BossState.Spawning;
        isInvincible = true;
        StopMovement();

        PlayState(spawnStateName);

        if (debugLog)
            Debug.Log("Boss xuất hiện.");

        yield return new WaitForSeconds(spawnDuration);

        state = BossState.Idle;
        isInvincible = false;

        PlayState(idleStateName);

        currentRoutine = StartCoroutine(CombatLoop());
    }

    private IEnumerator CombatLoop()
    {
        while (state != BossState.Dead)
        {
            if (player == null)
            {
                yield return null;
                continue;
            }

            if (state == BossState.PhaseBreak || state == BossState.Casting || state == BossState.Spawning)
            {
                yield return null;
                continue;
            }

            CheckFacingPlayer();

            if (NeedReposition())
            {
                yield return StartCoroutine(RepositionRoutine());
                continue;
            }

            if (phase3Started && mustUseUltimate && Time.time >= nextUltimateTime)
            {
                yield return StartCoroutine(CastUltimateRoutine());
                mustUseUltimate = false;
                continue;
            }

            if (phase3Started)
            {
                yield return StartCoroutine(ChoosePhase3Action());
            }
            else if (phase2Started)
            {
                yield return StartCoroutine(ChoosePhase2Action());
            }
            else
            {
                yield return StartCoroutine(ChoosePhase1Action());
            }

            yield return null;
        }
    }

    private IEnumerator ChoosePhase1Action()
    {
        if (Time.time >= nextSkill1Time)
        {
            yield return StartCoroutine(CastSkill1Routine());
            yield break;
        }

        if (Time.time >= nextBasicTime)
        {
            yield return StartCoroutine(CastBasicFireballRoutine());
            yield break;
        }

        PlayState(idleStateName);
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator ChoosePhase2Action()
    {
        if (Time.time >= nextSkill2Time)
        {
            yield return StartCoroutine(CastFirePillarRoutine());
            yield break;
        }

        if (phase2AllowBasicFireball && Time.time >= nextBasicTime)
        {
            yield return StartCoroutine(CastBasicFireballRoutine());
            yield break;
        }

        PlayState(idleStateName);
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator ChoosePhase3Action()
    {
        List<int> availableSkills = new List<int>();

        if (Time.time >= nextSkill1Time)
            availableSkills.Add(1);

        if (Time.time >= nextSkill2Time)
            availableSkills.Add(2);

        if (Time.time >= nextUltimateTime)
            availableSkills.Add(3);

        if (availableSkills.Count <= 0)
        {
            if (Time.time >= nextBasicTime)
                yield return StartCoroutine(CastBasicFireballRoutine());
            else
                yield return new WaitForSeconds(0.2f);

            yield break;
        }

        int selected = availableSkills[Random.Range(0, availableSkills.Count)];

        if (selected == 1)
        {
            yield return StartCoroutine(CastSkill1Routine());
        }
        else if (selected == 2)
        {
            yield return StartCoroutine(CastFirePillarRoutine());
        }
        else
        {
            yield return StartCoroutine(CastUltimateRoutine());
        }
    }

    private bool NeedReposition()
    {
        float distance = GetDistanceToPlayer();

        if (distance > tooFarDistance)
            return true;

        if (distance < tooCloseDistance && Time.time < nextSkill1Time)
            return true;

        return false;
    }

    private IEnumerator RepositionRoutine()
    {
        state = BossState.Moving;

        PlayState(moveStateName);

        float timer = 0f;

        while (timer < repositionTime)
        {
            if (player == null) break;

            float distance = GetDistanceToPlayer();
            float direction = 0f;

            if (distance > idealDistance)
            {
                direction = Mathf.Sign(player.position.x - transform.position.x);
            }
            else if (distance < tooCloseDistance)
            {
                direction = -Mathf.Sign(player.position.x - transform.position.x);
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            }
            else
            {
                transform.position += Vector3.right * direction * moveSpeed * Time.deltaTime;
            }

            CheckFacingPlayer();

            timer += Time.deltaTime;
            yield return null;
        }

        StopMovement();

        state = BossState.Idle;
        PlayState(idleStateName);
    }

    private IEnumerator CastBasicFireballRoutine()
    {
        state = BossState.Casting;
        StopMovement();
        CheckFacingPlayer();

        PlayState(rangedSkillStateName);

        yield return new WaitForSeconds(basicCastDelay);

        SpawnProjectile(basicFireballPrefab, basicFireballDamage, basicFireballSpeed);

        nextBasicTime = Time.time + basicCooldown;

        yield return new WaitForSeconds(basicRecovery);

        state = BossState.Idle;
        PlayState(idleStateName);
    }

    private IEnumerator CastSkill1Routine()
    {
        nextSkill1Time = Time.time + skill1Cooldown;

        if (GetDistanceToPlayer() <= closeSkillRange)
        {
            yield return StartCoroutine(CastSweepRoutine());
        }
        else
        {
            yield return StartCoroutine(CastBigFireballRoutine());
        }
    }

    private IEnumerator CastSweepRoutine()
    {
        state = BossState.Casting;
        StopMovement();
        CheckFacingPlayer();

        PlayState(closeSkillStateName);

        float facing = GetFacingSign();
        Vector3 center = transform.position + new Vector3(facing * sweepOffsetX, 0.8f, 0f);

        GameObject warning = SpawnVisual(sweepWarningPrefab, center, sweepSize);
        DestroySafe(warning, sweepWarningTime + sweepActiveTime + 0.2f);

        yield return new WaitForSeconds(sweepWarningTime);

        SpawnDamageZone(sweepDamagePrefab, center, sweepSize, sweepDamage, sweepActiveTime);

        yield return new WaitForSeconds(sweepActiveTime + sweepRecovery);

        state = BossState.Idle;
        PlayState(idleStateName);
    }

    private IEnumerator CastBigFireballRoutine()
    {
        state = BossState.Casting;
        StopMovement();
        CheckFacingPlayer();

        PlayState(rangedSkillStateName);

        yield return new WaitForSeconds(bigCastDelay);

        SpawnProjectile(bigFireballPrefab, bigFireballDamage, bigFireballSpeed);

        yield return new WaitForSeconds(bigRecovery);

        state = BossState.Idle;
        PlayState(idleStateName);
    }

    private IEnumerator CastFirePillarRoutine()
    {
        state = BossState.Casting;
        StopMovement();
        CheckFacingPlayer();

        PlayState(skill2StateName);

        nextSkill2Time = Time.time + skill2Cooldown;

        yield return new WaitForSeconds(pillarCastDelay);

        List<Vector3> positions = BuildPillarPositions();

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject warning = SpawnVisual(pillarWarningPrefab, positions[i], pillarSize);
            DestroySafe(warning, pillarWarningTime + pillarActiveTime + 0.2f);
        }

        yield return new WaitForSeconds(pillarWarningTime);

        for (int i = 0; i < positions.Count; i++)
        {
            SpawnDamageZone(pillarDamagePrefab, positions[i], pillarSize, pillarDamage, pillarActiveTime);
        }

        yield return new WaitForSeconds(pillarActiveTime + pillarRecovery);

        state = BossState.Idle;
        PlayState(idleStateName);
    }

    private IEnumerator CastUltimateRoutine()
    {
        state = BossState.Casting;
        StopMovement();
        CheckFacingPlayer();

        PlayState(ultimateStateName);

        nextUltimateTime = Time.time + ultimateCooldown;

        if (debugLog)
            Debug.Log("Boss dùng Ultimate.");

        yield return new WaitForSeconds(ultimateChargeTime);

        for (int i = 0; i < ultimateWaveCount; i++)
        {
            bool playerOnPlatform = IsPlayerOnPlatform();

            if (playerOnPlatform)
            {
                yield return StartCoroutine(UltimatePlatformWave());
            }
            else
            {
                yield return StartCoroutine(UltimateGroundWave());
            }

            yield return new WaitForSeconds(ultimateDelayBetweenWaves);
        }

        yield return new WaitForSeconds(ultimateRecovery);

        state = BossState.Idle;
        PlayState(idleStateName);
    }

    private IEnumerator UltimateGroundWave()
    {
        Vector3 center = groundWaveCenter != null
            ? groundWaveCenter.position
            : new Vector3(transform.position.x, fallbackGroundY + groundWaveSize.y * 0.5f, 0f);

        GameObject warning = SpawnVisual(groundWarningPrefab, center, groundWaveSize);
        DestroySafe(warning, ultimateWarningTime + ultimateWaveActiveTime + 0.2f);

        yield return new WaitForSeconds(ultimateWarningTime);

        SpawnDamageZone(groundWaveDamagePrefab, center, groundWaveSize, ultimateDamage, ultimateWaveActiveTime);

        yield return new WaitForSeconds(ultimateWaveActiveTime);
    }

    private IEnumerator UltimatePlatformWave()
    {
        Vector3 center = player != null ? player.position : transform.position;
        Vector2 size = platformBurstSize;

        Collider2D platformCollider;

        if (TryGetPlatformBelowPlayer(out platformCollider))
        {
            Bounds bounds = platformCollider.bounds;
            center = new Vector3(bounds.center.x, bounds.max.y + platformBurstSize.y * 0.5f, 0f);
            size = new Vector2(bounds.size.x, platformBurstSize.y);
        }

        GameObject warning = SpawnVisual(platformWarningPrefab, center, size);
        DestroySafe(warning, ultimateWarningTime + ultimateWaveActiveTime + 0.2f);

        yield return new WaitForSeconds(ultimateWarningTime);

        SpawnDamageZone(platformBurstDamagePrefab, center, size, ultimateDamage, ultimateWaveActiveTime);

        yield return new WaitForSeconds(ultimateWaveActiveTime);
    }

    private List<Vector3> BuildPillarPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        if (player == null)
            return positions;

        Vector3 playerGroundPos = GetFloorPointBelow(player.position);
        positions.Add(playerGroundPos);

        for (int i = 1; i < pillarCount; i++)
        {
            float randomX = player.position.x + Random.Range(-pillarRandomXRange, pillarRandomXRange);
            Vector3 source = new Vector3(randomX, player.position.y + 3f, 0f);
            Vector3 pos = GetFloorPointBelow(source);
            positions.Add(pos);
        }

        return positions;
    }

    private Vector3 GetFloorPointBelow(Vector3 source)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            source,
            Vector2.down,
            groundRayDistance,
            groundAndPlatformLayer
        );

        if (hit.collider != null)
        {
            return new Vector3(hit.point.x, hit.point.y + pillarSize.y * 0.5f, 0f);
        }

        return new Vector3(source.x, fallbackGroundY + pillarSize.y * 0.5f, 0f);
    }

    private bool IsPlayerOnPlatform()
    {
        Collider2D platformCollider;

        if (TryGetPlatformBelowPlayer(out platformCollider))
            return true;

        if (player != null && player.position.y >= platformYThreshold)
            return true;

        return false;
    }

    private bool TryGetPlatformBelowPlayer(out Collider2D platformCollider)
    {
        platformCollider = null;

        if (player == null) return false;

        RaycastHit2D hit = Physics2D.Raycast(
            player.position,
            Vector2.down,
            groundRayDistance,
            groundAndPlatformLayer
        );

        if (hit.collider == null) return false;

        if (IsInLayerMask(hit.collider.gameObject.layer, platformLayer))
        {
            platformCollider = hit.collider;
            return true;
        }

        return false;
    }

    private void SpawnProjectile(GameObject prefab, int damage, float speed)
    {
        if (prefab == null) return;

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;

        GameObject obj = Instantiate(
            prefab,
            spawnPoint.position,
            Quaternion.identity
        );

        BossProjectile projectile = obj.GetComponent<BossProjectile>();

        Vector2 direction;

        if (player != null)
        {
            direction = (player.position - spawnPoint.position).normalized;
        }
        else
        {
            direction = new Vector2(GetFacingSign(), 0f);
        }

        if (projectile != null)
        {
            projectile.Init(direction, damage, speed);
        }
    }

    private GameObject SpawnVisual(GameObject prefab, Vector3 position, Vector2 size)
    {
        if (prefab == null) return null;

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        ApplyBoxSize(obj, size);

        return obj;
    }

    private void SpawnDamageZone(GameObject prefab, Vector3 position, Vector2 size, int damage, float duration)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);

        ApplyBoxSize(obj, size);

        BossDamageZone damageZone = obj.GetComponent<BossDamageZone>();

        if (damageZone != null)
        {
            damageZone.SetData(damage, duration);
        }
        else
        {
            Destroy(obj, duration);
        }
    }

    private void ApplyBoxSize(GameObject obj, Vector2 size)
    {
        if (obj == null) return;

        BoxCollider2D box = obj.GetComponent<BoxCollider2D>();

        if (box != null)
        {
            box.size = size;
            box.isTrigger = true;
        }

        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.size = size;
        }
    }

    public void TakeDamage(int amount)
    {
        if (state == BossState.Dead) return;
        if (isInvincible) return;

        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        if (debugLog)
            Debug.Log("Boss HP = " + currentHP + "/" + maxHP);

        HitFlash();

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        CheckPhaseTransition();
    }

    private void CheckPhaseTransition()
    {
        if (!phase2Started && currentHP <= maxHP * 2 / 3)
        {
            phase2Started = true;
            StartNewRoutine(PhaseBreakRoutine(2));
            return;
        }

        if (!phase3Started && currentHP <= maxHP / 3)
        {
            phase3Started = true;
            mustUseUltimate = useUltimateImmediatelyInPhase3;
            StartNewRoutine(PhaseBreakRoutine(3));
        }
    }

    private IEnumerator PhaseBreakRoutine(int nextPhase)
    {
        state = BossState.PhaseBreak;
        isInvincible = true;
        StopMovement();

        PlayState(phaseBreakStateName);

        if (debugLog)
            Debug.Log("Boss chuyển Phase " + nextPhase);

        yield return new WaitForSeconds(phaseBreakDuration);

        state = BossState.Idle;
        isInvincible = false;

        PlayState(idleStateName);

        currentRoutine = StartCoroutine(CombatLoop());
    }

    private void Die()
    {
        if (state == BossState.Dead) return;

        state = BossState.Dead;
        isInvincible = true;

        StopAllCoroutines();
        StopMovement();

        PlayState(defeatStateName);

        if (debugLog)
            Debug.Log("Boss chết.");
    }

    private void HitFlash()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            StartCoroutine(FlashSprite(renderers[i]));
        }
    }

    private IEnumerator FlashSprite(SpriteRenderer sr)
    {
        if (sr == null) yield break;

        Color oldColor = sr.color;
        sr.color = Color.white;

        yield return new WaitForSeconds(0.06f);

        if (sr != null)
            sr.color = oldColor;
    }

    private float GetDistanceToPlayer()
    {
        if (player == null) return 999f;

        return Vector2.Distance(transform.position, player.position);
    }

    private void CheckFacingPlayer()
    {
        if (player == null) return;

        bool shouldFaceRight = player.position.x > transform.position.x;

        if (shouldFaceRight != isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private float GetFacingSign()
    {
        return isFacingRight ? 1f : -1f;
    }

    private void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void PlayState(string stateName)
    {
        if (animator == null) return;
        if (string.IsNullOrEmpty(stateName)) return;

        animator.Play(stateName, 0, 0f);
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void DestroySafe(GameObject obj, float delay)
    {
        if (obj != null)
            Destroy(obj, delay);
    }
}