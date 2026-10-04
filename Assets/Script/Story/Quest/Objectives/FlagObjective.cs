using System;
using UnityEngine;

/// <summary>
/// เป้าหมายแบบ "เกิดเหตุการณ์แล้ว" — สำเร็จเมื่อธงที่กำหนดขึ้น
/// ครอบคลุมการ์ด Figma: Explore (เดินไปถึง) · Interact Object (กดสำรวจ) · Puzzle · ส่งเควส
/// ฝั่งซีนเป็นคนปักธง (StoryTriggerZone / StoryTriggerInteract / StoryFlagSetter / ระบบปริศนา)
/// </summary>
[Serializable, PickerName("เกิดเหตุการณ์แล้ว (ธง)")]
public class FlagObjective : QuestObjective
{
    [Tooltip("ธงนี้ขึ้นเมื่อไหร่ = ข้อนี้สำเร็จ")]
    public StoryFlagId flag;

    public override bool IsMet(StoryContext ctx)
    {
        if (flag == null) return false;   // ยังไม่ได้กรอก ห้ามนับว่าสำเร็จ
        IStoryFlags flags = ctx?.Flags ?? ServiceLocator.Get<IStoryFlags>();
        return flags != null && flags.Has(flag);
    }
}
