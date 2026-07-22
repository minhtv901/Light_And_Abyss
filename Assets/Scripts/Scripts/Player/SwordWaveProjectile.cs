using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SwordWaveProjectile : MonoBehaviour
{
    [Header("Move")]
    public float speed = 8f;
    public float lifeTime = 1.2f;
    public Vector2 moveDirection = Vector2.right;

    [Header("Visual")]
    public Transform visualRoot;
    public bool rotateVisualToDirection = false;
    public float visualRotationOffset = 0f;
    public bool flipVisualByDirection = true;
    public bool invertVisualFlip = false;

    [Header("Damage")]
    public int damage = 1;

    [Tooltip("Optional. Leave empty to let trigger contacts decide. Recommended: Enemy + Boss.")]
    public LayerMask enemyLayer;

    [Header("Rage Gain")]
    public bool gainRageOnHit = true;
    public float rageGain = 0.35f;

    [Header("Hit Behaviour")]
    public bool destroyOnEnemyHit = true;

    [Tooltip("Keeps checking overlaps in case Unity misses OnTriggerEnter at high projectile speed.")]
    public bool scanOverlaps = true;

    private Rigidbody2D rb;
    private Collider2D projectileCollider;
    private RageSystem ownerRageSystem;
    private readonly HashSet<GameObject> hitTargets = new HashSet<GameObject>();
    private readonly Collider2D[] overlapResults = new Collider2D[24];
    private Vector3 originalVisualScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();

        if (projectileCollider != null)
            projectileCollider.isTrigger = true;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        if (visualRoot == null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) visualRoot = sr.transform;
        }

        if (visualRoot != null)
            originalVisualScale = visualRoot.localScale;
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
            rb.linearVelocity = velocity;
        else
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);

        if (scanOverlaps)
            TryHitCurrentOverlaps();
    }

    public void Init(Vector2 direction, int projectileDamage, RageSystem rageSystem, float rageAmount)
    {
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.right;

        moveDirection = direction.normalized;
        damage = projectileDamage;
        ownerRageSystem = rageSystem;
        rageGain = rageAmount;

        ApplyVisualDirection();
    }

    private void ApplyVisualDirection()
    {
        if (visualRoot == null) return;

        if (rotateVisualToDirection && moveDirection.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            visualRoot.rotation = Quaternion.Euler(0f, 0f, angle + visualRotationOffset);
            return;
        }

        if (!flipVisualByDirection) return;
        if (Mathf.Abs(moveDirection.x) < 0.05f) return;

        Vector3 scale = originalVisualScale;
        bool movingRight = moveDirection.x > 0f;

        if (invertVisualFlip)
            movingRight = !movingRight;

        scale.x = movingRight
            ? Mathf.Abs(originalVisualScale.x)
            : -Mathf.Abs(originalVisualScale.x);

        visualRoot.localScale = scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHitCurrentOverlaps()
    {
        if (projectileCollider == null || !projectileCollider.enabled) return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;

        if (enemyLayer.value != 0)
        {
            filter.useLayerMask = true;
            filter.SetLayerMask(enemyLayer);
        }

        int count = projectileCollider.Overlap(filter, overlapResults);

        for (int i = 0; i < count; i++)
        {
            TryHit(overlapResults[i]);
            overlapResults[i] = null;

            if (this == null) return;
        }
    }

    private void TryHit(Collider2D other)
    {
        if (other == null) return;

        if (enemyLayer.value != 0 && !IsInLayerMask(other.gameObject.layer, enemyLayer))
            return;

        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();

        if (enemy != null)
        {
            DamageTarget(enemy.gameObject, () => enemy.TakeDamage(damage));
            return;
        }

        StationaryGreenFlameBossAI boss = other.GetComponentInParent<StationaryGreenFlameBossAI>();

        if (boss != null)
        {
            DamageTarget(boss.gameObject, () => boss.TakeDamage(damage));
            return;
        }

        BreakableBarrel barrel = other.GetComponentInParent<BreakableBarrel>();

        if (barrel != null)
        {
            DamageTarget(barrel.gameObject, () => barrel.TakeDamage(damage));
        }
    }

    private void DamageTarget(GameObject targetObject, System.Action applyDamage)
    {
        if (targetObject == null) return;
        if (hitTargets.Contains(targetObject)) return;

        hitTargets.Add(targetObject);
        applyDamage?.Invoke();

        if (gainRageOnHit && ownerRageSystem != null)
            ownerRageSystem.AddRage(rageGain);

        if (destroyOnEnemyHit)
            Destroy(gameObject);
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
