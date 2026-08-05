using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    public Animator animator; // 🟢 เพิ่มช่องให้ลาก Animator มาใส่
    public Transform attackPoint;

    [Header("Unarmed Stats (สเตตัสมือเปล่า)")]
    public int defaultDamage = 5;
    public float defaultRange = 1.5f;
    public float defaultCooldown = 0.5f;

    private float nextAttackTime = 0f;

    void Update()
    {
        // 🟢 1. เช็กคูลดาวน์ก่อน ว่าถึงเวลาที่ฟันรอบต่อไปได้หรือยัง
        if (Time.time >= nextAttackTime)
        {
            // ถ้ากดคลิกซ้าย
            if (Input.GetMouseButtonDown(0))
            {
                // ดึงข้อมูลอาวุธมาคำนวณคูลดาวน์ทันที เพื่อกันไม่ให้กดรัวๆ
                ItemData weapon = GetEquippedWeapon();
                float cooldown = weapon != null ? weapon.lightAttackCooldown : defaultCooldown;
                nextAttackTime = Time.time + cooldown;

                // 🟢 สั่งเล่นแอนิเมชันง้างดาบ (ยังไม่ทำดาเมจ)
                if (animator != null)
                {
                    animator.SetTrigger("Attack");
                }
                else
                {
                    // ถ้ายังไม่ได้ใส่ Animator ให้ข้ามไปทำดาเมจเลย (เอาไว้เทสตอนยังไม่มีแอนิเมชัน)
                    PerformStrikeDamage();
                }
            }
        }
    }

    // 🟢 2. ฟังก์ชันนี้ให้ Animation Event หรือตัวกลาง (Proxy) เรียกใช้ตอนดาบฟันลงมา
    public void PerformStrikeDamage()
    {
        ItemData weapon = GetEquippedWeapon();

        int damage = weapon != null ? weapon.damage : defaultDamage;
        float range = weapon != null ? weapon.attackRange : defaultRange;
        float knockback = weapon != null ? weapon.knockback : 0f;

        // สร้างวงกลม 3D เช็กว่าฟันโดนใครบ้าง
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, range);
        bool hitSomething = false;

        foreach (Collider enemy in hitEnemies)
        {
            IDamageable damageable = enemy.GetComponent<IDamageable>();

            // เช็กว่ามีหลอดเลือด และไม่ใช่ตัวเราเอง
            if (damageable != null && enemy.gameObject != this.gameObject)
            {
                // ส่งดาเมจและแรงกระเด็นไปให้ศัตรู
                damageable.TakeDamage(damage, knockback);
                hitSomething = true;
            }
        }

        if (hitSomething)
            Debug.Log("<color=green>ฟันโดนเป้าหมาย!</color>");
        else
            Debug.Log("ฟันวืดดดด...");
    }

    // ฟังก์ชันช่วยดึงข้อมูลอาวุธปัจจุบัน (เขียนแยกไว้จะได้ดูสะอาดตา)
    private ItemData GetEquippedWeapon()
    {
        if (Inventory.Instance != null)
        {
            return Inventory.Instance.currentEquippedItem;
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;

        // วาดวงกลมระยะโจมตีให้ดูในหน้า Scene 
        float currentRange = defaultRange;
        if (Application.isPlaying && GetEquippedWeapon() != null)
        {
            currentRange = GetEquippedWeapon().attackRange;
        }

        Gizmos.DrawWireSphere(attackPoint.position, currentRange);
    }
}