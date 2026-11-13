using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Stats Settings (เลือด & สเตมิน่า)")]
    public int maxHealth = 100;
    private int currentHealth;
    public int CurrentHealth => currentHealth;

    public float maxStamina = 100f;
    private float currentStamina;
    public float staminaRegenRate = 15f;
    public float dashStaminaCost = 25f;

    [Header("UI References")]
    public Slider healthSlider;
    public Slider staminaSlider;

    [Header("Movement Settings")]
    public float baseSpeed = 5f;
    public float acceleration = 20f;
    public float deceleration = 25f;

    [Header("Sneak Settings (ย่องเบา)")]
    public float sneakSpeedMultiplier = 0.5f;
    [HideInInspector] public bool isSneaking = false;

    [Header("Walk / Run (เดิน / วิ่ง)")]
    public KeyCode runKey = KeyCode.LeftShift;
    [Range(0.2f, 1f)] public float walkSpeedMultiplier = 0.6f;
    public float runStaminaPerSecond = 20f;
    public float staminaRegenDelay = 0.8f;
    [Range(0f, 1f)] public float exhaustedRecoverRatio = 0.3f;

    [Header("Animator State Names (ต้องตรงกับชื่อกล่องใน Animator เป๊ะ ๆ)")]
    public string idleState = "Idle";
    public string idleGunState = "Idle_Gun_Tree";
    public string walkState = "Movement";          // เดิน มือเปล่า / อาวุธประชิด
    public string walkGunState = "Gun_Movement";   // เดิน ถือปืน
    public string runState = "Run_Tree";
    public string runMeleeState = "Run_Melee_Tree";
    public string runGunState = "Run_Gun_Tree";
    public int animatorLayer = 0;

    public bool IsRunning { get; private set; }
    public bool IsExhausted { get; private set; }
    public float CurrentStamina => currentStamina;
    public float StaminaRatio => maxStamina > 0f ? currentStamina / maxStamina : 0f;
    public float HealthRatio => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    private float lastStaminaUseTime = -99f;

    // ตัวแปรคุมสถานะแอนิเมชัน ไม่ให้สั่งเล่นซ้ำเฟรมเดิม
    private int lastCombinedState = -1;

    public bool TryUseStamina(float amount)
    {
        if (amount <= 0f) return true;
        if (currentStamina < amount) return false;
        SpendStamina(amount);
        return true;
    }

    private void SpendStamina(float amount)
    {
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;
        if (currentStamina <= 0.01f) IsExhausted = true;
        if (staminaSlider != null) staminaSlider.value = currentStamina;
    }

    [Header("Backward Settings (ถอยหลังช้าลง)")]
    [Range(0.1f, 1f)] public float backwardSpeedMultiplier = 0.6f;
    public float backwardStartDot = -0.2f;
    private float backwardFactor = 0f;

    private Vector3 currentVelocity;
    private Vector3 moveInput;

    [Header("Dash Settings")]
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float dashSpeedMultiplier = 3f;

    private bool isDashing = false;
    private bool canDash = true;
    [HideInInspector] public bool isInvincible = false;
    [HideInInspector] public bool isStunned = false;
    private bool isAttackPaused = false;

    [Header("References")]
    public Animator animator;
    public Transform aimAnchor;
    private Camera mainCam;
    private Rigidbody rb;
    private PlayerCombat combatScript;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        combatScript = GetComponent<PlayerCombat>();

        currentHealth = maxHealth;
        currentStamina = maxStamina;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (isDashing || isStunned || isAttackPaused) return;
        if (IsActionLocked) { IsRunning = false; return; }

        if (currentStamina < maxStamina && !IsRunning && Time.time - lastStaminaUseTime >= staminaRegenDelay)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

            if (staminaSlider != null)
            {
                staminaSlider.value = currentStamina;
            }
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        if (IsExhausted && currentStamina >= maxStamina * exhaustedRecoverRatio) IsExhausted = false;

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
        {
            isSneaking = !isSneaking;
        }

        Vector3 camForward = mainCam.transform.forward;
        Vector3 camRight = mainCam.transform.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        moveInput = (camForward * moveZ + camRight * moveX).normalized;

        if (animator != null)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
        }

        HandleFacingDirection();

        // ตรวจจับว่า กำลังหยุด, เดิน, หรือวิ่ง
        bool isMoving = moveInput.magnitude > 0.1f;
        bool wantRun = Input.GetKey(runKey) && isMoving && !IsExhausted && currentStamina > 0f;
        if (wantRun && isSneaking) isSneaking = false;

        // 0 = หยุดนิ่ง, 1 = เดิน, 2 = วิ่ง
        int targetMoveState = isMoving ? (wantRun ? 2 : 1) : 0;

        bool hasGun = combatScript != null && combatScript.HasRangedWeaponEquipped;
        bool hasMelee = combatScript != null && combatScript.GetCurrentWeaponDamage() != combatScript.defaultDamage && !hasGun;

        int weaponState = hasGun ? 2 : (hasMelee ? 1 : 0);
        int combinedState = (targetMoveState * 10) + weaponState;

        // ถ้ามีการเปลี่ยนท่าเดิน/วิ่ง หรือ สลับอาวุธ -> สั่ง Animator เปลี่ยนกล่องทันที
        if (combinedState != lastCombinedState)
        {
            PlayMovementState(targetMoveState, hasGun, hasMelee);
            lastCombinedState = combinedState;
        }

        IsRunning = wantRun;
        if (IsRunning) SpendStamina(runStaminaPerSecond * Time.deltaTime);

        if (animator != null)
        {
            animator.SetBool("IsRunning", IsRunning);
        }

        if (Input.GetKeyDown(KeyCode.Space) && canDash && moveInput.magnitude > 0.1f && currentStamina >= dashStaminaCost)
        {
            StartCoroutine(DashRoutine());
        }
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        if (isStunned || isAttackPaused || IsActionLocked)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        float walkSpeed = baseSpeed * walkSpeedMultiplier;
        float currentMaxSpeed = IsRunning ? baseSpeed : (isSneaking ? walkSpeed * sneakSpeedMultiplier : walkSpeed);

        if (combatScript != null && combatScript.isChargingMelee)
        {
            currentMaxSpeed *= combatScript.heavyChargeSpeedMultiplier;
        }

        if (combatScript != null && combatScript.isAiming && combatScript.HasRangedWeaponEquipped)
        {
            currentMaxSpeed *= combatScript.steadyAimSpeedMultiplier;
        }

        currentMaxSpeed *= Mathf.Lerp(1f, backwardSpeedMultiplier, backwardFactor);

        Vector3 targetVelocity = moveInput * currentMaxSpeed;

        if (moveInput.magnitude > 0)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);
    }

    private void HandleFacingDirection()
    {
        if (mainCam == null) return;

        Vector3 aimDirection = Vector3.zero;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        float aimHeight = aimAnchor != null ? aimAnchor.position.y : transform.position.y;
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, aimHeight, 0));

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 mousePoint = ray.GetPoint(rayDistance);
            aimDirection = (mousePoint - transform.position).normalized;
        }

        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude > 0.001f) aimDirection.Normalize();

        if (moveInput.sqrMagnitude > 0.01f && aimDirection.sqrMagnitude > 0.001f)
        {
            float dot = Vector3.Dot(moveInput.normalized, aimDirection);
            backwardFactor = Mathf.InverseLerp(backwardStartDot, -1f, dot);
        }
        else
        {
            backwardFactor = 0f;
        }

        if (aimAnchor != null)
        {
            aimAnchor.forward = aimDirection;
        }

        bool useRawAim = combatScript != null && combatScript.HasRangedWeaponEquipped;

        if (animator != null)
        {
            float angle = Mathf.Atan2(aimDirection.x, aimDirection.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            float snappedAimX = Mathf.Sin(snappedAngle * Mathf.Deg2Rad);
            float snappedAimZ = Mathf.Cos(snappedAngle * Mathf.Deg2Rad);

            if (useRawAim)
            {
                animator.SetFloat("AimX", aimDirection.x);
                animator.SetFloat("AimZ", aimDirection.z);
            }
            else
            {
                animator.SetFloat("AimX", snappedAimX);
                animator.SetFloat("AimZ", snappedAimZ);
            }

            if (moveInput.magnitude > 0)
            {
                float dotProduct = Vector3.Dot(moveInput.normalized, aimDirection.normalized);

                if (dotProduct < backwardStartDot)
                {
                    animator.SetFloat("AnimSpeed", -1f);
                }
                else
                {
                    animator.SetFloat("AnimSpeed", 1f);
                }
            }
            else
            {
                animator.SetFloat("AnimSpeed", 1f);
            }
        }
    }

    // ฟังก์ชันสั่งกระโดดข้ามกล่อง Animator (ใช้ชื่อ State ที่ตั้งได้ใน Inspector)
    private void PlayMovementState(int moveState, bool hasGun, bool hasMelee)
    {
        if (animator == null) return;

        string targetStateName;

        if (moveState == 2) // 🏃 วิ่ง
        {
            if (hasGun) targetStateName = runGunState;
            else if (hasMelee) targetStateName = runMeleeState;
            else targetStateName = runState;
        }
        else if (moveState == 1) // 🚶 เดิน
        {
            targetStateName = hasGun ? walkGunState : walkState;
        }
        else // 🧍 หยุดนิ่ง
        {
            targetStateName = hasGun ? idleGunState : idleState;
        }

        int hash = Animator.StringToHash(targetStateName);

        // ถ้าชื่อ State ไม่มีใน Animator ให้เตือนใน Console แทนที่จะเงียบไป
        if (!animator.HasState(animatorLayer, hash))
        {
            Debug.LogWarning($"[PlayerMovement] ไม่พบ State '{targetStateName}' ใน Animator Layer {animatorLayer} (เช็คชื่อใน Inspector)");
            return;
        }

        animator.CrossFadeInFixedTime(hash, 0.1f, animatorLayer);
    }

    // สั่งให้ Update เล่นท่าเดิน/ยืนใหม่ในเฟรมถัดไป (เรียกหลังโจมตี/สตัน/ล็อกแอ็คชั่นจบ)
    public void ForceRefreshMovementAnim()
    {
        lastCombinedState = -1;
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;
        isInvincible = true;

        SpendStamina(dashStaminaCost);

        Vector3 dashVelocity = moveInput * (baseSpeed * dashSpeedMultiplier);
        rb.linearVelocity = new Vector3(dashVelocity.x, rb.linearVelocity.y, dashVelocity.z);

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        isInvincible = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public void TakeDamage(int damage)
    {
        if (isInvincible) return;
        TakeRawDamage(damage);
    }

    public void TakeRawDamage(int damage)
    {
        currentHealth -= damage;

        GameEventBus.Publish(new PlayerDamagedEvent(damage, transform.position));

        if (healthSlider != null)
        {
            healthSlider.value = currentHealth;
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void SetHealthFromSave(int value)
    {
        currentHealth = Mathf.Clamp(value, 1, maxHealth);
        if (healthSlider != null) healthSlider.value = currentHealth;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        if (healthSlider != null) healthSlider.value = currentHealth;
    }

    public void ApplyStun(float duration)
    {
        if (!isInvincible && !isStunned)
        {
            StartCoroutine(StunRoutine(duration));
        }
    }

    private int actionLockCount;
    public bool IsActionLocked => actionLockCount > 0;
    public void PushActionLock() { actionLockCount++; moveInput = Vector3.zero; currentVelocity = Vector3.zero; if (animator != null) animator.SetFloat("Speed", 0f); }
    public void PopActionLock() { actionLockCount = Mathf.Max(0, actionLockCount - 1); ForceRefreshMovementAnim(); }

    public void ApplyAttackPause(float duration)
    {
        StartCoroutine(AttackPauseRoutine(duration));
    }

    private IEnumerator AttackPauseRoutine(float duration)
    {
        isAttackPaused = true;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }

        yield return new WaitForSeconds(duration);
        isAttackPaused = false;
        ForceRefreshMovementAnim();
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }

        yield return new WaitForSeconds(duration);

        isStunned = false;
        ForceRefreshMovementAnim();
    }

    private void Die()
    {
        Debug.Log("<color=black>ผู้เล่นตาย (Game Over)</color>");
    }
}