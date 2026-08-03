using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Collider meleeHitboxCollider; // ลาก Sphere Collider จากวัตถุ MeleeHitbox มาใส่

    private bool isAttacking = false;
    private PlayerMovement playerMovement;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();

        if (meleeHitboxCollider != null)
        {
            meleeHitboxCollider.enabled = false;
        }
    }

    void Update()
    {
        // โจมตีเมื่อคลิกซ้าย (ห้ามตีถ้ากำลังแดช หรือกำลังตีอยู่แล้ว)
        if (Input.GetMouseButtonDown(0) && !isAttacking && (playerMovement == null || !playerMovement.isInvincible))
        {
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        isAttacking = true;
        animator.SetTrigger("Attack");
    }

    // --- ฟังก์ชัน 2 ตัวนี้จะถูกเรียกจาก Animation Event บนไทม์ไลน์ ---

    public void OnAttackActive()
    {
        if (meleeHitboxCollider != null)
            meleeHitboxCollider.enabled = true;
    }

    public void OnAttackDeactive()
    {
        if (meleeHitboxCollider != null)
            meleeHitboxCollider.enabled = false;

        isAttacking = false;
    }
}