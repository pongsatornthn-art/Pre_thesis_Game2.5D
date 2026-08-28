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
                bool isHeavy = playerCombat.isCurrentAttackHeavy;
                weaponHitbox.SetupPayload(damage, knockback, isHeavy);
            }
            
            // สั่งเคลียร์ความจำ "รายชื่อมอนสเตอร์ที่โดนฟันรอบที่แล้ว" ทิ้ง ไม่งั้นฟันรอบสองจะไม่เข้า
            weaponHitbox.ResetHits();

            // เสก Effect รอยตวัดดาบ (ถ้ามีตั้งค่าไว้ใน ItemData ของอาวุธที่ถืออยู่)
            if (playerCombat != null)
            {
                GameObject currentVfx = playerCombat.GetCurrentWeaponVFX();
                if (currentVfx != null)
                {
                    // 1. คำนวณมุม 2D บนหน้าจอ (ทิศจากตัวละครไปหาเมาส์)
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(weaponHitbox.transform.position);
                    Vector3 mousePos = Input.mousePosition;
                    Vector2 aimDir2D = (mousePos - screenPos).normalized;
                    
                    // แปลงเป็นองศา (0=ขวา, 90=บน, 180=ซ้าย, -90=ล่าง)
                    float angle2D = Mathf.Atan2(aimDir2D.y, aimDir2D.x) * Mathf.Rad2Deg;
                    
                    // 2. ล็อคให้เป็น 8 ทิศทาง (Snap 45 องศา)
                    float snappedAngle = Mathf.Round(angle2D / 45f) * 45f;

                    // 3. เสกควันออกมา ให้หันหน้าหากล้องเสมอ (Billboard) 
                    // แล้วหมุนแกน Z เพื่อให้ฟันตรงตามทิศ 8 ทิศ
                    Quaternion vfxRotation = Camera.main.transform.rotation * Quaternion.Euler(0, 0, snappedAngle);
                    
                    // ดึงตำแหน่งมาใกล้กล้องนิดหน่อย (0.3 หน่วย) เพื่อป้องกันปัญหาภาพมอนสเตอร์บังภาพฟันดาบ (Sorting Issue)
                    Vector3 vfxPos = weaponHitbox.transform.position - (Camera.main.transform.forward * 0.3f);
                    
                    Instantiate(currentVfx, vfxPos, vfxRotation);
                }
            }

            // สั่งเปิด Hitbox 
            hitboxCollider.enabled = true;
            Debug.Log("<color=green>[AnimEvent] เปิด Hitbox โจมตี!</color>");
            
            // ป้องกันบัค Animation Event ไม่ทำงานตอนจบ (เฟรมข้าม)
            StopAllCoroutines();
            StartCoroutine(AutoDisableHitbox(0.5f));
        }
    }

    private System.Collections.IEnumerator AutoDisableHitbox(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (hitboxCollider != null && hitboxCollider.enabled)
        {
            hitboxCollider.enabled = false;
            Debug.Log("<color=orange>[AnimEvent/Fallback] ปิด Hitbox อัตโนมัติ (ป้องกันบัคกางค้าง)!</color>");
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
