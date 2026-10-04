using UnityEngine;

/// <summary>
/// ตัวปักธงจากฝั่งซีน — มีไว้ให้ลากไปผูกกับช่อง UnityEvent ของอะไรก็ได้
/// เช่น SimpleMover.onArrived → StoryFlagSetter.Set  (ประตูเปิดเสร็จ → ปักธงบอกเนื้อเรื่อง)
/// หรือผูกกับปุ่ม / Animation Event / ทริกเกอร์ของเพื่อน โดยไม่ต้องเขียนโค้ดใหม่
///
/// คู่กับ WaitForFlagAction ฝั่งเนื้อเรื่อง: ปักธงนี้เมื่อไหร่ คัตซีนที่รออยู่ก็เดินต่อ
/// </summary>
public class StoryFlagSetter : MonoBehaviour
{
    [Tooltip("ธงที่จะปักเมื่อถูกเรียก Set()")]
    public StoryFlagId flag;

    /// <summary>ปักธง — ลากเมธอดนี้ไปใส่ช่อง UnityEvent ได้เลย</summary>
    public void Set()
    {
        if (flag == null) return;
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags == null)
        {
            Debug.LogWarning($"[StoryFlagSetter] ไม่พบ IStoryFlags — ปักธง '{flag.flagId}' ไม่สำเร็จ", this);
            return;
        }
        flags.Set(flag);
    }

    /// <summary>ลบธง (ใช้น้อยมาก — ระวังทำให้เควสที่จบไปแล้วย้อนกลับ)</summary>
    public void Clear()
    {
        if (flag == null) return;
        ServiceLocator.GetOptional<IStoryFlags>()?.Clear(flag);
    }
}
