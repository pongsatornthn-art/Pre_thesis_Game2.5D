using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// คลาสฐานสำหรับกระสุนทุกชนิดในเกม (OOP: Open/Closed Principle)
    /// หากต้องการกระสุนชนิดใหม่ เช่น ปืนพลุ ระเบิด ให้สืบทอดคลาสนี้แล้ว override StepMove/OnImpact
    /// </summary>
    public abstract class ProjectileBase : MonoBehaviour, IPoolable
    {
        [Header("Projectile Base Settings")]
        [Tooltip("เลเยอร์ที่กระสุนสามารถชนได้")]
        [SerializeField] protected LayerMask hitLayerMask = ~0;

        protected Vector3 direction;
        protected RangedWeaponData gun;
        protected GameObject owner;
        protected Transform ownerRoot;
        protected float speed = 25f, radius = 0.08f, maxRange = 50f, travelled = 0f;
        protected bool isLaunched = false;

        /// <summary>เริ่มยิงกระสุนออกจาก origin ไปในทิศทาง flatDir (ห้าม override เพื่อรักษา lifecycle)</summary>
        public void Launch(Vector3 origin, Vector3 flatDir, RangedWeaponData gunData, GameObject ownerObj)
        {
            transform.position = origin;
            // บังคับให้กระสุนวิ่งในระนาบแนวนอนล้วน (y = 0) ป้องกันมุมกล้อง 2.5D หลอกตา
            flatDir.y = 0f;
            direction = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : Vector3.forward;
            gun = gunData;
            owner = ownerObj;
            // เก็บ transform ของผู้ยิงตรง ๆ ไม่ใช่ transform.root
            // (ถ้าใช้ root แล้วเอา Player ไปใส่โฟลเดอร์ในซีน กระสุนจะทะลุทุกอย่างที่อยู่โฟลเดอร์เดียวกัน)
            ownerRoot = ownerObj != null ? ownerObj.transform : null;
            speed = gunData != null ? gunData.projectileSpeed : 25f;
            radius = gunData != null ? gunData.projectileRadius : 0.08f;
            maxRange = gunData != null ? gunData.attackRange : 50f;
            travelled = 0f;
            isLaunched = true;

            transform.rotation = Quaternion.LookRotation(direction);
            OnLaunched();
        }

        protected virtual void OnLaunched() { }

        /// <summary>คำนวณระยะและทิศทางการเคลื่อนที่ในเฟรมนี้ (ลูกกระสุนแต่ละแบบกำหนดเอง)</summary>
        protected abstract Vector3 StepMove(float dt);

        /// <summary>จัดการเมื่อกระสุนกระทบสิ่งกีดขวางหรือศัตรู (สร้างดาเมจ/เสกเอฟเฟกต์)</summary>
        protected abstract void OnImpact(RaycastHit hit);

        protected virtual void Update()
        {
            if (!isLaunched) return;

            Vector3 delta = StepMove(Time.deltaTime);
            float stepDistance = delta.magnitude;
            if (stepDistance <= 0.0001f) return;

            Vector3 moveDir = delta / stepDistance;

            // กวาด SphereCast ล่วงหน้าตามระยะในเฟรมนี้เพื่อกัน Bullet Tunneling (ทะลุกำแพง) โดยไม่พึ่ง Rigidbody
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, moveDir, stepDistance, hitLayerMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit h = hits[i];
                    // ข้าม collider ของผู้ยิง
                    if (owner != null && (h.collider.gameObject == owner ||
                        (ownerRoot != null && (h.collider.transform == ownerRoot || h.collider.transform.IsChildOf(ownerRoot))))) continue;

                    // ⚠️ ข้าม collider ของตัวกระสุนเอง รวมถึง**ลูกหลานทุกชิ้น**
                    // (เช่นโมเดล 3D ที่ลากมาจาก asset pack แล้วมี Collider ติดมาด้วย)
                    // ของเดิมเช็คแค่ GetComponent บนก้อนที่ชน ซึ่งจับไม่ได้ถ้า Collider อยู่ที่ลูก
                    // ผลคือกระสุนชนตัวเองในเฟรมแรกแล้วหายทันที เห็นแค่แสงวาบที่ตัวผู้เล่น
                    if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;

                    // ข้ามกระสุนนัดอื่นที่บินอยู่
                    if (h.collider.GetComponentInParent<ProjectileBase>() != null) continue;

                    OnImpact(h);
                    Despawn();
                    return;
                }
            }

            transform.position += delta;
            travelled += stepDistance;
            if (travelled >= maxRange) Despawn();
        }

        protected void Despawn()
        {
            isLaunched = false;
            SimplePool.Return(gameObject);
        }

        public virtual void OnSpawn() { travelled = 0f; isLaunched = false; }
        public virtual void OnDespawn() { isLaunched = false; }
    }
}
