using UnityEngine;

public class BreakableBarrel : MonoBehaviour
{
    [Header("Break Settings")]
    public int hp = 1;
    public float destroyDelay = 1.2f;

    [Header("Drop After Break")]
    public GameObject dropPrefab;
    public Transform dropPoint;

    private Animator animator;
    private Collider2D col;
    private bool isBroken = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
    }

    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        hp -= damage;

        if (hp <= 0)
        {
            Break();
        }
    }

    private void Break()
    {
        isBroken = true;

        if (col != null)
        {
            col.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger("Break");
        }

        Invoke(nameof(FinishBreak), destroyDelay);
    }

    private void FinishBreak()
    {
        if (dropPrefab != null)
        {
            Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position;
            Instantiate(dropPrefab, spawnPos, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}