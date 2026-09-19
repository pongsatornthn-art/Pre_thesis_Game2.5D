using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้ากระดาษฝั่งขวาของสมุด — โชว์รายละเอียดไอเทมที่เลือกอยู่ในช่องกลาง
///
/// ทั้งก้อนนี้อยู่ใน Page_Bag (ไม่ใช่ในพื้นหลังสมุด)
/// เวลาสลับไปหน้าของสำคัญ/เอกสาร กระดาษใบนี้จะหายไปพร้อมหน้ากระเป๋าเอง
/// </summary>
public class BagDetailPanel : MonoBehaviour
{
    [Header("ก้อนทั้งหมดของกระดาษฝั่งขวา (ปิดตอนไม่ได้เลือกอะไร)")]
    [Tooltip("ลากก้อนที่มีทั้งกระดาษ+รูป+ข้อความ — เว้นว่าง = ใช้ก้อนนี้เอง")]
    [SerializeField] private GameObject content;

    [Header("ช่องแสดงผล")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;

    private ILocalizationService locService;

    private void Awake()
    {
        if (content == null) content = gameObject;
        locService = ServiceLocator.Get<ILocalizationService>();
    }

    /// <summary>โชว์รายละเอียดของไอเทม (ส่ง null = ซ่อนกระดาษทั้งใบ)</summary>
    public void Show(ItemData item)
    {
        if (item == null)
        {
            Clear();
            return;
        }

        if (content != null) content.SetActive(true);

        if (icon != null)
        {
            icon.sprite = item.icon;
            // ไม่มีรูปก็ซ่อนไปเลย จะได้ไม่เห็นสี่เหลี่ยมขาวเปล่า ๆ
            icon.enabled = item.icon != null;
        }

        if (locService == null) locService = ServiceLocator.Get<ILocalizationService>();

        // ใช้ DisplayName/DisplayDescription เพื่อให้แปลภาษาได้ถ้าไอเทมนั้นใส่ key ไว้
        if (nameText != null)
        {
            nameText.text = item.DisplayName;
            if (locService != null) nameText.font = locService.GetFont(FontCategory.Header);
        }

        if (descriptionText != null)
        {
            descriptionText.text = item.DisplayDescription;
            if (locService != null) descriptionText.font = locService.GetFont(FontCategory.Default);
        }
    }

    /// <summary>ไม่ได้เลือกอะไรอยู่ = ซ่อนกระดาษ</summary>
    public void Clear()
    {
        if (content != null) content.SetActive(false);
    }
}
