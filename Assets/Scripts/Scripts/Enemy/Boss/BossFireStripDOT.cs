using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BossFireStripDOT : MonoBehaviour
{
    [Header("Damage Over Time")]
    public int damagePerTick = 1;
    public float tickInterval = 0.6f;
    public float lifeTime = 3f;

    [Header("Options")]
    public bool respectPlayerInvincible = true;

    [Tooltip("Keeps checking overlaps even if the fire is spawned directly under the player.")]
    public bool scanOverlaps = true;

    [Tooltip("0 = scan every frame. Example: 0.02 = scan roughly every 0.02 second.")]
    public float scanInterval = 0f;

    [Tooltip("Optional. Leave empty to check all layers, or set this to Player only.")]
    public LayerMask playerLayer;

    [Header("Debug")]
    public bool debugDamage = false;

    private BoxCollider2D boxCollider;
    private readonly Dictionary<PlayerHealth, float> nextTickTimeByPlayer = new Dictionary<PlayerHealth, float>();
    private readonly Collider2D[] overlapResults = new Collider2D[32];
    private Coroutine scanRoutine;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        if (boxCollider != null)
            boxCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        ScheduleDestroy(lifeTime);

        if (scanOverlaps)
        {
            if (scanRoutine != null)
                StopCoroutine(scanRoutine);

            scanRoutine = StartCoroutine(ScanOverlapRoutine());
        }
    }

    private void Start()
    {
        TryDamageCurrentOverlaps();
    }

    private void OnDisable()
    {
        if (scanRoutine != null)
        {
            StopCoroutine(scanRoutine);
            scanRoutine = null;
        }
    }

    public void SetData(int damage, float tickRate, float duration, Vector2 size)
    {
        damagePerTick = Mathf.Max(0, damage);
        tickInterval = Mathf.Max(0.05f, tickRate);
        lifeTime = Mathf.Max(0.05f, duration);

        nextTickTimeByPlayer.Clear();

        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();

        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
            boxCollider.size = size;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
        }

        ScheduleDestroy(lifeTime);
        TryDamageCurrentOverlaps();
    }

    private void ScheduleDestroy(float delay)
    {
        CancelInvoke(nameof(SelfDestroy));
        Invoke(nameof(SelfDestroy), Mathf.Max(0.05f, delay));
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }

    private IEnumerator ScanOverlapRoutine()
    {
        yield return new WaitForFixedUpdate();

        while (isActiveAndEnabled)
        {
            TryDamageCurrentOverlaps();

            if (scanInterval > 0f)
                yield return new WaitForSeconds(scanInterval);
            else
                yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamageCurrentOverlaps()
    {
        if (boxCollider == null || !boxCollider.enabled) return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;

        if (playerLayer.value != 0)
        {
            filter.useLayerMask = true;
            filter.SetLayerMask(playerLayer);
        }

        int count = boxCollider.Overlap(filter, overlapResults);

        for (int i = 0; i < count; i++)
        {
            TryDamage(overlapResults[i]);
            overlapResults[i] = null;
        }
    }

    private void TryDamage(Collider2D other)
    {
        if (other == null) return;

        if (playerLayer.value != 0 && !IsInLayerMask(other.gameObject.layer, playerLayer))
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;

        if (respectPlayerInvincible && playerHealth.IsInvincible)
            return;

        float nextTickTime = 0f;
        nextTickTimeByPlayer.TryGetValue(playerHealth, out nextTickTime);

        if (Time.time < nextTickTime)
            return;

        playerHealth.TakeDamage(damagePerTick);
        nextTickTimeByPlayer[playerHealth] = Time.time + Mathf.Max(0.05f, tickInterval);

        if (debugDamage)
            Debug.Log($"BossFireStripDOT ticked Player for {damagePerTick} damage.");
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
