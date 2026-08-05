using System;
using UnityEngine;

/// <summary>
/// ⭐ รวมจุดที่เคยทำงานซ้ำกัน 2 ที่ (HotbarController เดิม + SelectHotbarSlot/HandleHotbarInput ใน InventoryUI เดิม)
/// ให้เหลือจุดเดียว: สคริปต์นี้เป็นเจ้าของ "ช่อง hotbar ที่เลือกอยู่" ทั้งหมด
/// InventoryUI แค่ subscribe event เพื่ออัปเดตกรอบไฮไลต์เท่านั้น ไม่ทำ logic ซ้ำอีก
/// </summary>
public class HotbarController : MonoBehaviour
{
    [Tooltip("จำนวนช่อง hotbar ที่ใช้ปุ่มเลข (1-9) ได้")]
    public int hotbarSize = 9;

    public int CurrentSlotIndex { get; private set; } = 0;

    /// <summary>แจ้งเมื่อผู้เล่นเปลี่ยนช่อง hotbar เพื่อให้ UI (กรอบไฮไลต์) อัปเดตตาม</summary>
    public event Action<int> OnHotbarSlotSelected;

    Inventory inventory;

    void Start()
    {
        inventory = Inventory.Instance;
        if (inventory != null)
        {
            inventory.OnInventoryChanged += RefreshCurrentSlot;
        }
        RefreshCurrentSlot();
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= RefreshCurrentSlot;
    }

    void Update()
    {
        for (int i = 0; i < hotbarSize; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectSlot(i);
            }
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= hotbarSize) return;

        CurrentSlotIndex = index;
        OnHotbarSlotSelected?.Invoke(index);
        RefreshCurrentSlot();
    }

    /// <summary>สั่ง Inventory ให้สวมใส่/ถอดของตามช่องที่เลือกอยู่ตอนนี้ (เรียกซ้ำได้ปลอดภัยเมื่อของในกระเป๋าเปลี่ยน)</summary>
    void RefreshCurrentSlot()
    {
        if (inventory == null || inventory.items == null) return;
        if (CurrentSlotIndex >= inventory.items.Count) return;

        var slot = inventory.items[CurrentSlotIndex];

        if (slot != null)
            inventory.EquipItem(slot.itemData);
        else
            inventory.Unequip();
    }
}