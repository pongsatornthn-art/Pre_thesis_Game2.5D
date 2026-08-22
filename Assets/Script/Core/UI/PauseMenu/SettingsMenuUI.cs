using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สคริปต์นี้ใช้แปะที่ Panel ของ Settings Menu
/// ควบคุมเฉพาะปุ่ม Back ส่วนพวกหน้าจอเปลี่ยนภาษาหรืออื่นๆ จะคุมด้วย SettingsUIController 
/// </summary>
public class SettingsMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button backBtn;

    private void Start()
    {
        if (backBtn != null) 
            backBtn.onClick.AddListener(() => PauseManager.Instance.CloseSettings());
    }
}
