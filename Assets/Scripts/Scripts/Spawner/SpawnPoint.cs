using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    public enum SpawnPointType
    {
        Ground,
        Platform,
        RangedBack,
        Tank,
        Air
    }

    [Header("Type")]
    public SpawnPointType spawnPointType = SpawnPointType.Ground;

    [Header("Availability")]
    public float cooldownAfterSpawn = 0.5f;
    public float minDistanceFromPlayer = 1.5f;

    [Header("Debug")]
    public float gizmoSize = 0.25f;
    public bool drawLabelLine = true;

    private float nextAvailableTime = 0f;

    public bool CanUse(Transform player)
    {
        if (!gameObject.activeInHierarchy) return false;
        if (Time.time < nextAvailableTime) return false;

        if (player != null && minDistanceFromPlayer > 0f)
        {
            float distance = Vector2.Distance(transform.position, player.position);

            if (distance < minDistanceFromPlayer)
            {
                return false;
            }
        }

        return true;
    }

    public void NotifySpawned()
    {
        nextAvailableTime = Time.time + cooldownAfterSpawn;
    }

    private void OnDrawGizmos()
    {
        switch (spawnPointType)
        {
            case SpawnPointType.Ground:
                Gizmos.color = Color.green;
                break;

            case SpawnPointType.Platform:
                Gizmos.color = Color.yellow;
                break;

            case SpawnPointType.RangedBack:
                Gizmos.color = Color.cyan;
                break;

            case SpawnPointType.Tank:
                Gizmos.color = Color.red;
                break;

            case SpawnPointType.Air:
                Gizmos.color = Color.magenta;
                break;
        }

        Gizmos.DrawSphere(transform.position, gizmoSize);

        if (drawLabelLine)
        {
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.7f);
        }
    }
}