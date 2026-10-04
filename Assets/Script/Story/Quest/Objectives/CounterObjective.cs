using System;
using UnityEngine;

/// <summary>
/// เป้าหมายแบบ "เก็บ/ทำให้ครบ N" — สมุดโชว์ (2/4)
/// ครอบคลุมการ์ด Figma: PTSD Objective 1/N … (ชิ้นส่วนรูปภาพกระจายทั่วบ้าน)
/// ฝั่งซีนเพิ่มตัวนับด้วย AddCounterAction (หรือ CounterIncrementer ในช่วง 3)
/// </summary>
[Serializable, PickerName("เก็บ/ทำให้ครบ N (ตัวนับ)")]
public class CounterObjective : QuestObjective
{
    [Tooltip("ตัวนับที่ใช้วัด")]
    public StoryCounterId counter;

    [Tooltip("ต้องถึงเท่าไหร่ถึงสำเร็จ")]
    [Min(1)] public int target = 1;

    public override bool IsMet(StoryContext ctx)
    {
        if (counter == null) return false;
        return GetValue(ctx) >= target;
    }

    public override bool TryGetProgress(StoryContext ctx, out int current, out int target)
    {
        target = Mathf.Max(1, this.target);
        current = counter == null ? 0 : Mathf.Min(GetValue(ctx), target);
        return true;
    }

    private int GetValue(StoryContext ctx)
    {
        IStoryCounters counters = ctx?.Counters ?? ServiceLocator.GetOptional<IStoryCounters>();
        return counters != null ? counters.Get(counter) : 0;
    }
}
