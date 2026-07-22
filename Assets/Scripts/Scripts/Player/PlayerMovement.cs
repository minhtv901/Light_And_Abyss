using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public enum PlayerRouteForm
    {
        Normal,
        Purified,
        Dark
    }

    [Header("Movement Settings")]
    public float speed = 7f;
    public float jumpForce = 6f;

    [Header("Jump Settings")]
    public int maxJumpCount = 2;
    public float doubleJumpForceMultiplier = 0.9f;

    private int jumpCount = 0;

    [Header("Dash Settings")]
    public KeyCode dashKey = KeyCode.L;
    public float dashSpeed = 16f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.7f;
    public bool dashWithInputDirection = true;
    public bool dashInvincible = true;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.25f);
    public float groundCheckExtraDistance = 0.1f;

    [Header("Animator")]
    public Animator animator;

    [Header("Assist / Summon - Optional")]
    [Tooltip("Currently OFF by default. The transformed forms should keep the same combat style and only unlock ultimate.")]
    public bool enableAssistSummon = false;
    public KeyCode summonKey = KeyCode.Q;
    public GameObject strikerPrefab;
    public float summonCooldown = 2f;

    [Header("Dialogue Route Result")]
    public PlayerRouteForm currentRouteForm = PlayerRouteForm.Normal;

    [Tooltip("Purified result maps to GameManager.GameRoute.Light. Dark result maps to GameManager.GameRoute.Abyss.")]
    public bool updateGameManagerRoute = true;

    [Tooltip("After the boss dialogue result, the player keeps the same attacks and only unlocks route ultimate.")]
    public bool unlockUltimateAfterRouteChoice = true;

    [Tooltip("Optional. Enable only if your Animator has this trigger parameter.")]
    public bool playEvolutionAnimationTrigger = false;
    public string evolveTriggerName = "Evolve";

    [Tooltip("Optional. Keep OFF if all forms use exactly the same animation controller and attacks.")]
    public bool setAnimatorFormParameter = false;
    public string formAnimatorIntName = "Form";
    public int normalFormValue = 0;
    public int purifiedFormValue = 1;
    public int darkFormValue = 2;

    [Tooltip("Optional visual-only form objects. Leave null if you do not swap model visuals yet.")]
    public GameObject normalFormVisual;
    public GameObject purifiedFormVisual;
    public GameObject darkFormVisual;
    public bool swapFormVisuals = false;

    public bool debugRouteEvolution = true;

    [Header("Evolution Settings")]
    public float evolveDuration = 1.5f;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private PlayerAttack playerAttack;
    private PlayerHealth playerHealth;
    private PlayerGuard playerGuard;

    private float moveInput;
    private bool isGrounded;
    private bool wasGrounded;
    private bool hasCheckedGround;
    private bool isFacingRight = true;
    private bool isEvolving = false;
    private bool isDashing = false;
    private bool routeUltimateUnlocked = false;

    private float nextSummonTime = 0f;
    private float nextDashTime = 0f;
    private Coroutine evolveCoroutine;

    public bool IsGrounded => isGrounded;
    public bool IsFacingRight => isFacingRight;
    public bool IsEvolving => isEvolving;
    public bool IsDashing => isDashing;
    public bool RouteUltimateUnlocked => routeUltimateUnlocked;
    public bool HasRouteUltimate => routeUltimateUnlocked;
    public bool IsPurifiedRoute => currentRouteForm == PlayerRouteForm.Purified;
    public bool IsDarkRoute => currentRouteForm == PlayerRouteForm.Dark;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        playerAttack = GetComponent<PlayerAttack>();
        playerHealth = GetComponent<PlayerHealth>();
        playerGuard = GetComponent<PlayerGuard>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        ApplyRouteVisualAndAnimator(false);
    }

    private void Update()
    {
        CheckGround();

        if (isDashing)
        {
            moveInput = 0f;
            UpdateAnimator();
            return;
        }

        if (!CanControl())
        {
            moveInput = 0f;
            UpdateAnimator();
            return;
        }

        ReadMoveInput();
        Jump();
        Dash();
        Summon();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (isDashing)
        {
            return;
        }

        if (!CanControl())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
    }

    private bool CanControl()
    {
        if (isEvolving) return false;
        if (isDashing) return false;
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (playerAttack != null && playerAttack.IsAttacking) return false;

        bool guarding = playerGuard != null && playerGuard.IsGuarding;

        if (guarding)
        {
            return false;
        }

        return true;
    }

    private void ReadMoveInput()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (moveInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (moveInput < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Jump()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;
        if (jumpCount >= maxJumpCount) return;
        if (rb == null) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        float finalJumpForce = jumpForce;

        if (jumpCount > 0)
        {
            finalJumpForce *= doubleJumpForceMultiplier;
        }

        rb.AddForce(Vector2.up * finalJumpForce, ForceMode2D.Impulse);
        jumpCount++;

        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    private void Dash()
    {
        if (!Input.GetKeyDown(dashKey)) return;
        if (Time.time < nextDashTime) return;
        if (rb == null) return;

        float dashDirection = GetDashDirection();

        if (dashDirection > 0f && !isFacingRight)
        {
            Flip();
        }
        else if (dashDirection < 0f && isFacingRight)
        {
            Flip();
        }

        StartCoroutine(DashRoutine(dashDirection));
    }

    private float GetDashDirection()
    {
        if (!dashWithInputDirection)
        {
            return isFacingRight ? 1f : -1f;
        }

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            return Mathf.Sign(moveInput);
        }

        float rawInput = Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(rawInput) > 0.01f)
        {
            return Mathf.Sign(rawInput);
        }

        return isFacingRight ? 1f : -1f;
    }

    private IEnumerator DashRoutine(float direction)
    {
        isDashing = true;
        nextDashTime = Time.time + dashCooldown;

        float oldGravityScale = rb.gravityScale;

        SetDashInvincible(true);

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);

        if (animator != null)
        {
            animator.SetTrigger("Dash");
        }

        yield return new WaitForSeconds(dashDuration);

        if (rb != null)
        {
            rb.gravityScale = oldGravityScale;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        SetDashInvincible(false);
        isDashing = false;
    }

    private void SetDashInvincible(bool value)
    {
        if (!dashInvincible) return;

        gameObject.SendMessage(
            "SetInvincible",
            value,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private void CheckGround()
    {
        wasGrounded = isGrounded;

        Vector2 checkPos;

        if (playerCollider != null)
        {
            checkPos = new Vector2(
                playerCollider.bounds.center.x,
                playerCollider.bounds.min.y - groundCheckExtraDistance
            );
        }
        else
        {
            checkPos = new Vector2(transform.position.x, transform.position.y - 0.6f);
        }

        isGrounded = Physics2D.OverlapBox(
            checkPos,
            groundCheckSize,
            0f,
            groundLayer
        );

        if (hasCheckedGround && !wasGrounded && isGrounded)
        {
            jumpCount = 0;

            if (animator != null)
            {
                animator.SetTrigger("Land");
            }
        }

        hasCheckedGround = true;
    }

    private void UpdateAnimator()
    {
        if (animator == null || rb == null) return;

        animator.SetFloat("Speed", Mathf.Abs(moveInput));
        animator.SetFloat("YVelocity", rb.linearVelocity.y);
        animator.SetBool("IsGrounded", isGrounded);
    }

    private void Summon()
    {
        if (!enableAssistSummon) return;
        if (!Input.GetKeyDown(summonKey)) return;
        if (Time.time < nextSummonTime) return;

        SummonStriker();
        nextSummonTime = Time.time + summonCooldown;
    }

    // Kept as a no-op for compatibility with older scripts.
    // Weapon switching is intentionally removed from the route transformation flow.
    public void UpdateWeaponVisual()
    {
    }

    public void ApplyBossDialogueResult(bool darkWins)
    {
        if (darkWins)
        {
            EvolveToDarkRoute();
        }
        else
        {
            EvolveToPurifiedRoute();
        }
    }

    public void EvolveToPurifiedRoute()
    {
        StartRouteEvolution(GameManager.GameRoute.Light, PlayerRouteForm.Purified, "Purified");
    }

    public void EvolveToDarkRoute()
    {
        StartRouteEvolution(GameManager.GameRoute.Abyss, PlayerRouteForm.Dark, "Dark");
    }

    public void ChangeWeaponByRoute(GameManager.GameRoute route)
    {
        // Kept for compatibility with old code names.
        // It now only changes route state and ultimate availability, not weapon/combat style.
        if (route == GameManager.GameRoute.Light)
        {
            StartRouteEvolution(GameManager.GameRoute.Light, PlayerRouteForm.Purified, "Purified");
        }
        else if (route == GameManager.GameRoute.Abyss)
        {
            StartRouteEvolution(GameManager.GameRoute.Abyss, PlayerRouteForm.Dark, "Dark");
        }
    }

    private void StartRouteEvolution(GameManager.GameRoute route, PlayerRouteForm routeForm, string routeLabel)
    {
        if (updateGameManagerRoute && GameManager.instance != null)
        {
            GameManager.instance.currentRoute = route;
        }

        if (evolveCoroutine != null)
        {
            StopCoroutine(evolveCoroutine);
            evolveCoroutine = null;
            isEvolving = false;
        }

        evolveCoroutine = StartCoroutine(EvolveRoutine(routeForm, routeLabel));
    }

    private IEnumerator EvolveRoutine(PlayerRouteForm routeForm, string routeLabel)
    {
        isEvolving = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (animator != null && playEvolutionAnimationTrigger && !string.IsNullOrEmpty(evolveTriggerName))
        {
            animator.ResetTrigger(evolveTriggerName);
            animator.SetTrigger(evolveTriggerName);
        }

        if (debugRouteEvolution)
        {
            Debug.Log($"ĐANG MỞ KHÓA ROUTE: {routeLabel} - giữ nguyên lối đánh, chỉ mở ultimate.");
        }

        yield return new WaitForSeconds(evolveDuration);

        currentRouteForm = routeForm;

        if (unlockUltimateAfterRouteChoice)
        {
            routeUltimateUnlocked = true;
        }

        ApplyRouteVisualAndAnimator(true);
        NotifyRouteChanged(routeLabel);

        if (debugRouteEvolution)
        {
            Debug.Log($"ROUTE READY: {routeLabel} | Ultimate unlocked = {routeUltimateUnlocked}");
        }

        isEvolving = false;
        evolveCoroutine = null;
    }

    private void ApplyRouteVisualAndAnimator(bool notify)
    {
        if (swapFormVisuals)
        {
            if (normalFormVisual != null)
                normalFormVisual.SetActive(currentRouteForm == PlayerRouteForm.Normal);

            if (purifiedFormVisual != null)
                purifiedFormVisual.SetActive(currentRouteForm == PlayerRouteForm.Purified);

            if (darkFormVisual != null)
                darkFormVisual.SetActive(currentRouteForm == PlayerRouteForm.Dark);
        }

        if (animator != null && setAnimatorFormParameter && !string.IsNullOrEmpty(formAnimatorIntName))
        {
            int formValue = normalFormValue;

            if (currentRouteForm == PlayerRouteForm.Purified)
                formValue = purifiedFormValue;
            else if (currentRouteForm == PlayerRouteForm.Dark)
                formValue = darkFormValue;

            animator.SetInteger(formAnimatorIntName, formValue);
        }
    }

    private void NotifyRouteChanged(string routeLabel)
    {
        // Optional hooks for your future skill/ultimate scripts.
        // These do not require receiver scripts, so they are safe for the demo.
        gameObject.SendMessage("OnRouteUltimateUnlocked", currentRouteForm, SendMessageOptions.DontRequireReceiver);
        gameObject.SendMessage("OnPlayerRouteChanged", currentRouteForm, SendMessageOptions.DontRequireReceiver);
        gameObject.SendMessage("UnlockUltimate", routeLabel, SendMessageOptions.DontRequireReceiver);
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private void SummonStriker()
    {
        if (strikerPrefab == null) return;

        Vector3 spawnPos = transform.position + new Vector3(isFacingRight ? 1.5f : -1.5f, 0.5f, 0f);
        GameObject striker = Instantiate(strikerPrefab, spawnPos, Quaternion.identity);
        Destroy(striker, 1f);

        Debug.Log("Assist time!");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Collider2D col = GetComponent<Collider2D>();
        Vector2 groundPos;

        if (col != null)
        {
            groundPos = new Vector2(
                col.bounds.center.x,
                col.bounds.min.y - groundCheckExtraDistance
            );
        }
        else
        {
            groundPos = new Vector2(transform.position.x, transform.position.y - 0.6f);
        }

        Gizmos.DrawWireCube(groundPos, groundCheckSize);
    }
}
