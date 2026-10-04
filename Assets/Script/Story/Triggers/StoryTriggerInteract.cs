using UnityEngine;

/// <summary>
/// ทริกเกอร์สำหรับเริ่มฉากเนื้อเรื่องเมื่อผู้เล่นเดินเข้ามาใกล้แล้วกดปุ่มโต้ตอบ (เช่น กดสำรวจประตู/ศพ/ป้าย/วิทยุ)
///
/// [2026-10-04] สืบทอดจาก StoryInteractableBase แล้ว — ได้ฟรี: ปุ่มกลาง (F) · ไม่รับปุ่มตอนหยุดเกม/ตอนฉากเล่น · ป้าย "กด F"
/// ค่าที่ตั้งไว้ใน Inspector เดิมไม่หาย (ชื่อช่องเหมือนเดิม)
/// </summary>
public class StoryTriggerInteract : StoryInteractableBase
{
    [Header("ฉากเนื้อเรื่อง")]
    [Tooltip("ลำดับเหตุการณ์ที่จะเล่นเมื่อกดปุ่มโต้ตอบ")]
    [SerializeField] private StorySequence sequence;

    [Header("เงื่อนไขธง (Story Flags)")]
    [Tooltip("ต้องมีธงนี้ก่อนจึงจะสามารถกดโต้ตอบได้ (เว้นว่าง = ไม่ตรวจเช็ค)")]
    [SerializeField] private StoryFlagId requiredFlag;

    [Tooltip("ถ้ามีธงนี้แล้ว 'ห้ามทำงาน' (เช่น เคยสำรวจไปแล้ว) — ใช้ตัวนี้แทน Only Once ถ้าอยากให้จำข้ามการโหลดเซฟ")]
    [SerializeField] private StoryFlagId blockedByFlag;

    [Header("เงื่อนไขขั้นสูง (Advanced Conditions)")]
    [Tooltip("เงื่อนไขขั้นสูงเพิ่มเติม (เช่น HasItem, QuestActive, And, Not) — เว้นว่างได้")]
    [SerializeReference, SubclassPicker] private IStoryCondition customCondition;

    [Header("การทำงาน")]
    [Tooltip("เล่นเพียงครั้งเดียวในรอบการเล่นนี้ (โหลดเซฟแล้วจะลืม — อยากให้จำ ใช้ Blocked By Flag)")]
    [SerializeField] private bool onlyOnce = true;

    private bool hasTriggered;

    protected override bool IsInteractable()
    {
        if (sequence == null) return false;
        if (onlyOnce && hasTriggered) return false;

        if (!ServiceLocator.TryGet(out IStoryFlags flags)) return requiredFlag == null;
        if (requiredFlag != null && !flags.Has(requiredFlag)) return false;
        if (blockedByFlag != null && flags.Has(blockedByFlag)) return false;

        return customCondition == null || customCondition.IsMet(StoryContext.Create(this, NearPlayer));
    }

    protected override void OnInteract(StoryContext ctx)
    {
        hasTriggered = true;
        PlaySequence(sequence);
    }
}
