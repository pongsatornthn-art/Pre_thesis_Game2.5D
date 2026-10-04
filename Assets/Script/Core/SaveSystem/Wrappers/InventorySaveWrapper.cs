using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เซฟ/โหลดของในกระเป๋า (hotbar + กระเป๋า) — แบบเดียวกับ SpawnerSaveWrapper
///
/// Inventory เป็นไฟล์ของเพื่อน และไม่ได้ implement ISaveable → ตัวนี้อ่าน/เขียนผ่าน API สาธารณะของเขา
/// **ไม่แก้ไฟล์เพื่อน** · วางที่ [STORY] คู่กับ SaveableEntity (ไม่ต้องแปะที่ตัว Player)
///
/// ช่องที่ถืออยู่ / กระสุนในแม็ก / เลือด → PlayerStateSaveWrapper (โหลดหลังตัวนี้)
/// </summary>
[RequireComponent(typeof(SaveableEntity))]
public class InventorySaveWrapper : MonoBehaviour, ISaveable, ISaveRestoreOrder
{
    // ของต้องอยู่ในกระเป๋าก่อน PlayerStateSaveWrapper (10) จะเลือกช่องถือ
    public int RestoreOrder => 0;

    [Tooltip("เว้นว่าง = หา Resources/ItemCatalog เอง")]
    [SerializeField] private ItemCatalog catalog;

    [Serializable]
    private class SlotSave
    {
        public int index;
        public string itemId;
        public int amount;
    }

    [Serializable]
    private class InventorySaveData
    {
        public List<SlotSave> slots = new List<SlotSave>();
    }

    public string CaptureState()
    {
        InventorySaveData data = new InventorySaveData();
        Inventory inv = Inventory.Instance;
        if (inv == null || inv.items == null) return JsonUtility.ToJson(data);

        for (int i = 0; i < inv.items.Count; i++)
        {
            InventoryItem slot = inv.items[i];
            if (slot == null || slot.itemData == null || slot.amount <= 0) continue;
            data.slots.Add(new SlotSave { index = i, itemId = ItemCatalog.GetId(slot.itemData), amount = slot.amount });
        }
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null || string.IsNullOrEmpty(stateJson)) return;

        if (catalog == null) catalog = ItemCatalog.LoadDefault();
        if (catalog == null)
        {
            Debug.LogError("[InventorySaveWrapper] ไม่มี ItemCatalog — ของในกระเป๋าโหลดไม่ได้ (สร้างที่ Assets/Resources/ItemCatalog)");
            return;
        }

        InventorySaveData data = JsonUtility.FromJson<InventorySaveData>(stateJson);

        // ใส่ทีละช่องตรงตำแหน่งเดิม — hotbar ช่อง 1-4 ต้องอยู่ช่องเดิม (AddItem ของเพื่อนจะอัดเข้าช่องว่างแรก ตำแหน่งเพี้ยน)
        for (int i = 0; i < inv.items.Count; i++) inv.items[i] = null;

        if (data?.slots != null)
        {
            foreach (SlotSave s in data.slots)
            {
                if (s.index < 0 || s.index >= inv.items.Count) continue;
                ItemData item = catalog.Find(s.itemId);
                if (item != null) inv.items[s.index] = new InventoryItem(item, s.amount);
            }
        }

        // แจ้ง UI ให้วาดใหม่: RemoveItem(null, 0) ของเพื่อนไม่เจอช่องไหนตรง (เพิ่งล้างช่องว่างเป็น null หมดแล้ว)
        // จึงไม่ลบอะไร แค่ยิง OnInventoryChanged — เป็นทางเดียวที่ยิง event ได้จากข้างนอกโดยไม่แก้ไฟล์เพื่อน
        inv.RemoveItem(null, 0);
    }
}
