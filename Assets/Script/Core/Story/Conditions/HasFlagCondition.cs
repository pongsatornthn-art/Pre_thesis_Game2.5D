using System;
using UnityEngine;

/// <summary>
/// เงื่อนไขตรวจสอบสถานะของ Story Flag
/// </summary>
[Serializable]
public class HasFlagCondition : IStoryCondition
{
    [Tooltip("ธงความจำที่ต้องการตรวจสอบ")]
    public StoryFlagId flag;

    [Tooltip("สถานะที่คาดหวัง (true = ต้องมีธงนี้ / false = ต้องไม่มีธงนี้)")]
    public bool expectedState = true;

    public bool IsMet(StoryContext ctx)
    {
        if (flag == null) return true;

        IStoryFlags flags = ctx?.Flags ?? ServiceLocator.Get<IStoryFlags>();
        if (flags == null) return false;

        return flags.Has(flag) == expectedState;
    }
}
