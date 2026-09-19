using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// ตัวช่วยตรวจสอบว่าผู้เล่นกำลังเล็งเมาส์/ปากกระบอกปืนไปที่ศัตรูหรือไม่ (Plain C# Class)
    /// 
    /// เหตุผลการออกแบบ:
    /// 1. ตามข้อตกลง GDD ข้อ 3: เป้าเล็งจะเริ่มหุบเข้าหาความแม่นยำ 0 องศา ก็ต่อเมื่อ "หยุดเดิน + เล็งใส่ศัตรู"
    /// 2. ใช้ SphereCast แทน Raycast เส้นเดียว เพราะในเกม 2.5D มอนสเตอร์เป็นสไปรท์บิลบอร์ดแผ่นบาง
    ///    ถ้าใช้เส้นเรย์เดี่ยว ผู้เล่นจะทาบโดนยากมากจนรู้สึกหงุดหงิด
    /// 3. มี Grace Period 0.25 วินาที เพื่อป้องกันเป้ากระพริบเข้า-ออกเวลาศัตรูก้าวเดินหรือขยับตัว
    /// </summary>
    public class EnemyAimDetector
    {
        private readonly LayerMask enemyLayerMask;
        private readonly Transform ownerRoot;
        private readonly float gracePeriodDuration;
        private float lastAimAtEnemyTime = -10f;

        public EnemyAimDetector(LayerMask layerMask, Transform owner = null, float gracePeriod = 0.25f)
        {
            enemyLayerMask = layerMask;
            ownerRoot = owner;
            gracePeriodDuration = gracePeriod;
        }

        /// <summary>
        /// ตรวจสอบว่าทิศทางการเล็งจากปากกระบอกปืนชี้ไปโดนศัตรูหรือไม่
        /// </summary>
        public bool IsAimingAtEnemy(Vector3 muzzlePos, Vector3 flatDir, float range, float assistRadius = 0.6f)
        {
            int mask = enemyLayerMask.value != 0 ? enemyLayerMask.value : Physics.DefaultRaycastLayers;

            // ⚠️ ต้องใช้ SphereCastAll ไม่ใช่ SphereCast เส้นเดียว
            // เพราะ SphereCast คืนเฉพาะ "สิ่งที่ใกล้สุด" และทรงกลมรัศมี ~0.6 ที่เริ่มจากปลายกระบอกปืน
            // มักคาบเกี่ยวกับตัวผู้เล่นเอง ถ้าได้ตัวผู้เล่นมาก็จะตอบ false ทันทีทั้งที่มีผียืนอยู่ข้างหลัง
            // → กรวยจะไม่มีวันหุบเลยสักครั้ง (เล่นแล้วจะรู้สึกว่าปืนไม่แม่นตลอดกาล)
            RaycastHit[] hits = Physics.SphereCastAll(muzzlePos, assistRadius, flatDir, range, mask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider col = hits[i].collider;
                    if (col == null) continue;

                    // ข้ามตัวผู้เล่นเอง (รวมลูกหลานทุกชิ้น) แล้วไปดูตัวถัดไป ไม่ใช่เลิกตรวจทั้งหมด
                    // เทียบด้วย IsChildOf ไม่ใช่ transform.root เพราะถ้าเอา Player ไปใส่โฟลเดอร์ในซีน
                    // root จะกลายเป็นโฟลเดอร์ แล้วของทุกชิ้นในโฟลเดอร์นั้นจะถูกข้ามตามไปด้วย
                    if (ownerRoot != null && (col.transform == ownerRoot || col.transform.IsChildOf(ownerRoot))) continue;

                    bool isEnemy = enemyLayerMask.value != 0
                        ? ((1 << col.gameObject.layer) & enemyLayerMask.value) != 0
                        : col.GetComponentInParent<IDamageable>() != null;

                    if (isEnemy)
                    {
                        lastAimAtEnemyTime = Time.time;
                        return true;
                    }

                    // เจอของแข็งที่ไม่ใช่ศัตรู (กำแพง/ลัง) = เล็งไม่ถึงตัวผี ถือว่าไม่โฟกัส
                    return (Time.time - lastAimAtEnemyTime) <= gracePeriodDuration;
                }
            }

            // หากหลุดจากเป้า ให้ตรวจสอบว่ายังอยู่ในระยะ Grace Period หรือไม่
            return (Time.time - lastAimAtEnemyTime) <= gracePeriodDuration;
        }

        public bool IsAimingAtEnemy(Vector3 muzzlePos, Vector3 flatDir, float range)
        {
            return IsAimingAtEnemy(muzzlePos, flatDir, range, 0.6f);
        }
    }
}
