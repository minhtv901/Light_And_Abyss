using System.Collections.Generic;
using UnityEngine;

public class BossDamageZone : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 1;
    public float lifeTime = 0.3f;
    public bool damageOnce = true;

    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

    private BoxCollider2D boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void SetData(int newDamage, float newLifeTime)
    {
        damage = newDamage;
        lifeTime = newLifeTime;

        Destroy(gameObject, lifeTime);
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
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;
        if (playerHealth.IsInvincible) return;

        if (damageOnce && hitPlayers.Contains(playerHealth))
        {
            return;
        }

        hitPlayers.Add(playerHealth);
        playerHealth.TakeDamage(damage);
    }
}