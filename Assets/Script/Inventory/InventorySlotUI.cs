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

    // 🌟 ระบบกล่อง
    [Header("Storage System")]
    public bool isStorageSlot = false;
    public StorageBox currentStorage;

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
            icon.color = new Color(1, 1, 1, 0);
        }

        if (amountText != null)
        {
            amountText.text = "";
            amountText.gameObject.SetActive(false);
        }
    }

    // 🌟 อัปเดตระบบ OnDrop
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;

        if (droppedObject != null)
        {
            ItemDrag draggedItem = droppedObject.GetComponent<ItemDrag>();
            InventorySlotUI fromSlot = draggedItem?.mySlot as InventorySlotUI;

            if (fromSlot == null && draggedItem != null)
            {
                // เผื่อว่า mySlot ของคุณพงศธรไม่ได้เก็บค่าเป็น InventorySlotUI โดยตรง
                fromSlot = draggedItem.mySlot.GetComponent<InventorySlotUI>();
            }

            if (fromSlot != null && fromSlot != this)
            {
                if (!fromSlot.isStorageSlot && !this.isStorageSlot)
                {
                    // กระเป๋า <-> กระเป๋า
                    Inventory.Instance.SwapItems(fromSlot.slotIndex, slotIndex);
                }
                else if (fromSlot.isStorageSlot && this.isStorageSlot && currentStorage != null)
                {
                    // กล่อง <-> กล่อง
                    currentStorage.SwapItems(fromSlot.slotIndex, slotIndex);
                }
                else
                {
                    // กระเป๋า <-> กล่อง 
                    Inventory.Instance.TransferItemCross(fromSlot, this);
                }
            }
        }
    }
}