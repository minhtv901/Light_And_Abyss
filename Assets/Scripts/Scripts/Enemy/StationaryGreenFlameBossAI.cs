using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StationaryGreenFlameBossAI : MonoBehaviour
{
    private enum BossState
    {
        Inactive,
        Spawning,
        Idle,
        Casting,
        PhaseBreak,
        Dead
    }

    public enum ProtectedCastDefenseMode
    {
        None,
        DamageReduction,
        Invincible
    }

    [Header("References")]
    public Transform player;
    public Collider2D playerCollider;
    public Animator animator;
    public Rigidbody2D rb;
    public Collider2D bossCollider;
    public Transform projectileSpawnPoint;

    [Header("Health")]
    public int maxHP = 600;
    public int currentHP = 600;
    public bool invincibleBeforeStart = true;

    [Header("Skill 2 / Ultimate Cast Defense")]
    [Tooltip("Defense used only while Skill 2 or Ultimate is actively casting.")]
    public ProtectedCastDefenseMode protectedCastDefenseMode = ProtectedCastDefenseMode.Invincible;

    [Tooltip("0.70 means the boss receives 70% less damage and takes only 30% of the incoming damage.")]
    [Range(0f, 1f)] public float protectedCastDamageReduction = 0.70f;

    [Tooltip("Prints a Console message whenever damage is blocked or reduced during Skill 2 / Ultimate.")]
    public bool debugProtectedCastDefense = true;

    [Header("Pillar Spawn Points")]
    public List<Transform> pillarSpawnPoints = new List<Transform>();
    public bool usePillarSpawnPoints = true;
    public bool randomizePillarSpawnPoints = true;

    [Header("Start")]
    public bool startOnAwake = false;
    public bool autoFindPlayer = true;
    public string playerTag = "Player";

    [Header("Facing")]
    public bool flipToPlayer = true;
    public bool isFacingRight = false;
    public bool useSpriteRendererFlip = false;
    public bool invertSpriteFacing = false;
    public SpriteRenderer visualSpriteRenderer;

    [Header("Animator State Names")]
    public string spawnStateName = "Boss_Spawn";
    public string idleStateName = "Boss_Idle";

    [Header("Forced Animation Durations")]
    [Tooltip("Keep the Animator component Speed at 1 in the Inspector. During a cast, this script temporarily calculates the playback speed so even a 2-frame clip lasts for the target duration.")]
    public bool forceAnimationDuration = true;

    [Tooltip("Automatically extends Skill 2 and Ultimate animation durations when their warning/damage sequence needs more time than the configured target duration.")]
    public bool autoExtendDurationForSkillEffects = true;

    [Header("Fixed Sprite Playback FPS")]
    [Tooltip("When enabled, cast animations are never allowed to run faster than the FPS values below. The script estimates the real sprite-frame count from AnimationClip.length and AnimationClip.frameRate.")]
    public bool limitAnimationBySpriteFPS = true;

    [Tooltip("Recommended for pixel-art clips. The Animator is paused during casts and the script displays each sprite frame manually, preventing a short 10-12 frame clip from flashing by too quickly.")]
    public bool manuallyStepCastFrames = true;

    [Tooltip("Example: a 12-frame clip at 8 FPS lasts about 1.5 seconds. Lower this value to make the animation slower.")]
    [Range(1f, 30f)] public float spawnPlaybackFPS = 8f;
    [Range(1f, 30f)] public float basicPlaybackFPS = 8f;
    [Range(1f, 30f)] public float closeSkillPlaybackFPS = 8f;
    [Range(1f, 30f)] public float rangedSkillPlaybackFPS = 8f;
    [Range(1f, 30f)] public float skill2PlaybackFPS = 7f;
    [Range(1f, 30f)] public float ultimatePlaybackFPS = 7f;
    [Range(1f, 30f)] public float defeatPlaybackFPS = 8f;

    [Min(0.05f)] public float spawnTargetDuration = 1.5f;
    [Min(0.05f)] public float basicTargetDuration = 0.9f;
    [Min(0.05f)] public float closeSkillTargetDuration = 1.1f;
    [Min(0.05f)] public float rangedSkillTargetDuration = 1.2f;
    [Min(0.05f)] public float skill2TargetDuration = 2.0f;
    [Min(0.05f)] public float ultimateTargetDuration = 3.5f;
    [Min(0.05f)] public float defeatTargetDuration = 2.2f;

    [Tooltip("Normal/basic attack animation. In your Animator this is Boss_Hit.")]
    public string basicAttackStateName = "Boss_Hit";

    public string rangedSkillStateName = "Boss_Ranged_Skill";
    public string closeSkillStateName = "Boss_Closed_Skill";
    public string firePillarStateName = "Boss_Skill_2";
    public string ultimateStateName = "Boss_Ultimate";

    [Tooltip("Used only for the short invincible phase break. If you do not have a separate phase-break clip, keep this as Boss_Idle.")]
    public string phaseBreakStateName = "Boss_Idle";

    public string defeatStateName = "Boss_Defeat";

    [Header("Animation Timing Stabilizer")]
    [Tooltip("If enabled, the script reads the real active AnimationClip length to calculate playback speed. Keep this enabled unless you intentionally want to use only the fallback lengths.")]
    public bool useClipLengthTiming = true;

    [Tooltip("Fallback if the animation clip name cannot be found.")]
    public float spawnFallbackDuration = 1.0f;
    public float basicFallbackDuration = 0.45f;
    public float closeSkillFallbackDuration = 0.7f;
    public float rangedSkillFallbackDuration = 0.7f;
    public float skill2FallbackDuration = 1.4f;
    public float ultimateFallbackDuration = 2.6f;
    public float defeatFallbackDuration = 1.5f;

    [Range(0.05f, 0.95f)] public float basicFireNormalizedTime = 0.55f;
    [Range(0.05f, 0.95f)] public float bigFireNormalizedTime = 0.55f;
    [Range(0.05f, 0.95f)] public float sweepHitNormalizedTime = 0.55f;
    [Range(0.05f, 0.95f)] public float pillarWarningNormalizedTime = 0.25f;
    [Range(0.05f, 0.95f)] public float ultimateWaveStartNormalizedTime = 0.35f;

    [Header("Phase Settings")]
    [Range(0.1f, 0.95f)] public float phase2HpRatio = 0.66f;
    [Range(0.05f, 0.8f)] public float phase3HpRatio = 0.33f;
    public float phaseBreakDuration = 1.8f;
    public bool useUltimateImmediatelyInPhase3 = true;

    [Header("Idle / Combat Decision")]
    public float decisionDelay = 0.15f;
    public float closeSkillRange = 3.2f;

    [Header("Basic Attack - Boss_Hit")]
    public GameObject basicFireballPrefab;
    public int basicFireballDamage = 1;
    public float basicFireballSpeed = 8f;
    public float basicCooldown = 1.35f;

    [Header("Skill 1 Cooldown")]
    public float skill1Cooldown = 4.5f;

    [Header("Skill 1A - Sweep When Player Is Close")]
    public GameObject sweepWarningPrefab;
    public GameObject sweepDamagePrefab;
    public int sweepDamage = 2;
    public float sweepOffsetX = 1.8f;
    public float sweepOffsetY = 0.8f;
    public Vector2 sweepSize = new Vector2(3.5f, 2.0f);
    public float sweepActiveTime = 0.25f;

    [Header("Skill 1B - Big Fireball When Player Is Far")]
    public GameObject bigFireballPrefab;
    public int bigFireballDamage = 2;
    public float bigFireballSpeed = 7f;

    [Header("Skill 2 - Fire Pillars")]
    public GameObject pillarWarningPrefab;
    public GameObject pillarDamagePrefab;
    public int pillarDamage = 2;
    public int pillarCount = 4;
    public Vector2 pillarSize = new Vector2(1.2f, 4f);
    public float pillarWarningTime = 0.75f;
    public float pillarActiveTime = 0.45f;
    public float pillarRandomXRange = 5f;
    public float skill2Cooldown = 6.5f;

    [Header("Ultimate")]
    public GameObject groundWarningPrefab;
    public GameObject groundWaveDamagePrefab;
    public GameObject platformWarningPrefab;
    public GameObject platformBurstDamagePrefab;
    public int ultimateDamage = 2;
    public int ultimateWaveCount = 4;
    public float ultimateWarningTime = 0.7f;
    public float ultimateWaveActiveTime = 0.35f;
    public float ultimateDelayBetweenWaves = 0.45f;
    public float ultimateCooldown = 18f;

    [Header("Ultimate Ground Wave")]
    public Transform groundWaveCenter;
    public Vector2 groundWaveSize = new Vector2(18f, 1.5f);
    public float fallbackGroundY = 0f;

    [Header("Ultimate Platform Burst")]
    public Vector2 platformBurstSize = new Vector2(3f, 2.5f);
    public LayerMask platformLayer;
    public float platformYThreshold = 1.5f;

    [Header("Ground / Platform Detection")]
    public LayerMask groundAndPlatformLayer;
    public float groundRayDistance = 10f;

    [Header("Hit Feedback")]
    public bool useHitFlash = true;
    public float hitFlashDuration = 0.06f;
    public GameObject hitParticlePrefab;

    [Header("Debug")]
    public bool debugLog = true;

    private BossState state = BossState.Inactive;
    private bool isInvincible;
    private bool phase2Started;
    private bool phase3Started;
    private bool mustUseUltimate;
    private bool isCastingProtectedSkill;

    private float nextBasicTime;
    private float nextSkill1Time;
    private float nextSkill2Time;
    private float nextUltimateTime;

    private Coroutine mainRoutine;
    private Coroutine animationPlaybackRoutine;

    public bool IsDead => state == BossState.Dead;
    public bool IsInvincible => isInvincible ||
        (isCastingProtectedSkill && protectedCastDefenseMode == ProtectedCastDefenseMode.Invincible);
    public bool IsCastingProtectedSkill => isCastingProtectedSkill;
    public int CurrentPhase { get; private set; } = 1;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (bossCollider == null) bossCollider = GetComponent<Collider2D>();

        currentHP = maxHP;
        isInvincible = invincibleBeforeStart;
        SetupStationaryRigidbody();
    }

    private void Start()
    {
        if (autoFindPlayer) FindPlayer();
        if (startOnAwake) BeginBossFight();
        else PlayIdleState(true);
    }

    private void SetupStationaryRigidbody()
    {
        if (rb == null) return;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void BeginBossFight()
    {
        if (state != BossState.Inactive) return;
        StartMainRoutine(SpawnRoutine());
    }

    private void StartMainRoutine(IEnumerator routine)
    {
        if (mainRoutine != null) StopCoroutine(mainRoutine);
        mainRoutine = StartCoroutine(routine);
    }

    private IEnumerator SpawnRoutine()
    {
        state = BossState.Spawning;
        isInvincible = true;
        EndProtectedCast();
        StopPhysics();

        float spawnDuration = PlayStateWithDuration(
            spawnStateName,
            spawnTargetDuration,
            spawnFallbackDuration
        );

        if (debugLog) Debug.Log("Green Flame Boss: Spawn.");

        yield return new WaitForSeconds(spawnDuration);

        state = BossState.Idle;
        isInvincible = false;
        PlayIdleState(true);

        StartMainRoutine(CombatLoop());
    }

    private IEnumerator CombatLoop()
    {
        while (state != BossState.Dead)
        {
            if (player == null && autoFindPlayer) FindPlayer();

            if (state == BossState.Casting || state == BossState.PhaseBreak || state == BossState.Spawning)
            {
                yield return null;
                continue;
            }

            StopPhysics();
            FacePlayer();

            if (CurrentPhase == 1)
            {
                yield return ChoosePhase1Action();
            }
            else if (CurrentPhase == 2)
            {
                yield return ChoosePhase2Action();
            }
            else
            {
                yield return ChoosePhase3Action();
            }

            yield return new WaitForSeconds(decisionDelay);
        }
    }

    private float PlayStateWithDuration(string stateName, float targetDuration, float fallbackDuration)
    {
        float sourceClipLength = Mathf.Max(0.01f, fallbackDuration);
        AnimationClip activeClip = null;

        if (animator != null && !string.IsNullOrEmpty(stateName))
        {
            // Animator Speed can remain 1 in the Inspector. The script changes it only
            // while this state is playing, then resets it when returning to Idle.
            animator.speed = 1f;
            animator.Play(stateName, 0, 0f);
            animator.Update(0f);

            activeClip = GetCurrentAnimatorClip();

            if (useClipLengthTiming && activeClip != null)
                sourceClipLength = Mathf.Max(0.01f, activeClip.length);
            else
                sourceClipLength = Mathf.Max(0.01f, GetClipLength(stateName, fallbackDuration));
        }

        float finalDuration = forceAnimationDuration && targetDuration > 0.01f
            ? targetDuration
            : sourceClipLength;

        int estimatedSpriteFrameCount = 0;
        float desiredPlaybackFPS = GetPlaybackFPSForState(stateName);
        float frameBasedDuration = 0f;

        if (limitAnimationBySpriteFPS && activeClip != null && desiredPlaybackFPS > 0.01f)
        {
            // Unity clip.length normally ends on the last keyframe, so +1 gives a much
            // better estimate for sprite clips: 12 keys at 60 Samples => 12 frames.
            estimatedSpriteFrameCount = Mathf.Max(
                1,
                Mathf.RoundToInt(activeClip.length * activeClip.frameRate) + 1
            );

            frameBasedDuration = estimatedSpriteFrameCount / desiredPlaybackFPS;

            // Never let the configured Target Duration make the sprite animation faster
            // than the desired FPS. Target Duration can still make it slower.
            finalDuration = Mathf.Max(finalDuration, frameBasedDuration);
        }

        finalDuration = Mathf.Max(0.01f, finalDuration);

        if (animator == null || string.IsNullOrEmpty(stateName))
            return finalDuration;

        float playbackSpeed = 1f;

        if (forceAnimationDuration && sourceClipLength > 0.01f)
            playbackSpeed = Mathf.Clamp(sourceClipLength / finalDuration, 0.001f, 100f);

        StopManualAnimationPlayback();

        if (manuallyStepCastFrames)
        {
            int manualFrameCount = estimatedSpriteFrameCount;

            if (manualFrameCount <= 0)
            {
                manualFrameCount = Mathf.Max(
                    1,
                    Mathf.RoundToInt(finalDuration * Mathf.Max(1f, desiredPlaybackFPS))
                );
            }

            animator.speed = 0f;
            animationPlaybackRoutine = StartCoroutine(
                PlayStateFrameByFrame(stateName, manualFrameCount, finalDuration)
            );
        }
        else
        {
            animator.speed = playbackSpeed;
        }

        if (debugLog)
        {
            string clipName = activeClip != null ? activeClip.name : "NOT FOUND";
            float nativeFPS = activeClip != null ? activeClip.frameRate : 0f;

            Debug.Log(
                $"Boss animation '{stateName}' | Clip: {clipName} | " +
                $"Frames: {estimatedSpriteFrameCount} | Native FPS: {nativeFPS:0.##} | " +
                $"Desired FPS: {desiredPlaybackFPS:0.##} | Duration: {finalDuration:0.###}s | " +
                $"Mode: {(manuallyStepCastFrames ? "MANUAL FRAMES" : "ANIMATOR SPEED")} | " +
                $"Animator runtime speed: {(manuallyStepCastFrames ? 0f : playbackSpeed):0.###}"
            );
        }

        return finalDuration;
    }

    private IEnumerator PlayStateFrameByFrame(string stateName, int frameCount, float totalDuration)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            animationPlaybackRoutine = null;
            yield break;
        }

        frameCount = Mathf.Max(1, frameCount);
        totalDuration = Mathf.Max(0.01f, totalDuration);

        float secondsPerFrame = totalDuration / frameCount;
        animator.speed = 0f;

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            if (animator == null || state == BossState.Dead && stateName != defeatStateName)
                break;

            float normalizedTime = frameCount <= 1
                ? 0f
                : frameIndex / (float)(frameCount - 1);

            // 0.999 prevents a looping clip from wrapping from the last frame back to frame 1.
            normalizedTime = Mathf.Min(normalizedTime, 0.999f);

            animator.Play(stateName, 0, normalizedTime);
            animator.Update(0f);

            yield return new WaitForSeconds(secondsPerFrame);
        }

        animationPlaybackRoutine = null;
    }

    private void StopManualAnimationPlayback()
    {
        if (animationPlaybackRoutine == null) return;

        StopCoroutine(animationPlaybackRoutine);
        animationPlaybackRoutine = null;
    }

    private float GetPlaybackFPSForState(string stateName)
    {
        if (stateName == spawnStateName) return spawnPlaybackFPS;
        if (stateName == basicAttackStateName) return basicPlaybackFPS;
        if (stateName == closeSkillStateName) return closeSkillPlaybackFPS;
        if (stateName == rangedSkillStateName) return rangedSkillPlaybackFPS;
        if (stateName == firePillarStateName) return skill2PlaybackFPS;
        if (stateName == ultimateStateName) return ultimatePlaybackFPS;
        if (stateName == defeatStateName) return defeatPlaybackFPS;

        return basicPlaybackFPS;
    }

    private void PlayStateAtNormalSpeed(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        StopManualAnimationPlayback();
        animator.speed = 1f;
        animator.Play(stateName, 0, 0f);
        animator.Update(0f);
    }

    private void PlayIdleState(bool restart)
    {
        if (animator == null || string.IsNullOrEmpty(idleStateName)) return;

        StopManualAnimationPlayback();
        animator.speed = 1f;

        if (!restart && animator.GetCurrentAnimatorStateInfo(0).IsName(idleStateName))
            return;

        animator.Play(idleStateName, 0, 0f);
        animator.Update(0f);
    }

    private float GetMinimumDurationForTimedEffects(float normalizedStartTime, float effectDurationAfterStart)
    {
        float remainingNormalizedTime = Mathf.Max(0.01f, 1f - normalizedStartTime);
        return Mathf.Max(0.01f, effectDurationAfterStart / remainingNormalizedTime);
    }

    // Phase 1: basic attack + skill 1.
    private IEnumerator ChoosePhase1Action()
    {
        if (Time.time >= nextSkill1Time)
        {
            yield return CastSkill1Routine();
            yield break;
        }

        if (Time.time >= nextBasicTime)
        {
            yield return CastBasicFireballRoutine();
            yield break;
        }

        PlayIdleState(false);
        yield return null;
    }

    // Phase 2: skill 2 + basic attack.
    private IEnumerator ChoosePhase2Action()
    {
        if (Time.time >= nextSkill2Time)
        {
            yield return CastFirePillarRoutine();
            yield break;
        }

        if (Time.time >= nextBasicTime)
        {
            yield return CastBasicFireballRoutine();
            yield break;
        }

        PlayIdleState(false);
        yield return null;
    }

    // Phase 3: ultimate + skill 1 + basic attack. Skill 2 is intentionally not used here.
    private IEnumerator ChoosePhase3Action()
    {
        if (mustUseUltimate && Time.time >= nextUltimateTime)
        {
            mustUseUltimate = false;
            yield return CastUltimateRoutine();
            yield break;
        }

        List<int> availableSkills = new List<int>();

        if (Time.time >= nextSkill1Time) availableSkills.Add(1);
        if (Time.time >= nextUltimateTime) availableSkills.Add(3);

        if (availableSkills.Count > 0)
        {
            int selectedSkill = availableSkills[Random.Range(0, availableSkills.Count)];

            if (selectedSkill == 1) yield return CastSkill1Routine();
            else yield return CastUltimateRoutine();

            yield break;
        }

        if (Time.time >= nextBasicTime)
        {
            yield return CastBasicFireballRoutine();
        }
    }

    private IEnumerator CastBasicFireballRoutine()
    {
        state = BossState.Casting;
        StopPhysics();
        FacePlayer();

        float castDuration = PlayStateWithDuration(
            basicAttackStateName,
            basicTargetDuration,
            basicFallbackDuration
        );
        float fireTime = Mathf.Clamp(castDuration * basicFireNormalizedTime, 0.01f, castDuration);

        yield return new WaitForSeconds(fireTime);

        SpawnProjectile(basicFireballPrefab, basicFireballDamage, basicFireballSpeed);
        nextBasicTime = Time.time + basicCooldown;

        yield return new WaitForSeconds(Mathf.Max(0f, castDuration - fireTime));

        ReturnToIdle();
    }

    private IEnumerator CastSkill1Routine()
    {
        nextSkill1Time = Time.time + skill1Cooldown;

        if (GetDistanceToPlayer() <= closeSkillRange)
            yield return CastSweepRoutine();
        else
            yield return CastBigFireballRoutine();
    }

    private IEnumerator CastSweepRoutine()
    {
        state = BossState.Casting;
        StopPhysics();
        FacePlayer();

        float castDuration = PlayStateWithDuration(
            closeSkillStateName,
            closeSkillTargetDuration,
            closeSkillFallbackDuration
        );
        float hitTime = Mathf.Clamp(castDuration * sweepHitNormalizedTime, 0.01f, castDuration);

        float facing = GetFacingSign();
        Vector3 center = transform.position + new Vector3(facing * sweepOffsetX, sweepOffsetY, 0f);

        GameObject warning = SpawnAreaVisual(sweepWarningPrefab, center, sweepSize);
        DestroySafe(warning, castDuration + 0.2f);

        yield return new WaitForSeconds(hitTime);

        SpawnDamageZone(sweepDamagePrefab, center, sweepSize, sweepDamage, sweepActiveTime);

        yield return new WaitForSeconds(Mathf.Max(0f, castDuration - hitTime));

        ReturnToIdle();
    }

    private IEnumerator CastBigFireballRoutine()
    {
        state = BossState.Casting;
        StopPhysics();
        FacePlayer();

        float castDuration = PlayStateWithDuration(
            rangedSkillStateName,
            rangedSkillTargetDuration,
            rangedSkillFallbackDuration
        );
        float fireTime = Mathf.Clamp(castDuration * bigFireNormalizedTime, 0.01f, castDuration);

        yield return new WaitForSeconds(fireTime);

        SpawnProjectile(bigFireballPrefab, bigFireballDamage, bigFireballSpeed);

        yield return new WaitForSeconds(Mathf.Max(0f, castDuration - fireTime));

        ReturnToIdle();
    }

    private IEnumerator CastFirePillarRoutine()
    {
        state = BossState.Casting;
        BeginProtectedCast("Skill 2");
        StopPhysics();
        FacePlayer();

        nextSkill2Time = Time.time + skill2Cooldown;

        float requestedDuration = skill2TargetDuration;

        if (autoExtendDurationForSkillEffects)
        {
            float minimumDuration = GetMinimumDurationForTimedEffects(
                pillarWarningNormalizedTime,
                pillarWarningTime + pillarActiveTime
            );
            requestedDuration = Mathf.Max(requestedDuration, minimumDuration);
        }

        float castDuration = PlayStateWithDuration(
            firePillarStateName,
            requestedDuration,
            skill2FallbackDuration
        );
        float warningStartTime = Mathf.Clamp(castDuration * pillarWarningNormalizedTime, 0.01f, castDuration);
        float elapsed = 0f;

        yield return new WaitForSeconds(warningStartTime);
        elapsed += warningStartTime;

        List<Vector3> positions = BuildPillarPositions();

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject warning = SpawnAreaVisual(pillarWarningPrefab, positions[i], pillarSize);
            DestroySafe(warning, pillarWarningTime + pillarActiveTime + 0.2f);
        }

        yield return new WaitForSeconds(pillarWarningTime);
        elapsed += pillarWarningTime;

        for (int i = 0; i < positions.Count; i++)
        {
            SpawnDamageZone(pillarDamagePrefab, positions[i], pillarSize, pillarDamage, pillarActiveTime);
        }

        yield return new WaitForSeconds(pillarActiveTime);
        elapsed += pillarActiveTime;

        yield return new WaitForSeconds(Mathf.Max(0f, castDuration - elapsed));

        EndProtectedCast();
        ReturnToIdle();
    }

    private IEnumerator CastUltimateRoutine()
    {
        state = BossState.Casting;
        BeginProtectedCast("Ultimate");
        StopPhysics();
        FacePlayer();

        nextUltimateTime = Time.time + ultimateCooldown;

        if (debugLog) Debug.Log("Green Flame Boss: Ultimate.");

        int waveCount = Mathf.Max(0, ultimateWaveCount);
        float delaysBetweenWaves = Mathf.Max(0, waveCount - 1) * ultimateDelayBetweenWaves;
        float effectDurationAfterStart =
            waveCount * (ultimateWarningTime + ultimateWaveActiveTime) +
            delaysBetweenWaves;

        float requestedDuration = ultimateTargetDuration;

        if (autoExtendDurationForSkillEffects && waveCount > 0)
        {
            float minimumDuration = GetMinimumDurationForTimedEffects(
                ultimateWaveStartNormalizedTime,
                effectDurationAfterStart
            );
            requestedDuration = Mathf.Max(requestedDuration, minimumDuration);
        }

        float castDuration = PlayStateWithDuration(
            ultimateStateName,
            requestedDuration,
            ultimateFallbackDuration
        );
        float waveStartTime = Mathf.Clamp(castDuration * ultimateWaveStartNormalizedTime, 0.01f, castDuration);
        float elapsed = 0f;

        yield return new WaitForSeconds(waveStartTime);
        elapsed += waveStartTime;

        for (int i = 0; i < waveCount; i++)
        {
            if (IsPlayerOnPlatform())
                yield return UltimatePlatformWave();
            else
                yield return UltimateGroundWave();

            elapsed += ultimateWarningTime + ultimateWaveActiveTime;

            if (i < waveCount - 1)
            {
                yield return new WaitForSeconds(ultimateDelayBetweenWaves);
                elapsed += ultimateDelayBetweenWaves;
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0f, castDuration - elapsed));

        EndProtectedCast();
        ReturnToIdle();
    }

    private IEnumerator UltimateGroundWave()
    {
        Vector3 center = groundWaveCenter != null
            ? groundWaveCenter.position
            : new Vector3(transform.position.x, fallbackGroundY + groundWaveSize.y * 0.5f, 0f);

        GameObject warning = SpawnAreaVisual(groundWarningPrefab, center, groundWaveSize);
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

        GameObject warning = SpawnAreaVisual(platformWarningPrefab, center, size);
        DestroySafe(warning, ultimateWarningTime + ultimateWaveActiveTime + 0.2f);

        yield return new WaitForSeconds(ultimateWarningTime);
        SpawnDamageZone(platformBurstDamagePrefab, center, size, ultimateDamage, ultimateWaveActiveTime);
        yield return new WaitForSeconds(ultimateWaveActiveTime);
    }

    private List<Vector3> BuildPillarPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        if (usePillarSpawnPoints && pillarSpawnPoints != null && pillarSpawnPoints.Count > 0)
        {
            List<Transform> validPoints = new List<Transform>();

            for (int i = 0; i < pillarSpawnPoints.Count; i++)
            {
                if (pillarSpawnPoints[i] != null && pillarSpawnPoints[i].gameObject.activeInHierarchy)
                {
                    validPoints.Add(pillarSpawnPoints[i]);
                }
            }

            if (validPoints.Count > 0)
            {
                int count = Mathf.Min(pillarCount, validPoints.Count);

                for (int i = 0; i < count; i++)
                {
                    int selectedIndex = randomizePillarSpawnPoints
                        ? Random.Range(0, validPoints.Count)
                        : 0;

                    Transform selectedPoint = validPoints[selectedIndex];

                    positions.Add(selectedPoint.position);

                    // Xóa khỏi list để không spawn trùng 1 điểm trong cùng 1 lần cast.
                    validPoints.RemoveAt(selectedIndex);
                }

                return positions;
            }
        }

        // Fallback nếu chưa gán spawn point.
        if (player == null)
        {
            positions.Add(new Vector3(transform.position.x, fallbackGroundY + pillarSize.y * 0.5f, 0f));
            return positions;
        }

        positions.Add(GetFloorPointBelow(player.position, pillarSize));

        for (int i = 1; i < pillarCount; i++)
        {
            float randomX = player.position.x + Random.Range(-pillarRandomXRange, pillarRandomXRange);
            Vector3 source = new Vector3(randomX, player.position.y + groundRayDistance * 0.5f, 0f);
            positions.Add(GetFloorPointBelow(source, pillarSize));
        }

        return positions;
    }

    private Vector3 GetFloorPointBelow(Vector3 source, Vector2 effectSize)
    {
        RaycastHit2D hit = Physics2D.Raycast(source, Vector2.down, groundRayDistance, groundAndPlatformLayer);

        if (hit.collider != null)
            return new Vector3(hit.point.x, hit.point.y + effectSize.y * 0.5f, 0f);

        return new Vector3(source.x, fallbackGroundY + effectSize.y * 0.5f, 0f);
    }

    private bool IsPlayerOnPlatform()
    {
        Collider2D platformCollider;

        if (TryGetPlatformBelowPlayer(out platformCollider)) return true;
        return player != null && player.position.y >= platformYThreshold;
    }

    private bool TryGetPlatformBelowPlayer(out Collider2D platformCollider)
    {
        platformCollider = null;

        if (player == null) return false;

        Vector2 origin = playerCollider != null
            ? new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y + 0.05f)
            : (Vector2)player.position;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundRayDistance, groundAndPlatformLayer);

        if (hit.collider == null) return false;

        if (IsInLayerMask(hit.collider.gameObject.layer, platformLayer))
        {
            platformCollider = hit.collider;
            return true;
        }

        return false;
    }

    private void SpawnProjectile(GameObject prefab, int projectileDamage, float projectileSpeed)
    {
        if (prefab == null) return;

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        GameObject obj = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        BossProjectile projectile = obj.GetComponent<BossProjectile>();

        Vector2 target = playerCollider != null
            ? playerCollider.bounds.center
            : player != null ? (Vector2)player.position : (Vector2)spawnPoint.position + Vector2.right * GetFacingSign();

        Vector2 direction = (target - (Vector2)spawnPoint.position).normalized;

        if (direction.sqrMagnitude <= 0.001f)
            direction = new Vector2(GetFacingSign(), 0f);

        if (projectile != null)
            projectile.Init(direction, projectileDamage, projectileSpeed);
    }

    private GameObject SpawnAreaVisual(GameObject prefab, Vector3 position, Vector2 size)
    {
        if (prefab == null) return null;

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        ApplyAreaSize(obj, size);

        return obj;
    }

    private void SpawnDamageZone(GameObject prefab, Vector3 position, Vector2 size, int zoneDamage, float duration)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        ApplyAreaSize(obj, size);

        BossDamageZone damageZone = obj.GetComponent<BossDamageZone>();

        if (damageZone != null)
            damageZone.SetData(zoneDamage, duration);
        else
            Destroy(obj, duration);
    }

    private void ApplyAreaSize(GameObject obj, Vector2 size)
    {
        if (obj == null) return;

        BoxCollider2D box = obj.GetComponent<BoxCollider2D>();

        if (box != null)
        {
            box.isTrigger = true;
            box.size = size;
        }

        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
        }
    }

    public void TakeDamage(int amount)
    {
        if (state == BossState.Dead) return;
        if (amount <= 0) return;
        if (isInvincible) return;

        int originalAmount = amount;

        if (isCastingProtectedSkill)
        {
            if (protectedCastDefenseMode == ProtectedCastDefenseMode.Invincible)
            {
                if (debugLog && debugProtectedCastDefense)
                    Debug.Log($"Boss blocked {originalAmount} damage while casting a protected skill.");

                return;
            }

            if (protectedCastDefenseMode == ProtectedCastDefenseMode.DamageReduction)
            {
                float reduction = Mathf.Clamp01(protectedCastDamageReduction);
                amount = Mathf.Max(0, Mathf.RoundToInt(originalAmount * (1f - reduction)));

                if (debugLog && debugProtectedCastDefense)
                    Debug.Log($"Boss protected cast damage: {originalAmount} -> {amount} ({reduction * 100f:0}% reduced).");

                if (amount <= 0) return;
            }
        }

        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        if (debugLog) Debug.Log($"Boss HP: {currentHP}/{maxHP}");

        HitFeedback();

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        CheckPhaseTransition();
    }

    private void CheckPhaseTransition()
    {
        if (!phase2Started && currentHP <= Mathf.RoundToInt(maxHP * phase2HpRatio))
        {
            phase2Started = true;
            CurrentPhase = 2;
            StartMainRoutine(PhaseBreakRoutine(2));
            return;
        }

        if (!phase3Started && currentHP <= Mathf.RoundToInt(maxHP * phase3HpRatio))
        {
            phase3Started = true;
            CurrentPhase = 3;
            mustUseUltimate = useUltimateImmediatelyInPhase3;
            StartMainRoutine(PhaseBreakRoutine(3));
        }
    }

    private IEnumerator PhaseBreakRoutine(int nextPhase)
    {
        state = BossState.PhaseBreak;
        isInvincible = true;
        EndProtectedCast();
        StopPhysics();

        PlayStateAtNormalSpeed(phaseBreakStateName);

        if (debugLog) Debug.Log($"Boss entered Phase {nextPhase}.");

        yield return new WaitForSeconds(phaseBreakDuration);

        state = BossState.Idle;
        isInvincible = false;
        PlayIdleState(true);

        StartMainRoutine(CombatLoop());
    }

    private void Die()
    {
        if (state == BossState.Dead) return;

        state = BossState.Dead;
        isInvincible = true;
        EndProtectedCast();

        StopAllCoroutines();
        StopPhysics();

        if (bossCollider != null) bossCollider.enabled = false;

        PlayStateWithDuration(
            defeatStateName,
            defeatTargetDuration,
            defeatFallbackDuration
        );

        if (debugLog) Debug.Log("Green Flame Boss defeated.");
    }

    private void ReturnToIdle()
    {
        if (state == BossState.Dead) return;

        EndProtectedCast();
        state = BossState.Idle;
        StopPhysics();
        PlayIdleState(true);
    }

    private void BeginProtectedCast(string skillName)
    {
        isCastingProtectedSkill = true;

        if (debugLog && debugProtectedCastDefense)
            Debug.Log($"Boss protected cast started: {skillName} | Defense: {protectedCastDefenseMode}.");
    }

    private void EndProtectedCast()
    {
        isCastingProtectedSkill = false;
    }

    private void HitFeedback()
    {
        if (hitParticlePrefab != null)
            Instantiate(hitParticlePrefab, transform.position, Quaternion.identity);

        if (!useHitFlash) return;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
            StartCoroutine(FlashSprite(renderers[i]));
    }

    private IEnumerator FlashSprite(SpriteRenderer sr)
    {
        if (sr == null) yield break;

        Color oldColor = sr.color;
        sr.color = Color.white;

        yield return new WaitForSeconds(hitFlashDuration);

        if (sr != null) sr.color = oldColor;
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObj == null)
        {
            player = null;
            playerCollider = null;
            return;
        }

        player = playerObj.transform;

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

    private float GetDistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector2.Distance(transform.position, player.position);
    }

    private void FacePlayer()
    {
        if (!flipToPlayer || player == null) return;

        float direction = player.position.x - transform.position.x;
        if (Mathf.Abs(direction) < 0.05f) return;

        ApplyFacing(direction > 0f);
    }

    private void ApplyFacing(bool faceRight)
    {
        isFacingRight = faceRight;

        if (useSpriteRendererFlip)
        {
            if (visualSpriteRenderer == null)
                visualSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (visualSpriteRenderer == null) return;

            bool flip = faceRight;

            if (invertSpriteFacing) flip = !flip;

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

    private float GetFacingSign()
    {
        return isFacingRight ? 1f : -1f;
    }

    private AnimationClip GetCurrentAnimatorClip()
    {
        if (animator == null) return null;

        AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);

        if (clipInfos != null && clipInfos.Length > 0)
            return clipInfos[0].clip;

        return null;
    }

    private float GetClipLength(string clipName, float fallback)
    {
        if (!useClipLengthTiming) return fallback;
        if (animator == null) return fallback;
        if (animator.runtimeAnimatorController == null) return fallback;
        if (string.IsNullOrEmpty(clipName)) return fallback;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (clips[i].name == clipName)
            {
                return Mathf.Max(0.01f, clips[i].length);
            }
        }

        return fallback;
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void OnDisable()
    {
        EndProtectedCast();
        StopManualAnimationPlayback();

        if (animator != null)
            animator.speed = 1f;
    }

    private void StopPhysics()
    {
        if (rb == null) return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void DestroySafe(GameObject obj, float delay)
    {
        if (obj != null) Destroy(obj, delay);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, closeSkillRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.25f);

        Gizmos.color = Color.cyan;
        Vector3 center = groundWaveCenter != null
            ? groundWaveCenter.position
            : new Vector3(transform.position.x, fallbackGroundY + groundWaveSize.y * 0.5f, 0f);
        Gizmos.DrawWireCube(center, groundWaveSize);
    }
}
