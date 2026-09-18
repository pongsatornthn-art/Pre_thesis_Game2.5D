using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// กระสุนปืนมาตรฐาน: เคลื่อนที่แนวตรงด้วยความเร็วคงที่
    /// เมื่อกระทบเป้าหมายจะสร้างความเสียหาย (IDamageable) หรือเสกสะเก็ดไฟบนกำแพง
    /// </summary>
    public class BulletProjectile : ProjectileBase
    {
        protected override Vector3 StepMove(float dt)
        {
            // กระสุนปืนทั่วไปจะพุ่งตรงไปข้างหน้าตามทิศทางแนวนอนด้วยความเร็วคงที่
            return direction * (speed * dt);
        }

        protected override void OnImpact(RaycastHit hit)
        {
            // ค้นหาคอมโพเนนต์ที่รับดาเมจได้จากวัตถุที่ชนหรือพ่อแม่ของมัน
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                int dmg = gun != null ? gun.damage : 20;
                float kb = gun != null ? gun.knockback : 0f;

                // สร้างความเสียหายและแรงกระเด็นตามสเปกของปืน
                damageable.TakeDamage(dmg, kb);

                // หากมี Prefab เลือด/เอฟเฟกต์โดนเนื้อ ให้ดึงจากพูลมาแสดง ณ จุดกระทบ
                if (gun != null && gun.impactFleshPrefab != null)
                {
                    Quaternion rot = hit.normal != Vector3.zero ? Quaternion.LookRotation(hit.normal) : Quaternion.identity;
                    SimplePool.Get(gun.impactFleshPrefab, hit.point, rot);
                }
            }
            else
            {
                // ชนกำแพงหรือสิ่งแวดล้อม: ดึง Prefab สะเก็ดไฟจากพูลมาแสดงโดยหันตามแนวระนาบของพื้นผิว
                if (gun != null && gun.impactWallPrefab != null)
                {
                    Quaternion rot = hit.normal != Vector3.zero ? Quaternion.LookRotation(hit.normal) : Quaternion.identity;
                    SimplePool.Get(gun.impactWallPrefab, hit.point, rot);
                }
            }
        }
    }
}
