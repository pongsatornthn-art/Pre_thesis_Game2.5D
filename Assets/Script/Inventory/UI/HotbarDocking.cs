using UnityEngine;

/// <summary>
/// ย้ายแถบ hotbar ไป-กลับระหว่าง 2 ตำแหน่ง ตามการเปิด/ปิดสมุด
///
/// - ปิดสมุด → ไปอยู่ตำแหน่ง HUD (ล่างจอตามปกติ)
/// - เปิดสมุด → เลื่อนเข้าไปอยู่ในหน้าสมุด ให้ดูเป็นของชิ้นเดียวกับกระเป๋า
///
/// ทำไมต้อง "ย้าย" แทนที่จะ "ซ่อน":
/// ถ้าซ่อนตอนเปิดสมุด ผู้เล่นจะลากของระหว่าง hotbar กับกระเป๋าไม่ได้เลย
/// ถ้าปล่อยไว้ที่เดิมก็ลอยอยู่นอกสมุด ดูเป็นคนละระบบ
/// การย้ายตัวเดิมไปเลยแก้ได้ทั้งสองอย่าง และ**ไม่ต้องแตะโค้ดลากของ**
/// เพราะยังเป็นช่องเดิม ตัวเดิม index เดิมทุกอย่าง
/// </summary>
public class HotbarDocking : MonoBehaviour
{
    [Header("ก้อนที่จะถูกย้าย")]
    [Tooltip("แผง hotbar ทั้งแถบ (เว้นว่าง = ใช้ก้อนที่แปะสคริปต์นี้)")]
    [SerializeField] private RectTransform hotbarPanel;

    [Header("ตำแหน่งปลายทาง (ใช้ empty RectTransform วางไว้เป็นหมุด)")]
    [Tooltip("ตำแหน่งตอนปิดสมุด — ล่างจอตามปกติ")]
    [SerializeField] private RectTransform hudAnchor;

    [Tooltip("ตำแหน่งตอนเปิดสมุด — วางหมุดไว้ในหน้าสมุดตรงที่อยากให้ไปอยู่")]
    [SerializeField] private RectTransform bookAnchor;

    [Header("ขนาดในแต่ละที่ (ปรับได้ถ้าในสมุดต้องเล็ก/ใหญ่กว่า)")]
    [SerializeField] private float hudScale = 1f;
    [SerializeField] private float bookScale = 1f;

    [Header("ความลื่น")]
    [Tooltip("ยิ่งเยอะยิ่งเลื่อนไว — 0 = วาร์ปทันทีไม่มีอนิเมชัน")]
    [SerializeField] private float moveSpeed = 10f;

    [Tooltip("ดันแถบขึ้นมาวาดหน้าสุดตอนอยู่ในสมุด (กันโดนหน้าสมุดทับ)")]
    [SerializeField] private bool bringToFrontInBook = true;

    [Header("ซ่อนตอนอยู่ในสมุด")]
    [Tooltip("ก้อนที่จะถูกปิดตอนเปิดสมุด แล้วเปิดคืนตอนปิดสมุด\n" +
             "ปกติใส่ SelectionCursor (กรอบไฮไลท์ของ hotbar)\n" +
             "เพราะในสมุดมีไฮไลท์ช่องกระเป๋าอยู่แล้ว ถ้ามี 2 กรอบพร้อมกันผู้เล่นจะงงว่าอันไหนคือของที่เลือกอยู่")]
    [SerializeField] private GameObject[] hideWhileInBook;

    private bool isInBook;
    private int originalSiblingIndex;

    private void Awake()
    {
        if (hotbarPanel == null) hotbarPanel = GetComponent<RectTransform>();
        if (hotbarPanel != null) originalSiblingIndex = hotbarPanel.GetSiblingIndex();
    }

    private void OnEnable()
    {
        JournalController.OnJournalToggled += OnJournalToggled;
    }

    private void OnDisable()
    {
        JournalController.OnJournalToggled -= OnJournalToggled;
    }

    private void Start()
    {
        // เริ่มเกมให้ไปอยู่ตำแหน่ง HUD ทันที ไม่ต้องเลื่อนให้เห็น
        SnapTo(false);
        ApplyHiddenParts(false);
    }

    /// <summary>ปิด/เปิดก้อนที่ไม่อยากให้โผล่ตอนอยู่ในสมุด</summary>
    private void ApplyHiddenParts(bool inBook)
    {
        if (hideWhileInBook == null) return;

        foreach (GameObject go in hideWhileInBook)
        {
            if (go != null) go.SetActive(!inBook);
        }
    }

    private void OnJournalToggled(bool isOpen)
    {
        isInBook = isOpen;
        ApplyHiddenParts(isOpen);

        if (!bringToFrontInBook || hotbarPanel == null) return;

        // อยู่ในสมุด = ต้องวาดทับหน้าสมุด ไม่งั้นจะมุดหายไปข้างหลัง
        // (Unity UI วาดของที่อยู่ล่างในลิสต์ทับของที่อยู่บน)
        if (isOpen) hotbarPanel.SetAsLastSibling();
        else hotbarPanel.SetSiblingIndex(originalSiblingIndex);
    }

    private void Update()
    {
        if (hotbarPanel == null) return;

        RectTransform target = isInBook ? bookAnchor : hudAnchor;
        if (target == null) return;

        float targetScale = isInBook ? bookScale : hudScale;

        if (moveSpeed <= 0f)
        {
            SnapTo(isInBook);
            return;
        }

        // ใช้ unscaledDeltaTime เพราะตอนเปิดสมุดเกมถูกหยุดเวลา (Time.timeScale = 0)
        // ถ้าใช้ deltaTime ปกติ แถบจะค้างไม่ขยับเลย
        float t = Time.unscaledDeltaTime * moveSpeed;

        hotbarPanel.position = Vector3.Lerp(hotbarPanel.position, target.position, t);
        hotbarPanel.localScale = Vector3.Lerp(hotbarPanel.localScale, Vector3.one * targetScale, t);
    }

    /// <summary>ย้ายไปตำแหน่งนั้นทันทีแบบไม่มีอนิเมชัน</summary>
    private void SnapTo(bool toBook)
    {
        RectTransform target = toBook ? bookAnchor : hudAnchor;
        if (hotbarPanel == null || target == null) return;

        hotbarPanel.position = target.position;
        hotbarPanel.localScale = Vector3.one * (toBook ? bookScale : hudScale);
    }
}
