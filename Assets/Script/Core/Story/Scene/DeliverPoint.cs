using UnityEngine;

/// <summary>
/// จุด "ส่งเควส" ในซีน (การ์ด Figma: PTSD Objective 5/5 ส่งเควส / PTSD Objective 2/2)
///
/// กด E:
///   ครบเงื่อนไข (เช่น ชิ้นส่วน ≥ 4) → หักตัวนับ (ถ้าตั้งไว้) → ปักธง "ส่งแล้ว" → เล่นฉากสำเร็จ
///   ยังไม่ครบ → เล่นฉาก "ยังไม่ครบ" (เช่น บทพูด 'ยังขาดอีกหลายชิ้น...')
///
/// ใช้คู่กับเป้าหมาย FlagObjective ที่รอธง "ส่งแล้ว" ตัวเดียวกัน
/// </summary>
public class DeliverPoint : StoryInteractableBase
{
    [Header("ต้องมีอะไรถึงจะส่งได้")]
    [Tooltip("เช่น 'มีไอเทมครบชุด' (ชิ้นส่วนรูปภาพ 4 ชิ้น) หรือ 'ตัวนับถึงจำนวน' · เว้นว่าง = ส่งได้เลย")]
    [SerializeReference, SubclassPicker] private IStoryCondition requirement;

    [Tooltip("เอาไอเทมเหล่านี้ออกตอนส่ง เช่น ชิ้นส่วนรูปภาพ 4 ชิ้น (ของสำคัญหักเฉพาะชิ้นที่ตั้ง Consume On Use · เอกสารไม่หัก)")]
    [SerializeField] private System.Collections.Generic.List<ItemData> consumeItems = new System.Collections.Generic.List<ItemData>();

    [Tooltip("หักตัวนับนี้ออกตอนส่ง (เว้นว่าง = ไม่หัก)")]
    [SerializeField] private StoryCounterId consumeCounter;
    [SerializeField, Min(0)] private int consumeAmount;

    [Header("ส่งสำเร็จ")]
    [Tooltip("ธงที่ปักเมื่อส่งสำเร็จ — ให้เป้าหมายเควส (FlagObjective) รอธงนี้")]
    [SerializeField] private StoryFlagId deliveredFlag;
    [SerializeField] private StorySequence onDelivered;

    [Header("ยังไม่ครบ")]
    [SerializeField] private StorySequence onNotReady;

    protected override bool IsInteractable()
    {
        // ส่งไปแล้ว = กดไม่ได้อีก (จำผ่านธง ไม่ใช่ตัวแปรในตัวเอง → โหลดเซฟแล้วถูกต้อง)
        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        return deliveredFlag == null || flags == null || !flags.Has(deliveredFlag);
    }

    protected override void OnInteract(StoryContext ctx)
    {
        if (deliveredFlag == null)
        {
            Debug.LogError($"[DeliverPoint] '{name}' ยังไม่ได้ใส่ Delivered Flag — เควสจะรู้ไม่ได้ว่าส่งแล้ว", this);
            return;
        }

        if (requirement != null && !requirement.IsMet(ctx))
        {
            PlaySequence(onNotReady);
            return;
        }

        if (consumeItems != null) foreach (ItemData item in consumeItems) ItemOwnership.Remove(item);
        if (consumeCounter != null && consumeAmount > 0) ctx.Counters?.Add(consumeCounter, -consumeAmount);
        ctx.Flags?.Set(deliveredFlag);
        PlaySequence(onDelivered);
    }
}
