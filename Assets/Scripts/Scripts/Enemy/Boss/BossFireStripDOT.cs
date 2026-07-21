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

    private BoxCollider2D boxCollider;
    private readonly Dictionary<PlayerHealth, float> nextTickTimeByPlayer = new Dictionary<PlayerHealth, float>();

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void SetData(int damage, float tickRate, float duration, Vector2 size)
    {
        damagePerTick = damage;
        tickInterval = Mathf.Max(0.05f, tickRate);
        lifeTime = Mathf.Max(0.05f, duration);

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }

        boxCollider.isTrigger = true;
        boxCollider.size = size;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
        }

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other, true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other, false);
    }

    private void TryDamage(Collider2D other, bool firstTouch)
    {
        if (other == null) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;

        if (respectPlayerInvincible && playerHealth.IsInvincible)
        {
            return;
        }

        float nextTickTime = 0f;
        nextTickTimeByPlayer.TryGetValue(playerHealth, out nextTickTime);

        if (!firstTouch && Time.time < nextTickTime)
        {
            return;
        }

        playerHealth.TakeDamage(damagePerTick);
        nextTickTimeByPlayer[playerHealth] = Time.time + tickInterval;
    }
}