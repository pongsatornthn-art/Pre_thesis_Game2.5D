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
        return true;
    }
}