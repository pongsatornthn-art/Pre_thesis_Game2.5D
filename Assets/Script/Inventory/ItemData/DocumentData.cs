using UnityEngine;

/// <summary>
/// เอกสาร/โน้ตที่ผู้เล่นเก็บได้ — ไม่เข้ากระเป๋า แต่เข้า "สมุดเอกสาร" (DocumentLog) แทน
/// เก็บแล้วอยู่ถาวร อ่านซ้ำได้ตลอดเกม
/// </summary>
[CreateAssetMenu(fileName = "New Document", menuName = "Inventory/Items/Document")]
public class DocumentData : ItemData
{
    [Header("Document")]
    [Tooltip("รหัสไม่ซ้ำ ใช้เช็คว่าเก็บ/อ่านแล้วหรือยัง เช่น NOTE_LAB_01")]
    public string documentId;

    [Tooltip("หัวข้อที่โชว์ในสารบัญหน้าเอกสาร")]
    public string title = "เอกสารไม่มีชื่อ";

    [Tooltip("เนื้อความที่โชว์หน้าขวา")]
    [TextArea(5, 20)] public string body;

    [Tooltip("รูปประกอบ (เว้นว่างได้)")]
    public Sprite pageImage;

    private void Reset()
    {
        itemType = ItemType.Document;
        isStackable = false;
    }

    /// <summary>เก็บเข้าสมุดเอกสารแทนกระเป๋า</summary>
    public override bool Collect(int amount = 1)
    {
        IDocumentLog log = ServiceLocator.Get<IDocumentLog>();
        if (log == null)
        {
            Debug.LogError($"[DocumentData] เก็บ \"{title}\" ไม่ได้ — ยังไม่มี DocumentLog ในซีน (ใส่ไว้ที่ก้อน [JOURNAL])");
            return false;
        }

        log.Collect(this);
        return true;
    }
}
