using System;
using System.Collections;
using UnityEngine;

/// <summary>เปลี่ยนเป็นมุมกล้อง Close-up FPS ที่วางไว้ในซีน (StoryCloseUpView) — เช่น หันเข้าหาโต๊ะวาดรูป</summary>
[Serializable, PickerName("กล้อง/เปลี่ยนเป็นมุม Close-up")]
public class EnterCloseUpAction : IStoryAction
{
    [Tooltip("ต้องตรงกับ Scene Id ของ StoryCloseUpView ในซีน เช่น drawing_table")]
    public string viewId = "drawing_table";

    [Tooltip("ค้างมุมนี้ไว้กี่วินาทีก่อนทำคำสั่งถัดไป (วินาทีจริง)")]
    [Min(0f)] public float holdSeconds = 1f;

    public IEnumerator Execute(StoryContext ctx)
    {
        StoryCloseUpView view = StoryCloseUpView.Find(viewId);
        if (view == null)
        {
            Debug.LogWarning($"[EnterCloseUpAction] ไม่พบมุมกล้อง '{viewId}' ในซีน — ลืมวาง StoryCloseUpView หรือ Scene Id ไม่ตรง");
            yield break;
        }

        view.Enter();
        if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);
    }
}

/// <summary>กลับมุมกล้องปกติ (ปิดมุม Close-up ที่เปิดอยู่)</summary>
[Serializable, PickerName("กล้อง/กลับมุมปกติ")]
public class ExitCloseUpAction : IStoryAction
{
    [Tooltip("รอหลังกลับมุมปกติ (วินาทีจริง)")]
    [Min(0f)] public float waitSeconds = 0f;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (StoryCloseUpView.Active != null) StoryCloseUpView.Active.Exit();
        if (waitSeconds > 0f) yield return new WaitForSecondsRealtime(waitSeconds);
    }
}
