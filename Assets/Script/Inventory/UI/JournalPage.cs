using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ฐานของ "หนึ่งหน้าในสมุด" (กระเป๋า / ของสำคัญ / เอกสาร / อะไรก็ตามในอนาคต)
///
/// อยากเพิ่มหน้าใหม่ → สร้างคลาสใหม่สืบทอดตัวนี้ แล้วลากใส่ช่อง Pages ของ JournalController
/// **ไม่ต้องแก้ JournalController เลยสักบรรทัด** (นี่คือหลัก OCP)
///
/// ใช้ abstract class แทน interface ล้วน เพราะ Unity ลาก interface ใส่ช่อง Inspector ไม่ได้
/// แต่ลาก abstract MonoBehaviour ได้ → ได้ทั้ง polymorphism และต่อสายในซีนได้
/// </summary>
public abstract class JournalPage : MonoBehaviour
{
    [Header("Journal Page")]
    [Tooltip("ชื่อรหัสของหน้านี้ เช่น bag / key / note (ห้ามซ้ำกัน)")]
    [SerializeField] private string pageId = "page";

    [Tooltip("ปุ่มลัดที่กดแล้วเปิดหน้านี้ (I = กระเป๋า, K = ของสำคัญ, N = เอกสาร)")]
    [SerializeField] private KeyCode shortcutKey = KeyCode.None;

    [Tooltip("ตัวคุม UI ของหน้านี้ (ถ้ามี จะใช้ fade + scale แทนการ SetActive)")]
    [SerializeField] private UIPanelController pagePanel;

    [Tooltip("ก้อน UI ของหน้านี้ (จะถูกเปิด/ปิดเวลาสลับแท็บ — กรณีไม่มี UIPanelController)")]
    [SerializeField] private GameObject content;

    [Tooltip("ปุ่มแท็บของหน้านี้ (เว้นว่างได้ถ้าไม่มีแท็บ) — จะผูก onClick ให้อัตโนมัติ")]
    [SerializeField] private Button tabButton;

    [Tooltip("ภาพแท็บตอนถูกเลือกอยู่ (เว้นว่างได้)")]
    [SerializeField] private GameObject tabActiveHighlight;

    [Tooltip("ของบนพื้นหลังสมุดที่ใช้ร่วมกันทุกหน้า แต่หน้านี้ไม่อยากให้โชว์ (เช่น หน้าของสำคัญซ่อนรูป+ชื่อตัวละคร) — ปิดหน้านี้แล้วโผล่กลับเอง")]
    [SerializeField] private GameObject[] hideWhileOpen = new GameObject[0];

    [Tooltip("ของที่มีสคริปต์ทำงานอยู่ (เช่น แถบ Hotbar) — ซ่อนแบบโปร่งใสด้วย CanvasGroup ไม่ปิด object สคริปต์ของมันจึงไม่หลุดค่า")]
    [SerializeField] private CanvasGroup[] fadeWhileOpen = new CanvasGroup[0];

    private readonly System.Collections.Generic.Dictionary<CanvasGroup, (float alpha, bool raycasts)> faded = new();

    [Tooltip("รูปพื้นหลังสมุด (ใช้ร่วมกันทุกหน้า) — ลาก Image ของ BookBG มาใส่ ถ้าหน้านี้อยากใช้รูปพื้นหลังอีกแบบ")]
    [SerializeField] private Image sharedBackground;
    [Tooltip("รูปพื้นหลังตอนเปิดหน้านี้ (เว้นว่าง = ใช้รูปเดิม) — ปิดหน้านี้แล้วคืนรูปเดิมเอง")]
    [SerializeField] private Sprite backgroundWhileOpen;

    private Sprite originalBackground;
    private bool backgroundSwapped;

    public string PageId => pageId;
    public KeyCode ShortcutKey => shortcutKey;
    public Button TabButton => tabButton;

    /// <summary>เปิดหน้านี้ (JournalController เป็นคนเรียก)</summary>
    public virtual void Show()
    {
        if (pagePanel != null) pagePanel.Show();
        else if (content != null) content.SetActive(true);

        if (tabActiveHighlight != null) tabActiveHighlight.SetActive(true);
        SetSharedHidden(true);
        Refresh();
    }

    /// <summary>ปิดหน้านี้</summary>
    public virtual void Hide()
    {
        if (pagePanel != null) pagePanel.Hide();
        else if (content != null) content.SetActive(false);

        if (tabActiveHighlight != null) tabActiveHighlight.SetActive(false);
        SetSharedHidden(false);
    }

    /// <summary>ปิดหน้านี้ทันที (ใช้ตอนเริ่มเกม)</summary>
    public virtual void HideImmediate()
    {
        if (pagePanel != null) pagePanel.HideImmediate();
        else if (content != null) content.SetActive(false);

        if (tabActiveHighlight != null) tabActiveHighlight.SetActive(false);
        SetSharedHidden(false);
    }

    private void SetSharedHidden(bool hidden)
    {
        if (sharedBackground != null && backgroundWhileOpen != null)
        {
            if (hidden && !backgroundSwapped)
            {
                originalBackground = sharedBackground.sprite;
                sharedBackground.sprite = backgroundWhileOpen;
                backgroundSwapped = true;
            }
            else if (!hidden && backgroundSwapped)
            {
                sharedBackground.sprite = originalBackground;
                backgroundSwapped = false;
            }
        }

        foreach (GameObject go in hideWhileOpen) if (go != null) go.SetActive(!hidden);

        foreach (CanvasGroup cg in fadeWhileOpen)
        {
            if (cg == null) continue;
            if (hidden)
            {
                if (faded.ContainsKey(cg)) continue;   // ซ่อนอยู่แล้ว อย่าจำค่าทับ
                faded[cg] = (cg.alpha, cg.blocksRaycasts);
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
            }
            else if (faded.TryGetValue(cg, out var saved))
            {
                cg.alpha = saved.alpha;
                cg.blocksRaycasts = saved.raycasts;
                faded.Remove(cg);
            }
        }
    }

    /// <summary>กด Esc ตอนเปิดหน้านี้ — คืน true = หน้านี้จัดการเอง (เช่น ปิดหน้าอ่านเอกสาร) สมุดจะไม่ปิด</summary>
    public virtual bool HandleBack() => false;

    /// <summary>วาดข้อมูลใหม่ (เรียกตอนเปิดหน้า และตอนข้อมูลเปลี่ยน)</summary>
    public abstract void Refresh();
}
