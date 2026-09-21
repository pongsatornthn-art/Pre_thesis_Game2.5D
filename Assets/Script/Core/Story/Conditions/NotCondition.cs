using System;
using UnityEngine;

/// <summary>
/// เงื่อนไขกลับด้าน NOT (กลับค่าจริงเป็นเท็จ และเท็จเป็นจริง)
/// ออกแบบตามรูปแบบ Decorator Pattern เพื่อใช้ครอบเงื่อนไขตัวอื่น
/// </summary>
[Serializable]
public class NotCondition : IStoryCondition
{
    [Tooltip("เงื่อนไขที่จะถูกกลับค่า")]
    [SerializeReference]
    public IStoryCondition condition;

    public bool IsMet(StoryContext ctx)
    {
        if (condition == null) return true;
        return !condition.IsMet(ctx);
    }
}
