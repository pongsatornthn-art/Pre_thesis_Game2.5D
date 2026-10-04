using System;
using UnityEngine;

/// <summary>
/// เงื่อนไขตรวจสอบว่าเควสที่ระบุกำลังทำอยู่ (จดลงสมุดแล้ว ยังไม่จบ)
/// </summary>
[Serializable, PickerName("เควส/กำลังทำเควสนี้อยู่")]
public class QuestActiveCondition : IStoryCondition
{
    [Tooltip("เควสที่ต้องการตรวจสอบว่ากำลังทำอยู่หรือไม่")]
    public QuestData quest;

    public bool IsMet(StoryContext ctx)
    {
        if (quest == null) return true;

        IQuestService quests = ctx?.Quests ?? ServiceLocator.GetOptional<IQuestService>();
        return quests != null && quests.GetState(quest) == QuestState.Active;
    }
}
