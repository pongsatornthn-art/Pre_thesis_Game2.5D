using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] คำสั่งในฉาก: เด้งการ์ดคำสอน (ครั้งเดียว — เคยขึ้นแล้วข้ามไปคำสั่งถัดไปทันที)
/// ใช้กับ StoryTriggerZone (เดินถึง) / ต่อท้ายฉากได้ปืน ฯลฯ — สเปก docs/TUTORIAL_SPEC.md
/// </summary>
[Serializable, PickerName("สอนเล่น/แสดงคำสอน")]
public class ShowTutorialAction : IStoryAction
{
    public TutorialData tutorial;

    [Tooltip("รอผู้เล่นปิดการ์ดก่อนค่อยทำคำสั่งถัดไป")]
    public bool waitUntilClosed = true;

    [Tooltip("รอนานสุดกี่วินาที (เวลาจริง) ถ้าการ์ดยังไม่ขึ้นเพราะไม่ว่าง — กันฉากค้าง")]
    [Min(1f)] public float maxWaitSeconds = 30f;

    public IEnumerator Execute(StoryContext ctx)
    {
        if (tutorial == null)
        {
            Debug.LogWarning("[ShowTutorialAction] ยังไม่ได้ใส่ไฟล์คำสอน (Tutorial)");
            yield break;
        }

        ITutorialService service = ServiceLocator.GetOptional<ITutorialService>();
        if (service == null)
        {
            Debug.LogWarning("[ShowTutorialAction] ไม่มี TutorialService ในซีน (prefab [TUTORIAL]) — ข้าม");
            yield break;
        }
        if (service.HasSeen(tutorial)) yield break;

        service.Request(tutorial);
        if (!waitUntilClosed) yield break;

        // รอจนการ์ดขึ้น (ถูกจำว่าเห็นแล้ว) แล้วรอจนปิด
        float giveUpAt = Time.unscaledTime + maxWaitSeconds;
        while (!service.HasSeen(tutorial))
        {
            if (Time.unscaledTime >= giveUpAt)
            {
                Debug.LogWarning($"[ShowTutorialAction] รอการ์ด '{tutorial.tutorialId}' นานเกิน {maxWaitSeconds} วิ ({TutorialGate.Reason ?? "การ์ดอื่นค้าง"}) — ทำฉากต่อ การ์ดยังอยู่ในคิว");
                yield break;
            }
            yield return null;
        }
        while (service.IsShowing) yield return null;
    }
}
