using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เป้าหมายแบบ "เก็บไอเทมให้ครบชุด" — สมุดโชว์ (2/4)
/// ครอบคลุมการ์ด Figma: ชิ้นส่วนรูปภาพ (Memory Key Item) กระจายทั่วบ้าน
///
/// ใส่ไอเทมหลายชิ้นในลิสต์ (เช่น ชิ้นส่วนรูปภาพ 1–4 เป็น KeyItemData คนละไฟล์) → นับว่ามีกี่ชิ้นแล้ว
/// ไอเทมอยู่คลังไหนก็นับได้ (ของสำคัญ / เอกสาร / กระเป๋า) ผ่าน ItemOwnership
/// วางไอเทมในแมพด้วย ItemPickup ของเพื่อนตามปกติ — ไม่ต้องใช้ StoryCollectible
/// </summary>
[Serializable, PickerName("เก็บไอเทมให้ครบชุด (x/N)")]
public class CollectItemsObjective : QuestObjective
{
    [Tooltip("ไอเทมที่ต้องเก็บ (ลากไฟล์ไอเทมใส่ — ชิ้นซ้ำกันได้ถ้าเป็นไอเทมกระเป๋าที่ซ้อนกัน)")]
    public List<ItemData> items = new List<ItemData>();

    [Tooltip("ต้องมีกี่ชิ้น (0 = ครบทุกชิ้นในลิสต์)")]
    [Min(0)] public int required;

    public override bool IsMet(StoryContext ctx)
    {
        TryGetProgress(ctx, out int current, out int target);
        return target > 0 && current >= target;
    }

    public override bool TryGetProgress(StoryContext ctx, out int current, out int target)
    {
        int distinct = CountListed();
        target = required > 0 ? required : distinct;
        current = 0;

        if (items != null)
        {
            HashSet<ItemData> seen = new HashSet<ItemData>();
            foreach (ItemData item in items)
            {
                if (item == null || !seen.Add(item)) continue;
                current += Mathf.Min(ItemOwnership.Count(item), CountInList(item));
            }
        }

        current = Mathf.Min(current, target);
        return true;
    }

    private int CountListed()
    {
        int n = 0;
        if (items != null) foreach (ItemData item in items) if (item != null) n++;
        return n;
    }

    private int CountInList(ItemData item)
    {
        int n = 0;
        foreach (ItemData i in items) if (i == item) n++;
        return n;
    }
}
