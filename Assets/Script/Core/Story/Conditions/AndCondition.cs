using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// รวมเงื่อนไขแบบ AND (ทุกเงื่อนไขย่อยต้องเป็นจริงทั้งหมด)
/// ออกแบบตามรูปแบบ Composite Pattern สามารถซ้อนเงื่อนไขย่อยได้ไม่จำกัด
/// </summary>
[Serializable]
public class AndCondition : IStoryCondition
{
    [Tooltip("รายการเงื่อนไขย่อยที่ต้องเป็นจริงทั้งหมด")]
    [SerializeReference]
    public List<IStoryCondition> conditions = new List<IStoryCondition>();

    public bool IsMet(StoryContext ctx)
    {
        if (conditions == null || conditions.Count == 0) return true;

        for (int i = 0; i < conditions.Count; i++)
        {
            if (conditions[i] != null && !conditions[i].IsMet(ctx))
            {
                return false;
            }
        }

        return true;
    }
}
