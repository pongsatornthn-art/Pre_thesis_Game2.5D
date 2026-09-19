using System;
using UnityEngine;

/// <summary>
/// ตัวคุมสมุดทั้งเล่ม — เปิด/ปิด, สลับแท็บ, ปุ่มลัด
///
/// ตัวนี้ **ไม่รู้จัก** ว่ามีหน้าอะไรบ้าง รู้แค่ว่ามี JournalPage หลายอัน
/// ปุ่มลัดของแต่ละหน้าไปตั้งเอาที่ตัวหน้าเอง (ช่อง Shortcut Key)
/// → เพิ่มหน้าใหม่ = สร้างคลาส + ลากใส่ Pages เท่านั้น ไม่ต้องแตะไฟล์นี้
///
/// เชื่อมต่อกับบริการกลาง:
/// - AudioService: เล่นเสียงเปิด/ปิด/เปลี่ยนหน้า
/// - UIPanelController: ทำแอนิเมชัน Fade + Scale ตอนเปิด/ปิด
/// - PauseManager: หยุดเกมตอนเปิดสมุด และไม่ตีกันเรื่องปุ่ม Esc หรือเมาส์
/// </summary>
public class JournalController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ตัวคุมพาเนลสมุดทั้งเล่ม (มี Fade + Scale) — ต้องมี CanvasGroup")]
    [SerializeField] private UIPanelController bookPanel;

    [Tooltip("ก้อนสมุดทั้งเล่ม (สำรองกรณีไม่ได้ใช้ UIPanelController)")]
    [SerializeField] private GameObject bookRoot;

    [Tooltip("ลากทุกหน้าใส่ที่นี่ — กระเป๋า / ของสำคัญ / เอกสาร")]
    [SerializeField] private JournalPage[] pages;

    [Header("Options")]
    [Tooltip("ปุ่มปิดสมุด")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Tooltip("หน้าที่เปิดเป็นค่าเริ่มต้นถ้าเรียกเปิดโดยไม่ระบุหน้า (เว้นว่าง = หน้าแรกใน Pages)")]
    [SerializeField] private string defaultPageId = "";

    [Tooltip("หยุดเวลาในเกมตอนเปิดสมุดไหม (แนะนำเปิดไว้ ไม่งั้นเปิดอ่านโน้ตอยู่ผีเดินมากัดได้)")]
    [SerializeField] private bool pauseGameWhenOpen = true;

    /// <summary>สมุดเปิดอยู่ไหม — ให้ระบบอื่นเช็คได้ (เช่น กันยิงปืนตอนเปิดสมุด)</summary>
    public static bool IsAnyOpen { get; private set; }

    /// <summary>ยิงตอนเปิด/ปิดสมุด (true = เปิด) ให้ระบบอื่นมา subscribe</summary>
    public static event Action<bool> OnJournalToggled;

    public bool IsOpen => bookPanel != null ? bookPanel.IsVisible : (bookRoot != null && bookRoot.activeSelf);
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
        // 1. ถ้าเกมติด Pause อยู่ และสมุดไม่ได้เปิดอยู่ -> ไม่อนุญาตให้เปิดสมุด
        if (!IsOpen && PauseManager.Instance != null && PauseManager.Instance.IsPaused)
        {
            return;
        }

        // 2. ดักปุ่มลัดของแต่ละหน้า (I, K, N)
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

        // 3. ดักปุ่ม Esc เพื่อปิดสมุด
        //    PauseManager.Update() เช็ค JournalController.IsAnyOpen แล้วปล่อยผ่านให้เรา
        //    จึงไม่ต้องตามเช็ด ResumeGame() ทีหลังอีก
        if (IsOpen && Input.GetKeyDown(closeKey)) Close();
    }

    /// <summary>เปิดสมุดไปที่หน้าที่ระบุ</summary>
    public void Open(string pageId)
    {
        if (pages == null || pages.Length == 0) return;

        JournalPage target = FindPage(pageId) ?? FindPage(defaultPageId) ?? pages[0];
        if (target == null) return;

        // อยู่หน้านี้อยู่แล้ว = ไม่ต้องทำอะไรเลย
        // ไม่งั้นกดแท็บเดิมซ้ำ ๆ หน้าจะเด้งแอนิเมชันใหม่ทุกครั้ง เสียงคลิกดังรัว
        // และหน้ากุญแจ/เอกสารจะลบช่องรายการทิ้งแล้วสร้างใหม่ทั้งหมดทุกครั้งที่กด (เปลืองและกระพริบ)
        if (IsOpen && CurrentPageId == target.PageId) return;

        bool wasAlreadyOpen = IsOpen;

        // เปิดพาเนลหลัก
        if (bookPanel != null) bookPanel.Show();
        else if (bookRoot != null) bookRoot.SetActive(true);

        // สลับหน้าข้างใน
        foreach (JournalPage page in pages)
        {
            if (page == null) continue;
            if (page == target) page.Show();
            else page.Hide();
        }

        CurrentPageId = target.PageId;

        // คุมเมาส์และเสียง
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        ServiceLocator.Get<IAudioService>()?.PlayMenuSound(MenuSoundType.Click);

        if (!wasAlreadyOpen)
        {
            IsAnyOpen = true;
            ApplyPauseState(true);
            OnJournalToggled?.Invoke(true);
        }
    }

    /// <summary>หยุด/คืนเวลาเกมตอนเปิดปิดสมุด — ไม่ยุ่งถ้าเกมติดหน้า Pause อยู่</summary>
    private void ApplyPauseState(bool isOpening)
    {
        if (!pauseGameWhenOpen) return;

        // เกมติดหน้าจอ Pause อยู่ = ปล่อยให้ PauseManager คุมเวลาเอง อย่าไปแย่ง
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        Time.timeScale = isOpening ? 0f : 1f;
    }

    /// <summary>เปิดสมุดหน้าเริ่มต้น</summary>
    public void Open() => Open(defaultPageId);

    /// <summary>ปิดสมุด</summary>
    public void Close()
    {
        if (!IsOpen) return;

        if (pages != null)
        {
            foreach (JournalPage page in pages)
            {
                if (page != null) page.Hide();
            }
        }

        if (bookPanel != null) bookPanel.Hide();
        else if (bookRoot != null) bookRoot.SetActive(false);

        CurrentPageId = null;
        IsAnyOpen = false;
        ApplyPauseState(false);

        Cursor.lockState = CursorLockMode.Confined;

        ServiceLocator.Get<IAudioService>()?.PlayMenuSound(MenuSoundType.Back);
        OnJournalToggled?.Invoke(false);
    }

    /// <summary>ปิดแบบทันทีไม่ยิง event (ใช้ตอนเริ่มเกม)</summary>
    private void CloseInstant()
    {
        if (pages != null)
        {
            foreach (JournalPage page in pages)
            {
                if (page != null) page.HideImmediate();
            }
        }

        if (bookPanel != null) bookPanel.HideImmediate();
        else if (bookRoot != null) bookRoot.SetActive(false);

        CurrentPageId = null;
        IsAnyOpen = false;
        ApplyPauseState(false);

        Cursor.lockState = CursorLockMode.Confined;
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
