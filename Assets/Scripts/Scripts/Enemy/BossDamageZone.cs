using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossDamageZone : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 1;
    public float lifeTime = 0.3f;
    public bool damageOnce = true;

    [Header("Options")]
    public bool ignoreInvinciblePlayer = true;

    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();
    private Collider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        ScheduleDestroy(lifeTime);
    }

    public void SetData(int newDamage, float newLifeTime)
    {
        damage = newDamage;
        lifeTime = Mathf.Max(0.01f, newLifeTime);

        hitPlayers.Clear();
        ScheduleDestroy(lifeTime);
    }

    private void ScheduleDestroy(float delay)
    {
        CancelInvoke(nameof(SelfDestroy));
        Invoke(nameof(SelfDestroy), delay);
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (other == null) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;

        if (ignoreInvinciblePlayer && playerHealth.IsInvincible)
        {
            return;
        }

        if (damageOnce && hitPlayers.Contains(playerHealth))
        {
            return;
        }

        hitPlayers.Add(playerHealth);
        playerHealth.TakeDamage(damage);
    }
}