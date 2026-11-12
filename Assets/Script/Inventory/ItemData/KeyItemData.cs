using UnityEngine;

[CreateAssetMenu(fileName = "New Key", menuName = "Inventory/Items/Key")]
public class KeyItemData : ItemData
{
    [Header("Key Settings")]
    public string targetDoorID; // รหัสของประตูที่กุญแจดอกนี้เปิดได้ (เช่น "BossDoor_01")
    public bool consumeOnUse = true; // เปิดแล้วกุญแจหัก/หายไปเลยไหม?

    private void Reset()
    {
        itemType = ItemType.Key;
        isStackable = false; // กุญแจมักจะไม่ทับซ้อนกัน
    }

    /// <summary>ของสำคัญไม่เข้ากระเป๋าปกติ — เข้าคลังแยกที่ไม่กินช่องและทิ้งไม่ได้</summary>
    public override bool Collect(int amount = 1)
    {
        IKeyItemHolder holder = ServiceLocator.Get<IKeyItemHolder>();
        if (holder == null)
        {
            Debug.LogError($"[KeyItemData] เก็บ \"{itemName}\" ไม่ได้ — ยังไม่มี KeyItemHolder ในซีน (ใส่ไว้ที่ก้อน [JOURNAL])");
            return false;
        }

        holder.Add(this);
        // [Claude 2026-11-12] ประกาศว่าได้ของสำคัญ → ป้ายแจ้งเตือนมุมจอ (QuestNotifier ฟังอยู่) · โหลดเซฟไม่ผ่านตรงนี้ จึงไม่เด้งตอนโหลด
        GameEventBus.Publish(new KeyItemCollectedEvent(this));
        return true;
    }
}

/// <summary>[Claude 2026-11-12] ผู้เล่นได้ของสำคัญ (กุญแจ/รูป/เอกสารสำคัญ) — ใช้โชว์ป้าย "ได้รับ ..."</summary>
public readonly struct KeyItemCollectedEvent
{
    public readonly KeyItemData Item;
    public KeyItemCollectedEvent(KeyItemData item) { Item = item; }
}