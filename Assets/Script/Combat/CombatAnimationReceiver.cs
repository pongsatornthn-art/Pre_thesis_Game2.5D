using UnityEngine;

/// <summary>
/// สคริปต์ล่ามแปลภาษา แปะไว้ที่เดียวกับ Animator
/// ทำหน้าที่รับสัญญาณ Event จากแอนิเมชัน แล้วส่งต่อไปเปิดปิด Hitbox
/// </summary>
[RequireComponent(typeof(Animator))]
public class CombatAnimationReceiver : MonoBehaviour
{
    [Tooltip("ลาก GameObject ที่เป็นตัวดาบล่องหน (MeleeHitbox) มาใส่ช่องนี้")]
    public MeleeHitbox weaponHitbox;
    
    [Tooltip("ลากตัว Player ตัวแม่ที่มี PlayerCombat มาใส่ช่องนี้ (เพื่อดึงค่าดาเมจ)")]
    public PlayerCombat playerCombat;

    private Collider hitboxCollider;

    private void Start()
    {
        if (weaponHitbox != null)
        {
            hitboxCollider = weaponHitbox.GetComponent<Collider>();
            if (hitboxCollider != null)
            {
                hitboxCollider.enabled = false; // ปิดไว้ก่อนตอนเริ่มเกม
            }
        }
    }

    /// <summary>
    /// ถูกเรียกโดย Animation Event ตอนที่ "ดาบตวัดไปข้างหน้า"
    /// </summary>
    public void OnAttackActive()
    {
        if (weaponHitbox != null && hitboxCollider != null)
        {
            // ดึงค่าพลังโจมตีของมีด/ดาบ จากช่องเก็บของ
            if (playerCombat != null)
            {
                int damage = playerCombat.GetCurrentWeaponDamage();
                float knockback = playerCombat.GetCurrentWeaponKnockback();
                weaponHitbox.SetupPayload(damage, knockback);
            }
            
            // สั่งเคลียร์ความจำ "รายชื่อมอนสเตอร์ที่โดนฟันรอบที่แล้ว" ทิ้ง ไม่งั้นฟันรอบสองจะไม่เข้า
            weaponHitbox.ResetHits();

            // สั่งเปิด Hitbox 
            hitboxCollider.enabled = true;
            Debug.Log("<color=green>[AnimEvent] เปิด Hitbox โจมตี!</color>");
        }
    }

    /// <summary>
    /// ถูกเรียกโดย Animation Event ตอนที่ "ตวัดดาบเสร็จแล้ว"
    /// </summary>
    public void OnAttackDeactive()
    {
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
            // รีเซ็ตความจำให้ฟันรอบหน้าโดนใหม่ได้ (ตัว MeleeHitbox ทำตอน OnEnable)
            Debug.Log("<color=red>[AnimEvent] ปิด Hitbox!</color>");
        }
    }
}
