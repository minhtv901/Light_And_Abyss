using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private int damage;
    private bool initialized;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    public void Init(Vector2 shootDirection, float projectileSpeed, int projectileDamage, float lifeTime)
    {
        direction = shootDirection.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        initialized = true;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<EnemyAI>() != null) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth hp = other.GetComponentInParent<PlayerHealth>();

            if (hp != null)
            {
                hp.TakeDamage(damage);
                Debug.Log("Projectile gây damage Player = " + damage);
            }
            else
            {
                other.gameObject.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
                Debug.LogWarning("Projectile chạm Player nhưng Player chưa có PlayerHealth.cs. Đã thử SendMessage TakeDamage.");
            }

            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Ground") || other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }
}