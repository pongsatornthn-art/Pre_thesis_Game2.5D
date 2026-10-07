using UnityEngine;

/// <summary>
/// ตอบคำถามเดียว: "ผู้เล่นมีไอเทมนี้อยู่ไหม" — ไม่ว่าไอเทมจะไปอยู่คลังไหน
///   KeyItemData  → คลังของสำคัญ (หน้า K)
///   DocumentData → สมุดเอกสาร (หน้า N)
///   MemoryFragmentData → ไม่โชว์ที่ไหน จำไว้ในความจำกลาง — "มี" = เคยเก็บแล้ว
///   ItemData อื่น → กระเป๋า (หน้า I / hotbar)
/// ตรงกับที่ ItemData.Collect() ส่งของไปแต่ละคลัง — เพิ่มคลังใหม่ แก้ที่นี่ที่เดียว
/// ใช้โดย: CollectItemsObjective · HasItemsCondition · DeliverPoint
/// </summary>
public static class ItemOwnership
{
    public static int Count(ItemData item)
    {
        if (item == null) return 0;

        switch (item)
        {
            case KeyItemData key:
                IKeyItemHolder holder = ServiceLocator.GetOptional<IKeyItemHolder>();
                if (holder?.All == null) return 0;
                for (int i = 0; i < holder.All.Count; i++) if (holder.All[i] == key) return 1;
                return 0;

            case DocumentData doc:
                IDocumentLog log = ServiceLocator.GetOptional<IDocumentLog>();
                return log != null && log.HasCollected(doc.documentId) ? 1 : 0;

            case MemoryFragmentData fragment:
                return fragment.IsCollected() ? 1 : 0;

            default:
                return Inventory.Instance != null ? Inventory.Instance.GetItemCount(item) : 0;
        }
    }

    public static bool Has(ItemData item, int amount = 1) => Count(item) >= Mathf.Max(1, amount);

    /// <summary>
    /// เอาไอเทมออก (ส่งของ / ใช้ของ) — ของสำคัญหักผ่าน Consume ของคลัง (หักเฉพาะชิ้นที่ตั้ง consumeOnUse)
    /// เอกสารไม่หัก (สมุดเก็บถาวรตามแผน INVENTORY_PLAN)
    /// </summary>
    public static void Remove(ItemData item, int amount = 1)
    {
        switch (item)
        {
            case null:
                return;
            case KeyItemData key:
                ServiceLocator.GetOptional<IKeyItemHolder>()?.Consume(key.targetDoorID);
                return;
            case DocumentData _:
            case MemoryFragmentData _:
                return;   // เอกสาร/เศษที่เก็บแล้ว เก็บถาวร ไม่หัก
            default:
                if (Inventory.Instance != null) Inventory.Instance.RemoveItem(item, amount);
                return;
        }
    }
}
