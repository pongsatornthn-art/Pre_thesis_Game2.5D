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

    [Tooltip("คีย์ข้อความการกระทำใน LocalizationData.csv เช่น PROMPT_OPEN (เปิด/ปิด) · PROMPT_USE (ใช้)")]
    [SerializeField] private string labelKey = "PROMPT_USE";

    [Tooltip("จุดอ้างอิงตำแหน่งป้าย (เว้นว่าง = object นี้)")]
    [SerializeField] private Transform anchor;

    [Tooltip("ป้ายลอยสูงจากจุดอ้างอิงกี่เมตร")]
    [SerializeField] private float height = 2f;

    [Tooltip("ซ่อนป้ายเมื่อสคริปต์นี้ถูกปิด (เว้นว่างได้) เช่น ลากสคริปต์ตู้เซฟใส่ — เล่นจบแล้วปิดสคริปต์ = ป้ายไม่ขึ้นอีก")]
    [SerializeField] private Behaviour onlyWhileEnabled;

    private bool playerNear;

    private void Update()
    {
        IInteractPromptService prompt = ServiceLocator.GetOptional<IInteractPromptService>();
        if (prompt == null) return;

        bool show = playerNear && (onlyWhileEnabled == null || onlyWhileEnabled.isActiveAndEnabled);
        if (show) prompt.Show(this, anchor != null ? anchor : transform, height, key, labelKey);
        else prompt.Hide(this);
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
