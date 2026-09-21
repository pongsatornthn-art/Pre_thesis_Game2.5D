using System;
using System.Collections.Generic;

/// <summary>
/// อินเตอร์เฟสสำหรับระบบบริหารจัดการเควส
/// ออกแบบเพื่อให้ UI หน้าสมุด (QuestPage) และระบบอื่นๆ เรียกใช้ผ่าน Abstraction ตามหลัก DIP
/// </summary>
public interface IQuestService
{
    QuestData ActiveQuest { get; }
    IReadOnlyList<QuestData> CompletedQuests { get; }
    bool IsObjectiveDone(QuestObjective objective);
    void StartQuest(QuestData quest);
    event Action OnChanged;
}
