using System.Collections.Generic;
using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// อ่านว่า "ตอนนี้สไปรท์ตัวละครกำลังแสดงท่าหันทิศไหนอยู่จริง ๆ"
    ///
    /// ทำไมต้องอ่านจาก Animator แทนการคำนวณจากเมาส์:
    /// ตอนถือปืน PlayerMovement ส่งค่า AimX/AimZ แบบดิบ (ไม่ปัดองศา) เข้า Animator
    /// แล้วปล่อยให้ Blend Tree เป็นคนตัดสินเองว่าจะโชว์รูปไหน
    /// ซึ่งเส้นแบ่งของ Blend Tree ไม่ตรงกับการปัดองศาทีละ 45 เป๊ะ ๆ
    /// → ถ้าเอฟเฟกต์ไปคำนวณเองจากเมาส์ มันจะไม่ตรงกับรูปเวลาเล็งใกล้เส้นแบ่ง
    ///   (อาการ: สไปรท์หันเฉียงลงซ้าย แต่เปลวไฟพุ่งลงตรง ๆ)
    ///
    /// ตัวนี้จึงไปดูชื่อคลิปที่ Animator กำลังเล่นอยู่จริง แล้วแปลงเป็นทิศ
    /// = แหล่งความจริงแหล่งเดียว ทั้งตำแหน่งปลายกระบอกและองศาเปลวไฟอ้างอิงจากตรงนี้
    /// </summary>
    public class SpriteFacingReader : MonoBehaviour
    {
        // ⚠️ ต้องเรียง "ชื่อยาวก่อนชื่อสั้น" เสมอ
        // เพราะเทียบด้วย EndsWith ถ้าเอา "Left" ไว้ก่อน "BackLeft"
        // คลิป Idle_Gun_BackLeft จะไปเข้าเงื่อนไข "Left" ก่อน แล้วถูกอ่านเป็นทิศซ้ายตรง
        // (อาการ: ท่าเฉียงขึ้นซ้ายกับซ้ายตรงใช้ทิศเดียวกัน แก้ค่าองศาของทิศ 7 แล้วไม่มีผล)
        private static readonly string[] Suffixes =
        {
            "BackRight", "BackLeft", "FrontRight", "FrontLeft",   // ชื่อประกอบ 2 คำ ต้องมาก่อน
            "Back", "Front", "Right", "Left"                      // ชื่อคำเดียว ไว้ท้ายสุด
        };

        // แปลงเป็นหมายเลขทิศให้ตรงกับช่อง 8 ทิศของ DirectionalMuzzle
        // 0 = ขึ้น · 1 = เฉียงขึ้นขวา · 2 = ขวา · 3 = เฉียงลงขวา
        // 4 = ลง · 5 = เฉียงลงซ้าย · 6 = ซ้าย · 7 = เฉียงขึ้นซ้าย
        private static readonly int[] SuffixToIndex = { 1, 7, 3, 5, 0, 4, 2, 6 };

        [Header("อ้างอิง")]
        [Tooltip("Animator ของตัวละคร (เว้นว่าง = หาในลูก เช่นก้อน GFX)")]
        [SerializeField] private Animator animator;

        [Header("องศาบนจอของแต่ละท่า (ปรับได้ถ้าศิลปินวาดเอียงไม่เท่ากัน)")]
        [Tooltip("เรียงตามนี้: 0 ขึ้น · 1 เฉียงขึ้นขวา · 2 ขวา · 3 เฉียงลงขวา · 4 ลง · 5 เฉียงลงซ้าย · 6 ซ้าย · 7 เฉียงขึ้นซ้าย")]
        [SerializeField]
        private float[] screenAngles = { 90f, 45f, 0f, -45f, -90f, -135f, 180f, 135f };

        [Header("ดูเฉย ๆ ตอนเล่น (ไว้เช็คว่าอ่านท่าถูกไหม)")]
        [Tooltip("ชื่อคลิปที่กำลังเล่นอยู่จริง")]
        [SerializeField] private string debugClipName;
        [Tooltip("หมายเลขทิศที่อ่านได้ (0-7)")]
        [SerializeField] private int debugFacingIndex;

        private readonly List<AnimatorClipInfo> clipBuffer = new List<AnimatorClipInfo>();

        /// <summary>ทิศที่สไปรท์กำลังแสดงอยู่ (0-7) — เรียงเหมือนช่องของ DirectionalMuzzle</summary>
        public int FacingIndex { get; private set; }

        /// <summary>อ่านค่าได้แล้วหรือยัง (ถ้าไม่เจอ Animator จะเป็น false)</summary>
        public bool HasFacing { get; private set; }

        /// <summary>องศาบนจอของท่าที่กำลังแสดงอยู่</summary>
        public float ScreenAngle =>
            (screenAngles != null && FacingIndex < screenAngles.Length) ? screenAngles[FacingIndex] : 0f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (Time.timeScale == 0f) return;
            if (animator == null || !animator.isActiveAndEnabled) return;

            // หาคลิปที่มีน้ำหนักมากที่สุด = รูปที่ตาคนเห็นอยู่จริง
            animator.GetCurrentAnimatorClipInfo(0, clipBuffer);
            if (clipBuffer.Count == 0) return;

            string bestName = null;
            float bestWeight = -1f;
            for (int i = 0; i < clipBuffer.Count; i++)
            {
                if (clipBuffer[i].weight > bestWeight && clipBuffer[i].clip != null)
                {
                    bestWeight = clipBuffer[i].weight;
                    bestName = clipBuffer[i].clip.name;
                }
            }

            if (string.IsNullOrEmpty(bestName)) return;
            debugClipName = bestName;

            // เทียบท้ายชื่อคลิป (เช่น Idle_Gun_FrontLeft → FrontLeft)
            // ต้องไล่จากชื่อยาวไปสั้น ไม่งั้น "BackLeft" จะไปเข้าเงื่อนไข "Left" ก่อน
            for (int i = 0; i < Suffixes.Length; i++)
            {
                if (bestName.EndsWith(Suffixes[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    FacingIndex = SuffixToIndex[i];
                    debugFacingIndex = FacingIndex;
                    HasFacing = true;
                    return;
                }
            }
            // ชื่อไม่เข้าพวก (เช่นท่าฟัน/ท่าตาย) = คงทิศเดิมไว้ ไม่ต้องรีเซ็ต
        }
    }
}
