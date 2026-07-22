using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossDamageZone : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 1;
    public float lifeTime = 0.3f;

    [Tooltip("ON = damage the same player only once. OFF = damage over time while the player stays inside this zone.")]
    public bool damageOnce = true;

    [Tooltip("Used only when Damage Once is OFF.")]
    public float damageTickInterval = 0.5f;

    [Header("Options")]
    public bool ignoreInvinciblePlayer = true;

    [Tooltip("Keeps checking overlaps even if Unity misses OnTriggerEnter when the zone is spawned directly on top of the player.")]
    public bool scanOverlaps = true;

    [Tooltip("0 = scan every frame. Example: 0.02 = scan roughly every 0.02 second.")]
    public float scanInterval = 0f;

    [Tooltip("Optional. Leave empty to check all layers, or set this to Player only.")]
    public LayerMask playerLayer;

    [Header("Debug")]
    public bool debugDamage = false;

    private readonly HashSet<PlayerHealth> damagedOncePlayers = new HashSet<PlayerHealth>();
    private readonly Dictionary<PlayerHealth, float> nextDamageTimeByPlayer = new Dictionary<PlayerHealth, float>();

    private Collider2D zoneCollider;
    private readonly Collider2D[] overlapResults = new Collider2D[32];
    private Coroutine scanRoutine;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();

        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
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
        // Immediate scan helps short-lived hitboxes such as pillars that are spawned directly on the player.
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

    public void SetData(int newDamage, float newLifeTime)
    {
        damage = Mathf.Max(0, newDamage);
        lifeTime = Mathf.Max(0.01f, newLifeTime);

        damagedOncePlayers.Clear();
        nextDamageTimeByPlayer.Clear();

        ScheduleDestroy(lifeTime);
        TryDamageCurrentOverlaps();
    }

    public void SetOneShotData(int newDamage, float newLifeTime)
    {
        damageOnce = true;
        SetData(newDamage, newLifeTime);
    }

    public void SetDOTData(int newDamage, float newTickInterval, float newLifeTime)
    {
        damageOnce = false;
        damageTickInterval = Mathf.Max(0.05f, newTickInterval);
        SetData(newDamage, newLifeTime);
    }

    private void ScheduleDestroy(float delay)
    {
        CancelInvoke(nameof(SelfDestroy));
        Invoke(nameof(SelfDestroy), Mathf.Max(0.01f, delay));
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }

    private IEnumerator ScanOverlapRoutine()
    {
        // Wait one physics step so newly spawned colliders are registered.
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
        if (zoneCollider == null || !zoneCollider.enabled) return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;

        if (playerLayer.value != 0)
        {
            filter.useLayerMask = true;
            filter.SetLayerMask(playerLayer);
        }

        int count = zoneCollider.Overlap(filter, overlapResults);

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

        if (ignoreInvinciblePlayer && playerHealth.IsInvincible)
            return;

        if (damageOnce)
        {
            if (damagedOncePlayers.Contains(playerHealth))
                return;

            damagedOncePlayers.Add(playerHealth);
            ApplyDamage(playerHealth);
            return;
        }

        float nextDamageTime = 0f;
        nextDamageTimeByPlayer.TryGetValue(playerHealth, out nextDamageTime);

        if (Time.time < nextDamageTime)
            return;

        ApplyDamage(playerHealth);
        nextDamageTimeByPlayer[playerHealth] = Time.time + Mathf.Max(0.05f, damageTickInterval);
    }

    private void ApplyDamage(PlayerHealth playerHealth)
    {
        if (playerHealth == null) return;
        if (damage <= 0) return;

        playerHealth.TakeDamage(damage);

        if (debugDamage)
            Debug.Log($"BossDamageZone hit Player for {damage} damage. Mode: {(damageOnce ? "Once" : "DOT")}");
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
