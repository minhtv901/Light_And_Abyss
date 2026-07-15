using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Move")]
    public float speed = 7f;
    public float lifeTime = 4f;
    public Vector2 moveDirection = Vector2.left;

    [Header("Arc / Arrow Setting")]
    public bool useGravityArc = false;
    public float gravityScale = 0.8f;
    public float arcUpBoost = 0.8f;

    [Header("Aim Fairness")]
    [Tooltip("Cho đạn lệch nhẹ để player dễ né hơn. Cung nên 4-8, cầu phép nên 6-12.")]
    public float aimSpreadDegrees = 6f;

    [Header("Visual")]
    public Transform visualRoot;
    public bool flipVisualByDirection = true;
    public bool invertVisualFlip = false;

    [Header("Rotate Visual")]
    public bool rotateVisualToVelocity = false;
    public float visualRotationOffset = 0f;

    [Header("Damage")]
    public int damage = 1;
    public bool destroyOnPlayerHit = true;

    private Rigidbody2D rb;
    private EnemyAI ownerEnemy;
    private Vector3 originalVisualScale;
    private bool hasHitPlayer = false;
    private bool initialized = false;

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
        if (!initialized) return;

        if (rb == null)
        {
            transform.position += (Vector3)(moveDirection.normalized * speed * Time.fixedDeltaTime);
            return;
        }

        // Đạn thường bay thẳng
        if (!useGravityArc)
        {
            rb.angularVelocity = 0f;
            rb.rotation = 0f;
            rb.linearVelocity = moveDirection.normalized * speed;
        }

        // Đạn cong thì để Rigidbody2D + Gravity tự xử lý, không set velocity liên tục
        UpdateVisualRotation();
    }

    public void Init(Vector2 direction, float projectileSpeed, int projectileDamage, float projectileLifeTime)
    {
        Init(direction, projectileSpeed, projectileDamage, projectileLifeTime, null);
    }

    public void Init(Vector2 direction, float projectileSpeed, int projectileDamage, float projectileLifeTime, EnemyAI owner)
    {
        moveDirection = ApplyAimSpread(direction.normalized);
        speed = projectileSpeed;
        damage = projectileDamage;
        lifeTime = projectileLifeTime;
        ownerEnemy = owner;
        initialized = true;

        SetupRigidbody();
        ApplyVisualDirection();
        UpdateVisualRotation();

        Destroy(gameObject, lifeTime);
    }

    private Vector2 ApplyAimSpread(Vector2 direction)
    {
        if (aimSpreadDegrees <= 0f) return direction;

        float randomAngle = Random.Range(-aimSpreadDegrees, aimSpreadDegrees);
        Quaternion rotation = Quaternion.Euler(0f, 0f, randomAngle);

        return rotation * direction;
    }

    private void SetupRigidbody()
    {
        if (rb == null) return;

        rb.angularVelocity = 0f;
        rb.freezeRotation = true;

        if (useGravityArc)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravityScale;

            Vector2 velocity = moveDirection.normalized * speed;
            velocity.y += arcUpBoost;

            rb.linearVelocity = velocity;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = moveDirection.normalized * speed;
        }
    }

    private void ApplyVisualDirection()
    {
        if (visualRoot == null) return;
        if (!flipVisualByDirection) return;

        Vector3 scale = originalVisualScale;

        bool shouldFlipLeft = moveDirection.x < 0f;

        if (invertVisualFlip)
        {
            shouldFlipLeft = !shouldFlipLeft;
        }

        if (shouldFlipLeft)
        {
            scale.x = -Mathf.Abs(originalVisualScale.x);
        }
        else
        {
            scale.x = Mathf.Abs(originalVisualScale.x);
        }

        visualRoot.localScale = scale;
    }

    private void UpdateVisualRotation()
    {
        if (!rotateVisualToVelocity) return;
        if (visualRoot == null) return;

        Vector2 velocity;

        if (rb != null)
        {
            velocity = rb.linearVelocity;
        }
        else
        {
            velocity = moveDirection.normalized * speed;
        }

        if (velocity.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        visualRoot.rotation = Quaternion.Euler(0f, 0f, angle + visualRotationOffset);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHitPlayer) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;

        hasHitPlayer = true;

        playerHealth.TakeDamage(damage);

        if (ownerEnemy != null)
        {
            ownerEnemy.SendMessage(
                "AddDamageDealtToPlayer",
                damage,
                SendMessageOptions.DontRequireReceiver
            );
        }

        if (destroyOnPlayerHit)
        {
            Destroy(gameObject);
        }
    }
}