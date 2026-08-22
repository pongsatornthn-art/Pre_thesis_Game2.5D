using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUIController : MonoBehaviour
{
    [Header("Managers")]
    public SettingsManager settingsManager;

    [Header("UI Components (ปุ่มลูกศรซ้าย-ขวา)")]
    public SettingCycleUI languageCycle;

    [Header("UI Components (แถบสไลด์)")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider mouseSensitivitySlider;

    private void Start()
    {
        // ถ้าลืมลาก SettingsManager มาใส่ ให้สคริปต์วิ่งหาเองในฉาก
        if (settingsManager == null) settingsManager = Object.FindAnyObjectByType<SettingsManager>();

        if (settingsManager != null)
        {
            // === 1. จัดการภาษา (Language) ===
            if (languageCycle != null)
            {
                List<string> langs = new List<string> { "Thai", "English" };
                languageCycle.Setup(langs, settingsManager.GetCurrentLanguage());
                languageCycle.OnValueChanged += OnLanguageChanged;
            }

            // === 2. จัดการเสียง (Volume ใช้ Slider) ===
            // Slider ค่าปกติของ Unity คือ 0 ถึง 1 เราใช้ค่านี้อ้างอิงตรงๆ ได้เลย
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = PlayerPrefs.GetFloat("Game_Volume", 1.0f);
                masterVolumeSlider.onValueChanged.AddListener((val) => settingsManager.SaveAudioVolume(val));
            }
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.value = PlayerPrefs.GetFloat("Game_BGMVol", 1.0f);
                bgmVolumeSlider.onValueChanged.AddListener((val) => settingsManager.SaveBGMVolume(val));
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = PlayerPrefs.GetFloat("Game_SFXVol", 1.0f);
                sfxVolumeSlider.onValueChanged.AddListener((val) => settingsManager.SaveSFXVolume(val));
            }

            // === 3. จัดการเมาส์ (Sensitivity ใช้ Slider ค่า 1-10) ===
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.minValue = 1f;
                mouseSensitivitySlider.maxValue = 10f;
                mouseSensitivitySlider.value = PlayerPrefs.GetFloat("Game_MouseSens", 5.0f);
                mouseSensitivitySlider.onValueChanged.AddListener((val) => 
                {
                    PlayerPrefs.SetFloat("Game_MouseSens", val);
                    PlayerPrefs.Save();
                });
            }
        }
    }

    private void OnDestroy()
    {
        // ถอด Event ออกเพื่อป้องกัน Memory Leak
        if (languageCycle != null) languageCycle.OnValueChanged -= OnLanguageChanged;
        // (ส่วนตัวแปรอื่น เราใช้ Lambda Expression ตอน += ทำให้ไม่ต้องลบออกก็ได้ เพราะมันผูกติดกับก้อนนี้)
    }

    // ฟังก์ชันนี้จะทำงานทันทีทุกครั้งที่ผู้เล่นกดลูกศร < หรือ > 
    private void OnLanguageChanged(string newLang)
    {
        Debug.Log($"SettingsUI: ผู้เล่นเปลี่ยนภาษาเป็น {newLang}");
        settingsManager.SaveLanguage(newLang); // เซฟลงเครื่อง และสั่ง Service เปลี่ยนภาษาทันที!
    }
}
