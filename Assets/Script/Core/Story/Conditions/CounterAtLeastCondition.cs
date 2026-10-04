using System;
using UnityEngine;

/// <summary>
/// เงื่อนไข "ตัวนับถึงเท่านี้แล้ว" — เช่น ข้อ "ส่งเควส" ทำได้เมื่อเก็บชิ้นส่วนครบ 4
/// </summary>
[Serializable, PickerName("ตัวนับถึงจำนวน")]
public class CounterAtLeastCondition : IStoryCondition
{
    [Tooltip("ตัวนับที่ต้องการตรวจ")]
    public StoryCounterId counter;

    [Tooltip("ต้องมีอย่างน้อยเท่าไหร่")]
    [Min(0)] public int atLeast = 1;

    public bool IsMet(StoryContext ctx)
    {
        if (counter == null) return false;

        IStoryCounters counters = ctx?.Counters ?? ServiceLocator.Get<IStoryCounters>();
        return counters != null && counters.Get(counter) >= atLeast;
    }
}
