using System;
using UnityEngine;

/// <summary>
/// เงื่อนไขตรวจสอบว่าเควสที่ระบุกำลังเปิดใช้งานอยู่ (Active) หรือไม่
/// </summary>
[Serializable]
public class QuestActiveCondition : IStoryCondition
{
    [Tooltip("เควสที่ต้องการตรวจสอบว่ากำลังทำอยู่หรือไม่")]
    public QuestData quest;

    public bool IsMet(StoryContext ctx)
    {
        if (quest == null) return true;

        IQuestService quests = ctx?.Quests ?? ServiceLocator.Get<IQuestService>();
        if (quests == null) return false;

        return quests.ActiveQuest == quest;
    }
}
