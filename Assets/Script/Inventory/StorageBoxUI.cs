using UnityEngine;
using System.Collections.Generic;

public class StorageBoxUI : MonoBehaviour
{
    public GameObject uiPanel;
    public Transform slotsParent;

    public StorageBox currentBox;
    private List<InventorySlotUI> slots = new List<InventorySlotUI>();

    void Start()
    {

        slots.AddRange(slotsParent.GetComponentsInChildren<InventorySlotUI>());
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].slotIndex = i;
            slots[i].isStorageSlot = true;
        }
        uiPanel.SetActive(false);
    }

    public void OpenBox(StorageBox box)
    {
        if (currentBox != null) currentBox.OnStorageChanged -= UpdateUI;

        currentBox = box;
        currentBox.OnStorageChanged += UpdateUI;

        foreach (var slot in slots) slot.currentStorage = currentBox;

        uiPanel.SetActive(true);
        UpdateUI();
    }

    public void CloseBox()
    {
        if (currentBox != null) currentBox.OnStorageChanged -= UpdateUI;
        currentBox = null;
        uiPanel.SetActive(false);
    }

    void UpdateUI()
    {
        if (currentBox == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < currentBox.items.Count && currentBox.items[i] != null && currentBox.items[i].itemData != null)
            {
                slots[i].AddItem(currentBox.items[i].itemData, currentBox.items[i].amount, false);
            }
            else
            {
                slots[i].ClearSlot();
            }
        }
    }
}