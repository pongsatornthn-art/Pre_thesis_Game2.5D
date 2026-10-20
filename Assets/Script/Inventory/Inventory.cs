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

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public event Action OnInventoryChanged;
    public event Action<ItemData> OnEquipChanged;

    public int space = 30;
    public List<InventoryItem> items = new List<InventoryItem>();

    public ItemData currentEquippedItem { get; private set; }

    // 🌟 เพิ่มตัวแปรสำหรับจดจำ Base Animator (ท่ามือเปล่า) เพื่อให้ตอนถอดอาวุธกลับมายืนปกติได้
    private RuntimeAnimatorController defaultAnimatorController;

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
            int emptyIndex = items.FindIndex(i => i == null || i.itemData == null);
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

        // 🌟 โค้ดดึง Animator มาสวมทับเมื่อกดสวมใส่อาวุธ
        PlayerCombat playerCombat = FindObjectOfType<PlayerCombat>();
        if (playerCombat != null && playerCombat.animator != null)
        {
            // บันทึกท่ามือเปล่าต้นฉบับเก็บไว้ก่อน
            if (defaultAnimatorController == null)
            {
                defaultAnimatorController = playerCombat.animator.runtimeAnimatorController;
            }

            // เช็กว่าเป็นอาวุธประชิด (MeleeWeapon) และตั้งค่า Override (ท่าท่อแป๊ป) ไว้ในไฟล์ไอเทมหรือไม่
            if (itemToEquip is MeleeWeaponData meleeWeapon && meleeWeapon.weaponAnimatorOverride != null)
            {
                // ถ้ามี สั่งเปลี่ยนไปใช้แอนิเมชันอาวุธชิ้นนั้นทันที!
                playerCombat.animator.runtimeAnimatorController = meleeWeapon.weaponAnimatorOverride;
            }
            else
            {
                // ถ้าไม่ใช่ หรือเป็นไอเทมที่ไม่มี Override ให้กลับไปใช้ท่ามือเปล่า
                playerCombat.animator.runtimeAnimatorController = defaultAnimatorController;
            }
        }

        OnEquipChanged?.Invoke(itemToEquip);
    }

    public void Unequip()
    {
        currentEquippedItem = null;

        // 🌟 โค้ดรีเซ็ตกลับเป็นท่ามือเปล่าเมื่อผู้เล่นถอดอาวุธเก็บ
        PlayerCombat playerCombat = FindObjectOfType<PlayerCombat>();
        if (playerCombat != null && playerCombat.animator != null && defaultAnimatorController != null)
        {
            playerCombat.animator.runtimeAnimatorController = defaultAnimatorController;
        }

        OnEquipChanged?.Invoke(null);
    }

    public void SwapItems(int fromIndex, int toIndex)
    {
        while (items.Count <= Mathf.Max(fromIndex, toIndex))
        {
            items.Add(null);
        }

        var temp = items[fromIndex];
        items[fromIndex] = items[toIndex];
        items[toIndex] = temp;

        Debug.Log($"<color=cyan>สลับไอเทมจากช่อง {fromIndex} ไปช่อง {toIndex} เรียบร้อย!</color>");
        OnInventoryChanged?.Invoke();
    }

    // 🌟 เพิ่มใหม่: ระบบโอนย้ายข้ามหน้าต่าง (กระเป๋า <-> กล่อง) 
    public void TransferItemCross(InventorySlotUI fromSlot, InventorySlotUI toSlot)
    {
        List<InventoryItem> fromList = fromSlot.isStorageSlot ? fromSlot.currentStorage.items : this.items;
        List<InventoryItem> toList = toSlot.isStorageSlot ? toSlot.currentStorage.items : this.items;

        while (fromList.Count <= fromSlot.slotIndex) fromList.Add(null);
        while (toList.Count <= toSlot.slotIndex) toList.Add(null);

        var temp = fromList[fromSlot.slotIndex];
        fromList[fromSlot.slotIndex] = toList[toSlot.slotIndex];
        toList[toSlot.slotIndex] = temp;

        Debug.Log("<color=yellow>ย้ายไอเทมข้ามกระเป๋า/กล่อง เรียบร้อย!</color>");

        OnInventoryChanged?.Invoke();
        if (fromSlot.isStorageSlot) fromSlot.currentStorage.RefreshUI();
        if (toSlot.isStorageSlot) toSlot.currentStorage.RefreshUI();
    }
}