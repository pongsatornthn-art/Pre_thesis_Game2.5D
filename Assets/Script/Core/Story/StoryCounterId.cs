using UnityEngine;

/// <summary>
/// ตัวแทนของ "ตัวนับ" 1 ตัวในความจำกลาง เช่น ชิ้นส่วนรูปภาพที่เก็บได้ / ผีที่ฆ่าไป
/// คู่กับ StoryFlagId — ธงตอบได้แค่ "มี/ไม่มี" ส่วนตัวนับตอบว่า "กี่อัน"
///
/// ข้อควรระวัง: ระบบเซฟบันทึกด้วย counterId ห้ามแก้หลังเริ่มใช้งานแล้ว
/// </summary>
[CreateAssetMenu(fileName = "Counter_", menuName = "Story/Counter Id", order = 1)]
public class StoryCounterId : ScriptableObject
{
    [Tooltip("รหัสถาวร ห้ามแก้หลังใช้งานแล้ว เพราะระบบเซฟอ้างอิงค่านี้")]
    public string counterId;
}
