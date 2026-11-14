using UnityEngine;

/// <summary>ปุ่มที่ป้ายบอก — E = ของทั่วไป (เก็บ/ประตู/กล่อง/ตู้เซฟ/โต๊ะ) · F = ของเนื้อเรื่อง (สำรวจ/ชิ้นส่วน/แผนที่) ดู InteractInput</summary>
public enum PromptKey
{
    Pickup, // E (InteractInput.PickupKey)
    Story   // F (InteractInput.Key)
}

/// <summary>
/// ป้าย "[E] เก็บ" / "[F] ตรวจสอบ" ลอยเหนือของ (เจ้าของตกลง 2026-11-14)
/// ของทุกชิ้นขอโชว์ได้พร้อมกัน — ป้ายโชว์แค่ชิ้นที่ใกล้ผู้เล่นที่สุดชิ้นเดียว
/// </summary>
public interface IInteractPromptService
{
    /// <summary>ขอโชว์ป้าย (เรียกซ้ำทุกเฟรมได้) · owner = ใครขอ (ใช้ตอนยกเลิก) · offset = ระยะจาก anchor (เมตร, แกนโลก)</summary>
    void Show(object owner, Transform anchor, Vector3 offset, PromptKey key, string labelKey);

    /// <summary>ยกเลิกคำขอของ owner นี้</summary>
    void Hide(object owner);

    /// <summary>
    /// ตอนนี้ป้ายโชว์ "ของชิ้นอื่น" ที่ใช้ปุ่มเดียวกันอยู่ไหม — ใช้กันกดปุ่มเดียวแล้วทำงาน 2 อย่าง
    /// (เช่น ยืนใกล้ทั้งประตูและไอเทม กด E ครั้งเดียว ต้องทำแค่ชิ้นที่ป้ายโชว์)
    /// </summary>
    bool IsOtherShown(object owner, PromptKey key);
}
