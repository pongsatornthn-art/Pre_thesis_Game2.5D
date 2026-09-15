using UnityEngine;

/// <summary>
/// แขนถือปืน — หลักการเดียวกับปืน:
///   ปืนหมุนตามเมาส์ พอองศาถึงเกณฑ์ก็สลับรูป 6 ทิศ + เอียงในทิศนั้นนิดหน่อย
///   แขนก็เหมือนกัน แต่ "ฐานหมุน" ย้ายไปเกาะจุดข้อศอกของแต่ละทิศ (empty 6 อัน วางมือตรงข้อศอก body แต่ละท่า)
///   ใช้รูปแยกซ้าย-ขวา (ไม่ mirror = ไม่มีผีแขน) · ท่อนไหล่วาดติดใน body อยู่แล้ว
///
/// วางสคริปต์นี้ไว้ที่ก้อนเดียวกับ SpriteRenderer ของแขน (ก้อน "Arm")
///   Player
///   ├─ Elbow_Right / Elbow_Left / Elbow_FrontRight / Elbow_FrontLeft / Elbow_BackRight / Elbow_BackLeft   (empty)
///   └─ Arm   (SpriteRenderer + สคริปต์นี้ · pivot รูป = ข้อศอก · โค้ดย้ายตัวเองไปเกาะ Elbow + หมุน + สลับรูป)
/// </summary>
public class DirectionalArm : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ModularAimController ของปืน — อ่านทิศ/องศาเอียงมาใช้ให้ตรงกับปืน")]
    public ModularAimController aimController;
    [Tooltip("SpriteRenderer ของแขน (เว้นว่าง = หาในก้อนนี้)")]
    public SpriteRenderer armRenderer;
    [Tooltip("จุดวัดองศาตอนไม่มี aimController (เว้นว่าง = ก้อนนี้)")]
    public Transform aimOrigin;
    public Camera mainCam;

    [Header("รูปแขน 6 ทิศ (วาดแยกซ้าย-ขวา ไม่ mirror)")]
    public Sprite armRight;
    public Sprite armBackRight;   // เล็งขึ้น-ขวา
    public Sprite armBackLeft;    // เล็งขึ้น-ซ้าย
    public Sprite armLeft;
    public Sprite armFrontLeft;   // เล็งลง-ซ้าย
    public Sprite armFrontRight;  // เล็งลง-ขวา

    [Header("จุดข้อศอก 6 ทิศ (empty วางตรงข้อศอกของ body แต่ละท่า)")]
    public Transform elbowRight;
    public Transform elbowBackRight;
    public Transform elbowBackLeft;
    public Transform elbowLeft;
    public Transform elbowFrontLeft;
    public Transform elbowFrontRight;

    [Header("ปรับท่า")]
    [Tooltip("องศาหลัก บวกทุกทิศ — ปรับทีละ 90 ก่อน แล้วค่อยไล่ trim ทีละทิศ")]
    public float spriteOffset = 0f;
    [Tooltip("องศาเอียงสูงสุดในแต่ละทิศ (ใส่เท่า maxBarrelTilt ของปืนก็ได้)")]
    public float maxTilt = 18f;
    [Tooltip("ความเร็วเอียงตามปืน")]
    public float lerpSpeed = 20f;

    [Header("Trim แยกทีละทิศ (รูปแต่ละใบวาดคนละองศา — ปรับให้แขนตรงกับปืนทีละอัน)")]
    public float trimRight = 0f;
    public float trimBackRight = 0f;
    public float trimBackLeft = 0f;
    public float trimLeft = 0f;
    public float trimFrontLeft = 0f;
    public float trimFrontRight = 0f;

    [Header("Sorting (แขนหน้า/หลังตัว)")]
    public int orderSide = 1;
    public int orderBack = -1;   // เล็งขึ้น = แขนหลังตัว
    public int orderFront = 2;

    [Header("Debug")]
    [Tooltip("โชว์ Console ว่าตอนนี้ทิศไหน / เกาะ Elbow ตัวไหน / ตำแหน่ง")]
    public bool debugLog = false;

    private float curTilt;
    private bool inited;
    private int lastWarnW = -99;

    void Awake()
    {
        if (armRenderer == null) armRenderer = GetComponent<SpriteRenderer>();
        if (aimOrigin == null) aimOrigin = transform;
        if (mainCam == null) mainCam = Camera.main;
        if (aimController == null) aimController = Object.FindAnyObjectByType<ModularAimController>();
        if (aimController == null) Debug.LogWarning("[DirectionalArm] ไม่พบ ModularAimController — จะคำนวณองศาจากเมาส์เอง");
    }

    void LateUpdate()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        // --- ทิศ (6) + องศาเอียง ---
        float snapped, wobble;
        if (aimController != null)
        {
            snapped = aimController.snappedAngle;
            wobble = aimController.wobble;
        }
        else
        {
            Vector3 o = mainCam.WorldToScreenPoint(aimOrigin.position);
            Vector2 d = new Vector2(Input.mousePosition.x - o.x, Input.mousePosition.y - o.y);
            float ang = d.sqrMagnitude > 0.001f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
            snapped = Mathf.Round(ang / 60f) * 60f;
            wobble = Mathf.DeltaAngle(snapped, ang);
        }

        float a = ((snapped % 360f) + 360f) % 360f;
        int w = Mathf.RoundToInt(a / 60f) % 6; // 0=E 1=NE 2=NW 3=W 4=SW 5=SE

        Sprite sp;
        Transform elbow;
        int order;
        float extraOffset;
        string dirName;
        switch (w)
        {
            case 1:  sp = armBackRight;  elbow = elbowBackRight;  order = orderBack;  extraOffset = trimBackRight;  dirName = "BackRight";  break;
            case 2:  sp = armBackLeft;   elbow = elbowBackLeft;   order = orderBack;  extraOffset = trimBackLeft;   dirName = "BackLeft";   break;
            case 3:  sp = armLeft;       elbow = elbowLeft;       order = orderSide;  extraOffset = trimLeft;       dirName = "Left";       break;
            case 4:  sp = armFrontLeft;  elbow = elbowFrontLeft;  order = orderFront; extraOffset = trimFrontLeft;  dirName = "FrontLeft";  break;
            case 5:  sp = armFrontRight; elbow = elbowFrontRight; order = orderFront; extraOffset = trimFrontRight; dirName = "FrontRight"; break;
            default: sp = armRight;      elbow = elbowRight;      order = orderSide;  extraOffset = trimRight;      dirName = "Right";      break;
        }

        // --- เตือนถ้ายังไม่ได้ต่อช่อง (เตือนแค่ตอนเปลี่ยนทิศ ไม่สแปม) ---
        if (w != lastWarnW)
        {
            lastWarnW = w;
            if (elbow == null) Debug.LogWarning($"[DirectionalArm] ทิศ {dirName}: ยังไม่ได้ลาก Elbow_{dirName} — แขนจะไม่ย้ายตำแหน่ง");
            if (sp == null) Debug.LogWarning($"[DirectionalArm] ทิศ {dirName}: ยังไม่ได้ใส่รูปแขน");
        }

        // --- ฐานแขน ย้ายไปทับจุดข้อศอกของทิศนั้น ---
        if (elbow != null) transform.position = elbow.position;

        // --- สลับรูป + sorting ---
        if (armRenderer != null)
        {
            if (sp != null) armRenderer.sprite = sp;
            armRenderer.sortingOrder = order;
        }

        // --- เอียงตามปืน (clamp + lerp) ---
        float targetTilt = Mathf.Clamp(wobble, -maxTilt, maxTilt);
        if (!inited) { curTilt = targetTilt; inited = true; }
        curTilt = Mathf.LerpAngle(curTilt, targetTilt, Time.deltaTime * lerpSpeed);

        // --- billboard + หมุนแกน Z (รูปวาดชี้ตรงกลางทิศแล้ว หมุนแค่ส่วนเอียง) ---
        transform.rotation = mainCam.transform.rotation;
        transform.Rotate(0f, 0f, curTilt + spriteOffset + extraOffset, Space.Self);

        if (debugLog)
            Debug.Log($"[DirectionalArm] dir={dirName} (w={w}) snapped={snapped:F0} wobble={wobble:F1} elbow={(elbow ? elbow.name : "NULL")} pos={transform.position}");
    }
}
