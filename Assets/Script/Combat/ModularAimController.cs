using UnityEngine;

/// <summary>
/// หมุน "แกนปืน" (gunPivot) แบบ 2.5D billboard — บังคับด้วยโค้ดล้วน ไม่ยุ่งกับ Animator ตัวละคร
/// - หัน pivot เข้าหากล้องเสมอ
/// - หมุน barrel แค่ "ส่วนโยก" ตามเมาส์ (รูปปืนวาดชี้ตรงกลางทิศอยู่แล้ว เลยไม่หมุนเต็มองศา = กันหมุนซ้อน)
/// - การเลือกรูป/มิเรอร์/โชว์-ซ่อน ปืน อยู่ที่ WeaponSpriteRig (ตัวนั้นทำงานเองได้ ไม่ต้องพึ่งตัวนี้)
///
/// การจัดวาง: แปะสคริปต์นี้ที่ GunPivot, ให้ GunSprite (มี SpriteRenderer + WeaponSpriteRig) เป็นลูกของ GunPivot
/// </summary>
public class ModularAimController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("จุดหมุนของปืน (เว้นว่าง = ก้อนที่แปะสคริปต์นี้)")]
    public Transform gunPivot;
    [Tooltip("จุดอ้างอิงศูนย์กลางตัวละคร (เว้นว่าง = ใช้ gunPivot)")]
    public Transform aimOrigin;
    [Tooltip("WeaponSpriteRig ของปืน (เว้นว่าง = หาในลูก)")]
    public WeaponSpriteRig spriteRig;
    public Camera mainCam;

    [Header("Aim Feel")]
    [Tooltip("ความเร็วหมุนตามเมาส์ (มาก = ติดมือ, น้อย = หน่วงนุ่ม)")]
    public float rotateLerpSpeed = 20f;
    [Tooltip("องศาโยกสูงสุดของกระบอกปืน — ล็อกไม่ให้ปืนตั้งชันเกินไป (ทิศกว้าง 60°, ครึ่งนึง = 30)")]
    public float maxBarrelTilt = 18f;
    [Tooltip("ความเร็วเลื่อนตำแหน่งปืนตอนเปลี่ยนทิศ (hand offset จาก WeaponSpriteRig)")]
    public float offsetLerpSpeed = 15f;
    [Tooltip("เปิด = ปืนย้ายไปฝั่งที่หันเหมือนแขนจริง / ปิด = ปืนอยู่กลางตัวหมุนอย่างเดียว")]
    public bool useHandOffset = true;

    [Header("Aim Output (อ่านอย่างเดียว ดูตอนเทส)")]
    public float screenAimAngle;   // 0 = ขวา, 90 = บน, 180 = ซ้าย, -90 = ล่าง
    public float snappedAngle;     // snap ทีละ 60° (6 ทิศ)
    public float wobble;           // screenAimAngle - snappedAngle (-30..30)

    private float currentZ;
    private Vector3 homeLocalPos;
    private Vector2 currentOffset;
    private bool warnedPivot, warnedCam;

    void Start()
    {
        if (gunPivot == null) gunPivot = transform;
        if (aimOrigin == null) aimOrigin = gunPivot;
        if (spriteRig == null) spriteRig = GetComponentInChildren<WeaponSpriteRig>();
        if (mainCam == null) mainCam = Camera.main;

        // จำตำแหน่ง local ตั้งต้นของ pivot ไว้ แล้วบวก hand offset เข้าไปทีหลัง
        homeLocalPos = gunPivot.localPosition;
    }

    void LateUpdate()
    {
        if (gunPivot == null)
        {
            if (!warnedPivot) { Debug.LogWarning("[ModularAimController] ไม่มี Gun Pivot"); warnedPivot = true; }
            return;
        }
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null)
        {
            if (!warnedCam) { Debug.LogWarning("[ModularAimController] ไม่พบ Main Camera (ตั้ง Tag = MainCamera ให้กล้องด้วย)"); warnedCam = true; }
            return;
        }
        if (aimOrigin == null) aimOrigin = gunPivot;

        // 1) องศาจากตัวละคร -> เมาส์ บนจอ
        Vector3 originScreen = mainCam.WorldToScreenPoint(aimOrigin.position);
        Vector2 d = new Vector2(Input.mousePosition.x - originScreen.x, Input.mousePosition.y - originScreen.y);
        if (d.sqrMagnitude >= 0.001f)
        {
            screenAimAngle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            snappedAngle = Mathf.Round(screenAimAngle / 60f) * 60f;   // 6 ทิศ (ทิศละ 60°)
            wobble = Mathf.DeltaAngle(snappedAngle, screenAimAngle);  // -30..30
        }

        // 2) ดึงทิศมิเรอร์ / trim / hand offset จาก Rig (ถ้าไม่มี Rig ก็หมุนตรงๆ)
        float mirrorSign = spriteRig != null ? spriteRig.mirrorSign : 1f;
        float trim = spriteRig != null ? spriteRig.trim : 0f;
        Vector2 targetOffset = (useHandOffset && spriteRig != null) ? spriteRig.handOffset : Vector2.zero;

        // 3) เลื่อนตำแหน่งปืนไปฝั่งที่หัน (เหมือนแขนขยับไปข้างนั้น)
        currentOffset = Vector2.Lerp(currentOffset, targetOffset, Time.deltaTime * offsetLerpSpeed);
        gunPivot.localPosition = homeLocalPos + new Vector3(currentOffset.x, currentOffset.y, 0f);

        // 4) หมุน barrel = แค่ส่วนโยก (ล็อกไม่ให้ปืนตั้งชันเกิน maxBarrelTilt)
        float tilt = Mathf.Clamp(wobble, -maxBarrelTilt, maxBarrelTilt);
        float targetZ = tilt * mirrorSign + trim;
        currentZ = Mathf.LerpAngle(currentZ, targetZ, Time.deltaTime * rotateLerpSpeed);

        // 5) หัน pivot เข้ากล้อง แล้วบิดแกน Z
        gunPivot.rotation = mainCam.transform.rotation;
        gunPivot.Rotate(0f, 0f, currentZ, Space.Self);
    }
}
