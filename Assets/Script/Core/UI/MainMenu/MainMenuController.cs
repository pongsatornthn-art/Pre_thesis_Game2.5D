using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// หน้าเมนูหลัก (ซีน MainMenu) — เล่นต่อ / โหลดเกม / เริ่มเกมใหม่ / ตั้งค่า / ออกจากเกม
///
/// ใช้บริการกลางที่มีอยู่แล้วทั้งหมด (ซีนนี้มี [CORE_SERVICES] + SaveSlotPicker_Canvas):
/// - เล่นต่อ = SaveManager.LoadMostRecent() (ไม่มีเซฟ = ซ่อนปุ่ม)
/// - โหลดเกม = ISaveSlotLoader (สมุดเลือกช่อง หน้าเดียวกับหน้าเซฟ)
/// - ตั้งค่า = หน้าตั้งค่าของ PauseManager (เปิด MenuMode ไว้ → ESC ไม่เปิดเมนูหยุดเกม)
/// - เริ่มเกมใหม่ = มีเซฟอยู่แล้วถามก่อน (เซฟเก่าไม่ถูกลบ)
/// [Claude 2026-11-11]
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("ซีนเกม")]
    [Tooltip("ซีนที่เริ่มตอนกด 'เริ่มเกมใหม่' (ต้องอยู่ใน Build Settings)")]
    [SerializeField] private string newGameScene = "Scene_test02";

    [Header("ปุ่ม")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("แผง")]
    [Tooltip("ก้อนปุ่มทั้งหมด — ซ่อนตอนเปิดหน้าตั้งค่า")]
    [SerializeField] private UIPanelController menuPanel;
    [Tooltip("ถามก่อนเริ่มเกมใหม่ (ตอนมีเซฟอยู่แล้ว)")]
    [SerializeField] private UIPanelController newGameConfirm;
    [SerializeField] private Button newGameYes;
    [SerializeField] private Button newGameNo;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.MenuMode = true;
            PauseManager.Instance.SettingsClosed += OnSettingsClosed;
        }

        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (loadButton != null) loadButton.onClick.AddListener(OpenLoad);
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (newGameYes != null) newGameYes.onClick.AddListener(StartNewGame);
        if (newGameNo != null) newGameNo.onClick.AddListener(CancelNewGame);

        if (newGameConfirm != null) newGameConfirm.HideImmediate();
        RefreshButtons();
    }

    private void OnDestroy()
    {
        if (PauseManager.Instance != null) PauseManager.Instance.SettingsClosed -= OnSettingsClosed;
    }

    private void Update()
    {
        // ESC ปิดหน้าถามเริ่มเกมใหม่
        if (newGameConfirm != null && newGameConfirm.IsVisible && Input.GetKeyDown(KeyCode.Escape))
        {
            EscapeGuard.MarkHandled();
            CancelNewGame();
        }
    }

    private void RefreshButtons()
    {
        bool hasSave = Save() != null && Save().HasAnySave;
        if (continueButton != null) continueButton.gameObject.SetActive(hasSave);
        if (loadButton != null) loadButton.interactable = hasSave;
    }

    private void Continue()
    {
        SaveManager save = Save();
        if (save == null || !save.LoadMostRecent()) Error();
    }

    private void OpenLoad()
    {
        ISaveSlotLoader loader = ServiceLocator.GetOptional<ISaveSlotLoader>();
        if (loader == null) { Error(); return; }
        loader.OpenLoadPicker(false, null);   // อยู่เมนูหลัก ไม่มีอะไรจะหาย ไม่ต้องถาม
    }

    private void OnNewGameClicked()
    {
        SaveManager save = Save();
        if (save != null && save.HasAnySave && newGameConfirm != null)
        {
            newGameConfirm.Show();
            return;
        }
        StartNewGame();
    }

    private void CancelNewGame()
    {
        if (newGameConfirm != null) newGameConfirm.Hide();
        ServiceLocator.GetOptional<IAudioService>()?.PlayMenuSound(MenuSoundType.Back);
    }

    private void StartNewGame()
    {
        SaveManager.ResetForNewGame();
        SceneManager.LoadScene(newGameScene);
    }

    private void OpenSettings()
    {
        if (PauseManager.Instance == null) { Error(); return; }
        if (menuPanel != null) menuPanel.Hide();
        PauseManager.Instance.OpenSettings();
    }

    private void OnSettingsClosed()
    {
        if (menuPanel != null) menuPanel.Show();
    }

    private void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static SaveManager Save() => ServiceLocator.TryGet(out SaveManager s) ? s : null;
    private static void Error() => ServiceLocator.GetOptional<IAudioService>()?.PlayMenuSound(MenuSoundType.Error);
}
