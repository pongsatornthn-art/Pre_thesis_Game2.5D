using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public ItemData itemData;
    public int amount;

    public InventoryItem(ItemData item, int qty)
    {
        itemData = item;
        amount = qty;
    }

    public void AddAmount(int value) => amount += value;
}

/// <summary>
/// ระบบกระเป๋าของผู้เล่น (ตัวข้อมูล ไม่ผูกกับสคริปต์ตัวละครหรือ UI ตัวใดโดยเฉพาะ)
/// เกมใหม่สามารถ subscribe OnInventoryChanged / OnEquipChanged แทนการแก้โค้ดไฟล์นี้
/// </summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    /// <summary>เรียกทุกครั้งที่กระเป๋ามีการเปลี่ยนแปลง (เพิ่ม/ลบ/สลับของ)</summary>
    public event Action OnInventoryChanged;

    /// <summary>เรียกทุกครั้งที่ของที่ "สวมใส่/ถืออยู่" เปลี่ยน (null = ถอดออก)
    /// ให้สคริปต์ตัวละคร/อนิเมชันของเกมใหม่มา subscribe ตรงนี้แทนการอ้างอิง PlayerController ตรงๆ</summary>
    public event Action<ItemData> OnEquipChanged;

    public int space = 30;
    public List<InventoryItem> items = new List<InventoryItem>();

    public ItemData currentEquippedItem { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        while (items.Count < space) items.Add(null);
    }

    // ---------------- จัดการไอเทม ----------------

    public bool AddItem(ItemData item, int amount = 1)
    {
        InventoryItem existingItem = items.Find(i =>
            i != null && i.itemData == item && i.itemData.isStackable && i.amount < i.itemData.maxStack);

        if (existingItem != null)
        {
            existingItem.AddAmount(amount);
        }
        else
        {
            int emptyIndex = items.FindIndex(i => i == null);
            if (emptyIndex == -1)
            {
                Debug.Log("กระเป๋าเต็ม!");
                return false;
            }
            items[emptyIndex] = new InventoryItem(item, amount);
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public int GetItemCount(ItemData item)
    {
        if (items == null) return 0;
        int total = 0;
        foreach (var slot in items)
            if (slot != null && slot.itemData == item) total += slot.amount;
        return total;
    }

    public bool HasItem(ItemData item, int amountRequired = 1) => GetItemCount(item) >= amountRequired;

    public void RemoveItem(ItemData item, int amountToRemove = 1)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null || items[i].itemData != item) continue;

            if (items[i].amount > amountToRemove)
            {
                items[i].amount -= amountToRemove;
                amountToRemove = 0;
            }
            else
            {
                amountToRemove -= items[i].amount;
                if (currentEquippedItem == item) Unequip();
                items[i] = null;
            }

            if (amountToRemove <= 0) break;
        }
        OnInventoryChanged?.Invoke();
    }

    public List<InventoryItem> DropAllItemsExcept(string keepItemName)
    {
        var dropped = new List<InventoryItem>();

        for (int i = 0; i < items.Count; i++)
        {
            var slot = items[i];
            if (slot?.itemData == null) continue;
            if (slot.itemData.itemName == keepItemName) continue;

            dropped.Add(new InventoryItem(slot.itemData, slot.amount));
            if (currentEquippedItem == slot.itemData) Unequip();
            items[i] = null;
        }

        OnInventoryChanged?.Invoke();
        return dropped;
    }

    public InventoryItem GetItemAt(int index) =>
        (index >= 0 && index < items.Count) ? items[index] : null;

    // ---------------- สวมใส่ / ถืออาวุธ ----------------

    public void EquipItem(ItemData itemToEquip)
    {
        currentEquippedItem = itemToEquip;
        OnEquipChanged?.Invoke(itemToEquip);
    }

    public void Unequip()
    {
        currentEquippedItem = null;
        OnEquipChanged?.Invoke(null);
    }
    // ให้อยู่ใน Inventory.cs
    public void SwapItems(int fromIndex, int toIndex)
    {
        // เติมช่องว่าง (null) ให้เต็มกัน Error ในกรณีที่ลากไปวางในช่องที่ยังไม่เคยมีข้อมูลเลย
        while (items.Count <= Mathf.Max(fromIndex, toIndex))
        {
            items.Add(null);
        }

        // สลับข้อมูลใน List
        var temp = items[fromIndex];
        items[fromIndex] = items[toIndex];
        items[toIndex] = temp;

        Debug.Log($"<color=cyan>สลับไอเทมจากช่อง {fromIndex} ไปช่อง {toIndex} เรียบร้อย!</color>");

        // ตรงนี้สำคัญ: ถ้าคุณพงศธรมีระบบอัปเดต UI (เช่น event หรือเรียก UI_Manager) อย่าลืมเรียกใช้นะครับ
        // เช่น OnInventoryChanged?.Invoke(); หรือถ้าผูก UpdateUI ไว้ในฟังก์ชัน Update() อยู่แล้วก็จะอัปเดตทันทีครับ
    }
}