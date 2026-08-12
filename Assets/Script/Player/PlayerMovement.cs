using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Stats Settings (เลือด & สเตมิน่า)")]
    public int maxHealth = 100;
    private int currentHealth;

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
    private Vector3 currentVelocity;
    private Vector3 moveInput;

    [Header("Dash Settings")]
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float dashSpeedMultiplier = 3f;

    private bool isDashing = false;
    private bool canDash = true;
    [HideInInspector] public bool isInvincible = false;

    [Header("References")]
    public Animator animator;
    public Transform aimAnchor;
    private Camera mainCam;
    private Rigidbody rb;
    private PlayerCombat combatScript; // 🌟 เพิ่มตัวแปรรับสคริปต์ยิงปืน

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        combatScript = GetComponent<PlayerCombat>(); // 🌟 หาว่ามีสคริปต์ยิงปืนไหม

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
        if (isDashing) return;

        if (currentStamina < maxStamina)
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

        // 🌟 1. แก้ไขระบบเดินให้อิงตามหน้ากล้อง (Camera-Relative Movement)
        Vector3 camForward = mainCam.transform.forward;
        Vector3 camRight = mainCam.transform.right;

        // ล็อคแกน Y ไว้ไม่ให้ตัวละครบินขึ้นฟ้าถ้ากล้องเงยหน้า
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // คำนวณทิศการเดินใหม่ = (หน้ากล้อง * แกนตั้ง) + (ข้างกล้อง * แกนนอน)
        moveInput = (camForward * moveZ + camRight * moveX).normalized;

        if (animator != null)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
        }

        HandleFacingDirection();

        if (Input.GetKeyDown(KeyCode.Space) && canDash && moveInput.magnitude > 0.1f && currentStamina >= dashStaminaCost)
        {
            StartCoroutine(DashRoutine());
        }
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        Vector3 targetVelocity = moveInput * baseSpeed;

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
        bool isAiming = combatScript != null && combatScript.isAiming; // 🌟 เช็กว่ากำลังกดคลิกขวาเล็งอยู่ไหม

        // 🌟 2. แยกเงื่อนไขการหันหน้าไม่ให้ทะเลาะกัน
        if (isAiming)
        {
            // ถ้ากำลังเล็งปืน ให้หันหน้าตามทิศที่กล้องมอง
            aimDirection = mainCam.transform.forward;
        }
        else
        {
            // ถ้าเดินถือขวานมือเปล่า ให้หันหน้าตามเมาส์ปกติ
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

            if (groundPlane.Raycast(ray, out float rayDistance))
            {
                Vector3 mousePoint = ray.GetPoint(rayDistance);
                aimDirection = (mousePoint - transform.position).normalized;
            }
        }

        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude > 0.001f) aimDirection.Normalize();

        if (aimAnchor != null)
        {
            aimAnchor.forward = aimDirection;
        }

        if (animator != null)
        {
            float angle = Mathf.Atan2(aimDirection.x, aimDirection.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            float snappedAimX = Mathf.Sin(snappedAngle * Mathf.Deg2Rad);
            float snappedAimZ = Mathf.Cos(snappedAngle * Mathf.Deg2Rad);

            // ถ้ากำลังเล็งเป้าปืน ไม่ต้องให้ตัวละครหันแบบกระตุก 8 ทิศ
            if (isAiming)
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

                if (dotProduct < -0.2f)
                {
                    animator.SetFloat("AnimSpeed", -1f); // ถอยหลัง
                }
                else
                {
                    animator.SetFloat("AnimSpeed", 1f); // เดินหน้า
                }
            }
            else
            {
                animator.SetFloat("AnimSpeed", 1f);
            }
        }
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;
        isInvincible = true;

        currentStamina -= dashStaminaCost;
        if (staminaSlider != null) staminaSlider.value = currentStamina;

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
        if (isInvincible)
        {
            Debug.Log("แดชหลบได้! ผู้เล่นเป็นอมตะ ไม่โดนดาเมจ");
            return;
        }

        currentHealth -= damage;
        Debug.Log($"<color=red>ผู้เล่นโดนโจมตี {damage} ดาเมจ! เลือดเหลือ {currentHealth}</color>");

        if (healthSlider != null)
        {
            healthSlider.value = currentHealth;
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("<color=black>ผู้เล่นตาย (Game Over)</color>");
    }
}