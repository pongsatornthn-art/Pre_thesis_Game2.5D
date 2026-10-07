using UnityEngine;

/// <summary>
/// ปุ่มโต้ตอบกลางของทั้งเกม — **อยากเปลี่ยนปุ่ม แก้ที่นี่ที่เดียว**
///
/// เจ้าของตกลง 2026-10-07 — แยก 2 ปุ่ม:
///   F (Key)       = จุดเนื้อเรื่อง: กดสำรวจ (ขาตั้งวาดรูป ฯลฯ) · ส่งของเควส · ข้ามบทพูด/คัทซีน
///   E (PickupKey) = ของปกติ: เก็บไอเทมที่พื้น (รวมเศษรูป) · เปิด/ปิดกล่องเก็บของ · ประตู (DoorController ของปอใช้ E ตรงๆ อยู่แล้ว)
/// E ยังใช้กับ "ใช้ไอเทมในสมุด" (ตอนเปิดสมุด) และ "ดิ้นหลุดจากสตอล์กเกอร์" — คนละจังหวะ ไม่ชนกัน
/// ⏳ อนาคตย้ายไปตั้งค่าในเมนู Settings (เปลี่ยนปุ่มเองได้) — ตอนนั้นแก้แค่ไฟล์นี้
/// </summary>
public static class InteractInput
{
    public static KeyCode Key { get; set; } = KeyCode.F;

    /// <summary>ชื่อปุ่มสำหรับโชว์ในป้าย เช่น "F"</summary>
    public static string KeyName => Key.ToString();

    public static bool Pressed => Input.GetKeyDown(Key);

    /// <summary>ปุ่มเก็บของ / เปิด-ปิดกล่อง (E)</summary>
    public static KeyCode PickupKey { get; set; } = KeyCode.E;

    public static string PickupKeyName => PickupKey.ToString();

    public static bool PickupPressed => Input.GetKeyDown(PickupKey);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Key = KeyCode.F;
        PickupKey = KeyCode.E;
    }
}
