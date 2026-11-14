using UnityEngine;

/// <summary>ปุ่มที่ป้ายบอก — E = ของทั่วไป (เก็บ/ประตู/กล่อง/ตู้เซฟ/โต๊ะ) · F = ของเนื้อเรื่อง (สำรวจ/ชิ้นส่วน/แผนที่) ดู InteractInput</summary>
public enum PromptKey
{
    Pickup, // E (InteractInput.PickupKey)
    Story   // F (InteractInput.Key)
}

/// <summary>
/// ป้าย "[E] เก็บ" / "[F] ตรวจสอบ" ลอยเหนือของในโลก 3D (เจ้าของตกลง 2026-11-14)
/// ของทุกชิ้นขอโชว์ได้พร้อมกัน — ป้ายโชว์แค่ชิ้นที่ใกล้ผู้เล่นที่สุดชิ้นเดียว
/// </summary>
public interface IInteractPromptService
{
    /// <summary>ขอโชว์ป้าย (เรียกซ้ำทุกเฟรมได้) · owner = ใครขอ (ใช้ตอนยกเลิก) · height = สูงจาก anchor กี่เมตร</summary>
    void Show(object owner, Transform anchor, float height, PromptKey key, string labelKey);

    /// <summary>ยกเลิกคำขอของ owner นี้</summary>
    void Hide(object owner);
}
