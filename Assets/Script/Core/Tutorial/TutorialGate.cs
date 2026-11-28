using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] จุดเดียวที่บอกว่า "ตอนนี้ยังไม่ควรเด้งการ์ดคำสอน" (ใช้กับการขึ้นอัตโนมัติเท่านั้น — เปิดดูซ้ำจากสมุดไม่สน)
/// อยากเพิ่มจังหวะที่ห้ามเด้ง → เพิ่มเงื่อนไขที่นี่ที่เดียว
/// </summary>
public static class TutorialGate
{
    /// <summary>เข้าซีน/โหลดเซฟใหม่ ๆ รอให้ทุกอย่างเข้าที่ก่อน (วินาที)</summary>
    public const float StartupDelay = 1.5f;

    public static bool IsBusy => Reason != null;

    /// <summary>เหตุผลที่ยังไม่ว่าง (null = ว่าง) — ไว้ดีบักว่าการ์ดค้างคิวเพราะอะไร</summary>
    public static string Reason
    {
        get
        {
            if (SaveRestoreScope.IsBusy) return "กำลังโหลดเซฟ";
            if (Time.timeSinceLevelLoad < StartupDelay) return "เพิ่งเข้าซีน";
            if (Time.timeScale == 0f) return "เกมหยุดอยู่ (เมนู/ตาย/ฉากโดนจับ)";
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return "เมนูหยุดเกม";
            if (JournalController.IsAnyOpen) return "เปิดสมุดอยู่";
            if (SaveSlotPickerUI.IsOpen) return "หน้าเลือกช่องเซฟ";
            if (MinigameBase.AnyRunning) return "มินิเกม";
            if (DoorBarricadeStruggle.AnyActive) return "ยันประตู";
            if (MinionGrabStruggle.AnyActive) return "โดนมินเนียนตะครุบ";

            IDialogueService dialogue = ServiceLocator.GetOptional<IDialogueService>();
            if (dialogue != null && dialogue.IsPlaying) return "บทพูด";

            IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
            if (world != null && world.Mode != world.VisibleWorld) return "กำลังสลับโลก";

            return null;
        }
    }
}
