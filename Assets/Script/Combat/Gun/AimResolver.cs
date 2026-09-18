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
