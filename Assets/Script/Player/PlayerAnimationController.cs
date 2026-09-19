using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private PlayerCombat playerCombat;
    private PlayerMovement playerMovement;

    private string currentAnim;
    private const float transitionDuration = 0.15f;

    void Start()
    {
        animator = GetComponent<Animator>();
        playerCombat = GetComponentInParent<PlayerCombat>();
        playerMovement = GetComponentInParent<PlayerMovement>();

        if (animator != null)
        {
            animator.SetFloat("AimZ", -1f);
        }
    }

    void Update()
    {
        // [เพิ่มโดย Claude 2026-09-19] เกมหยุดอยู่ = หยุดอัปเดตท่าทางด้วย
        // ไม่งั้นตอนเปิดเมนู Pause แล้วกด WASD ค้างไว้ สไปรท์จะยังหันหน้าเปลี่ยนทิศอยู่
        if (Time.timeScale == 0f) return;

        CheckAnimationState();
    }

    void CheckAnimationState()
    {
        // 1. เช็กว่าถือปืนไหม?
        bool isHoldingGun = false;
        if (Inventory.Instance != null && Inventory.Instance.currentEquippedItem != null)
        {
            isHoldingGun = Inventory.Instance.currentEquippedItem.itemType == ItemType.RangedWeapon;
        }

        // 2. ดึงค่าการเคลื่อนที่
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        bool isMoving = (moveX != 0 || moveY != 0); // เช็กว่ามีการกดปุ่มเดินไหม

        // 3. เช็กว่ากำลังยิงอยู่ไหม
        // 🌟 [Alien Shooter Update] ยิงได้เลยไม่ต้องคลิกขวาค้างก่อนแล้ว เลยตัด isAiming ออกจากเงื่อนไขยิง
        bool isShooting = Input.GetMouseButton(0) && isHoldingGun;

        // ------------------------------------------------------------------
        // 🌟 จุดที่เพิ่มเข้ามาเพื่อแก้แอนิเมชันค้าง (ส่งค่าให้ Blend Tree ทำงาน)
        // ------------------------------------------------------------------
        if (animator != null)
        {
            // ส่งค่า Speed (ถ้า Blend Tree ใช้ Speed คูณความเร็วแอนิเมชัน ถ้าเป็น 0 มันจะค้าง)
            animator.SetFloat("Speed", isMoving ? 1f : 0f);

            // ถ้าถือปืนอยู่ ปล่อยให้ PlayerMovement เป็นคนคุม AimX/AimZ (หันตามเมาส์ตลอด) ไม่ต้องมาแย่งกัน
            // ไม่ถือปืน (มือเปล่า/ดาบ) ค่อยหันหน้าตามปุ่ม W A S D ที่กด
            if (!isHoldingGun && isMoving)
            {
                // แปลงค่าให้สมูทขึ้นนิดหน่อย ป้องกันแอนิเมชันกระตุก
                Vector2 moveDir = new Vector2(moveX, moveY).normalized;
                animator.SetFloat("AimX", moveDir.x);
                animator.SetFloat("AimZ", moveDir.y);
            }
        }
        // ------------------------------------------------------------------

        // ----- ลำดับความสำคัญในการเล่นแอนิเมชัน -----
        if (isHoldingGun)
        {
            if (isShooting)
            {
                ChangeAnimation("Shoot_Tree");
            }
            // 🌟 [Alien Shooter Update] ตัดท่า "Aim_Idle" (ท่าเล็ง FPS เก่า หันหลังให้กล้อง) ออก
            // คลิกขวาไม่เปลี่ยนท่าอีกแล้ว ใช้ท่าปกติที่หันตามเมาส์ 8 ทิศแทน
            else if (isMoving)
            {
                ChangeAnimation("Gun_Movement");
            }
            else
            {
                ChangeAnimation("Idle_Gun_Tree");
            }
        }
        else
        {
            if (isMoving)
            {
                ChangeAnimation("Movement");
            }
            else
            {
                ChangeAnimation("Idle");
            }
        }
    }

    public void ChangeAnimation(string newAnim)
    {
        if (currentAnim == newAnim) return;

        if (animator != null)
        {
 
            animator.Play(newAnim);
            currentAnim = newAnim;
        }
    }
}