using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-25 นา] จอ "จบเดโม" — จอดำค่อยๆ ขึ้น → ข้อความจบ/ขอบคุณ → ปุ่มกลับเมนูหลัก
/// เรียกจากฉากเนื้อเรื่องด้วยคำสั่ง "เกม/จบเดโม" (EndDemoAction) — ตัวนี้ไม่รู้ว่าใครเรียก
///
/// วิธีทำในซีน (ของอยู่ในซีนทั้งหมด):
///   1. ใน Canvas สร้าง "DemoEndScreen" เต็มจอ แปะ CanvasGroup + UIPanelController (ปิด Use Scale Popup) + สคริปต์นี้
///   2. ลูก: พื้นดำทึบ (Image) · หัวข้อ (TMP + LocalizedText key DEMO_END_TITLE) · ข้อความรอง (key DEMO_END_THANKS)
///      · ปุ่มกลับเมนู (key DEMO_END_MENU · แปะ UIButtonFX) — ลากใส่ช่อง Main Menu Button
///   3. ทางเข้าห้องสุดท้าย: StoryTriggerZone (ครั้งเดียว) → ฉาก Seq_DemoEnd ที่มีคำสั่ง "เกม/จบเดโม"
/// </summary>
public class DemoEndScreen : MonoBehaviour
{
    [SerializeField] private UIPanelController panel;
    [SerializeField] private Button mainMenuButton;

    [Tooltip("ความเร็ว fade จอดำ (ยิ่งน้อยยิ่งช้า) — ใช้แทนค่าใน UIPanelController ตอนขึ้นจอนี้")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 1.2f;

    [Tooltip("รอกี่วิ (เวลาจริง) หลังจอขึ้น ก่อนโชว์ปุ่มกลับเมนู")]
    [SerializeField, Min(0f)] private float buttonDelaySeconds = 2.5f;

    [Tooltip("ชื่อซีนเมนูหลัก (ต้องอยู่ใน Build Settings)")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    public static DemoEndScreen Instance { get; private set; }
    public bool IsShowing { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (panel == null) panel = GetComponent<UIPanelController>();
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (!IsShowing && panel != null) panel.HideImmediate();
    }

    private void Update()
    {
        // จบเดโมแล้ว ESC ต้องไม่เปิดเมนูหยุดเกม
        if (IsShowing && Input.GetKeyDown(KeyCode.Escape)) EscapeGuard.MarkHandled();
    }

    /// <summary>ขึ้นจอจบเดโม (เรียกซ้ำได้ ไม่ขึ้นซ้อน)</summary>
    public void Show()
    {
        if (IsShowing) return;
        IsShowing = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);   // ต้องเปิดก่อนถึงเริ่ม coroutine ได้
        StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(false);

        if (panel != null)
        {
            panel.fadeSpeed = fadeSpeed;
            panel.useScalePopup = false;
            panel.Show();
        }
        else gameObject.SetActive(true);

        // รอจอดำขึ้นก่อน แล้วค่อยหยุดโลก (หยุดก่อน = ภาพค้างกลางท่าเดิน ดูเหมือนเกมค้าง)
        yield return new WaitForSecondsRealtime(1f / fadeSpeed + 0.1f);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (buttonDelaySeconds > 0f) yield return new WaitForSecondsRealtime(buttonDelaySeconds);
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(true);
    }

    private void GoToMainMenu()
    {
        if (mainMenuButton != null) mainMenuButton.interactable = false;
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }
}
