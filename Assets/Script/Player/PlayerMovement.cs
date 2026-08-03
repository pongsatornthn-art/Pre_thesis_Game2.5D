using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
    }

    void Update()
    {
        if (isDashing) return;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        moveInput = new Vector3(moveX, 0f, moveZ).normalized;

        if (animator != null)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
        }

        HandleFacingDirection();

        if (Input.GetKeyDown(KeyCode.Space) && canDash && moveInput.magnitude > 0.1f)
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

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 mousePoint = ray.GetPoint(rayDistance);
            Vector3 aimDirection = (mousePoint - transform.position).normalized;
            aimDirection.y = 0f; // ล็อกแกน Y ไม่ให้ก้มเงย

            if (aimAnchor != null)
            {
                aimAnchor.forward = aimDirection;
            }

            if (animator != null)
            {
                animator.SetFloat("AimX", aimDirection.x);
                animator.SetFloat("AimZ", aimDirection.z);

                // --- ระบบเช็กการเดินถอยหลังด้วย Dot Product ---
                if (moveInput.magnitude > 0)
                {
                    // เทียบทิศที่เดิน (moveInput) กับทิศที่หันหน้า (aimDirection)
                    float dotProduct = Vector3.Dot(moveInput.normalized, aimDirection.normalized);

                    // ถ้าค่าน้อยกว่า -0.2 แสดงว่าทิศทางมันส่วนทางกัน (เดินถอยหลัง)
                    if (dotProduct < -0.2f)
                    {
                        animator.SetFloat("AnimSpeed", -1f); // เล่นแอนิเมชันย้อนกลับ
                    }
                    else
                    {
                        animator.SetFloat("AnimSpeed", 1f); // เล่นแอนิเมชันปกติ
                    }
                }
                else
                {
                    animator.SetFloat("AnimSpeed", 1f); // ถ้ายืนนิ่งให้รีเซ็ตความเร็วเป็นปกติ
                }
            }
        }
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;
        isInvincible = true;

        Vector3 dashVelocity = moveInput * (baseSpeed * dashSpeedMultiplier);
        rb.linearVelocity = new Vector3(dashVelocity.x, rb.linearVelocity.y, dashVelocity.z);

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        isInvincible = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
}