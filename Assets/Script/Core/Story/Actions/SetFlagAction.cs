using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับตั้งสถานะธงเนื้อเรื่อง 1 อัน
/// ทำงานเสร็จสิ้นทันทีในเฟรมเดียว
/// </summary>
[Serializable]
public class SetFlagAction : IStoryAction
{
    [Tooltip("ธงความจำที่ต้องการตั้งสถานะ")]
    public StoryFlagId flag;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (flag != null && ctx != null && ctx.Flags != null)
        {
            ctx.Flags.Set(flag);
        }
        yield break;
    }
}
