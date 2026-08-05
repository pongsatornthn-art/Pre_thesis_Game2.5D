using UnityEngine.EventSystems;

public interface IInventorySlotUI
{
    void AddItem(ItemData newItem, int amount, bool isHotbar);
    void ClearSlot();
    void OnDrop(PointerEventData eventData);
}