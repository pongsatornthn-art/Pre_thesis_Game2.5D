using System;
using System.Collections.Generic;

/// <summary>
/// สมุดเก็บ "เอกสาร/โน้ต" ที่ผู้เล่นเก็บได้ระหว่างเล่น
/// เก็บแล้วอยู่ถาวร อ่านซ้ำได้ ไม่กินช่องกระเป๋า (มาตรฐานเกม horror)
/// </summary>
public interface IDocumentLog
{
    /// <summary>ยิงทุกครั้งที่เก็บเอกสารใหม่ หรือสถานะอ่านเปลี่ยน</summary>
    event Action OnChanged;

    /// <summary>เอกสารทั้งหมดที่เก็บได้แล้ว (อ่านอย่างเดียว)</summary>
    IReadOnlyList<DocumentData> All { get; }

    /// <summary>เก็บเอกสารเข้าสมุด (เก็บซ้ำไม่ได้ เช็คด้วย documentId)</summary>
    void Collect(DocumentData doc);

    /// <summary>เคยเก็บเอกสารรหัสนี้แล้วหรือยัง</summary>
    bool HasCollected(string documentId);

    /// <summary>เคยเปิดอ่านแล้วหรือยัง (ไว้ทำจุดแดง "ยังไม่อ่าน")</summary>
    bool HasRead(string documentId);

    /// <summary>ทำเครื่องหมายว่าอ่านแล้ว</summary>
    void MarkRead(string documentId);
}
