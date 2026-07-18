using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    [Header("Move")]
    public float speed = 8f;
    public float lifeTime = 3f;
    public Vector2 moveDirection = Vector2.left;

    [Header("Damage")]
    public int damage = 1;

    [Header("Hit")]
    public bool destroyOnPlayerHit = true;
    public LayerMask obstacleLayer;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

    public void Init(Vector2 direction, int projectileDamage, float projectileSpeed)
    {
        moveDirection = direction.normalized;
        damage = projectileDamage;
        speed = projectileSpeed;

        ApplyVisualDirection();
    }

    private void ApplyVisualDirection()
    {
        if (moveDirection.x == 0f) return;

        Vector3 scale = transform.localScale;

        if (moveDirection.x > 0f)
            scale.x = Mathf.Abs(scale.x);
        else
            scale.x = -Mathf.Abs(scale.x);

        transform.localScale = scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            if (playerHealth.IsInvincible)
            {
                return;
            }

            playerHealth.TakeDamage(damage);

            if (destroyOnPlayerHit)
            {
                Destroy(gameObject);
            }

            return;
        }

        if (IsInLayerMask(other.gameObject.layer, obstacleLayer))
        {
            Destroy(gameObject);
        }
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}