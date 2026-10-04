using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ของในซีน "ฟังธง" — ธงขึ้นแล้วยิง UnityEvent (เช่น ประตูเปิด / สิ่งกีดขวางหาย / ทางลับโผล่)
///
/// ซิงก์กับความจำกลาง **ทุกครั้งที่ถูกเปิด (OnEnable)** ไม่ใช่แค่ตอนเริ่มซีน:
///   ของในโลก PTSD ถูกซ่อนตอนธงขึ้น → พอสลับเข้าโลก PTSD ก็วาร์ปไปสภาพปลายทางเอง
///   โหลดเซฟ → วาร์ปเหมือนกัน → ทางไม่มีวันหาย (QUEST_SAVE_SPEC.md หัวข้อ 5)
///
/// แยก event 2 ช่อง:
///   onFlagSet           = ธงขึ้นตอนผู้เล่นอยู่ตรงนั้น → เล่นอนิเมชัน (เช่น SimpleMover.Play)
///   onAlreadySetAtStart = ธงขึ้นอยู่แล้วตอนของนี้เพิ่งเปิด → วาร์ปไปเลย (เช่น SimpleMover.JumpToEnd)
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

    [Tooltip("ธงขึ้นอยู่ก่อนแล้ว (โหลดเซฟ / ธงขึ้นตอนของนี้ถูกซ่อนในอีกโลก) → วาร์ปไปสถานะปลายทาง เช่น SimpleMover.JumpToEnd")]
    public UnityEvent onAlreadySetAtStart;

    private IStoryFlags flags;
    private bool fired;
    private bool subscribed;

    private void OnEnable()
    {
        StartCoroutine(SyncNextFrame());
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // รอ 1 เฟรม — ให้บริการกลางลงทะเบียนเสร็จ และให้ระบบเซฟใส่ธงคืนก่อน
    private IEnumerator SyncNextFrame()
    {
        yield return null;

        // กำลังโหลดเซฟ → รอให้ใส่ธงคืนครบก่อน ไม่งั้นธงที่ถูกใส่คืนจะถูกมองว่า "เพิ่งขึ้น" แล้วเล่นอนิเมชันแทนการวาร์ป
        while (SaveRestoreScope.IsBusy) yield return null;

        if (flags == null) flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags == null)
        {
            Debug.LogWarning($"[StoryFlagListener] ไม่พบ IStoryFlags — '{name}' จะไม่ทำงาน (ลืมใส่ StoryFlagService ในซีนหรือเปล่า)", this);
            yield break;
        }

        if (!subscribed)
        {
            flags.OnChanged += HandleFlagsChanged;
            subscribed = true;
        }

        if (!fired && listenFor != null && flags.Has(listenFor))
        {
            fired = true;
            onAlreadySetAtStart?.Invoke();
        }
    }

    private void Unsubscribe()
    {
        if (subscribed && flags != null) flags.OnChanged -= HandleFlagsChanged;
        subscribed = false;
    }

    private void HandleFlagsChanged()
    {
        if (listenFor == null || flags == null) return;
        if (SaveRestoreScope.IsBusy) return;   // ธงที่ถูกใส่คืนตอนโหลด ไม่ใช่เหตุการณ์ที่เกิดตอนเล่น
        if (triggerOnce && fired) return;
        if (!flags.Has(listenFor)) return;

        fired = true;
        onFlagSet?.Invoke();
    }
}
