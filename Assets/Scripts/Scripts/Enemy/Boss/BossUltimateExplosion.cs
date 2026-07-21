using UnityEngine;

public class BossUltimateExplosion : MonoBehaviour
{
    [Header("Fire Strip")]
    public GameObject fireStripPrefab;
    public Collider2D targetSurface;

    [Header("Timing")]
    public float fireStripSpawnDelay = 0.6f;
    public float explosionLifeTime = 1.0f;

    [Header("Fire Strip Data")]
    public int fireDamagePerTick = 1;
    public float fireTickInterval = 0.6f;
    public float fireDuration = 3f;
    public float fireHeight = 1.2f;
    public float extraWidth = 0f;

    private bool spawnedFireStrip = false;

    private void Start()
    {
        Invoke(nameof(SpawnFireStrip), fireStripSpawnDelay);
        Destroy(gameObject, explosionLifeTime);
    }

    public void Init(
        Collider2D surface,
        GameObject stripPrefab,
        int damagePerTick,
        float tickInterval,
        float duration,
        float height
    )
    {
        targetSurface = surface;
        fireStripPrefab = stripPrefab;
        fireDamagePerTick = damagePerTick;
        fireTickInterval = tickInterval;
        fireDuration = duration;
        fireHeight = height;
    }

    private void SpawnFireStrip()
    {
        if (spawnedFireStrip) return;
        spawnedFireStrip = true;

        if (fireStripPrefab == null) return;
        if (targetSurface == null) return;

        Bounds bounds = targetSurface.bounds;

        float width = bounds.size.x + extraWidth;
        Vector2 stripSize = new Vector2(width, fireHeight);

        Vector3 stripPosition = new Vector3(
            bounds.center.x,
            bounds.max.y + fireHeight * 0.5f,
            0f
        );

        GameObject strip = Instantiate(fireStripPrefab, stripPosition, Quaternion.identity);

        BossFireStripDOT fireStrip = strip.GetComponent<BossFireStripDOT>();

        if (fireStrip != null)
        {
            fireStrip.SetData(
                fireDamagePerTick,
                fireTickInterval,
                fireDuration,
                stripSize
            );
        }
    }
}