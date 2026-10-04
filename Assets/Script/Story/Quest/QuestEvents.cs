// ประกาศของระบบเควสที่วิ่งใน GameEventBus
// ใครอยากรู้ (ป้ายแจ้งเตือน / เสียง / เซฟอัตโนมัติ) Subscribe เอง — QuestService ไม่ต้องรู้ว่ามีใครฟัง
// ไม่ยิงตอนโหลดเซฟ (กันป้ายเด้งรัวทีเดียว 10 อัน)

/// <summary>เควสใหม่ถูกจดลงสมุด</summary>
public readonly struct QuestStartedEvent
{
    public readonly QuestData Quest;
    public QuestStartedEvent(QuestData quest) { Quest = quest; }
}

/// <summary>เป้าหมายที่ซ่อนอยู่ (Hidden Until Available) โผล่ขึ้นมาในสมุด</summary>
public readonly struct ObjectiveRevealedEvent
{
    public readonly QuestData Quest;
    public readonly QuestObjective Objective;
    public ObjectiveRevealedEvent(QuestData quest, QuestObjective objective) { Quest = quest; Objective = objective; }
}

/// <summary>ตัวเลขของเป้าหมายแบบนับเปลี่ยน เช่น (1/4) → (2/4)</summary>
public readonly struct ObjectiveProgressEvent
{
    public readonly QuestData Quest;
    public readonly QuestObjective Objective;
    public readonly int Current;
    public readonly int Target;
    public ObjectiveProgressEvent(QuestData quest, QuestObjective objective, int current, int target)
    {
        Quest = quest; Objective = objective; Current = current; Target = target;
    }
}

/// <summary>ขีดฆ่าเป้าหมาย 1 ข้อ</summary>
public readonly struct ObjectiveCompletedEvent
{
    public readonly QuestData Quest;
    public readonly QuestObjective Objective;
    public ObjectiveCompletedEvent(QuestData quest, QuestObjective objective) { Quest = quest; Objective = objective; }
}

/// <summary>ขีดฆ่าทั้งเควส</summary>
public readonly struct QuestCompletedEvent
{
    public readonly QuestData Quest;
    public QuestCompletedEvent(QuestData quest) { Quest = quest; }
}
