using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class CombatZoneSpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemySpawnEntry
    {
        public string name;
        public GameObject prefab;
        public EnemyType enemyType;

        [Header("Spawn Point Filter")]
        public List<SpawnPoint.SpawnPointType> allowedSpawnPointTypes = new List<SpawnPoint.SpawnPointType>();

        [Header("Spawn Weight")]
        public float baseWeight = 1f;
        public float minWeight = 0.2f;
        public float maxWeight = 3f;

        [Header("Budget / Limit")]
        public int spawnCost = 1;
        public int maxActiveSameType = 3;

        [Header("Balance Target")]
        public float expectedLifeTime = 10f;
        public float targetDamageDealt = 2f;
    }

    private class EnemyRuntimeStats
    {
        public int deathCount;
        public float averageLifeTime;
        public float averageDamageDealt;

        public void AddSample(float lifeTime, int damageDealt)
        {
            deathCount++;

            float t = 1f / deathCount;

            averageLifeTime = Mathf.Lerp(averageLifeTime, lifeTime, t);
            averageDamageDealt = Mathf.Lerp(averageDamageDealt, damageDealt, t);
        }
    }

    [Header("Trigger")]
    public bool startOnlyOnce = true;
    public string playerTag = "Player";

    [Tooltip("Nếu bật, Player đi vào trigger của zone này sẽ tự start spawn. Nếu muốn chạy Phase 1 -> 2 -> 3 tuần tự bằng CombatPhaseSequenceManager thì tắt.")]
    public bool autoStartOnTrigger = false;

    [Header("Spawn Points")]
    public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
    public bool fallbackToAnySpawnPoint = true;

    [Header("Enemy Entries")]
    public List<EnemySpawnEntry> enemyEntries = new List<EnemySpawnEntry>();

    [Header("Phase")]
    public int phaseIndex = 1;
    public bool firstPhaseSpawnEvenly = true;
    public int firstPhaseEnemyCount = 4;

    [Header("Amount")]
    public int totalEnemyToSpawn = 12;
    public int targetActiveEnemyCount = 4;
    public int maxActiveEnemyCount = 6;
    public int activeBudget = 8;

    [Header("Role Limits")]
    public int maxTankActive = 1;
    public int maxRangedActive = 2;
    public int maxDuelistActive = 3;

    [Header("Spawn Interval")]
    public float defaultAverageEnemyLifeTime = 10f;
    public float minSpawnInterval = 0.8f;
    public float maxSpawnInterval = 4f;

    [Header("Stats")]
    public bool shareStatsBetweenPhases = true;

    [Header("Debug")]
    public bool debugLog = true;

    public event System.Action<CombatZoneSpawner> OnPhaseCleared;

    private bool hasStarted;
    private bool isSpawning;
    private bool phaseCleared;
    private bool clearEventSent;

    private int totalSpawned;
    private int roundRobinIndex;

    private Transform player;
    private readonly List<EnemyAI> aliveEnemies = new List<EnemyAI>();

    private readonly Dictionary<EnemyType, int> spawnedCountByType = new Dictionary<EnemyType, int>();
    private readonly List<string> phaseDecisionLogs = new List<string>();

    private readonly Dictionary<EnemyType, EnemyRuntimeStats> localStatsByType =
        new Dictionary<EnemyType, EnemyRuntimeStats>();

    private static readonly Dictionary<EnemyType, EnemyRuntimeStats> globalStatsByType =
        new Dictionary<EnemyType, EnemyRuntimeStats>();

    private static readonly Dictionary<EnemyType, int> globalSpawnedCountByType =
        new Dictionary<EnemyType, int>();

    private static readonly List<string> globalDecisionLogs = new List<string>();

    private static int globalClearedPhaseCount = 0;

    private Dictionary<EnemyType, EnemyRuntimeStats> StatsByType
    {
        get
        {
            return shareStatsBetweenPhases ? globalStatsByType : localStatsByType;
        }
    }

    public bool HasStarted => hasStarted;
    public bool IsSpawning => isSpawning;
    public bool PhaseCleared => phaseCleared;
    public int TotalSpawned => totalSpawned;

    public int AliveCount
    {
        get
        {
            CleanDeadEnemies();
            return aliveEnemies.Count;
        }
    }

    public bool IsCleared
    {
        get
        {
            CleanDeadEnemies();

            return hasStarted &&
                   phaseCleared &&
                   !isSpawning &&
                   totalSpawned >= GetTargetTotalEnemyToSpawn() &&
                   aliveEnemies.Count == 0;
        }
    }

    private void Awake()
    {
        FindPlayer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoStartOnTrigger) return;
        if (!other.CompareTag(playerTag)) return;
        if (startOnlyOnce && hasStarted) return;

        StartSpawner();
    }

    public void StartSpawner()
    {
        if (isSpawning) return;
        if (hasStarted && startOnlyOnce) return;

        hasStarted = true;
        isSpawning = false;
        phaseCleared = false;
        clearEventSent = false;

        if (player == null)
        {
            FindPlayer();
        }

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        isSpawning = true;
        totalSpawned = 0;
        roundRobinIndex = 0;

        spawnedCountByType.Clear();
        phaseDecisionLogs.Clear();

        int targetTotal = GetTargetTotalEnemyToSpawn();

        if (debugLog)
        {
            Debug.Log($"START PHASE {phaseIndex} | targetTotal={targetTotal}");
        }

        while (totalSpawned < targetTotal)
        {
            CleanDeadEnemies();

            if (CanSpawnMoreNow())
            {
                SpawnEnemy();
            }

            yield return new WaitForSeconds(CalculateSpawnInterval());
        }

        isSpawning = false;

        if (debugLog)
        {
            Debug.Log($"Phase {phaseIndex} đã spawn đủ {targetTotal} quái. Đợi clear hết quái còn sống.");
        }

        while (true)
        {
            CleanDeadEnemies();

            if (aliveEnemies.Count <= 0)
            {
                break;
            }

            yield return null;
        }

        phaseCleared = true;
        SendPhaseClearedEventOnce();
    }

    private void SendPhaseClearedEventOnce()
    {
        if (clearEventSent) return;

        clearEventSent = true;
        globalClearedPhaseCount++;

        if (debugLog)
        {
            Debug.Log($"PHASE {phaseIndex} CLEAR.");
            Debug.Log(BuildPhaseReport());
        }

        OnPhaseCleared?.Invoke(this);
    }

    private int GetTargetTotalEnemyToSpawn()
    {
        if (phaseIndex <= 1 && firstPhaseSpawnEvenly)
        {
            return Mathf.Max(1, firstPhaseEnemyCount);
        }

        return Mathf.Max(1, totalEnemyToSpawn);
    }

    private bool CanSpawnMoreNow()
    {
        CleanDeadEnemies();

        if (aliveEnemies.Count >= maxActiveEnemyCount) return false;
        if (aliveEnemies.Count >= targetActiveEnemyCount) return false;
        if (GetActiveBudgetUsed() >= activeBudget) return false;

        return true;
    }

    private float CalculateSpawnInterval()
    {
        float averageLife = GetGlobalAverageLifeTime();
        float interval = averageLife / Mathf.Max(1, targetActiveEnemyCount);

        return Mathf.Clamp(interval, minSpawnInterval, maxSpawnInterval);
    }

    private float GetGlobalAverageLifeTime()
    {
        float total = 0f;
        int count = 0;

        foreach (KeyValuePair<EnemyType, EnemyRuntimeStats> pair in StatsByType)
        {
            if (pair.Value.deathCount <= 0) continue;

            total += pair.Value.averageLifeTime;
            count++;
        }

        if (count <= 0)
        {
            return defaultAverageEnemyLifeTime;
        }

        return total / count;
    }

    private void SpawnEnemy()
    {
        EnemySpawnEntry entry = ChooseEntry();

        if (entry == null || entry.prefab == null)
        {
            if (debugLog)
            {
                Debug.LogWarning("CombatZoneSpawner thiếu enemy entry/prefab hợp lệ.");
            }

            return;
        }

        SpawnPoint spawnPoint = ChooseSpawnPoint(entry);

        if (spawnPoint == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"CombatZoneSpawner không tìm thấy SpawnPoint hợp lệ cho {entry.enemyType}.");
            }

            return;
        }

        GameObject obj = Instantiate(
            entry.prefab,
            spawnPoint.transform.position,
            Quaternion.identity
        );

        spawnPoint.NotifySpawned();

        EnemyAI enemy = obj.GetComponent<EnemyAI>();

        if (enemy != null)
        {
            enemy.OnEnemyDied -= HandleEnemyDied;
            enemy.OnEnemyDied += HandleEnemyDied;
            aliveEnemies.Add(enemy);
        }
        else
        {
            Debug.LogWarning($"{obj.name} thiếu EnemyAI.");
        }

        totalSpawned++;
        AddSpawnCount(entry.enemyType);

        if (debugLog)
        {
            Debug.Log(
                $"Spawn {entry.enemyType} tại {spawnPoint.name} | " +
                $"Phase={phaseIndex} | Alive={aliveEnemies.Count} | Total={totalSpawned}/{GetTargetTotalEnemyToSpawn()}"
            );
        }
    }

    private SpawnPoint ChooseSpawnPoint(EnemySpawnEntry entry)
    {
        List<SpawnPoint> validPoints = new List<SpawnPoint>();

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            SpawnPoint point = spawnPoints[i];

            if (point == null) continue;
            if (!point.CanUse(player)) continue;

            bool hasFilter = entry.allowedSpawnPointTypes != null &&
                             entry.allowedSpawnPointTypes.Count > 0;

            if (hasFilter && !entry.allowedSpawnPointTypes.Contains(point.spawnPointType))
            {
                continue;
            }

            validPoints.Add(point);
        }

        if (validPoints.Count > 0)
        {
            return validPoints[Random.Range(0, validPoints.Count)];
        }

        if (!fallbackToAnySpawnPoint)
        {
            return null;
        }

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            SpawnPoint point = spawnPoints[i];

            if (point == null) continue;
            if (!point.CanUse(player)) continue;

            validPoints.Add(point);
        }

        if (validPoints.Count == 0)
        {
            return null;
        }

        return validPoints[Random.Range(0, validPoints.Count)];
    }

    private EnemySpawnEntry ChooseEntry()
    {
        List<EnemySpawnEntry> validEntries = GetValidEntries();

        if (validEntries.Count == 0)
        {
            return null;
        }

        if (phaseIndex <= 1 && firstPhaseSpawnEvenly)
        {
            EnemySpawnEntry entry = validEntries[roundRobinIndex % validEntries.Count];
            roundRobinIndex++;

            LogSpawnDecision(entry, "Phase 1 spawn đều theo round-robin.");

            return entry;
        }

        float totalWeight = 0f;

        for (int i = 0; i < validEntries.Count; i++)
        {
            totalWeight += CalculateDynamicWeight(validEntries[i]);
        }

        if (totalWeight <= 0f)
        {
            EnemySpawnEntry randomEntry = validEntries[Random.Range(0, validEntries.Count)];
            LogSpawnDecision(randomEntry, "Tổng weight <= 0, chọn random trong danh sách hợp lệ.");
            return randomEntry;
        }

        float roll = Random.Range(0f, totalWeight);
        float current = 0f;

        for (int i = 0; i < validEntries.Count; i++)
        {
            current += CalculateDynamicWeight(validEntries[i]);

            if (roll <= current)
            {
                LogSpawnDecision(validEntries[i], $"Weight roll={roll:F2}/{totalWeight:F2}.");
                return validEntries[i];
            }
        }

        EnemySpawnEntry fallbackEntry = validEntries[validEntries.Count - 1];
        LogSpawnDecision(fallbackEntry, "Fallback chọn entry cuối danh sách.");

        return fallbackEntry;
    }

    private List<EnemySpawnEntry> GetValidEntries()
    {
        List<EnemySpawnEntry> valid = new List<EnemySpawnEntry>();
        int usedBudget = GetActiveBudgetUsed();

        for (int i = 0; i < enemyEntries.Count; i++)
        {
            EnemySpawnEntry entry = enemyEntries[i];

            if (entry == null) continue;
            if (entry.prefab == null) continue;

            if (usedBudget + entry.spawnCost > activeBudget) continue;
            if (CountActive(entry.enemyType) >= entry.maxActiveSameType) continue;
            if (!PassRoleLimit(entry.enemyType)) continue;

            valid.Add(entry);
        }

        return valid;
    }

    private bool PassRoleLimit(EnemyType type)
    {
        if (type == EnemyType.Tank && CountActive(EnemyType.Tank) >= maxTankActive)
        {
            return false;
        }

        if ((type == EnemyType.FastRanged || type == EnemyType.Mage) &&
            CountActiveRanged() >= maxRangedActive)
        {
            return false;
        }

        if (type == EnemyType.Duelist && CountActive(EnemyType.Duelist) >= maxDuelistActive)
        {
            return false;
        }

        return true;
    }

    private float CalculateDynamicWeight(EnemySpawnEntry entry)
    {
        float weight = entry.baseWeight;

        EnemyRuntimeStats stats;

        if (StatsByType.TryGetValue(entry.enemyType, out stats) && stats.deathCount > 0)
        {
            float expectedLife = Mathf.Max(0.1f, entry.expectedLifeTime);
            float targetDamage = Mathf.Max(0.1f, entry.targetDamageDealt);

            float lifeRatio = stats.averageLifeTime / expectedLife;
            float damageRatio = stats.averageDamageDealt / targetDamage;

            bool livesTooLong = lifeRatio > 1.2f;
            bool diesTooFast = lifeRatio < 0.75f;
            bool dealsTooMuchDamage = damageRatio > 1.2f;
            bool dealsLowDamage = damageRatio < 0.75f;

            if (livesTooLong && dealsTooMuchDamage)
            {
                weight *= 0.55f;
            }
            else if (livesTooLong && dealsLowDamage)
            {
                weight *= 0.9f;
            }
            else if (diesTooFast && dealsLowDamage)
            {
                weight *= 1.25f;
            }
            else if (diesTooFast && dealsTooMuchDamage)
            {
                weight *= 0.75f;
            }
        }

        return Mathf.Clamp(weight, entry.minWeight, entry.maxWeight);
    }

    private void HandleEnemyDied(EnemyAI enemy)
    {
        if (enemy == null) return;

        enemy.OnEnemyDied -= HandleEnemyDied;

        EnemyRuntimeStats stats;

        if (!StatsByType.TryGetValue(enemy.enemyType, out stats))
        {
            stats = new EnemyRuntimeStats();
            StatsByType.Add(enemy.enemyType, stats);
        }

        stats.AddSample(enemy.lifeTime, enemy.damageDealtToPlayer);

        if (debugLog)
        {
            Debug.Log(
                $"Stats {enemy.enemyType}: " +
                $"lifeAvg={stats.averageLifeTime:F2}s, " +
                $"dmgAvg={stats.averageDamageDealt:F1}, " +
                $"deaths={stats.deathCount}"
            );
        }

        CleanDeadEnemies();
    }

    private void CleanDeadEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null || aliveEnemies[i].IsDead)
            {
                aliveEnemies.RemoveAt(i);
            }
        }
    }

    private int CountActive(EnemyType type)
    {
        CleanDeadEnemies();

        int count = 0;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            if (aliveEnemies[i] != null && aliveEnemies[i].enemyType == type)
            {
                count++;
            }
        }

        return count;
    }

    private int CountActiveRanged()
    {
        CleanDeadEnemies();

        int count = 0;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            if (aliveEnemies[i] == null) continue;

            if (aliveEnemies[i].enemyType == EnemyType.FastRanged ||
                aliveEnemies[i].enemyType == EnemyType.Mage)
            {
                count++;
            }
        }

        return count;
    }

    private int GetActiveBudgetUsed()
    {
        CleanDeadEnemies();

        int total = 0;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            if (aliveEnemies[i] == null) continue;

            EnemySpawnEntry entry = FindEntry(aliveEnemies[i].enemyType);

            if (entry != null)
            {
                total += entry.spawnCost;
            }
        }

        return total;
    }

    private EnemySpawnEntry FindEntry(EnemyType type)
    {
        for (int i = 0; i < enemyEntries.Count; i++)
        {
            if (enemyEntries[i] != null && enemyEntries[i].enemyType == type)
            {
                return enemyEntries[i];
            }
        }

        return null;
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    private void AddSpawnCount(EnemyType type)
    {
        if (!spawnedCountByType.ContainsKey(type))
        {
            spawnedCountByType.Add(type, 0);
        }

        spawnedCountByType[type]++;

        if (!globalSpawnedCountByType.ContainsKey(type))
        {
            globalSpawnedCountByType.Add(type, 0);
        }

        globalSpawnedCountByType[type]++;
    }

    private void LogSpawnDecision(EnemySpawnEntry entry, string reason)
    {
        if (entry == null) return;

        EnemyRuntimeStats stats;
        bool hasStats = StatsByType.TryGetValue(entry.enemyType, out stats) && stats.deathCount > 0;

        float weight = CalculateDynamicWeight(entry);
        float lifeAvg = hasStats ? stats.averageLifeTime : 0f;
        float dmgAvg = hasStats ? stats.averageDamageDealt : 0f;

        string shortReason = BuildShortSpawnReason(entry, hasStats ? stats : null, reason);

        string log =
            $"P{phaseIndex} → {entry.enemyType} | " +
            $"w={weight:F2} | " +
            $"life={lifeAvg:F1}s | " +
            $"dmg={dmgAvg:F1} | " +
            $"{shortReason}";

        phaseDecisionLogs.Add(log);
        globalDecisionLogs.Add(log);
    }

    private string BuildShortSpawnReason(EnemySpawnEntry entry, EnemyRuntimeStats stats, string fallbackReason)
    {
        if (phaseIndex <= 1 && firstPhaseSpawnEvenly)
        {
            return "Phase 1 spawn đều để lấy dữ liệu";
        }

        if (stats == null || stats.deathCount <= 0)
        {
            return "chưa có data, dùng baseWeight";
        }

        float expectedLife = Mathf.Max(0.1f, entry.expectedLifeTime);
        float targetDamage = Mathf.Max(0.1f, entry.targetDamageDealt);

        float lifeRatio = stats.averageLifeTime / expectedLife;
        float dmgRatio = stats.averageDamageDealt / targetDamage;

        bool livesTooLong = lifeRatio > 1.2f;
        bool diesTooFast = lifeRatio < 0.75f;
        bool dmgHigh = dmgRatio > 1.2f;
        bool dmgLow = dmgRatio < 0.75f;

        if (livesTooLong && dmgHigh)
            return "sống lâu + dmg cao → giảm spawn";

        if (livesTooLong && dmgLow)
            return "sống lâu nhưng dmg thấp → giữ vừa";

        if (diesTooFast && dmgLow)
            return "chết nhanh + yếu → tăng nhẹ";

        if (diesTooFast && dmgHigh)
            return "chết nhanh nhưng dmg cao → hạn chế";

        return "chỉ số ổn → giữ gần baseWeight";
    }

    private string BuildPhaseReport()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"========== PHASE {phaseIndex} REPORT ==========");
        sb.AppendLine("---- Spawn Count In This Phase ----");

        foreach (KeyValuePair<EnemyType, int> pair in spawnedCountByType)
        {
            sb.AppendLine($"{pair.Key}: spawned {pair.Value} lần");
        }

        sb.AppendLine("---- Spawn Decisions In This Phase ----");

        for (int i = 0; i < phaseDecisionLogs.Count; i++)
        {
            sb.AppendLine(phaseDecisionLogs[i]);
        }

        sb.AppendLine("====================================");

        return sb.ToString();
    }

    public static void ResetGlobalSpawnReport()
    {
        globalStatsByType.Clear();
        globalSpawnedCountByType.Clear();
        globalDecisionLogs.Clear();
        globalClearedPhaseCount = 0;
    }

    private static int GetTotalSpawnedGlobal()
    {
        int total = 0;

        foreach (KeyValuePair<EnemyType, int> pair in globalSpawnedCountByType)
        {
            total += pair.Value;
        }

        return total;
    }

    private static bool TryGetMostSpawnedType(out EnemyType type, out int count)
    {
        type = default(EnemyType);
        count = 0;
        bool found = false;

        foreach (KeyValuePair<EnemyType, int> pair in globalSpawnedCountByType)
        {
            if (!found || pair.Value > count)
            {
                found = true;
                type = pair.Key;
                count = pair.Value;
            }
        }

        return found;
    }

    private static bool TryGetHighestAverageDamageType(out EnemyType type, out float damage)
    {
        type = default(EnemyType);
        damage = 0f;
        bool found = false;

        foreach (KeyValuePair<EnemyType, EnemyRuntimeStats> pair in globalStatsByType)
        {
            if (pair.Value.deathCount <= 0) continue;

            if (!found || pair.Value.averageDamageDealt > damage)
            {
                found = true;
                type = pair.Key;
                damage = pair.Value.averageDamageDealt;
            }
        }

        return found;
    }

    private static bool TryGetLongestLifeType(out EnemyType type, out float lifeTime)
    {
        type = default(EnemyType);
        lifeTime = 0f;
        bool found = false;

        foreach (KeyValuePair<EnemyType, EnemyRuntimeStats> pair in globalStatsByType)
        {
            if (pair.Value.deathCount <= 0) continue;

            if (!found || pair.Value.averageLifeTime > lifeTime)
            {
                found = true;
                type = pair.Key;
                lifeTime = pair.Value.averageLifeTime;
            }
        }

        return found;
    }

    private static string BuildFinalSummary()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("========== SPAWN SUMMARY ==========");
        sb.AppendLine($"Total Spawned: {ShortReport_TotalSpawned()}");
        sb.AppendLine();

        sb.AppendLine("Enemies:");

        if (globalSpawnedCountByType.Count == 0)
        {
            sb.AppendLine("- Chưa spawn quái nào.");
            return sb.ToString();
        }

        foreach (KeyValuePair<EnemyType, int> pair in globalSpawnedCountByType)
        {
            EnemyType type = pair.Key;
            int count = pair.Value;

            EnemyRuntimeStats stats;
            bool hasStats = globalStatsByType.TryGetValue(type, out stats) && stats.deathCount > 0;

            if (hasStats)
            {
                sb.AppendLine(
                    $"- {type}: spawn {count} lần | " +
                    $"life {stats.averageLifeTime:F1}s | " +
                    $"dmg {stats.averageDamageDealt:F1} | " +
                    $"{BuildShortFinalReason(stats)}"
                );
            }
            else
            {
                sb.AppendLine(
                    $"- {type}: spawn {count} lần | " +
                    $"chưa có data chết"
                );
            }
        }

        sb.AppendLine();
        sb.AppendLine("Why Spawned:");

        if (globalDecisionLogs.Count == 0)
        {
            sb.AppendLine("- Chưa có log quyết định spawn.");
        }
        else
        {
            int maxLines = Mathf.Min(globalDecisionLogs.Count, 12);

            for (int i = 0; i < maxLines; i++)
            {
                sb.AppendLine("- " + globalDecisionLogs[i]);
            }

            if (globalDecisionLogs.Count > maxLines)
            {
                sb.AppendLine($"- ... còn {globalDecisionLogs.Count - maxLines} dòng đã rút gọn");
            }
        }

        sb.AppendLine("===================================");

        return sb.ToString();
    }

    private static string BuildShortFinalReason(EnemyRuntimeStats stats)
    {
        if (stats == null || stats.deathCount <= 0)
        {
            return "chưa đủ data";
        }

        float life = stats.averageLifeTime;
        float dmg = stats.averageDamageDealt;

        if (life >= 14f && dmg >= 3f)
            return "nguy hiểm → nên spawn ít";

        if (life >= 14f && dmg < 2f)
            return "tank/đỡ đòn → spawn vừa";

        if (life < 8f && dmg < 2f)
            return "lính nền yếu → có thể spawn thêm";

        if (life < 8f && dmg >= 3f)
            return "burst dmg → hạn chế spawn";

        return "cân bằng → giữ gần mặc định";
    }

    private static int ShortReport_TotalSpawned()
    {
        int total = 0;

        foreach (KeyValuePair<EnemyType, int> pair in globalSpawnedCountByType)
        {
            total += pair.Value;
        }

        return total;
    }

    public static void PrintGlobalSpawnReport()
    {
        Debug.Log(BuildFinalSummary());
    }

    public void ResetSpawner()
    {
        StopAllCoroutines();

        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] != null)
            {
                aliveEnemies[i].OnEnemyDied -= HandleEnemyDied;
                Destroy(aliveEnemies[i].gameObject);
            }
        }

        aliveEnemies.Clear();
        spawnedCountByType.Clear();
        phaseDecisionLogs.Clear();

        hasStarted = false;
        isSpawning = false;
        phaseCleared = false;
        clearEventSent = false;
        totalSpawned = 0;
        roundRobinIndex = 0;
    }
}
