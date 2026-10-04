using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// เข้าโลก PTSD — ใช้ในฉาก เช่น เห็นภาพวาด → บทพูด → ปวดหัว → [EnterPtsd]
/// เรียกผ่าน IWorldModeService ไม่แตะ PTSDManager ตรงๆ
/// </summary>
[Serializable, PickerName("โลก PTSD/เข้าโลก PTSD")]
public class EnterPtsdAction : IStoryAction
{
    [Tooltip("แบบ A = เอาชีวิตรอด มีผี · แบบ B = สืบเรื่อง")]
    public WorldMode mode = WorldMode.PtsdNarrative;

    [Tooltip("รอให้อนิเมชันสลับโลกจบก่อนทำคำสั่งถัดไป (วินาทีจริง ไม่สนการหยุดเกม)")]
    [Min(0f)] public float waitSeconds = 1.5f;

    public IEnumerator Execute(StoryContext ctx)
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        if (world == null)
        {
            Debug.LogWarning("[EnterPtsdAction] ไม่พบ IWorldModeService — ลืมแปะ PtsdWorldModeAdapter ที่ [STORY] หรือเปล่า");
            yield break;
        }

        world.EnterPtsd(mode);
        if (waitSeconds > 0f) yield return new WaitForSecondsRealtime(waitSeconds);
    }
}
