using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossProjectile : MonoBehaviour
{
    [Header("Move")]
    public float speed = 8f;
    public float lifeTime = 3f;
    public Vector2 moveDirection = Vector2.left;

    [Header("Damage")]
    public int damage = 1;
    public bool damageOnce = true;

    [Header("Hit")]
    public bool destroyOnPlayerHit = true;
    public bool destroyOnObstacleHit = true;
    public LayerMask obstacleLayer;

    [Header("Visual")]
    public Transform visualRoot;
    public bool rotateVisualToDirection = false;
    public bool flipVisualByDirection = true;
    public bool invertVisualFlip = false;
    public float visualRotationOffset = 0f;

    private Rigidbody2D rb;
    private Collider2D projectileCollider;
    private Vector3 originalVisualScale;

    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        if (visualRoot == null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();

            if (sr != null)
            {
                visualRoot = sr.transform;
            }
        }

        if (visualRoot != null)
        {
            originalVisualScale = visualRoot.localScale;
        }
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
        ApplyVisualDirection();
    }

    private void FixedUpdate()
    {
        Vector2 velocity = moveDirection.normalized * speed;

        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
        else
        {
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
        }
    }

    public void Init(Vector2 direction, int projectileDamage, float projectileSpeed)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.left;
        }

        moveDirection = direction.normalized;
        damage = projectileDamage;
        speed = projectileSpeed;

        ApplyVisualDirection();
    }

    private void ApplyVisualDirection()
    {
        if (visualRoot == null) return;

        if (rotateVisualToDirection)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            visualRoot.rotation = Quaternion.Euler(0f, 0f, angle + visualRotationOffset);
        }

        if (!flipVisualByDirection) return;

        // Only flip when the projectile has a clear horizontal direction.
        if (Mathf.Abs(moveDirection.x) < 0.05f) return;

        Vector3 scale = originalVisualScale;
        bool movingRight = moveDirection.x > 0f;

        if (invertVisualFlip)
        {
            movingRight = !movingRight;
        }

        scale.x = movingRight
            ? Mathf.Abs(originalVisualScale.x)
            : -Mathf.Abs(originalVisualScale.x);

        visualRoot.localScale = scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            TryDamagePlayer(playerHealth);
            return;
        }

        if (destroyOnObstacleHit && IsInLayerMask(other.gameObject.layer, obstacleLayer))
        {
            Destroy(gameObject);
        }
    }

    private void TryDamagePlayer(PlayerHealth playerHealth)
    {
        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;

        // Dash / invincible should let the projectile pass through the Player.
        if (playerHealth.IsInvincible)
        {
            return;
        }

        if (damageOnce && hitPlayers.Contains(playerHealth))
        {
            return;
        }

        hitPlayers.Add(playerHealth);
        playerHealth.TakeDamage(damage);

        if (destroyOnPlayerHit)
        {
            Destroy(gameObject);
        }
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}