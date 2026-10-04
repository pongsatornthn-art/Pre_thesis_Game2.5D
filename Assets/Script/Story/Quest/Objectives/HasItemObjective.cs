using System;
using UnityEngine;

/// <summary>
/// เป้าหมายแบบ "มีไอเทมในตัว" — ครอบคลุมการ์ด Figma: Key item (กุญแจห้องทำงาน)
/// ใช้ตัวเช็คเดียวกับ HasItemCondition (ทั้งของสำคัญและกระเป๋าปกติ) ไม่เขียนซ้ำ
///
/// เอากุญแจไปใช้เปิดประตูแล้วของหายจากตัว → ข้อนี้ยังขีดฆ่าค้างอยู่ เพราะ QuestService ล็อกตอนสำเร็จครั้งแรก
/// </summary>
[Serializable, PickerName("มีไอเทมในตัว")]
public class HasItemObjective : QuestObjective
{
    [Tooltip("กรอกอย่างใดอย่างหนึ่ง: ของสำคัญ (Key Item) หรือไอเทมในกระเป๋าปกติ")]
    public HasItemCondition item = new HasItemCondition();

    public override bool IsMet(StoryContext ctx) => item != null && item.IsMet(ctx);
}
