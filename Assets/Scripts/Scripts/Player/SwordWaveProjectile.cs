using System.Collections.Generic;
using UnityEngine;

public class SwordWaveProjectile : MonoBehaviour
{
    [Header("Move")]
    public float speed = 8f;
    public float lifeTime = 1.2f;
    public Vector2 moveDirection = Vector2.right;

    [Header("Visual")]
    public Transform visualRoot;

    [Header("Damage")]
    public int damage = 1;
    public LayerMask enemyLayer;

    [Header("Rage Gain")]
    public bool gainRageOnHit = true;
    public float rageGain = 0.35f;

    private Rigidbody2D rb;
    private RageSystem ownerRageSystem;
    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

    private Vector3 originalVisualScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

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

    public void Init(
        Vector2 direction,
        int projectileDamage,
        RageSystem rageSystem,
        float rageAmount
    )
    {
        moveDirection = direction.normalized;
        damage = projectileDamage;
        ownerRageSystem = rageSystem;
        rageGain = rageAmount;

        ApplyVisualDirection();
    }

    private void ApplyVisualDirection()
    {
        if (visualRoot == null) return;

        Vector3 scale = originalVisualScale;

        if (moveDirection.x < 0f)
        {
            scale.x = -Mathf.Abs(originalVisualScale.x);
        }
        else
        {
            scale.x = Mathf.Abs(originalVisualScale.x);
        }

        visualRoot.localScale = scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();

        if (enemy == null) return;
        if (hitEnemies.Contains(enemy.gameObject)) return;

        hitEnemies.Add(enemy.gameObject);

        enemy.TakeDamage(damage);

        if (gainRageOnHit && ownerRageSystem != null)
        {
            ownerRageSystem.AddRage(rageGain);
        }

        // Nếu muốn kiếm khí biến mất khi trúng 1 enemy thì mở dòng này:
        Destroy(gameObject);
    }
}