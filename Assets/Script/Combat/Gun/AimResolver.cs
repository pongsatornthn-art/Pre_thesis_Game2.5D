using UnityEngine;

/// <summary>
/// ตัวกลางคำนวณจุดเล็งและทิศทางเล็งของผู้เล่น (Single Source of Truth)
/// 
/// ทำไมต้องมีคลาสนี้:
/// 1. แก้ปัญหาเรย์ชนขอบกำแพง/หลังคา: เดิมยิงเรย์จากกล้องผ่านเมาส์แล้วเอาวัตถุแรกที่โดน
///    ทำให้เวลาสิ่งกีดขวางบังหน้ากล้อง กระสุนจะพุ่งผิดทิศ คลาสนี้เปลี่ยนมา "ตัดระนาบแนวนอน"
///    (Math Plane) แทน Physics ทำให้ไม่มีอะไรในฉากมาบังแนวเล็งได้อีก
/// 2. รวมจุดคำนวณ: ปืน ตัวละครหันหน้า และกระสุน จะอ่านจุดเล็งจากที่นี่จุดเดียว ทิศจึงตรงกัน 100%
/// 3. รองรับ New Input System: ย้ายการอ่าน Input.mousePosition มารวมไว้ใน ReadPointerPosition()
///    จุดเดียวทั้งโปรเจกต์ เวลาเปลี่ยนระบบ Input ในอนาคตจะแก้แค่ฟังก์ชันเดียว
/// </summary>
public class AimResolver : MonoBehaviour
{
    [Header("References (เว้นว่างได้ = หา MainCamera ให้อัตโนมัติ)")]
    [SerializeField] private Camera mainCam;

    // ตัวแปรแคชผลลัพธ์ต่อเฟรม เพื่อไม่ให้คำนวณซ้ำเมื่อหลายสคริปต์เรียกพร้อมกันในเฟรมเดียว
    private int lastFrameCount = -1;
    private float lastWorldY;
    private Vector3 lastAimPoint;
    private bool hasValidAimPoint = false;

    private void Awake()
    {
        if (mainCam == null) mainCam = Camera.main;
    }

    /// <summary>
    /// อ่านตำแหน่งพอยน์เตอร์/เมาส์บนหน้าจอ
    /// รวมไว้ที่นี่ที่เดียวตามกฎ เพื่อให้ย้ายไประบบ New Input System ได้ง่ายในอนาคต
    /// </summary>
    private Vector2 ReadPointerPosition()
    {
        return Input.mousePosition;
    }

    /// <summary>
    /// หาตำแหน่งเป้าหมายบนระนาบแนวนอนระดับความสูง worldY
    /// </summary>
    /// <param name="worldY">ระดับความสูงแกน Y ในโลก 3D ที่ต้องการตัดระนาบ</param>
    /// <returns>พิกัด Vector3 ในโลก 3D</returns>
    public Vector3 GetAimPoint(float worldY)
    {
        // 1. ถ้าในเฟรมเดียวกันคำนวณความสูงเดิมไปแล้ว คืนค่าแคชทันที (ประหยัด CPU)
        if (Time.frameCount == lastFrameCount && Mathf.Approximately(worldY, lastWorldY) && hasValidAimPoint)
        {
            return lastAimPoint;
        }

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null)
        {
            // ถ้าหากล้องไม่เจอ คืนค่าเดิมหรือตำแหน่งตัวผู้เล่น เพื่อไม่ให้เป็น Vector3.zero
            return hasValidAimPoint ? lastAimPoint : transform.position;
        }

        // 2. ยิงเรย์จากกล้องผ่านตำแหน่งเมาส์ ตัดกับระนาบแนวนอน Plane(Vector3.up, worldY)
        Ray ray = mainCam.ScreenPointToRay(ReadPointerPosition());
        Plane horizontalPlane = new Plane(Vector3.up, new Vector3(0f, worldY, 0f));

        if (horizontalPlane.Raycast(ray, out float enter))
        {
            lastAimPoint = ray.GetPoint(enter);
            // [2026-11-25 นา: ยิงบอสไม่ค่อยโดน] เมาส์ชี้อยู่บนรูปมอน → เล็งกลางตัวมอนตรงๆ
            // (เดิมจุดเล็ง = จุดบนพื้นระดับปากกระบอก · ชี้หัว/อกมอนตัวสูง = จุดเลยหลังมอนไป กระสุนเฉียด)
            if (snapToEnemyUnderCursor && TryEnemyUnderCursor(lastAimPoint, out Vector3 enemyCenter))
                lastAimPoint = new Vector3(enemyCenter.x, worldY, enemyCenter.z);
            lastFrameCount = Time.frameCount;
            lastWorldY = worldY;
            hasValidAimPoint = true;
            return lastAimPoint;
        }

        // 3. ถ้าเรย์ขนานกับพื้น (เช่น กล้องเงยผิดมุม) ให้คืนค่าล่าสุดที่เคยถูกต้อง ห้ามคืน Vector3.zero
        return hasValidAimPoint ? lastAimPoint : new Vector3(transform.position.x, worldY, transform.position.z);
    }

    /// <summary>
    /// หาเวกเตอร์ทิศทางจากจุดเริ่มต้นไปยังเป้าหมาย โดยตัดแกน Y ออก (แบนราบขนานพื้น) และ Normalize แล้ว
    /// เหมาะสำหรับใช้ยิงกระสุนและหันปืนในเกมมุมมอง 2.5D
    /// </summary>
    [Header("[2026-11-25] เล็งติดมอนใต้เมาส์")]
    [Tooltip("เมาส์ชี้บนรูปมอน = เล็งกลางตัวมอน (แก้ยิงมอนตัวสูงไม่โดน)")]
    [SerializeField] private bool snapToEnemyUnderCursor = true;
    [Tooltip("หามอนรอบจุดเล็งบนพื้นในรัศมีนี้ (เมตร) — มอนตัวสูง ชี้หัวแล้วจุดบนพื้นเลยตัวไปไกล")]
    [SerializeField, Min(0.5f)] private float snapSearchRadius = 4f;
    [Tooltip("ขยายกรอบรูปมอนบนจอออกกี่พิกเซล (ชี้เฉียดขอบก็นับ)")]
    [SerializeField, Min(0f)] private float snapScreenPadding = 6f;

    private readonly Collider[] snapBuffer = new Collider[32];

    /// <summary>หามอน (IDamageable) ที่รูปบนจอทับตำแหน่งเมาส์ — เจอหลายตัวเอาตัวที่ใกล้กล้องสุด</summary>
    private bool TryEnemyUnderCursor(Vector3 groundAimPoint, out Vector3 center)
    {
        center = default;
        Vector2 mouse = ReadPointerPosition();
        int n = Physics.OverlapSphereNonAlloc(groundAimPoint, snapSearchRadius, snapBuffer, ~0, QueryTriggerInteraction.Ignore);
        float bestDepth = float.MaxValue;
        bool found = false;

        for (int i = 0; i < n; i++)
        {
            Collider col = snapBuffer[i];
            IDamageable dmg = col.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;
            Component owner = dmg as Component;
            if (owner == null || owner.CompareTag("Player")) continue;

            SpriteRenderer sr = BodySprite(owner);
            if (sr == null) continue;

            if (!ScreenRect(sr.bounds, out Rect rect)) continue;
            if (!rect.Contains(mouse)) continue;

            float depth = mainCam.WorldToScreenPoint(col.bounds.center).z;
            if (depth < bestDepth)
            {
                bestDepth = depth;
                center = col.bounds.center;
                found = true;
            }
        }
        return found;
    }

    /// <summary>รูปตัวมอน = สไปรท์ที่สูงที่สุด (ไม่เอาเงากลมใต้เท้า)</summary>
    private static SpriteRenderer BodySprite(Component owner)
    {
        SpriteRenderer best = null;
        float bestH = 0f;
        foreach (SpriteRenderer sr in owner.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sr.enabled || !sr.isVisible || sr.sprite == null) continue;
            float h = sr.bounds.size.y;
            if (h > bestH) { bestH = h; best = sr; }
        }
        return best;
    }

    private bool ScreenRect(Bounds b, out Rect rect)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 s = mainCam.WorldToScreenPoint(corner);
            if (s.z <= 0f) { rect = default; return false; }
            min = Vector2.Min(min, s); max = Vector2.Max(max, s);
        }
        rect = Rect.MinMaxRect(min.x - snapScreenPadding, min.y - snapScreenPadding, max.x + snapScreenPadding, max.y + snapScreenPadding);
        return true;
    }

    public Vector3 GetFlatAimDirection(Vector3 from)
    {
        Vector3 target = GetAimPoint(from.y);
        Vector3 dir = target - from;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
        {
            return dir.normalized;
        }

        // หากเมาส์อยู่ตรงกับจุดยิงพอดี คืนค่าทิศด้านหน้าของตัวละครแทน
        return transform.forward;
    }
}
