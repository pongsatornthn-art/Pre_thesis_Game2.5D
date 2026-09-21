using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับมอบเควสใหม่ให้ผู้เล่น
/// เริ่มเควสผ่าน IQuestService และส่งเสียงแจ้งเตือนผ่าน IAudioService
/// </summary>
[Serializable]
public class GiveQuestAction : IStoryAction
{
    [Tooltip("เควสที่จะมอบให้ผู้เล่น")]
    public QuestData quest;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (quest != null && ctx != null && ctx.Quests != null)
        {
            ctx.Quests.StartQuest(quest);

            // เล่นเสียงแจ้งเตือนผ่านบริการกลาง IAudioService ตามข้อตกลง
            IAudioService audio = ServiceLocator.Get<IAudioService>();
            audio?.PlayMenuSound(MenuSoundType.Click);
        }
        yield break;
    }
}
