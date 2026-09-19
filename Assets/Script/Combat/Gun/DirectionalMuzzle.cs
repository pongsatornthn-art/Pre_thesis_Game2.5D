using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// ย้ายจุดปลายกระบอกปืนไปตามทิศที่สไปรท์ตัวละครกำลังแสดงอยู่ (8 ทิศ)
    ///
    /// ทำไมต้องมี:
    /// ตัวละครเป็นสไปรท์วาดมือแยก 8 ทิศ ปลายกระบอกในแต่ละภาพศิลปินวาดไว้คนละตำแหน่ง/คนละความสูง
    /// ถ้าใช้จุดเดียวที่โคจรรอบตัว จะตรงแค่บางทิศเท่านั้น จูนยังไงก็ไม่มีทางตรงครบ
    ///
    /// ตัวนี้จึงให้วาง "จุดอ้างอิง" ไว้ 8 จุด (ทิศละจุด) แล้วสลับใช้ตามทิศที่เล็งอยู่
    /// เป็นหลักการเดียวกับระบบแขนของโปรเจกต์นี้ที่ใช้จุด Elbow 6 จุด
    ///
    /// ⚠️ จุดอ้างอิงทั้ง 8 ต้องเป็นลูกของ "Player" (หรือก้อนที่ไม่หมุน)
    ///    ห้ามเป็นลูกของ AimAnchor เพราะ AimAnchor หมุนตามเมาส์ จุดจะโคจรตามไปด้วย
    /// </summary>
    public class DirectionalMuzzle : MonoBehaviour
    {
        [Header("จุดที่จะถูกย้าย")]
        [Tooltip("ก้อน MuzzlePoint (ตัวที่มี MuzzleFlash เป็นลูก และถูกใส่ในช่อง Muzzle ของ GunfireController)")]
        [SerializeField] private Transform muzzlePoint;

        [Header("จุดอ้างอิง 8 ทิศ — ต้องเป็นลูกของ Player ไม่ใช่ลูกของ AimAnchor")]
        [Tooltip("0 = เล็งขึ้น (หันหลังให้กล้อง)")]
        [SerializeField] private Transform up;
        [Tooltip("1 = เล็งเฉียงขึ้น-ขวา")]
        [SerializeField] private Transform upRight;
        [Tooltip("2 = เล็งขวา")]
        [SerializeField] private Transform right;
        [Tooltip("3 = เล็งเฉียงลง-ขวา")]
        [SerializeField] private Transform downRight;
        [Tooltip("4 = เล็งลง (หันหน้าเข้ากล้อง)")]
        [SerializeField] private Transform down;
        [Tooltip("5 = เล็งเฉียงลง-ซ้าย")]
        [SerializeField] private Transform downLeft;
        [Tooltip("6 = เล็งซ้าย")]
        [SerializeField] private Transform left;
        [Tooltip("7 = เล็งเฉียงขึ้น-ซ้าย")]
        [SerializeField] private Transform upLeft;

        [Header("อ้างอิง")]
        [Tooltip("ตัวอ่านว่าสไปรท์หันทิศไหนอยู่จริง (เว้นว่าง = หาในก้อนเดียวกัน/ลูก)\n" +
                 "ถ้ามีตัวนี้จะใช้ทิศจากรูปจริง แม่นกว่าการคำนวณจากเมาส์")]
        [SerializeField] private SpriteFacingReader spriteFacing;

        [Tooltip("ตัวหาจุดเล็งกลาง — ใช้สำรองตอนไม่มี Sprite Facing Reader")]
        [SerializeField] private AimResolver aimResolver;

        [Tooltip("เปิดไว้ตอนจูน = โชว์ลูกบอลสีที่จุดที่กำลังถูกใช้ในหน้าต่าง Scene")]
        [SerializeField] private bool debugGizmo = true;

        private Transform[] anchors;

        /// <summary>ทิศที่กำลังใช้อยู่ (0-7) ไว้ดูตอนดีบัก</summary>
        public int CurrentIndex { get; private set; }

        private void Awake()
        {
            anchors = new[] { up, upRight, right, downRight, down, downLeft, left, upLeft };
            if (aimResolver == null) aimResolver = GetComponentInParent<AimResolver>();
            if (spriteFacing == null) spriteFacing = GetComponentInChildren<SpriteFacingReader>();
        }

        // ใช้ LateUpdate เพื่อให้ทำงานหลังตัวละครหันหน้าเสร็จแล้วในเฟรมนั้น
        private void LateUpdate()
        {
            // เกมหยุดอยู่ = หยุดขยับจุดด้วย จะได้จูนตำแหน่งตอนหยุดเกมได้นิ่ง ๆ
            if (Time.timeScale == 0f) return;

            if (muzzlePoint == null) return;

            // ใช้ทิศจาก "รูปที่แสดงอยู่จริง" ก่อนเสมอ ถ้าไม่มีค่อยคำนวณจากเมาส์
            if (spriteFacing != null && spriteFacing.HasFacing)
            {
                CurrentIndex = spriteFacing.FacingIndex;
            }
            else
            {
                if (aimResolver == null) return;
                Vector3 dir = aimResolver.GetFlatAimDirection(transform.position);
                if (dir.sqrMagnitude < 0.0001f) return;
                CurrentIndex = DirectionToIndex(dir);
            }

            Transform anchor = anchors[CurrentIndex];
            if (anchor != null) muzzlePoint.position = anchor.position;
        }

        /// <summary>
        /// แปลงทิศเล็งเป็นหมายเลขทิศ 0-7 (สแนปทีละ 45 องศา ให้ตรงกับที่สไปรท์สลับภาพ)
        /// 0 องศา = +Z = เล็งขึ้น (ออกจากกล้อง) · 90 องศา = +X = เล็งขวา
        /// </summary>
        public static int DirectionToIndex(Vector3 flatDir)
        {
            float angle = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;   // -180..180
            if (angle < 0f) angle += 360f;                                      // 0..360
            return Mathf.RoundToInt(angle / 45f) % 8;
        }

        /// <summary>
        /// ปัดทิศเล็งให้ตรงกับ 1 ใน 8 ทิศที่สไปรท์ใช้จริง
        ///
        /// มีไว้ให้เอฟเฟกต์ (เช่นเปลวไฟปากกระบอก) ใช้ร่วมกัน
        /// เพราะรูปตัวละคร/ปืนถูกวาดล็อกไว้ทิศละ 45 องศา ถ้าเอฟเฟกต์หมุนตามเมาส์แบบต่อเนื่อง
        /// มันจะเชิดไม่ตรงกับปากกระบอกที่วาดไว้ เวลาเมาส์อยู่กึ่งกลางระหว่าง 2 ทิศ
        /// </summary>
        public static Vector3 SnapToEightDirections(Vector3 flatDir)
        {
            float angle = DirectionToIndex(flatDir) * 45f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        private void OnDrawGizmos()
        {
            if (!debugGizmo || !Application.isPlaying || anchors == null) return;

            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null) continue;
                // จุดที่กำลังใช้ = เขียว · จุดอื่น = เทาจาง
                Gizmos.color = (i == CurrentIndex) ? Color.green : new Color(1f, 1f, 1f, 0.25f);
                Gizmos.DrawSphere(anchors[i].position, 0.01f);
            }
        }
    }
}
