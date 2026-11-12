using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สคริปต์นี้ใช้แปะที่ Panel ของ Pause Menu (หน้าแรก)
/// เอาไว้เชื่อมปุ่มหลักเข้ากับ PauseManager
/// [Claude 2026-11-11] เพิ่มปุ่ม "โหลดเกม" (เปิดสมุดเลือกช่อง · ถามก่อนโหลด) + "กลับเมนูหลัก" (กด 2 ครั้งเพื่อยืนยัน)
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button resumeBtn;
    public Button settingsBtn;
    public Button quitBtn;
    public Button loadBtn;
    public Button mainMenuBtn;

    [Header("กลับเมนูหลัก")]
    [Tooltip("ชื่อซีนเมนูหลัก (ต้องอยู่ใน Build Settings)")]
    public string mainMenuScene = "MainMenu";
    [Tooltip("ข้อความบนปุ่มตอนรอกดยืนยันครั้งที่ 2")]
    public string confirmAgainKey = "UI_CONFIRM_AGAIN";
    [Min(0.5f)] public float confirmWindowSeconds = 3f;

    private bool waitingConfirm;
    private Coroutine confirmRoutine;
    private string mainMenuKey;

    private void Start()
    {
        // ผูกปุ่มเข้ากับ Manager แบบอัตโนมัติ (สไตล์ OOP) โดยไม่ต้องไปตั้งค่าใน Inspector ทีละอัน
        if (resumeBtn != null)
            resumeBtn.onClick.AddListener(() => PauseManager.Instance.ResumeGame());

        if (settingsBtn != null)
            settingsBtn.onClick.AddListener(() => PauseManager.Instance.OpenSettings());

        if (quitBtn != null)
            quitBtn.onClick.AddListener(() => PauseManager.Instance.QuitGame());

        if (loadBtn != null)
            loadBtn.onClick.AddListener(OpenLoad);

        if (mainMenuBtn != null)
        {
            LocalizedText lt = mainMenuBtn.GetComponentInChildren<LocalizedText>(true);
            if (lt != null) mainMenuKey = lt.Key;
            mainMenuBtn.onClick.AddListener(OnMainMenuClicked);
        }
    }

    private void OnDisable()
    {
        ResetConfirm();
    }

    private void OpenLoad()
    {
        ISaveSlotLoader loader = ServiceLocator.GetOptional<ISaveSlotLoader>();
        if (loader == null)
        {
            ServiceLocator.GetOptional<IAudioService>()?.PlayMenuSound(MenuSoundType.Error);
            return;
        }
        loader.OpenLoadPicker(true, null);   // อยู่ในเกม → ถามก่อนโหลด
    }

    private void OnMainMenuClicked()
    {
        if (!waitingConfirm)
        {
            waitingConfirm = true;
            SetMainMenuLabel(confirmAgainKey);
            if (confirmRoutine != null) StopCoroutine(confirmRoutine);
            confirmRoutine = StartCoroutine(ConfirmTimeout());
            return;
        }
        ResetConfirm();
        PauseManager.Instance.GoToMainMenu(mainMenuScene);
    }

    private IEnumerator ConfirmTimeout()
    {
        yield return new WaitForSecondsRealtime(confirmWindowSeconds);
        ResetConfirm();
    }

    private void ResetConfirm()
    {
        waitingConfirm = false;
        if (confirmRoutine != null) { StopCoroutine(confirmRoutine); confirmRoutine = null; }
        if (!string.IsNullOrEmpty(mainMenuKey)) SetMainMenuLabel(mainMenuKey);
    }

    private void SetMainMenuLabel(string key)
    {
        if (mainMenuBtn == null || string.IsNullOrEmpty(key)) return;
        LocalizedText lt = mainMenuBtn.GetComponentInChildren<LocalizedText>(true);
        if (lt != null) lt.SetKey(key);
    }
}
