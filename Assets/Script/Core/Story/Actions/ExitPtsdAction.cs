using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ออกจากโลก PTSD กลับโลกปกติ — เช่น ส่งเควส → Cutscene → ปวดหัว → [ExitPtsd]
/// ออกแล้วระบบเซฟอัตโนมัติจะเซฟให้เอง (AutoSaveController)
/// </summary>
[Serializable, PickerName("โลก PTSD/ออกจากโลก PTSD")]
public class ExitPtsdAction : IStoryAction
{
    [Tooltip("รอให้อนิเมชันสลับโลกจบก่อนทำคำสั่งถัดไป (วินาทีจริง)")]
    [Min(0f)] public float waitSeconds = 1.5f;

    public IEnumerator Execute(StoryContext ctx)
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        if (world == null)
        {
            Debug.LogWarning("[ExitPtsdAction] ไม่พบ IWorldModeService — ลืมแปะ PtsdWorldModeAdapter ที่ [STORY] หรือเปล่า");
            yield break;
        }

        world.ExitPtsd();
        if (waitSeconds > 0f) yield return new WaitForSecondsRealtime(waitSeconds);
    }
}
