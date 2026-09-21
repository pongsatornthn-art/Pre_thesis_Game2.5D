using UnityEngine;

/// <summary>
/// พื้นที่ตรวจจับ (Trigger Zone) สำหรับเริ่มเล่นฉากเนื้อเรื่องเมื่อผู้เล่นเดินผ่าน
/// รองรับการตรวจสอบเงื่อนไขธงเนื้อเรื่อง และป้องกันการเล่นซ้ำ
/// 
/// ข้อกำหนด: ต้องมี Collider ที่เปิดใช้งาน 'Is Trigger' ไว้เสมอ
/// </summary>
[RequireComponent(typeof(Collider))]
public class StoryTriggerZone : MonoBehaviour
{
    [Header("ฉากเนื้อเรื่อง")]
    [Tooltip("ลำดับเหตุการณ์ที่จะเล่นเมื่อผู้เล่นเหยียบทริกเกอร์")]
    [SerializeField] private StorySequence sequence;

    [Header("เงื่อนไขธง (Story Flags)")]
    [Tooltip("ต้องมีธงนี้ก่อนจึงจะทำงาน (เว้นว่าง = ไม่ตรวจเช็ค)")]
    [SerializeField] private StoryFlagId requiredFlag;

    [Tooltip("ถ้ามีธงนี้แล้ว 'ห้ามทำงาน' (ใช้สำหรับป้องกันไม่ให้ฉากเดิมเล่นซ้ำ)")]
    [SerializeField] private StoryFlagId blockedByFlag;

    [Header("เงื่อนไขขั้นสูง (Advanced Conditions)")]
    [Tooltip("เงื่อนไขขั้นสูงเพิ่มเติม (เช่น HasItem, QuestActive, And, Not) — เว้นว่างได้")]
    [SerializeReference] private IStoryCondition customCondition;

    [Header("การทำงาน")]
    [Tooltip("เล่นเพียงครั้งเดียวในรอบการเล่นนี้หรือไม่")]
    [SerializeField] private bool onlyOnce = true;

    private bool hasTriggered = false;

    private void Reset()
    {
        // ช่วยตั้งค่า isTrigger อัตโนมัติเมื่อแปะคอมโพเนนต์
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // ตรวจสอบเฉพาะ GameObject ที่มีแท็ก Player เท่านั้น
        if (!other.CompareTag("Player")) return;

        if (onlyOnce && hasTriggered) return;
        if (sequence == null) return;

        IStoryFlags flags = ServiceLocator.Get<IStoryFlags>();

        // ตรวจสอบเงื่อนไขว่ามีธงที่จำเป็นครบหรือไม่
        if (requiredFlag != null && (flags == null || !flags.Has(requiredFlag)))
        {
            return;
        }

        // ตรวจสอบว่าติดธงที่บล็อกไม่ให้เล่นหรือไม่ (เช่น เคยเล่นไปแล้ว)
        if (blockedByFlag != null && flags != null && flags.Has(blockedByFlag))
        {
            return;
        }

        // ตรวจสอบเงื่อนไขขั้นสูงเพิ่มเติม (ถ้ามีกำหนดไว้)
        if (customCondition != null)
        {
            StoryContext ctx = new StoryContext
            {
                Player = other.gameObject,
                Flags = flags,
                Quests = ServiceLocator.Get<IQuestService>(),
                Runner = this
            };
            if (!customCondition.IsMet(ctx)) return;
        }

        StoryDirector director = StoryDirector.Instance ?? ServiceLocator.Get<StoryDirector>();
        if (director == null)
        {
            director = FindFirstObjectByType<StoryDirector>();
        }

        if (director != null)
        {
            hasTriggered = true;
            director.Play(sequence);
        }
        else
        {
            Debug.LogError("[StoryTriggerZone] ไม่พบ StoryDirector ในซีน!");
        }
    }
}
