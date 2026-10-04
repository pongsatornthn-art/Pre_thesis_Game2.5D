using System;
using System.Collections.Generic;

public enum QuestState
{
    NotStarted,
    Active,
    Completed
}

/// <summary>
/// ระบบเควสแบบสมุดบันทึก — หลายเควสพร้อมกัน · เรียงตามลำดับที่ได้รับ · เสร็จแล้วอยู่ที่เดิม
/// UI (QuestPage) และระบบอื่นเรียกผ่าน interface นี้ตามหลัก DIP
/// </summary>
public interface IQuestService
{
    /// <summary>ทุกเควสที่เคยได้รับ เรียงตามลำดับที่ได้รับ (เควสที่จบแล้วยังอยู่ตำแหน่งเดิม)</summary>
    IReadOnlyList<QuestData> Journal { get; }

    QuestState GetState(QuestData quest);
    bool IsObjectiveDone(QuestData quest, QuestObjective objective);

    /// <summary>ควรโชว์ข้อนี้ในสมุดไหม (ข้อที่ตั้งซ่อนไว้จะโผล่เมื่อเงื่อนไขครบ)</summary>
    bool IsObjectiveVisible(QuestData quest, QuestObjective objective);

    /// <summary>ข้อแบบนับ — คืน true พร้อม (current/target) · ข้อธรรมดาคืน false</summary>
    bool TryGetObjectiveProgress(QuestData quest, QuestObjective objective, out int current, out int target);

    /// <summary>จดเควสลงสมุด (เควสที่เคยได้รับแล้ว ไม่ว่าจะทำอยู่หรือจบแล้ว จะถูกเมิน)</summary>
    void StartQuest(QuestData quest);

    /// <summary>ยิงเมื่อสมุดควรวาดใหม่</summary>
    event Action OnChanged;
}
