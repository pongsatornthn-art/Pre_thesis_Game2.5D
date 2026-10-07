using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// เล่นมินิเกมที่วางไว้ในซีน แล้วรอจนจบ
/// สำเร็จ → ปักธง (ถ้าใส่) แล้วไปคำสั่งถัดไป · ออกกลางคัน → **หยุดทั้งฉาก** (คัทซีน/ออก PTSD ที่ตามมาจะไม่เล่น)
/// ผลสำเร็จจำผ่านธง → ระบบเซฟจำให้ · กลับมากดใหม่ได้ถ้าออกกลางคัน
/// </summary>
[Serializable, PickerName("มินิเกม/เล่นมินิเกม")]
public class PlayMinigameAction : IStoryAction
{
    [Tooltip("ต้องตรงกับ Scene Id ของมินิเกมในซีน เช่น picture_assembly")]
    public string minigameId = "picture_assembly";

    [Tooltip("ธงที่ปักเมื่อทำสำเร็จ (เว้นว่างได้) — ใช้ให้เควส/ประตู/ฉากอื่นรู้ว่าผ่านแล้ว")]
    public StoryFlagId flagOnSuccess;

    [Tooltip("ออกกลางคัน → กลับมุมกล้องปกติให้ด้วย (ปกติเปิดไว้)")]
    public bool exitCloseUpOnAbort = true;

    public IEnumerator Execute(StoryContext ctx)
    {
        MinigameBase game = MinigameBase.Find(minigameId);
        if (game == null)
        {
            Debug.LogWarning($"[PlayMinigameAction] ไม่พบมินิเกม '{minigameId}' ในซีน — หยุดฉากไว้ก่อน (ลืมวาง หรือ Scene Id ไม่ตรง)");
            ctx.StopSequence = true;
            yield break;
        }

        game.Begin();
        while (game != null && game.IsRunning) yield return null;

        if (game != null && game.Result == MinigameResult.Success)
        {
            if (flagOnSuccess != null) (ctx.Flags ?? ServiceLocator.GetOptional<IStoryFlags>())?.Set(flagOnSuccess);
        }
        else
        {
            ctx.StopSequence = true;
            // ฉากหยุดก่อนถึงคำสั่ง "กลับมุมปกติ" → ต้องคืนกล้องเอง ไม่งั้นผู้เล่นค้างในมุม close-up
            if (exitCloseUpOnAbort && StoryCloseUpView.Active != null) StoryCloseUpView.Active.Exit();
        }
    }
}
