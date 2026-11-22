using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-22] แถบดำบน-ล่างแบบหนัง (Letterbox) ตอนเข้าโลก PTSD — เลื่อนเข้าเมื่อเข้า PTSD · เลื่อนออกเมื่อออก
/// วิธีใช้: Create Empty ในฉาก (ชื่อ PTSD_Letterbox) → Add Component → PTSDLetterbox · ไม่ต้องลากอะไรใส่
///         สร้าง Canvas + แถบดำให้เอง วาดอยู่ "ใต้" HUD (หลอดเลือด/Hotbar/เป้า ยังเห็นปกติ)
/// ฟังสัญญาณ PTSDManager.OnPTSDStateChanged ตัวเดียวกับ PTSDVisualController — ไม่แก้ไฟล์ของนา
/// </summary>
[DisallowMultipleComponent]
public class PTSDLetterbox : MonoBehaviour
{
    [Tooltip("ความสูงแถบดำ 1 ข้าง เทียบกับความสูงจอ (0.12 = บน 12% + ล่าง 12%)")]
    [Range(0f, 0.3f)] public float barHeight = 0.12f;
    [Tooltip("เวลาเลื่อนเข้า/ออก (วินาที)")]
    [Min(0.01f)] public float slideSeconds = 1.2f;
    public Color barColor = Color.black;
    [Tooltip("ลำดับวาด — ต้องน้อยกว่า Canvas ของ HUD (UI_Manager = 0) เพื่อให้ HUD อยู่ทับแถบ")]
    public int sortingOrder = -10;

    private RectTransform top, bottom;
    private float current;   // 0 = ไม่มีแถบ · 1 = แถบเต็ม
    private float target;

    private void Awake() => Build();

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandlePtsd;
        // โหลดเซฟ/เริ่มฉากกลางโลก PTSD → โชว์แถบทันที ไม่ต้องรอสัญญาณ
        bool inPtsd = PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
        target = current = inPtsd ? 1f : 0f;
        Apply();
    }

    private void OnDisable() => PTSDManager.OnPTSDStateChanged -= HandlePtsd;

    private void HandlePtsd(bool active) => target = active ? 1f : 0f;

    private void Update()
    {
        if (Mathf.Approximately(current, target)) return;
        // เวลาจริง — เปิดสมุด/หยุดเกม (timeScale = 0) แถบก็ยังเลื่อนจบ
        current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime / slideSeconds);
        Apply();
    }

    private void Apply()
    {
        if (top == null) return;
        float e = current * current * (3f - 2f * current); // ช้า-เร็ว-ช้า ดูนุ่มแบบกล้องหนัง
        float h = barHeight * e;
        top.anchorMin = new Vector2(0f, 1f - h);
        bottom.anchorMax = new Vector2(1f, h);
        bool on = h > 0.0001f;
        if (top.gameObject.activeSelf != on) { top.gameObject.SetActive(on); bottom.gameObject.SetActive(on); }
    }

    private void Build()
    {
        GameObject canvasGo = new GameObject("PTSD_Letterbox_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        Canvas c = canvasGo.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;

        top = NewBar("Top", canvasGo.transform);
        top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f);
        bottom = NewBar("Bottom", canvasGo.transform);
        bottom.anchorMin = new Vector2(0f, 0f); bottom.anchorMax = new Vector2(1f, 0f);
    }

    private RectTransform NewBar(string n, Transform parent)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = barColor;
        img.raycastTarget = false;
        go.SetActive(false);
        return rt;
    }
}
