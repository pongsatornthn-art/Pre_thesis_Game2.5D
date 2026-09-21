using System;
using UnityEngine;

/// <summary>
/// เงื่อนไขตรวจสอบว่าผู้เล่นมีไอเทมที่กำหนดอยู่ในตัวหรือไม่
/// รองรับทั้ง "คลังของสำคัญ" (IKeyItemHolder) และ "กระเป๋าปกติ" (Inventory)
/// </summary>
[Serializable]
public class HasItemCondition : IStoryCondition
{
    [Header("ตรวจสอบของสำคัญ (Key Item)")]
    [Tooltip("Asset ของสำคัญที่ต้องการตรวจ (เว้นว่างได้)")]
    public KeyItemData keyItemData;

    [Tooltip("รหัส doorId ของกุญแจสำคัญ (เว้นว่างได้)")]
    public string keyItemId;

    [Header("ตรวจสอบกระเป๋าปกติ (Inventory)")]
    [Tooltip("Asset ไอเทมในกระเป๋าปกติ (เว้นว่างได้)")]
    public ItemData regularItem;

    [Tooltip("จำนวนขั้นต่ำที่ต้องมีในกระเป๋าปกติ")]
    public int requiredAmount = 1;

    public bool IsMet(StoryContext ctx)
    {
        // 1. ตรวจสอบกุญแจสำคัญด้วย doorId
        if (!string.IsNullOrEmpty(keyItemId))
        {
            IKeyItemHolder holder = ServiceLocator.Get<IKeyItemHolder>();
            if (holder != null && holder.Has(keyItemId)) return true;
        }

        // 2. ตรวจสอบของสำคัญด้วย KeyItemData Asset
        if (keyItemData != null)
        {
            IKeyItemHolder holder = ServiceLocator.Get<IKeyItemHolder>();
            if (holder != null && holder.All != null)
            {
                for (int i = 0; i < holder.All.Count; i++)
                {
                    if (holder.All[i] == keyItemData) return true;
                }
            }
        }

        // 3. ตรวจสอบไอเทมทั่วไปในกระเป๋า
        if (regularItem != null && Inventory.Instance != null)
        {
            int count = 0;
            if (Inventory.Instance.items != null)
            {
                for (int i = 0; i < Inventory.Instance.items.Count; i++)
                {
                    var slot = Inventory.Instance.items[i];
                    if (slot != null && slot.itemData == regularItem)
                    {
                        count += slot.amount;
                    }
                }
            }
            return count >= requiredAmount;
        }

        return false;
    }
}
