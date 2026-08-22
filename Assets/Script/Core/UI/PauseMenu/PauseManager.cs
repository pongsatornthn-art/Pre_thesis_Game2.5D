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

    private void Update()
    {
        // กด ESC สลับเปิด/ปิด Pause
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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
        if (pauseMenu != null) pauseMenu.Show();
    }
    
    public void QuitGame()
    {
        Debug.Log("❌ ออกจากเกม!");
        Application.Quit();
    }
}
