using UnityEngine;

/// <summary>
/// ของที่เก็บได้ในซีน ที่นับเข้าเควส — เช่น ชิ้นส่วนรูปภาพ 1/4 (การ์ด Figma "PTSD Objective")
///
/// เก็บแล้ว: ปักธงประจำชิ้น + ตัวนับ +N + เล่นฉาก (ถ้ามี) + หายไป
/// **จำผ่านธงในความจำกลาง** — โหลดเซฟ / สลับโลก / ตายเริ่มใหม่ ชิ้นที่เก็บแล้วจะไม่โผล่ซ้ำ
///   ซิงก์ทุกครั้งที่ถูกเปิด (OnEnable) จึงทำงานถูกแม้อยู่ในโลก PTSD ที่ถูกซ่อนไว้
///
/// [2026-10-07] ช่อง "Collected Flag" ไม่บังคับแล้ว — เว้นว่าง = ชิ้นนี้จำตัวเองด้วยรหัสที่สุ่มให้ (ไม่ต้องสร้างไฟล์ธงทีละชิ้น)
/// ใส่ธงเฉพาะตอนที่อยากให้อย่างอื่นรอธงนี้ (เช่น เป้าหมายเควส / ประตู)
/// ⚠️ Duplicate ชิ้นที่มีรหัสแล้ว → ระบบสุ่มรหัสใหม่ให้เองใน Editor
/// </summary>
public class StoryCollectible : StoryInteractableBase, IPresenceGate
{
    [Header("เก็บแล้วเกิดอะไร")]
    [Tooltip("ไม่บังคับ — เว้นว่างได้ ชิ้นนี้จำตัวเองอยู่แล้ว · ใส่เมื่ออยากให้เควส/ประตูรอธงนี้ (ห้ามใช้ธงซ้ำกับชิ้นอื่น)")]
    [SerializeField] private StoryFlagId collectedFlag;

    [SerializeField, HideInInspector] private string autoKey;   // รหัสจำตัวเอง สุ่มให้ใน Editor

    private string CollectedKey => collectedFlag != null ? collectedFlag.Id : (string.IsNullOrEmpty(autoKey) ? null : "collected:" + autoKey);

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
        Subscribe();
        SyncWithWorldState();
    }

    // ซีนเพิ่งเปิด: ชิ้นนี้อาจ OnEnable ก่อน StoryFlagService ลงทะเบียน → ลองต่ออีกรอบตอน Start
    private void Start()
    {
        Subscribe();
        SyncWithWorldState();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (flags != null) flags.OnChanged -= SyncWithWorldState;
        flags = null;
    }

    private void Subscribe()
    {
        if (flags != null) return;
        flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null) flags.OnChanged += SyncWithWorldState;
    }

    private void OnTriggerStay(Collider other)
    {
        // [แก้ 2026-10-07] เดิมเช็ค IsInteractable() ซึ่งตอบ false เสมอเมื่อติ๊ก Collect On Touch (กันป้ายกด F ขึ้น)
        // → เดินชนแล้วไม่เก็บเลย · ทางเดินชนต้องเช็คแค่ "เก็บไปแล้วหรือยัง"
        if (collectOnTouch && other.CompareTag("Player") && !IsCollected())
        {
            OnInteract(StoryContext.Create(this, other.gameObject));
        }
    }

    protected override bool IsInteractable() => !collectOnTouch && !IsCollected();

    protected override void OnInteract(StoryContext ctx)
    {
        if (IsCollected()) return;
        if (CollectedKey == null)
        {
            Debug.LogError($"[StoryCollectible] '{name}' ไม่มีรหัสจำตัวเอง — คลิกขวาที่คอมโพเนนต์ → 'สุ่มรหัสใหม่' แล้วเซฟซีน", this);
            return;
        }

        // ตัวนับก่อน แล้วค่อยปักธง — QuestService คำนวณใหม่ทุกครั้งที่อย่างใดอย่างหนึ่งเปลี่ยน
        if (counter != null) ctx.Counters?.Add(counter, amount);
        ctx.Flags?.SetKey(CollectedKey);   // → SyncWithWorldState ซ่อนตัวเอง

        PlaySequence(onCollected);
    }

    private bool IsCollected() => flags != null && flags.HasKey(CollectedKey);

#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();
        RegenerateKey();
    }

    // สุ่มรหัสให้ชิ้นที่ยังไม่มี + แก้รหัสซ้ำจากการ Duplicate (Ctrl+D ก๊อปรหัสมาด้วย)
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (string.IsNullOrEmpty(autoKey)) { RegenerateKey(); return; }

        foreach (StoryCollectible other in FindObjectsByType<StoryCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other != this && other.autoKey == autoKey && other.gameObject.scene == gameObject.scene)
            {
                RegenerateKey();
                return;
            }
        }
    }

    [ContextMenu("สุ่มรหัสใหม่")]
    private void RegenerateKey()
    {
        autoKey = System.Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    /// <summary>เหตุผลการโผล่ข้อนี้: เก็บไปแล้ว = ไม่โผล่ (รวมกับ WorldPresence ฯลฯ ผ่าน ScenePresence)</summary>
    public bool AllowVisible => !IsCollected();

    // ซิงก์ได้ทั้ง 2 ทาง: เก็บแล้ว → ซ่อน · ธงถูกล้าง (คืนจุดเซฟ/เริ่ม PTSD ใหม่) → โผล่กลับ
    // ใช้ ScenePresence รวมผลกับเหตุผลอื่นบน object เดียวกัน (เช่น WorldPresence) — ไม่เปิด/ปิดเองตรงๆ แล้ว กันแย่งกัน
    private void SyncWithWorldState()
    {
        ScenePresence.Refresh(gameObject);
        if (visualRoot != null) visualRoot.SetActive(ScenePresence.IsAllowed(gameObject));
    }
}
