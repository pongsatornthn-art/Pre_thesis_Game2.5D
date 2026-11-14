using UnityEngine;

/// <summary>
/// แปะที่ของที่ "เดินเข้าไปแล้วกดปุ่ม" ซึ่งมี trigger ตรวจผู้เล่นอยู่แล้ว (ประตู / กล่องเก็บของ / ตู้เซฟ / โต๊ะวิทยุ)
/// → ผู้เล่นเข้า trigger = ขึ้นป้ายลอยเหนือของ (WorldInteractPrompt) · ออก = ป้ายหาย
/// ใช้ trigger ตัวเดียวกับสคริปต์ของชิ้นนั้น ป้ายจึงขึ้นตรงกับจังหวะที่กดได้จริง โดยไม่ต้องแก้สคริปต์เดิม
/// (ของเนื้อเรื่องที่ใช้ StoryInteractableBase ขึ้นป้ายเองแล้ว ไม่ต้องแปะ)
/// </summary>
[DisallowMultipleComponent]
public class InteractPromptMarker : MonoBehaviour
{
    [Tooltip("Pickup = ปุ่ม E (ของทั่วไป) · Story = ปุ่ม F (เนื้อเรื่อง)")]
    [SerializeField] private PromptKey key = PromptKey.Pickup;

    [Tooltip("คีย์ข้อความการกระทำใน LocalizationData.csv เช่น PROMPT_OPEN (เปิด/ปิด) · PROMPT_USE (ตรวจดู)")]
    [SerializeField] private string labelKey = "PROMPT_USE";

    [Tooltip("จุดอ้างอิงตำแหน่งป้าย (เว้นว่าง = กึ่งกลางระยะกด (trigger) ของ object นี้ — เช่น กลางบานประตู ไม่ใช่ฝั่งบานพับ)")]
    [SerializeField] private Transform anchor;

    [Tooltip("ป้ายลอยสูงจากพื้น/จุดอ้างอิงกี่เมตร")]
    [SerializeField] private float height = 2f;

    [Tooltip("ซ่อนป้ายเมื่อสคริปต์นี้ถูกปิด (เว้นว่างได้) เช่น ลากสคริปต์ตู้เซฟใส่ — เล่นจบแล้วปิดสคริปต์ = ป้ายไม่ขึ้นอีก")]
    [SerializeField] private Behaviour onlyWhileEnabled;

    private bool playerNear;
    private Collider triggerCol;

    public PromptKey Key => key;

    /// <summary>
    /// ป้ายกำลังโชว์ของชิ้นอื่นที่ใช้ปุ่มเดียวกันอยู่ไหม — สคริปต์ที่รับปุ่มเอง (เช่น ประตู) เช็คก่อนทำงาน
    /// กันยืนใกล้ประตู+ไอเทม กด E ครั้งเดียวแล้วทำทั้ง 2 อย่าง · ไม่มี marker/ไม่มีบริการป้าย = ไม่กัน (ทำงานแบบเดิม)
    /// </summary>
    public static bool OtherPromptShown(GameObject go)
    {
        InteractPromptMarker m = go != null ? go.GetComponent<InteractPromptMarker>() : null;
        if (m == null) return false;
        IInteractPromptService prompt = ServiceLocator.GetOptional<IInteractPromptService>();
        return prompt != null && prompt.IsOtherShown(m, m.key);
    }

    private void Awake()
    {
        foreach (Collider c in GetComponents<Collider>())
            if (c.isTrigger) { triggerCol = c; break; }
    }

    private void Update()
    {
        IInteractPromptService prompt = ServiceLocator.GetOptional<IInteractPromptService>();
        if (prompt == null) return;

        bool show = playerNear && (onlyWhileEnabled == null || onlyWhileEnabled.isActiveAndEnabled);
        if (show) prompt.Show(this, anchor != null ? anchor : transform, ComputeOffset(), key, labelKey);
        else prompt.Hide(this);
    }

    private Vector3 ComputeOffset()
    {
        if (anchor != null || triggerCol == null) return Vector3.up * height;
        Vector3 c = triggerCol.bounds.center - transform.position;
        return new Vector3(c.x, height, c.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerNear = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerNear = false;
    }

    private void OnDisable()
    {
        playerNear = false;
        ServiceLocator.GetOptional<IInteractPromptService>()?.Hide(this);
    }
}
