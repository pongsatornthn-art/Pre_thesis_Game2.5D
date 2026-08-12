using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IDropHandler, IInventorySlotUI
{
    public Image icon;
    public TextMeshProUGUI amountText;
    public ItemData item;
    public int slotIndex;

    public void AddItem(ItemData newItem, int amount, bool isHotbar)
    {
        item = newItem;

        if (icon == null)
        {
            icon = transform.Find("ItemIcon")?.GetComponent<Image>();
        }

        if (icon != null && item != null && item.icon != null)
        {
            icon.sprite = item.icon;
            icon.color = new Color(1, 1, 1, 1);
        }

        if (amountText != null)
        {
            // 🌟 แก้ตรงบรรทัดนี้ครับ เปลี่ยนจาก (amount > 1) เป็น (amount >= 1)
            if (amount >= 1)
            {
                amountText.text = amount.ToString();
                amountText.gameObject.SetActive(true);
            }
            else
            {
                amountText.gameObject.SetActive(false);
            }
        }
    }

    public void ClearSlot()
    {
        item = null;

        if (icon != null)
        {
            icon.sprite = null;
            icon.color = new Color(1, 1, 1, 0); // ทำให้รูปไอเทมโปร่งใส
        }

        if (amountText != null)
        {
            // ซ่อนเลข 99 (หรือตัวเลขใดๆ) ทิ้งไปเลยเมื่อช่องนี้ว่างเปล่า
            amountText.text = "";
            amountText.gameObject.SetActive(false);
        }
    }
    // ฟังก์ชันนี้จะทำงานเมื่อมีการ "ปล่อยเมาส์" ใส่ช่องนี้
    public void OnDrop(PointerEventData eventData)
    {
        // เช็กว่าของที่กำลังโดนลากมาคืออะไร
        GameObject droppedObject = eventData.pointerDrag;

        if (droppedObject != null)
        {
            ItemDrag draggedItem = droppedObject.GetComponent<ItemDrag>();

            // ถ้าสิ่งที่ลากมาคือไอเทม และไม่ได้ปล่อยกลับลงไปที่ช่องเดิมของตัวเอง
            if (draggedItem != null && draggedItem.mySlot != this)
            {
                // สั่งระบบกระเป๋าหลักให้สลับของระหว่าง 2 ช่องนี้!
                Inventory.Instance.SwapItems(draggedItem.mySlot.slotIndex, slotIndex);
            }
        }
    }
}