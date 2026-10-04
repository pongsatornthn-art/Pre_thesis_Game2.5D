using System;
using UnityEngine;

/// <summary>
/// เงื่อนไข "เควสนี้จบแล้ว" — ตัวหลักของการจัดลำดับเควส (prerequisite)
/// เช่น ตั้งใน Available When ของเควส B = เควส B โผล่เมื่อเควส A จบ
/// </summary>
[Serializable, PickerName("เควส/เควสนี้จบแล้ว")]
public class QuestCompletedCondition : IStoryCondition
{
    [Tooltip("เควสที่ต้องจบก่อน")]
    public QuestData quest;

    public bool IsMet(StoryContext ctx)
    {
        if (quest == null) return false;   // ยังไม่ได้กรอก ห้ามปล่อยผ่าน ไม่งั้นเควสที่รออยู่จะโผล่ทันที

        IQuestService quests = ctx?.Quests ?? ServiceLocator.Get<IQuestService>();
        return quests != null && quests.GetState(quest) == QuestState.Completed;
    }
}
