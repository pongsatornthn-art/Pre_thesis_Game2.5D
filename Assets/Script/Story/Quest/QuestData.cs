using System;
using UnityEngine;

/// <summary>
/// เป้าหมายย่อย 1 ข้อในเควส
/// เชื่อมโยงความสำเร็จกับ StoryFlagId โดยตรง ทำให้ไม่ต้องแยกเซฟสถานะของแต่ละเป้าหมาย
/// ลดความซับซ้อนและป้องกันความไม่สอดคล้องระหว่างเนื้อเรื่องกับเซฟเกม
/// </summary>
[Serializable]
public class QuestObjective
{
    [Tooltip("คีย์ข้อความอธิบายเป้าหมายใน LocalizationData.csv")]
    public string descriptionKey;

    [Tooltip("เมื่อธงนี้ถูกตั้งค่า = เป้าหมายข้อนี้สำเร็จ")]
    public StoryFlagId completedWhenFlagSet;

    [Tooltip("ซ่อนเป้าหมายนี้ในหน้าสมุดจนกว่าเป้าหมายข้อก่อนหน้าจะสำเร็จ (กันสปอยล์)")]
    public bool hiddenUntilPrevious;
}

/// <summary>
/// ข้อมูลเควส 1 ภารกิจ (ScriptableObject)
/// ออกแบบให้ต่อกันเป็นเส้นตรง (Linear) ตามสเปกของโปรเจกต์
/// เมื่อทำเป้าหมายครบทุกข้อ เควสถัดไป (nextQuest) จะเริ่มขึ้นโดยอัตโนมัติ
/// </summary>
[CreateAssetMenu(fileName = "Quest_", menuName = "Story/Quest", order = 2)]
public class QuestData : ScriptableObject
{
    [Tooltip("รหัสถาวรของเควส ห้ามแก้หลังเริ่มใช้งาน เพราะระบบเซฟใช้อ้างอิง")]
    public string questId;

    [Tooltip("คีย์ชื่อเควสใน LocalizationData.csv")]
    public string titleKey;

    [Tooltip("คีย์คำอธิบายเควสใน LocalizationData.csv")]
    public string descriptionKey;

    [Tooltip("รายการเป้าหมายทั้งหมดของเควสนี้")]
    public QuestObjective[] objectives;

    [Tooltip("เควสถัดไปที่จะเริ่มทันทีเมื่อเควสนี้สำเร็จ (เว้นว่าง = จบสายเควส)")]
    public QuestData nextQuest;
}
