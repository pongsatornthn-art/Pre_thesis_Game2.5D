using System;
using System.Collections.Generic;
using UnityEngine;

public class LocalizationService : MonoBehaviour, ILocalizationService
{
    [Header("Fonts - Thai")]
    public TMPro.TMP_FontAsset thaiDefaultFont;
    public TMPro.TMP_FontAsset thaiHeaderFont;
    public TMPro.TMP_FontAsset thaiButtonFont;
    public TMPro.TMP_FontAsset thaiDialogFont;

    [Header("Fonts - English")]
    public TMPro.TMP_FontAsset engDefaultFont;
    public TMPro.TMP_FontAsset engHeaderFont;
    public TMPro.TMP_FontAsset engButtonFont;
    public TMPro.TMP_FontAsset engDialogFont;

    private Dictionary<string, Dictionary<string, string>> localizedText; // Key -> (Language -> Text)
    public string CurrentLanguage { get; private set; } = "Thai"; // ตั้งค่าเริ่มต้นเป็นภาษาไทย

    public event Action OnLanguageChanged;

    private void Awake()
    {
        // 1. ลงทะเบียนตัวเองให้เป็น Service กลาง
        ServiceLocator.Register<ILocalizationService>(this);
        
        // 2. โหลดไฟล์ CSV 
        LoadLocalizationData();
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<ILocalizationService>();
    }

    private void LoadLocalizationData()
    {
        localizedText = new Dictionary<string, Dictionary<string, string>>();
        
        // ต้องเอาไฟล์ CSV ไปใส่ในโฟลเดอร์ Resources ด้วยนะ
        TextAsset csvFile = Resources.Load<TextAsset>("LocalizationData");
        if (csvFile == null)
        {
            Debug.LogError("LocalizationService: ไม่พบไฟล์ Resources/LocalizationData.csv");
            return;
        }

        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1) return;

        // บรรทัดแรกคือ Header (เช่น Key;Thai;English)
        string[] headers = lines[0].Split(';');

        for (int i = 1; i < lines.Length; i++)
        {
            string[] row = lines[i].Split(';');
            if (row.Length < headers.Length) continue;

            string key = row[0].Trim(); // ป้องกันการเผลอเคาะสเปซบาร์ใน Excel
            var langDict = new Dictionary<string, string>();
            
            for (int j = 1; j < headers.Length; j++)
            {
                langDict[headers[j].Trim()] = row[j].Trim();
            }
            
            localizedText[key] = langDict;
        }
    }

    public void SetLanguage(string language)
    {
        CurrentLanguage = language;
        OnLanguageChanged?.Invoke(); // ตะโกนบอกทุก UI ว่า "เปลี่ยนภาษาแล้วนะ!"
    }

    public string GetText(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        
        key = key.Trim(); // ป้องกันคนพิมพ์สเปซบาร์แถมมาใน Inspector (เช่น "UI_BACK ")

        if (localizedText != null && localizedText.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(CurrentLanguage, out string text))
            {
                return text;
            }
        }
        
        Debug.LogWarning($"LocalizationService: ไม่พบคำแปลสำหรับ Key [{key}] ในภาษา [{CurrentLanguage}]");
        return key; // ถ้าหาไม่เจอ ให้คืนค่า Key กลับไป (จะได้รู้ว่าบั๊กตรงไหน)
    }

    public TMPro.TMP_FontAsset GetFont(FontCategory category)
    {
        bool isThai = CurrentLanguage == "Thai";
        switch (category)
        {
            case FontCategory.Header: return isThai ? thaiHeaderFont : engHeaderFont;
            case FontCategory.Button: return isThai ? thaiButtonFont : engButtonFont;
            case FontCategory.Dialog: return isThai ? thaiDialogFont : engDialogFont;
            case FontCategory.Default:
            default:
                return isThai ? thaiDefaultFont : engDefaultFont;
        }
    }
}
