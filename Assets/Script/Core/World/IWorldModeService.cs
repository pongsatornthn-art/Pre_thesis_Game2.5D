using System;

/// <summary>ตอนนี้ผู้เล่นอยู่โลกไหน</summary>
public enum WorldMode
{
    Real,            // โลกปกติ
    PtsdSurvival,    // โลก PTSD แบบ A — เอาชีวิตรอด มีผี
    PtsdNarrative    // โลก PTSD แบบ B — สืบเรื่อง
}

/// <summary>
/// ตัวกลางเรื่อง "โลกปกติ ↔ โลก PTSD" — ระบบของเรา (เซฟ / เควส / ตาย / ฉากเนื้อเรื่อง) คุยผ่านตัวนี้เท่านั้น
/// ไม่มีใครเรียก PTSDManager (ของเพื่อน) ตรงๆ ยกเว้นตัวแปลง PtsdWorldModeAdapter
/// → ถ้าวันหนึ่งเพื่อนเปลี่ยนวิธีสลับโลก แก้ที่ตัวแปลงที่เดียว (Adapter + DIP)
/// </summary>
public interface IWorldModeService
{
    WorldMode Mode { get; }
    bool IsInPtsd { get; }

    /// <summary>
    /// โลกที่ "ตาเห็น" ตอนนี้ — ต่างจาก Mode ช่วงอนิเมชันสลับโลก (Mode เปลี่ยนทันทีที่สั่ง แต่ฉากเพิ่งสลับตอนจอมืดสุด)
    /// ของที่โผล่/หายตามโลก (WorldPresence) ใช้ตัวนี้ จะได้สลับพร้อมฉากของปอ
    /// </summary>
    WorldMode VisibleWorld { get; }

    void EnterPtsd(WorldMode mode);
    void ExitPtsd();

    /// <summary>(ก่อนหน้า, ตอนนี้)</summary>
    event Action<WorldMode, WorldMode> OnModeChanged;
}

/// <summary>ประกาศตอนฉากสลับเสร็จจริง (จอมืดสุด · ตรงกับ OnEnterPTSD/OnExitPTSD ของ PTSDManager)</summary>
public readonly struct WorldVisualsSwappedEvent
{
    public readonly WorldMode Visible;
    public WorldVisualsSwappedEvent(WorldMode visible) { Visible = visible; }
}

/// <summary>ประกาศใน GameEventBus ทุกครั้งที่สลับโลก</summary>
public readonly struct WorldModeChangedEvent
{
    public readonly WorldMode Previous;
    public readonly WorldMode Current;
    public WorldModeChangedEvent(WorldMode previous, WorldMode current) { Previous = previous; Current = current; }

    public bool EnteredPtsd => Previous == WorldMode.Real && Current != WorldMode.Real;
    public bool ExitedPtsd => Previous != WorldMode.Real && Current == WorldMode.Real;
}
