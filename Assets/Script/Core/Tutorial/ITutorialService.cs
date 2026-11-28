using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// [Claude 2026-11-28] บริการคำสอน — ตัดสินใจว่าเคยขึ้นยัง / เข้าคิว / รอจังหวะว่าง
/// เรียกใช้: ServiceLocator.GetOptional&lt;ITutorialService&gt;()?.Request(data)
/// </summary>
public interface ITutorialService
{
    /// <summary>การ์ดกำลังโชว์อยู่ไหม</summary>
    bool IsShowing { get; }

    bool HasSeen(TutorialData tutorial);

    /// <summary>ขอให้ขึ้น (ครั้งเดียว) — เคยขึ้นแล้วไม่ทำอะไร · ยังไม่ว่าง (บทพูด/หยุดเกม/สลับโลก) = เข้าคิวรอ</summary>
    void Request(TutorialData tutorial);

    /// <summary>เหมือน Request แต่ใช้รหัส (ต้องอยู่ใน Catalog ของ TutorialService) · ไม่เจอรหัส = false + เตือน</summary>
    bool Request(string tutorialId);

    /// <summary>เปิดดูซ้ำทันที ไม่สนว่าเคยขึ้น (ใช้จากสมุดแท็บ Tutorials)</summary>
    void Replay(TutorialData tutorial);

    /// <summary>ปิดการ์ดที่โชว์อยู่ (เช่น กด Esc ในสมุด)</summary>
    void CloseCurrent();

    /// <summary>คำสอนที่เคยขึ้นแล้วและให้โชว์ในสมุด — เรียงตาม Catalog</summary>
    IReadOnlyList<TutorialData> SeenForJournal { get; }

    /// <summary>มีคำสอนเคยขึ้นเพิ่ม / โหลดเซฟ → สมุดวาดรายการใหม่</summary>
    event Action OnChanged;
}

/// <summary>หน้าตาการ์ด — แยกจากตัวตัดสินใจ (อาร์ตเปลี่ยนกี่รอบไม่ต้องแตะ TutorialService)</summary>
public interface ITutorialPresenter
{
    bool IsOpen { get; }

    /// <summary>โชว์การ์ดแล้วรอจนผู้เล่นปิด</summary>
    IEnumerator Show(TutorialData tutorial);

    void Close();
}
