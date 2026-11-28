using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] วางในซีน: "พอเงื่อนไขนี้เป็นจริง → เด้งการ์ดคำสอน" (ครั้งเดียว)
/// เลือกเงื่อนไขจากเมนูเดียวกับระบบเนื้อเรื่อง เช่น
///   โลก PTSD/อยู่โลกไหน · มีไอเทม (ได้ปืน/ท่อเหล็ก/ของชิ้นแรก) · มีธง · กล้อง/อยู่มุม Close-up · ห้องปลอดภัย/นั่งโต๊ะอยู่ · ทันที
/// (ระหว่างเปิดสมุด/หยุดเกม/บทพูด การ์ดจะรอให้จบก่อน — ดู TutorialGate)
/// ย้าย/เปลี่ยนจังหวะได้ใน Inspector (เนื้อเรื่องยังไม่นิ่ง ไม่ฝังในโค้ด) — สเปก docs/TUTORIAL_SPEC.md
/// 1 ตัว = 1 คำสอน · วางหลายตัวไว้ใต้ก้อน [TUTORIAL]/Triggers ได้
/// </summary>
public class TutorialOnEvent : MonoBehaviour
{
    [SerializeField] private TutorialData tutorial;

    [Tooltip("เป็นจริงเมื่อไหร่ เด้งการ์ด (เว้นว่าง = ไม่ทำงาน)")]
    [SerializeReference, SubclassPicker] private IStoryCondition when;

    [Tooltip("เช็คเงื่อนไขทุกกี่วินาที")]
    [SerializeField, Min(0.05f)] private float checkInterval = 0.25f;

    private StoryContext ctx;
    private float nextCheck;
    private bool requested;

    private void Start()
    {
        if (tutorial == null) Debug.LogWarning($"[TutorialOnEvent] '{name}' ยังไม่ได้ใส่ไฟล์คำสอน", this);
        if (when == null) Debug.LogWarning($"[TutorialOnEvent] '{name}' ยังไม่ได้เลือกเงื่อนไข (When)", this);
    }

    private void Update()
    {
        if (requested || tutorial == null || when == null) return;
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;

        ITutorialService service = ServiceLocator.GetOptional<ITutorialService>();
        if (service == null || SaveRestoreScope.IsBusy) return;
        if (service.HasSeen(tutorial)) { requested = true; return; }

        if (ctx == null) ctx = StoryContext.Create(this);
        if (!when.IsMet(ctx)) return;

        service.Request(tutorial);   // ยังไม่ว่าง → บริการเข้าคิวรอให้เอง
        requested = true;
    }
}
