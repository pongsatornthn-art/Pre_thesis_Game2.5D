using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-25 นา] จอ "คุณตายแล้ว" — โชว์หลังตาย ให้ผู้เล่นเลือก
///   ปุ่มเล่นต่อ  → ทำตามกติกาตายเดิมของ DeathHandler (โลกปกติ = เซฟล่าสุด · PTSD = เริ่มโลก PTSD ใหม่จากทางเข้า)
///   ปุ่มกลับเมนู → ไปหน้าเมนูหลัก
///
/// DeathHandler เป็นคนเรียก Ask() — ตัวนี้ไม่รู้ว่าโหลดยังไง รู้แค่ "ผู้เล่นเลือกอะไร" (แยกหน้าที่ UI ออกจากกติกา)
///
/// วิธีทำในซีน (ของอยู่ในซีนทั้งหมด):
///   1. ใน Canvas สร้าง "DeathScreen" เต็มจอ แปะ CanvasGroup + UIPanelController + สคริปต์นี้
///   2. ลูก: พื้นดำโปร่ง (Image) · ข้อความหัว (TMP + LocalizedText key DEATH_TITLE)
///      · ปุ่ม 2 อัน (ข้อความ LocalizedText key DEATH_CONTINUE / DEATH_MAIN_MENU · แปะ UIButtonFX ได้เสียงเอง)
///   3. ลากปุ่มใส่ช่องด้านล่าง · ลาก "DeathScreen" ใส่ช่อง Death Screen ของ DeathHandler
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    public enum Choice { None, Continue, MainMenu }

    [SerializeField] private UIPanelController panel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button mainMenuButton;

    [Tooltip("รอกี่วิ (เวลาจริง) หลังจอขึ้น ก่อนกดปุ่มได้ — กันผู้เล่นกดพลาดตอนกำลังรัวปุ่มหนี")]
    [SerializeField, Min(0f)] private float buttonDelaySeconds = 1f;

    [Tooltip("ชื่อซีนเมนูหลัก (ต้องอยู่ใน Build Settings)")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    public string MainMenuScene => mainMenuScene;
    public bool IsOpen { get; private set; }

    private Choice choice;

    private void Awake()
    {
        if (panel == null) panel = GetComponent<UIPanelController>();
        if (continueButton != null) continueButton.onClick.AddListener(() => Pick(Choice.Continue));
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => Pick(Choice.MainMenu));
    }

    private void Start()
    {
        if (!IsOpen && panel != null) panel.HideImmediate();
    }

    private void Update()
    {
        // ESC ระหว่างจอตาย = ไม่ให้เมนูหยุดเกมเด้งซ้อน (PauseManager เช็คใน LateUpdate)
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) EscapeGuard.MarkHandled();
    }

    /// <summary>โชว์จอแล้วรอจนผู้เล่นเลือก — ผลอยู่ใน onChosen</summary>
    public IEnumerator Ask(System.Action<Choice> onChosen)
    {
        IsOpen = true;
        choice = Choice.None;

        // หยุดโลก (มอนเลิกตี/เดิน) + ปล่อยเมาส์ให้กดปุ่มได้
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetButtonsInteractable(false);
        if (panel != null) panel.Show(); else gameObject.SetActive(true);

        if (buttonDelaySeconds > 0f) yield return new WaitForSecondsRealtime(buttonDelaySeconds);
        SetButtonsInteractable(true);

        while (choice == Choice.None) yield return null;

        IsOpen = false;
        Time.timeScale = 1f;
        onChosen?.Invoke(choice);
    }

    private void Pick(Choice c)
    {
        if (!IsOpen) return;
        choice = c;
        SetButtonsInteractable(false);   // กันกดซ้ำระหว่างโหลด
    }

    private void SetButtonsInteractable(bool on)
    {
        if (continueButton != null) continueButton.interactable = on;
        if (mainMenuButton != null) mainMenuButton.interactable = on;
    }
}
