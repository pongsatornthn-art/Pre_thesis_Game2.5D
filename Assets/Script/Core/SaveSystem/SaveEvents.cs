// ประกาศของระบบเซฟใน GameEventBus — UI (ป้าย "บันทึกแล้ว" / "บันทึกไม่ได้ตอนนี้") มาฟังเอง

public enum SaveReason
{
    Manual,      // จุดเซฟในแมพ
    Auto,        // เซฟอัตโนมัติ (เควสจบ / ออกจาก PTSD)
}

/// <summary>เซฟลงไฟล์สำเร็จ</summary>
public readonly struct GameSavedEvent
{
    public readonly SaveReason Reason;
    public GameSavedEvent(SaveReason reason) { Reason = reason; }
}

/// <summary>ขอเซฟแต่ไม่อนุญาต — ReasonKey เป็น key แปลภาษา เอาไปโชว์ได้เลย</summary>
public readonly struct SaveBlockedEvent
{
    public readonly SaveReason Reason;
    public readonly string ReasonKey;
    public SaveBlockedEvent(SaveReason reason, string reasonKey) { Reason = reason; ReasonKey = reasonKey; }
}

/// <summary>โหลดเสร็จ ใส่ความจำคืนครบแล้ว</summary>
public readonly struct GameLoadedEvent
{
    public readonly bool FromCheckpoint;
    public GameLoadedEvent(bool fromCheckpoint) { FromCheckpoint = fromCheckpoint; }
}

/// <summary>
/// สถานะ "กำลังโหลด" ที่ระบบอื่นต้องรู้ — เช่น QuestService ห้ามยิงป้ายแจ้งเตือนระหว่างใส่ความจำคืน
/// แยกเป็น static เล็กๆ เพื่อไม่ให้ระบบอื่นต้องพึ่งคลาส SaveManager ทั้งตัว
/// </summary>
public static class SaveRestoreScope
{
    /// <summary>มีเซฟรอใส่คืนหลังซีนโหลดเสร็จ</summary>
    public static bool HasPendingRestore { get; internal set; }

    /// <summary>กำลังเรียก RestoreState ของทุกชิ้นอยู่</summary>
    public static bool IsRestoring { get; internal set; }

    public static bool IsBusy => HasPendingRestore || IsRestoring;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HasPendingRestore = false;
        IsRestoring = false;
    }
}

/// <summary>
/// ไม่บังคับ — ISaveable ที่ต้องโหลด "ก่อน" ชิ้นอื่นให้ implement เพิ่ม
/// ค่าน้อยโหลดก่อน · ไม่ implement = 0
/// ความจำกลาง (ธง/ตัวนับ) = -100 · เควส = -50 → ของในซีนที่ซิงก์ตามธงจะเห็นค่าที่ถูกเสมอ
/// </summary>
public interface ISaveRestoreOrder
{
    int RestoreOrder { get; }
}
