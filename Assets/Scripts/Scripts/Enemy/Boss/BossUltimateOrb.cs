using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossUltimateOrb : MonoBehaviour
{
    [Header("Move")]
    public float speed = 8f;
    public float lifeTime = 5f;

    [Header("Impact")]
    public LayerMask impactLayer;
    public GameObject explosionPrefab;
    public GameObject fireStripPrefab;

    [Header("Fire Strip")]
    public int fireDamagePerTick = 1;
    public float fireTickInterval = 0.6f;
    public float fireDuration = 3f;
    public float fireHeight = 1.2f;
    public float extraWidth = 0f;

    [Header("Visual")]
    public Transform visualRoot;
    public bool rotateVisualToDirection = false;
    public float visualRotationOffset = 0f;

    private Rigidbody2D rb;
    private Collider2D orbCollider;

    private Collider2D targetSurface;
    private Vector2 moveDirection = Vector2.down;
    private bool hasImpacted = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        orbCollider = GetComponent<Collider2D>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        if (orbCollider != null)
        {
            orbCollider.isTrigger = true;
        }

        if (visualRoot == null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();

            if (sr != null)
            {
                visualRoot = sr.transform;
            }
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

        ApplyVisualDirection();
    }

    public void Init(
        Collider2D surface,
        Vector2 direction,
        float orbSpeed,
        GameObject impactExplosionPrefab,
        GameObject stripPrefab,
        int damagePerTick,
        float tickInterval,
        float stripDuration,
        float stripHeight,
        LayerMask layerToImpact
    )
    {
        targetSurface = surface;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.down;
        }

        moveDirection = direction.normalized;
        speed = orbSpeed;

        explosionPrefab = impactExplosionPrefab;
        fireStripPrefab = stripPrefab;

        fireDamagePerTick = damagePerTick;
        fireTickInterval = tickInterval;
        fireDuration = stripDuration;
        fireHeight = stripHeight;

        impactLayer = layerToImpact;

        ApplyVisualDirection();
    }

    private void ApplyVisualDirection()
    {
        if (!rotateVisualToDirection) return;
        if (visualRoot == null) return;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        visualRoot.rotation = Quaternion.Euler(0f, 0f, angle + visualRotationOffset);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasImpacted) return;
        if (other == null) return;

        if (!IsInLayerMask(other.gameObject.layer, impactLayer))
        {
            return;
        }

        if (targetSurface != null && other != targetSurface)
        {
            return;
        }

        Impact(other);
    }

    private void Impact(Collider2D surface)
    {
        if (hasImpacted) return;
        hasImpacted = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Bounds bounds = surface.bounds;

        Vector3 explosionPosition = new Vector3(
            bounds.center.x,
            bounds.max.y,
            0f
        );

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab,
                explosionPosition,
                Quaternion.identity
            );

            BossUltimateExplosion explosionScript = explosion.GetComponent<BossUltimateExplosion>();

            if (explosionScript != null)
            {
                explosionScript.Init(
                    surface,
                    fireStripPrefab,
                    fireDamagePerTick,
                    fireTickInterval,
                    fireDuration,
                    fireHeight
                );
            }
        }

        Destroy(gameObject);
    }

    private void SpawnFireStrip(Collider2D surface)
    {
        if (fireStripPrefab == null) return;
        if (surface == null) return;

        Bounds bounds = surface.bounds;

        float width = bounds.size.x + extraWidth;
        Vector2 stripSize = new Vector2(width, fireHeight);

        Vector3 stripPosition = new Vector3(
            bounds.center.x,
            bounds.max.y + fireHeight * 0.5f,
            0f
        );

        GameObject strip = Instantiate(fireStripPrefab, stripPosition, Quaternion.identity);

        BossFireStripDOT dot = strip.GetComponent<BossFireStripDOT>();

        if (dot != null)
        {
            dot.SetData(
                fireDamagePerTick,
                fireTickInterval,
                fireDuration,
                stripSize
            );
        }
        else
        {
            BoxCollider2D box = strip.GetComponent<BoxCollider2D>();

            if (box != null)
            {
                box.isTrigger = true;
                box.size = stripSize;
            }

            SpriteRenderer sr = strip.GetComponent<SpriteRenderer>();

            if (sr != null)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = stripSize;
            }

            Destroy(strip, fireDuration);
        }
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}