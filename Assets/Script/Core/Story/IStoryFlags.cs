using System;

/// <summary>
/// อินเตอร์เฟสสำหรับระบบบันทึกสถานะธงเนื้อเรื่อง (Story Flags)
/// ทำหน้าที่เป็น "ความจำกลาง" ของเกม เพื่อให้ระบบอื่น (เควส, ทริกเกอร์, ไดอะล็อก)
/// สื่อสารผ่าน Abstraction โดยไม่ขึ้นตรงกับ StoryFlagService ตามหลัก Dependency Inversion (DIP)
/// </summary>
public interface IStoryFlags
{
    bool Has(StoryFlagId flag);
    void Set(StoryFlagId flag);      // ตั้งซ้ำไม่ยิง event ซ้ำ
    void Clear(StoryFlagId flag);
    event Action OnChanged;          // แจ้งเตือนเมื่อมีสถานะธงเปลี่ยนแปลง
}
