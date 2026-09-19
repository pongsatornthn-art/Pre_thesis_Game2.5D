using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ช่องไอเทม 1 ช่องในหน้ากระเป๋าของสมุด — คุมเฉพาะ "หน้าตา" กับ "การคลิกเลือก"
///
/// ทำไมแยกไฟล์ใหม่แทนการไปแก้ InventorySlotUI:
/// InventorySlotUI เป็นของเพื่อนร่วมทีม และถูกใช้ร่วมกับกล่องเก็บของ/hotbar ด้วย
/// ถ้าไปยัดระบบไฮไลท์เข้าไปจะกระทบทุกที่ที่ใช้มัน → เลยเกาะข้าง ๆ แทน
///
/// อาร์ตใช้หลักเดียวกับแท็บ: วาง 2 รูปขนาดเท่ากันซ้อนกัน แล้วเปิด/ปิดรูปบน
/// - รูปล่าง (Image ของก้อนนี้) = Inventory_Slot_01.png   (กรอบปกติ + เลขช่อง)
/// - รูปบน (Selected Frame)     = Inventory_Slot_Selected01.png (กรอบแดง + เลขช่อง)
/// </summary>
[RequireComponent(typeof(InventorySlotUI))]
public class BagSlotVisual : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("ก้อนรูปกรอบไฮไลท์ (วางซ้อนทับกรอบปกติเป๊ะ ๆ) — จะถูกเปิด/ปิดให้อัตโนมัติ")]
    [SerializeField] private GameObject selectedFrame;

    private InventorySlotUI slot;
    private BagSelection owner;

    /// <summary>ไอเทมที่อยู่ในช่องนี้ตอนนี้ (null = ช่องว่าง)</summary>
    public ItemData Item => slot != null ? slot.item : null;

    public bool HasItem => Item != null;

    private void Awake()
    {
        slot = GetComponent<InventorySlotUI>();
        SetSelected(false);
    }

    /// <summary>ตัวคุมกลางเรียกตอนเริ่ม เพื่อบอกว่า "ฉันเป็นเจ้าของนาย"</summary>
    public void Bind(BagSelection selection) => owner = selection;

    public void SetSelected(bool isSelected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(isSelected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // เลือกได้ทุกช่องรวมถึงช่องว่าง (เปลี่ยนตามที่เจ้าของสั่ง 2026-09-19)
        // ช่องว่าง = ไฮไลท์ได้ แต่กระดาษรายละเอียดฝั่งขวาจะซ่อนตัวเอง
        if (owner != null) owner.SelectSlot(this);
    }
}
