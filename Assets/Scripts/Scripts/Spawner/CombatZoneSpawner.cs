using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatZoneSpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject[] enemyPrefabs;

    [Header("Right Side Spawn Points")]
    public Transform[] rightSpawnPoints;

    [Header("Spawn Settings")]
    public int maxAliveEnemies = 5;
    public int maxTotalEnemies = 15;
    public int batchSize = 5;

    [Header("Spawn Timing")]
    public float delayBetweenEachEnemy = 0.4f;
    public float enemyWakeUpDelay = 1.2f;
    public float delayBetweenBatches = 2f;

    [Header("Debug")]
    public bool drawGizmos = true;

    private int totalSpawned = 0;
    private bool phaseStarted = false;
    private bool phaseCleared = false;
    private bool isSpawning = false;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    public bool HasStarted => phaseStarted;
    public bool IsCleared => phaseCleared;
    public int TotalSpawned => totalSpawned;
    public int AliveEnemyCount => activeEnemies.Count;
    public bool IsSpawning => isSpawning;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        StartPhase();
    }

    private void Update()
    {
        if (!phaseStarted || phaseCleared) return;

        activeEnemies.RemoveAll(enemy => enemy == null);

        if (isSpawning) return;

        if (activeEnemies.Count == 0)
        {
            if (totalSpawned >= maxTotalEnemies)
            {
                ClearPhase();
            }
            else
            {
                StartCoroutine(SpawnBatchRoutine());
            }
        }
    }

    public void StartPhase()
    {
        if (phaseStarted || phaseCleared) return;

        phaseStarted = true;
        phaseCleared = false;
        isSpawning = false;
        totalSpawned = 0;
        activeEnemies.Clear();

        Debug.Log("Bắt đầu Combat Zone spawn quái!");

        StartCoroutine(SpawnBatchRoutine());
    }

    private IEnumerator SpawnBatchRoutine()
    {
        if (isSpawning || !phaseStarted || phaseCleared)
            yield break;

        isSpawning = true;

        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning(name + ": Chưa gán Enemy Prefabs.");
            isSpawning = false;
            ClearPhase();
            yield break;
        }

        if (rightSpawnPoints == null || rightSpawnPoints.Length == 0)
        {
            Debug.LogWarning(name + ": Chưa gán Right Side Spawn Points.");
            isSpawning = false;
            ClearPhase();
            yield break;
        }

        // Đợt đầu spawn luôn. Các đợt sau mới chờ delayBetweenBatches.
        if (totalSpawned > 0 && delayBetweenBatches > 0f)
            yield return new WaitForSeconds(delayBetweenBatches);

        activeEnemies.RemoveAll(enemy => enemy == null);

        int remaining = maxTotalEnemies - totalSpawned;
        int availableAliveSlots = Mathf.Max(0, maxAliveEnemies - activeEnemies.Count);
        int amountToSpawn = Mathf.Min(batchSize, availableAliveSlots, remaining);

        if (amountToSpawn <= 0)
        {
            isSpawning = false;
            yield break;
        }

        List<Transform> availablePoints = new List<Transform>(rightSpawnPoints);

        for (int i = 0; i < amountToSpawn; i++)
        {
            if (!phaseStarted || phaseCleared)
                break;

            if (availablePoints.Count == 0)
                availablePoints = new List<Transform>(rightSpawnPoints);

            GameObject enemyPrefab = GetRandomEnemyPrefab();
            Transform spawnPoint = GetRandomSpawnPoint(availablePoints);

            if (enemyPrefab == null || spawnPoint == null)
                continue;

            GameObject newEnemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);

            activeEnemies.Add(newEnemy);
            totalSpawned++;

            StartCoroutine(WakeUpEnemyRoutine(newEnemy));

            if (delayBetweenEachEnemy > 0f)
                yield return new WaitForSeconds(delayBetweenEachEnemy);
        }

        activeEnemies.RemoveAll(enemy => enemy == null);

        Debug.Log("Đã spawn batch. Tổng đã spawn: " + totalSpawned + "/" + maxTotalEnemies + ", còn sống: " + activeEnemies.Count);

        isSpawning = false;
    }

    private GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return null;

        for (int i = 0; i < 20; i++)
        {
            GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    private Transform GetRandomSpawnPoint(List<Transform> availablePoints)
    {
        if (availablePoints == null || availablePoints.Count == 0)
            return null;

        int pointIndex = Random.Range(0, availablePoints.Count);
        Transform spawnPoint = availablePoints[pointIndex];
        availablePoints.RemoveAt(pointIndex);

        return spawnPoint;
    }

    private IEnumerator WakeUpEnemyRoutine(GameObject enemy)
    {
        if (enemy == null)
            yield break;

        EnemyAI enemyAI = enemy.GetComponentInChildren<EnemyAI>();

        if (enemyAI != null)
            enemyAI.enabled = false;

        Animator animator = enemy.GetComponentInChildren<Animator>();

        if (animator != null)
            animator.SetTrigger("Spawn");

        if (enemyWakeUpDelay > 0f)
            yield return new WaitForSeconds(enemyWakeUpDelay);

        if (enemy != null && enemyAI != null)
            enemyAI.enabled = true;
    }

    private void ClearPhase()
    {
        if (phaseCleared) return;

        phaseStarted = false;
        phaseCleared = true;
        isSpawning = false;
        activeEnemies.Clear();

        Debug.Log("Clear Combat Zone! Đã tiêu diệt toàn bộ quái.");
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || rightSpawnPoints == null) return;

        Gizmos.color = Color.red;

        foreach (Transform point in rightSpawnPoints)
        {
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, 0.25f);
        }
    }
}
