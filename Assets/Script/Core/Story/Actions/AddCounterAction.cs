using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// เพิ่ม/ลดตัวนับในความจำกลาง — เช่น เก็บชิ้นส่วนรูปภาพ 1 ชิ้น = +1
/// ใส่ติดลบได้ (เช่น ส่งของแล้วหักออก) ค่าจะไม่ต่ำกว่า 0
/// </summary>
[Serializable, PickerName("ตัวนับ/เพิ่ม-ลดตัวนับ")]
public class AddCounterAction : IStoryAction
{
    [Tooltip("ตัวนับที่จะเปลี่ยน")]
    public StoryCounterId counter;

    [Tooltip("เพิ่มเท่าไหร่ (ติดลบ = ลด)")]
    public int amount = 1;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (counter != null)
        {
            IStoryCounters counters = ctx?.Counters ?? ServiceLocator.Get<IStoryCounters>();
            if (counters != null) counters.Add(counter, amount);
            else Debug.LogWarning("[AddCounterAction] ไม่พบ IStoryCounters (StoryFlagService) ในซีน");
        }
        yield break;
    }
}
