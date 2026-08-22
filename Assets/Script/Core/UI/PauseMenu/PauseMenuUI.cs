using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สคริปต์นี้ใช้แปะที่ Panel ของ Pause Menu (หน้าแรก)
/// เอาไว้เชื่อมปุ่ม 3 ปุ่มหลักเข้ากับ PauseManager
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button resumeBtn;
    public Button settingsBtn;
    public Button quitBtn;

    private void Start()
    {
        // ผูกปุ่มเข้ากับ Manager แบบอัตโนมัติ (สไตล์ OOP) โดยไม่ต้องไปตั้งค่าใน Inspector ทีละอัน
        if (resumeBtn != null) 
            resumeBtn.onClick.AddListener(() => PauseManager.Instance.ResumeGame());
            
        if (settingsBtn != null) 
            settingsBtn.onClick.AddListener(() => PauseManager.Instance.OpenSettings());
            
        if (quitBtn != null) 
            quitBtn.onClick.AddListener(() => PauseManager.Instance.QuitGame());
    }
}
