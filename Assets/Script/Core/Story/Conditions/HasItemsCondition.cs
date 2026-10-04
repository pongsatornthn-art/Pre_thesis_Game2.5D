using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เงื่อนไข "มีไอเทมเหล่านี้ครบ (หรือครบ N ชิ้น)" — ใช้กับจุดส่งเควส / ประตูที่ต้องมีของหลายชิ้น
/// นับได้ทุกคลัง (ของสำคัญ / เอกสาร / กระเป๋า) ผ่าน ItemOwnership
/// </summary>
[Serializable, PickerName("มีไอเทมครบชุด")]
public class HasItemsCondition : IStoryCondition
{
    public List<ItemData> items = new List<ItemData>();

    [Tooltip("ต้องมีกี่ชิ้น (0 = ครบทุกชิ้นในลิสต์)")]
    [Min(0)] public int required;

    public bool IsMet(StoryContext ctx)
    {
        if (items == null || items.Count == 0) return false;

        int owned = 0, listed = 0;
        foreach (ItemData item in items)
        {
            if (item == null) continue;
            listed++;
            if (ItemOwnership.Has(item)) owned++;
        }

        int target = required > 0 ? required : listed;
        return listed > 0 && owned >= target;
    }
}
