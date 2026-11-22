using UnityEngine;

/// <summary>
/// [แก้ 2026-11-20] ซ่อนแถบ Hotbar ตอนไม่ได้ใช้ — กดเลข 1–4 / หมุนลูกกลิ้งเปลี่ยนช่อง แถบถึงจะโผล่ แล้วค่อยๆ จางหายเอง
///
/// วิธีใช้: แปะที่ HotbarPanel (ตัวเดียวกับ HotbarController + HotbarDocking) — ไม่ต้องลากอะไรใส่
/// - เปิดสมุด (I/J/K/M/N) = แถบโชว์ค้างตลอด (ต้องลากของระหว่างกระเป๋ากับแถบได้)
/// - เก็บของ/ทิ้งของ = โชว์แป๊บนึง (เลือกปิดได้)
/// ไม่ยุ่งกับตำแหน่ง/ขนาด (HotbarDocking ยังทำงานเหมือนเดิม) ใช้แค่ CanvasGroup จางเข้า-ออก
/// </summary>
[DisallowMultipleComponent]
public class HotbarAutoHide : MonoBehaviour
{
    [Tooltip("เว้นว่าง = หาในตัวเอง")]
    public HotbarController hotbar;

    [Header("เวลา")]
    [Tooltip("โชว์ค้างกี่วินาทีหลังกดเปลี่ยนช่อง ก่อนเริ่มจาง")]
    [Min(0f)] public float showSeconds = 2.5f;
    [Tooltip("ความเร็วจางเข้า (ต่อวินาที)")]
    [Min(0.1f)] public float fadeInSpeed = 10f;
    [Tooltip("ความเร็วจางออก (ต่อวินาที)")]
    [Min(0.1f)] public float fadeOutSpeed = 3f;
    [Tooltip("ช่วงแรกหลังเข้าเกม ไม่โชว์ (ตอนเริ่มเกม/โหลดเซฟ ระบบเลือกช่องเองอัตโนมัติ)")]
    [Min(0f)] public float ignoreFirstSeconds = 1f;

    [Header("เงื่อนไขโชว์เพิ่ม")]
    [Tooltip("เก็บของ/ทิ้งของ/ใช้ของหมด = โชว์แป๊บนึงให้เห็นว่าช่องเปลี่ยน")]
    public bool showOnInventoryChange = true;
    [Tooltip("ถือของอยู่ในมือ = โชว์ค้างตลอด (ปิด = ซ่อนเสมอจนกว่าจะกดเลข)")]
    public bool stayVisibleWhileHoldingItem = false;

    [Header("ตอนซ่อน")]
    [Range(0f, 1f)] public float hiddenAlpha = 0f;

    private CanvasGroup group;
    private float visibleUntil = -1f;
    private float startTime;
    private bool subscribedInventory;

    private void Awake()
    {
        if (hotbar == null) hotbar = GetComponent<HotbarController>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = hiddenAlpha;
    }

    private void OnEnable()
    {
        startTime = Time.unscaledTime;
        if (hotbar != null) hotbar.OnHotbarSlotSelected += HandleSelected;
        TrySubscribeInventory();
    }

    private void OnDisable()
    {
        if (hotbar != null) hotbar.OnHotbarSlotSelected -= HandleSelected;
        if (subscribedInventory && Inventory.Instance != null) Inventory.Instance.OnInventoryChanged -= HandleInventoryChanged;
        subscribedInventory = false;
    }

    private void TrySubscribeInventory()
    {
        if (subscribedInventory || Inventory.Instance == null) return;
        Inventory.Instance.OnInventoryChanged += HandleInventoryChanged;
        subscribedInventory = true;
    }

    private void HandleSelected(int index) => Show();

    private void HandleInventoryChanged()
    {
        if (showOnInventoryChange) Show();
    }

    /// <summary>โชว์แถบชั่วคราว (เรียกจากที่อื่นได้ เช่น UnityEvent)</summary>
    public void Show()
    {
        if (Time.unscaledTime - startTime < ignoreFirstSeconds) return;
        visibleUntil = Time.unscaledTime + showSeconds;
    }

    private void Update()
    {
        TrySubscribeInventory(); // Inventory อาจเกิดทีหลังตัวนี้

        // สมุดเปิด (เกมหยุดเวลา) = ต้องเห็นแถบเพื่อลากของ → ใช้เวลาจริง ไม่ใช่ Time.deltaTime
        bool inBook = JournalController.IsAnyOpen;
        bool holding = stayVisibleWhileHoldingItem && Inventory.Instance != null && Inventory.Instance.currentEquippedItem != null;
        bool show = inBook || holding || Time.unscaledTime < visibleUntil;

        float target = show ? 1f : hiddenAlpha;
        float speed = show ? fadeInSpeed : fadeOutSpeed;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * speed);

        // ซ่อนอยู่ = ไม่รับคลิก (กันคลิกโดนช่องที่มองไม่เห็น) · ในสมุด = ลากของได้ปกติ
        bool interactive = inBook || group.alpha > 0.5f;
        if (group.blocksRaycasts != interactive) group.blocksRaycasts = interactive;
    }
}
