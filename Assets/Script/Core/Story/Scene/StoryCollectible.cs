using UnityEngine;

/// <summary>
/// ของที่เก็บได้ในซีน ที่นับเข้าเควส — เช่น ชิ้นส่วนรูปภาพ 1/4 (การ์ด Figma "PTSD Objective")
///
/// เก็บแล้ว: ปักธงประจำชิ้น + ตัวนับ +N + เล่นฉาก (ถ้ามี) + หายไป
/// **จำผ่านธงในความจำกลาง** — โหลดเซฟ / สลับโลก / ตายเริ่มใหม่ ชิ้นที่เก็บแล้วจะไม่โผล่ซ้ำ
///   ซิงก์ทุกครั้งที่ถูกเปิด (OnEnable) จึงทำงานถูกแม้อยู่ในโลก PTSD ที่ถูกซ่อนไว้
///
/// ⚠️ ธง "Collected Flag" ต้องไม่ซ้ำกันทุกชิ้น — 4 ชิ้น = ธง 4 อัน
/// </summary>
public class StoryCollectible : StoryInteractableBase
{
    [Header("เก็บแล้วเกิดอะไร")]
    [Tooltip("ธงประจำชิ้นนี้ (ห้ามซ้ำกับชิ้นอื่น) — ใช้จำว่าเก็บไปแล้ว")]
    [SerializeField] private StoryFlagId collectedFlag;

    [Tooltip("ตัวนับที่จะเพิ่ม เช่น Counter_PicturePieces (เว้นว่างได้)")]
    [SerializeField] private StoryCounterId counter;
    [SerializeField, Min(1)] private int amount = 1;

    [Tooltip("ฉากที่เล่นตอนเก็บ เช่น ขึ้นบทพูด 'เจอชิ้นส่วนรูปภาพ...' (เว้นว่างได้)")]
    [SerializeField] private StorySequence onCollected;

    [Header("การเก็บ")]
    [Tooltip("✔ = เดินชนก็เก็บเลย · ✘ = ต้องกด E")]
    [SerializeField] private bool collectOnTouch;

    [Tooltip("ลูกที่เป็นหน้าตาของชิ้นนี้ — ซ่อนเมื่อเก็บแล้ว (เว้นว่าง = ปิด Renderer ทุกตัวในลูกแทน)\n" +
             "⚠️ ตัว object หลักจะไม่ถูกปิด เพื่อให้ยังฟังความจำกลางอยู่ (ธงถูกล้างตอนเริ่มโลก PTSD ใหม่ → ชิ้นนี้กลับมา)")]
    [SerializeField] private GameObject visualRoot;

    private IStoryFlags flags;

    private void OnEnable()
    {
        flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null) flags.OnChanged += SyncWithWorldState;
        SyncWithWorldState();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (flags != null) flags.OnChanged -= SyncWithWorldState;
    }

    private void OnTriggerStay(Collider other)
    {
        if (collectOnTouch && other.CompareTag("Player") && IsInteractable())
        {
            OnInteract(StoryContext.Create(this, other.gameObject));
        }
    }

    protected override bool IsInteractable() => !collectOnTouch && !IsCollected();

    protected override void OnInteract(StoryContext ctx)
    {
        if (IsCollected()) return;
        if (collectedFlag == null)
        {
            Debug.LogError($"[StoryCollectible] '{name}' ยังไม่ได้ใส่ Collected Flag — เก็บแล้วจะจำไม่ได้ โหลดเซฟจะโผล่ซ้ำ", this);
            return;
        }

        // ตัวนับก่อน แล้วค่อยปักธง — QuestService คำนวณใหม่ทุกครั้งที่อย่างใดอย่างหนึ่งเปลี่ยน
        if (counter != null) ctx.Counters?.Add(counter, amount);
        ctx.Flags?.Set(collectedFlag);   // → SyncWithWorldState ซ่อนตัวเอง

        PlaySequence(onCollected);
    }

    private bool IsCollected() => collectedFlag != null && flags != null && flags.Has(collectedFlag);

    // ซิงก์ได้ทั้ง 2 ทาง: เก็บแล้ว → ซ่อน · ธงถูกล้าง (คืนจุดเซฟ/เริ่ม PTSD ใหม่) → โผล่กลับ
    private void SyncWithWorldState()
    {
        bool visible = !IsCollected();

        if (visualRoot != null)
        {
            if (visualRoot.activeSelf != visible) visualRoot.SetActive(visible);
        }
        else
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        // ปิดตัวชนด้วย — ไม่งั้นป้าย "กด E" ยังขึ้นตรงที่ว่าง
        foreach (Collider c in GetComponents<Collider>()) c.enabled = visible;
    }
}
