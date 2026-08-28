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
}