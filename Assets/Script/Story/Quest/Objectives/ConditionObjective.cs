using System;
using UnityEngine;

/// <summary>
/// เป้าหมายแบบ "เงื่อนไขอะไรก็ได้" — ใช้เงื่อนไขในระบบเนื้อเรื่องตัวไหนก็ได้ (And / Not / QuestCompleted ...)
/// ไว้สำหรับกรณีพิเศษที่ 3 แบบหลักไม่ครอบคลุม โดยไม่ต้องเขียนคลาสใหม่
/// </summary>
[Serializable, PickerName("เงื่อนไขอื่นๆ (ขั้นสูง)")]
public class ConditionObjective : QuestObjective
{
    [SerializeReference, SubclassPicker]
    public IStoryCondition condition;

    public override bool IsMet(StoryContext ctx) => condition != null && condition.IsMet(ctx);
}
