using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-11-25 นา] ขึ้นจอจบเดโม (DemoEndScreen ในซีน) แล้วหยุดฉากนี้ — คำสั่งหลังจากนี้ไม่ทำ
/// </summary>
[Serializable, PickerName("เกม/จบเดโม")]
public class EndDemoAction : IStoryAction
{
    public IEnumerator Execute(StoryContext ctx)
    {
        DemoEndScreen screen = DemoEndScreen.Instance;
        // จอถูกเซฟไว้แบบปิดในซีน = Awake ยังไม่เคยทำงาน → หาแบบรวมของที่ปิดอยู่
        if (screen == null) screen = UnityEngine.Object.FindAnyObjectByType<DemoEndScreen>(FindObjectsInactive.Include);
        if (screen == null)
        {
            Debug.LogWarning("[EndDemoAction] ไม่พบ DemoEndScreen ในซีน — ลืมวางจอจบเดโมใน Canvas?");
            yield break;
        }

        screen.Show();
        ctx.StopSequence = true;
    }
}
