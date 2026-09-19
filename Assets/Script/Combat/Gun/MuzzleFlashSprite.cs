using System.Collections;
using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// เล่นสไปรท์เปลวไฟปากกระบอกปืน (พิกเซลอาร์ต) แวบเดียวตอนยิง
    ///
    /// แยกจาก MuzzleFlashEffect เดิมเพราะตัวนั้นคุมได้แค่ Light
    /// (และเป็นไฟล์เก่าที่ระบบอื่นใช้อยู่ เลยไม่ไปแก้)
    ///
    /// 3 เรื่องที่ทำให้สไปรท์ไม่ดู "แปะ":
    /// 1. หันเข้ากล้องเสมอ แต่หมุนตามองศาที่ปืนเล็งบนจอ → เปลวชี้ไปทางเดียวกับกระสุนจริง
    /// 2. สุ่มพลิกบน-ล่าง + สุ่มขนาดเล็กน้อยทุกนัด → ไม่ใช่แสตมป์เดิมซ้ำ ๆ
    /// 3. อยู่แค่ 2-3 เฟรม (~0.06 วิ) สั้นจนตาไม่ทันตัดสินว่าเป็นภาพแปะ
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MuzzleFlashSprite : MonoBehaviour
    {
        [Header("เฟรมอนิเมชัน (ลากสไปรท์ที่สไลซ์แล้วใส่ตามลำดับ)")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("แต่ละเฟรมค้างกี่วินาที — 0.02 = 5 เฟรมจบใน 0.1 วิ")]
        [SerializeField] private float frameTime = 0.02f;

        [Header("ความหลากหลายในแต่ละนัด")]
        [Tooltip("สุ่มพลิกบน-ล่าง (ห้ามสุ่มหมุน เพราะเปลวต้องชี้ไปทางที่ยิงเสมอ)")]
        [SerializeField] private bool randomFlipVertical = true;

        [Tooltip("สุ่มขนาด ±กี่เปอร์เซ็นต์")]
        [Range(0f, 0.5f)][SerializeField] private float scaleJitter = 0.15f;

        [Header("การหันหน้า")]
        [Tooltip("ตัวหาจุดเล็งกลาง (เว้นว่าง = หาใน parent)")]
        [SerializeField] private AimResolver aimResolver;

        [Tooltip("ตัวอ่านว่าสไปรท์กำลังหันทิศไหนอยู่จริง (เว้นว่าง = หาใน parent)\n" +
                 "ถ้ามีตัวนี้ เปลวไฟจะใช้องศาของ 'ท่าที่แสดงอยู่จริง' แทนการคำนวณจากเมาส์")]
        [SerializeField] private SpriteFacingReader spriteFacing;

        [Tooltip("ปัดองศาให้ตรงกับ 8 ทิศ (ใช้เฉพาะตอนไม่มี Sprite Facing Reader)")]
        [SerializeField] private bool snapToSpriteDirections = true;

        [Tooltip("บิดองศาเพิ่มเอง (องศา) เผื่อปืนที่ศิลปินวาดไม่ได้เอียง 45 องศาเป๊ะ")]
        [Range(-45f, 45f)][SerializeField] private float angleOffset = 0f;

        private SpriteRenderer sr;
        private Camera cam;
        private Vector3 baseScale;
        private Coroutine playing;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            if (aimResolver == null) aimResolver = GetComponentInParent<AimResolver>();
            if (spriteFacing == null) spriteFacing = GetComponentInParent<SpriteFacingReader>();
            cam = Camera.main;

            // ซ่อนไว้ก่อน โผล่เฉพาะตอนยิง
            sr.enabled = false;
        }

        /// <summary>เล่นแฟลช 1 ครั้ง (GunFeedback เป็นคนเรียก)</summary>
        public void Play()
        {
            if (frames == null || frames.Length == 0) return;
            if (!gameObject.activeInHierarchy) return;

            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            FaceCameraAlongAim();

            // สุ่มให้แต่ละนัดไม่เหมือนกันเป๊ะ
            if (randomFlipVertical) sr.flipY = Random.value > 0.5f;
            float jitter = 1f + Random.Range(-scaleJitter, scaleJitter);
            transform.localScale = baseScale * jitter;

            sr.enabled = true;
            for (int i = 0; i < frames.Length; i++)
            {
                sr.sprite = frames[i];
                // ใช้เวลาจริง เผื่อจังหวะที่เกมหยุดเวลาอยู่
                yield return new WaitForSecondsRealtime(frameTime);
            }

            sr.enabled = false;
            transform.localScale = baseScale;
            playing = null;
        }

        /// <summary>
        /// หันแผ่นสไปรท์เข้าหากล้อง แล้วหมุนรอบแกนสายตาให้ชี้ไปทางเดียวกับที่เล็ง
        /// (ถ้าปล่อยให้บิลบอร์ดเฉย ๆ เปลวจะชี้ไปทางขวาตลอดแม้ตอนยิงไปทางซ้าย)
        /// </summary>
        private void FaceCameraAlongAim()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            transform.rotation = cam.transform.rotation;

            // ทางที่แม่นที่สุด: ใช้องศาของ "ท่าที่สไปรท์แสดงอยู่จริง"
            // ไม่ต้องเดาจากเมาส์ เพราะ Blend Tree เป็นคนตัดสินว่าจะโชว์รูปไหน
            // และเส้นแบ่งของมันไม่ตรงกับการปัดองศาทีละ 45 เป๊ะ ๆ
            if (spriteFacing != null && spriteFacing.HasFacing)
            {
                transform.Rotate(0f, 0f, spriteFacing.ScreenAngle + angleOffset, Space.Self);
                return;
            }

            if (aimResolver == null) return;

            Vector3 muzzlePos = transform.position;
            Vector3 aimDir = aimResolver.GetFlatAimDirection(muzzlePos);
            if (aimDir.sqrMagnitude < 0.0001f) return;

            // ปัดให้ตรงกับ 8 ทิศที่รูปใช้จริง
            // ไม่งั้นเมาส์ขยับนิดเดียว (แต่รูปยังเป็นท่าเดิม) เปลวไฟจะเชิดหลุดจากปากกระบอก
            if (snapToSpriteDirections) aimDir = DirectionalMuzzle.SnapToEightDirections(aimDir);

            Vector3 originScreen = cam.WorldToScreenPoint(muzzlePos);
            Vector3 targetScreen = cam.WorldToScreenPoint(muzzlePos + aimDir);
            Vector2 d = new Vector2(targetScreen.x - originScreen.x, targetScreen.y - originScreen.y);

            if (d.sqrMagnitude < 0.001f) return;

            float screenAngle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + angleOffset;
            transform.Rotate(0f, 0f, screenAngle, Space.Self);
        }
    }
}
