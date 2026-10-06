using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable, PickerName("ให้ไอเทมผู้เล่น (Give Item)")]
public class GiveItemAction : IStoryAction
{
    [Tooltip("ไอเทมที่ต้องการให้ (ใส่ได้หลายชิ้น เช่น กุญแจ, รูปวาด)")]
    public List<ItemData> items = new List<ItemData>();

    [Tooltip("จำนวนที่ให้ต่อ 1 ชิ้น")]
    public int amount = 1;

    public IEnumerator Execute(StoryContext ctx)
    {
        foreach (var item in items)
        {
            if (item != null)
            {
                item.Collect(amount);
                Debug.Log($"[GiveItemAction] ได้รับ {item.itemName}");
            }
        }

        yield break;
    }
}