using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Inventory/Items/Consumable")]
public class ConsumableItemData : ItemData, IUsable
{
    [Header("Consumable Stats")]
    public int healAmount = 50;
    public float digestionReduceAmount = 20f;

    private void Reset() { itemType = ItemType.Consumable; }

    /// <summary>ใช้ยา — ฟื้นเลือดให้ผู้ใช้</summary>
    public bool Use(GameObject user)
    {
        if (user == null) return false;

        PlayerMovement player = user.GetComponent<PlayerMovement>();
        if (player == null) player = user.GetComponentInParent<PlayerMovement>();

        if (player == null)
        {
            Debug.LogWarning($"[{itemName}] ใช้ไม่ได้ — หา PlayerMovement ไม่เจอ");
            return false;
        }

        if (healAmount > 0) player.Heal(healAmount);
        GameEventBus.Publish(new ConsumableUsedEvent(this));   // [Claude 2026-11-12] PtsdCheckpoint ฟัง — ยาที่กินใน PTSD ไม่คืนตอนตาย

        // digestionReduceAmount ยังไม่มีระบบรองรับ (เผื่อไว้ทำทีหลัง)
        return true;
    }
}

/// <summary>[Claude 2026-11-12] ผู้เล่นใช้ของกินได้ (ยา) ไปแล้ว 1 ชิ้น</summary>
public readonly struct ConsumableUsedEvent
{
    public readonly ConsumableItemData Item;
    public ConsumableUsedEvent(ConsumableItemData item) { Item = item; }
}
