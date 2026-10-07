using System;
using System.Collections.Generic;

/// <summary>
/// คลังความทรงจำ (Codex) — หน้า L ของสมุด · ปลดแล้วปลดถาวร
/// UI / มินิเกม / เงื่อนไข / ItemOwnership เรียกผ่าน interface นี้ (DIP)
/// </summary>
public interface IMemoryArchive
{
    /// <summary>ทุกช่องตามลำดับใน Catalog (รวมช่องที่ยังไม่ปลด)</summary>
    IReadOnlyList<MemoryEntryData> Entries { get; }

    bool IsUnlocked(MemoryEntryData entry);

    /// <summary>ปลดแล้วแต่ผู้เล่นยังไม่ได้เปิดดู (จุดแดง)</summary>
    bool IsNew(MemoryEntryData entry);

    /// <summary>ปลดช่อง — ปลดแล้วเรียกซ้ำไม่มีผล</summary>
    void Unlock(MemoryEntryData entry);

    /// <summary>ผู้เล่นเปิดดูแล้ว (เอาจุดแดงออก)</summary>
    void MarkSeen(MemoryEntryData entry);

    event Action OnChanged;
}

/// <summary>ประกาศใน GameEventBus ตอนปลดช่องใหม่ (ไม่ยิงตอนโหลดเซฟ) — ป้ายแจ้งเตือน/เสียงมาฟัง</summary>
public readonly struct MemoryUnlockedEvent
{
    public readonly MemoryEntryData Entry;
    public MemoryUnlockedEvent(MemoryEntryData entry) { Entry = entry; }
}
