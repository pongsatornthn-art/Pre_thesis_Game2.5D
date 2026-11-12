using UnityEngine;
using UnityEngine.Rendering;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("UI Menus")]
    public UIPanelController pauseMenu;
    public UIPanelController settingsMenu;

    [Header("Post Processing (Blur)")]
    [Tooltip("ลาก URP Volume ที่มี Depth of Field เบลอๆ มาใส่ตรงนี้")]
    public Volume blurVolume;

    public bool IsPaused { get; private set; }

    /// <summary>
    /// [Claude 2026-11-11] โหมดหน้าเมนูหลัก — ESC ไม่เปิดเมนูหยุดเกม (ใช้แค่ปิดหน้าตั้งค่า)
    /// ปิดหน้าตั้งค่าแล้วไม่เด้งเมนูหยุดเกม แต่ประกาศ SettingsClosed ให้เมนูหลักโชว์ปุ่มกลับมา
    /// </summary>
    public bool MenuMode { get; set; }
    public event System.Action SettingsClosed;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // ปิดให้หมดตอนเริ่มเกม และเซ็ต Blur เป็น 0
        if (pauseMenu != null) pauseMenu.HideImmediate();
        if (settingsMenu != null) settingsMenu.HideImmediate();
        if (blurVolume != null) blurVolume.weight = 0f;
    }

    // [แก้โดย Claude 2026-11-11 งานข้อ 3] ย้ายมา LateUpdate = ทำหลังทุก Update → รู้ก่อนว่าหน้าต่างอื่นใช้ ESC ปิดตัวเองไปหรือยัง
    private void LateUpdate()
    {
        // กด ESC สลับเปิด/ปิด Pause
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (MenuMode)
            {
                if (!EscapeGuard.HandledThisFrame && settingsMenu != null && settingsMenu.IsVisible) CloseSettings();
                return;
            }

            // สมุดเปิดอยู่ = ปล่อยให้สมุดจัดการ Esc เอง (ไม่งั้นเมนู Pause จะแวบขึ้นมา 1 เฟรม)
            if (JournalController.IsAnyOpen) return;

            // หน้าต่างอื่น (กระเป๋า/วิทยุ/ตู้เซฟ/กล่อง/มินิเกม) ใช้ ESC ปิดตัวเองไปแล้วในเฟรมนี้ → ไม่เปิดเมนูซ้อน
            if (EscapeGuard.HandledThisFrame) return;

            if (IsPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f; // หยุดเวลาของเกม
        
        if (blurVolume != null) blurVolume.weight = 1f; // เปิด Blur สุด
        
        // ถ้า settings เปิดค้างอยู่ ให้ปิด แล้วกลับไปโชว์หน้า Pause ปกติแทน
        if (settingsMenu != null && settingsMenu.IsVisible)
        {
            settingsMenu.HideImmediate();
        }
        
        if (pauseMenu != null) pauseMenu.Show(); // เด้งหน้า Pause ขึ้นมา
        
        Debug.Log("⏸️ เกมถูกหยุด");
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f; // เวลาเดินปกติ
        
        if (blurVolume != null) blurVolume.weight = 0f; // ปิด Blur

        if (pauseMenu != null) pauseMenu.Hide();
        if (settingsMenu != null) settingsMenu.Hide();
        
        Debug.Log("▶️ เล่นต่อ");
    }

    public void OpenSettings()
    {
        if (pauseMenu != null) pauseMenu.Hide();
        if (settingsMenu != null) settingsMenu.Show();
    }

    public void CloseSettings()
    {
        if (settingsMenu != null) settingsMenu.Hide();
        if (MenuMode)
        {
            SettingsClosed?.Invoke();
            return;
        }
        if (pauseMenu != null) pauseMenu.Show();
    }

    /// <summary>[Claude 2026-11-11] กลับหน้าเมนูหลัก (ความคืบหน้าที่ยังไม่เซฟหาย — ผู้เรียกถามยืนยันเอง)</summary>
    public void GoToMainMenu(string sceneName)
    {
        IsPaused = false;
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
    
    public void QuitGame()
    {
        Debug.Log("❌ ออกจากเกม!");
        Application.Quit();
    }
}
