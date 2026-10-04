using System;
using UnityEngine;

/// <summary>
/// เงื่อนไข "ตอนนี้อยู่โลกไหน" — เช่น ทริกเกอร์ที่ทำงานเฉพาะในโลก PTSD / ป้ายที่อ่านได้เฉพาะโลกปกติ
/// </summary>
[Serializable, PickerName("โลก PTSD/อยู่โลกไหน")]
public class WorldModeCondition : IStoryCondition
{
    public enum Check { InRealWorld, InAnyPtsd, InPtsdSurvival, InPtsdNarrative }

    public Check mustBe = Check.InAnyPtsd;

    public bool IsMet(StoryContext ctx)
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        WorldMode mode = world != null ? world.Mode : WorldMode.Real;   // ไม่มีบริการ = ถือว่าโลกปกติ

        switch (mustBe)
        {
            case Check.InRealWorld: return mode == WorldMode.Real;
            case Check.InAnyPtsd: return mode != WorldMode.Real;
            case Check.InPtsdSurvival: return mode == WorldMode.PtsdSurvival;
            case Check.InPtsdNarrative: return mode == WorldMode.PtsdNarrative;
            default: return false;
        }
    }
}
