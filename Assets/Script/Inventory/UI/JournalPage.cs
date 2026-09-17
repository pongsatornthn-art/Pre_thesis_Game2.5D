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

    public string PageId => pageId;
    public KeyCode ShortcutKey => shortcutKey;
    public Button TabButton => tabButton;

    /// <summary>เปิดหน้านี้ (JournalController เป็นคนเรียก)</summary>
    public virtual void Show()
    {
        if (pagePanel != null) pagePanel.Show();
        else if (content != null) content.SetActive(true);

        if (tabActiveHighlight != null) tabActiveHighlight.SetActive(true);
        Refresh();
    }

    /// <summary>ปิดหน้านี้</summary>
    public virtual void Hide()
    {
        if (pagePanel != null) pagePanel.Hide();
        else if (content != null) content.SetActive(false);

        if (tabActiveHighlight != null) tabActiveHighlight.SetActive(false);
    }

    /// <summary>ปิดหน้านี้ทันที (ใช้ตอนเริ่มเกม)</summary>
    public virtual void HideImmediate()
    {
        if (pagePanel != null) pagePanel.HideImmediate();
        else if (content != null) content.SetActive(false);

        if (tabActiveHighlight != null) tabActiveHighlight.SetActive(false);
    }

    /// <summary>วาดข้อมูลใหม่ (เรียกตอนเปิดหน้า และตอนข้อมูลเปลี่ยน)</summary>
    public abstract void Refresh();
}
