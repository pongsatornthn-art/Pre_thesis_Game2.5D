using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private PlayerCombat playerCombat;
    private PlayerMovement playerMovement;

    private string currentAnim;
    private const float transitionDuration = 0.15f;

    /// <summary>[Claude 2026-11-28] ฉากเดินอัตโนมัติ (OpeningWalkIn) สั่งทิศเดินแทนปุ่ม WASD — มีค่า = เล่นท่าเดินไปทางนั้นแม้ผู้เล่นโดนล็อก · null = ปกติ</summary>
    public static Vector2? ScriptedMove;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ScriptedMove = null;

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
        // [แก้โดย Claude 2026-11-28] ตัวละครโดนล็อก (คัทซีน/บทพูด/มินิเกม) = เดินไม่ได้จริง → ห้ามเล่นท่าเดิน
        // เดิม: กด W ค้างตอนชนทริกเกอร์คัทซีนแรก → ตัวยืนนิ่งแต่ขาเดินสับอยู่กับที่ (นาเจอ)
        // [2026-11-28 รอบ 2] + ถูกปิดสคริปต์เดิน (คัทซีน/FirstChaseStalker สั่ง playerMovement.enabled = false)
        //   เดิมไม่นับ → IsRunning ค้าง true (Update ไม่ทำงาน) → ท่าวิ่งสับอยู่กับที่
        bool locked = playerMovement != null &&
                      (!playerMovement.isActiveAndEnabled || playerMovement.isStunned || playerMovement.IsActionLocked);
        if (ScriptedMove.HasValue) { moveX = ScriptedMove.Value.x; moveY = ScriptedMove.Value.y; locked = false; }   // ฉากเดินอัตโนมัติ
        bool isMoving = !locked && (moveX != 0 || moveY != 0); // เช็กว่ามีการกดปุ่มเดินไหม

        // 3. เช็กว่ากำลังยิงอยู่ไหม
        // 🌟 [Alien Shooter Update] ยิงได้เลยไม่ต้องคลิกขวาค้างก่อนแล้ว เลยตัด isAiming ออกจากเงื่อนไขยิง
        bool isShooting = !locked && Input.GetMouseButton(0) && isHoldingGun;

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

        // [แก้โดย Claude 2026-11-08] กำลังเล่นท่าตีท่อ (PlayerCombat สั่ง Trigger Attack/HeavyAttack → Any State)
        // → ห้ามสั่ง Play ทับ ปล่อยให้ท่าตีเล่นจบ (มีทางกลับไป Idle เองตอนจบคลิป)
        // ล้าง currentAnim ไว้ → ตีจบแล้วเฟรมถัดไปสั่งท่ายืน/เดินที่ถูกต้องใหม่ (เดิมค้างท่าตีจนกว่าจะเปลี่ยนจากยืนเป็นเดิน)
        if (animator != null && IsInAttackState())
        {
            currentAnim = null;
            return;
        }

        // ----- ลำดับความสำคัญในการเล่นแอนิเมชัน -----

        // ยิงอยู่ → ท่ายิงสำคัญสุด (ทั้งตอนยืน เดิน และวิ่ง)
        if (isHoldingGun && isShooting)
        {
            ChangeAnimation("Shoot_Tree");
            return;
        }

        // [แก้โดย Claude 2026-11-25] กำลังวิ่ง → PlayerMovement เป็นคนคุมท่า (Run_Tree / Run_Melee_Tree / Run_Gun_Tree)
        // เดิมสคริปต์นี้สั่ง Play("Gun_Movement") / Play("Movement") ทับท่าวิ่งทุกครั้งที่ currentAnim ไม่ตรง
        // (เช่น เริ่มวิ่งพร้อมเริ่มเดิน หรือยิงเสร็จตอนวิ่งอยู่) แล้ว PlayerMovement ไม่สั่งซ้ำเพราะจำว่าสั่งไปแล้ว
        bool running = !locked && playerMovement != null && playerMovement.IsRunning;
        if (running)
        {
            // เพิ่งยิงเสร็จตอนวิ่งอยู่ → ให้ PlayerMovement สั่งท่าวิ่งกลับมาใหม่
            if (currentAnim == "Shoot_Tree") playerMovement.ForceRefreshMovementAnim();

            // ล้างไว้ → พอหยุดวิ่ง เฟรมถัดไปจะสั่งท่ายืน/เดินที่ถูกต้องใหม่
            currentAnim = null;
            return;
        }

        if (isHoldingGun)
        {
            // 🌟 [Alien Shooter Update] ตัดท่า "Aim_Idle" (ท่าเล็ง FPS เก่า หันหลังให้กล้อง) ออก
            // คลิกขวาไม่เปลี่ยนท่าอีกแล้ว ใช้ท่าปกติที่หันตามเมาส์ 8 ทิศแทน
            if (isMoving)
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

    private bool IsInAttackState()
    {
        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
        if (now.IsName("Test_Attack") || now.IsName("Heavy_Attack")) return true;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsName("Test_Attack") || next.IsName("Heavy_Attack")) return true;
        }
        return false;
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