using UnityEngine;

/// <summary>
/// เอกสาร/โน้ตที่ผู้เล่นเก็บได้ — ไม่เข้ากระเป๋า แต่เข้า "สมุดเอกสาร" (DocumentLog) แทน
/// เก็บแล้วอยู่ถาวร อ่านซ้ำได้ตลอดเกม
/// </summary>
[CreateAssetMenu(fileName = "New Document", menuName = "Inventory/Items/Document")]
public class DocumentData : ItemData
{
    [Header("Document Settings")]
    [Tooltip("รหัสไม่ซ้ำ ใช้เช็คว่าเก็บ/อ่านแล้วหรือยัง เช่น NOTE_LAB_01")]
    public string documentId;

    [Tooltip("Key แปลภาษาสำหรับหัวข้อเอกสาร เช่น DOC_LAB_TITLE")]
    public string titleKey = "DOC_UNTITLED_TITLE";

    [Tooltip("Key แปลภาษาสำหรับเนื้อหาเอกสาร เช่น DOC_LAB_BODY (ใส่ 'รหัส' ไม่ใช่เนื้อความ)")]
    public string bodyKey = "DOC_UNTITLED_BODY";

    [Tooltip("รูปประกอบ (เว้นว่างได้)")]
    public Sprite pageImage;

    /// <summary>หัวข้อเอกสารที่แปลตามภาษาปัจจุบัน (ถ้าไม่มี Service หรือหาไม่เจอจะคืน Key กลับไป)</summary>
    public string Title
    {
        get
        {
            if (string.IsNullOrEmpty(titleKey)) return "";
            return ServiceLocator.Get<ILocalizationService>()?.GetText(titleKey) ?? titleKey;
        }
    }

    /// <summary>เนื้อหาเอกสารที่แปลตามภาษาปัจจุบัน</summary>
    public string Body
    {
        get
        {
            if (string.IsNullOrEmpty(bodyKey)) return "";
            return ServiceLocator.Get<ILocalizationService>()?.GetText(bodyKey) ?? bodyKey;
        }
    }

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
            Debug.LogError($"[DocumentData] เก็บ \"{Title}\" ไม่ได้ — ยังไม่มี DocumentLog ในซีน (ใส่ไว้ที่ก้อน [JOURNAL])");
            return false;
        }

        log.Collect(this);
        return true;
    }
}
