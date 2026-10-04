using UnityEngine;

/// <summary>
/// ปุ่ม "โต้ตอบ" กลางของทั้งเกม — เก็บของ / เปิดประตู / เปิดกล่อง / กดสำรวจ / ส่งเควส / จุดเซฟ ใช้ปุ่มเดียวกันหมด
/// **อยากเปลี่ยนปุ่ม แก้ที่นี่ที่เดียว**
///
/// 2026-10-04: เปลี่ยนจาก E → F (ยังไม่ไฟนอล) · E ยังใช้กับ "ใช้ไอเทมในสมุด" และ "ดิ้นหลุดจากสตอล์กเกอร์"
/// ⏳ อนาคตย้ายไปตั้งค่าในเมนู Settings (เปลี่ยนปุ่มเองได้) — ตอนนั้นแก้แค่ไฟล์นี้
/// </summary>
public static class InteractInput
{
    public static KeyCode Key { get; set; } = KeyCode.F;

    /// <summary>ชื่อปุ่มสำหรับโชว์ในป้าย เช่น "F"</summary>
    public static string KeyName => Key.ToString();

    public static bool Pressed => Input.GetKeyDown(Key);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Key = KeyCode.F;
}
