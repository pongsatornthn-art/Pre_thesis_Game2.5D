using UnityEngine;

/// <summary>
/// ตัวแทนของ "ธงเนื้อเรื่อง" (Story Flag) 1 รายการในเกม
/// ออกแบบเป็น ScriptableObject แทนการใช้ string ดิบ เพื่อให้สามารถลากใส่ช่อง Inspector ได้โดยตรง
/// ป้องกันข้อผิดพลาดจากการพิมพ์ผิด (Typo) และลดความเสี่ยงที่การเปลี่ยนชื่อไฟล์จะกระทบต่อตรรกะของเกม
/// 
/// ข้อควรระวัง: ระบบเซฟเกมจะบันทึกค่า flagId ภายใน ห้ามแก้ไขค่า flagId หลังจากเริ่มใช้งานไปแล้ว
/// </summary>
[CreateAssetMenu(fileName = "Flag_", menuName = "Story/Flag Id", order = 1)]
public class StoryFlagId : ScriptableObject
{
    [Tooltip("รหัสถาวร ห้ามแก้หลังใช้งานแล้ว เพราะระบบเซฟอ้างอิงค่านี้")]
    public string flagId;

    /// <summary>
    /// รหัสที่ระบบใช้จริง — ช่อง Flag Id ว่าง = ใช้ชื่อไฟล์แทน
    /// (2026-10-06: เจอธงที่สร้างแล้วลืมกรอกรหัส 8 ไฟล์ ระบบเคยเมินทิ้งเงียบๆ → ปักแล้วไม่ขึ้น เควสไม่เดิน)
    /// ⚠️ ใช้ชื่อไฟล์แล้ว ห้ามเปลี่ยนชื่อไฟล์หลังมีเซฟ — อยากเปลี่ยนชื่อได้ ให้กรอก Flag Id ไว้
    /// </summary>
    public string Id => string.IsNullOrEmpty(flagId) ? name : flagId;
}
