using System;
using UnityEngine;

/// <summary>
/// ตัวคุมสมุดทั้งเล่ม — เปิด/ปิด, สลับแท็บ, ปุ่มลัด
///
/// ตัวนี้ **ไม่รู้จัก** ว่ามีหน้าอะไรบ้าง รู้แค่ว่ามี JournalPage หลายอัน
/// ปุ่มลัดของแต่ละหน้าไปตั้งเอาที่ตัวหน้าเอง (ช่อง Shortcut Key)
/// → เพิ่มหน้าใหม่ = สร้างคลาส + ลากใส่ Pages เท่านั้น ไม่ต้องแตะไฟล์นี้
/// </summary>
public class JournalController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ก้อนสมุดทั้งเล่ม (เปิด/ปิดทั้งก้อน)")]
    [SerializeField] private GameObject bookRoot;

    [Tooltip("ลากทุกหน้าใส่ที่นี่ — กระเป๋า / ของสำคัญ / เอกสาร")]
    [SerializeField] private JournalPage[] pages;

    [Header("Options")]
    [Tooltip("ปุ่มปิดสมุด")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Tooltip("หน้าที่เปิดเป็นค่าเริ่มต้นถ้าเรียกเปิดโดยไม่ระบุหน้า (เว้นว่าง = หน้าแรกใน Pages)")]
    [SerializeField] private string defaultPageId = "";

    [Tooltip("หยุดเวลาในเกมตอนเปิดสมุดไหม (ระวังชนกับระบบ Pause)")]
    [SerializeField] private bool pauseGameWhenOpen = false;

    /// <summary>สมุดเปิดอยู่ไหม — ให้ระบบอื่นเช็คได้ (เช่น กันยิงปืนตอนเปิดสมุด)</summary>
    public static bool IsAnyOpen { get; private set; }

    /// <summary>ยิงตอนเปิด/ปิดสมุด (true = เปิด) ให้ระบบอื่นมา subscribe</summary>
    public static event Action<bool> OnJournalToggled;

    public bool IsOpen => bookRoot != null && bookRoot.activeSelf;
    public string CurrentPageId { get; private set; }

    private void Start()
    {
        // ผูกปุ่มแท็บให้อัตโนมัติ ไม่ต้องไปลาก OnClick ทีละปุ่มใน Inspector
        if (pages != null)
        {
            foreach (JournalPage page in pages)
            {
                if (page == null || page.TabButton == null) continue;

                string id = page.PageId;   // คัดลอกไว้ก่อน ไม่งั้น closure จะจับตัวแปรลูป
                page.TabButton.onClick.AddListener(() => Open(id));
            }
        }

        CloseInstant();
    }

    private void OnDestroy()
    {
        if (IsOpen) IsAnyOpen = false;
    }

    private void Update()
    {
        if (pages != null)
        {
            foreach (JournalPage page in pages)
            {
                if (page == null || page.ShortcutKey == KeyCode.None) continue;
                if (!Input.GetKeyDown(page.ShortcutKey)) continue;

                // กดปุ่มของหน้าที่เปิดอยู่ = ปิดสมุด · กดปุ่มหน้าอื่น = สลับไปหน้านั้น
                if (IsOpen && CurrentPageId == page.PageId) Close();
                else Open(page.PageId);

                return;
            }
        }

        if (IsOpen && Input.GetKeyDown(closeKey)) Close();
    }

    private void LateUpdate()
    {
        // คุมเมาส์ใน LateUpdate เพื่อให้ทำงานทีหลังสคริปต์อื่นที่แย่งตั้งค่าเมาส์
        // (เช่น CrosshairController ที่ซ่อนเมาส์ตอนถือปืน)
        if (!IsOpen) return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>เปิดสมุดไปที่หน้าที่ระบุ</summary>
    public void Open(string pageId)
    {
        if (pages == null || pages.Length == 0) return;

        JournalPage target = FindPage(pageId) ?? FindPage(defaultPageId) ?? pages[0];
        if (target == null) return;

        if (bookRoot != null) bookRoot.SetActive(true);

        foreach (JournalPage page in pages)
        {
            if (page == null) continue;
            if (page == target) page.Show();
            else page.Hide();
        }

        CurrentPageId = target.PageId;

        if (!IsAnyOpen)
        {
            IsAnyOpen = true;
            ApplyPauseState(true);
            OnJournalToggled?.Invoke(true);
        }
    }

    /// <summary>เปิดสมุดหน้าเริ่มต้น</summary>
    public void Open() => Open(defaultPageId);

    /// <summary>ปิดสมุด</summary>
    public void Close()
    {
        if (!IsOpen) return;

        CloseInstant();
        OnJournalToggled?.Invoke(false);
    }

    /// <summary>ปิดแบบไม่ยิง event (ใช้ตอนเริ่มเกม)</summary>
    private void CloseInstant()
    {
        if (pages != null)
        {
            foreach (JournalPage page in pages)
            {
                if (page != null) page.Hide();
            }
        }

        if (bookRoot != null) bookRoot.SetActive(false);

        CurrentPageId = null;
        IsAnyOpen = false;
        ApplyPauseState(false);

        Cursor.lockState = CursorLockMode.Confined;
    }

    private void ApplyPauseState(bool isOpen)
    {
        if (!pauseGameWhenOpen) return;
        Time.timeScale = isOpen ? 0f : 1f;
    }

    private JournalPage FindPage(string pageId)
    {
        if (string.IsNullOrEmpty(pageId) || pages == null) return null;

        foreach (JournalPage page in pages)
        {
            if (page != null && page.PageId == pageId) return page;
        }
        return null;
    }
}
