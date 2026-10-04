using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// สะพานเชื่อมระหว่าง "เนื้อเรื่อง" กับ "ของในซีน"
/// แปะที่ประตู / กำแพง / ผี แล้วเลือกธงที่จะฟัง — ธงขึ้นเมื่อไหร่ก็ทำงานทันที
///
/// หัวใจของวิธีนี้: ฝั่งเนื้อเรื่อง (ไฟล์ asset) แค่ปักธง ไม่ต้องรู้จักวัตถุชิ้นนี้เลย
/// เลยไม่ติดข้อจำกัดที่ ScriptableObject ชี้ไปหาของในซีนไม่ได้
/// และของในซีนชิ้นไหนจะฟังธงเดียวกันกี่ชิ้นก็ได้ (ประตู 3 บานเปิดพร้อมกันด้วยธงเดียว)
/// </summary>
public class StoryFlagListener : MonoBehaviour
{
    [Header("ธงที่จะฟัง")]
    public StoryFlagId listenFor;

    [Tooltip("ทำงานครั้งเดียวพอ (ปกติเปิดไว้)")]
    public bool triggerOnce = true;

    [Header("เหตุการณ์")]
    [Tooltip("ธงขึ้นตอนกำลังเล่นอยู่ → ให้เล่นอนิเมชันปกติ เช่น ลาก SimpleMover.Play มาใส่")]
    public UnityEvent onFlagSet;

    [Tooltip("โหลดเซฟมาแล้วธงขึ้นอยู่ก่อนแล้ว → ให้วาร์ปไปสถานะปลายทางเลย เช่น SimpleMover.JumpToEnd")]
    public UnityEvent onAlreadySetAtStart;

    private IStoryFlags flags;
    private bool fired;

    private IEnumerator Start()
    {
        // รอ 1 เฟรมก่อนตรวจครั้งแรก เพื่อให้ระบบเซฟมีโอกาสกู้ธงคืนมาก่อน
        // ไม่งั้นพอโหลดเซฟ ประตูที่เคยเปิดไว้จะค่อยๆ เปิดใหม่ให้ผู้เล่นเห็นแทนที่จะเปิดค้างอยู่
        yield return null;

        flags = ServiceLocator.Get<IStoryFlags>();
        if (flags == null)
        {
            Debug.LogWarning($"[StoryFlagListener] ไม่พบ IStoryFlags — '{name}' จะไม่ทำงาน (ลืมใส่ StoryFlagService ในซีนหรือเปล่า)", this);
            yield break;
        }

        if (listenFor != null && flags.Has(listenFor))
        {
            fired = true;
            onAlreadySetAtStart?.Invoke();
        }

        flags.OnChanged += HandleFlagsChanged;
    }

    private void OnDestroy()
    {
        if (flags != null) flags.OnChanged -= HandleFlagsChanged;
    }

    private void HandleFlagsChanged()
    {
        if (listenFor == null) return;
        if (triggerOnce && fired) return;
        if (!flags.Has(listenFor)) return;

        fired = true;
        onFlagSet?.Invoke();
    }
}
