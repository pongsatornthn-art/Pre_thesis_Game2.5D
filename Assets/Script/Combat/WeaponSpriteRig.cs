using UnityEngine;

/// <summary>
/// สลับรูปปืน 6 ทิศ จาก 3 สไปรท์ (NE, E, SE) — ฝั่งซ้ายมิเรอร์: NW=NE พลิก / W=E พลิก / SW=SE พลิก
/// *ไม่มีทิศบน/ล่าง* — ทิศเฉียงถูกขยายเป็นทิศละ 60° จนชนแนวบน-ล่าง ทำให้ปืนหันข้างเห็นเต็มลำเสมอ ไม่ตั้งชันจ่อหัว
/// รูปแต่ละใบวาด "ชี้ตรงกลางทิศนั้น" (E ชี้ขวา, NE เฉียงบน-ขวา, SE เฉียงล่าง-ขวา)
///
/// ทำงานเอง (LateUpdate) ไม่ต้องพึ่ง ModularAimController ก็โชว์ปืนได้
/// ModularAimController แค่มาอ่าน mirrorSign / trim / handOffset ไปหมุน+ย้ายปืนต่อ
/// </summary>
public class WeaponSpriteRig : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SpriteRenderer ของปืน (เว้นว่าง = หาในก้อนนี้เอง) — ควรเป็นลูกของ GunPivot")]
    public SpriteRenderer gunSpriteRenderer;
    [Tooltip("จุดอ้างอิงตัวละคร ใช้วัดองศาไปหาเมาส์ (เว้นว่าง = ก้อนนี้)")]
    public Transform aimOrigin;
    public Camera mainCam;

    [Header("Gun Sprites (วาดชี้ตรงกลางทิศ — มีแค่ฝั่งขวา 3 ใบ ฝั่งซ้ายมิเรอร์เอา)")]
    public Sprite gunNorthEast;  // เฉียงบน-ขวา (มิเรอร์ -> NW)
    public Sprite gunEast;       // ชี้ขวา (มิเรอร์ -> W)
    public Sprite gunSouthEast;  // เฉียงล่าง-ขวา (มิเรอร์ -> SW)

    [Header("Trim องศา (เผื่อ Artist วาดเบี้ยว — ปกติ 0)")]
    public float trimNorthEast;
    public float trimEast;
    public float trimSouthEast;

    [Header("Hand Offset — ปืนย้ายไปฝั่งที่หัน เหมือนแขนจริง (local ของ Player)")]
    [Tooltip("ระยะเยื้องตอนหันทิศนั้นๆ (x = ซ้าย/ขวา, y = ขึ้น/ลง) ฝั่งซ้ายจะกลับ x ให้อัตโนมัติ")]
    public Vector2 offsetNorthEast = new Vector2(0.22f, 0.08f);
    public Vector2 offsetEast = new Vector2(0.32f, 0f);
    public Vector2 offsetSouthEast = new Vector2(0.26f, -0.14f);
    [Tooltip("คูณระยะเยื้องทั้งหมด (ปรับตัวเดียวเร็วๆ)")]
    public float handOffsetScale = 1f;
    [Tooltip("หันซ้ายแล้วปืนยังโดนตัวทับ? เพิ่มค่านี้ทีละ 0.05 (ดันปืนไปทางซ้ายเพิ่ม เฉพาะ 3 ทิศซ้าย)")]
    public float leftSidePush = 0.15f;

    [Header("Sorting (ปืนอยู่หน้า/หลังตัวคน)")]
    public int orderBehind = -1;
    public int orderFront = 1;

    [Header("Options")]
    [Tooltip("โชว์ปืนเฉพาะตอนถือ RangedWeapon (ไม่มี Inventory ในซีน = โชว์ตลอด)")]
    public bool onlyShowWhenRangedEquipped = true;
    [Tooltip("ฝั่งซ้าย (มิเรอร์) เอาเมาส์ขึ้นแล้วปืนหันลง / เอาลงแล้วหันขึ้น = ให้ติ๊กอันนี้ (เปิดไว้เป็นค่าเริ่มต้น)")]
    public bool invertMirrorRotation = true;

    [Header("Debug")]
    [Tooltip("บังคับโชว์ปืนตลอด ไม่ต้องหาปืนมาถือ")]
    public bool debugForceShow = false;
    [Tooltip("ปุ่ม toggle debugForceShow ตอนเล่น")]
    public KeyCode debugToggleKey = KeyCode.G;

    // เอาต์พุตให้ ModularAimController อ่าน
    [HideInInspector] public float mirrorSign = 1f;
    [HideInInspector] public float trim = 0f;
    [HideInInspector] public Vector2 handOffset;
    /// <summary>องศา snap 6 ทิศล่าสุด (ทีละ 60°, screen space)</summary>
    [HideInInspector] public float snappedAngle;

    private Transform spriteTf;
    private bool warnedNoRenderer;

    void Awake()
    {
        if (gunSpriteRenderer == null) gunSpriteRenderer = GetComponent<SpriteRenderer>();
        if (gunSpriteRenderer == null) gunSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (gunSpriteRenderer != null) spriteTf = gunSpriteRenderer.transform;
        if (aimOrigin == null) aimOrigin = transform;
        if (mainCam == null) mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugForceShow = !debugForceShow;
            Debug.Log(debugForceShow ? "🔫 [WeaponSpriteRig] บังคับโชว์ปืน (debug G)" : "🚫 [WeaponSpriteRig] ปิดบังคับโชว์ปืน");
        }

        if (gunSpriteRenderer == null)
        {
            if (!warnedNoRenderer) { Debug.LogWarning("[WeaponSpriteRig] ยังไม่ได้ลาก Gun Sprite Renderer และหาในก้อน/ลูกไม่เจอ"); warnedNoRenderer = true; }
            return;
        }

        bool show = debugForceShow || !onlyShowWhenRangedEquipped || IsHoldingRanged();
        gunSpriteRenderer.enabled = show;
        if (!show) return;

        snappedAngle = ComputeSnappedAngle();
        Apply(snappedAngle);
    }

    private bool IsHoldingRanged()
    {
        if (Inventory.Instance == null) return true;
        ItemData eq = Inventory.Instance.currentEquippedItem;
        return eq != null && eq.itemType == ItemType.RangedWeapon;
    }

    private float ComputeSnappedAngle()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || aimOrigin == null) return snappedAngle;

        Vector3 o = mainCam.WorldToScreenPoint(aimOrigin.position);
        Vector2 d = new Vector2(Input.mousePosition.x - o.x, Input.mousePosition.y - o.y);
        if (d.sqrMagnitude < 0.001f) return snappedAngle;

        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        return Mathf.Round(ang / 60f) * 60f;   // snap ทีละ 60° (6 ทิศ)
    }

    private void Apply(float snappedScreenAngle)
    {
        float a = ((snappedScreenAngle % 360f) + 360f) % 360f;
        int w = Mathf.RoundToInt(a / 60f) % 6; // 0=E 1=NE 2=NW 3=W 4=SW 5=SE

        Sprite sprite;
        bool mirror;
        float t;
        Vector2 off;
        switch (w)
        {
            case 1:  sprite = gunNorthEast; mirror = false; t = trimNorthEast; off = offsetNorthEast; break; // ขวาบน
            case 2:  sprite = gunNorthEast; mirror = true;  t = trimNorthEast; off = offsetNorthEast; break; // ซ้ายบน (มิเรอร์)
            case 3:  sprite = gunEast;      mirror = true;  t = trimEast;      off = offsetEast;      break; // ซ้าย (มิเรอร์)
            case 4:  sprite = gunSouthEast; mirror = true;  t = trimSouthEast; off = offsetSouthEast; break; // ซ้ายล่าง (มิเรอร์)
            case 5:  sprite = gunSouthEast; mirror = false; t = trimSouthEast; off = offsetSouthEast; break; // ขวาล่าง
            default: sprite = gunEast;      mirror = false; t = trimEast;      off = offsetEast;      break; // 0 = ขวา
        }

        if (sprite != null) gunSpriteRenderer.sprite = sprite;
        else Debug.LogWarning($"[WeaponSpriteRig] ยังไม่ได้ใส่รูปสำหรับทิศ w={w} (0=E 1=NE 2=NW 3=W 4=SW 5=SE)");

        if (spriteTf != null)
        {
            Vector3 s = spriteTf.localScale;
            float mag = Mathf.Abs(s.x) < 0.0001f ? 1f : Mathf.Abs(s.x);
            s.x = mag * (mirror ? -1f : 1f);
            spriteTf.localScale = s;
        }

        bool aimingUp = a > 0f && a < 180f;
        gunSpriteRenderer.sortingOrder = aimingUp ? orderBehind : orderFront;

        if (mirror) off.x = -off.x - leftSidePush;
        handOffset = off * handOffsetScale;

        mirrorSign = mirror ? (invertMirrorRotation ? 1f : -1f) : 1f;
        trim = mirror ? -t : t;
    }
}
