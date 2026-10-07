using System.Collections;

/// <summary>ชนิดป้ายแจ้งเตือน — ตัวแสดงผลเอาไปเลือกหน้าตา/สีเองได้</summary>
public enum NotificationKind
{
    QuestStarted,        // บันทึกใหม่
    ObjectiveRevealed,   // เป้าหมายใหม่โผล่
    ObjectiveProgress,   // (2/4)
    ObjectiveCompleted,  // ขีดฆ่า 1 ข้อ
    QuestCompleted,      // ขีดฆ่าทั้งเควส
    MemoryUnlocked,      // ความทรงจำใหม่ (หน้า L)
    GameSaved,
    SaveBlocked
}

/// <summary>ข้อความ 1 ป้าย — แปลภาษาแล้ว พร้อมโชว์</summary>
public readonly struct NotificationMessage
{
    public readonly NotificationKind Kind;
    public readonly string Text;
    public readonly float HoldSeconds;

    public NotificationMessage(NotificationKind kind, string text, float holdSeconds)
    {
        Kind = kind;
        Text = text;
        HoldSeconds = holdSeconds;
    }
}

/// <summary>
/// หน้าตาของป้ายแจ้งเตือน — ⏳ ทำตอนอาร์ตมา (กระดาษ/ลายมือตามธีมสมุด)
/// คลาสที่ implement: แสดงข้อความ → ค้างไว้ HoldSeconds → ซ่อน แล้วจบ coroutine
/// ใช้ UIPanelController สำหรับ fade + เวลาแบบ unscaled (สมุดหยุดเกม)
/// </summary>
public interface INotificationPresenter
{
    IEnumerator Show(NotificationMessage message);
}
