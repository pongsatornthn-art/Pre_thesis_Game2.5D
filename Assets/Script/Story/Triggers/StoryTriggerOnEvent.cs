using UnityEngine;

/// <summary>
/// ทริกเกอร์สำหรับเรียกเล่นฉากเนื้อเรื่องผ่าน UnityEvent
/// ออกแบบมาเพื่อต่อเชื่อมกับระบบเดิมของเพื่อนร่วมทีม (เช่น ประตู, หีบสมบัติ, มินิเกม)
/// โดยไม่ต้องแก้ไขโค้ดของเพื่อนแม้แต่บรรทัดเดียวตามหลัก Open-Closed (OCP)
/// </summary>
public class StoryTriggerOnEvent : MonoBehaviour
{
    [Header("ฉากเนื้อเรื่อง")]
    [Tooltip("ลำดับเหตุการณ์ที่จะเล่นเมื่อถูกเรียก")]
    [SerializeField] private StorySequence sequence;

    [Header("เงื่อนไขธง (Story Flags)")]
    [Tooltip("ต้องมีธงนี้ก่อนจึงจะทำงาน (เว้นว่าง = ไม่ตรวจเช็ค)")]
    [SerializeField] private StoryFlagId requiredFlag;

    [Tooltip("ถ้ามีธงนี้แล้ว 'ห้ามทำงาน' (เช่น เคยเล่นไปแล้ว)")]
    [SerializeField] private StoryFlagId blockedByFlag;

    [Header("การทำงาน")]
    [Tooltip("เล่นเพียงครั้งเดียวหรือไม่")]
    [SerializeField] private bool onlyOnce = true;

    private bool hasTriggered = false;

    /// <summary>
    /// เมธอดสำหรับให้ UnityEvent ใน Inspector เรียกใช้งาน
    /// </summary>
    public void Fire()
    {
        if (onlyOnce && hasTriggered) return;
        if (sequence == null) return;

        IStoryFlags flags = ServiceLocator.Get<IStoryFlags>();
        if (requiredFlag != null && (flags == null || !flags.Has(requiredFlag))) return;
        if (blockedByFlag != null && flags != null && flags.Has(blockedByFlag)) return;

        StoryDirector director = StoryDirector.Instance ?? ServiceLocator.Get<StoryDirector>();
        if (director == null) director = FindFirstObjectByType<StoryDirector>();

        if (director != null)
        {
            hasTriggered = true;
            director.Play(sequence);
        }
        else
        {
            Debug.LogError("[StoryTriggerOnEvent] ไม่พบ StoryDirector ในซีน!");
        }
    }
}
